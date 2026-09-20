namespace ReelForge.Models;

public sealed class TimelineItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Type { get; set; } = "";
    public string Kind { get; set; } = "";
    public double DurationSeconds { get; set; } = 5;
    public string Motion { get; set; } = "none";
    public string Transition { get; set; } = "none";
    public string Look { get; set; } = "none";
}
