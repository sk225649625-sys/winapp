using System.IO;

namespace ReelForge.Infrastructure;

public sealed class AppPaths
{
    public string BaseDirectory { get; } = AppContext.BaseDirectory;
    public string Root { get; }
    public string Media => Path.Combine(Root, "media");
    public string Projects => Path.Combine(Root, "projects");
    public string Exports => Path.Combine(Root, "exports");
    public string Cache => Path.Combine(Root, "cache");
    public string WebViewData => Path.Combine(Root, "webview2");
    public string WebRoot => Path.Combine(BaseDirectory, "wwwroot");

    public AppPaths()
    {
        // The user asked for the working folders beside the EXE.
        Root = Path.Combine(BaseDirectory, "data");
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Media);
        Directory.CreateDirectory(Projects);
        Directory.CreateDirectory(Exports);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(WebViewData);
    }

    public string SafeMediaPath(string name) => SafeChild(Media, name);
    public string SafeProjectPath(string name) => SafeChild(Projects, name);
    public string SafeExportPath(string name) => SafeChild(Exports, name);

    private static string SafeChild(string root, string name)
    {
        var clean = Path.GetFileName(name?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(clean) || clean is "." or "..")
            throw new ArgumentException("Invalid filename.");
        return Path.Combine(root, clean);
    }
}
