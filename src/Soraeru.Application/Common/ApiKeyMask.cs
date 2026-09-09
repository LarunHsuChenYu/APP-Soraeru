namespace Soraeru.Application.Common;

/// <summary>Mask secrets for curator UI / API responses.</summary>
public static class ApiKeyMask
{
    public static string Mask(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "(未設定)";
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 8)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..4]}…{trimmed[^4..]}";
    }

    public static bool LooksConfigured(string? apiKey) =>
        !string.IsNullOrWhiteSpace(apiKey)
        && !apiKey.Contains("REPLACE", StringComparison.OrdinalIgnoreCase);
}
