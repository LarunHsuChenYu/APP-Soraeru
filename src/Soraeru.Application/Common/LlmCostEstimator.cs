namespace Soraeru.Application.Common;

/// <summary>
/// Rough NTD cost from tokens (Flash-class). Used for curator dashboards — not billing.
/// </summary>
public static class LlmCostEstimator
{
    // Rough Gemini Flash order-of-magnitude (~NT$/1M tokens blended).
    public const decimal NtdPerMillionTokens = 15m;

    public static decimal EstimateNtd(int? promptTokens, int? completionTokens)
    {
        var total = (promptTokens ?? 0) + (completionTokens ?? 0);
        if (total <= 0)
        {
            return 0m;
        }

        return Math.Round(total / 1_000_000m * NtdPerMillionTokens, 4, MidpointRounding.AwayFromZero);
    }
}
