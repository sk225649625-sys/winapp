using System.Diagnostics;

namespace ReelForge.Infrastructure;

public static class FFmpegLocator
{
    public static string? FindFFmpeg()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
            Path.Combine(Environment.CurrentDirectory, "ffmpeg.exe"),
            @"C:\ffmpeg-8.1.1-essentials_build\bin\ffmpeg.exe",
            @"C:\ffmpeg-8.1.1-essentials_build\ffmpeg.exe"
        };

        foreach (var p in candidates)
            if (File.Exists(p)) return p;

        return FindOnPath("ffmpeg.exe");
    }

    public static string? FindFFprobe()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ffprobe.exe"),
            Path.Combine(Environment.CurrentDirectory, "ffprobe.exe"),
            @"C:\ffmpeg-8.1.1-essentials_build\bin\ffprobe.exe",
            @"C:\ffmpeg-8.1.1-essentials_build\ffprobe.exe"
        };

        foreach (var p in candidates)
            if (File.Exists(p)) return p;

        return FindOnPath("ffprobe.exe");
    }

    private static string? FindOnPath(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var p = Path.Combine(dir.Trim(), exe);
                if (File.Exists(p)) return p;
            }
            catch { }
        }
        return null;
    }
}
