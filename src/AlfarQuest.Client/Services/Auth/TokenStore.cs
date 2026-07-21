using Microsoft.JSInterop;

namespace AlfarQuest.Client.Services.Auth;

/// <summary>Where the bearer token lives between page loads.
///
/// "Remember me" chooses the storage, not the contents: localStorage survives
/// closing the browser, sessionStorage does not. The password is never stored
/// anywhere — what persists is a revocable token with an expiry the server set.
///
/// Known trade-off: Web Storage is readable by script, so a cross-site scripting
/// hole in this app would expose the token. An HttpOnly cookie would not have
/// that weakness, but needs the API and the client on one origin (or a
/// credentialed CORS setup with CSRF protection) — which this deployment is not.
/// Sessions are therefore short and revocable server-side, which limits what a
/// stolen token is worth.</summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string Key = "alfarquest.session";
    private const string Local = "localStorage";
    private const string Session = "sessionStorage";

    public async Task<string?> ReadAsync()
    {
        // Persistent first: a "remember me" token outranks a leftover tab-scoped
        // one, and reading both means a token written either way is found.
        var token = await GetAsync(Local) ?? await GetAsync(Session);
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public async Task WriteAsync(string token, bool persistent)
    {
        await ClearAsync();          // never leave a copy in the other store
        await js.InvokeVoidAsync($"{(persistent ? Local : Session)}.setItem", Key, token);
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync($"{Local}.removeItem", Key);
        await js.InvokeVoidAsync($"{Session}.removeItem", Key);
    }

    private async Task<string?> GetAsync(string store)
    {
        try { return await js.InvokeAsync<string?>($"{store}.getItem", Key); }
        catch (JSException) { return null; }   // storage disabled: sign-in still works, it just won't persist
    }
}
