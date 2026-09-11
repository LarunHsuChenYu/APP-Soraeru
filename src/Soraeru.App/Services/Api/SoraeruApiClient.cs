using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Soraeru.Services.Interfaces;

namespace Soraeru.Services.Api;

/// <summary>
/// Typed API client. Base address comes from MauiProgram configuration.
/// </summary>
public sealed class SoraeruApiClient : ISoraeruApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public SoraeruApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<AuthResult> LoginWithEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        PostAuthAsync("api/v1/auth/login", new { email, password, client = "app" }, cancellationToken);

    public Task<AuthResult> LoginWithGoogleAsync(
        string idToken,
        CancellationToken cancellationToken = default) =>
        PostAuthAsync("api/v1/auth/google", new { idToken, client = "app" }, cancellationToken);

    public Task<AuthResult> RegisterWithEmailAsync(
        string email,
        string password,
        string? displayName = null,
        CancellationToken cancellationToken = default) =>
        PostAuthAsync("api/v1/auth/register", new { email, password, displayName }, cancellationToken);

    public async Task<bool> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/v1/auth/forgot-password",
            new { email },
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public async Task<MeProfileDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("api/v1/me", cancellationToken);

        // Auth rejected → null (callers clear session). Other HTTP failures throw so token is kept.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GET /me failed with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return await response.Content.ReadFromJsonAsync<MeProfileDto>(JsonOptions, cancellationToken);
    }

    public async Task<MeProfileDto?> PatchMeAsync(
        bool? onboardingCompleted = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PatchAsJsonAsync(
            "api/v1/me",
            new { onboardingCompleted },
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"PATCH /me failed with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return await response.Content.ReadFromJsonAsync<MeProfileDto>(JsonOptions, cancellationToken);
    }

    public async Task<AnalyzeApiResult> AnalyzeWordAsync(
        AnalyzeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/v1/word/analyze",
                new
                {
                    text = request.Text,
                    sourceLanguage = request.SourceLanguage,
                    memoryLanguage = request.MemoryLanguage,
                    notationPreference = request.NotationPreference,
                    forceRefresh = request.ForceRefresh
                },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AnalyzeResultDto>(JsonOptions, cancellationToken);
                return result is null
                    ? AnalyzeApiResult.Fail(AnalyzeFailureKind.ServerError, "伺服器回傳格式異常。")
                    : AnalyzeApiResult.Success(result);
            }

            var apiError = await TryReadErrorAsync(response, cancellationToken);
            var code = apiError?.Code;
            var message = apiError?.Message;

            if (string.Equals(code, "REGENERATION_LIMIT_EXCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.RegenerationLimit,
                    message ?? "同一單字最多重新產生 3 次。");
            }

            if (string.Equals(code, "QUOTA_EXCEEDED", StringComparison.OrdinalIgnoreCase)
                || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.QuotaExceeded,
                    message ?? "今日分析次數已用完。");
            }

            if (string.Equals(code, "HARD_GATE_FAILED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "SCHEMA_INVALID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "ANALYZE_FAILED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "UNANALYZABLE", StringComparison.OrdinalIgnoreCase))
            {
                return AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.AnalyzeFailed,
                    message ?? "分析失敗，請稍後再試。");
            }

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.Unauthorized,
                    message ?? "請重新登入。"),
                HttpStatusCode.ServiceUnavailable => AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.LlmNotConfigured,
                    message ?? "伺服器尚未設定 LLM。"),
                HttpStatusCode.BadRequest => AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.Validation,
                    message ?? "輸入無法分析。"),
                _ => AnalyzeApiResult.Fail(
                    AnalyzeFailureKind.ServerError,
                    message ?? $"分析失敗（{(int)response.StatusCode}）。")
            };
        }
        catch (HttpRequestException ex)
        {
            return AnalyzeApiResult.Fail(
                AnalyzeFailureKind.Network,
                $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AnalyzeApiResult.Fail(AnalyzeFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<NotebookApiResult> SaveNotebookCardAsync(
        SaveNotebookCardRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "api/v1/notebook",
                new
                {
                    sourceText = request.SourceText,
                    detectedLanguage = request.DetectedLanguage,
                    meaningZh = request.MeaningZh,
                    pronunciation = request.Pronunciation,
                    selectedMnemonic = request.SelectedMnemonic
                },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var card = await response.Content.ReadFromJsonAsync<NotebookCardDto>(JsonOptions, cancellationToken);
                return card is null
                    ? NotebookApiResult.Fail(NotebookFailureKind.ServerError, "伺服器回傳格式異常。")
                    : NotebookApiResult.Success(card);
            }

            return MapNotebookFailure(response, (await TryReadErrorAsync(response, cancellationToken))?.Message);
        }
        catch (HttpRequestException ex)
        {
            return NotebookApiResult.Fail(NotebookFailureKind.Network, $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NotebookApiResult.Fail(NotebookFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<NotebookListApiResult> ListNotebookCardsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("api/v1/notebook", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var cards = await response.Content.ReadFromJsonAsync<List<NotebookCardDto>>(JsonOptions, cancellationToken);
                return NotebookListApiResult.Success(cards ?? []);
            }

            var message = (await TryReadErrorAsync(response, cancellationToken))?.Message;
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => NotebookListApiResult.Fail(
                    NotebookFailureKind.Unauthorized,
                    message ?? "請重新登入。"),
                _ => NotebookListApiResult.Fail(
                    NotebookFailureKind.ServerError,
                    message ?? $"讀取單字本失敗（{(int)response.StatusCode}）。")
            };
        }
        catch (HttpRequestException ex)
        {
            return NotebookListApiResult.Fail(NotebookFailureKind.Network, $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NotebookListApiResult.Fail(NotebookFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<NotebookApiResult> GetNotebookCardAsync(
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync($"api/v1/notebook/{cardId:D}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var card = await response.Content.ReadFromJsonAsync<NotebookCardDto>(JsonOptions, cancellationToken);
                return card is null
                    ? NotebookApiResult.Fail(NotebookFailureKind.ServerError, "伺服器回傳格式異常。")
                    : NotebookApiResult.Success(card);
            }

            return MapNotebookFailure(response, (await TryReadErrorAsync(response, cancellationToken))?.Message);
        }
        catch (HttpRequestException ex)
        {
            return NotebookApiResult.Fail(NotebookFailureKind.Network, $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NotebookApiResult.Fail(NotebookFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<NotebookActionApiResult> DeleteNotebookCardAsync(
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.DeleteAsync($"api/v1/notebook/{cardId:D}", cancellationToken);
            if (response.IsSuccessStatusCode)
                return NotebookActionApiResult.Success();

            var message = (await TryReadErrorAsync(response, cancellationToken))?.Message;
            var fail = MapNotebookFailure(response, message);
            return NotebookActionApiResult.Fail(fail.Failure, fail.Message ?? "刪除失敗。");
        }
        catch (HttpRequestException ex)
        {
            return NotebookActionApiResult.Fail(NotebookFailureKind.Network, $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NotebookActionApiResult.Fail(NotebookFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<NotebookMirrorPullApiResult> PullNotebookMirrorAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("api/v1/notebook/mirror", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var cards = await response.Content.ReadFromJsonAsync<List<NotebookMirrorCardDto>>(
                    JsonOptions,
                    cancellationToken);
                return NotebookMirrorPullApiResult.Success(cards ?? []);
            }

            var message = (await TryReadErrorAsync(response, cancellationToken))?.Message;
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => NotebookMirrorPullApiResult.Fail(
                    NotebookFailureKind.Unauthorized,
                    message ?? "請重新登入。"),
                _ => NotebookMirrorPullApiResult.Fail(
                    NotebookFailureKind.ServerError,
                    message ?? $"讀取雲端鏡像失敗（{(int)response.StatusCode}）。")
            };
        }
        catch (HttpRequestException ex)
        {
            return NotebookMirrorPullApiResult.Fail(NotebookFailureKind.Network, $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NotebookMirrorPullApiResult.Fail(NotebookFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<NotebookActionApiResult> PushNotebookMirrorAsync(
        IReadOnlyList<NotebookMirrorCardDto> cards,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PutAsJsonAsync(
                "api/v1/notebook/mirror",
                cards,
                cancellationToken);

            if (response.IsSuccessStatusCode)
                return NotebookActionApiResult.Success();

            var message = (await TryReadErrorAsync(response, cancellationToken))?.Message;
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => NotebookActionApiResult.Fail(
                    NotebookFailureKind.Unauthorized,
                    message ?? "請重新登入。"),
                HttpStatusCode.Forbidden => NotebookActionApiResult.Fail(
                    NotebookFailureKind.Unauthorized,
                    message ?? "無法寫入他人鏡像。"),
                HttpStatusCode.BadRequest => NotebookActionApiResult.Fail(
                    NotebookFailureKind.Validation,
                    message ?? "鏡像推送資料無效。"),
                _ => NotebookActionApiResult.Fail(
                    NotebookFailureKind.ServerError,
                    message ?? $"推送雲端鏡像失敗（{(int)response.StatusCode}）。")
            };
        }
        catch (HttpRequestException ex)
        {
            return NotebookActionApiResult.Fail(NotebookFailureKind.Network, $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NotebookActionApiResult.Fail(NotebookFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    public async Task<DeleteAccountApiClientResult> DeleteAccountAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.DeleteAsync("api/v1/me", cancellationToken);
            if (response.IsSuccessStatusCode)
                return DeleteAccountApiClientResult.Success();

            var message = (await TryReadErrorAsync(response, cancellationToken))?.Message;
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => DeleteAccountApiClientResult.Fail(
                    DeleteAccountFailureKind.Unauthorized,
                    message ?? "請重新登入。"),
                _ => DeleteAccountApiClientResult.Fail(
                    DeleteAccountFailureKind.ServerError,
                    message ?? $"刪除帳號失敗（{(int)response.StatusCode}）。")
            };
        }
        catch (HttpRequestException ex)
        {
            return DeleteAccountApiClientResult.Fail(
                DeleteAccountFailureKind.Network,
                $"無法連線 API。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DeleteAccountApiClientResult.Fail(DeleteAccountFailureKind.Network, "連線逾時，請稍後再試。");
        }
    }

    private static NotebookApiResult MapNotebookFailure(HttpResponseMessage response, string? apiError) =>
        response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => NotebookApiResult.Fail(
                NotebookFailureKind.Unauthorized,
                apiError ?? "請重新登入。"),
            HttpStatusCode.NotFound => NotebookApiResult.Fail(
                NotebookFailureKind.NotFound,
                apiError ?? "找不到這張單字卡。"),
            HttpStatusCode.BadRequest => NotebookApiResult.Fail(
                NotebookFailureKind.Validation,
                apiError ?? "無法儲存單字卡。"),
            _ => NotebookApiResult.Fail(
                NotebookFailureKind.ServerError,
                apiError ?? $"單字本操作失敗（{(int)response.StatusCode}）。")
        };

    private async Task<AuthResult> PostAuthAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(path, body, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var session = await response.Content.ReadFromJsonAsync<AuthSessionDto>(JsonOptions, cancellationToken);
                return session is null
                    ? AuthResult.Fail(AuthFailureKind.ServerRejected, "伺服器回傳格式異常。")
                    : AuthResult.Success(session);
            }

            var apiError = await TryReadErrorAsync(response, cancellationToken);
            var message = apiError?.Message;
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => AuthResult.Fail(
                    AuthFailureKind.InvalidCredentials,
                    message ?? "Email 或密碼不正確。"),
                HttpStatusCode.Conflict => AuthResult.Fail(
                    AuthFailureKind.Conflict,
                    message ?? "此 Email 已被使用。"),
                HttpStatusCode.ServiceUnavailable => AuthResult.Fail(
                    AuthFailureKind.ServerRejected,
                    message ?? "Google 登入尚未在伺服器設定完成。"),
                _ => AuthResult.Fail(
                    AuthFailureKind.ServerRejected,
                    message ?? $"伺服器拒絕請求（{(int)response.StatusCode}）。")
            };
        }
        catch (HttpRequestException ex)
        {
            return AuthResult.Fail(
                AuthFailureKind.Network,
                $"無法連線 API，請確認服務已啟動。\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthResult.Fail(AuthFailureKind.Network, "連線逾時，請確認 API 已啟動。");
        }
    }

    private static async Task<ApiErrorDto?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    private sealed record ApiErrorDto(string? Code, string? Message);
}
