#pragma once
#include "Models.h"
#include <string>

class ProjectStore
{
public:
    explicit ProjectStore(const std::wstring& baseDir);
    void Ensure();
    void Save(const ProjectModel& project) const;
    bool Load(const std::wstring& file, ProjectModel& project) const;
    std::wstring ProjectsDir() const;
    std::wstring ExportsDir() const;
    std::wstring CacheDir() const;

private:
    std::wstring m_baseDir;
};
