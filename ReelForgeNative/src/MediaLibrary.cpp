#include "MediaLibrary.h"
#include <windows.h>
#include <filesystem>
#include <algorithm>
#include <fstream>

namespace fs = std::filesystem;

static long long FileTimeMs(const fs::path& p)
{
    try
    {
        auto ft = fs::last_write_time(p);
        auto sctp = std::chrono::time_point_cast<std::chrono::milliseconds>(
            ft - fs::file_time_type::clock::now() + std::chrono::system_clock::now());
        return sctp.time_since_epoch().count();
    }
    catch (...) { return 0; }
}

static bool HasExt(const std::wstring& ext, std::initializer_list<const wchar_t*> list)
{
    for (auto x : list)
        if (_wcsicmp(ext.c_str(), x) == 0) return true;
    return false;
}

void MediaLibrary::AddPath(const std::wstring& path, const std::wstring& kind)
{
    if (!fs::exists(path) || !fs::is_regular_file(path)) return;

    for (const auto& x : m_items)
        if (_wcsicmp(x.fullPath.c_str(), path.c_str()) == 0)
            return;

    const fs::path p(path);
    const auto ext = p.extension().wstring();

    MediaItem item;
    item.name = p.filename().wstring();
    item.fullPath = fs::absolute(p).wstring();
    item.size = static_cast<long long>(fs::file_size(p));
    item.date = FileTimeMs(p);

    if (HasExt(ext, {L".jpg",L".jpeg",L".png",L".webp",L".bmp",L".gif",L".tif",L".tiff"}))
        item.type = L"image";
    else if (HasExt(ext, {L".mp3",L".wav",L".m4a",L".aac",L".ogg",L".flac",L".wma"}))
        item.type = L"audio";
    else if (HasExt(ext, {L".mp4",L".mov",L".mkv",L".avi",L".webm",L".m4v",L".wmv"}))
        item.type = L"video";
    else
        return;

    if (item.type == L"audio")
        item.kind = kind.empty() ? L"music" : kind;
    else
        item.kind = item.type;

    // Duration probing is intentionally deferred to render/inspector for speed.
    item.duration = item.type == L"image" ? 3.0 : 0.0;
    m_items.push_back(std::move(item));
}

void MediaLibrary::AddPaths(const std::vector<std::wstring>& paths, const std::wstring& kind)
{
    for (const auto& p : paths) AddPath(p, kind);
}

void MediaLibrary::Remove(const std::wstring& path)
{
    m_items.erase(
        std::remove_if(m_items.begin(), m_items.end(),
            [&](const MediaItem& x)
            {
                return _wcsicmp(x.name.c_str(), path.c_str()) == 0 ||
                       _wcsicmp(x.fullPath.c_str(), path.c_str()) == 0;
            }),
        m_items.end());
}

MediaItem* MediaLibrary::Find(const std::wstring& nameOrPath)
{
    for (auto& x : m_items)
        if (_wcsicmp(x.name.c_str(), nameOrPath.c_str()) == 0 ||
            _wcsicmp(x.fullPath.c_str(), nameOrPath.c_str()) == 0)
            return &x;
    return nullptr;
}

std::vector<MediaItem> MediaLibrary::Filter(const std::wstring& type, const std::wstring& kind) const
{
    std::vector<MediaItem> out;
    for (const auto& x : m_items)
    {
        if (!type.empty() && x.type != type) continue;
        if (!kind.empty() && x.kind != kind) continue;
        out.push_back(x);
    }
    return out;
}
