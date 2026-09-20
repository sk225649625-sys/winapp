using System.IO;

namespace ReelForge.Infrastructure;

public sealed class AppPaths
{
    public string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReelForge");

    public string Media => Path.Combine(Root, "media");
    public string Projects => Path.Combine(Root, "projects");
    public string Exports => Path.Combine(Root, "exports");
    public string Cache => Path.Combine(Root, "cache");
    public string WebRoot => Path.Combine(AppContext.BaseDirectory, "wwwroot");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Media);
        Directory.CreateDirectory(Projects);
        Directory.CreateDirectory(Exports);
        Directory.CreateDirectory(Cache);
    }

    public string SafeMediaPath(string name) => SafeChild(Media, name);
    public string SafeProjectPath(string name) => SafeChild(Projects, name);
    public string SafeExportPath(string name) => SafeChild(Exports, name);

    private static string SafeChild(string root, string name)
    {
        var clean = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(clean) || clean == "." || clean == "..")
            throw new ArgumentException("Invalid filename.");
        return Path.Combine(root, clean);
    }
}
