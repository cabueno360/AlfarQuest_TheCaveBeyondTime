using System.Security.Cryptography;

namespace AlfarQuest.Api.Auth;

/// <summary>PBKDF2-HMAC-SHA256, the OWASP-recommended in-box option. Chosen over
/// Argon2/bcrypt only because those need a third-party native dependency; the
/// interface exists so swapping one in later is a registration change.
///
/// The stored string carries its own parameters — "pbkdf2-sha256$iters$salt$hash"
/// — so raising the iteration count does not invalidate existing passwords. Old
/// hashes keep verifying with the count they were made under, and are quietly
/// upgraded the next time their owner signs in.</summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    // OWASP's 2023 floor for PBKDF2-HMAC-SHA256.
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "pbkdf2-sha256";

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, Iterations);
        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerification Verify(string storedHash, string password)
    {
        if (!TryParse(storedHash, out var iterations, out var salt, out var expected))
            return PasswordVerification.Failed;

        var actual = Derive(password, salt, iterations);

        // Fixed-time compare: a byte-by-byte one leaks how much of the hash matched.
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return PasswordVerification.Failed;

        return new PasswordVerification(true, NeedsRehash: iterations < Iterations);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);

    private static bool TryParse(string stored, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0; salt = []; hash = [];
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix) return false;
        if (!int.TryParse(parts[1], out iterations) || iterations <= 0) return false;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;      // a corrupted row must fail closed, not throw a 500
        }

        return salt.Length > 0 && hash.Length > 0;
    }
}
