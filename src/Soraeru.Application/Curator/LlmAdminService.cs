using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;

namespace Soraeru.Application.Curator;

public interface ILlmAdminService
{
    Task<ServiceResult<LlmSettingsView>> GetSettingsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LlmSettingsView>> UpdateSettingsAsync(
        UpdateLlmSettingsCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<LlmUsagePage>> ListUsageAsync(
        Guid actorUserId,
        int take,
        string? featureType,
        CancellationToken cancellationToken = default);
}

public sealed record UpdateLlmSettingsCommand(
    Guid ActorUserId,
    /// <summary>null = leave unchanged; non-empty = set override.</summary>
    string? ApiKey,
    bool ClearApiKey,
    /// <summary>null = leave unchanged; empty = clear override (use config).</summary>
    string? Model,
    /// <summary>null = leave unchanged; empty = clear override (use config).</summary>
    string? BaseUrl);

public sealed record LlmSettingsView(
    string ApiKeyMasked,
    bool HasApiKeyConfigured,
    bool ApiKeyFromDatabase,
    string Model,
    bool ModelFromDatabase,
    string BaseUrl,
    bool BaseUrlFromDatabase,
    string ConfigModel,
    string ConfigBaseUrl);

public sealed record LlmUsagePage(
    IReadOnlyList<LlmUsageItem> Items,
    LlmUsageSummary Today);

public sealed record LlmUsageItem(
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

public sealed record LlmUsageSummary(
    int Count,
    long PromptTokens,
    long CompletionTokens,
    decimal EstimatedCostNtd);

public sealed class LlmAdminService : ILlmAdminService
{
    private readonly IUserRepository _users;
    private readonly IDeveloperAccountPolicy _curatorPolicy;
    private readonly ILlmSettingsResolver _resolver;
    private readonly ILlmRuntimeSettingsRepository _runtime;
    private readonly ILlmUsageRepository _usage;

    public LlmAdminService(
        IUserRepository users,
        IDeveloperAccountPolicy curatorPolicy,
        ILlmSettingsResolver resolver,
        ILlmRuntimeSettingsRepository runtime,
        ILlmUsageRepository usage)
    {
        _users = users;
        _curatorPolicy = curatorPolicy;
        _resolver = resolver;
        _runtime = runtime;
        _usage = usage;
    }

    public async Task<ServiceResult<LlmSettingsView>> GetSettingsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<LlmSettingsView>.Failure(auth.Value.Code, auth.Value.Message);
        }

        var effective = await _resolver.ResolveAsync(cancellationToken);
        return ServiceResult<LlmSettingsView>.Success(ToView(effective));
    }

    public async Task<ServiceResult<LlmSettingsView>> UpdateSettingsAsync(
        UpdateLlmSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(command.ActorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<LlmSettingsView>.Failure(auth.Value.Code, auth.Value.Message);
        }

        var user = await _users.FindByIdAsync(command.ActorUserId, cancellationToken);
        var existing = await _runtime.GetAsync(cancellationToken);
        var apiKey = existing?.ApiKey;
        var keyBranch = "unchanged";
        if (command.ClearApiKey)
        {
            apiKey = null;
            keyBranch = "cleared";
        }
        else if (!string.IsNullOrWhiteSpace(command.ApiKey))
        {
            apiKey = command.ApiKey.Trim();
            keyBranch = "set_override";
        }

        var model = existing?.Model;
        if (command.Model is not null)
        {
            model = string.IsNullOrWhiteSpace(command.Model) ? null : command.Model.Trim();
        }

        var baseUrl = existing?.BaseUrl;
        if (command.BaseUrl is not null)
        {
            baseUrl = string.IsNullOrWhiteSpace(command.BaseUrl) ? null : command.BaseUrl.Trim().TrimEnd('/');
        }

        // #region agent log
        AgentDebugLog.Write(
            "H-A,H-B,H-C",
            "LlmAdminService.cs:UpdateSettingsAsync:pre-upsert",
            "LLM settings update branch",
            new
            {
                clearApiKey = command.ClearApiKey,
                hasIncomingApiKey = !string.IsNullOrWhiteSpace(command.ApiKey),
                incomingApiKeyLength = string.IsNullOrWhiteSpace(command.ApiKey) ? 0 : command.ApiKey.Trim().Length,
                hadDbApiKeyBefore = !string.IsNullOrWhiteSpace(existing?.ApiKey),
                keyBranch,
                willPersistDbApiKey = !string.IsNullOrWhiteSpace(apiKey),
                model = model,
                baseUrl = baseUrl,
                writesRailwayEnv = false
            });
        // #endregion

        await _runtime.UpsertAsync(
            new LlmRuntimeSettingsRecord(
                apiKey,
                model,
                baseUrl,
                DateTimeOffset.UtcNow,
                user?.Email),
            cancellationToken);

        var effective = await _resolver.ResolveAsync(cancellationToken);
        var view = ToView(effective);

        // #region agent log
        AgentDebugLog.Write(
            "H-A,H-D,H-E",
            "LlmAdminService.cs:UpdateSettingsAsync:post-resolve",
            "LLM effective settings after save",
            new
            {
                apiKeyMasked = view.ApiKeyMasked,
                apiKeyFromDatabase = view.ApiKeyFromDatabase,
                hasApiKeyConfigured = view.HasApiKeyConfigured,
                model = view.Model,
                modelFromDatabase = view.ModelFromDatabase,
                baseUrl = view.BaseUrl,
                baseUrlFromDatabase = view.BaseUrlFromDatabase,
                configModel = view.ConfigModel,
                configBaseUrl = view.ConfigBaseUrl
            });
        // #endregion

        return ServiceResult<LlmSettingsView>.Success(view);
    }

    public async Task<ServiceResult<LlmUsagePage>> ListUsageAsync(
        Guid actorUserId,
        int take,
        string? featureType,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<LlmUsagePage>.Failure(auth.Value.Code, auth.Value.Message);
        }

        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await _usage.ListRecentAsync(take, featureType, cancellationToken);
        var since = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var summary = await _usage.SummarizeSinceAsync(since, featureType, cancellationToken);

        return ServiceResult<LlmUsagePage>.Success(
            new LlmUsagePage(
                items.Select(x => new LlmUsageItem(
                    x.Id,
                    x.UserId,
                    x.FeatureType,
                    x.Model,
                    x.Provider,
                    x.PromptTokens,
                    x.CompletionTokens,
                    x.LatencyMs,
                    x.Success,
                    x.ErrorCode,
                    x.EstimatedCostNtd,
                    x.CreatedAtUtc)).ToList(),
                new LlmUsageSummary(
                    summary.Count,
                    summary.PromptTokens,
                    summary.CompletionTokens,
                    summary.EstimatedCostNtd)));
    }

    private static LlmSettingsView ToView(LlmEffectiveSettings effective) =>
        new(
            ApiKeyMask.Mask(effective.ApiKey),
            ApiKeyMask.LooksConfigured(effective.ApiKey),
            effective.ApiKeyFromDatabase,
            effective.Model,
            effective.ModelFromDatabase,
            effective.BaseUrl,
            effective.BaseUrlFromDatabase,
            effective.ConfigModel,
            effective.ConfigBaseUrl);

    private async Task<(string Code, string Message)?> EnsureCuratorAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
        {
            return ("VALIDATION", "User id is required.");
        }

        var user = await _users.FindByIdAsync(actorUserId, cancellationToken);
        if (user is null)
        {
            return ("NOT_FOUND", "使用者不存在。");
        }

        if (!(user.IsDeveloper || _curatorPolicy.IsDeveloperEmail(user.Email)))
        {
            return ("FORBIDDEN", "僅策展授權帳號可管理 LLM 設定與用量。");
        }

        return null;
    }
}
