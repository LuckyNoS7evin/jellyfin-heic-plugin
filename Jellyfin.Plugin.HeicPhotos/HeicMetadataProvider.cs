using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// A HEIC photo's details, as Jellyfin's own reader gives other photos (which skips HEIC): when it
/// was taken, the camera and its settings, where (GPS), and its size as shown.
/// </summary>
public class HeicMetadataProvider : ICustomMetadataProvider<Photo>, IHasItemChangeMonitor, IHasOrder
{
    private readonly ILogger<HeicMetadataProvider> _logger;

    /// <summary>Initializes a new instance of the <see cref="HeicMetadataProvider"/> class.</summary>
    /// <param name="logger">The logger.</param>
    public HeicMetadataProvider(ILogger<HeicMetadataProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "HEIC Photos";

    /// <inheritdoc />
    public int Order => 100; // after Jellyfin's own photo reader

    /// <inheritdoc />
    public bool HasChanged(BaseItem item, IDirectoryService directoryService)
    {
        if (!item.IsFileProtocol || !Heic.IsHeic(item.Path))
        {
            return false;
        }

        var file = directoryService.GetFile(item.Path);
        return file is not null && item.HasChanged(file.LastWriteTimeUtc);
    }

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Photo item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        if (!Heic.IsHeic(item.Path))
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        item.SetImagePath(ImageType.Primary, item.Path);
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(item.Path);
            Apply(item, dirs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't read the details of {Path}", item.Path);
        }

        // Converted pictures come out upright, so nothing more to turn.
        item.Orientation = ImageOrientation.TopLeft;
        return Task.FromResult(ItemUpdateType.ImageUpdate | ItemUpdateType.MetadataImport);
    }

    private static void Apply(Photo item, IReadOnlyList<MetadataExtractor.Directory> dirs)
    {
        if (Heic.ReadSize(dirs) is { } size)
        {
            item.Width = size.Width;
            item.Height = size.Height;
        }

        var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
        var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();

        item.CameraMake = ifd0?.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
        item.CameraModel = ifd0?.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
        item.Software = ifd0?.GetDescription(ExifDirectoryBase.TagSoftware)?.Trim();

        // The camera's local time, as Jellyfin reads it from other photos.
        var taken = Date(exif, ExifDirectoryBase.TagDateTimeOriginal) ?? Date(ifd0, ExifDirectoryBase.TagDateTime);
        if (taken is { } t)
        {
            item.DateCreated = t.ToUniversalTime();
            item.PremiereDate = t;
            item.ProductionYear = t.Year;
        }

        item.ExposureTime = Number(exif, ExifDirectoryBase.TagExposureTime);
        item.FocalLength = Number(exif, ExifDirectoryBase.TagFocalLength);
        // As Jellyfin stores them: the EXIF (APEX) values, not f-numbers and seconds.
        item.Aperture = Number(exif, ExifDirectoryBase.TagAperture);
        item.ShutterSpeed = Number(exif, ExifDirectoryBase.TagShutterSpeed);
        item.IsoSpeedRating = exif is not null && exif.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso) ? iso : null;

        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        if (gps is not null && gps.TryGetGeoLocation(out var where) && !where.IsZero)
        {
            item.Latitude = where.Latitude;
            item.Longitude = where.Longitude;
            if (gps.TryGetRational(GpsDirectory.TagAltitude, out var alt))
            {
                // Below sea level when the reference says so.
                var below = gps.TryGetInt32(GpsDirectory.TagAltitudeRef, out var aref) && aref == 1;
                item.Altitude = below ? -alt.ToDouble() : alt.ToDouble();
            }
        }
        else
        {
            item.Latitude = item.Longitude = item.Altitude = null;
        }
    }

    private static double? Number(MetadataExtractor.Directory? d, int tag)
        => d is not null && d.TryGetRational(tag, out var r) && r.Denominator != 0 ? r.ToDouble() : null;

    private static DateTime? Date(MetadataExtractor.Directory? d, int tag)
    {
        var s = d?.GetString(tag);
        return s is not null && DateTime.TryParseExact(s.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var t)
            ? t
            : null;
    }
}
