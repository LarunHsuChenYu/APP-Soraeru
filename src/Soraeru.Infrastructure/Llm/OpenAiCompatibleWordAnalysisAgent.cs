using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;

namespace Soraeru.Infrastructure.Llm;

/// <summary>
/// OpenAI-compatible Chat Completions client (works with Google AI Studio OpenAI endpoint too).
/// Resolves ApiKey/Model/BaseUrl per call (SQLite override + config).
/// </summary>
public sealed class OpenAiCompatibleWordAnalysisAgent : IWordAnalysisAgent
{
    private const int MaxRateLimitRetries = 2;
    public const string FeatureTypeTextAnalysis = "text_analysis";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILlmSettingsResolver _settings;
    private readonly ILlmUsageRepository _usage;
    private readonly ILogger<OpenAiCompatibleWordAnalysisAgent> _logger;

    public OpenAiCompatibleWordAnalysisAgent(
        HttpClient http,
        ILlmSettingsResolver settings,
        ILlmUsageRepository usage,
        ILogger<OpenAiCompatibleWordAnalysisAgent> logger)
    {
        _http = http;
        _settings = settings;
        _usage = usage;
        _logger = logger;
    }

    public async Task<WordAnalysisAgentOutcome> AnalyzeAsync(
        WordAnalysisAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var effective = await _settings.ResolveAsync(cancellationToken);
        EnsureConfigured(effective);

        var systemPrompt = request.SkipMnemonics
            ? WordAnalysisPrompts.MeaningReadingOnlySystem
            : WordAnalysisPrompts.System;
        var userPrompt = request.SkipMnemonics
            ? WordAnalysisPrompts.BuildMeaningReadingUserPrompt(
                request.Text,
                request.SourceLanguage,
                request.MemoryLanguage)
            : WordAnalysisPrompts.BuildUserPrompt(
                request.Text,
                request.SourceLanguage,
                request.MemoryLanguage,
                request.NotationPreference);

        var messages = new ChatMessage[]
        {
            new("system", systemPrompt),
            new("user", userPrompt)
        };

        var sw = Stopwatch.StartNew();
        var preferJsonObject = !IsGroqEndpoint(effective.BaseUrl);
        var (firstStatus, firstRaw, firstUsage) = await PostCompletionAsync(
            effective, messages, useJsonObject: preferJsonObject, cancellationToken);
        var raw = firstRaw;
        var tokenUsage = firstUsage;
        if (!IsSuccessStatusCode(firstStatus) && preferJsonObject && ShouldRetryWithoutJsonObject(firstStatus, firstRaw))
        {
            _logger.LogInformation(
                "LLM json_object rejected ({Status}); retrying without response_format.",
                (int)firstStatus);
            var (retryStatus, retryRaw, retryUsage) = await PostCompletionAsync(
                effective, messages, useJsonObject: false, cancellationToken);
            raw = retryRaw;
            tokenUsage = retryUsage ?? tokenUsage;
            if (!IsSuccessStatusCode(retryStatus))
            {
                sw.Stop();
                await RecordUsageAsync(
                    request.ActorUserId,
                    effective,
                    tokenUsage,
                    (int)sw.ElapsedMilliseconds,
                    success: false,
                    "LLM_HTTP_ERROR",
                    cancellationToken);
                _logger.LogWarning("LLM HTTP {Status}: {Body}", (int)retryStatus, Truncate(raw));
                return new WordAnalysisAgentFailure(
                    "LLM_HTTP_ERROR",
                    $"LLM 呼叫失敗（{(int)retryStatus}）。");
            }
        }
        else if (!IsSuccessStatusCode(firstStatus))
        {
            sw.Stop();
            await RecordUsageAsync(
                request.ActorUserId,
                effective,
                tokenUsage,
                (int)sw.ElapsedMilliseconds,
                success: false,
                "LLM_HTTP_ERROR",
                cancellationToken);
            _logger.LogWarning("LLM HTTP {Status}: {Body}", (int)firstStatus, Truncate(raw));
            return new WordAnalysisAgentFailure(
                "LLM_HTTP_ERROR",
                $"LLM 呼叫失敗（{(int)firstStatus}）。");
        }

        ChatCompletionResponse? completion;
        try
        {
            completion = JsonSerializer.Deserialize<ChatCompletionResponse>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            sw.Stop();
            await RecordUsageAsync(
                request.ActorUserId,
                effective,
                tokenUsage,
                (int)sw.ElapsedMilliseconds,
                success: false,
                "LLM_PARSE_ERROR",
                cancellationToken);
            _logger.LogWarning(ex, "Failed to parse chat completion envelope.");
            return new WordAnalysisAgentFailure("LLM_PARSE_ERROR", "無法解析 LLM 回應。");
        }

        tokenUsage ??= ParseUsage(completion);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            sw.Stop();
            await RecordUsageAsync(
                request.ActorUserId,
                effective,
                tokenUsage,
                (int)sw.ElapsedMilliseconds,
                success: false,
                "LLM_EMPTY",
                cancellationToken);
            return new WordAnalysisAgentFailure("LLM_EMPTY", "LLM 未回傳內容。");
        }

        var json = UnwrapMarkdownFence(content.Trim());
        var outcome = ParsePayload(json);
        sw.Stop();
        await RecordUsageAsync(
            request.ActorUserId,
            effective,
            tokenUsage,
            (int)sw.ElapsedMilliseconds,
            success: outcome is WordAnalysisAgentSuccess,
            outcome is WordAnalysisAgentFailure f ? f.Code : null,
            cancellationToken);
        return outcome;
    }

    private async Task RecordUsageAsync(
        Guid? userId,
        LlmEffectiveSettings settings,
        TokenUsage? tokens,
        int latencyMs,
        bool success,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await _usage.AddAsync(
                new LlmUsageRecord(
                    Guid.NewGuid(),
                    userId,
                    FeatureTypeTextAnalysis,
                    settings.Model,
                    "OpenAICompatible",
                    tokens?.PromptTokens,
                    tokens?.CompletionTokens,
                    latencyMs,
                    success,
                    errorCode,
                    LlmCostEstimator.EstimateNtd(tokens?.PromptTokens, tokens?.CompletionTokens),
                    DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record LlmUsage.");
        }
    }

    private async Task<(System.Net.HttpStatusCode Status, string Raw, TokenUsage? Usage)> PostCompletionAsync(
        LlmEffectiveSettings settings,
        IReadOnlyList<ChatMessage> messages,
        bool useJsonObject,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRateLimitRetries; attempt++)
        {
            var (status, raw, usage) = await PostCompletionOnceAsync(
                settings, messages, useJsonObject, cancellationToken);
            if (IsSuccessStatusCode(status) || !IsRateLimitExceeded(status, raw))
                return (status, raw, usage);

            if (attempt >= MaxRateLimitRetries)
                return (status, raw, usage);

            var delaySeconds = Math.Clamp(TryParseRetryAfterSeconds(raw) + 0.5, 1, 60);
            _logger.LogInformation(
                "LLM rate limited (429); waiting {Delay:F1}s before retry {Attempt}/{Max}.",
                delaySeconds,
                attempt + 1,
                MaxRateLimitRetries);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        return (System.Net.HttpStatusCode.TooManyRequests, string.Empty, null);
    }

    private async Task<(System.Net.HttpStatusCode Status, string Raw, TokenUsage? Usage)> PostCompletionOnceAsync(
        LlmEffectiveSettings settings,
        IReadOnlyList<ChatMessage> messages,
        bool useJsonObject,
        CancellationToken cancellationToken)
    {
        var body = useJsonObject
            ? new ChatCompletionRequest(
                settings.Model,
                messages,
                Temperature: 0.4,
                ResponseFormat: new ResponseFormat("json_object"))
            : new ChatCompletionRequest(
                settings.Model,
                messages,
                Temperature: 0.4,
                ResponseFormat: null);

        var url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        TokenUsage? usage = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(raw, JsonOptions);
            usage = ParseUsage(parsed);
        }
        catch (JsonException)
        {
            // ignore — caller may still parse
        }

        return (response.StatusCode, raw, usage);
    }

    private static TokenUsage? ParseUsage(ChatCompletionResponse? completion)
    {
        if (completion?.Usage is null)
        {
            return null;
        }

        return new TokenUsage(completion.Usage.PromptTokens, completion.Usage.CompletionTokens);
    }

    private static bool IsGroqEndpoint(string? baseUrl) =>
        baseUrl?.Contains("groq.com", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsRateLimitExceeded(System.Net.HttpStatusCode status, string raw)
    {
        if (status != System.Net.HttpStatusCode.TooManyRequests)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("error", out var error))
                return true;

            if (error.TryGetProperty("code", out var codeEl))
            {
                var code = codeEl.GetString();
                if (string.Equals(code, "rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return error.TryGetProperty("message", out var msgEl)
                && msgEl.GetString()?.Contains("Rate limit", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return raw.Contains("rate_limit_exceeded", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static double TryParseRetryAfterSeconds(string raw)
    {
        const string marker = "try again in ";
        var index = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return 16;

        var start = index + marker.Length;
        var end = start;
        while (end < raw.Length && (char.IsDigit(raw[end]) || raw[end] == '.'))
            end++;

        return end > start
            && double.TryParse(raw.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 16;
    }

    private static bool IsSuccessStatusCode(System.Net.HttpStatusCode status)
    {
        var code = (int)status;
        return code >= 200 && code <= 299;
    }

    private static bool ShouldRetryWithoutJsonObject(System.Net.HttpStatusCode status, string raw)
    {
        if (status != System.Net.HttpStatusCode.BadRequest)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("error", out var error))
                return false;

            if (error.TryGetProperty("code", out var codeEl)
                && string.Equals(codeEl.GetString(), "json_validate_failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return error.TryGetProperty("message", out var msgEl)
                && msgEl.GetString()?.Contains("Failed to validate JSON", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return raw.Contains("json_validate_failed", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void EnsureConfigured(LlmEffectiveSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey)
            || options.ApiKey.Contains("REPLACE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "LLM API Key 尚未設定。請用 User Secrets／策展後台設定 Llm:ApiKey（見 docs/dev-setup-llm.md）。");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("Llm:Model 尚未設定。");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("Llm:BaseUrl 尚未設定。");
        }
    }

    private static WordAnalysisAgentOutcome ParsePayload(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorEl))
        {
            var code = errorEl.ValueKind == JsonValueKind.String
                ? errorEl.GetString() ?? "UNANALYZABLE"
                : "UNANALYZABLE";
            var message = root.TryGetProperty("message", out var msgEl)
                ? msgEl.GetString() ?? "無法分析此輸入。"
                : "無法分析此輸入。";
            return new WordAnalysisAgentFailure(code, message);
        }

        LlmJsonPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LlmJsonPayload>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return new WordAnalysisAgentFailure("LLM_PARSE_ERROR", "無法解析模型 JSON。");
        }

        if (payload is null)
        {
            return new WordAnalysisAgentFailure("LLM_PARSE_ERROR", "無法解析模型 JSON。");
        }

        var mnemonics = (payload.Mnemonics ?? [])
            .Select(m => new WordAnalysisMnemonic(
                m.DisplayText ?? "",
                m.NotationType ?? "",
                m.NotationText ?? "",
                m.Explanation ?? ""))
            .ToList();

        return new WordAnalysisAgentSuccess(
            new WordAnalysisPayload(
                payload.SourceText ?? "",
                payload.NormalizedText ?? "",
                payload.SourceLanguage ?? "",
                payload.LanguageDisplayName ?? "",
                payload.Meaning ?? "",
                payload.ReadingText ?? "",
                mnemonics,
                payload.Notice ?? ""));
    }

    private static string UnwrapMarkdownFence(string content)
    {
        if (!content.StartsWith("```", StringComparison.Ordinal))
            return content;

        var lines = content.Split('\n');
        var sb = new StringBuilder();
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                break;
            sb.AppendLine(line);
        }

        return sb.ToString().Trim();
    }

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";

    private sealed record TokenUsage(int? PromptTokens, int? CompletionTokens);

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("response_format")] ResponseFormat? ResponseFormat);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ResponseFormat(
        [property: JsonPropertyName("type")] string Type);

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice>? Choices { get; set; }
        public UsageDto? Usage { get; set; }
    }

    private sealed class UsageDto
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessageDto? Message { get; set; }
    }

    private sealed class ChatMessageDto
    {
        public string? Content { get; set; }
    }

    private sealed class LlmJsonPayload
    {
        public string? SourceText { get; set; }
        public string? NormalizedText { get; set; }
        public string? SourceLanguage { get; set; }
        public string? LanguageDisplayName { get; set; }
        public string? Meaning { get; set; }
        public string? ReadingText { get; set; }
        public List<LlmMnemonic>? Mnemonics { get; set; }
        public string? Notice { get; set; }
    }

    private sealed class LlmMnemonic
    {
        public string? DisplayText { get; set; }
        public string? NotationType { get; set; }
        public string? NotationText { get; set; }
        public string? Explanation { get; set; }
    }
}

public static class OpenAiCompatibleWordAnalysisAgentExtensions
{
    public static void ConfigureHttpClient(HttpClient client, int timeoutSeconds = 60)
    {
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 10, 180));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
