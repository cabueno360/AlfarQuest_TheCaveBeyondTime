namespace AlfarQuest.Api.Data;

/// <summary>The avatar image, one row per player.
///
/// Stored as bytes in the database rather than as a file on disk. That costs a
/// little throughput and removes a whole class of bugs: no upload directory to
/// mis-permission, no filename to sanitise, no path traversal, and no way for the
/// image to be served back as anything but the content type recorded here. After
/// the resize these are tens of kilobytes.</summary>
public class PlayerAvatar
{
    public Guid PlayerAccountId { get; set; }

    public byte[] Content { get; set; } = [];

    /// <summary>Set by the server from what it actually encoded, never from the
    /// upload. A browser is not allowed to name the content type it will later be
    /// served with — that is how an "image" gets rendered as HTML.</summary>
    public string ContentType { get; set; } = "image/png";

    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
