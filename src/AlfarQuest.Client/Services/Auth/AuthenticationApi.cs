using System.Net;
using System.Net.Http.Json;
using AlfarQuest.Shared.Auth;
using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>The HTTP shape of the auth endpoints, and nothing else.
///
/// Kept apart from <see cref="AuthenticationService"/> so that what a sign-in
/// *means* to the app — storing a session, telling the router, redirecting — is
/// readable without wading through status codes, and so either can be replaced
/// alone.</summary>
public sealed class AuthenticationApi(HttpClient http)
{
    public Task<ApiResponse<AuthSessionDto>> SignUpAsync(SignUpRequest request, CancellationToken ct = default) =>
        PostAsync<SignUpRequest, AuthSessionDto>("api/auth/signup", request, ct);

    public Task<ApiResponse<AuthSessionDto>> SignInAsync(SignInRequest request, CancellationToken ct = default) =>
        PostAsync<SignInRequest, AuthSessionDto>("api/auth/signin", request, ct);

    public Task<ApiResponse<PlayerProfileDto>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default) =>
        PostAsync<ForgotPasswordRequest, PlayerProfileDto>("api/auth/forgot-password", request, ct);

    /// <summary>Confirms a stored token with the server. The token's own contents
    /// prove nothing, so this is the only honest way to answer "am I still signed
    /// in" on start-up.</summary>
    public async Task<PlayerProfileDto?> MeAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync("api/auth/me", ct);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<PlayerProfileDto>(ct)
                : null;
        }
        catch (HttpRequestException) { return null; }
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        // Best effort. If the call cannot be made the client still forgets the
        // token, so the player is signed out here even when the server is not
        // reachable to be told.
        try { await http.PostAsync("api/auth/signout", null, ct); }
        catch (HttpRequestException) { }
    }

    private async Task<ApiResponse<TOut>> PostAsync<TIn, TOut>(string url, TIn body, CancellationToken ct)
    {
        try
        {
            var response = await http.PostAsJsonAsync(url, body, ct);

            if (response.IsSuccessStatusCode)
            {
                // 202/204 carry no body — the forgot-password endpoint answers that
                // way on purpose, and reading a value from it would throw.
                if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.NoContent)
                    return ApiResponse<TOut>.Succeeded(default);

                return ApiResponse<TOut>.Succeeded(await response.Content.ReadFromJsonAsync<TOut>(ct));
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ApiResponse<TOut>.Failed(new AuthProblem("Too many attempts. Wait a few minutes and try again."));

            var problem = await ReadProblemAsync(response, ct);
            return ApiResponse<TOut>.Failed(problem);
        }
        catch (HttpRequestException)
        {
            return ApiResponse<TOut>.Unreachable();
        }
    }

    private static async Task<AuthProblem> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<AuthProblem>(ct)
                   ?? new AuthProblem("That did not work. Please try again.");
        }
        catch
        {
            // A response that is not the problem shape — a proxy error page, say.
            // Its body is not ours to show, so it is replaced rather than echoed.
            return new AuthProblem("That did not work. Please try again.");
        }
    }
}

/// <param name="Reachable">False when the request never got an answer, which the
/// UI reports differently from a refusal.</param>
public readonly record struct ApiResponse<T>(bool Ok, T? Value, AuthProblem? Problem, bool Reachable)
{
    public static ApiResponse<T> Succeeded(T? value) => new(true, value, null, true);
    public static ApiResponse<T> Failed(AuthProblem problem) => new(false, default, problem, true);
    public static ApiResponse<T> Unreachable() => new(false, default, null, false);
}
