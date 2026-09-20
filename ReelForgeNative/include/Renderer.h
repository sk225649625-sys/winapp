#pragma once
#include "Models.h"
#include "Timeline.h"
#include <string>
#include <functional>

class Renderer
{
public:
    explicit Renderer(const std::wstring& appDir);
    bool Render(const ProjectModel& project, const std::wstring& outputPath,
                std::wstring& error, std::function<void(double)> progress = {});

private:
    std::wstring m_appDir;
    std::wstring m_cacheDir;
};
