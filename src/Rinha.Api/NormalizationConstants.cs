// src/Rinha.Api/NormalizationConstants.cs
namespace Rinha.Api;

/// <summary>
/// Constantes de resources/normalization.json. Hardcoded porque são fixos durante todo o teste
/// e simplifica AOT (sem deserialization dinâmica no startup).
/// </summary>
public static class NormalizationConstants
{
    public const float MaxAmount = 10_000f;
    public const float MaxInstallments = 12f;
    public const float AmountVsAvgRatio = 10f;
    public const float MaxMinutes = 1_440f;
    public const float MaxKm = 1_000f;
    public const float MaxTxCount24h = 20f;
    public const float MaxMerchantAvgAmount = 10_000f;

    /// <summary>Sentinel para campos derivados de last_transaction quando esta é null.</summary>
    public const float SentinelMissing = -1f;
}
