using Soraeru.Application.Auth;

namespace Soraeru.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest body, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RegisterWithEmailAsync(
                new RegisterEmailCommand(body.Email, body.Password, body.DisplayName),
                ct);
            return ToHttp(result, session => Results.Ok(ToResponse(session)));
        });

        group.MapPost("/login", async (LoginRequest body, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginWithEmailAsync(
                new LoginEmailCommand(body.Email, body.Password, ParseAuthClient(body.Client)),
                ct);
            return ToHttp(result, session => Results.Ok(ToResponse(session)));
        });

        group.MapPost("/google", async (GoogleLoginRequest body, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginWithGoogleAsync(
                new LoginGoogleCommand(body.IdToken, ParseAuthClient(body.Client)),
                ct);
            return ToHttp(result, session => Results.Ok(ToResponse(session)));
        });

        group.MapPost("/forgot-password", async (ForgotPasswordRequest body, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.RequestPasswordResetAsync(body.Email, ct);
            return ToHttp(result, _ => Results.Accepted());
        });

        group.MapPost("/reset-password", async (ResetPasswordRequest body, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.ResetPasswordAsync(
                new ResetPasswordCommand(body.Token, body.NewPassword),
                ct);
            return ToHttp(result, _ => Results.NoContent());
        });

        return group;
    }

    /// <summary>
    /// Maps request <c>client</c> to <see cref="AuthClient"/>. Unknown / missing → App (backward compatible).
    /// </summary>
    internal static AuthClient ParseAuthClient(string? client) =>
        string.Equals(client?.Trim(), "curator", StringComparison.OrdinalIgnoreCase)
            ? AuthClient.Curator
            : AuthClient.App;

    private static AuthSessionResponse ToResponse(AuthSession session) =>
        new(session.UserId, session.Email, session.AccessToken, session.OnboardingCompleted, session.IsDeveloper);

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
            "INVALID_CREDENTIALS" => StatusCodes.Status401Unauthorized,
            "EMAIL_TAKEN" => StatusCodes.Status409Conflict,
            "EMAIL_TAKEN_GOOGLE" => StatusCodes.Status409Conflict,
            "GOOGLE_SUBJECT_CONFLICT" => StatusCodes.Status409Conflict,
            "INVALID_TOKEN" => StatusCodes.Status400BadRequest,
            "GOOGLE_AUTH_NOT_CONFIGURED" => StatusCodes.Status503ServiceUnavailable,
            "GOOGLE_TOKEN_INVALID" => StatusCodes.Status401Unauthorized,
            "GOOGLE_EMAIL_REQUIRED" => StatusCodes.Status400BadRequest,
            "AUTH_NOT_IMPLEMENTED" => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(
            new ErrorResponse(result.ErrorCode ?? "ERROR", result.ErrorMessage ?? "Request failed."),
            statusCode: status);
    }
}

public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);

public sealed record LoginRequest(string Email, string Password, string? Client = null);

public sealed record GoogleLoginRequest(string IdToken, string? Client = null);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record AuthSessionResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    bool OnboardingCompleted,
    bool IsDeveloper);

public sealed record ErrorResponse(string Code, string Message);
