// src/Rinha.Indexing/IvfIndexView.cs
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Rinha.Indexing;

/// <summary>
/// View read-only sobre um buffer mmap'ado contendo o index.bin.
/// Não copia: todas as regiões são spans sobre o buffer subjacente.
/// </summary>
public readonly ref struct IvfIndexView
{
    private readonly ReadOnlySpan<byte> _bytes;
    public int N { get; }
    public int K { get; }
    public int D { get; }
    public int VectorStride { get; }
    public float QuantScale { get; }
    public int CentroidsOffset { get; }
    public int PostingOffset { get; }
    public int VectorsOffset { get; }
    public int LabelsOffset { get; }

    public IvfIndexView(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < IndexFileLayout.HeaderSize)
            throw new ArgumentException($"buffer too small (got {bytes.Length}, need ≥{IndexFileLayout.HeaderSize})");

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffMagic..]);
        if (magic != IndexFileLayout.Magic)
            throw new InvalidDataException($"bad magic: expected 0x{IndexFileLayout.Magic:X8}, got 0x{magic:X8}");

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffVersion..]);
        if (version != IndexFileLayout.Version)
            throw new InvalidDataException($"unsupported version: {version}");

        N = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffN..]);
        K = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffK..]);
        D = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffD..]);
        VectorStride = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffVecStride..]);
        CentroidsOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffCentroidsOffset..]);
        PostingOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffPostingOffset..]);
        VectorsOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffVectorsOffset..]);
        LabelsOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[IndexFileLayout.OffLabelsOffset..]);
        QuantScale = BinaryPrimitives.ReadSingleLittleEndian(bytes[IndexFileLayout.OffQuantScale..]);
        _bytes = bytes;
    }

    /// <summary>K × D floats (centroides em float32).</summary>
    public ReadOnlySpan<float> Centroids =>
        MemoryMarshal.Cast<byte, float>(_bytes.Slice(CentroidsOffset, K * D * sizeof(float)));

    /// <summary>(K+1) × int32 (offsets CSR para vetores).</summary>
    public ReadOnlySpan<int> PostingOffsets =>
        MemoryMarshal.Cast<byte, int>(_bytes.Slice(PostingOffset, (K + 1) * sizeof(int)));

    /// <summary>N × VectorStride bytes (vetores int8 padded).</summary>
    public ReadOnlySpan<sbyte> Vectors =>
        MemoryMarshal.Cast<byte, sbyte>(_bytes.Slice(VectorsOffset, N * VectorStride));

    /// <summary>N labels (1 byte cada: 0=legit, 1=fraud).</summary>
    public ReadOnlySpan<byte> Labels =>
        _bytes.Slice(LabelsOffset, N);

    /// <summary>Retorna o slice de vetores do cluster <paramref name="clusterIdx"/>.</summary>
    public ReadOnlySpan<sbyte> ClusterVectors(int clusterIdx)
    {
        var start = PostingOffsets[clusterIdx];
        var end = PostingOffsets[clusterIdx + 1];
        return Vectors.Slice(start * VectorStride, (end - start) * VectorStride);
    }

    /// <summary>Retorna o slice de labels do cluster <paramref name="clusterIdx"/>.</summary>
    public ReadOnlySpan<byte> ClusterLabels(int clusterIdx)
    {
        var start = PostingOffsets[clusterIdx];
        var end = PostingOffsets[clusterIdx + 1];
        return Labels.Slice(start, end - start);
    }
}
