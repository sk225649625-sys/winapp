#pragma once
#include <string>
#include <windows.h>

class Preview
{
public:
    explicit Preview(const std::wstring& cacheDir);
    ~Preview();
    bool LoadImage(HWND hwnd, const std::wstring& path);
    HBITMAP Bitmap() const { return m_bitmap; }
    SIZE BitmapSize() const { return m_size; }
    bool OpenVideo(const std::wstring& path);
    void Clear();

private:
    std::wstring m_cache;
    std::wstring m_current;
    HBITMAP m_bitmap = nullptr;
    SIZE m_size{0,0};
};
