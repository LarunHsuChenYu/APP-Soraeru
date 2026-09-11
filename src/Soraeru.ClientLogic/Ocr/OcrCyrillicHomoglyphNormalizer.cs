using System.Text;

namespace Soraeru.ClientLogic.Ocr;

/// <summary>
/// Cyrillic-dominant OCR repair: Latin lookalike remap (ect→ест, ili→или), union of short
/// tokens from secondary passes, and Long–Short–Long middle scoring. If a pass produced
/// ect/ест or ili/или, that short wins; garbage middles lose to higher-quality observed
/// shorts and are never invented from a word list.
/// </summary>
public static class OcrCyrillicHomoglyphNormalizer
{
    public const int MaxLookalikeTokenLength = 5;
    public const int MinSideTokenLengthForButtonRow = 4;

    const string PreferredEst = "ест";
    const string PreferredIli = "или";

    public static string NormalizeMixedScript(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !OcrScriptQuality.ContainsCyrillic(text))
            return text ?? string.Empty;

        var parts = SplitPreserveSeparators(text);
        var changed = false;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.Length == 0 || char.IsWhiteSpace(part[0]))
                continue;

            if (TryRemapPureLookalikeToken(part, out var remapped) && remapped != part)
            {
                parts[i] = remapped;
                changed = true;
            }
        }

        return changed ? string.Concat(parts) : text;
    }

    public static string UnionMissingLookalikeTokens(string? primaryText, params string?[] secondaryTexts)
    {
        var current = primaryText ?? string.Empty;
        if (secondaryTexts is null || secondaryTexts.Length == 0)
            return ReconcileButtonRowMiddle(NormalizeMixedScript(current));

        current = ReconcileButtonRowMiddle(current, secondaryTexts);
        foreach (var secondary in secondaryTexts)
        {
            if (string.IsNullOrWhiteSpace(secondary))
                continue;
            current = MergeMissingLookalikeTokens(current, secondary);
        }

        return ReconcileButtonRowMiddle(current, secondaryTexts);
    }

    public static string MergeMissingLookalikeTokens(string? primaryText, string? secondaryText)
    {
        var primary = NormalizeMixedScript(primaryText);
        if (string.IsNullOrWhiteSpace(primary) || !OcrScriptQuality.ContainsCyrillic(primary))
            return primary;

        if (string.IsNullOrWhiteSpace(secondaryText))
            return primary;

        var primaryTokens = Tokenize(primary);
        var secondaryTokens = Tokenize(secondaryText);
        if (secondaryTokens.Count == 0)
            return primary;

        var primaryHasGoodMiddle = TryGetButtonRowMiddleIndex(primaryTokens, out var primaryMid)
            && !IsHighConfusionMiddleGarbage(primaryTokens[primaryMid]);

        var primarySet = new HashSet<string>(primaryTokens, StringComparer.OrdinalIgnoreCase);
        var missing = new List<(int SecondaryIndex, string Remapped)>();

        for (var i = 0; i < secondaryTokens.Count; i++)
        {
            if (!TryResolveSecondaryShort(secondaryTokens[i], out var remapped))
                continue;
            if (primarySet.Contains(remapped))
                continue;
            if (primaryHasGoodMiddle)
                continue;

            missing.Add((i, remapped));
        }

        if (missing.Count == 0)
            return primary;

        var merged = new List<string>(primaryTokens);
        foreach (var (secondaryIndex, remapped) in missing.OrderByDescending(m => m.SecondaryIndex))
        {
            if (merged.Contains(remapped, StringComparer.OrdinalIgnoreCase))
                continue;

            var insertAt = ResolveInsertIndex(
                merged,
                secondaryTokens,
                secondaryIndex,
                missing.Count);
            merged.Insert(insertAt, remapped);
        }

        return string.Join(' ', merged);
    }

    public static string ReconcileButtonRowMiddle(string? primaryText, params string?[] secondaryTexts)
    {
        var primary = NormalizeMixedScript(primaryText);
        if (string.IsNullOrWhiteSpace(primary) || !OcrScriptQuality.ContainsCyrillic(primary))
            return primary;

        var tokens = Tokenize(primary);
        if (!TryGetButtonRowMiddleIndex(tokens, out var midIdx))
            return primary;

        var observed = new List<(string Token, int Score)>();
        AddObservedShort(observed, tokens[midIdx]);

        if (secondaryTexts is not null)
        {
            foreach (var secondary in secondaryTexts)
            {
                if (string.IsNullOrWhiteSpace(secondary))
                    continue;

                foreach (var raw in EnumerateMiddleShortCandidates(secondary))
                    AddObservedShort(observed, raw);
            }
        }

        var best = PickBestMiddle(observed);
        if (string.IsNullOrEmpty(best) || string.Equals(tokens[midIdx], best, StringComparison.Ordinal))
            return primary;

        tokens[midIdx] = best;
        return string.Join(' ', tokens);
    }

    public static string PreferBestShortToken(params string?[] candidates)
    {
        var observed = new List<(string Token, int Score)>();
        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var trimmed = raw.Trim();
            foreach (var piece in EnumerateMiddleShortCandidates(trimmed))
                AddObservedShort(observed, piece);

            AddObservedShort(observed, trimmed);
        }

        return PickBestMiddle(observed) ?? string.Empty;
    }

    public static string PreferRicherCyrillic(string? a, string? b)
    {
        var left = NormalizeMixedScript(a);
        var right = NormalizeMixedScript(b);
        if (string.IsNullOrWhiteSpace(right))
            return left;
        if (string.IsNullOrWhiteSpace(left))
            return right;

        var leftQ = LineQuality(left);
        var rightQ = LineQuality(right);
        if (rightQ > leftQ && OcrScriptQuality.ContainsCyrillic(right))
            return right;
        if (leftQ > rightQ)
            return left;

        var leftNoise = OcrTextTokenizer.ScoreCyrillicLineNoise(left);
        var rightNoise = OcrTextTokenizer.ScoreCyrillicLineNoise(right);
        if (leftNoise != rightNoise)
            return leftNoise < rightNoise ? left : right;

        var leftCyr = CountCyrillicRunes(left);
        var rightCyr = CountCyrillicRunes(right);
        return rightCyr > leftCyr ? right : left;
    }

    public static bool TryNormalizeSecondaryShortToken(string token, out string normalized)
    {
        normalized = token;
        if (string.IsNullOrEmpty(token) || token.Length > MaxLookalikeTokenLength)
            return false;

        if (TryRemapPureLookalikeToken(token, out var remapped))
        {
            normalized = remapped;
            return true;
        }

        if (!IsShortLetterToken(token))
            return false;

        var hasCyrillic = false;
        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (OcrScriptQuality.IsCyrillicScript(v))
            {
                hasCyrillic = true;
                continue;
            }

            if (OcrScriptQuality.IsLatinLetter(v))
                return false;

            if (!IsAllowedTokenPunctuation(v))
                return false;
        }

        if (!hasCyrillic)
            return false;

        normalized = token;
        return true;
    }

    public static bool TryRemapPureLookalikeToken(string token, out string remapped)
    {
        remapped = token;
        if (string.IsNullOrEmpty(token) || token.Length > MaxLookalikeTokenLength)
            return false;

        if (IsLikelyEnglishStopword(token))
            return false;

        var sb = new StringBuilder(token.Length);
        var letterCount = 0;
        var mappedAnyLatin = false;
        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (OcrScriptQuality.IsCyrillicScript(v))
            {
                letterCount++;
                sb.Append(rune.ToString());
                continue;
            }

            if (OcrScriptQuality.IsLatinLetter(v))
            {
                letterCount++;
                if (!TryMapLookalike(v, out var mapped))
                    return false;
                sb.Append(char.ConvertFromUtf32(mapped));
                mappedAnyLatin = true;
                continue;
            }

            if (IsAllowedTokenPunctuation(v))
            {
                sb.Append(rune.ToString());
                continue;
            }

            return false;
        }

        if (letterCount == 0 || !mappedAnyLatin)
            return false;

        remapped = sb.ToString();
        return OcrScriptQuality.ContainsCyrillic(remapped);
    }

    /// <summary>
    /// Pattern, not a word list: all-caps 2–4 or Latin+Cyrillic mix.
    /// Preferred shorts (ест / или and ect / ili) are never garbage.
    /// </summary>
    public static bool IsHighConfusionMiddleGarbage(string token)
    {
        if (string.IsNullOrEmpty(token) || IsPreferredMiddleShort(token))
            return false;

        if (TryRemapPureLookalikeToken(token, out var remapped) && IsPreferredMiddleShort(remapped))
            return false;

        var letters = CountLetters(token);
        if (letters is < 2 or > 4)
            return false;

        return IsMixedLatinAndCyrillic(token) || IsAllCapsLetters(token);
    }

    public static int ScoreMiddleShortCandidate(string token, bool fromLatinLookalike)
    {
        if (string.IsNullOrEmpty(token) || !TryNormalizeSecondaryShortToken(token, out var normalized))
            return int.MinValue;

        var raw = token;
        token = normalized;
        var len = CountLetters(token);

        if (IsPreferredMiddleShort(token))
        {
            var preferred = 80;
            if (fromLatinLookalike)
                preferred += 25;
            if (IsPureLowercaseCyrillic(token))
                preferred += 20;
            if (len is 2 or 3)
                preferred += 10;
            return preferred;
        }

        var score = 10;
        if (len is 2 or 3)
            score += 25;
        else if (len == 4)
            score += 5;
        else if (len == 5)
            score -= 5;

        if (IsPureLowercaseCyrillic(token))
            score += 20;

        if (IsHighConfusionMiddleGarbage(raw) || IsHighConfusionMiddleGarbage(token))
            score -= 80;

        return score;
    }

    static bool TryResolveSecondaryShort(string raw, out string remapped)
    {
        remapped = raw;
        if (!TryNormalizeSecondaryShortToken(raw, out var normalized))
            return false;

        if (IsHighConfusionMiddleGarbage(raw) || IsHighConfusionMiddleGarbage(normalized))
            return false;

        remapped = normalized;
        return true;
    }

    static void AddObservedShort(List<(string Token, int Score)> candidates, string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        var fromLatin = TryRemapPureLookalikeToken(raw, out _);
        if (!TryNormalizeSecondaryShortToken(raw, out var normalized))
            return;

        AddScored(candidates, normalized, ScoreMiddleShortCandidate(normalized, fromLatin));
    }

    static void AddScored(List<(string Token, int Score)> candidates, string token, int score)
    {
        if (string.IsNullOrEmpty(token) || score == int.MinValue)
            return;

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!string.Equals(candidates[i].Token, token, StringComparison.OrdinalIgnoreCase))
                continue;

            if (score > candidates[i].Score)
                candidates[i] = (token, score);
            return;
        }

        candidates.Add((token, score));
    }

    static string? PickBestMiddle(IReadOnlyList<(string Token, int Score)> observed)
    {
        foreach (var c in observed)
        {
            if (IsPreferredEst(c.Token))
                return PreferredEst;
        }

        foreach (var c in observed)
        {
            if (IsPreferredIli(c.Token))
                return PreferredIli;
        }

        if (observed.Count == 0)
            return null;

        return observed.OrderByDescending(c => c.Score).ThenBy(c => c.Token.Length).First().Token;
    }

    static int LineQuality(string text)
    {
        var tokens = Tokenize(text);
        var quality = CountLetterTokens(text) * 10;
        if (!TryGetButtonRowMiddleIndex(tokens, out var mid))
            return quality;

        var middle = tokens[mid];
        var fromLatin = TryRemapPureLookalikeToken(middle, out _);
        return quality + ScoreMiddleShortCandidate(middle, fromLatin);
    }

    public static bool IsPreferredMiddleShort(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        var folded = token;
        if (TryRemapPureLookalikeToken(token, out var remapped))
            folded = remapped;

        return IsPreferredEst(folded) || IsPreferredIli(folded);
    }

    static bool IsPreferredEst(string token) =>
        string.Equals(token, PreferredEst, StringComparison.OrdinalIgnoreCase);

    static bool IsPreferredIli(string token) =>
        string.Equals(token, PreferredIli, StringComparison.OrdinalIgnoreCase);

    static bool IsAllCapsLetters(string token)
    {
        var letters = 0;
        foreach (var ch in token)
        {
            if (!char.IsLetter(ch))
                continue;
            letters++;
            if (!char.IsUpper(ch))
                return false;
        }

        return letters >= 2;
    }

    static bool IsMixedLatinAndCyrillic(string token)
    {
        var latin = false;
        var cyr = false;
        foreach (var rune in token.EnumerateRunes())
        {
            if (OcrScriptQuality.IsLatinLetter(rune.Value))
                latin = true;
            else if (OcrScriptQuality.IsCyrillicScript(rune.Value))
                cyr = true;
        }

        return latin && cyr;
    }

    static bool IsPureLowercaseCyrillic(string token)
    {
        var letters = 0;
        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (OcrScriptQuality.IsLatinLetter(v))
                return false;
            if (!OcrScriptQuality.IsCyrillicScript(v))
                continue;
            letters++;
            var s = rune.ToString();
            if (s.Length == 0 || !char.IsLower(s[0]))
                return false;
        }

        return letters > 0;
    }

    static IEnumerable<string> EnumerateMiddleShortCandidates(string text)
    {
        var tokens = Tokenize(text);
        if (tokens.Count == 0)
            yield break;

        if (tokens.Count == 1)
        {
            yield return tokens[0];
            yield break;
        }

        if (TryGetButtonRowMiddleIndex(tokens, out var mid))
        {
            yield return tokens[mid];
            yield break;
        }

        foreach (var t in tokens)
        {
            if (IsShortLetterToken(t) && t.Length <= MaxLookalikeTokenLength)
                yield return t;
        }
    }

    static bool TryGetButtonRowMiddleIndex(IReadOnlyList<string> tokens, out int middleIndex)
    {
        middleIndex = -1;
        if (tokens.Count != 3)
            return false;

        var leftLen = CountLetters(tokens[0]);
        var midLen = CountLetters(tokens[1]);
        var rightLen = CountLetters(tokens[2]);
        if (leftLen < MinSideTokenLengthForButtonRow
            || rightLen < MinSideTokenLengthForButtonRow
            || midLen is < 1 or > MaxLookalikeTokenLength)
        {
            return false;
        }

        if (midLen >= leftLen || midLen >= rightLen)
            return false;

        middleIndex = 1;
        return true;
    }

    static int CountLetters(string token)
    {
        var n = 0;
        foreach (var rune in token.EnumerateRunes())
        {
            if (OcrScriptQuality.IsLatinLetter(rune.Value) || OcrScriptQuality.IsCyrillicScript(rune.Value))
                n++;
        }

        return n;
    }

    static int ResolveInsertIndex(
        IReadOnlyList<string> merged,
        IReadOnlyList<string> secondaryTokens,
        int secondaryIndex,
        int missingCount)
    {
        if (TryInsertBetweenAnchors(merged, secondaryTokens, secondaryIndex, out var anchored))
            return anchored;

        if (merged.Count == 2 && missingCount == 1)
            return 1;

        if (merged.Count >= 2
            && secondaryTokens.Count == 1
            && missingCount == 1)
        {
            return Math.Clamp(merged.Count / 2, 1, merged.Count - 1);
        }

        var insertAt = secondaryTokens.Count == 0
            ? merged.Count
            : (secondaryIndex * (merged.Count + 1)) / secondaryTokens.Count;
        if (insertAt < 0)
            insertAt = 0;
        if (insertAt > merged.Count)
            insertAt = merged.Count;
        return insertAt;
    }

    static bool TryInsertBetweenAnchors(
        IReadOnlyList<string> merged,
        IReadOnlyList<string> secondaryTokens,
        int secondaryIndex,
        out int insertAt)
    {
        insertAt = 0;
        if (secondaryIndex <= 0 || secondaryIndex >= secondaryTokens.Count - 1)
            return false;

        var leftSec = secondaryTokens[secondaryIndex - 1];
        var rightSec = secondaryTokens[secondaryIndex + 1];
        var leftIdx = FindAlignedTokenIndex(merged, leftSec);
        var rightIdx = FindAlignedTokenIndex(merged, rightSec);
        if (leftIdx < 0 || rightIdx < 0 || rightIdx <= leftIdx)
            return false;

        insertAt = leftIdx + 1;
        return true;
    }

    static int FindAlignedTokenIndex(IReadOnlyList<string> tokens, string secondaryToken)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (string.Equals(tokens[i], secondaryToken, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        if (TryRemapPureLookalikeToken(secondaryToken, out var remapped))
        {
            for (var i = 0; i < tokens.Count; i++)
            {
                if (string.Equals(tokens[i], remapped, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        var secFold = FoldForAlign(secondaryToken);
        if (secFold.Length < 2)
            return -1;

        var best = -1;
        var bestScore = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var priFold = FoldForAlign(tokens[i]);
            if (priFold.Length < 2)
                continue;

            var score = SharedPrefixLength(secFold, priFold);
            if (score >= 2 && score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    static string FoldForAlign(string token)
    {
        var sb = new StringBuilder(token.Length);
        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (OcrScriptQuality.IsLatinLetter(v))
            {
                sb.Append(char.ToLowerInvariant((char)v));
                continue;
            }

            if (TryAppendCyrillicLatinFold(v, sb))
                continue;

            if (OcrScriptQuality.IsCyrillicScript(v))
                sb.Append(rune.ToString().ToLowerInvariant());
        }

        return sb.ToString();
    }

    static int SharedPrefixLength(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < n && a[i] == b[i])
            i++;
        return i;
    }

    static bool TryAppendCyrillicLatinFold(int codePoint, StringBuilder sb)
    {
        switch (codePoint)
        {
            case 'а' or 'А': sb.Append('a'); return true;
            case 'е' or 'Е' or 'ё' or 'Ё' or 'э' or 'Э': sb.Append('e'); return true;
            case 'о' or 'О': sb.Append('o'); return true;
            case 'р' or 'Р': sb.Append('p'); return true;
            case 'с' or 'С': sb.Append('c'); return true;
            case 'х' or 'Х': sb.Append('x'); return true;
            case 'у' or 'У': sb.Append('u'); return true;
            case 'т' or 'Т': sb.Append('t'); return true;
            case 'н' or 'Н': sb.Append('n'); return true;
            case 'в' or 'В': sb.Append('v'); return true;
            case 'м' or 'М': sb.Append('m'); return true;
            case 'к' or 'К': sb.Append('k'); return true;
            case 'и' or 'И' or 'й' or 'Й' or 'ы' or 'Ы': sb.Append('i'); return true;
            case 'л' or 'Л': sb.Append('l'); return true;
            case 'д' or 'Д': sb.Append('d'); return true;
            case 'б' or 'Б': sb.Append('b'); return true;
            case 'г' or 'Г': sb.Append('g'); return true;
            case 'з' or 'З': sb.Append('z'); return true;
            case 'ч' or 'Ч': sb.Append('c'); sb.Append('h'); return true;
            case 'ш' or 'Ш': sb.Append('s'); sb.Append('h'); return true;
            case 'я' or 'Я': sb.Append('y'); sb.Append('a'); return true;
            case 'ю' or 'Ю': sb.Append('y'); sb.Append('u'); return true;
            case 'ж' or 'Ж': sb.Append('z'); sb.Append('h'); return true;
            case 'ц' or 'Ц': sb.Append('t'); sb.Append('s'); return true;
            default: return false;
        }
    }

    static bool IsShortLetterToken(string token)
    {
        var letters = 0;
        foreach (var rune in token.EnumerateRunes())
        {
            var v = rune.Value;
            if (OcrScriptQuality.IsLatinLetter(v) || OcrScriptQuality.IsCyrillicScript(v))
            {
                letters++;
                continue;
            }

            if (IsAllowedTokenPunctuation(v))
                continue;

            return false;
        }

        return letters is >= 1 and <= MaxLookalikeTokenLength;
    }

    static bool IsLikelyEnglishStopword(string token) =>
        token.Equals("a", StringComparison.OrdinalIgnoreCase)
        || token.Equals("an", StringComparison.OrdinalIgnoreCase)
        || token.Equals("the", StringComparison.OrdinalIgnoreCase)
        || token.Equals("to", StringComparison.OrdinalIgnoreCase)
        || token.Equals("of", StringComparison.OrdinalIgnoreCase)
        || token.Equals("in", StringComparison.OrdinalIgnoreCase)
        || token.Equals("on", StringComparison.OrdinalIgnoreCase)
        || token.Equals("is", StringComparison.OrdinalIgnoreCase)
        || token.Equals("it", StringComparison.OrdinalIgnoreCase)
        || token.Equals("be", StringComparison.OrdinalIgnoreCase)
        || token.Equals("or", StringComparison.OrdinalIgnoreCase)
        || token.Equals("at", StringComparison.OrdinalIgnoreCase)
        || token.Equals("as", StringComparison.OrdinalIgnoreCase)
        || token.Equals("by", StringComparison.OrdinalIgnoreCase)
        || token.Equals("we", StringComparison.OrdinalIgnoreCase)
        || token.Equals("he", StringComparison.OrdinalIgnoreCase)
        || token.Equals("me", StringComparison.OrdinalIgnoreCase)
        || token.Equals("my", StringComparison.OrdinalIgnoreCase)
        || token.Equals("if", StringComparison.OrdinalIgnoreCase)
        || token.Equals("no", StringComparison.OrdinalIgnoreCase)
        || token.Equals("so", StringComparison.OrdinalIgnoreCase)
        || token.Equals("do", StringComparison.OrdinalIgnoreCase);

    static bool TryMapLookalike(int codePoint, out int mapped)
    {
        mapped = codePoint switch
        {
            'a' => 'а',
            'A' => 'А',
            'e' => 'е',
            'E' => 'Е',
            'o' => 'о',
            'O' => 'О',
            'p' => 'р',
            'P' => 'Р',
            'c' => 'с',
            'C' => 'С',
            'x' => 'х',
            'X' => 'Х',
            'y' => 'у',
            'Y' => 'У',
            't' => 'т',
            'T' => 'Т',
            'h' => 'н',
            'H' => 'Н',
            'B' => 'В',
            'm' => 'м',
            'M' => 'М',
            'k' => 'к',
            'K' => 'К',
            'i' => 'и',
            'I' => 'И',
            'l' => 'л',
            'L' => 'Л',
            _ => -1
        };
        return mapped >= 0;
    }

    static bool IsLetterLike(int codePoint) =>
        OcrScriptQuality.IsLatinLetter(codePoint) || OcrScriptQuality.IsCyrillicScript(codePoint);

    static bool IsAllowedTokenPunctuation(int codePoint) =>
        codePoint is '-' or '\'' or '\u2019' or '.';

    static List<string> Tokenize(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    static int CountLetterTokens(string text) =>
        Tokenize(text).Count(t => t.EnumerateRunes().Any(r => IsLetterLike(r.Value)));

    static int CountCyrillicRunes(string text)
    {
        var n = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (OcrScriptQuality.IsCyrillicScript(rune.Value))
                n++;
        }

        return n;
    }

    static List<string> SplitPreserveSeparators(string text)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        var inWs = false;
        var started = false;

        foreach (var ch in text)
        {
            var ws = char.IsWhiteSpace(ch);
            if (!started)
            {
                inWs = ws;
                started = true;
                sb.Append(ch);
                continue;
            }

            if (ws == inWs)
            {
                sb.Append(ch);
                continue;
            }

            parts.Add(sb.ToString());
            sb.Clear();
            sb.Append(ch);
            inWs = ws;
        }

        if (sb.Length > 0)
            parts.Add(sb.ToString());

        return parts;
    }
}
