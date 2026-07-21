namespace AlfarQuest.Api.Common;

/// <summary>A success carrying a value, or a failure carrying a reason the player
/// may read.
///
/// Services return this instead of throwing, because "that email is taken" is an
/// expected outcome of registering, not an exceptional one — and because a
/// controller that must handle the failure to compile cannot forget to.</summary>
public readonly record struct Result<T>(T? Value, string? Error, string? Field)
{
    public bool Ok => Error is null;

    public static Result<T> Success(T value) => new(value, null, null);

    /// <param name="field">Name of the input the message belongs beside, when it
    /// belongs beside one.</param>
    public static Result<T> Fail(string error, string? field = null) => new(default, error, field);

    /// <summary>Folds both cases into one value, so callers cannot read
    /// <see cref="Value"/> without having considered the failure.</summary>
    public TOut Match<TOut>(Func<T, TOut> ok, Func<string, string?, TOut> fail) =>
        Ok ? ok(Value!) : fail(Error!, Field);
}
