namespace AlfarQuest.Api.Profiles;

/// <summary>What an avatar is allowed to be. Separate from the code that enforces
/// it so the limits can be read in one place — and so the client can be shown the
/// same numbers rather than hard-coding its own.</summary>
public static class AvatarPolicy
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;      // 5 MB, as specified

    /// <summary>What the stored image is resized to. Large enough for a retina
    /// profile header, small enough that the row stays cheap to read.</summary>
    public const int StoredSize = 256;

    /// <summary>Refused before decoding. A 40000×40000 PNG can be a few hundred
    /// kilobytes on disk and gigabytes once decoded — the classic decompression
    /// bomb, which a byte-length limit alone does not catch.</summary>
    public const int MaxSourceDimension = 8_000;

    public static readonly string[] AcceptedContentTypes =
        ["image/png", "image/jpeg", "image/webp"];

    /// <summary>For the file picker's accept attribute. A convenience only — the
    /// browser can be told to ignore it, which is why the server checks too.</summary>
    public const string AcceptAttribute = ".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp";
}
