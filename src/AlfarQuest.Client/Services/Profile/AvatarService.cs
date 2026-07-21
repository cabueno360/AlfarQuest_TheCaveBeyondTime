using System.Net.Http.Json;
using AlfarQuest.Client.Services.Auth;
using AlfarQuest.Shared.Auth;
using AlfarQuest.Shared.Profiles;
using Microsoft.AspNetCore.Components.Forms;

namespace AlfarQuest.Client.Services.Profile;

/// <summary>Uploading and removing the player's portrait.
///
/// The checks here are for the player's benefit — they turn "413 Payload Too
/// Large" into a sentence, before a slow upload of a file that was never going to
/// be accepted. They are not the security boundary: the server repeats every one
/// of them, and decodes the image besides.</summary>
public sealed class AvatarService(HttpClient http, UserSessionService session)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const string Accept = ".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp";

    private static readonly string[] AllowedTypes = ["image/png", "image/jpeg", "image/webp"];

    public static OperationResult Precheck(IBrowserFile file)
    {
        if (file.Size > MaxBytes)
            return OperationResult.Fail("That image is larger than 5 MB. Choose a smaller one.");

        // The browser derives this from the file extension, so it is a hint, not a
        // fact — which is exactly why the server sniffs the bytes instead.
        if (!AllowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return OperationResult.Fail("Portraits must be PNG, JPG or WEBP.");

        return OperationResult.Success;
    }

    public async Task<OperationResult> UploadAsync(IBrowserFile file, CancellationToken ct = default)
    {
        if (Precheck(file) is { Ok: false } rejected) return rejected;

        try
        {
            // Read the file fully before sending. A StreamContent wrapped around
            // the browser's file stream arrives at the server with an empty body:
            // WebAssembly's HTTP stack cannot stream a request, so the content has
            // to be a buffer it can hand to fetch() whole. The 5 MB cap above is
            // what makes buffering safe.
            //
            // OpenReadStream needs its own limit or it throws at 512 KB, long
            // before our policy would have refused anything.
            using var buffer = new MemoryStream();
            await file.OpenReadStream(MaxBytes, ct).CopyToAsync(buffer, ct);

            using var content = new MultipartFormDataContent();
            var part = new ByteArrayContent(buffer.ToArray());
            part.Headers.ContentType = new(file.ContentType);
            content.Add(part, "file", file.Name);

            var response = await http.PostAsync("api/avatars", content, ct);
            if (!response.IsSuccessStatusCode) return await Problem(response, ct);

            if (await response.Content.ReadFromJsonAsync<PlayerProfileDto>(ct) is { } profile)
                session.UpdatePlayer(profile);      // every portrait on screen refreshes from this

            return OperationResult.Success;
        }
        catch (HttpRequestException) { return OperationResult.Offline(); }
        catch (IOException) { return OperationResult.Fail("That image is larger than 5 MB."); }
    }

    public async Task<OperationResult> RemoveAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.DeleteAsync("api/avatars", ct);
            if (!response.IsSuccessStatusCode) return await Problem(response, ct);

            if (await response.Content.ReadFromJsonAsync<PlayerProfileDto>(ct) is { } profile)
                session.UpdatePlayer(profile);

            return OperationResult.Success;
        }
        catch (HttpRequestException) { return OperationResult.Offline(); }
    }

    private static async Task<OperationResult> Problem(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<AuthProblem>(ct);
            return OperationResult.Fail(problem?.Message ?? "The upload was refused.");
        }
        catch
        {
            return OperationResult.Fail("The upload was refused.");
        }
    }
}
