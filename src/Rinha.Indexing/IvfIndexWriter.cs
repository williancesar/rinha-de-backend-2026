// src/Rinha.Indexing/IvfIndexWriter.cs
using System.Buffers.Binary;

namespace Rinha.Indexing;

/// <summary>
/// Escreve um index.bin completo a partir de dados em memória.
/// Os vetores e labels devem JÁ ESTAR ordenados por cluster (responsabilidade do caller).
/// </summary>
public static class IvfIndexWriter
{
    public static void Write(
        Stream output,
        int n,
        int k,
        int d,
        ReadOnlySpan<float> centroids,            // k * d floats
        ReadOnlySpan<int> postingOffsets,         // k+1 ints (sentinel no final = n)
        ReadOnlySpan<sbyte> vectors,              // n * VectorStride bytes (já paddados)
        ReadOnlySpan<byte> labels)                // n bytes
    {
        if (centroids.Length != k * d) throw new ArgumentException(nameof(centroids));
        if (postingOffsets.Length != k + 1) throw new ArgumentException(nameof(postingOffsets));
        if (vectors.Length != n * IndexFileLayout.VectorStride) throw new ArgumentException(nameof(vectors));
        if (labels.Length != n) throw new ArgumentException(nameof(labels));

        var centroidsOff = IndexFileLayout.HeaderSize;
        var centroidsSize = k * d * sizeof(float);
        var postingOff = centroidsOff + centroidsSize;
        var postingSize = (k + 1) * sizeof(int);
        var vectorsOff = IndexFileLayout.AlignUp(postingOff + postingSize, IndexFileLayout.CentroidsAlignment);
        var vectorsSize = n * IndexFileLayout.VectorStride;
        var labelsOff = vectorsOff + vectorsSize;

        // Header
        Span<byte> header = stackalloc byte[IndexFileLayout.HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffMagic..], IndexFileLayout.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffVersion..], IndexFileLayout.Version);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffN..], (uint)n);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffK..], (uint)k);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffD..], (uint)d);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffVecStride..], (uint)IndexFileLayout.VectorStride);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffCentroidsOffset..], (uint)centroidsOff);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffPostingOffset..], (uint)postingOff);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffVectorsOffset..], (uint)vectorsOff);
        BinaryPrimitives.WriteUInt32LittleEndian(header[IndexFileLayout.OffLabelsOffset..], (uint)labelsOff);
        BinaryPrimitives.WriteSingleLittleEndian(header[IndexFileLayout.OffQuantScale..], IndexFileLayout.QuantScale);
        output.Write(header);

        // Centroides
        Span<byte> floatBuf = stackalloc byte[4];
        foreach (var f in centroids)
        {
            BinaryPrimitives.WriteSingleLittleEndian(floatBuf, f);
            output.Write(floatBuf);
        }

        // Posting offsets
        Span<byte> intBuf = stackalloc byte[4];
        foreach (var i in postingOffsets)
        {
            BinaryPrimitives.WriteInt32LittleEndian(intBuf, i);
            output.Write(intBuf);
        }

        // Padding até alinhamento
        var padding = vectorsOff - (postingOff + postingSize);
        Span<byte> pad = stackalloc byte[padding];
        pad.Clear();
        output.Write(pad);

        // Vetores (sbyte → byte cast)
        unsafe
        {
            fixed (sbyte* p = vectors)
            {
                var span = new ReadOnlySpan<byte>(p, vectors.Length);
                output.Write(span);
            }
        }

        // Labels
        output.Write(labels);
    }
}
