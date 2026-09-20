using System.Text.Json;
using ReelForge.Infrastructure;

namespace ReelForge.Services;

public sealed class MediaStore
{
    private readonly AppPaths _paths;
    public MediaStore(AppPaths paths) => _paths = paths;

    public object List()
    {
        var items = Directory.EnumerateFiles(_paths.Media)
            .Select(p => new FileInfo(p))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new
            {
                name = f.Name,
                type = IsImage(f.Extension) ? "image" : "audio",
                kind = InferKind(f.Extension, f.Name),
                size = f.Length,
                date = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeMilliseconds()
            });
        return items;
    }

    private static bool IsImage(string ext) =>
        new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff" }
        .Contains(ext, StringComparer.OrdinalIgnoreCase);

    private static string InferKind(string ext, string name)
    {
        if (name.Contains("voice", StringComparison.OrdinalIgnoreCase)) return "voice";
        if (name.Contains("sfx", StringComparison.OrdinalIgnoreCase)) return "sfx";
        return "music";
    }
}
