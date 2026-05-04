// src/Rinha.Indexing/IvfSearch.cs
namespace Rinha.Indexing;

public static class IvfSearch
{
    public const int K = 5; // top-5 fixo (host spec)

    /// <summary>
    /// Busca os 5 vizinhos mais próximos da query e retorna fraud_score = #frauds / 5.
    /// </summary>
    /// <param name="view">Index mmap'ado.</param>
    /// <param name="queryQuantized">Vetor query quantizado em int8, length = D.</param>
    /// <param name="queryFloat">Vetor query em float, length = D (para distância aos centroides).</param>
    /// <param name="nprobe">Número de clusters mais próximos a escanear.</param>
    /// <returns>fraud_score em [0, 1].</returns>
    public static float Search(
        IvfIndexView view,
        ReadOnlySpan<sbyte> queryQuantized,
        ReadOnlySpan<float> queryFloat,
        int nprobe)
    {
        if (queryFloat.Length != view.D) throw new ArgumentException(nameof(queryFloat));
        if (queryQuantized.Length != view.D) throw new ArgumentException(nameof(queryQuantized));

        // Fase 1: top-nprobe clusters por distância L2² da query (float) aos centroides (float).
        Span<int> topClusters = stackalloc int[nprobe];
        Span<float> topClusterDists = stackalloc float[nprobe];
        for (var i = 0; i < nprobe; i++) topClusterDists[i] = float.PositiveInfinity;

        var centroids = view.Centroids;
        for (var c = 0; c < view.K; c++)
        {
            var d = L2SquaredFloat(queryFloat, centroids.Slice(c * view.D, view.D));
            // insere em heap-array de tamanho fixo nprobe (max-heap implícito pelo maior elemento na frente)
            InsertSmaller(topClusters, topClusterDists, nprobe, c, d);
        }

        // Fase 2: scan dos clusters selecionados, mantendo top-K candidatos por L2² em int32.
        Span<int> topResultIdx = stackalloc int[K];
        Span<int> topResultDist = stackalloc int[K];
        for (var i = 0; i < K; i++) topResultDist[i] = int.MaxValue;

        var allLabels = view.Labels;
        for (var p = 0; p < nprobe; p++)
        {
            var clusterIdx = topClusters[p];
            var start = view.PostingOffsets[clusterIdx];
            var end = view.PostingOffsets[clusterIdx + 1];
            var clusterVecs = view.Vectors.Slice(start * view.VectorStride, (end - start) * view.VectorStride);

            for (var i = 0; i < end - start; i++)
            {
                var refVec = clusterVecs.Slice(i * view.VectorStride, view.D);
                var dist = L2SquaredInt8(queryQuantized, refVec);
                if (dist < topResultDist[K - 1]) // pior atual
                {
                    var globalIdx = start + i;
                    InsertSmaller(topResultIdx, topResultDist, K, globalIdx, dist);
                }
            }
        }

        // Conta frauds entre os K finalistas
        var fraudCount = 0;
        for (var i = 0; i < K; i++)
        {
            if (topResultDist[i] == int.MaxValue) continue; // nprobe pequeno demais
            if (allLabels[topResultIdx[i]] == IndexFileLayout.LabelFraud)
                fraudCount++;
        }

        return fraudCount / (float)K;
    }

    private static float L2SquaredFloat(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var sum = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return sum;
    }

    private static int L2SquaredInt8(ReadOnlySpan<sbyte> a, ReadOnlySpan<sbyte> b)
    {
        var sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return sum;
    }

    /// <summary>
    /// Mantém um array de tamanho <paramref name="size"/> com os menores valores observados.
    /// Insere (idx, dist) se dist < pior_atual; o pior fica no índice <c>size-1</c>.
    /// Estrutura: ordenação simples por inserção. OK para size pequeno (5..16).
    /// </summary>
    private static void InsertSmaller(Span<int> idxs, Span<int> dists, int size, int newIdx, int newDist)
    {
        if (newDist >= dists[size - 1]) return;
        var pos = size - 1;
        while (pos > 0 && dists[pos - 1] > newDist)
        {
            dists[pos] = dists[pos - 1];
            idxs[pos] = idxs[pos - 1];
            pos--;
        }
        dists[pos] = newDist;
        idxs[pos] = newIdx;
    }

    private static void InsertSmaller(Span<int> idxs, Span<float> dists, int size, int newIdx, float newDist)
    {
        if (newDist >= dists[size - 1]) return;
        var pos = size - 1;
        while (pos > 0 && dists[pos - 1] > newDist)
        {
            dists[pos] = dists[pos - 1];
            idxs[pos] = idxs[pos - 1];
            pos--;
        }
        dists[pos] = newDist;
        idxs[pos] = newIdx;
    }
}
