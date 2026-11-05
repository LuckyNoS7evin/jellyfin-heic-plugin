# Jellyfin HEIC Photos Plugin

Shows **HEIC/HEIF photos** (what iPhones take) in Jellyfin's photo libraries. Jellyfin's image library can't read HEIC, so without this plugin the server skips those files: they never appear in any Jellyfin app.

## How it works

- **Scanning:** `.heic`, `.heif` and `.hif` files in a Photos library (or a Home Videos library with photos turned on) are listed as photos, and a folder of them is a photo album.
- **Details:** each photo's date taken, camera, exposure settings, GPS position and size are read from the file, as Jellyfin does for JPEGs.
- **Pictures:** whenever an app asks for a HEIC photo's image (a thumbnail, a full-screen view, the original size), the plugin converts it with **Jellyfin's own ffmpeg** to a temporary JPEG, Jellyfin resizes it as it would any photo, and the temporary file is deleted straight away.

**No converted copies are kept beside your photos, and your photos are never changed.** Jellyfin caches the sizes it serves (thumbnails, mostly) in its own cache folder, as it does for every image, and its daily cache cleanup deletes any older than 30 days.

Every Jellyfin app benefits: the web client, the phone and TV apps. iPhone photos are stored as dozens of small tiles; a thumbnail takes about a tenth of a second to convert and a full photo about two tenths, after which Jellyfin's cache answers.

## Requirements

- Jellyfin 12.4 or later, with its bundled ffmpeg (jellyfin-ffmpeg 8 reads HEIC, its tiles and its rotation).

## Also: photos Jellyfin's reader misses

Jellyfin reads other photos' details (JPEG, PNG, camera raw...) with a library that isn't safe to use on several files at once, and a library scan does exactly that. Now and then a photo fails ("Image Provider - Error reading image tag ... Index was outside the bounds of the array" in the log) and is left with no date, camera or GPS position. Which ones is down to timing; the files are fine.

When a photo comes out of Jellyfin's reader with none of those details, this plugin reads it again with its own reader (which has no such problem) and fills in what it finds: date taken, camera, settings, GPS position and rotation. It never clears anything Jellyfin did read. A rescan of the library repairs photos that failed in earlier scans.

## Notes

- **Live Photos:** an iPhone Live Photo is a HEIC plus a short `.mov` clip of the same name. Jellyfin normally hides a photo that has a video of the same name beside it (it takes the photo to be the video's poster). This plugin lists the HEIC anyway; the clip shows as a video too, as before.
- **Official clients' "original" view** may download the original file, which browsers other than Safari can't show. Thumbnails and resized views always come back as JPEG or WebP.

## Install

In Jellyfin go to **Dashboard → Plugins → Repositories**, add

```
https://luckynos7evin.github.io/jellyfin-plugins/manifest.json
```

then install **HEIC Photos** from the catalogue, restart Jellyfin, and scan your photo libraries.

## Build

```
dotnet build Jellyfin.Plugin.HeicPhotos/Jellyfin.Plugin.HeicPhotos.csproj -c Release
```

Copy `Jellyfin.Plugin.HeicPhotos.dll`, `MetadataExtractor.dll` and `XmpCore.dll` from `bin/Release/net10.0/` into a folder under Jellyfin's `plugins` directory.
