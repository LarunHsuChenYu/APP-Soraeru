using System.Text;
using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;

namespace Soraeru.Application.Curator;

/// <summary>
/// Curator CRUD for verified empty-ear entries (allowlist = developer email policy).
/// </summary>
public sealed class CuratorMnemonicService : ICuratorMnemonicService
{
    private readonly IUserRepository _users;
    private readonly IVerifiedMnemonicRepository _verified;
    private readonly IDeveloperAccountPolicy _curatorPolicy;

    public CuratorMnemonicService(
        IUserRepository users,
        IVerifiedMnemonicRepository verified,
        IDeveloperAccountPolicy curatorPolicy)
    {
        _users = users;
        _verified = verified;
        _curatorPolicy = curatorPolicy;
    }

    public async Task<ServiceResult<VerifiedMnemonicDto>> CreateAsync(
        CreateVerifiedMnemonicCommand command,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(command.ActorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(auth.Value.Code, auth.Value.Message);
        }

        if (string.IsNullOrWhiteSpace(command.Language)
            || string.IsNullOrWhiteSpace(command.SourceText)
            || string.IsNullOrWhiteSpace(command.DisplayText)
            || string.IsNullOrWhiteSpace(command.NotationText)
            || string.IsNullOrWhiteSpace(command.Explanation))
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(
                "VALIDATION",
                "語言、原詞、displayText、notationText、explanation 皆必填。");
        }

        var language = command.Language.Trim();
        var sourceText = command.SourceText.Trim();
        var normalized = NormalizeText(sourceText);

        var existing = await _verified.FindByLanguageAndNormalizedAsync(
            language,
            normalized,
            cancellationToken);
        if (existing is not null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(
                "CONFLICT",
                "此語言與原詞已有已驗證空耳條目。");
        }

        var now = DateTimeOffset.UtcNow;
        var record = new VerifiedMnemonicRecord(
            Guid.NewGuid(),
            language,
            sourceText,
            normalized,
            command.DisplayText.Trim(),
            command.NotationText.Trim(),
            command.Explanation.Trim(),
            command.IsEnabled,
            now,
            now);

        var saved = await _verified.AddAsync(record, cancellationToken);
        return ServiceResult<VerifiedMnemonicDto>.Success(ToDto(saved));
    }

    public async Task<ServiceResult<VerifiedMnemonicDto>> UpdateAsync(
        UpdateVerifiedMnemonicCommand command,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(command.ActorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(auth.Value.Code, auth.Value.Message);
        }

        if (command.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(command.DisplayText)
            || string.IsNullOrWhiteSpace(command.NotationText)
            || string.IsNullOrWhiteSpace(command.Explanation))
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(
                "VALIDATION",
                "Id、displayText、notationText、explanation 皆必填。");
        }

        var existing = await _verified.GetByIdAsync(command.Id, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure("NOT_FOUND", "找不到已驗證空耳條目。");
        }

        var updated = existing with
        {
            DisplayText = command.DisplayText.Trim(),
            NotationText = command.NotationText.Trim(),
            Explanation = command.Explanation.Trim(),
            IsEnabled = command.IsEnabled ?? existing.IsEnabled,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var saved = await _verified.UpdateAsync(updated, cancellationToken);
        return ServiceResult<VerifiedMnemonicDto>.Success(ToDto(saved));
    }

    public async Task<ServiceResult<VerifiedMnemonicDto>> SetEnabledAsync(
        SetVerifiedMnemonicEnabledCommand command,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(command.ActorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(auth.Value.Code, auth.Value.Message);
        }

        if (command.Id == Guid.Empty)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure("VALIDATION", "Id is required.");
        }

        var existing = await _verified.GetByIdAsync(command.Id, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure("NOT_FOUND", "找不到已驗證空耳條目。");
        }

        var updated = existing with
        {
            IsEnabled = command.IsEnabled,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var saved = await _verified.UpdateAsync(updated, cancellationToken);
        return ServiceResult<VerifiedMnemonicDto>.Success(ToDto(saved));
    }

    public async Task<ServiceResult<IReadOnlyList<VerifiedMnemonicDto>>> ListAsync(
        Guid actorUserId,
        string? language,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<IReadOnlyList<VerifiedMnemonicDto>>.Failure(
                auth.Value.Code,
                auth.Value.Message);
        }

        var rows = await _verified.SearchAsync(language, query, cancellationToken);
        IReadOnlyList<VerifiedMnemonicDto> list = rows.Select(ToDto).ToList();
        return ServiceResult<IReadOnlyList<VerifiedMnemonicDto>>.Success(list);
    }

    public async Task<ServiceResult<VerifiedMnemonicDto>> GetAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure(auth.Value.Code, auth.Value.Message);
        }

        var existing = await _verified.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<VerifiedMnemonicDto>.Failure("NOT_FOUND", "找不到已驗證空耳條目。");
        }

        return ServiceResult<VerifiedMnemonicDto>.Success(ToDto(existing));
    }

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
            return ("FORBIDDEN", "僅策展授權帳號可管理已驗證空耳。");
        }

        return null;
    }

    private static VerifiedMnemonicDto ToDto(VerifiedMnemonicRecord record) =>
        new(
            record.Id,
            record.Language,
            record.SourceText,
            record.NormalizedSource,
            record.DisplayText,
            record.NotationText,
            record.Explanation,
            record.IsEnabled,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);

    private static string NormalizeText(string text)
    {
        var collapsed = string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return collapsed.Normalize(NormalizationForm.FormC);
    }
}
