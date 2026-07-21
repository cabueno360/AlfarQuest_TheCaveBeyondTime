using AlfarQuest.Api.Common;
using AlfarQuest.Api.Data;
using AlfarQuest.Shared.Auth;

namespace AlfarQuest.Api.Accounts;

/// <summary>Creating accounts and proving who owns one. Knows nothing about HTTP,
/// so its rules can be read — and tested — without spinning up a request.</summary>
public interface IAccountService
{
    Task<Result<PlayerAccount>> RegisterAsync(SignUpRequest request, CancellationToken ct = default);

    Task<Result<PlayerAccount>> AuthenticateAsync(SignInRequest request, CancellationToken ct = default);

    Task<Result<PlayerAccount>> ChangePasswordAsync(Guid accountId, ChangePasswordRequest request, CancellationToken ct = default);

    Task<PlayerAccount?> FindAsync(Guid accountId, CancellationToken ct = default);
}
