using System.Security.Claims;
using Soraeru.Application.Curator;

namespace Soraeru.Api.Endpoints;

public static class CuratorUserAdminEndpoints
{
    public static RouteGroupBuilder MapCuratorUserAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/curator/users")
            .WithTags("CuratorUsers")
            .RequireAuthorization();

        group.MapGet("/", async (
            ICuratorUserAdminService admin,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var id = ResolveUserId(user);
            if (id is null)
            {
                return Results.Unauthorized();
            }

            var result = await admin.ListUsersAsync(id.Value, ct);
            return ToHttp(result, items => Results.Ok(items.Select(ToResponse).ToList()));
        });

        group.MapPatch("/{userId:guid}/developer", async (
            Guid userId,
            SetDeveloperRequest body,
            ICuratorUserAdminService admin,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var id = ResolveUserId(user);
            if (id is null)
            {
                return Results.Unauthorized();
            }

            var result = await admin.SetDeveloperAsync(id.Value, userId, body.IsDeveloper, ct);
            return ToHttp(result, dto => Results.Ok(ToResponse(dto)));
        });

        group.MapPost("/{userId:guid}/reset-password", async (
            Guid userId,
            AdminResetPasswordRequest body,
            ICuratorUserAdminService admin,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var id = ResolveUserId(user);
            if (id is null)
            {
                return Results.Unauthorized();
            }

            var result = await admin.ResetPasswordAsync(id.Value, userId, body.NewPassword ?? "", ct);
            return ToHttp(result, _ => Results.Ok(new { ok = true }));
        });

        return group;
    }

    private static object ToResponse(CuratorUserListItem dto) => new
    {
        id = dto.Id,
        email = dto.Email,
        displayName = dto.DisplayName,
        isDeveloper = dto.IsDeveloper,
        dailyQuota = dto.DailyQuota,
        onboardingCompleted = dto.OnboardingCompleted,
        hasPassword = dto.HasPassword,
        createdAtUtc = dto.CreatedAtUtc
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

public sealed record SetDeveloperRequest(bool IsDeveloper);

public sealed record AdminResetPasswordRequest(string? NewPassword);
