// tests/Rinha.Tests/IndexFileLayoutTests.cs
using Rinha.Indexing;
using Xunit;

namespace Rinha.Tests;

public class IndexFileLayoutTests
{
    [Fact]
    public void Magic_Is_RFI1_LittleEndian()
    {
        // "RFI1" como bytes ASCII em little-endian: 'R'=0x52, 'F'=0x46, 'I'=0x49, '1'=0x31
        // u32 LE: 0x31494652
        Assert.Equal(0x31494652u, IndexFileLayout.Magic);
    }

    [Fact]
    public void HeaderSize_Is_64()
    {
        Assert.Equal(64, IndexFileLayout.HeaderSize);
    }

    [Fact]
    public void VectorStride_Is_16()
    {
        Assert.Equal(16, IndexFileLayout.VectorStride);
    }

    [Fact]
    public void Dim_Is_14()
    {
        Assert.Equal(14, IndexFileLayout.VectorDim);
    }

    [Fact]
    public void QuantScale_Is_127()
    {
        Assert.Equal(127f, IndexFileLayout.QuantScale);
    }
}
