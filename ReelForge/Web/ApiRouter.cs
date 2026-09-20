using System.Net;
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
    private readonly AppPaths _paths;
    private readonly ProjectStore _projects;
    private readonly MediaStore _media;
    private readonly JobManager _jobs;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ApiRouter(AppPaths paths)
    {
        _paths = paths;
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
            var path = Uri.UnescapeDataString(uri.AbsolutePath);

            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                var result = await Api(path, uri, req);
                e.Response = Response(200, "application/json; charset=utf-8", JsonSerializer.Serialize(result, _json));
                return;
            }

            if (path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/thumb/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/exports/", StringComparison.OrdinalIgnoreCase))
            {
                var name = Uri.UnescapeDataString(path[(path.IndexOf('/', 1) + 1)..]);
                var root = path.StartsWith("/exports/", StringComparison.OrdinalIgnoreCase) ? _paths.Exports : _paths.Media;
                var file = Path.Combine(root, Path.GetFileName(name));
                if (!File.Exists(file))
                {
                    e.Response = Response(404, "text/plain", "Not found");
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(file);
                e.Response = Response(200, Mime(file), bytes);
                return;
            }

            if (path == "/" || path == "/editor.html")
            {
                var file = Path.Combine(_paths.WebRoot, "editor.html");
                e.Response = Response(200, "text/html; charset=utf-8", await File.ReadAllTextAsync(file));
                return;
            }

            var local = Path.Combine(_paths.WebRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(local))
                e.Response = Response(200, Mime(local), await File.ReadAllBytesAsync(local));
            else
                e.Response = Response(404, "text/plain", "Not found");
        }
        catch (Exception ex)
        {
            e.Response = Response(500, "application/json; charset=utf-8",
                JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
        }
    }

    private async Task<object> Api(string path, Uri uri, CoreWebView2WebResourceRequest req)
    {
        if (path == "/api/hello") return new { ok = true, app = "ReelForge", python = false, native = true, system = SystemInfo.Get() };
        if (path == "/api/projects") return new { ok = true, projects = _projects.List() };
        if (path == "/api/media") return new { ok = true, media = _media.List() };
        if (path == "/api/progress")
        {
            var s = _jobs.State;
            return new { ok = true, kind = s.Kind, status = s.Status, progress = s.Progress, elapsed = s.Elapsed, result = s.Result, error = s.Error };
        }

        if (path == "/api/cancel")
        {
            _jobs.Cancel();
            return new { ok = true };
        }

        if (path == "/api/openfolder")
        {
            var which = QueryString.Get(uri, "which") ?? "exports";
            var dir = which == "media" ? _paths.Media : which == "projects" ? _paths.Projects : _paths.Exports;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            return new { ok = true };
        }

        if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path == "/api/project")
        {
            var name = QueryString.Get(uri, "name") ?? "last.json";
            return new { ok = true, name, project = _projects.Load(name) };
        }

        var body = await ReadBody(req);
        var node = string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body)!.AsObject();

        if (path == "/api/save")
        {
            var name = node["name"]?.GetValue<string>() ?? "last.json";
            var project = node["project"]?.Deserialize<Project>(_json) ?? new Project();
            _projects.Save(name, project);
            return new { ok = true, name };
        }

        if (path == "/api/project/new")
        {
            var name = node["name"]?.GetValue<string>() ?? "project";
            if (string.IsNullOrWhiteSpace(name)) return new { ok = false, error = "project name required" };
            var project = _projects.New(name);
            return new { ok = true, name = name.EndsWith(".json") ? name : name + ".json", project, projects = _projects.List() };
        }

        if (path == "/api/project/delete")
        {
            var name = node["name"]?.GetValue<string>() ?? "";
            return new { ok = _projects.Delete(name), projects = _projects.List() };
        }

        if (path == "/api/preview" || path == "/api/export")
        {
            var project = node["project"]?.Deserialize<Project>(_json) ?? new Project();
            var kind = path.EndsWith("preview") ? "preview" : "export";
            return new { ok = _jobs.Start(project, kind), error = _jobs.State.Error };
        }

        if (path == "/api/upload" && req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            return await Upload(req);

        return new { ok = false, error = "Unknown API route" };
    }

    private async Task<object> Upload(CoreWebView2WebResourceRequest req)
    {
        // The editor sends multipart/form-data. Parse the multipart payload without
        // adding any server or Python dependency.
        var contentType = req.Headers.GetHeader("Content-Type");
        var boundary = contentType.Split("boundary=", StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim('"');
        if (string.IsNullOrWhiteSpace(boundary)) return new { ok = false, error = "multipart boundary missing" };

        using var ms = new MemoryStream();
        req.Content?.CopyTo(ms);
        var bytes = ms.ToArray();
        var marker = Encoding.UTF8.GetBytes("--" + boundary);
        var parts = MultipartParser.Split(bytes, marker);

        var saved = new List<string>();
        foreach (var part in parts)
        {
            var headerEnd = MultipartParser.IndexOf(part, Encoding.UTF8.GetBytes("\r\n\r\n"));
            if (headerEnd < 0) continue;
            var header = Encoding.UTF8.GetString(part, 0, headerEnd);
            var dataStart = headerEnd + 4;
            var dataEnd = part.Length;
            if (dataEnd >= 2 && part[dataEnd - 2] == '\r' && part[dataEnd - 1] == '\n') dataEnd -= 2;

            var cd = header.Split("\r\n").FirstOrDefault(x => x.StartsWith("Content-Disposition", StringComparison.OrdinalIgnoreCase)) ?? "";
            var markerName = "filename=\"";
            var idx = cd.IndexOf(markerName, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var end = cd.IndexOf('"', idx + markerName.Length);
            if (end < 0) continue;
            var filename = Path.GetFileName(cd[(idx + markerName.Length)..end]);
            if (string.IsNullOrWhiteSpace(filename)) continue;

            var target = _paths.SafeMediaPath(filename);
            await File.WriteAllBytesAsync(target, bytes[dataStart..dataEnd]);
            saved.Add(filename);
        }

        return new { ok = true, saved, count = saved.Count };
    }

    private static async Task<string> ReadBody(CoreWebView2WebResourceRequest req)
    {
        if (req.Content is null) return "";
        using var sr = new StreamReader(req.Content, Encoding.UTF8, true, leaveOpen: false);
        return await sr.ReadToEndAsync();
    }

    private static CoreWebView2WebResourceResponse Response(int status, string mime, string text)
        => new(status.ToString(), "OK", "Content-Type: " + mime + "\r\nCache-Control: no-store", new MemoryStream(Encoding.UTF8.GetBytes(text)));

    private static CoreWebView2WebResourceResponse Response(int status, string mime, byte[] bytes)
        => new(status.ToString(), "OK", "Content-Type: " + mime + "\r\nCache-Control: no-store", new MemoryStream(bytes));

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
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream"
    };
}

internal static class MultipartParser
{
    public static List<byte[]> Split(byte[] data, byte[] marker)
    {
        var result = new List<byte[]>();
        var positions = new List<int>();
        int p = 0;
        while ((p = IndexOf(data, marker, p)) >= 0) { positions.Add(p); p += marker.Length; }
        for (int i = 0; i + 1 < positions.Count; i++)
        {
            var start = positions[i] + marker.Length;
            var len = positions[i + 1] - start;
            if (len > 0) result.Add(data[start..(start + len)]);
        }
        return result;
    }

    public static int IndexOf(byte[] data, byte[] pattern, int start = 0)
    {
        for (int i = start; i <= data.Length - pattern.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }
}
