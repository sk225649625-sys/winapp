#include "Timeline.h"
#include "Catalogs.h"
#include <algorithm>
#include <random>

static double Clamp(double v, double a, double b) { return std::min(b, std::max(a, v)); }

double Timeline::VideoLength() const
{
    double t = 0;
    for (const auto& c : m_project.clips) t += std::max(0.0, c.dur);
    return t;
}

double Timeline::VoiceLength() const
{
    double t = 0;
    for (const auto& v : m_project.voice) t += std::max(0.0, v.dur);
    return t;
}

void Timeline::AddSelectedImages(const MediaLibrary& media, const std::vector<std::wstring>& names)
{
    const auto& dm = Catalogs::DefaultMotions();
    const auto& dt = Catalogs::DefaultTransitions();

    int idx = static_cast<int>(m_project.clips.size());
    for (const auto& name : names)
    {
        const auto* m = const_cast<MediaLibrary&>(media).Find(name);
        if (!m) continue;
        ClipItem c;
        c.file = m->fullPath;
        c.dur = m->duration > 0 ? m->duration : 3;
        c.motion = dm[idx % dm.size()];
        c.intensity = 0.6;
        c.transition = dt[idx % dt.size()];
        c.transitionDur = 0.6;
        c.look = L"none";
        c.fit = L"auto";
        c.seed = static_cast<double>(GetTickCount64() % 100000) / 100.0;
        m_project.clips.push_back(c);
        ++idx;
    }
    AutoFitToVoice();
}

void Timeline::AddAudio(const MediaItem& media, double atSeconds)
{
    AudioItem a;
    a.file = media.fullPath;
    a.kind = media.kind;
    a.start = std::max(0.0, atSeconds);
    a.offset = 0;
    a.dur = media.duration > 0 ? media.duration : 10;
    a.volume = 1;
    a.fadeIn = 0.2;
    a.fadeOut = 0.4;
    a.loop = false;
    const double remain = std::max(0.1, VideoLength() - a.start);
    a.dur = std::min(a.dur, remain);
    m_project.audio.push_back(std::move(a));
}

void Timeline::AddVoiceSequence(const MediaLibrary& media, const std::vector<std::wstring>& names)
{
    for (const auto& name : names)
    {
        const auto* m = const_cast<MediaLibrary&>(media).Find(name);
        if (!m) continue;
        VoiceItem v;
        v.file = m->fullPath;
        v.dur = m->duration > 0 ? m->duration : 3;
        m_project.voice.push_back(std::move(v));
    }
    AutoFitToVoice();
}

void Timeline::AutoFitToVoice()
{
    const double total = VoiceLength();
    if (total <= 0 || m_project.clips.empty()) return;

    const double minDur = 0.3;
    const double current = VideoLength();
    if (current <= 0) return;

    double acc = 0;
    for (size_t i = 0; i + 1 < m_project.clips.size(); ++i)
    {
        double wanted = m_project.clips[i].dur * total / current;
        double cap = total - acc - minDur * (m_project.clips.size() - i - 1);
        double v = Clamp(wanted, minDur, std::max(minDur, cap));
        m_project.clips[i].dur = v;
        acc += v;
    }

    m_project.clips.back().dur = std::max(minDur, total - acc);
}

void Timeline::RemoveClip(size_t index)
{
    if (index >= m_project.clips.size()) return;
    m_project.clips.erase(m_project.clips.begin() + index);
    AutoFitToVoice();
}

void Timeline::MoveClip(size_t from, size_t to)
{
    if (from >= m_project.clips.size() || to >= m_project.clips.size() || from == to) return;
    auto x = m_project.clips[from];
    m_project.clips.erase(m_project.clips.begin() + from);
    m_project.clips.insert(m_project.clips.begin() + to, x);
}

void Timeline::ApplyClipToAll(size_t index)
{
    if (index >= m_project.clips.size()) return;
    const auto c = m_project.clips[index];
    for (auto& x : m_project.clips)
    {
        x.dur = c.dur;
        x.motion = c.motion;
        x.intensity = c.intensity;
        x.transition = c.transition;
        x.transitionDur = c.transitionDur;
        x.look = c.look;
        x.fit = c.fit;
    }
    AutoFitToVoice();
}
