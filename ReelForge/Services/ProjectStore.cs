using System.Text.Json;
using ReelForge.Infrastructure;
using ReelForge.Models;

namespace ReelForge.Services;

public sealed class ProjectStore
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProjectStore(AppPaths paths) => _paths = paths;

    public IEnumerable<string> List()
        => Directory.EnumerateFiles(_paths.Projects, "*.json")
                    .Select(Path.GetFileName)
                    .Where(x => x is not null)!
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

    public Project Load(string name)
    {
        var path = _paths.SafeProjectPath(name);
        if (!File.Exists(path)) return new Project();
        return JsonSerializer.Deserialize<Project>(File.ReadAllText(path), _json) ?? new Project();
    }

    public void Save(string name, Project project)
    {
        var path = _paths.SafeProjectPath(name);
        File.WriteAllText(path, JsonSerializer.Serialize(project, _json));
    }

    public bool Delete(string name)
    {
        var path = _paths.SafeProjectPath(name);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public Project New(string name)
    {
        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";
        var p = new Project();
        Save(name, p);
        return p;
    }
}
