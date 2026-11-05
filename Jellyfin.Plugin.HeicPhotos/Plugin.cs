using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// HEIC Photos: iPhone photos (HEIC/HEIF) in photo libraries. Jellyfin's image library can't read
/// them, so the server skips them; this plugin lists them, reads their details, and converts them
/// with Jellyfin's own ffmpeg whenever an image is asked for. No copies are kept beside the photos;
/// Jellyfin caches the sizes it serves, as it does for every image.
/// </summary>
public class Plugin : BasePlugin<BasePluginConfiguration>
{
    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    /// <param name="applicationPaths">The server's paths.</param>
    /// <param name="xmlSerializer">The XML serializer for the configuration.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => "HEIC Photos";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("5b0f7c1e-3a6d-4f2b-9e84-2c71d0a9b6e3");

    /// <inheritdoc />
    public override string Description => "Shows HEIC/HEIF photos (iPhone) in photo libraries, converted with Jellyfin's ffmpeg when they're viewed.";
}
