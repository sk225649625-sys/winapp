namespace ReelForge.Models;

public sealed class Project
{
    public string Ratio { get; set; } = "16:9";
    public int Quality { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public List<Clip> Clips { get; set; } = [];
    public List<AudioClip> Audio { get; set; } = [];
    public List<VoiceClip> Voice { get; set; } = [];
    public Dictionary<string, object?> Fxg { get; set; } = new();
    public Dictionary<string, object?> Afx { get; set; } = new();
    public List<Overlay> Overlays { get; set; } = [];
}

public sealed class Clip
{
    public string File { get; set; } = "";
    public double Dur { get; set; } = 3;
    public string Motion { get; set; } = "kenIn";
    public double Intensity { get; set; } = .6;
    public string Trans { get; set; } = "none";
    public double TransDur { get; set; } = .6;
    public string Look { get; set; } = "none";
    public string Fit { get; set; } = "auto";
    public string Text { get; set; } = "";
    public string Tstyle { get; set; } = "fadeup";
    public string Tpos { get; set; } = "bottom";
    public double Seed { get; set; }
}

public sealed class AudioClip
{
    public string File { get; set; } = "";
    public double Start { get; set; }
    public double Dur { get; set; } = 3;
    public double Offset { get; set; }
    public double Vol { get; set; } = 1;
    public double FadeIn { get; set; } = .2;
    public double FadeOut { get; set; } = .4;
    public bool Loop { get; set; }
}

public sealed class VoiceClip
{
    public string File { get; set; } = "";
    public double Dur { get; set; } = 3;
    public double Offset { get; set; }
    public double Vol { get; set; } = 1;
    public double FadeIn { get; set; } = .1;
    public double FadeOut { get; set; } = .2;
}

public sealed class Overlay
{
    public string Text { get; set; } = "";
    public double T0 { get; set; }
    public double T1 { get; set; }
    public double X { get; set; } = .5;
    public double Y { get; set; } = .5;
    public double Size { get; set; } = 42;
    public double Opacity { get; set; } = 1;
}
