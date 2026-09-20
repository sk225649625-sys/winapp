namespace ReelForge.Models;

public sealed class ProjectModel
{
    public string Name { get; set; } = "last.json";
    public string Ratio { get; set; } = "16:9";
    public int Quality { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public List<TimelineItem> Clips { get; set; } = new();
    public List<TimelineItem> Audio { get; set; } = new();
}
