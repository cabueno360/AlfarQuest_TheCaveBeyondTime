using System.Net;
using System.Net.Http.Json;
using AlfarQuest.Client.Services.Auth;
using AlfarQuest.Shared.Auth;
using AlfarQuest.Shared.Profiles;

namespace AlfarQuest.Client.Services.Profile;

/// <summary>Reading and editing the signed-in player's profile.
///
/// Every successful write feeds the fresh profile back into the session, so the
/// header, the dropdown and the page all show the new name without any of them
/// knowing an edit happened.</summary>
public sealed class ProfileService(HttpClient http, UserSessionService session)
{
    public async Task<PlayerProfileDto?> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var profile = await http.GetFromJsonAsync<PlayerProfileDto>("api/profile", ct);
            if (profile is not null) session.UpdatePlayer(profile);
            return profile;
        }
        catch (HttpRequestException) { return session.Player; }   // fall back to what is already known
    }

    public async Task<OperationResult> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PutAsJsonAsync("api/profile", request, ct);
            if (!response.IsSuccessStatusCode) return await Problem(response, ct);

            if (await response.Content.ReadFromJsonAsync<PlayerProfileDto>(ct) is { } profile)
                session.UpdatePlayer(profile);

            return OperationResult.Success;
        }
        catch (HttpRequestException) { return OperationResult.Offline(); }
    }

    /// <summary>Changing a password ends every session, including this one — so
    /// the caller must send the player back to sign in afterwards.</summary>
    public async Task<OperationResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/profile/password", request, ct);
            return response.IsSuccessStatusCode ? OperationResult.Success : await Problem(response, ct);
        }
        catch (HttpRequestException) { return OperationResult.Offline(); }
    }

    /// <summary>Reports what the last play session achieved. Failure is silent:
    /// this runs as the player leaves the game, and an error box on the way out
    /// about a statistic would be worse than the lost statistic.</summary>
    public async Task ReportStatsAsync(PlayerStatsDto stats, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync("api/profile/stats", stats, ct);
            if (response.IsSuccessStatusCode &&
                await response.Content.ReadFromJsonAsync<PlayerProfileDto>(ct) is { } profile)
                session.UpdatePlayer(profile);
        }
        catch (HttpRequestException) { }
    }

    private static async Task<OperationResult> Problem(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return OperationResult.Fail("Your session has ended. Sign in again.");

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<AuthProblem>(ct);
            return OperationResult.Fail(problem?.Message ?? "That did not work.", problem?.Field);
        }
        catch
        {
            return OperationResult.Fail("That did not work. Please try again.");
        }
    }
}
