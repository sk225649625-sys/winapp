#include "../include/ReelForgeNative.h"

#include <windows.h>
#include <string>
#include <vector>
#include <fstream>
#include <sstream>
#include <filesystem>
#include <algorithm>
#include <locale>
#include <codecvt>

namespace fs = std::filesystem;

static void SetError(wchar_t* buffer, int chars, const std::wstring& message)
{
    if (!buffer || chars <= 0) return;
    wcsncpy_s(buffer, static_cast<size_t>(chars), message.c_str(), _TRUNCATE);
}

static std::wstring QuoteArg(const std::wstring& value)
{
    std::wstring out = L"\"";
    for (wchar_t ch : value)
    {
        if (ch == L'"') out += L'\\';
        out += ch;
    }
    out += L"\"";
    return out;
}

static bool RunProcess(const std::wstring& exe, const std::wstring& args, std::wstring& error)
{
    std::wstring cmd = QuoteArg(exe) + L" " + args;

    std::vector<wchar_t> mutableCmd(cmd.begin(), cmd.end());
    mutableCmd.push_back(L'\0');

    STARTUPINFOW si{};
    PROCESS_INFORMATION pi{};
    si.cb = sizeof(si);

    HANDLE hStdOutRead = nullptr;
    HANDLE hStdOutWrite = nullptr;
    HANDLE hStdErrRead = nullptr;
    HANDLE hStdErrWrite = nullptr;

    SECURITY_ATTRIBUTES sa{};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;

    if (!CreatePipe(&hStdErrRead, &hStdErrWrite, &sa, 0)) {
        error = L"CreatePipe(stderr) failed";
        return false;
    }
    if (!SetHandleInformation(hStdErrRead, HANDLE_FLAG_INHERIT, 0)) {
        CloseHandle(hStdErrRead); CloseHandle(hStdErrWrite);
        error = L"SetHandleInformation(stderr) failed";
        return false;
    }

    if (!CreatePipe(&hStdOutRead, &hStdOutWrite, &sa, 0)) {
        CloseHandle(hStdErrRead); CloseHandle(hStdErrWrite);
        error = L"CreatePipe(stdout) failed";
        return false;
    }
    if (!SetHandleInformation(hStdOutRead, HANDLE_FLAG_INHERIT, 0)) {
        CloseHandle(hStdOutRead); CloseHandle(hStdOutWrite);
        CloseHandle(hStdErrRead); CloseHandle(hStdErrWrite);
        error = L"SetHandleInformation(stdout) failed";
        return false;
    }

    si.dwFlags |= STARTF_USESTDHANDLES;
    si.hStdError = hStdErrWrite;
    si.hStdOutput = hStdOutWrite;
    si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

    BOOL ok = CreateProcessW(
        nullptr,
        mutableCmd.data(),
        nullptr,
        nullptr,
        TRUE,
        CREATE_NO_WINDOW,
        nullptr,
        nullptr,
        &si,
        &pi);

    CloseHandle(hStdErrWrite);
    CloseHandle(hStdOutWrite);

    if (!ok) {
        CloseHandle(hStdErrRead);
        CloseHandle(hStdOutRead);
        error = L"FFmpeg process start failed. Error code: " + std::to_wstring(GetLastError());
        return false;
    }

    auto readPipe = [](HANDLE h) {
        std::string bytes;
        char buf[4096];
        DWORD got = 0;
        while (ReadFile(h, buf, sizeof(buf), &got, nullptr) && got > 0)
            bytes.append(buf, buf + got);
        return bytes;
    };

    std::string errBytes = readPipe(hStdErrRead);
    std::string outBytes = readPipe(hStdOutRead);

    WaitForSingleObject(pi.hProcess, INFINITE);

    DWORD code = 0;
    GetExitCodeProcess(pi.hProcess, &code);

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    CloseHandle(hStdErrRead);
    CloseHandle(hStdOutRead);

    if (code != 0) {
        std::wstring wide;
        if (!errBytes.empty())
        {
            int n = MultiByteToWideChar(CP_UTF8, 0, errBytes.data(), static_cast<int>(errBytes.size()), nullptr, 0);
            if (n > 0)
            {
                wide.resize(n);
                MultiByteToWideChar(CP_UTF8, 0, errBytes.data(), static_cast<int>(errBytes.size()), wide.data(), n);
            }
        }
        error = wide.empty() ? L"FFmpeg failed." : wide;
        return false;
    }

    return true;
}

extern "C" __declspec(dllexport)
int __cdecl RF_CopyFileFast(
    const wchar_t* sourcePath,
    const wchar_t* destinationPath,
    wchar_t* errorBuffer,
    int errorBufferChars)
{
    try
    {
        if (!sourcePath || !destinationPath) {
            SetError(errorBuffer, errorBufferChars, L"source/destination missing");
            return 1;
        }
        fs::copy_file(sourcePath, destinationPath, fs::copy_options::overwrite_existing);
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::wstring msg;
        int n = MultiByteToWideChar(CP_UTF8, 0, ex.what(), -1, nullptr, 0);
        if (n > 0) {
            msg.resize(n - 1);
            MultiByteToWideChar(CP_UTF8, 0, ex.what(), -1, msg.data(), n);
        } else {
            msg = L"copy failed";
        }
        SetError(errorBuffer, errorBufferChars, msg);
        return 1;
    }
}

extern "C" __declspec(dllexport)
int __cdecl RF_RenderTimeline(
    const wchar_t* ffmpegPath,
    const wchar_t* manifestPath,
    const wchar_t* outputPath,
    int width,
    int height,
    int fps,
    const wchar_t* audioPath,
    wchar_t* errorBuffer,
    int errorBufferChars)
{
    try
    {
        if (!ffmpegPath || !manifestPath || !outputPath) {
            SetError(errorBuffer, errorBufferChars, L"render arguments missing");
            return 2;
        }

        std::wifstream in(manifestPath);
        in.imbue(std::locale(std::locale::classic(), new std::codecvt_utf8_utf16<wchar_t>));
        if (!in.is_open()) {
            SetError(errorBuffer, errorBufferChars, L"manifest open failed");
            return 3;
        }

        struct Entry { std::wstring type; double duration; std::wstring path; };
        std::vector<Entry> entries;

        std::wstring line;
        while (std::getline(in, line))
        {
            if (line.empty()) continue;

            size_t a = line.find(L'\t');
            size_t b = (a == std::wstring::npos) ? std::wstring::npos : line.find(L'\t', a + 1);
            if (a == std::wstring::npos || b == std::wstring::npos) continue;

            Entry e;
            e.type = line.substr(0, a);
            e.duration = std::max(0.1, std::stod(line.substr(a + 1, b - a - 1)));
            e.path = line.substr(b + 1);
            entries.push_back(std::move(e));
        }

        if (entries.empty()) {
            SetError(errorBuffer, errorBufferChars, L"timeline empty");
            return 4;
        }

        std::wstring args;
        args += L"-y -hide_banner -loglevel error ";

        for (const auto& e : entries)
        {
            if (e.type == L"image")
            {
                args += L"-loop 1 -t " + std::to_wstring(e.duration) + L" -i " + QuoteArg(e.path) + L" ";
            }
            else
            {
                args += L"-i " + QuoteArg(e.path) + L" ";
            }
        }

        const int clipCount = static_cast<int>(entries.size());
        std::wstring filter;

        for (int i = 0; i < clipCount; ++i)
        {
            filter += L"[" + std::to_wstring(i) + L":v]scale=" +
                      std::to_wstring(width) +
                      L":" + std::to_wstring(height) +
                      L":force_original_aspect_ratio=decrease,pad=" +
                      std::to_wstring(width) + L":" + std::to_wstring(height) +
                      L":(ow-iw)/2:(oh-ih)/2,setsar=1,fps=" +
                      std::to_wstring(fps) +
                      L",format=yuv420p[v" + std::to_wstring(i) + L"];";
        }

        filter += L"[v";
        filter += L"0";
        filter += L"]";
        for (int i = 1; i < clipCount; ++i)
            filter += L"[v" + std::to_wstring(i) + L"]";
        filter += L"concat=n=" + std::to_wstring(clipCount) + L":v=1:a=0[vout]";

        args += L"-filter_complex " + QuoteArg(filter) + L" ";
        args += L"-map [vout] ";

        bool hasAudio = audioPath && *audioPath;
        if (hasAudio)
        {
            args += L"-stream_loop -1 -i " + QuoteArg(audioPath) + L" ";
            const int audioIndex = clipCount;
            args += L"-map " + std::to_wstring(audioIndex) + L":a:0 -shortest ";
            args += L"-c:a aac -b:a 192k ";
        }

        args += L"-c:v libx264 -preset veryfast -crf 20 -movflags +faststart ";
        args += QuoteArg(outputPath);

        std::wstring error;
        if (!RunProcess(ffmpegPath, args, error))
        {
            SetError(errorBuffer, errorBufferChars, error);
            return 5;
        }

        return 0;
    }
    catch (const std::exception& ex)
    {
        std::wstring msg;
        int n = MultiByteToWideChar(CP_UTF8, 0, ex.what(), -1, nullptr, 0);
        if (n > 0) {
            msg.resize(n - 1);
            MultiByteToWideChar(CP_UTF8, 0, ex.what(), -1, msg.data(), n);
        } else {
            msg = L"render failed";
        }
        SetError(errorBuffer, errorBufferChars, msg);
        return 6;
    }
}
