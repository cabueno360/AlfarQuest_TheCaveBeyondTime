namespace AlfarQuest.Api.Profiles;

/// <summary>Identifies an image by its leading bytes.
///
/// The filename and the browser's Content-Type are both supplied by the caller,
/// so neither is evidence of anything. This reads what the file actually is. It
/// is a cheap first gate, not the last one: the decode that follows is what truly
/// proves the bytes are an image.</summary>
public static class ImageSignature
{
    public enum Kind { Unknown, Png, Jpeg, Webp }

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Kind Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(PngMagic))
            return Kind.Png;

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return Kind.Jpeg;

        // RIFF....WEBP — the four size bytes in between are not part of the tag.
        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes[8..12].SequenceEqual("WEBP"u8))
            return Kind.Webp;

        return Kind.Unknown;
    }

    public static bool IsAccepted(Kind kind) => kind is Kind.Png or Kind.Jpeg or Kind.Webp;
}
