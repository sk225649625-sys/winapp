using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReelForge.Infrastructure;

namespace ReelForge.Services;

public sealed class MediaStore
{
    private readonly AppPaths _paths;
    private readonly string _metaFile;
    private readonly object _metaGate = new();
    private Dictionary<string, string> _kinds = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CachedMeta> _cache = new(StringComparer.OrdinalIgnoreCase);

    public MediaStore(AppPaths paths)
    {
        _paths = paths;
        _metaFile = Path.Combine(_paths.Cache, "media-meta.json");
        LoadMeta();
    }

    public IReadOnlyList<MediaItem> List()
    {
        var items = new List<MediaItem>();
        foreach (var p in Directory.EnumerateFiles(_paths.Media))
        {
            try
            {
                var f = new FileInfo(p);
                var image = IsImage(f.Extension);
                var stamp = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeMilliseconds();
                var key = f.Name;
                var duration = 0d;
                if (!image)
                {
                    if (_cache.TryGetValue(key, out var cached) &&
                        cached.Size == f.Length &&
                        cached.Date == stamp)
                    {
                        duration = cached.Duration;
                    }
                    else
                    {
                        duration = ProbeDurationSeconds(f.FullName);
                        _cache[key] = new CachedMeta { Size = f.Length, Date = stamp, Duration = duration };
                        SaveMetaLocked();
                    }
                }

                items.Add(new MediaItem
                {
                    Name = f.Name,
                    Type = image ? "image" : "audio",
                    Kind = image ? "image" : GetKind(f.Name),
                    Size = f.Length,
                    Date = stamp,
                    Duration = duration
                });
            }
            catch
            {
                // Ignore a file that disappears while the library is being scanned.
            }
        }

        return items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<MediaItem> SaveUploadAsync(Stream content, string filename, string? kind)
    {
        var clean = Path.GetFileName((filename ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(clean))
            throw new ArgumentException("Filename missing hai.");

        var target = _paths.SafeMediaPath(clean);
        await using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(output);
        }

        if (!IsImage(Path.GetExtension(clean)))
        {
            var selectedKind = NormalizeKind(kind);
            lock (_metaGate)
            {
                _kinds[clean] = selectedKind;
                SaveMetaLocked();
            }
        }

        var fi = new FileInfo(target);
        return new MediaItem
        {
            Name = fi.Name,
            Type = IsImage(fi.Extension) ? "image" : "audio",
            Kind = IsImage(fi.Extension) ? "image" : GetKind(fi.Name),
            Size = fi.Length,
            Date = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
            Duration = IsImage(fi.Extension) ? 0 : ProbeDurationSeconds(fi.FullName)
        };
    }

    public bool Delete(string name)
    {
        var clean = Path.GetFileName((name ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(clean)) return false;

        var path = _paths.SafeMediaPath(clean);
        if (!File.Exists(path)) return false;

        File.Delete(path);
        lock (_metaGate)
        {
            _kinds.Remove(clean);
            _cache.Remove(clean);
            SaveMetaLocked();
        }
        return true;
    }

    private string GetKind(string name)
    {
        lock (_metaGate)
            return _kinds.TryGetValue(name, out var kind) ? kind : InferKind(name);
    }

    private static bool IsImage(string ext) =>
        ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif" or ".tif" or ".tiff";

    private static string InferKind(string name)
    {
        if (name.Contains("voice", StringComparison.OrdinalIgnoreCase)) return "voice";
        if (name.Contains("sfx", StringComparison.OrdinalIgnoreCase)) return "sfx";
        return "music";
    }

    private static string NormalizeKind(string? kind)
        => kind?.Trim().ToLowerInvariant() switch
        {
            "voice" => "voice",
            "sfx" => "sfx",
            _ => "music"
        };

    private void LoadMeta()
    {
        try
        {
            if (!File.Exists(_metaFile))
            {
                _kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _cache = new Dictionary<string, CachedMeta>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var root = JsonNode.Parse(File.ReadAllText(_metaFile))?.AsObject();
            if (root is not null && root["kinds"] is JsonObject kindsNode)
            {
                _kinds = kindsNode.Deserialize<Dictionary<string, string>>()
                         ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                _cache = root["cache"]?.Deserialize<Dictionary<string, CachedMeta>>()
                         ?? new Dictionary<string, CachedMeta>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                // Backward compatibility with the old simple dictionary format.
                _kinds = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_metaFile))
                         ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _cache = new Dictionary<string, CachedMeta>(StringComparer.OrdinalIgnoreCase);
            }

            _kinds = new Dictionary<string, string>(_kinds, StringComparer.OrdinalIgnoreCase);
            _cache = new Dictionary<string, CachedMeta>(_cache, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _kinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _cache = new Dictionary<string, CachedMeta>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveMetaLocked()
    {
        try
        {
            var root = new
            {
                kinds = _kinds,
                cache = _cache
            };
            File.WriteAllText(_metaFile, JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Metadata is optional; the media file itself remains valid.
        }
    }

    public async Task<MediaItem> ImportFileAsync(string sourcePath, string? kind)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file nahi mila.", sourcePath);

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        var isImage = IsImage(ext);
        var originalName = Path.GetFileName(sourcePath);
        var targetName = MakeUniqueMediaName(originalName);
        var target = _paths.SafeMediaPath(targetName);

        await using (var input = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 1024, useAsync: true))
        await using (var output = new FileStream(
            target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 1024 * 1024, useAsync: true))
        {
            await input.CopyToAsync(output, 1024 * 1024);
        }

        if (!isImage)
        {
            lock (_metaGate)
            {
                _kinds[targetName] = NormalizeKind(kind);
                SaveMetaLocked();
            }
        }

        var fi = new FileInfo(target);
        var date = new DateTimeOffset(fi.LastWriteTimeUtc).ToUnixTimeMilliseconds();
        var duration = isImage ? 0 : ProbeDurationSeconds(fi.FullName);

        lock (_metaGate)
        {
            _cache[targetName] = new CachedMeta { Size = fi.Length, Date = date, Duration = duration };
            SaveMetaLocked();
        }

        return new MediaItem
        {
            Name = fi.Name,
            Type = isImage ? "image" : "audio",
            Kind = isImage ? "image" : GetKind(fi.Name),
            Size = fi.Length,
            Date = date,
            Duration = duration
        };
    }

    private string MakeUniqueMediaName(string original)
    {
        var clean = Path.GetFileName((original ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(clean))
            throw new ArgumentException("Filename missing hai.");

        var candidate = clean;
        var ext = Path.GetExtension(clean);
        var stem = Path.GetFileNameWithoutExtension(clean);
        var i = 1;

        while (File.Exists(_paths.SafeMediaPath(candidate)))
            candidate = $"{stem} ({i++}){ext}";

        return candidate;
    }

    private static double ProbeDurationSeconds(string path)
    {
        var ffprobe = FFmpegLocator.FindFFprobe();
        if (ffprobe is null) return 0;

        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.StartInfo.ArgumentList.Add("-v");
            p.StartInfo.ArgumentList.Add("error");
            p.StartInfo.ArgumentList.Add("-show_entries");
            p.StartInfo.ArgumentList.Add("format=duration");
            p.StartInfo.ArgumentList.Add("-of");
            p.StartInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            p.StartInfo.ArgumentList.Add(path);

            if (!p.Start()) return 0;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2500);
            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? Math.Max(0, value)
                : 0;
        }
        catch
        {
            return 0;
        }
    }
}

public sealed class MediaItem
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Size { get; set; }
    public long Date { get; set; }
    public double Duration { get; set; }
}


public sealed class CachedMeta
{
    public long Size { get; set; }
    public long Date { get; set; }
    public double Duration { get; set; }
}
