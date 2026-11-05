using System.Diagnostics;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HeicPhotos;

/// <summary>
/// Turns a HEIC into a temporary JPEG with Jellyfin's own ffmpeg (8.x reads HEIC, its tiles and
/// its rotation). The caller deletes the file when done: nothing is kept.
/// </summary>
public sealed class HeicConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _services;
    private readonly IApplicationPaths _paths;
    private readonly ILogger<HeicConverter> _logger;

    // A library scan asks for many at once (blurhashes); keep some cores for everything else.
    private readonly SemaphoreSlim _slots = new(Math.Max(1, Environment.ProcessorCount / 2));

    /// <summary>Initializes a new instance of the <see cref="HeicConverter"/> class.</summary>
    /// <param name="services">For the media encoder, looked up when first needed.</param>
    /// <param name="paths">The server's paths (its temp folder).</param>
    /// <param name="logger">The logger.</param>
    public HeicConverter(IServiceProvider services, IApplicationPaths paths, ILogger<HeicConverter> logger)
    {
        _services = services;
        _paths = paths;
        _logger = logger;
    }

    private string Ffmpeg => _services.GetRequiredService<IMediaEncoder>().EncoderPath;

    /// <summary>
    /// Convert to a temporary JPEG at least <paramref name="width"/> wide and <paramref name="height"/>
    /// tall where the picture is that big (0: no need); both 0 converts at full size.
    /// </summary>
    /// <returns>The temporary file's path. The caller deletes it.</returns>
    public string ToTempJpeg(string input, int width = 0, int height = 0)
    {
        var dir = Path.Combine(_paths.TempDirectory, "heic-photos");
        Directory.CreateDirectory(dir);
        var output = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".jpg");

        _slots.Wait();
        try
        {
            // Scaled while decoding when a size is asked for (a thumbnail), so the image library
            // gets a small picture to finish. Some HEICs aren't tiled (no stream group to scale);
            // those convert at full size instead.
            if ((width > 0 || height > 0) && Run(input, output, Scale(width, height)))
            {
                return output;
            }

            if (Run(input, output, null))
            {
                return output;
            }
        }
        finally
        {
            _slots.Release();
        }

        TryDelete(output);
        throw new InvalidOperationException($"ffmpeg couldn't convert {input}");
    }

    /// <summary>Delete a temporary file, quietly.</summary>
    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // At least w wide and h tall (covering both), never larger than the picture itself.
    private static string Scale(int w, int h)
        => string.Create(CultureInfo.InvariantCulture, $"[0:g:0]scale=w=min(iw\\,max({w}\\,{h}*iw/ih)):h=-2");

    private bool Run(string input, string output, string? filter)
    {
        var psi = new ProcessStartInfo(Ffmpeg)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        // One thread per decoder: an iPhone photo is ~48 tiles, each its own HEVC decoder, and by
        // default each starts a thread per core (over a thousand threads for one photo, enough to
        // hit a container's limit when a few convert at once). Single-threaded tiles are faster.
        foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-threads", "1", "-i", input, "-frames:v", "1" })
        {
            psi.ArgumentList.Add(a);
        }

        if (filter is not null)
        {
            psi.ArgumentList.Add("-filter_complex");
            psi.ArgumentList.Add(filter);
        }

        foreach (var a in new[] { "-q:v", "2", "-f", "image2", output })
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi);
        if (p is null)
        {
            return false;
        }

        var err = p.StandardError.ReadToEndAsync();
        _ = p.StandardOutput.ReadToEndAsync();
        if (!p.WaitForExit(Timeout))
        {
            try
            {
                p.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }

            _logger.LogWarning("ffmpeg took too long converting {Path}", input);
            return false;
        }

        if (p.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 0)
        {
            return true;
        }

        _logger.LogDebug("ffmpeg couldn't convert {Path} (filter {Filter}): {Error}", input, filter ?? "none", err.Result.Trim());
        return false;
    }
}
