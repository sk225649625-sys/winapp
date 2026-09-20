namespace ReelForge.Infrastructure;

public sealed class AppPaths
{
    public string Root { get; } = AppContext.BaseDirectory;
    public string Data { get; }
    public string Projects { get; }
    public string Cache { get; }
    public string Exports { get; }
    public string Logs { get; }

    public AppPaths()
    {
        Data = Path.Combine(Root, "data");
        Projects = Path.Combine(Data, "projects");
        Cache = Path.Combine(Data, "cache");
        Exports = Path.Combine(Data, "exports");
        Logs = Path.Combine(Data, "logs");
    }

    public void Ensure()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Projects);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Exports);
        Directory.CreateDirectory(Logs);
    }
}
