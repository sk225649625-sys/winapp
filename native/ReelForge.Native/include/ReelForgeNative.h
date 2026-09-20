#pragma once
#include <windows.h>

extern "C" __declspec(dllexport)
int __cdecl RF_CopyFileFast(
    const wchar_t* sourcePath,
    const wchar_t* destinationPath,
    wchar_t* errorBuffer,
    int errorBufferChars);

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
    int errorBufferChars);
