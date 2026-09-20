#pragma once
#include "Models.h"
#include "MediaLibrary.h"
#include <vector>
#include <string>

class Timeline
{
public:
    std::vector<ClipItem>& Clips() { return m_project.clips; }
    std::vector<AudioItem>& Audio() { return m_project.audio; }
    std::vector<VoiceItem>& Voice() { return m_project.voice; }

    ProjectModel& Project() { return m_project; }
    const ProjectModel& Project() const { return m_project; }

    double VideoLength() const;
    double VoiceLength() const;
    void AddSelectedImages(const MediaLibrary& media, const std::vector<std::wstring>& names);
    void AddAudio(const MediaItem& media, double atSeconds);
    void AddVoiceSequence(const MediaLibrary& media, const std::vector<std::wstring>& names);
    void AutoFitToVoice();
    void RemoveClip(size_t index);
    void MoveClip(size_t from, size_t to);
    void ApplyClipToAll(size_t index);

private:
    ProjectModel m_project;
};
