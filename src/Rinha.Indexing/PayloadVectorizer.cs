// src/Rinha.Indexing/PayloadVectorizer.cs
namespace Rinha.Indexing;

public interface IMcCRiskLookup
{
    float Get(string mcc);
}

/// <summary>
/// Constantes de normalização (replicadas aqui para a lib não depender da Api).
/// Devem casar com resources/normalization.json.
/// NOTA: NormalizationConstants em Rinha.Api deve ter os mesmos valores. Se algum mudar, atualizar ambos.
/// </summary>
public static class NormalizationDefaults
{
    public const float MaxAmount = 10_000f;
    public const float MaxInstallments = 12f;
    public const float AmountVsAvgRatio = 10f;
    public const float MaxMinutes = 1_440f;
    public const float MaxKm = 1_000f;
    public const float MaxTxCount24h = 20f;
    public const float MaxMerchantAvgAmount = 10_000f;
    public const float SentinelMissing = -1f;
}

public static class PayloadVectorizer
{
    /// <summary>
    /// Transforma campos brutos do payload em vetor de 14 floats normalizados.
    /// Output deve ter Length = 14.
    /// Sentinel -1 nas posições 5 e 6 quando lastTransactionTimestamp é null.
    /// </summary>
    public static void Vectorize(
        double amount, int installments, DateTime requestedAt,
        double customerAvgAmount, int txCount24h, string[] customerKnownMerchants,
        string merchantId, string merchantMcc, double merchantAvgAmount,
        bool isOnline, bool cardPresent, double kmFromHome,
        DateTime? lastTransactionTimestamp, double? kmFromLastTransaction,
        IMcCRiskLookup mccRisk,
        Span<float> output)
    {
        if (output.Length != IndexFileLayout.VectorDim)
            throw new ArgumentException($"output must have length {IndexFileLayout.VectorDim}");

        // 0: amount
        output[0] = Clamp01((float)(amount / NormalizationDefaults.MaxAmount));
        // 1: installments
        output[1] = Clamp01(installments / NormalizationDefaults.MaxInstallments);
        // 2: amount_vs_avg
        var ratio = customerAvgAmount > 0
            ? (float)((amount / customerAvgAmount) / NormalizationDefaults.AmountVsAvgRatio)
            : 0f;
        output[2] = Clamp01(ratio);
        // 3: hour_of_day (0..23 / 23)
        output[3] = requestedAt.Hour / 23f;
        // 4: day_of_week (mon=0..sun=6) / 6
        output[4] = MapDayOfWeekMon0(requestedAt.DayOfWeek) / 6f;

        // 5,6: minutes_since_last_tx, km_from_last_tx (sentinel -1 se null)
        if (lastTransactionTimestamp.HasValue)
        {
            var minutes = (float)(requestedAt - lastTransactionTimestamp.Value).TotalMinutes;
            output[5] = Clamp01(minutes / NormalizationDefaults.MaxMinutes);
            output[6] = Clamp01((float)(kmFromLastTransaction ?? 0d) / NormalizationDefaults.MaxKm);
        }
        else
        {
            output[5] = NormalizationDefaults.SentinelMissing;
            output[6] = NormalizationDefaults.SentinelMissing;
        }

        // 7: km_from_home
        output[7] = Clamp01((float)kmFromHome / NormalizationDefaults.MaxKm);
        // 8: tx_count_24h
        output[8] = Clamp01(txCount24h / NormalizationDefaults.MaxTxCount24h);
        // 9: is_online
        output[9] = isOnline ? 1f : 0f;
        // 10: card_present
        output[10] = cardPresent ? 1f : 0f;
        // 11: unknown_merchant (1 se merchantId NÃO está em customerKnownMerchants)
        output[11] = ContainsString(customerKnownMerchants, merchantId) ? 0f : 1f;
        // 12: mcc_risk
        output[12] = mccRisk.Get(merchantMcc);
        // 13: merchant_avg_amount
        output[13] = Clamp01((float)(merchantAvgAmount / NormalizationDefaults.MaxMerchantAvgAmount));
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Mapeia DayOfWeek do .NET (Sun=0..Sat=6) para spec do host (Mon=0..Sun=6).</summary>
    private static int MapDayOfWeekMon0(DayOfWeek d)
    {
        // Sun=0 → 6; Mon=1 → 0; ... Sat=6 → 5
        return ((int)d + 6) % 7;
    }

    private static bool ContainsString(string[] arr, string target)
    {
        for (var i = 0; i < arr.Length; i++)
            if (arr[i] == target) return true;
        return false;
    }
}
