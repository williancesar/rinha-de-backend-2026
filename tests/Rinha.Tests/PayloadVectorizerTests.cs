// tests/Rinha.Tests/PayloadVectorizerTests.cs
using Rinha.Indexing;
using Xunit;

namespace Rinha.Tests;

public class PayloadVectorizerTests
{
    [Fact]
    public void Canonical_Legit_Example_From_DetectionRules_Md()
    {
        // Input do exemplo do host:
        //   tx 41.12, installments=2, requested_at=2026-03-11T18:45:53Z (Wednesday, hora 18)
        //   customer.avg_amount=82.24, tx_count_24h=3, known_merchants=["MERC-003","MERC-016"]
        //   merchant.id=MERC-016, mcc=5411, avg_amount=60.25
        //   terminal.is_online=false, card_present=true, km_from_home=29.23
        //   last_transaction=null
        //
        // Vetor esperado:
        // [0.0041, 0.1667, 0.05, 0.7826, 0.3333, -1, -1, 0.0292, 0.15, 0, 1, 0, 0.15, 0.006]

        var mcc = new TestMcCRisk(("5411", 0.15f));
        Span<float> v = stackalloc float[14];
        PayloadVectorizer.Vectorize(
            amount: 41.12,
            installments: 2,
            requestedAt: DateTimeOffset.Parse("2026-03-11T18:45:53Z").UtcDateTime,
            customerAvgAmount: 82.24,
            txCount24h: 3,
            customerKnownMerchants: new[] { "MERC-003", "MERC-016" },
            merchantId: "MERC-016",
            merchantMcc: "5411",
            merchantAvgAmount: 60.25,
            isOnline: false,
            cardPresent: true,
            kmFromHome: 29.23,
            lastTransactionTimestamp: null,
            kmFromLastTransaction: null,
            mccRisk: mcc,
            output: v);

        Assert.Equal(0.0041f, v[0], 4);
        Assert.Equal(0.1667f, v[1], 4);
        Assert.Equal(0.05f, v[2], 4);
        Assert.Equal(0.7826f, v[3], 4);
        Assert.Equal(0.3333f, v[4], 4);
        Assert.Equal(-1f, v[5]);
        Assert.Equal(-1f, v[6]);
        Assert.Equal(0.0292f, v[7], 4);
        Assert.Equal(0.15f, v[8], 4);
        Assert.Equal(0f, v[9]);
        Assert.Equal(1f, v[10]);
        Assert.Equal(0f, v[11]);
        Assert.Equal(0.15f, v[12], 4);
        Assert.Equal(0.006f, v[13], 3);
    }

    [Fact]
    public void Canonical_Fraud_Example_From_DetectionRules_Md()
    {
        // Input do segundo exemplo do host:
        //   tx 9505.97, installments=10, requested_at=2026-03-14T05:15:12Z (Saturday, hora 5)
        //   customer.avg_amount=81.28, tx_count_24h=20, known_merchants=["MERC-008","MERC-007","MERC-005"]
        //   merchant.id=MERC-068, mcc=7802, avg_amount=54.86
        //   terminal.is_online=false, card_present=true, km_from_home=952.27
        //   last_transaction=null
        //
        // Vetor esperado:
        // [0.9506, 0.8333, 1.0, 0.2174, 0.8333, -1, -1, 0.9523, 1.0, 0, 1, 1, 0.75, 0.0055]

        var mcc = new TestMcCRisk(("7802", 0.75f));
        Span<float> v = stackalloc float[14];
        PayloadVectorizer.Vectorize(
            amount: 9505.97,
            installments: 10,
            requestedAt: DateTimeOffset.Parse("2026-03-14T05:15:12Z").UtcDateTime,
            customerAvgAmount: 81.28,
            txCount24h: 20,
            customerKnownMerchants: new[] { "MERC-008", "MERC-007", "MERC-005" },
            merchantId: "MERC-068",
            merchantMcc: "7802",
            merchantAvgAmount: 54.86,
            isOnline: false,
            cardPresent: true,
            kmFromHome: 952.27,
            lastTransactionTimestamp: null,
            kmFromLastTransaction: null,
            mccRisk: mcc,
            output: v);

        Assert.Equal(0.9506f, v[0], 4);
        Assert.Equal(0.8333f, v[1], 4);
        Assert.Equal(1.0f, v[2], 4);
        Assert.Equal(0.2174f, v[3], 4);
        Assert.Equal(0.8333f, v[4], 4);
        Assert.Equal(-1f, v[5]);
        Assert.Equal(-1f, v[6]);
        Assert.Equal(0.9523f, v[7], 4);
        Assert.Equal(1.0f, v[8], 4);
        Assert.Equal(0f, v[9]);
        Assert.Equal(1f, v[10]);
        Assert.Equal(1f, v[11]);
        Assert.Equal(0.75f, v[12], 4);
        Assert.Equal(0.0055f, v[13], 4);
    }

    /// <summary>Stub de IMcCRiskLookup para os tests.</summary>
    private sealed class TestMcCRisk : IMcCRiskLookup
    {
        private readonly Dictionary<string, float> _map;
        public TestMcCRisk(params (string mcc, float risk)[] entries)
        {
            _map = entries.ToDictionary(e => e.mcc, e => e.risk);
        }
        public float Get(string mcc) => _map.TryGetValue(mcc, out var r) ? r : 0.5f;
    }
}
