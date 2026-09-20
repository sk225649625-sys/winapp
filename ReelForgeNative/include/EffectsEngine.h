#pragma once
#include <string>
#include "Models.h"

class EffectsEngine
{
public:
    static std::wstring VideoFilter(const ClipItem& clip, int width, int height, int fps);
    static std::wstring LookFilter(const std::wstring& look);
    static std::wstring GlobalFilter(const GlobalFx& fx, int width, int height, int fps);
    static std::wstring AudioFilter(const AudioFxState& fx);
};
