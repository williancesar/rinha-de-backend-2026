// src/Rinha.IndexBuilder/KMeans.cs
namespace Rinha.IndexBuilder;

public static class KMeans
{
    /// <summary>
    /// Lloyd's k-means. Inicialização: k-means++. Paralelo na fase de assignment.
    /// </summary>
    /// <param name="data">N * D floats em layout row-major.</param>
    public static (float[] centroids, int[] assignments) Run(
        float[] data, int n, int d, int k,
        int maxIters = 25,
        int seed = 42,
        float tol = 1e-4f)
    {
        var rng = new Random(seed);
        var centroids = InitializeKMeansPlusPlus(data, n, d, k, rng);
        var assignments = new int[n];

        for (var iter = 0; iter < maxIters; iter++)
        {
            // Assignment phase (paralelo em N)
            var changed = AssignParallel(data, n, d, k, centroids, assignments);

            // Update phase (sequencial, k pequeno)
            var newCentroids = new float[k * d];
            var counts = new int[k];
            for (var i = 0; i < n; i++)
            {
                var c = assignments[i];
                counts[c]++;
                for (var j = 0; j < d; j++)
                    newCentroids[c * d + j] += data[i * d + j];
            }
            for (var c = 0; c < k; c++)
            {
                if (counts[c] == 0)
                {
                    // Cluster vazio: re-seed aleatoriamente
                    var randomIdx = rng.Next(n);
                    for (var j = 0; j < d; j++)
                        newCentroids[c * d + j] = data[randomIdx * d + j];
                    continue;
                }
                for (var j = 0; j < d; j++)
                    newCentroids[c * d + j] /= counts[c];
            }

            // Convergência
            var movement = 0f;
            for (var i = 0; i < k * d; i++)
            {
                var diff = newCentroids[i] - centroids[i];
                movement += diff * diff;
            }
            centroids = newCentroids;
            if (movement < tol || changed == 0) break;
        }

        return (centroids, assignments);
    }

    private static float[] InitializeKMeansPlusPlus(float[] data, int n, int d, int k, Random rng)
    {
        var centroids = new float[k * d];
        // Primeiro centroide aleatório
        var firstIdx = rng.Next(n);
        for (var j = 0; j < d; j++) centroids[j] = data[firstIdx * d + j];

        var minDistSq = new float[n];
        Array.Fill(minDistSq, float.PositiveInfinity);

        for (var c = 1; c < k; c++)
        {
            // Atualiza distância de cada ponto ao centroide mais próximo já escolhido
            for (var i = 0; i < n; i++)
            {
                var dist = 0f;
                for (var j = 0; j < d; j++)
                {
                    var diff = data[i * d + j] - centroids[(c - 1) * d + j];
                    dist += diff * diff;
                }
                if (dist < minDistSq[i]) minDistSq[i] = dist;
            }

            // Sample weighted by minDistSq
            var sum = 0d;
            for (var i = 0; i < n; i++) sum += minDistSq[i];
            var threshold = rng.NextDouble() * sum;
            var acc = 0d;
            var pickedIdx = n - 1;
            for (var i = 0; i < n; i++)
            {
                acc += minDistSq[i];
                if (acc >= threshold) { pickedIdx = i; break; }
            }
            for (var j = 0; j < d; j++) centroids[c * d + j] = data[pickedIdx * d + j];
        }

        return centroids;
    }

    private static int AssignParallel(float[] data, int n, int d, int k, float[] centroids, int[] assignments)
    {
        var changed = 0;
        Parallel.For(0, n, () => 0, (i, state, localChanged) =>
        {
            var bestC = 0; var bestD = float.PositiveInfinity;
            for (var c = 0; c < k; c++)
            {
                var dist = 0f;
                for (var j = 0; j < d; j++)
                {
                    var diff = data[i * d + j] - centroids[c * d + j];
                    dist += diff * diff;
                }
                if (dist < bestD) { bestD = dist; bestC = c; }
            }
            if (assignments[i] != bestC) { assignments[i] = bestC; localChanged++; }
            return localChanged;
        }, lc => Interlocked.Add(ref changed, lc));
        return changed;
    }
}
