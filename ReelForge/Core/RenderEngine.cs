using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using ReelForge.Infrastructure;
using ReelForge.Models;

namespace ReelForge.Core;

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

        var clips = project.Clips ?? [];
        if (clips.Count == 0)
            throw new InvalidOperationException("Timeline khaali hai.");

        var output = Path.Combine(_paths.Exports,
            $"reelforge_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{kind}.mp4");

        var total = clips.Sum(c => Math.Max(.1, c.Dur));
        var fps = project.Fps is 24 or 30 or 60 ? project.Fps : 30;
        var (w, h) = SizeFor(project.Ratio, project.Quality);
        var filters = new StringBuilder();
        var inputs = new List<string>();

        for (var i = 0; i < clips.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var clip = clips[i];
            var dur = Math.Max(.1, clip.Dur);
            var file = _paths.SafeMediaPath(clip.File);
            if (!File.Exists(file))
                throw new FileNotFoundException($"Media file nahi mila: {clip.File}");

            inputs.Add($"-loop 1 -t {dur.ToString(CultureInfo.InvariantCulture)} -i {Quote(file)}");
            if (string.Equals(clip.Fit, "contain", StringComparison.OrdinalIgnoreCase))
            {
                filters.Append($"[{i}:v]scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,format=yuv420p[v{i}];");
            }
            else
            {
                filters.Append($"[{i}:v]scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},format=yuv420p[v{i}];");
            }
            progress((i / (double)clips.Count) * .15);
        }

        var concatInputs = string.Concat(Enumerable.Range(0, clips.Count).Select(i => $"[v{i}]"));
        filters.Append($"{concatInputs}concat=n={clips.Count}:v=1:a=0[vout]");

        var args = new StringBuilder();
        args.Append(string.Join(" ", inputs));
        args.Append(" -filter_complex ");
        args.Append(Quote(filters.ToString()));
        args.Append(" -map \"[vout]\"");
        args.Append(" -an");
        args.Append($" -r {fps}");
        args.Append(" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -movflags +faststart -y ");
        args.Append(Quote(output));

        await RunAsync(_ffmpeg, args.ToString(), total, ct, progress);

        var info = new FileInfo(output);
        return new
        {
            url = "/exports/" + Uri.EscapeDataString(info.Name),
            path = info.FullName,
            seconds = total,
            size = info.Length
        };
    }

    private static async Task RunAsync(string exe, string args, double totalSeconds,
        CancellationToken ct, Action<double> progress)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg start nahi hua.");
        using var cancelRegistration = ct.Register(() =>
        {
            try
            {
                if (!p.HasExited) p.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process may have exited between the check and Kill.
            }
        });

        var stderrTask = p.StandardError.ReadToEndAsync();
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var started = Stopwatch.StartNew();

        while (!p.HasExited)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(250, ct);
            // FFmpeg's progress stream is intentionally not parsed here; keep a
            // smooth visual progress value until the process exits.
            progress(Math.Min(.98, .15 + 0.80 * (1 - Math.Exp(-started.Elapsed.TotalSeconds / 4.0))));
        }

        var stderr = await stderrTask;
        _ = await stdoutTask;
        ct.ThrowIfCancellationRequested();
        progress(1);

        if (p.ExitCode != 0)
        {
            var tail = stderr.Length > 1600 ? stderr[^1600..] : stderr;
            throw new InvalidOperationException("FFmpeg render failed:\n" + tail);
        }
    }

    private static (int w, int h) SizeFor(string ratio, int quality)
    {
        quality = quality <= 480 ? 480 : quality <= 720 ? 720 : quality <= 1080 ? 1080 : 1440;
        static int Even(int value) => Math.Max(2, value % 2 == 0 ? value : value + 1);

        return ratio switch
        {
            "9:16" => (Even(quality * 9 / 16), Even(quality)),
            "1:1" => (Even(quality), Even(quality)),
            "4:5" => (Even(quality * 4 / 5), Even(quality)),
            "21:9" => (Even(quality), Even(quality * 9 / 21)),
            _ => (Even(quality), Even(quality * 9 / 16))
        };
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
