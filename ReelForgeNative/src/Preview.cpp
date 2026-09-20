#include "Preview.h"
#include "FFmpegRunner.h"
#include <shellapi.h>
#include <filesystem>
#include <wincodec.h>

#pragma comment(lib, "windowscodecs.lib")

namespace fs = std::filesystem;

Preview::Preview(const std::wstring& cacheDir) : m_cache(cacheDir)
{
    fs::create_directories(cacheDir);
}

Preview::~Preview()
{
    Clear();
}

bool Preview::LoadImage(HWND, const std::wstring& path)
{
    Clear();
    m_current = path;

    if (!fs::exists(path)) return false;

    IWICImagingFactory* factory = nullptr;
    IWICBitmapDecoder* decoder = nullptr;
    IWICBitmapFrameDecode* frame = nullptr;
    IWICFormatConverter* converter = nullptr;

    bool ok = false;
    do
    {
        if (FAILED(CoCreateInstance(
            CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
            IID_PPV_ARGS(&factory)))) break;

        if (FAILED(factory->CreateDecoderFromFilename(
            path.c_str(), nullptr, GENERIC_READ,
            WICDecodeMetadataCacheOnLoad, &decoder))) break;

        if (FAILED(decoder->GetFrame(0, &frame))) break;

        if (FAILED(factory->CreateFormatConverter(&converter))) break;

        if (FAILED(converter->Initialize(
            frame, GUID_WICPixelFormat32bppBGRA,
            WICBitmapDitherTypeNone, nullptr, 0.0,
            WICBitmapPaletteTypeCustom))) break;

        UINT w = 0, h = 0;
        if (FAILED(converter->GetSize(&w, &h)) || w == 0 || h == 0) break;

        BITMAPINFO bi{};
        bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bi.bmiHeader.biWidth = static_cast<LONG>(w);
        bi.bmiHeader.biHeight = -static_cast<LONG>(h);
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = BI_RGB;

        void* bits = nullptr;
        HDC dc = GetDC(nullptr);
        m_bitmap = CreateDIBSection(dc, &bi, DIB_RGB_COLORS, &bits, nullptr, 0);
        ReleaseDC(nullptr, dc);
        if (!m_bitmap || !bits) break;

        const UINT stride = w * 4;
        const UINT bytes = stride * h;
        if (FAILED(converter->CopyPixels(nullptr, stride, bytes, static_cast<BYTE*>(bits))))
        {
            DeleteObject(m_bitmap);
            m_bitmap = nullptr;
            break;
        }

        m_size = {static_cast<LONG>(w), static_cast<LONG>(h)};
        ok = true;
    } while (false);

    if (converter) converter->Release();
    if (frame) frame->Release();
    if (decoder) decoder->Release();
    if (factory) factory->Release();

    return ok;
}

bool Preview::OpenVideo(const std::wstring& path)
{
    if (!fs::exists(path)) return false;

    const auto ffplay = FFmpegRunner::FindFfplay();
    if (!ffplay.empty())
    {
        std::wstring args = FFmpegRunner::Quote(path);
        ShellExecuteW(nullptr, L"open", ffplay.c_str(), args.c_str(), nullptr, SW_SHOWNORMAL);
        return true;
    }

    ShellExecuteW(nullptr, L"open", path.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
    return true;
}

void Preview::Clear()
{
    if (m_bitmap)
    {
        DeleteObject(m_bitmap);
        m_bitmap = nullptr;
    }
    m_size = {0,0};
    m_current.clear();
}
