using System.Security.Cryptography;

namespace AlfarQuest.Api.Auth;

/// <summary>Session tokens: how they are minted and how they are stored.
///
/// The token itself is 256 bits of CSPRNG output and carries no meaning, so it
/// cannot be forged or reasoned about — unlike a JWT, whose validity is a pure
/// function of its signature and therefore cannot be withdrawn. Sign-out here is
/// a real revocation, which is what "Logout" has to mean.
///
/// Only the SHA-256 of a token is written to the database. A leaked dump then
/// yields no usable sessions. Plain SHA-256 is right here and would be wrong for
/// a password: the input already has 256 bits of entropy, so there is nothing for
/// a slow hash to protect.</summary>
public static class SecureTokens
{
    private const int TokenBytes = 32;

    public static string Mint() => Base64Url(RandomNumberGenerator.GetBytes(TokenBytes));

    public static string Fingerprint(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
