// tests/Rinha.Tests/KMeansTests.cs
using Rinha.IndexBuilder;
using Xunit;

namespace Rinha.Tests;

public class KMeansTests
{
    [Fact]
    public void Converges_On_Two_Well_Separated_Clusters_2D()
    {
        // 100 pontos em volta de (0,0), 100 pontos em volta de (10,10). K=2.
        var n = 200; var d = 2; var k = 2;
        var data = new float[n * d];
        var rng = new Random(0);
        for (var i = 0; i < 100; i++)
        {
            data[i * 2 + 0] = (float)(rng.NextDouble() * 0.5);
            data[i * 2 + 1] = (float)(rng.NextDouble() * 0.5);
        }
        for (var i = 100; i < 200; i++)
        {
            data[i * 2 + 0] = 10f + (float)(rng.NextDouble() * 0.5);
            data[i * 2 + 1] = 10f + (float)(rng.NextDouble() * 0.5);
        }

        var (centroids, assignments) = KMeans.Run(data, n, d, k, maxIters: 50, seed: 1);

        Assert.Equal(k * d, centroids.Length);
        // Verifica: pontos do primeiro grupo estão num cluster e do segundo grupo no outro.
        var firstGroupCluster = assignments[0];
        var secondGroupCluster = assignments[150];
        Assert.NotEqual(firstGroupCluster, secondGroupCluster);
        for (var i = 0; i < 100; i++)
            Assert.Equal(firstGroupCluster, assignments[i]);
        for (var i = 100; i < 200; i++)
            Assert.Equal(secondGroupCluster, assignments[i]);
    }
}
