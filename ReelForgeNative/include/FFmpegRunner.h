#pragma once
#include <string>
#include <vector>

class FFmpegRunner
{
public:
    static std::wstring FindFfmpeg();
    static std::wstring FindFfplay();
    static bool Run(const std::wstring& exe, const std::wstring& args, std::wstring& output, DWORD* exitCode = nullptr);
    static std::wstring Quote(const std::wstring& s);
};
