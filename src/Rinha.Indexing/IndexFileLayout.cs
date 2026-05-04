// src/Rinha.Indexing/IndexFileLayout.cs
namespace Rinha.Indexing;

public static class IndexFileLayout
{
    /// <summary>"RFI1" em ASCII LE → 0x31494652. Falha rápida se layout incompatível.</summary>
    public const uint Magic = 0x31494652u;
    public const uint Version = 1u;
    public const int HeaderSize = 64;
    public const int VectorDim = 14;
    public const int VectorStride = 16; // 14 int8 + 2 bytes padding (alinhamento SIMD)
    public const float QuantScale = 127f;
    public const int CentroidsAlignment = 64;

    // Offsets dentro do header (64 bytes)
    public const int OffMagic = 0;
    public const int OffVersion = 4;
    public const int OffN = 8;
    public const int OffK = 12;
    public const int OffD = 16;
    public const int OffVecStride = 20;
    public const int OffCentroidsOffset = 24;
    public const int OffPostingOffset = 28;
    public const int OffVectorsOffset = 32;
    public const int OffLabelsOffset = 36;
    public const int OffQuantScale = 40;

    public const byte LabelLegit = 0;
    public const byte LabelFraud = 1;

    /// <summary>Arredonda <paramref name="value"/> para o próximo múltiplo de <paramref name="alignment"/>.</summary>
    public static int AlignUp(int value, int alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }
}
