using System.Globalization;
using System.Text;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;
using Soraeru.Application.Quota;

namespace Soraeru.Application.Analyze;

/// <summary>
/// Word analysis: validate → verified lookup → cache → quota →
/// (hit: meaning/reading LLM + curated mnemonics) | (miss: full LLM → schema → hard gate) → consume.
/// Verified／金標優先只影響本次結果候選，不回寫學習者 WordCards／個人空耳（ADR-0007／票 17）。
/// </summary>
public sealed class AnalyzeWordService : IAnalyzeWordService
{
    public const string PromptVersion = "word-analysis.v1.3";
    public const int MaxTextLength = 50;
    public const int MaxLlmAttempts = 2;
    public const int MaxRegenerationsPerWord = 3;
    public const string RegenerationLimitErrorCode = "REGENERATION_LIMIT_EXCEEDED";

    private readonly IUserRepository _users;
    private readonly IQuotaService _quota;
    private readonly IWordAnalysisAgent _agent;
    private readonly IAnalysisResultCache _cache;
    private readonly IVerifiedMnemonicRepository _verified;
    private readonly IWordRegenerationRepository _regenerations;

    public AnalyzeWordService(
        IUserRepository users,
        IQuotaService quota,
        IWordAnalysisAgent agent,
        IAnalysisResultCache cache,
        IVerifiedMnemonicRepository verified,
        IWordRegenerationRepository regenerations)
    {
        _users = users;
        _quota = quota;
        _agent = agent;
        _cache = cache;
        _verified = verified;
        _regenerations = regenerations;
    }

    public async Task<ServiceResult<AnalyzeWordResult>> AnalyzeAsync(
        AnalyzeWordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            return ServiceResult<AnalyzeWordResult>.Failure("VALIDATION", "User id is required.");
        }

        var text = (command.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return ServiceResult<AnalyzeWordResult>.Failure("VALIDATION", "請輸入單字或短語。");
        }

        if (text.Length > MaxTextLength)
        {
            return ServiceResult<AnalyzeWordResult>.Failure(
                "VALIDATION",
                $"單字／短語不可超過 {MaxTextLength} 字。");
        }

        var memoryLanguage = string.IsNullOrWhiteSpace(command.MemoryLanguage)
            ? "zh-TW"
            : command.MemoryLanguage.Trim();
        if (!string.Equals(memoryLanguage, "zh-TW", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<AnalyzeWordResult>.Failure(
                "VALIDATION",
                "memoryLanguage 目前僅支援 zh-TW。");
        }

        var user = await _users.FindByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<AnalyzeWordResult>.Failure("NOT_FOUND", "使用者不存在。");
        }

        var sourceLanguage = NormalizeSourceLanguage(command.SourceLanguage);
        var notationPreference = NormalizeNotationPreference(
            command.NotationPreference,
            user.NotationPref);

        var normalizedText = NormalizeText(text);
        var regenerationLanguageKey = sourceLanguage.ToLowerInvariant();

        if (command.ForceRefresh)
        {
            var regenerationCount = await _regenerations.GetCountAsync(
                user.Id,
                regenerationLanguageKey,
                normalizedText,
                cancellationToken);
            if (regenerationCount >= MaxRegenerationsPerWord)
            {
                return ServiceResult<AnalyzeWordResult>.Failure(
                    RegenerationLimitErrorCode,
                    $"同一單字最多重新產生 {MaxRegenerationsPerWord} 次，請稍後再試或改手動輸入空耳。");
            }
        }

        VerifiedMnemonicRecord? verifiedHit = null;
        if (!string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase))
        {
            verifiedHit = await _verified.FindActiveByLanguageAndNormalizedAsync(
                sourceLanguage,
                normalizedText,
                cancellationToken);
        }

        var mnemonicSource = verifiedHit is null
            ? AnalyzeMnemonicSources.LlmDraft
            : AnalyzeMnemonicSources.Verified;
        var cacheKey = BuildCacheKey(
            sourceLanguage,
            normalizedText,
            notationPreference,
            verifiedHit?.Id);

        if (!command.ForceRefresh
            && _cache.TryGet(cacheKey, out var cached)
            && cached is not null)
        {
            var remainingCached = await _quota.GetRemainingAsync(user, cancellationToken);
            var remainingRegenCached = await GetRemainingRegenerationsAsync(
                user.Id,
                regenerationLanguageKey,
                normalizedText,
                cancellationToken);
            return ServiceResult<AnalyzeWordResult>.Success(
                ToResult(
                    cached,
                    Cached: true,
                    remainingCached.RemainingDailyQuota,
                    mnemonicSource,
                    remainingRegenCached));
        }

        var remainingBefore = await _quota.GetRemainingAsync(user, cancellationToken);
        if (!remainingBefore.IsUnlimited && remainingBefore.RemainingDailyQuota <= 0)
        {
            return ServiceResult<AnalyzeWordResult>.Failure(
                "QUOTA_EXCEEDED",
                "今日分析次數已用完，請明日再試。");
        }

        if (verifiedHit is not null)
        {
            return await AnalyzeVerifiedAsync(
                text,
                sourceLanguage,
                notationPreference,
                verifiedHit,
                cacheKey,
                user,
                command.ForceRefresh,
                regenerationLanguageKey,
                normalizedText,
                cancellationToken);
        }

        return await AnalyzeLlmDraftAsync(
            text,
            sourceLanguage,
            notationPreference,
            cacheKey,
            user,
            command.ForceRefresh,
            regenerationLanguageKey,
            normalizedText,
            cancellationToken);
    }

    private async Task<ServiceResult<AnalyzeWordResult>> AnalyzeVerifiedAsync(
        string text,
        string sourceLanguage,
        string notationPreference,
        VerifiedMnemonicRecord verifiedHit,
        string cacheKey,
        UserRecord user,
        bool forceRefresh,
        string regenerationLanguageKey,
        string normalizedText,
        CancellationToken cancellationToken)
    {
        var request = new WordAnalysisAgentRequest(
            text,
            sourceLanguage,
            "zh-TW",
            notationPreference,
            SkipMnemonics: true,
            ActorUserId: user.Id);

        WordAnalysisPayload? meaningPayload = null;
        string? lastErrorCode = null;
        string? lastErrorMessage = null;

        for (var attempt = 0; attempt < MaxLlmAttempts; attempt++)
        {
            WordAnalysisAgentOutcome outcome;
            try
            {
                outcome = await _agent.AnalyzeAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceResult<AnalyzeWordResult>.Failure("LLM_NOT_CONFIGURED", ex.Message);
            }
            catch (Exception ex) when (attempt == 0)
            {
                lastErrorCode = "ANALYZE_FAILED";
                lastErrorMessage = ex.Message;
                continue;
            }
            catch (Exception ex)
            {
                return ServiceResult<AnalyzeWordResult>.Failure(
                    "ANALYZE_FAILED",
                    $"分析失敗：{ex.Message}");
            }

            switch (outcome)
            {
                case WordAnalysisAgentSuccess success:
                    if (!TryValidateMeaningReadingPayload(success.Payload, out var schemaError))
                    {
                        lastErrorCode = "SCHEMA_INVALID";
                        lastErrorMessage = schemaError;
                        continue;
                    }

                    meaningPayload = success.Payload;
                    break;

                case WordAnalysisAgentFailure failure:
                    if (string.Equals(failure.Code, "UNANALYZABLE", StringComparison.OrdinalIgnoreCase))
                    {
                        return ServiceResult<AnalyzeWordResult>.Failure(
                            "UNANALYZABLE",
                            failure.Message);
                    }

                    lastErrorCode = failure.Code;
                    lastErrorMessage = failure.Message;
                    continue;

                default:
                    lastErrorCode = "ANALYZE_FAILED";
                    lastErrorMessage = "未知的分析結果。";
                    continue;
            }

            break;
        }

        if (meaningPayload is null)
        {
            return ServiceResult<AnalyzeWordResult>.Failure(
                lastErrorCode ?? "ANALYZE_FAILED",
                lastErrorMessage ?? "分析失敗，請稍後重試。");
        }

        // Trust curated empty-ear fields; never fall back to LLM mnemonics on hard-gate failure.
        var curatedMnemonic = new WordAnalysisMnemonic(
            verifiedHit.DisplayText,
            notationPreference is "roman" or "mixed" ? notationPreference : "bopomofo",
            verifiedHit.NotationText,
            verifiedHit.Explanation);

        var payload = meaningPayload with
        {
            SourceText = string.IsNullOrWhiteSpace(meaningPayload.SourceText)
                ? verifiedHit.SourceText
                : meaningPayload.SourceText,
            NormalizedText = string.IsNullOrWhiteSpace(meaningPayload.NormalizedText)
                ? verifiedHit.NormalizedSource
                : meaningPayload.NormalizedText,
            SourceLanguage = string.IsNullOrWhiteSpace(meaningPayload.SourceLanguage)
                ? verifiedHit.Language
                : meaningPayload.SourceLanguage,
            Mnemonics = new[] { curatedMnemonic }
        };

        var consumed = await _quota.TryConsumeAsync(user, cancellationToken);
        if (!consumed)
        {
            return ServiceResult<AnalyzeWordResult>.Failure(
                "QUOTA_EXCEEDED",
                "今日分析次數已用完，請明日再試。");
        }

        var remainingRegenerations = await RecordRegenerationAndGetRemainingAsync(
            user.Id,
            regenerationLanguageKey,
            normalizedText,
            forceRefresh,
            cancellationToken);

        _cache.Set(cacheKey, payload);
        var remainingAfter = await _quota.GetRemainingAsync(user, cancellationToken);
        return ServiceResult<AnalyzeWordResult>.Success(
            ToResult(
                payload,
                Cached: false,
                remainingAfter.RemainingDailyQuota,
                AnalyzeMnemonicSources.Verified,
                remainingRegenerations));
    }

    private async Task<ServiceResult<AnalyzeWordResult>> AnalyzeLlmDraftAsync(
        string text,
        string sourceLanguage,
        string notationPreference,
        string cacheKey,
        UserRecord user,
        bool forceRefresh,
        string regenerationLanguageKey,
        string normalizedText,
        CancellationToken cancellationToken)
    {
        var request = new WordAnalysisAgentRequest(
            text,
            sourceLanguage,
            "zh-TW",
            notationPreference,
            SkipMnemonics: false,
            ActorUserId: user.Id);

        WordAnalysisPayload? payload = null;
        string? lastErrorCode = null;
        string? lastErrorMessage = null;
        var hardGateFailed = false;

        for (var attempt = 0; attempt < MaxLlmAttempts; attempt++)
        {
            WordAnalysisAgentOutcome outcome;
            try
            {
                outcome = await _agent.AnalyzeAsync(request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return ServiceResult<AnalyzeWordResult>.Failure("LLM_NOT_CONFIGURED", ex.Message);
            }
            catch (Exception ex) when (attempt == 0)
            {
                lastErrorCode = "ANALYZE_FAILED";
                lastErrorMessage = ex.Message;
                continue;
            }
            catch (Exception ex)
            {
                return ServiceResult<AnalyzeWordResult>.Failure(
                    "ANALYZE_FAILED",
                    $"分析失敗：{ex.Message}");
            }

            switch (outcome)
            {
                case WordAnalysisAgentSuccess success:
                    if (!TryValidatePayload(success.Payload, text, notationPreference, out var schemaError))
                    {
                        lastErrorCode = "SCHEMA_INVALID";
                        lastErrorMessage = schemaError;
                        continue;
                    }

                    if (!MnemonicHardGate.TryValidateAll(
                            success.Payload.Mnemonics.Select(m => m.DisplayText),
                            out var gateError))
                    {
                        hardGateFailed = true;
                        lastErrorCode = MnemonicHardGate.FailureCode;
                        lastErrorMessage = gateError ?? "空耳未通過聽感硬閘，請稍後再試。";
                        continue;
                    }

                    payload = success.Payload;
                    break;

                case WordAnalysisAgentFailure failure:
                    if (string.Equals(failure.Code, "UNANALYZABLE", StringComparison.OrdinalIgnoreCase))
                    {
                        return ServiceResult<AnalyzeWordResult>.Failure(
                            "UNANALYZABLE",
                            failure.Message);
                    }

                    lastErrorCode = failure.Code;
                    lastErrorMessage = failure.Message;
                    continue;

                default:
                    lastErrorCode = "ANALYZE_FAILED";
                    lastErrorMessage = "未知的分析結果。";
                    continue;
            }

            break;
        }

        if (payload is null)
        {
            if (hardGateFailed)
            {
                return ServiceResult<AnalyzeWordResult>.Failure(
                    MnemonicHardGate.FailureCode,
                    lastErrorMessage ?? "空耳未通過聽感硬閘，請稍後再試。");
            }

            return ServiceResult<AnalyzeWordResult>.Failure(
                lastErrorCode ?? "ANALYZE_FAILED",
                lastErrorMessage ?? "分析失敗，請稍後重試。");
        }

        var consumed = await _quota.TryConsumeAsync(user, cancellationToken);
        if (!consumed)
        {
            return ServiceResult<AnalyzeWordResult>.Failure(
                "QUOTA_EXCEEDED",
                "今日分析次數已用完，請明日再試。");
        }

        var remainingRegenerations = await RecordRegenerationAndGetRemainingAsync(
            user.Id,
            regenerationLanguageKey,
            normalizedText,
            forceRefresh,
            cancellationToken);

        _cache.Set(cacheKey, payload);
        var remainingAfter = await _quota.GetRemainingAsync(user, cancellationToken);
        return ServiceResult<AnalyzeWordResult>.Success(
            ToResult(
                payload,
                Cached: false,
                remainingAfter.RemainingDailyQuota,
                AnalyzeMnemonicSources.LlmDraft,
                remainingRegenerations));
    }

    private async Task<int> GetRemainingRegenerationsAsync(
        Guid userId,
        string languageKey,
        string normalizedText,
        CancellationToken cancellationToken)
    {
        var count = await _regenerations.GetCountAsync(userId, languageKey, normalizedText, cancellationToken);
        return Math.Max(0, MaxRegenerationsPerWord - count);
    }

    private async Task<int> RecordRegenerationAndGetRemainingAsync(
        Guid userId,
        string languageKey,
        string normalizedText,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var count = await _regenerations.GetCountAsync(userId, languageKey, normalizedText, cancellationToken);
        if (forceRefresh)
        {
            await _regenerations.IncrementAsync(userId, languageKey, normalizedText, cancellationToken);
            count += 1;
        }

        return Math.Max(0, MaxRegenerationsPerWord - count);
    }

    private static AnalyzeWordResult ToResult(
        WordAnalysisPayload p,
        bool Cached,
        int remaining,
        string mnemonicSource,
        int remainingRegenerations) =>
        new(
            p.SourceText,
            p.NormalizedText,
            p.SourceLanguage,
            p.LanguageDisplayName,
            p.Meaning,
            p.ReadingText,
            p.Mnemonics
                .Select(m => new AnalyzeMnemonicCandidate(
                    m.DisplayText,
                    m.NotationType,
                    m.NotationText,
                    m.Explanation))
                .ToList(),
            p.Notice,
            Cached,
            remaining,
            mnemonicSource,
            remainingRegenerations);

    internal static bool TryValidateMeaningReadingPayload(
        WordAnalysisPayload payload,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(payload.SourceText)
            || string.IsNullOrWhiteSpace(payload.NormalizedText)
            || string.IsNullOrWhiteSpace(payload.SourceLanguage)
            || string.IsNullOrWhiteSpace(payload.LanguageDisplayName)
            || string.IsNullOrWhiteSpace(payload.Meaning)
            || string.IsNullOrWhiteSpace(payload.ReadingText)
            || string.IsNullOrWhiteSpace(payload.Notice))
        {
            error = "分析結果缺少必填欄位。";
            return false;
        }

        error = null;
        return true;
    }

    internal static bool TryValidatePayload(
        WordAnalysisPayload payload,
        string originalText,
        string notationPreference,
        out string? error)
    {
        if (!TryValidateMeaningReadingPayload(payload, out error))
        {
            return false;
        }

        if (payload.Mnemonics is null || payload.Mnemonics.Count is < 2 or > 3)
        {
            error = "空耳候選必須為 2～3 個。";
            return false;
        }

        foreach (var m in payload.Mnemonics)
        {
            if (string.IsNullOrWhiteSpace(m.DisplayText)
                || string.IsNullOrWhiteSpace(m.NotationType)
                || string.IsNullOrWhiteSpace(m.NotationText)
                || string.IsNullOrWhiteSpace(m.Explanation))
            {
                error = "空耳候選欄位不完整。";
                return false;
            }

            if (!IsAllowedNotationType(m.NotationType))
            {
                error = $"不支援的 notationType：{m.NotationType}";
                return false;
            }
        }

        // Soft consistency: prefer matching preference but allow mixed responses if types valid.
        _ = originalText;
        _ = notationPreference;
        error = null;
        return true;
    }

    private static bool IsAllowedNotationType(string type) =>
        type is "bopomofo" or "roman" or "mixed";

    private static string NormalizeSourceLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return "auto";

        return value.Trim();
    }

    private static string NormalizeNotationPreference(string? requestValue, string userPref)
    {
        var raw = !string.IsNullOrWhiteSpace(requestValue) ? requestValue : userPref;
        raw = raw.Trim().ToLowerInvariant();

        return raw switch
        {
            "bopomofo" or "zh-tw-phonetic" or "phonetic" or "zhuyin" or "注音" => "bopomofo",
            "roman" or "pinyin" or "roma" or "羅馬" => "roman",
            "mixed" or "混合" => "mixed",
            _ => "bopomofo"
        };
    }

    internal static string NormalizeText(string text)
    {
        var collapsed = string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed.Normalize(NormalizationForm.FormC);
    }

    private static string BuildCacheKey(
        string sourceLanguage,
        string normalizedText,
        string notationPreference,
        Guid? verifiedEntryId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{PromptVersion}|{sourceLanguage.ToLowerInvariant()}|{normalizedText}|{notationPreference}|{(verifiedEntryId is null ? "llm" : $"v:{verifiedEntryId:N}")}");
}
