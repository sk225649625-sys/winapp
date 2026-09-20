using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using ReelForge.Infrastructure;
using ReelForge.Models;
using ReelForge.Services;

namespace ReelForge.Web;

public sealed class ApiRouter
{
    private static readonly byte[] EmptyPng =
    [
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,
        0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,
        0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0A,0x49,0x44,0x41,
        0x54,0x78,0x9C,0x63,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,
        0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    ];

    private readonly AppPaths _paths;
    private readonly CoreWebView2Environment _environment;
    private readonly ProjectStore _projects;
    private readonly MediaStore _media;
    private readonly JobManager _jobs;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiRouter(AppPaths paths, CoreWebView2Environment environment)
    {
        _paths = paths;
        _environment = environment;
        _projects = new ProjectStore(paths);
        _media = new MediaStore(paths);
        _jobs = new JobManager(paths);
    }

    public async void HandleRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var req = e.Request;
            var uri = new Uri(req.Uri);
            if (!uri.Host.Equals("reelforge.local", StringComparison.OrdinalIgnoreCase))
                return;

            var path = Uri.UnescapeDataString(uri.AbsolutePath);

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                var result = await Api(path, uri, req);
                e.Response = JsonResponse(200, result);
                return;
            }

            if (path.StartsWith("/thumb/", StringComparison.OrdinalIgnoreCase))
            {
                await ServeThumb(e, path);
                return;
            }

            if (path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/exports/", StringComparison.OrdinalIgnoreCase))
            {
                await ServeFile(e, path);
                return;
            }

            if (path == "/" || path == "/editor.html")
            {
                var file = Path.Combine(_paths.WebRoot, "editor.html");
                e.Response = File.Exists(file)
                    ? Response(200, "text/html; charset=utf-8", await File.ReadAllBytesAsync(file))
                    : TextResponse(404, "editor.html nahi mila.");
                return;
            }

            await ServeWebAsset(e, path);
        }
        catch (Exception ex)
        {
            e.Response = JsonResponse(500, new { ok = false, error = ex.Message });
        }
    }

    private async Task<object> Api(string path, Uri uri, CoreWebView2WebResourceRequest req)
    {
        if (path == "/api/hello")
        {
            var system = SystemInfo.Get();
            return new
            {
                ok = true,
                app = "ReelForge",
                python = false,
                native = true,
                media = _media.List(),
                projects = _projects.List(),
                cores = Environment.ProcessorCount,
                font = true,
                system
            };
        }

        if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            if (path == "/api/projects") return new { ok = true, projects = _projects.List() };
            if (path == "/api/media") return new { ok = true, media = _media.List() };
            if (path == "/api/progress")
            {
                var s = _jobs.State;
                return new
                {
                    ok = true,
                    kind = s.Kind,
                    status = s.Status,
                    progress = s.Progress,
                    elapsed = s.Elapsed,
                    result = s.Result,
                    error = s.Error,
                    message = s.Error
                };
            }

            if (path == "/api/project")
            {
                var name = QueryString.Get(uri, "name") ?? "last.json";
                var safe = ProjectStore.NormalizeProjectName(name);
                return new { ok = true, name = safe, project = _projects.Load(safe) };
            }
        }

        if (path == "/api/openfolder")
        {
            var which = QueryString.Get(uri, "which") ?? "exports";
            var dir = which.Equals("media", StringComparison.OrdinalIgnoreCase)
                ? _paths.Media
                : which.Equals("projects", StringComparison.OrdinalIgnoreCase)
                    ? _paths.Projects
                    : _paths.Exports;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"")
            {
                UseShellExecute = true
            });
            return new { ok = true };
        }

        if (path == "/api/cancel")
        {
            _jobs.Cancel();
            return new { ok = true };
        }

        // IMPORTANT: upload must be handled before ReadBody(req).
        // ReadBody consumes the WebView2 request Content stream; if upload is
        // handled afterward the file body is empty/disposed and the editor
        // reports that the upload failed.
        if (path == "/api/upload" && req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return await Upload(req);

        var body = await ReadBody(req);
        var node = string.IsNullOrWhiteSpace(body)
            ? new JsonObject()
            : JsonNode.Parse(body)?.AsObject() ?? new JsonObject();

        if (path == "/api/save")
        {
            var name = ProjectStore.NormalizeProjectName(node["name"]?.GetValue<string>() ?? "last.json");
            var project = node["project"]?.Deserialize<Project>(_json) ?? new Project();
            _projects.Save(name, project);
            return new { ok = true, name };
        }

        if (path == "/api/project/new")
        {
            var raw = node["name"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw)) return new { ok = false, error = "project name required" };
            var name = ProjectStore.NormalizeProjectName(raw);
            var project = _projects.New(name);
            return new { ok = true, name, project, projects = _projects.List() };
        }

        if (path == "/api/project/delete")
        {
            var name = node["name"]?.GetValue<string>() ?? "";
            var deleted = _projects.Delete(name);
            return new { ok = deleted, error = deleted ? null : "project nahi mila", projects = _projects.List() };
        }

        if (path == "/api/delete")
        {
            var name = node["name"]?.GetValue<string>() ?? QueryString.Get(uri, "name") ?? "";
            var deleted = _media.Delete(name);
            return new { ok = deleted, error = deleted ? null : "media nahi mila", media = _media.List() };
        }

        if (path == "/api/preview" || path == "/api/export")
        {
            var project = node["project"]?.Deserialize<Project>(_json) ?? new Project();
            var kind = path == "/api/preview" ? "preview" : "export";
            var started = _jobs.Start(project, kind);
            return new { ok = started, error = started ? null : "ek render pehle se chal raha hai" };
        }

        return new { ok = false, error = "Unknown API route" };
    }

    private async Task<object> Upload(CoreWebView2WebResourceRequest req)
    {
        var fileName = Header(req, "X-Name");
        var kind = Header(req, "X-Kind");

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Uri.UnescapeDataString(fileName);
            if (req.Content is null) return new { ok = false, error = "upload body empty" };
            var media = await _media.SaveUploadAsync(req.Content, fileName, kind);
            return new { ok = true, media };
        }

        // Backward-compatible multipart handler for clients that send FormData.
        var contentType = Header(req, "Content-Type");
        var boundaryToken = contentType?
            .Split(new[] { "boundary=" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(boundaryToken))
            return new { ok = false, error = "X-Name missing hai" };

        var boundary = boundaryToken.Trim().Trim('"');
        using var ms = new MemoryStream();
        if (req.Content is not null) await req.Content.CopyToAsync(ms);
        var payload = ms.ToArray();
        var saved = new List<MediaItem>();

        foreach (var part in MultipartParser.Split(payload, Encoding.UTF8.GetBytes("--" + boundary)))
        {
            var headerEnd = MultipartParser.IndexOf(part, Encoding.UTF8.GetBytes("\r\n\r\n"));
            if (headerEnd < 0) continue;
            var header = Encoding.UTF8.GetString(part, 0, headerEnd);
            var dataStart = headerEnd + 4;
            var dataEnd = part.Length;
            if (dataEnd >= 2 && part[dataEnd - 2] == '\r' && part[dataEnd - 1] == '\n') dataEnd -= 2;
            var filename = MultipartParser.FileNameFromDisposition(header);
            if (string.IsNullOrWhiteSpace(filename)) continue;

            await using var content = new MemoryStream(part, dataStart, dataEnd - dataStart, writable: false);
            saved.Add(await _media.SaveUploadAsync(content, filename, kind));
        }

        return new { ok = saved.Count > 0, saved, count = saved.Count };
    }

    private async Task ServeThumb(CoreWebView2WebResourceRequestedEventArgs e, string path)
    {
        var name = Path.GetFileName(path["/thumb/".Length..]);
        if (string.IsNullOrWhiteSpace(name))
        {
            e.Response = Response(404, "image/png", EmptyPng);
            return;
        }

        var file = _paths.SafeMediaPath(name);
        if (!File.Exists(file))
        {
            e.Response = Response(404, "image/png", EmptyPng);
            return;
        }

        var ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp" or ".tif" or ".tiff")
        {
            e.Response = Response(200, Mime(file), await File.ReadAllBytesAsync(file));
            return;
        }

        // Audio library uses the same thumbnail element. A 1x1 transparent PNG
        // keeps the existing editor layout intact without changing editor.html.
        e.Response = Response(200, "image/png", EmptyPng);
    }

    private async Task ServeFile(CoreWebView2WebResourceRequestedEventArgs e, string path)
    {
        var prefix = path.StartsWith("/exports/", StringComparison.OrdinalIgnoreCase) ? "/exports/" : "/media/";
        var name = Path.GetFileName(path[prefix.Length..]);
        if (string.IsNullOrWhiteSpace(name))
        {
            e.Response = TextResponse(404, "file missing");
            return;
        }

        var root = prefix == "/exports/" ? _paths.Exports : _paths.Media;
        var file = Path.Combine(root, name);
        if (!File.Exists(file))
        {
            e.Response = TextResponse(404, "file not found");
            return;
        }

        e.Response = Response(200, Mime(file), await File.ReadAllBytesAsync(file));
    }

    private async Task ServeWebAsset(CoreWebView2WebResourceRequestedEventArgs e, string path)
    {
        var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(_paths.WebRoot, relative));
        var root = Path.GetFullPath(_paths.WebRoot) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            e.Response = TextResponse(404, "not found");
            return;
        }

        e.Response = Response(200, Mime(candidate), await File.ReadAllBytesAsync(candidate));
    }

    private static string Header(CoreWebView2WebResourceRequest req, string name)
    {
        try { return req.Headers.GetHeader(name) ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static async Task<string> ReadBody(CoreWebView2WebResourceRequest req)
    {
        if (req.Content is null) return string.Empty;
        using var sr = new StreamReader(req.Content, Encoding.UTF8, true, 1024, leaveOpen: false);
        return await sr.ReadToEndAsync();
    }

    private CoreWebView2WebResourceResponse JsonResponse(int status, object value)
        => Response(status, "application/json; charset=utf-8",
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private CoreWebView2WebResourceResponse TextResponse(int status, string text)
        => Response(status, "text/plain; charset=utf-8", text);

    private CoreWebView2WebResourceResponse Response(int status, string mime, string text)
        => Response(status, mime, Encoding.UTF8.GetBytes(text));

    private CoreWebView2WebResourceResponse Response(int status, string mime, byte[] bytes)
        => _environment.CreateWebResourceResponse(
            new MemoryStream(bytes, writable: false),
            status,
            StatusText(status),
            "Content-Type: " + mime + "\r\nCache-Control: no-store");

    private static string StatusText(int code) => code switch
    {
        200 => "OK",
        400 => "Bad Request",
        404 => "Not Found",
        500 => "Internal Server Error",
        _ => "OK"
    };

    private static string Mime(string p) => Path.GetExtension(p).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".aac" => "audio/aac",
        ".ogg" => "audio/ogg",
        ".flac" => "audio/flac",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream"
    };
}

internal static class MultipartParser
{
    public static List<byte[]> Split(byte[] data, byte[] marker)
    {
        var result = new List<byte[]>();
        var positions = new List<int>();
        var p = 0;
        while ((p = IndexOf(data, marker, p)) >= 0)
        {
            positions.Add(p);
            p += marker.Length;
        }

        for (var i = 0; i + 1 < positions.Count; i++)
        {
            var start = positions[i] + marker.Length;
            var length = positions[i + 1] - start;
            if (length > 0) result.Add(data[start..(start + length)]);
        }
        return result;
    }

    public static string? FileNameFromDisposition(string headers)
    {
        var line = headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(x => x.StartsWith("Content-Disposition", StringComparison.OrdinalIgnoreCase));
        if (line is null) return null;
        const string token = "filename=\"";
        var idx = line.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var end = line.IndexOf('"', idx + token.Length);
        if (end < 0) return null;
        return Path.GetFileName(line[(idx + token.Length)..end]);
    }

    public static int IndexOf(byte[] data, byte[] pattern, int start = 0)
    {
        if (pattern.Length == 0) return start;
        for (var i = Math.Max(0, start); i <= data.Length - pattern.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] == pattern[j]) continue;
                ok = false;
                break;
            }
            if (ok) return i;
        }
        return -1;
    }
}
