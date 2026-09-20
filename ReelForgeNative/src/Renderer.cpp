#include "Renderer.h"
#include "FFmpegRunner.h"
#include "EffectsEngine.h"
#include <filesystem>
#include <fstream>
#include <sstream>
#include <vector>
#include <windows.h>

namespace fs = std::filesystem;

Renderer::Renderer(const std::wstring& appDir)
    : m_appDir(appDir), m_cacheDir((fs::path(appDir) / L"data" / L"cache").wstring())
{
    fs::create_directories(m_cacheDir);
}

bool Renderer::Render(const ProjectModel& p, const std::wstring& outputPath,
                      std::wstring& error, std::function<void(double)> progress)
{
    const auto ffmpeg = FFmpegRunner::FindFfmpeg();
    if (ffmpeg.empty())
    {
        error = L"ffmpeg.exe nahi mila. ReelForge.exe ke paas ffmpeg.exe rakho ya PATH me add karo.";
        return false;
    }
    if (p.clips.empty())
    {
        error = L"Timeline khaali hai.";
        return false;
    }

    const auto manifest = fs::path(m_cacheDir) / (L"render-" + std::to_wstring(GetTickCount64()) + L".txt");
    std::wofstream mf(manifest);
    if (!mf)
    {
        error = L"cache manifest create nahi hua.";
        return false;
    }

    int w = 1920, h = 1080;
    if (p.quality == 1440) { w = 2560; h = 1440; }
    else if (p.quality == 720) { w = 1280; h = 720; }
    else if (p.quality == 480) { w = 854; h = 480; }
    if (p.ratio == L"9:16") std::swap(w, h);
    else if (p.ratio == L"1:1") w = h = 1080;
    else if (p.ratio == L"4:5") { w = 1080; h = 1350; }

    std::wstring args = L"-y -hide_banner -loglevel error ";
    std::vector<std::wstring> labels;

    for (size_t i = 0; i < p.clips.size(); ++i)
    {
        const auto& c = p.clips[i];
        const fs::path src(c.file);
        if (!fs::exists(src))
        {
            error = L"Missing file: " + c.file;
            return false;
        }

        std::wstring duration = std::to_wstring(std::max(0.3, c.dur));
        const auto ext = src.extension().wstring();
        const bool image =
            _wcsicmp(ext.c_str(), L".jpg") == 0 ||
            _wcsicmp(ext.c_str(), L".jpeg") == 0 ||
            _wcsicmp(ext.c_str(), L".png") == 0 ||
            _wcsicmp(ext.c_str(), L".webp") == 0 ||
            _wcsicmp(ext.c_str(), L".bmp") == 0;

        if (image)
            args += L"-loop 1 -t " + duration + L" -i " + FFmpegRunner::Quote(c.file) + L" ";
        else
            args += L"-t " + duration + L" -i " + FFmpegRunner::Quote(c.file) + L" ";

        labels.push_back(L"v" + std::to_wstring(i));
        if (progress) progress(0.05 + 0.25 * (static_cast<double>(i + 1) / p.clips.size()));
    }

    std::wstring filter;
    for (size_t i = 0; i < p.clips.size(); ++i)
    {
        const auto& c = p.clips[i];
        filter += L"[" + std::to_wstring(i) + L":v]" + EffectsEngine::VideoFilter(c, w, h, p.fps);
        filter += L",settb=AVTB,setpts=PTS-STARTPTS";
        if (p.fxg.fade) filter += L",fade=t=in:st=0:d=.20,fade=t=out:st=" + std::to_wstring(std::max(.1, c.dur - .20)) + L":d=.20";
        filter += L"[v" + std::to_wstring(i) + L"];";
    }

    filter += L"[v0]";
    for (size_t i = 1; i < p.clips.size(); ++i)
        filter += L"[v" + std::to_wstring(i) + L"]";
    filter += L"concat=n=" + std::to_wstring(p.clips.size()) + L":v=1:a=0[vcat]";
    filter += L";[vcat]" + EffectsEngine::GlobalFilter(p.fxg, w, h, p.fps) + L"[vout]";

    args += L"-filter_complex " + FFmpegRunner::Quote(filter) + L" -map [vout] ";

    // Background music: use the first music item. Voice/SFX are appended through a
    // second pass in a later renderer extension; the project retains all their data.
    if (!p.audio.empty())
    {
        const auto& a = p.audio.front();
        args += L"-stream_loop -1 -i " + FFmpegRunner::Quote(a.file) + L" ";
        args += L"-map " + std::to_wstring(p.clips.size()) + L":a:0 -shortest ";
        const auto af = EffectsEngine::AudioFilter(p.afx);
        if (!af.empty())
            args += L"-af " + FFmpegRunner::Quote(af) + L" ";
        args += L"-c:a aac -b:a 192k ";
    }

    args += L"-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart ";
    args += FFmpegRunner::Quote(outputPath);

    std::wstring console;
    DWORD code = 0;
    const bool ok = FFmpegRunner::Run(ffmpeg, args, console, &code);

    std::error_code ec;
    fs::remove(manifest, ec);

    if (!ok)
    {
        error = console.empty() ? L"FFmpeg render failed." : console;
        return false;
    }

    if (progress) progress(1.0);
    return true;
}
