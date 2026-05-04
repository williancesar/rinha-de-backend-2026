// src/Rinha.Api/Program.cs
using System.IO.MemoryMappedFiles;
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Rinha.Api;
using Rinha.Indexing;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);

var port = int.TryParse(Environment.GetEnvironmentVariable("HTTP_PORT"), out var hp) ? hp : 8080;

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.AllowSynchronousIO = false;
    o.Limits.MaxConcurrentConnections = 1024;
    o.ListenAnyIP(port, l => l.Protocols = HttpProtocols.Http1);
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});

// Index path: configurável via env (default /app/data/index.bin no container)
var indexPath = Environment.GetEnvironmentVariable("INDEX_PATH") ?? "/app/data/index.bin";
var mccPath = Environment.GetEnvironmentVariable("MCC_RISK_PATH") ?? "/app/data/mcc_risk.json";
var nprobe = int.TryParse(Environment.GetEnvironmentVariable("NPROBE"), out var np) ? np : 8;

// State (singleton)
var ready = new ReadyState();
builder.Services.AddSingleton(ready);

var app = builder.Build();

// Warmup async — /ready returns 503 until done
_ = Task.Run(() =>
{
    try
    {
        app.Logger.LogWarning("loading mcc_risk from {Path}", mccPath);
        var mccTable = McCRiskTable.Load(mccPath);

        app.Logger.LogWarning("opening index from {Path}", indexPath);
        var mmf = MemoryMappedFile.CreateFromFile(indexPath, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.Read);
        var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        // Force page-in + 100 synthetic warmup queries using a TEMPORARY local pointer
        // (MarkReady is called AFTER warmup so /ready is not set until JIT hot path is done)
        unsafe
        {
            byte* localPtr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref localPtr);
            try
            {
                // Force page-in
                long sum = 0;
                var len = accessor.Capacity;
                for (long i = 0; i < len; i += 4096) sum += localPtr[i];
                _ = sum; // prevent dead code elimination

                // 100 synthetic queries using the LOCAL pointer
                var rng = new Random(0);
                Span<float> qf = stackalloc float[14];
                Span<sbyte> qq = stackalloc sbyte[14];
                for (var i = 0; i < 100; i++)
                {
                    for (var j = 0; j < 14; j++) qf[j] = (float)rng.NextDouble();
                    Quantization.QuantizeVector(qf, qq);
                    var span = new ReadOnlySpan<byte>(localPtr, (int)len);
                    var view = new IvfIndexView(span);
                    _ = IvfSearch.Search(view, qq, qf, nprobe);
                }
            }
            finally
            {
                accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            }
        }

        // Now mark ready — MarkReady will AcquirePointer internally for the long-lived ptr.
        ready.MarkReady(mccTable, mmf, accessor, nprobe);
        app.Logger.LogWarning("ready");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "warmup failed");
        Environment.Exit(2);
    }
});

app.MapGet("/ready", (ReadyState s) => s.IsReady ? Results.Ok() : Results.StatusCode(503));

app.MapPost("/fraud-score", async (HttpContext ctx, ReadyState s) =>
{
    if (!s.IsReady) { ctx.Response.StatusCode = 503; return; }

    FraudRequest? req;
    try
    {
        req = await JsonSerializer.DeserializeAsync(ctx.Request.Body, JsonContext.Default.FraudRequest);
    }
    catch
    {
        ctx.Response.StatusCode = 400;
        return;
    }
    if (req is null) { ctx.Response.StatusCode = 400; return; }

    Span<float> qf = stackalloc float[14];
    PayloadVectorizer.Vectorize(
        amount: req.Transaction.Amount,
        installments: req.Transaction.Installments,
        requestedAt: ParseUtc(req.Transaction.RequestedAt),
        customerAvgAmount: req.Customer.AvgAmount,
        txCount24h: req.Customer.TxCount24h,
        customerKnownMerchants: req.Customer.KnownMerchants,
        merchantId: req.Merchant.Id,
        merchantMcc: req.Merchant.Mcc,
        merchantAvgAmount: req.Merchant.AvgAmount,
        isOnline: req.Terminal.IsOnline,
        cardPresent: req.Terminal.CardPresent,
        kmFromHome: req.Terminal.KmFromHome,
        lastTransactionTimestamp: req.LastTransaction is null ? null : ParseUtc(req.LastTransaction.Timestamp),
        kmFromLastTransaction: req.LastTransaction?.KmFromCurrent,
        mccRisk: s.McCTable!,
        output: qf);

    Span<sbyte> qq = stackalloc sbyte[14];
    Quantization.QuantizeVector(qf, qq);

    var view = ReadIndex(s);
    // Guard nprobe ≤ K (T7 review fold-in)
    var probe = s.NProbe > view.K ? view.K : s.NProbe;
    var fraudScore = IvfSearch.Search(view, qq, qf, probe);

    var resp = new FraudResponse(Approved: fraudScore < 0.6f, FraudScore: fraudScore);
    ctx.Response.ContentType = "application/json";
    ctx.Response.StatusCode = 200;
    await JsonSerializer.SerializeAsync(ctx.Response.Body, resp, JsonContext.Default.FraudResponse);
});

app.Run();

static unsafe IvfIndexView ReadIndex(ReadyState s)
{
    var span = new ReadOnlySpan<byte>(s.IndexPtr, (int)s.IndexLen);
    return new IvfIndexView(span);
}

static DateTime ParseUtc(string iso)
{
    return DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal).UtcDateTime;
}

namespace Rinha.Api
{
    public sealed class ReadyState
    {
        private volatile bool _ready;
        public McCRiskTable? McCTable { get; private set; }
        public MemoryMappedFile? Mmf { get; private set; }
        public MemoryMappedViewAccessor? Accessor { get; private set; }
        public unsafe byte* IndexPtr { get; private set; }
        public long IndexLen { get; private set; }
        public int NProbe { get; private set; }
        public bool IsReady => _ready;

        public unsafe void MarkReady(McCRiskTable mcc, MemoryMappedFile mmf, MemoryMappedViewAccessor acc, int nprobe)
        {
            McCTable = mcc; Mmf = mmf; Accessor = acc; NProbe = nprobe;
            byte* ptr = null;
            acc.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr); // intentionally never released: pointer held for process lifetime
            IndexPtr = ptr;
            IndexLen = acc.Capacity;
            _ready = true;
        }
    }
}
