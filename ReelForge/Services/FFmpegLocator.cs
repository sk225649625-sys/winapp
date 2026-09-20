using Microsoft.Win32;
using System.Diagnostics;

namespace ReelForge.Services;

public static class FFmpegLocator
{
    public static string? FindFfmpeg()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin", "ffmpeg.exe"),
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\ffmpeg-8.1.1-essentials_build\bin\ffmpeg.exe",
            @"C:\ffmpeg-8.1.1-full_build\bin\ffmpeg.exe"
        };

        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "ffmpeg",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            var line = p.StandardOutput.ReadLine();
            p.WaitForExit(1500);
            if (!string.IsNullOrWhiteSpace(line) && File.Exists(line.Trim()))
                return line.Trim();
        }
        catch { }

        return null;
    }
}
