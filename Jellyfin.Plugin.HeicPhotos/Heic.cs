using MetadataExtractor;
using MetadataExtractor.Formats.Heif;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>What's a HEIC file, and its size as shown.</summary>
internal static class Heic
{
    /// <summary>The extensions handled, without the dot.</summary>
    public static readonly string[] Formats = ["heic", "heif", "hif"];

    /// <summary>Whether the path is a HEIC/HEIF file.</summary>
    public static bool IsHeic(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path.AsSpan()).TrimStart('.');
        foreach (var f in Formats)
        {
            if (ext.Equals(f, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The picture's size as displayed: the primary image's size from the container, turned by its
    /// rotation (an iPhone stores portraits as landscape, turned). Null when it can't be read.
    /// </summary>
    public static (int Width, int Height)? ReadSize(IReadOnlyList<MetadataExtractor.Directory> dirs)
    {
        // The first set of image properties is the main picture's; later ones are thumbnails.
        foreach (var d in dirs.OfType<HeicImagePropertiesDirectory>())
        {
            if (d.TryGetInt32(HeicImagePropertiesDirectory.TagImageWidth, out var w)
                && d.TryGetInt32(HeicImagePropertiesDirectory.TagImageHeight, out var h)
                && w > 0 && h > 0)
            {
                var turned = d.TryGetInt32(HeicImagePropertiesDirectory.TagRotation, out var r) && (r == 90 || r == 270);
                return turned ? (h, w) : (w, h);
            }
        }

        return null;
    }

    /// <summary>The size as displayed, read from the file.</summary>
    public static (int Width, int Height)? ReadSize(string path)
    {
        try
        {
            return ReadSize(ImageMetadataReader.ReadMetadata(path));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
