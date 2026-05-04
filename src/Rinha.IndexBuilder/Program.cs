// src/Rinha.IndexBuilder/Program.cs
using System.Diagnostics;
using Rinha.IndexBuilder;
using Rinha.Indexing;

if (args.Length < 4 || args[0] != "--in" || args[2] != "--out")
{
    Console.Error.WriteLine("usage: Rinha.IndexBuilder --in <references.json.gz> --out <index.bin> [--k 256] [--iters 25] [--seed 42]");
    return 1;
}

var inputPath = args[1];
var outputPath = args[3];
var k = 256;
var iters = 25;
var seed = 42;
for (var i = 4; i < args.Length - 1; i += 2)
{
    switch (args[i])
    {
        case "--k": k = int.Parse(args[i + 1]); break;
        case "--iters": iters = int.Parse(args[i + 1]); break;
        case "--seed": seed = int.Parse(args[i + 1]); break;
    }
}

var sw = Stopwatch.StartNew();
Console.WriteLine($"[builder] reading {inputPath}");
var (vectorsFloat, labels, n) = await ReferencesReader.ReadAsync(inputPath);
var d = IndexFileLayout.VectorDim;
Console.WriteLine($"[builder] N={n}, D={d}, K={k} (read in {sw.Elapsed.TotalSeconds:F1}s)");

sw.Restart();
Console.WriteLine($"[builder] running k-means (iters={iters}, seed={seed})");
var (centroids, assignments) = KMeans.Run(vectorsFloat, n, d, k, iters, seed);
Console.WriteLine($"[builder] k-means done in {sw.Elapsed.TotalSeconds:F1}s");

sw.Restart();
Console.WriteLine($"[builder] reordering by cluster + quantizing");

var clusterCounts = new int[k];
foreach (var a in assignments) clusterCounts[a]++;
var postingOffsets = new int[k + 1];
for (var c = 0; c < k; c++) postingOffsets[c + 1] = postingOffsets[c] + clusterCounts[c];

var vectorsByCluster = new sbyte[n * IndexFileLayout.VectorStride];
var labelsByCluster = new byte[n];
var cursor = new int[k];
Array.Copy(postingOffsets, cursor, k);
for (var i = 0; i < n; i++)
{
    var c = assignments[i];
    var pos = cursor[c]++;
    labelsByCluster[pos] = labels[i];
    Span<sbyte> dst = vectorsByCluster.AsSpan(pos * IndexFileLayout.VectorStride, IndexFileLayout.VectorStride);
    dst.Clear();
    Quantization.QuantizeVector(vectorsFloat.AsSpan(i * d, d), dst[..d]);
}
Console.WriteLine($"[builder] reorder+quantize in {sw.Elapsed.TotalSeconds:F1}s");

sw.Restart();
Console.WriteLine($"[builder] writing {outputPath}");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await using (var fs = File.Create(outputPath))
{
    IvfIndexWriter.Write(fs, n, k, d, centroids, postingOffsets, vectorsByCluster, labelsByCluster);
}
var fileLen = new FileInfo(outputPath).Length;
Console.WriteLine($"[builder] wrote {fileLen / (1024.0 * 1024.0):F1} MB in {sw.Elapsed.TotalSeconds:F1}s");

return 0;
