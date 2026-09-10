using System.Security.Claims;
using Soraeru.Application.Curator;

namespace Soraeru.Api.Endpoints;

public static class CuratorLlmAdminEndpoints
{
    public static RouteGroupBuilder MapCuratorLlmAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/curator/llm")
            .WithTags("CuratorLlm")
            .RequireAuthorization();

        group.MapGet("/settings", async (
            ILlmAdminService admin,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var id = ResolveUserId(user);
            if (id is null)
            {
                return Results.Unauthorized();
            }

            var result = await admin.GetSettingsAsync(id.Value, ct);
            return ToHttp(result, dto => Results.Ok(ToSettingsResponse(dto)));
        });

        group.MapPut("/settings", async (
            UpdateLlmSettingsRequest body,
            ILlmAdminService admin,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var id = ResolveUserId(user);
            if (id is null)
            {
                return Results.Unauthorized();
            }

            var result = await admin.UpdateSettingsAsync(
                new UpdateLlmSettingsCommand(
                    id.Value,
                    body.ApiKey,
                    body.ClearApiKey ?? false,
                    body.Model,
                    body.BaseUrl),
                ct);
            return ToHttp(result, dto => Results.Ok(ToSettingsResponse(dto)));
        });

        group.MapGet("/usage", async (
            int? take,
            string? featureType,
            ILlmAdminService admin,
            ClaimsPrincipal user,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var id = ResolveUserId(user);
            if (id is null)
            {
                return Results.Unauthorized();
            }

            try
            {
                var result = await admin.ListUsageAsync(id.Value, take ?? 50, featureType, ct);
                // #region agent log
                Console.WriteLine(
                    $"AGENT_DEBUG {System.Text.Json.JsonSerializer.Serialize(new
                    {
                        sessionId = "1a7969",
                        runId = "post-fix",
                        hypothesisId = "H-usage-sqlite",
                        location = "CuratorLlmAdminEndpoints:usage",
                        message = "API LLM usage result",
                        data = new
                        {
                            ok = result.IsSuccess,
                            code = result.ErrorCode,
                            itemCount = result.Value?.Items.Count
                        },
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    })}");
                // #endregion
                return ToHttp(result, page => Results.Ok(new
                {
                    items = page.Items.Select(ToUsageItem).ToList(),
                    today = new
                    {
                        count = page.Today.Count,
                        promptTokens = page.Today.PromptTokens,
                        completionTokens = page.Today.CompletionTokens,
                        estimatedCostNtd = page.Today.EstimatedCostNtd
                    }
                }));
            }
            catch (Exception ex)
            {
                // #region agent log
                Console.WriteLine(
                    $"AGENT_DEBUG {System.Text.Json.JsonSerializer.Serialize(new
                    {
                        sessionId = "1a7969",
                        runId = "post-fix",
                        hypothesisId = "H-usage-sqlite",
                        location = "CuratorLlmAdminEndpoints:usage-catch",
                        message = "API LLM usage exception",
                        data = new
                        {
                            exceptionType = ex.GetType().FullName,
                            exceptionMessage = ex.Message
                        },
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    })}");
                // #endregion
                loggerFactory.CreateLogger("CuratorLlmAdmin").LogError(ex, "LLM usage failed");
                return Results.Json(
                    new ErrorResponse("USAGE_QUERY_FAILED", $"{ex.GetType().Name}: {ex.Message}"),
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return group;
    }

    private static object ToSettingsResponse(LlmSettingsView dto) => new
    {
        apiKeyMasked = dto.ApiKeyMasked,
        hasApiKeyConfigured = dto.HasApiKeyConfigured,
        apiKeyFromDatabase = dto.ApiKeyFromDatabase,
        model = dto.Model,
        modelFromDatabase = dto.ModelFromDatabase,
        baseUrl = dto.BaseUrl,
        baseUrlFromDatabase = dto.BaseUrlFromDatabase,
        configModel = dto.ConfigModel,
        configBaseUrl = dto.ConfigBaseUrl
    };

    private static object ToUsageItem(LlmUsageItem x) => new
    {
        id = x.Id,
        userId = x.UserId,
        featureType = x.FeatureType,
        model = x.Model,
        provider = x.Provider,
        promptTokens = x.PromptTokens,
        completionTokens = x.CompletionTokens,
        latencyMs = x.LatencyMs,
        success = x.Success,
        errorCode = x.ErrorCode,
        estimatedCostNtd = x.EstimatedCostNtd,
        createdAtUtc = x.CreatedAtUtc
    };

    private static IResult ToHttp<T>(
        Application.Common.ServiceResult<T> result,
        Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return onSuccess(result.Value);
        }

        var status = result.ErrorCode switch
        {
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            new ErrorResponse(result.ErrorCode ?? "ERROR", result.ErrorMessage ?? "Request failed."),
            statusCode: status);
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(claim, out var fromClaim) ? fromClaim : null;
    }
}

public sealed record UpdateLlmSettingsRequest(
    string? ApiKey,
    bool? ClearApiKey,
    string? Model,
    string? BaseUrl);
