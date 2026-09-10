using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Soraeru.Curator.Access;

namespace Soraeru.Curator.Api;

public interface ICuratorApiClient
{
    Task<CuratorApiResult<CuratorSession>> LoginWithGoogleAsync(string idToken, CancellationToken ct = default);
    Task<CuratorApiResult<CuratorSession>> LoginWithEmailAsync(string email, string password, CancellationToken ct = default);
    Task<CuratorApiResult<IReadOnlyList<VerifiedMnemonicDto>>> ListAsync(
        string accessToken,
        string? language,
        string? query,
        CancellationToken ct = default);
    Task<CuratorApiResult<VerifiedMnemonicDto>> GetAsync(string accessToken, Guid id, CancellationToken ct = default);
    Task<CuratorApiResult<VerifiedMnemonicDto>> CreateAsync(
        string accessToken,
        CreateVerifiedMnemonicRequest request,
        CancellationToken ct = default);
    Task<CuratorApiResult<VerifiedMnemonicDto>> UpdateAsync(
        string accessToken,
        Guid id,
        UpdateVerifiedMnemonicRequest request,
        CancellationToken ct = default);
    Task<CuratorApiResult<VerifiedMnemonicDto>> SetEnabledAsync(
        string accessToken,
        Guid id,
        bool isEnabled,
        CancellationToken ct = default);
    Task<CuratorApiResult<LlmSettingsDto>> GetLlmSettingsAsync(string accessToken, CancellationToken ct = default);
    Task<CuratorApiResult<LlmSettingsDto>> UpdateLlmSettingsAsync(
        string accessToken,
        UpdateLlmSettingsBody body,
        CancellationToken ct = default);
    Task<CuratorApiResult<LlmUsagePageDto>> GetLlmUsageAsync(
        string accessToken,
        int take = 50,
        string? featureType = null,
        CancellationToken ct = default);
    Task<CuratorApiResult<IReadOnlyList<CuratorUserDto>>> ListUsersAsync(
        string accessToken,
        CancellationToken ct = default);
    Task<CuratorApiResult<CuratorUserDto>> SetDeveloperAsync(
        string accessToken,
        Guid userId,
        bool isDeveloper,
        CancellationToken ct = default);
    Task<CuratorApiResult<bool>> ResetUserPasswordAsync(
        string accessToken,
        Guid userId,
        string newPassword,
        CancellationToken ct = default);
}

public sealed class CuratorApiClient : ICuratorApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;

    public CuratorApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<CuratorApiResult<CuratorSession>> LoginWithGoogleAsync(string idToken, CancellationToken ct = default) =>
        LoginAsync("/api/v1/auth/google", new { idToken }, ct);

    public Task<CuratorApiResult<CuratorSession>> LoginWithEmailAsync(
        string email,
        string password,
        CancellationToken ct = default) =>
        LoginAsync("/api/v1/auth/login", new { email, password }, ct);

    public async Task<CuratorApiResult<IReadOnlyList<VerifiedMnemonicDto>>> ListAsync(
        string accessToken,
        string? language,
        string? query,
        CancellationToken ct = default)
    {
        var url = "/api/v1/curator/verified-mnemonics";
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(language))
        {
            qs.Add($"language={Uri.EscapeDataString(language.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            qs.Add($"q={Uri.EscapeDataString(query.Trim())}");
        }

        if (qs.Count > 0)
        {
            url += "?" + string.Join('&', qs);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadListAsync(response, ct);
    }

    public async Task<CuratorApiResult<VerifiedMnemonicDto>> GetAsync(
        string accessToken,
        Guid id,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/curator/verified-mnemonics/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadOneAsync(response, ct);
    }

    public async Task<CuratorApiResult<VerifiedMnemonicDto>> CreateAsync(
        string accessToken,
        CreateVerifiedMnemonicRequest body,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/curator/verified-mnemonics")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadOneAsync(response, ct);
    }

    public async Task<CuratorApiResult<VerifiedMnemonicDto>> UpdateAsync(
        string accessToken,
        Guid id,
        UpdateVerifiedMnemonicRequest body,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/curator/verified-mnemonics/{id}")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadOneAsync(response, ct);
    }

    public async Task<CuratorApiResult<VerifiedMnemonicDto>> SetEnabledAsync(
        string accessToken,
        Guid id,
        bool isEnabled,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/curator/verified-mnemonics/{id}/enabled")
        {
            Content = JsonContent.Create(new { isEnabled }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadOneAsync(response, ct);
    }

    public async Task<CuratorApiResult<LlmSettingsDto>> GetLlmSettingsAsync(
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/curator/llm/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadJsonAsync<LlmSettingsDto>(response, ct);
    }

    public async Task<CuratorApiResult<LlmSettingsDto>> UpdateLlmSettingsAsync(
        string accessToken,
        UpdateLlmSettingsBody body,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/curator/llm/settings")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadJsonAsync<LlmSettingsDto>(response, ct);
    }

    public async Task<CuratorApiResult<LlmUsagePageDto>> GetLlmUsageAsync(
        string accessToken,
        int take = 50,
        string? featureType = null,
        CancellationToken ct = default)
    {
        var url = $"/api/v1/curator/llm/usage?take={take}";
        if (!string.IsNullOrWhiteSpace(featureType))
        {
            url += $"&featureType={Uri.EscapeDataString(featureType)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadJsonAsync<LlmUsagePageDto>(response, ct);
    }

    public async Task<CuratorApiResult<IReadOnlyList<CuratorUserDto>>> ListUsersAsync(
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/curator/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct);
            return CuratorApiResult<IReadOnlyList<CuratorUserDto>>.Fail(
                err.Code,
                err.Message,
                (int)response.StatusCode);
        }

        var list = await response.Content.ReadFromJsonAsync<List<CuratorUserDto>>(JsonOptions, ct) ?? [];
        return CuratorApiResult<IReadOnlyList<CuratorUserDto>>.Ok(list);
    }

    public async Task<CuratorApiResult<CuratorUserDto>> SetDeveloperAsync(
        string accessToken,
        Guid userId,
        bool isDeveloper,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/curator/users/{userId}/developer")
        {
            Content = JsonContent.Create(new { isDeveloper }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        return await ReadJsonAsync<CuratorUserDto>(response, ct);
    }

    public async Task<CuratorApiResult<bool>> ResetUserPasswordAsync(
        string accessToken,
        Guid userId,
        string newPassword,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/curator/users/{userId}/reset-password")
        {
            Content = JsonContent.Create(new { newPassword }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct);
            return CuratorApiResult<bool>.Fail(err.Code, err.Message, (int)response.StatusCode);
        }

        return CuratorApiResult<bool>.Ok(true);
    }

    private async Task<CuratorApiResult<CuratorSession>> LoginAsync(string path, object body, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct);
            return CuratorApiResult<CuratorSession>.Fail(err.Code, err.Message, (int)response.StatusCode);
        }

        var dto = await response.Content.ReadFromJsonAsync<AuthSessionDto>(JsonOptions, ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
        {
            return CuratorApiResult<CuratorSession>.Fail("EMPTY_SESSION", "登入回應缺少 token。", (int)response.StatusCode);
        }

        var session = new CuratorSession(dto.UserId, dto.Email, dto.AccessToken, dto.IsDeveloper);
        return CuratorApiResult<CuratorSession>.Ok(session);
    }

    private static async Task<CuratorApiResult<VerifiedMnemonicDto>> ReadOneAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct);
            return CuratorApiResult<VerifiedMnemonicDto>.Fail(err.Code, err.Message, (int)response.StatusCode);
        }

        var dto = await response.Content.ReadFromJsonAsync<VerifiedMnemonicDto>(JsonOptions, ct);
        if (dto is null)
        {
            return CuratorApiResult<VerifiedMnemonicDto>.Fail("EMPTY_BODY", "回應為空。", (int)response.StatusCode);
        }

        return CuratorApiResult<VerifiedMnemonicDto>.Ok(dto);
    }

    private static async Task<CuratorApiResult<IReadOnlyList<VerifiedMnemonicDto>>> ReadListAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct);
            return CuratorApiResult<IReadOnlyList<VerifiedMnemonicDto>>.Fail(
                err.Code,
                err.Message,
                (int)response.StatusCode);
        }

        var list = await response.Content.ReadFromJsonAsync<List<VerifiedMnemonicDto>>(JsonOptions, ct)
            ?? [];
        return CuratorApiResult<IReadOnlyList<VerifiedMnemonicDto>>.Ok(list);
    }

    private static async Task<CuratorApiResult<T>> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var err = await TryReadErrorAsync(response, ct);
            return CuratorApiResult<T>.Fail(err.Code, err.Message, (int)response.StatusCode);
        }

        var dto = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        if (dto is null)
        {
            return CuratorApiResult<T>.Fail("EMPTY_BODY", "回應為空。", (int)response.StatusCode);
        }

        return CuratorApiResult<T>.Ok(dto);
    }

    private static async Task<(string Code, string Message)> TryReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            var err = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions, ct);
            if (err is not null && (!string.IsNullOrWhiteSpace(err.Code) || !string.IsNullOrWhiteSpace(err.Message)))
            {
                return (err.Code ?? "ERROR", err.Message ?? response.ReasonPhrase ?? "Request failed.");
            }
        }
        catch
        {
            // fall through
        }

        return ("HTTP_ERROR", response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}");
    }

    private sealed record AuthSessionDto(
        Guid UserId,
        string Email,
        string AccessToken,
        bool OnboardingCompleted,
        bool IsDeveloper);

    private sealed record ApiErrorDto(string? Code, string? Message);
}

public sealed class CuratorApiResult<T>
{
    public bool IsSuccess { get; private init; }
    public T? Value { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int StatusCode { get; private init; }

    public static CuratorApiResult<T> Ok(T value) => new()
    {
        IsSuccess = true,
        Value = value,
        StatusCode = 200
    };

    public static CuratorApiResult<T> Fail(string code, string message, int statusCode) => new()
    {
        IsSuccess = false,
        ErrorCode = code,
        ErrorMessage = message,
        StatusCode = statusCode
    };
}

public sealed record VerifiedMnemonicDto(
    Guid Id,
    string Language,
    string SourceText,
    string NormalizedSource,
    string DisplayText,
    string NotationText,
    string Explanation,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateVerifiedMnemonicRequest(
    string Language,
    string SourceText,
    string DisplayText,
    string NotationText,
    string Explanation,
    bool? IsEnabled = true);

public sealed record UpdateVerifiedMnemonicRequest(
    string DisplayText,
    string NotationText,
    string Explanation,
    bool? IsEnabled = null);

public sealed record LlmSettingsDto(
    string ApiKeyMasked,
    bool HasApiKeyConfigured,
    bool ApiKeyFromDatabase,
    string Model,
    bool ModelFromDatabase,
    string BaseUrl,
    bool BaseUrlFromDatabase,
    string ConfigModel,
    string ConfigBaseUrl);

public sealed record UpdateLlmSettingsBody(
    string? ApiKey,
    bool? ClearApiKey,
    string? Model,
    string? BaseUrl);

public sealed record LlmUsagePageDto(
    List<LlmUsageItemDto> Items,
    LlmUsageSummaryDto Today);

public sealed record LlmUsageItemDto(
    Guid Id,
    Guid? UserId,
    string FeatureType,
    string Model,
    string Provider,
    int? PromptTokens,
    int? CompletionTokens,
    int LatencyMs,
    bool Success,
    string? ErrorCode,
    decimal EstimatedCostNtd,
    DateTimeOffset CreatedAtUtc);

public sealed record LlmUsageSummaryDto(
    int Count,
    long PromptTokens,
    long CompletionTokens,
    decimal EstimatedCostNtd);

public sealed record CuratorUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsDeveloper,
    int DailyQuota,
    bool OnboardingCompleted,
    bool HasPassword,
    DateTimeOffset CreatedAtUtc,
    int LoginCount,
    DateTimeOffset? LastLoginAtUtc);
