using System.Globalization;
using System.IO;
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

    public IReadOnlyList<ProjectSummary> List()
    {
        var result = new List<ProjectSummary>();
        foreach (var file in Directory.EnumerateFiles(_paths.Projects, "*.json"))
        {
            try
            {
                var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(file), _json) ?? new Project();
                result.Add(new ProjectSummary
                {
                    Name = Path.GetFileName(file),
                    Mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds(),
                    Clips = project.Clips.Count,
                    Voice = project.Voice.Count,
                    Dur = project.Clips.Sum(c => Math.Max(0, c.Dur)).ToString("0.##", CultureInfo.InvariantCulture)
                });
            }
            catch
            {
                // A corrupt project should not stop the project list from loading.
                result.Add(new ProjectSummary { Name = Path.GetFileName(file) });
            }
        }

        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Project Load(string name)
    {
        var path = _paths.SafeProjectPath(NormalizeProjectName(name));
        if (!File.Exists(path)) return new Project();

        try
        {
            return JsonSerializer.Deserialize<Project>(File.ReadAllText(path), _json) ?? new Project();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Project JSON invalid hai: {Path.GetFileName(path)}");
        }
    }

    public void Save(string name, Project project)
    {
        var safeName = NormalizeProjectName(name);
        var path = _paths.SafeProjectPath(safeName);
        var json = JsonSerializer.Serialize(project, _json);
        File.WriteAllText(path, json);
    }

    public bool Delete(string name)
    {
        var path = _paths.SafeProjectPath(NormalizeProjectName(name));
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public Project New(string name)
    {
        var safeName = NormalizeProjectName(name);
        var p = new Project();
        Save(safeName, p);
        return p;
    }

    public static string NormalizeProjectName(string name)
    {
        var clean = Path.GetFileName((name ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(clean)) clean = "last.json";
        if (!clean.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) clean += ".json";
        return clean;
    }
}

public sealed class ProjectSummary
{
    public string Name { get; set; } = "";
    public long Mtime { get; set; }
    public int Clips { get; set; }
    public int Voice { get; set; }
    public string Dur { get; set; } = "0";
}
