using AlfarQuest.Api.Common;

namespace AlfarQuest.Api.Accounts;

/// <summary>The seam for "sign in with Google / Steam / Discord".
///
/// Adding one means implementing this and registering it — nothing above it
/// changes, because <see cref="ExternalAccountLinker"/> already turns an
/// <see cref="ExternalIdentity"/> into an account and the controller already turns
/// an account into a session. No provider is registered today; this is the shape
/// the first one plugs into.</summary>
public interface IExternalIdentityProvider
{
    /// <summary>Stable key used in the URL and stored on the account, e.g. "google".</summary>
    string Name { get; }

    /// <summary>Where to send the browser to begin the handshake.</summary>
    Uri AuthorizationUrl(string returnUrl, string state);

    /// <summary>Trades the provider's one-time code for the identity behind it.</summary>
    Task<Result<ExternalIdentity>> ExchangeAsync(string code, CancellationToken ct = default);
}

/// <param name="SubjectId">The provider's own immutable id for this user. Matched
/// on rather than the email, which providers let people change.</param>
public readonly record struct ExternalIdentity(
    string Provider, string SubjectId, string Email, string FullName, bool EmailVerified);
