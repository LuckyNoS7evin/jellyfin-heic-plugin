using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// A folder of HEIC photos is a photo album, as a folder of JPEGs is (Jellyfin's PhotoAlbumResolver
/// doesn't count HEICs, so a folder of only those would be a plain folder).
/// </summary>
public class HeicAlbumResolver : ItemResolver<PhotoAlbum>
{
    /// <inheritdoc />
    public override ResolverPriority Priority => ResolverPriority.Second;

    /// <inheritdoc />
    protected override PhotoAlbum? Resolve(ItemResolveArgs args)
    {
        if (!args.IsDirectory || !HeicPhotoResolver.PhotoLibrary(args.GetCollectionType(), args))
        {
            return null;
        }

        return args.FileSystemChildren.Any(f => !f.IsDirectory && HeicPhotoResolver.IsHeicPhoto(f.FullName))
            ? new PhotoAlbum { Path = args.Path }
            : null;
    }
}
