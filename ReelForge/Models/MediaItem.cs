namespace ReelForge.Models;

public sealed class MediaItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Type { get; set; } = ""; // image, video, audio
    public string Kind { get; set; } = ""; // image, background, sfx, voice
    public long Size { get; set; }
    public double DurationSeconds { get; set; }

    public override string ToString() => Name;
}
