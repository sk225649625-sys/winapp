namespace ReelForge.Infrastructure;

public static class SystemInfo
{
    public static object Get()
        => new
        {
            os = Environment.OSVersion.VersionString,
            machine = Environment.MachineName,
            arch = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            app = "ReelForge Native",
            python = false
        };
}
