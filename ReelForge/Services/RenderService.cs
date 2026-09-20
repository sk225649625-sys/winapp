using System.Text;
using ReelForge.Infrastructure;
using ReelForge.Models;
using ReelForge.Native;

namespace ReelForge.Services;

public sealed class RenderService
{
    private readonly AppPaths _paths;

    public RenderService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<string> RenderAsync(ProjectModel project, CancellationToken cancellationToken)
    {
        var ffmpeg = FFmpegLocator.FindFfmpeg();
        if (ffmpeg is null)
            throw new InvalidOperationException(
                "FFmpeg nahi mila. ffmpeg.exe ko ReelForge.exe ke paas rakho ya PATH mein add karo.");

        if (project.Clips.Count == 0)
            throw new InvalidOperationException("Timeline khaali hai.");

        var manifest = Path.Combine(_paths.Cache, $"timeline-{Guid.NewGuid():N}.txt");
        var output = Path.Combine(
            _paths.Exports,
            $"{Path.GetFileNameWithoutExtension(project.Name)}-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");

        Directory.CreateDirectory(_paths.Cache);
        Directory.CreateDirectory(_paths.Exports);

        try
        {
            var lines = project.Clips.Select(c =>
                $"{(c.Type.Equals("image", StringComparison.OrdinalIgnoreCase) ? "image" : "video")}\t{c.DurationSeconds:0.###}\t{c.FullPath.Replace("\r", "").Replace("\n", "")}");
            await File.WriteAllLinesAsync(manifest, lines, Encoding.UTF8, cancellationToken);

            var audio = project.Audio.FirstOrDefault()?.FullPath ?? "";
            var error = new StringBuilder(4096);

            var (w, h) = project.Ratio switch
            {
                "9:16" => (1080, 1920),
                "1:1" => (1080, 1080),
                "4:5" => (1080, 1350),
                _ => (1920, 1080)
            };

            var result = await Task.Run(() =>
                NativeEngine.RF_RenderTimeline(
                    ffmpeg,
                    manifest,
                    output,
                    w,
                    h,
                    project.Fps,
                    audio,
                    error,
                    error.Capacity), cancellationToken);

            if (result != 0)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(NativeEngine.GetError(error))
                        ? "Native render failed."
                        : NativeEngine.GetError(error));

            return output;
        }
        finally
        {
            try { File.Delete(manifest); } catch { }
        }
    }
}
