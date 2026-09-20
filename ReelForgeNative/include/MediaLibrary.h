#pragma once
#include "Models.h"
#include <string>
#include <vector>

class MediaLibrary
{
public:
    std::vector<MediaItem>& Items() { return m_items; }
    const std::vector<MediaItem>& Items() const { return m_items; }

    void AddPath(const std::wstring& path, const std::wstring& kind = L"");
    void AddPaths(const std::vector<std::wstring>& paths, const std::wstring& kind = L"");
    void Remove(const std::wstring& path);
    MediaItem* Find(const std::wstring& nameOrPath);
    std::vector<MediaItem> Filter(const std::wstring& type, const std::wstring& kind = L"") const;

private:
    std::vector<MediaItem> m_items;
};
