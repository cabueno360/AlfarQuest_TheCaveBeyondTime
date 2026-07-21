namespace AlfarQuest.Api.Data;

/// <summary>A registered player: the credential, the profile and the game
/// statistics, in one aggregate.
///
/// They are kept together because they share a lifetime — deleting an account
/// deletes all three — and splitting them would buy a join and nothing else.
/// New profile fields belong here; new *kinds* of thing (characters, friends,
/// guild membership) belong in their own table pointing back at
/// <see cref="Id"/>.</summary>
public class PlayerAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Lower-cased at write time and uniquely indexed. Doing the casing
    /// in C# rather than leaning on the collation means "the address is taken" has
    /// the same answer whichever database this runs on.</summary>
    public string Email { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string FullName { get; set; } = "";
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Set once an avatar has been uploaded. The bytes live in
    /// <see cref="PlayerAvatar"/>; this is the cache-busting stamp so a new upload
    /// is fetched immediately instead of being served from the browser cache.</summary>
    public string? AvatarVersion { get; set; }

    // ---- game statistics, reported by the client after a session -------------
    public string? CurrentCharacter { get; set; }
    public int HighestLevel { get; set; }
    public long TotalGold { get; set; }
    public long TotalPlayTimeSeconds { get; set; }

    // ---- future-ready seams --------------------------------------------------
    /// <summary>False until the address is confirmed. Nothing enforces it yet; the
    /// column exists so turning verification on is a policy change, not a
    /// migration on a live table.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Set when the account is created by Google/Steam/Discord instead of
    /// a password. Null for local accounts.</summary>
    public string? ExternalProvider { get; set; }
    public string? ExternalSubjectId { get; set; }

    public List<PlayerSession> Sessions { get; set; } = [];
}
