#include "FFmpegRunner.h"
#include <windows.h>
#include <filesystem>
#include <sstream>

namespace fs = std::filesystem;

std::wstring FFmpegRunner::Quote(const std::wstring& s)
{
    std::wstring q = L"\"";
    for (wchar_t c : s)
    {
        if (c == L'"') q += L'\\';
        q += c;
    }
    q += L"\"";
    return q;
}

std::wstring FFmpegRunner::FindFfmpeg()
{
    std::vector<std::wstring> c;
    wchar_t exe[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    fs::path base(exe);
    c = {
        (base.parent_path() / L"ffmpeg.exe").wstring(),
        (base.parent_path() / L"ffmpeg" / L"bin" / L"ffmpeg.exe").wstring(),
        L"C:\\ffmpeg\\bin\\ffmpeg.exe",
        L"C:\\ffmpeg-8.1.1-essentials_build\\bin\\ffmpeg.exe"
    };
    for (const auto& x : c) if (fs::exists(x)) return x;

    // PATH
    DWORD n = SearchPathW(nullptr, L"ffmpeg.exe", nullptr, MAX_PATH, exe, nullptr);
    if (n > 0 && n < MAX_PATH) return std::wstring(exe, n);
    return {};
}

std::wstring FFmpegRunner::FindFfplay()
{
    wchar_t exe[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    fs::path base(exe);
    std::vector<std::wstring> c = {
        (base.parent_path() / L"ffplay.exe").wstring(),
        L"C:\\ffmpeg\\bin\\ffplay.exe",
        L"C:\\ffmpeg-8.1.1-essentials_build\\bin\\ffplay.exe"
    };
    for (const auto& x : c) if (fs::exists(x)) return x;
    DWORD n = SearchPathW(nullptr, L"ffplay.exe", nullptr, MAX_PATH, exe, nullptr);
    if (n > 0 && n < MAX_PATH) return std::wstring(exe, n);
    return {};
}

bool FFmpegRunner::Run(const std::wstring& exe, const std::wstring& args, std::wstring& output, DWORD* exitCode)
{
    std::wstring cmd = Quote(exe) + L" " + args;
    std::vector<wchar_t> buf(cmd.begin(), cmd.end());
    buf.push_back(L'\0');

    SECURITY_ATTRIBUTES sa{};
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;

    HANDLE outR = nullptr, outW = nullptr;
    if (!CreatePipe(&outR, &outW, &sa, 0)) return false;
    SetHandleInformation(outR, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOW si{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = outW;
    si.hStdError = outW;
    si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);

    PROCESS_INFORMATION pi{};
    if (!CreateProcessW(nullptr, buf.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW,
        nullptr, nullptr, &si, &pi))
    {
        CloseHandle(outR); CloseHandle(outW);
        return false;
    }

    CloseHandle(outW);

    std::string bytes;
    char tmp[4096];
    DWORD got = 0;
    while (ReadFile(outR, tmp, sizeof(tmp), &got, nullptr) && got)
        bytes.append(tmp, tmp + got);

    WaitForSingleObject(pi.hProcess, INFINITE);
    DWORD code = 0;
    GetExitCodeProcess(pi.hProcess, &code);

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    CloseHandle(outR);

    if (exitCode) *exitCode = code;

    if (!bytes.empty())
    {
        int n = MultiByteToWideChar(CP_UTF8, 0, bytes.data(), static_cast<int>(bytes.size()), nullptr, 0);
        if (n > 0)
        {
            output.resize(n);
            MultiByteToWideChar(CP_UTF8, 0, bytes.data(), static_cast<int>(bytes.size()), output.data(), n);
        }
    }

    return code == 0;
}
