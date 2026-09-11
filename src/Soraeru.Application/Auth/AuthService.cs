using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;

namespace Soraeru.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IPasswordResetTokenStore _resetTokens;
    private readonly IDeveloperAccountPolicy _developers;
    private readonly IGoogleIdTokenValidator _googleTokens;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IEmailSender email,
        IPasswordResetTokenStore resetTokens,
        IDeveloperAccountPolicy developers,
        IGoogleIdTokenValidator googleTokens)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _email = email;
        _resetTokens = resetTokens;
        _developers = developers;
        _googleTokens = googleTokens;
    }

    public async Task<ServiceResult<AuthSession>> RegisterWithEmailAsync(
        RegisterEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(command.Email);
        if (email is null || string.IsNullOrWhiteSpace(command.Password))
        {
            return ServiceResult<AuthSession>.Failure("VALIDATION", "Email and password are required.");
        }

        if (command.Password.Length < 8)
        {
            return ServiceResult<AuthSession>.Failure("VALIDATION", "Password must be at least 8 characters.");
        }

        var existing = await _users.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            if (string.IsNullOrEmpty(existing.PasswordHash)
                && !string.IsNullOrEmpty(existing.GoogleSubject))
            {
                return ServiceResult<AuthSession>.Failure(
                    "EMAIL_TAKEN_GOOGLE",
                    "此 Email 已使用 Google 登入建立帳號，請改用「使用 Google 登入」，無法再以密碼註冊覆蓋。");
            }

            return ServiceResult<AuthSession>.Failure("EMAIL_TAKEN", "An account with this email already exists.");
        }

        var isDeveloper = _developers.IsDeveloperEmail(email);
        var displayName = string.IsNullOrWhiteSpace(command.DisplayName)
            ? email.Split('@')[0]
            : command.DisplayName.Trim();

        var user = new UserRecord(
            Id: Guid.NewGuid(),
            Email: email,
            PasswordHash: _passwordHasher.HashPassword(command.Password),
            GoogleSubject: null,
            DisplayName: displayName,
            PlanTier: AppConstants.PlanTierFree,
            DailyQuota: isDeveloper ? AppConstants.UnlimitedDailyQuota : AppConstants.FreeDailyQuota,
            NotationPref: AppConstants.DefaultNotationPref,
            IsDeveloper: isDeveloper,
            OnboardingCompleted: false,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        await _users.AddAsync(user, cancellationToken);
        return ServiceResult<AuthSession>.Success(ToSession(user));
    }

    public async Task<ServiceResult<AuthSession>> LoginWithEmailAsync(
        LoginEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(command.Email);
        if (email is null || string.IsNullOrWhiteSpace(command.Password))
        {
            return ServiceResult<AuthSession>.Failure("VALIDATION", "Email and password are required.");
        }

        var user = await _users.FindByEmailAsync(email, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return ServiceResult<AuthSession>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");
        }

        if (!_passwordHasher.VerifyHashedPassword(user.PasswordHash, command.Password))
        {
            return ServiceResult<AuthSession>.Failure("INVALID_CREDENTIALS", "Invalid email or password.");
        }

        user = await AfterSuccessfulAuthAsync(user, command.Client, cancellationToken);
        return ServiceResult<AuthSession>.Success(ToSession(user));
    }

    public async Task<ServiceResult<AuthSession>> LoginWithGoogleAsync(
        LoginGoogleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.IdToken))
        {
            return ServiceResult<AuthSession>.Failure("VALIDATION", "Google id token is required.");
        }

        var validated = await _googleTokens.ValidateAsync(command.IdToken.Trim(), cancellationToken);
        if (!validated.IsSuccess || validated.Payload is null)
        {
            return ServiceResult<AuthSession>.Failure(
                validated.ErrorCode ?? "GOOGLE_TOKEN_INVALID",
                validated.ErrorMessage ?? "Google id token validation failed.");
        }

        var payload = validated.Payload;
        var email = NormalizeEmail(payload.Email);
        if (email is null)
        {
            return ServiceResult<AuthSession>.Failure(
                "GOOGLE_EMAIL_REQUIRED",
                "Google 帳號未提供 Email，無法登入。請改用有 Email 的 Google 帳號。");
        }

        var bySubject = await _users.FindByGoogleSubjectAsync(payload.Subject, cancellationToken);
        if (bySubject is not null)
        {
            bySubject = await AfterSuccessfulAuthAsync(bySubject, command.Client, cancellationToken);
            return ServiceResult<AuthSession>.Success(ToSession(bySubject));
        }

        var byEmail = await _users.FindByEmailAsync(email, cancellationToken);
        if (byEmail is not null)
        {
            if (!string.IsNullOrEmpty(byEmail.GoogleSubject)
                && !string.Equals(byEmail.GoogleSubject, payload.Subject, StringComparison.Ordinal))
            {
                return ServiceResult<AuthSession>.Failure(
                    "GOOGLE_SUBJECT_CONFLICT",
                    "此 Email 已綁定其他 Google 帳號。");
            }

            var displayName = string.IsNullOrWhiteSpace(byEmail.DisplayName) && !string.IsNullOrWhiteSpace(payload.Name)
                ? payload.Name.Trim()
                : byEmail.DisplayName;

            var bound = byEmail with
            {
                GoogleSubject = payload.Subject,
                DisplayName = displayName
            };
            await _users.UpdateAsync(bound, cancellationToken);
            bound = await AfterSuccessfulAuthAsync(bound, command.Client, cancellationToken);
            return ServiceResult<AuthSession>.Success(ToSession(bound));
        }

        var isDeveloper = _developers.IsDeveloperEmail(email);
        var name = string.IsNullOrWhiteSpace(payload.Name)
            ? email.Split('@')[0]
            : payload.Name.Trim();

        var created = new UserRecord(
            Id: Guid.NewGuid(),
            Email: email,
            PasswordHash: null,
            GoogleSubject: payload.Subject,
            DisplayName: name,
            PlanTier: AppConstants.PlanTierFree,
            DailyQuota: isDeveloper ? AppConstants.UnlimitedDailyQuota : AppConstants.FreeDailyQuota,
            NotationPref: AppConstants.DefaultNotationPref,
            IsDeveloper: isDeveloper,
            OnboardingCompleted: false,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            LoginCount: 0,
            LastLoginAtUtc: null);

        await _users.AddAsync(created, cancellationToken);
        created = await AfterSuccessfulAuthAsync(created, command.Client, cancellationToken);
        return ServiceResult<AuthSession>.Success(ToSession(created));
    }

    public async Task<ServiceResult<bool>> RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null)
        {
            return ServiceResult<bool>.Failure("VALIDATION", "Email is required.");
        }

        // Always accept to avoid account enumeration.
        var user = await _users.FindByEmailAsync(normalized, cancellationToken);
        if (user is not null)
        {
            var token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
            await _resetTokens.StoreAsync(token, user.Id, TimeSpan.FromHours(1), cancellationToken);
            var resetLink = $"soraeru://reset-password?token={token}";
            await _email.SendPasswordResetAsync(user.Email, resetLink, cancellationToken);
        }

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ResetPasswordAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token) || string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return ServiceResult<bool>.Failure("VALIDATION", "Token and new password are required.");
        }

        if (command.NewPassword.Length < 8)
        {
            return ServiceResult<bool>.Failure("VALIDATION", "Password must be at least 8 characters.");
        }

        var userId = await _resetTokens.TakeUserIdAsync(command.Token.Trim(), cancellationToken);
        if (userId is null)
        {
            return ServiceResult<bool>.Failure("INVALID_TOKEN", "Reset token is invalid or expired.");
        }

        var user = await _users.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return ServiceResult<bool>.Failure("INVALID_TOKEN", "Reset token is invalid or expired.");
        }

        var updated = user with { PasswordHash = _passwordHasher.HashPassword(command.NewPassword) };
        await _users.UpdateAsync(updated, cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<UserRecord> SyncDeveloperFlagAsync(UserRecord user, CancellationToken cancellationToken)
    {
        // Union: config allowlist OR DB IsDeveloper. Allowlist can grant but not revoke a Curator UI toggle.
        var shouldBeDeveloper = _developers.IsDeveloperEmail(user.Email) || user.IsDeveloper;
        if (user.IsDeveloper == shouldBeDeveloper
            && (!shouldBeDeveloper || user.DailyQuota == AppConstants.UnlimitedDailyQuota))
        {
            return user;
        }

        var updated = user with
        {
            IsDeveloper = shouldBeDeveloper,
            DailyQuota = shouldBeDeveloper ? AppConstants.UnlimitedDailyQuota : AppConstants.FreeDailyQuota
        };
        await _users.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    private async Task<UserRecord> AfterSuccessfulAuthAsync(
        UserRecord user,
        AuthClient client,
        CancellationToken cancellationToken)
    {
        user = await SyncDeveloperFlagAsync(user, cancellationToken);
        // LoginCount / LastLoginAtUtc are App-only metrics for curator Accounts UI.
        if (client == AuthClient.App)
        {
            user = await RecordSuccessfulAppLoginAsync(user, cancellationToken);
        }

        return user;
    }

    private async Task<UserRecord> RecordSuccessfulAppLoginAsync(
        UserRecord user,
        CancellationToken cancellationToken)
    {
        var updated = user with
        {
            LoginCount = user.LoginCount + 1,
            LastLoginAtUtc = DateTimeOffset.UtcNow
        };
        await _users.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    private AuthSession ToSession(UserRecord user) =>
        new(
            user.Id,
            user.Email,
            _tokens.CreateAccessToken(user.Id, user.Email),
            user.OnboardingCompleted,
            user.IsDeveloper);

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return email.Trim().ToLowerInvariant();
    }
}
