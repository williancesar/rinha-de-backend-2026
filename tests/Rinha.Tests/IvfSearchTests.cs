// tests/Rinha.Tests/IvfSearchTests.cs
using Rinha.Indexing;
using Xunit;

namespace Rinha.Tests;

public class IvfSearchTests
{
    private const int D = 14;

    [Fact]
    public void Search_With_All_Clusters_Probed_Matches_BruteForce()
    {
        // Dataset sintético: 1000 vetores aleatórios em [0,1]^14, K=8 clusters via assignment aleatório.
        // Quando nprobe=K, IVF deve concordar 100% com brute force exato.
        const int n = 1000;
        const int k = 8;
        var rng = new Random(42);
        var vectorsFloat = GenerateRandomVectors(n, D, rng);
        var labels = GenerateRandomLabels(n, rng);

        var (centroids, assignments) = AssignToRandomCentroids(vectorsFloat, k, rng);
        var (postingOffsets, vectorsByCluster, labelsByCluster, originalIndices) =
            ReorderByCluster(vectorsFloat, labels, assignments, k, n, D);

        var indexBytes = WriteIndexToMemory(n, k, D, centroids, postingOffsets, vectorsByCluster, labelsByCluster);
        var view = new IvfIndexView(indexBytes);

        // Para 20 queries aleatórias, comparar IVF (nprobe=K) com brute force.
        var matches = 0;
        for (var q = 0; q < 20; q++)
        {
            Span<float> query = stackalloc float[D];
            for (var i = 0; i < D; i++) query[i] = (float)rng.NextDouble();
            Span<sbyte> queryQ = stackalloc sbyte[D];
            Quantization.QuantizeVector(query, queryQ);

            var ivfScore = IvfSearch.Search(view, queryQ, query, nprobe: k);
            var bruteScore = BruteForceFraudScore(query, vectorsFloat, labels, n);

            if (Math.Abs(ivfScore - bruteScore) < 0.21f) matches++; // mesmo veredito = mesma fração
        }

        Assert.True(matches >= 18, $"matches: {matches}/20"); // 90% concordância mínima
    }

    [Fact]
    public void Search_Returns_FraudScore_Between_0_And_1()
    {
        var rng = new Random(7);
        var vectors = GenerateRandomVectors(100, D, rng);
        var labels = GenerateRandomLabels(100, rng);
        var (centroids, assignments) = AssignToRandomCentroids(vectors, 4, rng);
        var (po, vc, lc, _) = ReorderByCluster(vectors, labels, assignments, 4, 100, D);
        var bytes = WriteIndexToMemory(100, 4, D, centroids, po, vc, lc);
        var view = new IvfIndexView(bytes);

        Span<float> query = stackalloc float[D];
        for (var i = 0; i < D; i++) query[i] = 0.5f;
        Span<sbyte> queryQ = stackalloc sbyte[D];
        Quantization.QuantizeVector(query, queryQ);

        var score = IvfSearch.Search(view, queryQ, query, nprobe: 2);
        Assert.InRange(score, 0f, 1f);
    }

    // --- helpers ---

    private static float[][] GenerateRandomVectors(int n, int d, Random rng)
    {
        var v = new float[n][];
        for (var i = 0; i < n; i++)
        {
            v[i] = new float[d];
            for (var j = 0; j < d; j++) v[i][j] = (float)rng.NextDouble();
        }
        return v;
    }

    private static byte[] GenerateRandomLabels(int n, Random rng)
    {
        var labels = new byte[n];
        for (var i = 0; i < n; i++) labels[i] = (byte)(rng.Next(2));
        return labels;
    }

    private static (float[] centroids, int[] assignments) AssignToRandomCentroids(float[][] vectors, int k, Random rng)
    {
        var centroids = new float[k * D];
        for (var i = 0; i < k * D; i++) centroids[i] = (float)rng.NextDouble();

        var assignments = new int[vectors.Length];
        for (var i = 0; i < vectors.Length; i++)
        {
            var bestC = 0; var bestD = float.PositiveInfinity;
            for (var c = 0; c < k; c++)
            {
                var d = 0f;
                for (var j = 0; j < D; j++)
                {
                    var diff = vectors[i][j] - centroids[c * D + j];
                    d += diff * diff;
                }
                if (d < bestD) { bestD = d; bestC = c; }
            }
            assignments[i] = bestC;
        }
        return (centroids, assignments);
    }

    private static (int[] postingOffsets, sbyte[] vectorsByCluster, byte[] labelsByCluster, int[] originalIndices)
        ReorderByCluster(float[][] vectors, byte[] labels, int[] assignments, int k, int n, int d)
    {
        var clusterCounts = new int[k];
        foreach (var a in assignments) clusterCounts[a]++;

        var postingOffsets = new int[k + 1];
        for (var c = 0; c < k; c++) postingOffsets[c + 1] = postingOffsets[c] + clusterCounts[c];

        var vectorsByCluster = new sbyte[n * IndexFileLayout.VectorStride];
        var labelsByCluster = new byte[n];
        var originalIndices = new int[n];
        var cursor = new int[k];
        Array.Copy(postingOffsets, cursor, k);

        for (var i = 0; i < n; i++)
        {
            var c = assignments[i];
            var pos = cursor[c]++;
            originalIndices[pos] = i;
            labelsByCluster[pos] = labels[i];
            Span<sbyte> dst = vectorsByCluster.AsSpan(pos * IndexFileLayout.VectorStride, IndexFileLayout.VectorStride);
            dst.Clear();
            Span<sbyte> dq = dst[..d];
            Quantization.QuantizeVector(vectors[i], dq);
        }

        return (postingOffsets, vectorsByCluster, labelsByCluster, originalIndices);
    }

    private static byte[] WriteIndexToMemory(
        int n, int k, int d,
        float[] centroids, int[] postingOffsets, sbyte[] vectorsByCluster, byte[] labelsByCluster)
    {
        using var ms = new MemoryStream();
        IvfIndexWriter.Write(ms, n, k, d, centroids, postingOffsets, vectorsByCluster, labelsByCluster);
        return ms.ToArray();
    }

    private static float BruteForceFraudScore(ReadOnlySpan<float> query, float[][] vectors, byte[] labels, int n)
    {
        var dists = new (float dist, int idx)[n];
        for (var i = 0; i < n; i++)
        {
            var d = 0f;
            for (var j = 0; j < D; j++)
            {
                var diff = query[j] - vectors[i][j];
                d += diff * diff;
            }
            dists[i] = (d, i);
        }
        Array.Sort(dists, (a, b) => a.dist.CompareTo(b.dist));
        var fraudCount = 0;
        for (var i = 0; i < 5; i++)
            if (labels[dists[i].idx] == 1) fraudCount++;
        return fraudCount / 5f;
    }
}
