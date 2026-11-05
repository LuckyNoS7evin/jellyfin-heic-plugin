using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Drawing;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// Jellyfin's image encoder (Skia), with HEIC added: a HEIC source is converted to a temporary JPEG
/// first, then the encoder resizes, crops and encodes it as it would any photo; the temporary file
/// is deleted straight after. Everything else goes straight through.
/// </summary>
public sealed class HeicImageEncoder : IImageEncoder
{
    private readonly IImageEncoder _inner;
    private readonly HeicConverter _converter;

    /// <summary>Initializes a new instance of the <see cref="HeicImageEncoder"/> class.</summary>
    /// <param name="inner">The server's own encoder.</param>
    /// <param name="converter">The HEIC converter.</param>
    public HeicImageEncoder(IImageEncoder inner, HeicConverter converter)
    {
        _inner = inner;
        _converter = converter;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedInputFormats
        => new HashSet<string>(_inner.SupportedInputFormats.Concat(Heic.Formats), StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<ImageFormat> SupportedOutputFormats => _inner.SupportedOutputFormats;

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public bool SupportsImageCollageCreation => _inner.SupportsImageCollageCreation;

    /// <inheritdoc />
    public bool SupportsImageEncoding => _inner.SupportsImageEncoding;

    /// <inheritdoc />
    public ImageDimensions GetImageSize(string path)
    {
        if (!Heic.IsHeic(path))
        {
            return _inner.GetImageSize(path);
        }

        // From the container when it says (no decoding); otherwise convert and measure.
        if (Heic.ReadSize(path) is { } size)
        {
            return new ImageDimensions(size.Width, size.Height);
        }

        return WithJpeg(path, 0, 0, _inner.GetImageSize);
    }

    /// <inheritdoc />
    public string GetImageBlurHash(int xComp, int yComp, string path)
        => Heic.IsHeic(path)
            ? WithJpeg(path, 128, 128, jpg => _inner.GetImageBlurHash(xComp, yComp, jpg))
            : _inner.GetImageBlurHash(xComp, yComp, path);

    /// <inheritdoc />
    public string EncodeImage(string inputPath, DateTime dateModified, string outputPath, bool autoOrient, ImageOrientation? orientation, int quality, ImageProcessingOptions options, ImageFormat outputFormat)
    {
        if (!Heic.IsHeic(inputPath))
        {
            return _inner.EncodeImage(inputPath, dateModified, outputPath, autoOrient, orientation, quality, options, outputFormat);
        }

        var (w, h) = Box(options);
        return WithJpeg(inputPath, w, h, jpg =>
        {
            // ffmpeg has already turned the picture upright. Asking for orientation "as is" makes
            // the encoder always write the output, rather than hand back the temporary file when
            // no resizing was asked for.
            var result = _inner.EncodeImage(jpg, dateModified, outputPath, true, ImageOrientation.TopLeft, quality, options, outputFormat);
            return string.Equals(result, jpg, StringComparison.Ordinal) ? Keep(jpg, outputPath) : result;
        });
    }

    /// <inheritdoc />
    public void CreateImageCollage(ImageCollageOptions options, string? libraryName)
    {
        var temps = new List<string>();
        try
        {
            options.InputPaths = options.InputPaths.Select(p =>
            {
                if (!Heic.IsHeic(p))
                {
                    return p;
                }

                var jpg = _converter.ToTempJpeg(p, 600, 600);
                temps.Add(jpg);
                return jpg;
            }).ToList();
            _inner.CreateImageCollage(options, libraryName);
        }
        finally
        {
            temps.ForEach(HeicConverter.TryDelete);
        }
    }

    /// <inheritdoc />
    public void CreateSplashscreen(IReadOnlyList<string> posters, IReadOnlyList<string> backdrops)
        => _inner.CreateSplashscreen(posters, backdrops);

    /// <inheritdoc />
    public int CreateTrickplayTile(ImageCollageOptions options, int quality, int imgWidth, int? imgHeight)
        => _inner.CreateTrickplayTile(options, quality, imgWidth, imgHeight);

    // The smallest picture that still covers what was asked for (0: no size asked).
    private static (int Width, int Height) Box(ImageProcessingOptions o)
    {
        static int Most(params int?[] v) => v.Max(x => x ?? 0);
        return (Most(o.Width, o.MaxWidth, o.FillWidth), Most(o.Height, o.MaxHeight, o.FillHeight));
    }

    private static string Keep(string jpg, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.Copy(jpg, outputPath, true);
        return outputPath;
    }

    private T WithJpeg<T>(string heic, int width, int height, Func<string, T> use)
    {
        var jpg = _converter.ToTempJpeg(heic, width, height);
        try
        {
            return use(jpg);
        }
        finally
        {
            HeicConverter.TryDelete(jpg);
        }
    }
}
