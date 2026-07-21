namespace AlfarQuest.Client.Services.Auth;

/// <summary>The outcome of something the player asked for, in the form a form can
/// render: succeeded, or failed with a message and — when it belongs beside one
/// input rather than above the whole form — which field it belongs to.
///
/// Client-side twin of the API's Result. Kept separate rather than shared,
/// because the API's carries an entity and this one carries only what the UI is
/// allowed to show.</summary>
public readonly record struct OperationResult(bool Ok, string? Error = null, string? Field = null)
{
    public static readonly OperationResult Success = new(true);

    public static OperationResult Fail(string error, string? field = null) => new(false, error, field);

    /// <summary>The message shown when the network itself failed. Deliberately not
    /// the exception text: "TypeError: Failed to fetch" tells a player nothing and
    /// tells an attacker a little.</summary>
    public static OperationResult Offline() =>
        new(false, "Could not reach the server. Check your connection and try again.");
}
