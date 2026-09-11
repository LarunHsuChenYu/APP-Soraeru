namespace Soraeru.ClientLogic.Ocr;

/// <summary>
/// Splits on-device OCR full text into selectable word/phrase candidates (single-select UI).
/// Drops icon / UI noise tokens (e.g. "$)", "PP\"") that are not vocabulary-like.
/// </summary>
public static class OcrTextTokenizer
{
    public const int MaxTokenLength = 50;

    public static IReadOnlyList<string> Tokenize(string? text) =>
        FilterTokens(text, dedupe: true);

    /// <summary>
    /// Rebuilds whitespace-separated text without icon/UI noise tokens (keeps order, no dedupe).
    /// </summary>
    public static string StripNoiseTokens(string? text)
    {
        var kept = FilterTokens(text, dedupe: false);
        return kept.Count == 0 ? string.Empty : string.Join(' ', kept);
    }

    /// <summary>
    /// Higher score means more icon/UI debris relative to the cleaned line (for picking among OCR passes).
    /// </summary>
    public static int ScoreCyrillicLineNoise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !OcrScriptQuality.ContainsCyrillic(text))
            return 0;

        var rawParts = text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rawParts.Length == 0)
            return 0;

        var kept = FilterTokens(text, dedupe: false);
        var removedTokens = rawParts.Length - kept.Count;
        var removedChars = Math.Max(0, text.Length - string.Join(' ', kept).Length);
        return removedTokens * 30 + removedChars;
    }

    static IReadOnlyList<string> FilterTokens(string? text, bool dedupe)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var parts = text.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return Array.Empty<string>();

        var cyrillicContext = OcrScriptQuality.ContainsCyrillic(text);
        parts = TrimLeadingIconPrefix(parts, cyrillicContext);

        var result = new List<string>(parts.Length);
        HashSet<string>? seen = dedupe ? new HashSet<string>(StringComparer.Ordinal) : null;

        for (var i = 0; i < parts.Length; i++)
        {
            var token = Truncate(parts[i]);
            if (token.Length == 0)
                continue;
            if (!IsLikelyVocabularyToken(token, cyrillicContext))
                continue;
            if (cyrillicContext && IsMidSentenceCapitalizedOutlier(token, parts, i))
                continue;
            if (seen is not null && !seen.Add(token))
                continue;
            result.Add(token);
        }

        return result;
    }

    /// <summary>
    /// True when the token looks like a learnable word rather than OCR junk from icons/borders.
    /// </summary>
    public static bool IsLikelyVocabularyToken(string token, bool cyrillicContext = false)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var letters = 0;
        var junk = 0;
        var latinLetters = 0;
        var cyrillicLetters = 0;

        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (IsLetterAnyScript(v))
            {
                letters++;
                if (OcrScriptQuality.IsLatinLetter(v))
                    latinLetters++;
                if (OcrScriptQuality.IsCyrillicScript(v))
                    cyrillicLetters++;
                continue;
            }

            if (v is '-' or '\'' or '\u2019' or '.')
                continue;

            junk++;
        }

        if (letters == 0)
            return false;

        // More symbol/digit junk than letters → icon debris ("$)", "PP\"").
        if (junk >= letters)
            return false;

        if (!cyrillicContext || cyrillicLetters > 0)
        {
            // Single Cyrillic glyph from UI debris (e.g. "Ч") — keep only real 1-letter words.
            if (cyrillicContext
                && letters == 1
                && cyrillicLetters == 1
                && junk == 0
                && !IsAllowedSingleCyrillicWord(token))
            {
                return false;
            }

            return true;
        }

        // Beside Cyrillic: drop Latin-only crumbs from icons / borders.
        if (cyrillicLetters == 0 && latinLetters > 0)
        {
            if (latinLetters <= 3 && junk > 0)
                return false;

            if (latinLetters == 1 && junk == 0)
                return false;

            // Pure Latin short tokens (pea, OK, PP) without Cyrillic.
            if (junk == 0)
            {
                if (latinLetters <= 3)
                    return false;

                if (latinLetters <= 4 && (IsAllAsciiUpper(token) || IsAllAsciiLower(token)))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Drops short prefix tokens before the first capitalized Cyrillic anchor (speaker-icon debris).
    /// </summary>
    static string[] TrimLeadingIconPrefix(string[] parts, bool cyrillicContext)
    {
        if (!cyrillicContext || parts.Length < 2)
            return parts;

        var anchorIdx = -1;
        for (var i = 0; i < parts.Length; i++)
        {
            if (IsSentenceAnchor(parts[i]))
            {
                anchorIdx = i;
                break;
            }
        }

        if (anchorIdx <= 0)
            return parts;

        for (var i = 0; i < anchorIdx; i++)
        {
            if (!IsLeadingDebrisToken(parts[i]))
                return parts;
        }

        return parts[anchorIdx..];
    }

    /// <summary>
    /// Capitalized 4+ Cyrillic token mid-sentence when lowercase words follow (UI/icon hallucination).
    /// </summary>
    static bool IsMidSentenceCapitalizedOutlier(string token, string[] parts, int index)
    {
        if (index <= 0)
            return false;

        var cyrillicLetters = CountCyrillicLetters(token);
        if (cyrillicLetters < 4 || !StartsWithUppercaseCyrillic(token))
            return false;

        for (var j = index + 1; j < parts.Length; j++)
        {
            if (HasLowercaseCyrillic(parts[j]))
                return true;
        }

        return false;
    }

    static bool IsSentenceAnchor(string token) =>
        CountCyrillicLetters(token) >= 3 && StartsWithUppercaseCyrillic(token);

    static bool IsLeadingDebrisToken(string token)
    {
        if (IsLatinOnlyAscii(token))
            return true;

        return CountCyrillicLetters(token) <= 2;
    }

    static bool IsLatinOnlyAscii(string token)
    {
        var latin = 0;
        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (OcrScriptQuality.IsLatinLetter(v))
            {
                latin++;
                continue;
            }

            if (v is not ('-' or '\'' or '\u2019' or '.'))
                return false;
        }

        return latin > 0;
    }

    static bool StartsWithUppercaseCyrillic(string token)
    {
        foreach (var rune in token.EnumerateRunes())
        {
            if (!OcrScriptQuality.IsCyrillicScript(rune.Value))
                continue;

            return char.IsUpper(char.ConvertFromUtf32(rune.Value), 0);
        }

        return false;
    }

    static bool HasLowercaseCyrillic(string token)
    {
        foreach (var rune in token.EnumerateRunes())
        {
            if (OcrScriptQuality.IsCyrillicScript(rune.Value)
                && char.IsLower(char.ConvertFromUtf32(rune.Value), 0))
            {
                return true;
            }
        }

        return false;
    }

    static int CountCyrillicLetters(string token)
    {
        var count = 0;
        foreach (var rune in token.EnumerateRunes())
        {
            if (OcrScriptQuality.IsCyrillicScript(rune.Value))
                count++;
        }

        return count;
    }

    static bool IsAllAsciiLower(string token)
    {
        var sawLetter = false;
        foreach (var ch in token)
        {
            if (ch is >= 'A' and <= 'Z')
                return false;
            if (ch is >= 'a' and <= 'z')
                sawLetter = true;
        }

        return sawLetter;
    }

    static bool IsAllowedSingleCyrillicWord(string token)
    {
        // Common Russian / Ukrainian 1-letter function words (case-insensitive).
        return token.Equals("а", StringComparison.OrdinalIgnoreCase)
            || token.Equals("и", StringComparison.OrdinalIgnoreCase)
            || token.Equals("о", StringComparison.OrdinalIgnoreCase)
            || token.Equals("у", StringComparison.OrdinalIgnoreCase)
            || token.Equals("я", StringComparison.OrdinalIgnoreCase)
            || token.Equals("в", StringComparison.OrdinalIgnoreCase)
            || token.Equals("с", StringComparison.OrdinalIgnoreCase)
            || token.Equals("к", StringComparison.OrdinalIgnoreCase)
            || token.Equals("е", StringComparison.OrdinalIgnoreCase)
            || token.Equals("і", StringComparison.OrdinalIgnoreCase) // Ukrainian i
            || token.Equals("й", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsAllAsciiUpper(string token)
    {
        var sawLetter = false;
        foreach (var ch in token)
        {
            if (ch is >= 'a' and <= 'z')
                return false;
            if (ch is >= 'A' and <= 'Z')
                sawLetter = true;
        }

        return sawLetter;
    }

    static bool IsLetterAnyScript(int codePoint) =>
        OcrScriptQuality.IsLatinLetter(codePoint)
        || OcrScriptQuality.IsCyrillicScript(codePoint)
        || OcrScriptQuality.IsCjkScript(codePoint)
        || OcrScriptQuality.IsArabicScript(codePoint)
        || OcrScriptQuality.IsDevanagariScript(codePoint)
        || OcrScriptQuality.IsSoutheastAsianScript(codePoint)
        || char.IsLetter(char.ConvertFromUtf32(codePoint), 0);

    static string Truncate(string value) =>
        value.Length <= MaxTokenLength ? value : value[..MaxTokenLength];
}
