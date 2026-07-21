using System.Security.Claims;

namespace AlfarQuest.Api.Auth;

/// <summary>Who is calling, according to the token they presented.
///
/// Controllers depend on this rather than digging through User.Claims, so the id
/// they act on can only ever be the authenticated one. There is no method here
/// that takes an id from the request body.</summary>
public interface ICurrentUser
{
    Guid? AccountId { get; }
    string? BearerToken { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? AccountId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    /// <summary>Needed by sign-out, which revokes the exact session in hand.</summary>
    public string? BearerToken
    {
        get
        {
            var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
            return header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                ? header["Bearer ".Length..].Trim()
                : null;
        }
    }
}
