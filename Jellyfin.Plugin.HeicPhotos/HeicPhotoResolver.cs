using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// HEIC files in a photo library (or a home videos library with photos on) become photos, as
/// Jellyfin's own photo resolver does for the formats it knows. Unlike that one, a HEIC is listed
/// even when a video of the same name sits beside it: that's an iPhone Live Photo's motion clip,
/// and the photo is the point.
/// </summary>
public class HeicPhotoResolver : ItemResolver<Photo>
{
    // As Jellyfin's PhotoResolver: artwork, not photos.
    private static readonly string[] _artwork = ["folder", "thumb", "landscape", "fanart", "backdrop", "poster", "cover", "logo", "default"];

    /// <summary>Whether a library holds photos.</summary>
    internal static bool PhotoLibrary(CollectionType? type, ItemResolveArgs args)
        => type == CollectionType.photos || (type == CollectionType.homevideos && args.LibraryOptions.EnablePhotos);

    /// <summary>Whether a file is a HEIC photo (not artwork).</summary>
    internal static bool IsHeicPhoto(string path)
        => Heic.IsHeic(path) && !_artwork.Any(a => Path.GetFileNameWithoutExtension(path).StartsWith(a, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    protected override Photo? Resolve(ItemResolveArgs args)
    {
        if (args.IsDirectory || !PhotoLibrary(args.CollectionType, args) || !IsHeicPhoto(args.Path))
        {
            return null;
        }

        return new Photo { Path = args.Path };
    }
}
