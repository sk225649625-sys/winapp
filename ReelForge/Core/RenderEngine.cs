using System.Diagnostics;
using System.Globalization;
using System.Text;
using ReelForge.Infrastructure;
using ReelForge.Models;

namespace ReelForge.Services;

public sealed class RenderEngine
{
    private readonly AppPaths _paths;
    private readonly string? _ffmpeg;

    public RenderEngine(AppPaths paths)
    {
        _paths = paths;
        _ffmpeg = FFmpegLocator.FindFFmpeg();
    }

    public async Task<object> RenderAsync(Project project, string kind, Action<double> progress, CancellationToken ct)
    {
        if (_ffmpeg is null)
            throw new InvalidOperationException("FFmpeg nahi mila. ffmpeg.exe ko ReelForge.exe ke paas ya PATH me rakhein.");

        if (project.Clips.Count == 0)
            throw new InvalidOperationException("Timeline khaali hai.");

        var output = Path.Combine(_paths.Exports, $"reelforge_{DateTime.Now:yyyyMMdd_HHmmss}_{kind}.mp4");
        var total = project.Clips.Sum(c => Math.Max(.1, c.Dur));
        var fps = project.Fps is 24 or 30 or 60 ? project.Fps : 30;
        var (w, h) = SizeFor(project.Ratio, project.Quality);

        // Native baseline renderer: each still becomes a timed video segment.
        // The project model is preserved so additional effect parity can be added
        // without changing the editor API.
        var inputs = new List<string>();
        var filters = new StringBuilder();
        for (int i = 0; i < project.Clips.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var c = project.Clips[i];
            var file = _paths.SafeMediaPath(c.File);
            if (!File.Exists(file)) throw new FileNotFoundException($"Media file nahi mila: {c.File}");

            inputs.Add($"-loop 1 -t {c.Dur.ToString(CultureInfo.InvariantCulture)} -i \"{file}\"");
            filters.Append($"[{i}:v]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,format=yuv420p[v{i}];");
            progress((i / (double)Math.Max(1, project.Clips.Count)) * .25);
        }

        var concatInputs = string.Concat(Enumerable.Range(0, project.Clips.Count).Select(i => $"[v{i}]"));
        filters.Append($"{concatInputs}concat=n={project.Clips.Count}:v=1:a=0[vout]");

        var args = $"{string.Join(" ", inputs)} -filter_complex \"{filters}\" -map \"[vout]\" -r {fps} -c:v libx264 -preset veryfast -crf 18 -movflags +faststart -y \"{output}\"";
        await RunAsync(_ffmpeg, args, ct, p => progress(.25 + p * .75));

        var info = new FileInfo(output);
        return new
        {
            url = "/exports/" + Uri.EscapeDataString(info.Name),
            path = info.FullName,
            seconds = total,
            size = info.Length
        };
    }

    private static async Task RunAsync(string exe, string args, CancellationToken ct, Action<double> progress)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg start nahi hua.");
        var stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        progress(1);
        if (p.ExitCode != 0)
            throw new InvalidOperationException("FFmpeg render failed: " + stderr[^Math.Min(stderr.Length, 1200)..]);
    }

    private static (int w, int h) SizeFor(string ratio, int quality)
    {
        quality = quality <= 480 ? 480 : quality <= 720 ? 720 : quality <= 1080 ? 1080 : 1440;
        return ratio switch
        {
            "9:16" => (quality * 9 / 16, quality),
            "1:1" => (quality, quality),
            "4:5" => (quality * 4 / 5, quality),
            "21:9" => (quality, quality * 9 / 21),
            _ => (quality, quality * 9 / 16)
        };
    }
}
