#pragma once
#include <string>
#include <vector>

struct MediaItem
{
    std::wstring name;
    std::wstring fullPath;
    std::wstring type; // image, video, audio
    std::wstring kind; // image, music, sfx, voice
    long long size = 0;
    double duration = 0.0;
    long long date = 0;
};

struct AudioItem
{
    std::wstring file;
    double start = 0;
    double offset = 0;
    double dur = 0;
    double volume = 1.0;
    double fadeIn = 0.2;
    double fadeOut = 0.4;
    bool loop = false;
    std::wstring kind;
};

struct VoiceItem
{
    std::wstring file;
    double dur = 3;
    double offset = 0;
    double volume = 1.0;
    double fadeIn = 0.1;
    double fadeOut = 0.2;
};

struct OverlayItem
{
    std::wstring type = L"text";
    std::wstring text;
    double x = 0.5;
    double y = 0.5;
    double size = 0.08;
    std::wstring color = L"#ffffff";
    bool timed = false;
    double t0 = 0;
    double t1 = 3;
};

struct ClipItem
{
    std::wstring file;
    double dur = 3;
    std::wstring motion = L"kenIn";
    double intensity = 0.6;
    std::wstring transition = L"fade";
    double transitionDur = 0.6;
    std::wstring look = L"none";
    std::wstring fit = L"auto";
    std::wstring text;
    std::wstring textStyle = L"fadeup";
    std::wstring textPosition = L"bottom";
    double seed = 0;
};

struct GlobalFx
{
    bool grain = true;
    bool vignette = true;
    bool letterbox = false;
    bool fade = true;
    bool leaks = false;
    double leakAmt = 0.6;
    bool dust = false;
    double dustAmt = 0.5;
    bool lights = false;
    double lightAmt = 0.6;
    bool sparkle = false;
    double sparkAmt = 0.5;
    int bpm = 0;
    double phase = 0;
    double pulse = 0.5;
};

struct AudioFx
{
    bool on = false;
    double amount = 0.5;
};

struct AudioFxState
{
    AudioFx bass, treble, muffle, echo, crush;
    AudioFx pitch;
    double pitchAmount = 0;
};

struct ProjectModel
{
    std::wstring name = L"last.json";
    std::wstring ratio = L"16:9";
    int quality = 1080;
    int fps = 30;
    std::vector<ClipItem> clips;
    std::vector<AudioItem> audio;
    std::vector<VoiceItem> voice;
    GlobalFx fxg;
    AudioFxState afx;
    std::vector<OverlayItem> overlays;
};
