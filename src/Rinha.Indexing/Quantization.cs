// src/Rinha.Indexing/Quantization.cs
using System.Runtime.CompilerServices;

namespace Rinha.Indexing;

/// <summary>
/// Quantização simétrica float ↔ int8 com escala 127.
/// Faixa válida do float: [-1, 1]. Sentinel -1 é preservado exatamente em -127.
/// Valores fora de [-1, 1] são clampados.
/// </summary>
public static class Quantization
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte QuantizeFloat(float value)
    {
        // Clamp to [-1, 1] then scale to [-127, 127]
        // MathF.Round usa banker's rounding; aqui queremos arredondamento half-away-from-zero
        // para casar com a expectativa de "round(0.5 * 127) = 64".
        if (value <= -1f) return -127;
        if (value >= 1f) return 127;
        var scaled = value * IndexFileLayout.QuantScale;
        // half-away-from-zero
        return (sbyte)(scaled >= 0f ? scaled + 0.5f : scaled - 0.5f);
    }

    public static void QuantizeVector(ReadOnlySpan<float> input, Span<sbyte> output)
    {
        if (input.Length != output.Length)
            throw new ArgumentException("input and output spans must have the same length");
        for (var i = 0; i < input.Length; i++)
            output[i] = QuantizeFloat(input[i]);
    }
}
