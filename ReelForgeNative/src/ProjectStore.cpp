#include "ProjectStore.h"
#include <windows.h>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <regex>

namespace fs = std::filesystem;

static std::wstring JsonEscape(const std::wstring& s)
{
    std::wstring o;
    for (wchar_t c : s)
    {
        switch (c)
        {
        case L'\\': o += L"\\\\"; break;
        case L'"': o += L"\\\""; break;
        case L'\r': o += L"\\r"; break;
        case L'\n': o += L"\\n"; break;
        default: o += c; break;
        }
    }
    return o;
}

static std::wstring JsonUnescape(std::wstring s)
{
    std::wstring o;
    for (size_t i = 0; i < s.size(); ++i)
    {
        if (s[i] == L'\\' && i + 1 < s.size())
        {
            wchar_t c = s[++i];
            if (c == L'n') o += L'\n';
            else if (c == L'r') o += L'\r';
            else o += c;
        }
        else o += s[i];
    }
    return o;
}

static void WriteString(std::wostream& o, const wchar_t* key, const std::wstring& value, bool comma = true)
{
    o << L"  \"" << key << L"\":\"" << JsonEscape(value) << L"\"" << (comma ? L"," : L"") << L"\n";
}

static void WriteNumber(std::wostream& o, const wchar_t* key, double v, bool comma = true)
{
    o << L"  \"" << key << L"\":" << v << (comma ? L"," : L"") << L"\n";
}

ProjectStore::ProjectStore(const std::wstring& baseDir) : m_baseDir(baseDir) {}

void ProjectStore::Ensure()
{
    fs::create_directories(ProjectsDir());
    fs::create_directories(ExportsDir());
    fs::create_directories(CacheDir());
}

std::wstring ProjectStore::ProjectsDir() const { return (fs::path(m_baseDir) / L"data" / L"projects").wstring(); }
std::wstring ProjectStore::ExportsDir() const { return (fs::path(m_baseDir) / L"data" / L"exports").wstring(); }
std::wstring ProjectStore::CacheDir() const { return (fs::path(m_baseDir) / L"data" / L"cache").wstring(); }

void ProjectStore::Save(const ProjectModel& p) const
{
    Ensure();
    const fs::path file = fs::path(ProjectsDir()) / (p.name.empty() ? L"last.json" : p.name);
    std::wofstream out(file, std::ios::trunc);
    if (!out) return;

    out << L"{\n";
    WriteString(out, L"name", p.name);
    WriteString(out, L"ratio", p.ratio);
    out << L"  \"quality\":" << p.quality << L",\n";
    out << L"  \"fps\":" << p.fps << L",\n";

    out << L"  \"clips\":[\n";
    for (size_t i = 0; i < p.clips.size(); ++i)
    {
        const auto& c = p.clips[i];
        out << L"    {\"file\":\"" << JsonEscape(c.file) << L"\","
            << L"\"dur\":" << c.dur << L","
            << L"\"motion\":\"" << JsonEscape(c.motion) << L"\","
            << L"\"intensity\":" << c.intensity << L","
            << L"\"transition\":\"" << JsonEscape(c.transition) << L"\","
            << L"\"transitionDur\":" << c.transitionDur << L","
            << L"\"look\":\"" << JsonEscape(c.look) << L"\","
            << L"\"fit\":\"" << JsonEscape(c.fit) << L"\","
            << L"\"text\":\"" << JsonEscape(c.text) << L"\"}";
        if (i + 1 != p.clips.size()) out << L",";
        out << L"\n";
    }
    out << L"  ],\n";

    out << L"  \"audio\":[\n";
    for (size_t i = 0; i < p.audio.size(); ++i)
    {
        const auto& a = p.audio[i];
        out << L"    {\"file\":\"" << JsonEscape(a.file) << L"\","
            << L"\"start\":" << a.start << L",\"offset\":" << a.offset
            << L",\"dur\":" << a.dur << L",\"volume\":" << a.volume
            << L",\"fadeIn\":" << a.fadeIn << L",\"fadeOut\":" << a.fadeOut
            << L",\"loop\":" << (a.loop ? L"true" : L"false")
            << L",\"kind\":\"" << JsonEscape(a.kind) << L"\"}";
        if (i + 1 != p.audio.size()) out << L",";
        out << L"\n";
    }
    out << L"  ],\n";

    out << L"  \"voice\":[\n";
    for (size_t i = 0; i < p.voice.size(); ++i)
    {
        const auto& v = p.voice[i];
        out << L"    {\"file\":\"" << JsonEscape(v.file) << L"\","
            << L"\"dur\":" << v.dur << L",\"offset\":" << v.offset
            << L",\"volume\":" << v.volume << L",\"fadeIn\":" << v.fadeIn
            << L",\"fadeOut\":" << v.fadeOut << L"}";
        if (i + 1 != p.voice.size()) out << L",";
        out << L"\n";
    }
    out << L"  ],\n";

    out << L"  \"overlays\":[\n";
    for (size_t i = 0; i < p.overlays.size(); ++i)
    {
        const auto& o = p.overlays[i];
        out << L"    {\"type\":\"" << JsonEscape(o.type) << L"\","
            << L"\"text\":\"" << JsonEscape(o.text) << L"\","
            << L"\"x\":" << o.x << L",\"y\":" << o.y
            << L",\"size\":" << o.size << L",\"color\":\"" << JsonEscape(o.color) << L"\","
            << L"\"timed\":" << (o.timed ? L"true" : L"false")
            << L",\"t0\":" << o.t0 << L",\"t1\":" << o.t1 << L"}";
        if (i + 1 != p.overlays.size()) out << L",";
        out << L"\n";
    }
    out << L"  ]\n";
    out << L"}\n";
}

static std::wstring MatchString(const std::wstring& text, const std::wstring& key, const std::wstring& fallback)
{
    const std::wregex re(L"\"" + key + L"\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
    std::wsmatch m;
    if (std::regex_search(text, m, re))
        return JsonUnescape(m[1].str());
    return fallback;
}

static double MatchNumber(const std::wstring& text, const std::wstring& key, double fallback)
{
    const std::wregex re(L"\"" + key + L"\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
    std::wsmatch m;
    if (std::regex_search(text, m, re))
        return _wtof(m[1].str().c_str());
    return fallback;
}

bool ProjectStore::Load(const std::wstring& fileName, ProjectModel& p) const
{
    std::wifstream in(fs::path(ProjectsDir()) / fileName);
    if (!in) return false;
    std::wstringstream ss;
    ss << in.rdbuf();
    const std::wstring text = ss.str();

    p = ProjectModel{};
    p.name = MatchString(text, L"name", fileName);
    p.ratio = MatchString(text, L"ratio", L"16:9");
    p.quality = static_cast<int>(MatchNumber(text, L"quality", 1080));
    p.fps = static_cast<int>(MatchNumber(text, L"fps", 30));

    // Lightweight loader intentionally reads the core project structure.
    std::wregex clipRe(LR"(\{"file":"((?:\\.|[^"\\])*)","dur":([-0-9.]+),"motion":"((?:\\.|[^"\\])*)","intensity":([-0-9.]+),"transition":"((?:\\.|[^"\\])*)","transitionDur":([-0-9.]+),"look":"((?:\\.|[^"\\])*)","fit":"((?:\\.|[^"\\])*)","text":"((?:\\.|[^"\\])*)"\})");
    for (std::wsregex_iterator it(text.begin(), text.end(), clipRe), end; it != end; ++it)
    {
        ClipItem c;
        c.file = JsonUnescape((*it)[1].str());
        c.dur = _wtof((*it)[2].str().c_str());
        c.motion = JsonUnescape((*it)[3].str());
        c.intensity = _wtof((*it)[4].str().c_str());
        c.transition = JsonUnescape((*it)[5].str());
        c.transitionDur = _wtof((*it)[6].str().c_str());
        c.look = JsonUnescape((*it)[7].str());
        c.fit = JsonUnescape((*it)[8].str());
        c.text = JsonUnescape((*it)[9].str());
        p.clips.push_back(c);
    }

    return true;
}
