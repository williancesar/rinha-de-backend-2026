// tests/Rinha.Tests/QuantizationTests.cs
using Rinha.Indexing;
using Xunit;

namespace Rinha.Tests;

public class QuantizationTests
{
    [Fact]
    public void Sentinel_Negative_One_Quantizes_To_Negative_127()
    {
        Assert.Equal((sbyte)-127, Quantization.QuantizeFloat(-1f));
    }

    [Fact]
    public void Zero_Quantizes_To_Zero()
    {
        Assert.Equal((sbyte)0, Quantization.QuantizeFloat(0f));
    }

    [Fact]
    public void One_Quantizes_To_127()
    {
        Assert.Equal((sbyte)127, Quantization.QuantizeFloat(1f));
    }

    [Fact]
    public void Above_One_Clamps_To_127()
    {
        Assert.Equal((sbyte)127, Quantization.QuantizeFloat(1.5f));
        Assert.Equal((sbyte)127, Quantization.QuantizeFloat(100f));
    }

    [Fact]
    public void Below_Negative_One_Clamps_To_Negative_127()
    {
        Assert.Equal((sbyte)-127, Quantization.QuantizeFloat(-2f));
    }

    [Theory]
    [InlineData(0.5f, 64)]   // round(0.5 * 127) = round(63.5) = 64
    [InlineData(0.25f, 32)]  // round(0.25 * 127) = round(31.75) = 32
    [InlineData(0.1f, 13)]   // round(0.1 * 127) = round(12.7) = 13
    public void Quantizes_Within_Range_With_Rounding(float input, sbyte expected)
    {
        Assert.Equal(expected, Quantization.QuantizeFloat(input));
    }

    [Fact]
    public void QuantizeVector_Writes_All_Dims()
    {
        Span<float> input = stackalloc float[14] { 0f, 0.5f, 1f, -1f, 0.25f, 0.1f, 0.75f, -1f, 1f, 0f, 1f, 0f, 0.5f, 0.0083f };
        Span<sbyte> output = stackalloc sbyte[14];
        Quantization.QuantizeVector(input, output);

        Assert.Equal((sbyte)0, output[0]);
        Assert.Equal((sbyte)64, output[1]);
        Assert.Equal((sbyte)127, output[2]);
        Assert.Equal((sbyte)-127, output[3]);
        Assert.Equal((sbyte)32, output[4]);
        Assert.Equal((sbyte)13, output[5]);
        Assert.Equal((sbyte)95, output[6]);     // round(0.75 * 127) = 95
        Assert.Equal((sbyte)-127, output[7]);
        Assert.Equal((sbyte)127, output[8]);
        Assert.Equal((sbyte)0, output[9]);
        Assert.Equal((sbyte)127, output[10]);
        Assert.Equal((sbyte)0, output[11]);
        Assert.Equal((sbyte)64, output[12]);
        Assert.Equal((sbyte)1, output[13]);     // round(0.0083 * 127) = round(1.054) = 1
    }
}
