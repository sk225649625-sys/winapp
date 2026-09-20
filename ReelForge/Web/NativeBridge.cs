using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using ReelForge.Infrastructure;
using ReelForge.Models;
using ReelForge.Services;

using System.IO;
namespace ReelForge.Web;

public sealed class NativeBridge
{
    private readonly CoreWebView2 _webView;
    private readonly AppPaths _paths;
    private readonly ProjectStore _projects;
    private readonly MediaStore _media;
    private readonly JobManager _jobs;

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public NativeBridge(CoreWebView2 webView, AppPaths paths)
    {
        _webView = webView;
        _paths = paths;
        _projects = new ProjectStore(paths);
        _media = new MediaStore(paths);
        _jobs = new JobManager(paths);
    }

    public void Attach() => _webView.WebMessageReceived += OnWebMessageReceived;

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var raw = e.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(raw)) return;

            var msg = JsonNode.Parse(raw)?.AsObject();
            if (msg is null) return;

            var type = msg["type"]?.GetValue<string>() ?? "";
            if (type == "rpc")
            {
                var id = msg["id"]?.GetValue<long>() ?? 0;
                var method = (msg["method"]?.GetValue<string>() ?? "GET").ToUpperInvariant();
                var path = msg["path"]?.GetValue<string>() ?? "/";
                var body = msg["body"];
                var result = await HandleRpcAsync(method, path, body);
                await ReplyAsync(id, new { ok = true, result });
                return;
            }

            if (type == "pickFiles")
            {
                var requestId = msg["id"]?.GetValue<long>() ?? 0;
                var kind = msg["kind"]?.GetValue<string>() ?? "image";
                var result = await PickAndImportAsync(kind);
                await ReplyAsync(requestId, result);
            }
        }
        catch (Exception ex)
        {
            var id = 0L;
            try
            {
                var raw = e.TryGetWebMessageAsString();
                var msg = JsonNode.Parse(raw)?.AsObject();
                id = msg?["id"]?.GetValue<long>() ?? 0;
            }
            catch { }

            if (id != 0)
                await ReplyAsync(id, new { ok = false, error = ex.Message });
        }
    }

    private async Task<object> HandleRpcAsync(string method, string path, JsonNode? body)
    {
        if (path.StartsWith("/api/cancel", StringComparison.OrdinalIgnoreCase))
        {
            _jobs.Cancel();
            return new { ok = true };
        }

        if (method == "GET")
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
                    localMode = true,
                    media = _media.List(),
                    projects = _projects.List(),
                    cores = Environment.ProcessorCount,
                    font = true,
                    system
                };
            }

            if (path == "/api/projects")
                return new { ok = true, projects = _projects.List() };

            if (path == "/api/media")
                return new { ok = true, media = _media.List() };

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

            if (path.StartsWith("/api/project", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri("https://reelforge.local" + path);
                var name = QueryString.Get(uri, "name") ?? "last.json";
                var safe = ProjectStore.NormalizeProjectName(name);
                return new { ok = true, name = safe, project = _projects.Load(safe) };
            }

            if (path.StartsWith("/api/openfolder", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri("https://reelforge.local" + path);
                var which = QueryString.Get(uri, "which") ?? "exports";
                OpenFolder(which);
                return new { ok = true };
            }

            if (path == "/api/hello")
                return new { ok = true };
        }

        var node = body?.AsObject() ?? new JsonObject();

        if (path == "/api/save")
        {
            var name = ProjectStore.NormalizeProjectName(
                node["name"]?.GetValue<string>() ?? "last.json");
            var project = node["project"]?.Deserialize<Project>(_json) ?? new Project();
            _projects.Save(name, project);
            return new { ok = true, name };
        }

        if (path == "/api/project/new")
        {
            var raw = node["name"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
                return new { ok = false, error = "project name required" };

            var name = ProjectStore.NormalizeProjectName(raw);
            var project = _projects.New(name);
            return new { ok = true, name, project, projects = _projects.List() };
        }

        if (path == "/api/project/delete")
        {
            var name = node["name"]?.GetValue<string>() ?? "";
            var deleted = _projects.Delete(name);
            return new
            {
                ok = deleted,
                error = deleted ? null : "project nahi mila",
                projects = _projects.List()
            };
        }

        if (path == "/api/delete")
        {
            var name = node["name"]?.GetValue<string>() ?? "";
            var deleted = _media.Delete(name);
            return new
            {
                ok = deleted,
                error = deleted ? null : "media nahi mila",
                media = _media.List()
            };
        }

        if (path == "/api/preview" || path == "/api/export")
        {
            var project = node["project"]?.Deserialize<Project>(_json) ?? new Project();
            var kind = path == "/api/preview" ? "preview" : "export";
            var started = _jobs.Start(project, kind);
            return new
            {
                ok = started,
                error = started ? null : "ek render pehle se chal raha hai"
            };
        }

        return new { ok = false, error = "Unknown local command: " + path };
    }

    private async Task<object> PickAndImportAsync(string kind)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            AddExtension = false,
            Filter = kind == "image"
                ? "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif;*.tif;*.tiff|All files|*.*"
                : "Audio|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.wma|All files|*.*",
            Title = kind == "image"
                ? "ReelForge — photos add karo"
                : $"ReelForge — {kind} audio add karo"
        };

        if (dialog.ShowDialog() != true)
            return new { ok = true, cancelled = true, media = Array.Empty<MediaItem>(), failed = Array.Empty<string>() };

        var imported = new List<MediaItem>();
        var failed = new List<object>();

        foreach (var source in dialog.FileNames)
        {
            try
            {
                imported.Add(await _media.ImportFileAsync(source, kind == "image" ? null : kind));
            }
            catch (Exception ex)
            {
                failed.Add(new { name = Path.GetFileName(source), error = ex.Message });
            }
        }

        return new
        {
            ok = imported.Count > 0 || failed.Count == 0,
            cancelled = false,
            media = imported,
            failed,
            count = imported.Count
        };
    }

    private void OpenFolder(string which)
    {
        var dir = which.Equals("media", StringComparison.OrdinalIgnoreCase)
            ? _paths.Media
            : which.Equals("projects", StringComparison.OrdinalIgnoreCase)
                ? _paths.Projects
                : _paths.Exports;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"")
        {
            UseShellExecute = true
        });
    }

    private async Task ReplyAsync(long id, object payload)
    {
        var json = JsonSerializer.Serialize(payload, _json);
        var script = $"window.__reelforgeNativeResult({id}, {json});";
        await _webView.ExecuteScriptAsync(script);
    }
}
