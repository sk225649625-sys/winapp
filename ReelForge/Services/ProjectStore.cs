using System.Text.Json;
using ReelForge.Infrastructure;
using ReelForge.Models;

namespace ReelForge.Services;

public sealed class ProjectStore
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ProjectStore(AppPaths paths)
    {
        _paths = paths;
    }

    public IEnumerable<string> List()
        => Directory.EnumerateFiles(_paths.Projects, "*.json")
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))!
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

    public ProjectModel Load(string name)
    {
        var safe = Normalize(name);
        var path = Path.Combine(_paths.Projects, safe);
        if (!File.Exists(path))
            return new ProjectModel { Name = safe };

        var p = JsonSerializer.Deserialize<ProjectModel>(File.ReadAllText(path), _json)
                ?? new ProjectModel { Name = safe };
        p.Name = safe;
        return p;
    }

    public void Save(ProjectModel model)
    {
        model.Name = Normalize(model.Name);
        var path = Path.Combine(_paths.Projects, model.Name);
        File.WriteAllText(path, JsonSerializer.Serialize(model, _json));
    }

    public static string Normalize(string name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "last.json" : Path.GetFileName(name.Trim());
        return n.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? n : n + ".json";
    }
}
