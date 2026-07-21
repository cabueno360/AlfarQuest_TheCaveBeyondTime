using AlfarQuest.Api.Common;
using SkiaSharp;

namespace AlfarQuest.Api.Profiles;

/// <summary>Decodes the upload and writes a brand-new PNG from the pixels.
///
/// Re-encoding rather than storing what arrived is the point. Whatever else was
/// in the file — EXIF, a trailing ZIP, an HTML polyglot, a comment block of
/// script — does not survive the round trip through a pixel buffer. What gets
/// stored is only what could be drawn.</summary>
public sealed class SkiaImageProcessor(ILogger<SkiaImageProcessor> log) : IImageProcessor
{
    public Result<ProcessedImage> ToSquareAvatar(byte[] source, int size)
    {
        var kind = ImageSignature.Detect(source);
        if (!ImageSignature.IsAccepted(kind))
            return Result<ProcessedImage>.Fail("That file is not a PNG, JPG or WEBP image.");

        try
        {
            using var codec = SKCodec.Create(new MemoryStream(source, writable: false));
            if (codec is null)
                return Result<ProcessedImage>.Fail("That image could not be read.");

            // Checked from the header, before a single pixel is allocated.
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0)
                return Result<ProcessedImage>.Fail("That image could not be read.");
            if (info.Width > AvatarPolicy.MaxSourceDimension || info.Height > AvatarPolicy.MaxSourceDimension)
                return Result<ProcessedImage>.Fail(
                    $"That image is larger than {AvatarPolicy.MaxSourceDimension}px on a side.");

            using var decoded = SKBitmap.Decode(codec);
            if (decoded is null)
                return Result<ProcessedImage>.Fail("That image could not be read.");

            using var square = CropToSquare(decoded);
            using var scaled = square.Resize(new SKImageInfo(size, size), new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (scaled is null)
                return Result<ProcessedImage>.Fail("That image could not be resized.");

            using var image = SKImage.FromBitmap(scaled);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);

            return Result<ProcessedImage>.Success(
                new ProcessedImage(data.ToArray(), "image/png", size, size));
        }
        catch (Exception ex)
        {
            // A malformed image is a bad request, not a server fault. Logged
            // because a spike here is worth seeing; not surfaced, because the
            // decoder's own message is no use to the player.
            log.LogWarning(ex, "Rejected an avatar upload that failed to decode");
            return Result<ProcessedImage>.Fail("That image could not be read.");
        }
    }

    /// <summary>Centre crop. Squashing a portrait into a square instead would make
    /// every non-square upload look subtly wrong, and the frame is round anyway.</summary>
    private static SKBitmap CropToSquare(SKBitmap source)
    {
        var edge = Math.Min(source.Width, source.Height);
        var rect = new SKRectI(
            (source.Width - edge) / 2,
            (source.Height - edge) / 2,
            (source.Width - edge) / 2 + edge,
            (source.Height - edge) / 2 + edge);

        var square = new SKBitmap(edge, edge);
        return source.ExtractSubset(square, rect) ? square : source.Copy();
    }
}
