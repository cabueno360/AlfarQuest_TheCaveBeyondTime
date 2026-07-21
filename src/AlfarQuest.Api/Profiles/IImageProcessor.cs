using AlfarQuest.Api.Common;

namespace AlfarQuest.Api.Profiles;

/// <summary>Decodes an upload and re-encodes it to a known-good image.
///
/// An interface because the cropping the brief wants next belongs here, behind
/// the same call — and because the security value of this step is that it is
/// swappable but never skippable.</summary>
public interface IImageProcessor
{
    /// <param name="source">Raw upload bytes, already length-checked.</param>
    /// <param name="size">Target edge, in pixels.</param>
    Result<ProcessedImage> ToSquareAvatar(byte[] source, int size);
}

public readonly record struct ProcessedImage(byte[] Content, string ContentType, int Width, int Height);
