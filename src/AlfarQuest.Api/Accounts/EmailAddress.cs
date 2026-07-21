namespace AlfarQuest.Api.Accounts;

/// <summary>One definition of how an address is stored and compared.
///
/// Every read and every write goes through <see cref="Normalise"/>, so
/// "Ada@Example.com " and "ada@example.com" cannot become two accounts. Casing is
/// folded here rather than left to the database collation: the answer to "is this
/// taken" should not depend on how the server was installed.</summary>
public static class EmailAddress
{
    public static string Normalise(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
