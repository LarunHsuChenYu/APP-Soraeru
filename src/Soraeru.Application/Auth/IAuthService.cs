using Soraeru.Application.Common;

namespace Soraeru.Application.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthSession>> RegisterWithEmailAsync(
        RegisterEmailCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthSession>> LoginWithEmailAsync(
        LoginEmailCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthSession>> LoginWithGoogleAsync(
        LoginGoogleCommand command,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ResetPasswordAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record RegisterEmailCommand(string Email, string Password, string? DisplayName = null);

/// <summary>
/// Which product surface initiated auth. Only <see cref="App"/> updates login stats.
/// </summary>
public enum AuthClient
{
    App = 0,
    Curator = 1
}

public sealed record LoginEmailCommand(
    string Email,
    string Password,
    AuthClient Client = AuthClient.App);

public sealed record LoginGoogleCommand(
    string IdToken,
    AuthClient Client = AuthClient.App);

public sealed record ResetPasswordCommand(string Token, string NewPassword);

public sealed record AuthSession(
    Guid UserId,
    string Email,
    string AccessToken,
    bool OnboardingCompleted,
    bool IsDeveloper);
