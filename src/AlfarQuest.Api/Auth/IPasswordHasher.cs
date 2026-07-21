namespace AlfarQuest.Api.Auth;

/// <summary>Turns a password into something safe to store, and checks one against
/// it. An interface rather than a static so the algorithm can be replaced — and
/// so <see cref="PasswordVerification.NeedsRehash"/> gives the replacement a way
/// in without a migration.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string storedHash, string password);
}

/// <param name="Success">Whether the password matched.</param>
/// <param name="NeedsRehash">True when the stored hash used weaker parameters than
/// the ones in force now. The caller is holding the plaintext at that moment and
/// will never have a better chance to upgrade it.</param>
public readonly record struct PasswordVerification(bool Success, bool NeedsRehash)
{
    public static readonly PasswordVerification Failed = new(false, false);
}
