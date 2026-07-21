using AlfarQuest.Shared.Auth;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>What signing in and out mean to this application.
///
/// The forms call these three methods and read an <see cref="OperationResult"/>;
/// they know nothing about tokens, storage or the router. Adding "sign in with
/// Discord" later means one more method here that ends in the same
/// <see cref="EstablishAsync"/> — everything downstream already works.</summary>
public sealed class AuthenticationService(
    AuthenticationApi api,
    UserSessionService session,
    AlfarQuestAuthenticationStateProvider state)
{
    public async Task<OperationResult> SignUpAsync(SignUpRequest request, CancellationToken ct = default)
    {
        var response = await api.SignUpAsync(request, ct);
        // A fresh account is signed in but not remembered: the choice to stay
        // signed in on this machine is one the player makes, and nobody has been
        // asked yet.
        return await EstablishAsync(response, persistent: false);
    }

    public async Task<OperationResult> SignInAsync(SignInRequest request, CancellationToken ct = default)
    {
        var response = await api.SignInAsync(request, ct);
        return await EstablishAsync(response, request.RememberMe);
    }

    public async Task SignOutAsync()
    {
        // The server first, so the token is revoked rather than merely forgotten —
        // otherwise it would stay valid until it expired, in whatever hands.
        await api.SignOutAsync();
        await session.EndAsync();
        state.NotifyChanged();
    }

    public async Task<OperationResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var response = await api.ForgotPasswordAsync(request, ct);
        if (!response.Reachable) return OperationResult.Offline();
        return response.Ok
            ? OperationResult.Success
            : OperationResult.Fail(response.Problem?.Message ?? "That did not work.", response.Problem?.Field);
    }

    private async Task<OperationResult> EstablishAsync(ApiResponse<AuthSessionDto> response, bool persistent)
    {
        if (!response.Reachable) return OperationResult.Offline();

        if (!response.Ok || response.Value is not { } issued)
            return OperationResult.Fail(
                response.Problem?.Message ?? "That did not work.", response.Problem?.Field);

        await session.BeginAsync(issued.Token, issued.Player, persistent);
        state.NotifyChanged();
        return OperationResult.Success;
    }
}
