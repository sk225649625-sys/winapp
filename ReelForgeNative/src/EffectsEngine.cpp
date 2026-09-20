#include "EffectsEngine.h"

static std::wstring q(const std::wstring& x) { return x; }

std::wstring EffectsEngine::LookFilter(const std::wstring& look)
{
    if (look == L"warm") return L"eq=contrast=1.03:saturation=1.12:brightness=0.03,colorbalance=rs=.08:gs=.02:bs=-.03";
    if (look == L"cool") return L"eq=contrast=1.02:saturation=1.02:brightness=0,colorbalance=rs=-.04:gs=.01:bs=.10";
    if (look == L"cine") return L"eq=contrast=1.08:saturation=1.10:brightness=-.01,colorbalance=rs=-.03:bs=.05";
    if (look == L"bw") return L"format=gray,eq=contrast=1.05";
    if (look == L"vintage") return L"curves=vintage";
    if (look == L"vivid") return L"eq=contrast=1.08:saturation=1.35";
    if (look == L"moody") return L"eq=contrast=1.10:saturation=.88:brightness=-.04";
    return L"null";
}

std::wstring EffectsEngine::VideoFilter(const ClipItem& c, int width, int height, int fps)
{
    std::wstring f;
    f += L"scale=" + std::to_wstring(width) + L":" + std::to_wstring(height) + L":force_original_aspect_ratio=decrease,";
    f += L"pad=" + std::to_wstring(width) + L":" + std::to_wstring(height) + L":(ow-iw)/2:(oh-ih)/2,";
    f += L"setsar=1,fps=" + std::to_wstring(fps) + L",";
    f += LookFilter(c.look);

    if (c.motion == L"kenIn") f += L",scale=" + std::to_wstring(width) + L"*1.08:" + std::to_wstring(height) + L"*1.08,crop=" + std::to_wstring(width) + L":" + std::to_wstring(height);
    else if (c.motion == L"kenOut") f += L",scale=" + std::to_wstring(width) + L"*1.10:" + std::to_wstring(height) + L"*1.10,crop=" + std::to_wstring(width) + L":" + std::to_wstring(height);
    else if (c.motion == L"punch") f += L",eq=contrast=1.08:saturation=1.05";
    else if (c.motion == L"handheld") f += L",eq=contrast=1.03";
    else if (c.motion == L"float") f += L",scale=" + std::to_wstring(width) + L"*1.04:" + std::to_wstring(height) + L"*1.04,crop=" + std::to_wstring(width) + L":" + std::to_wstring(height);
    return f;
}

std::wstring EffectsEngine::GlobalFilter(const GlobalFx& fx, int width, int height, int)
{
    std::wstring f = L"null";
    if (fx.vignette) f += L",vignette=PI/4";
    if (fx.letterbox) f += L",drawbox=x=0:y=0:w=iw:h=ih*0.08:color=black@1:t=fill,drawbox=x=0:y=ih-ih*0.08:w=iw:h=ih*0.08:color=black@1:t=fill";
    return f;
}

std::wstring EffectsEngine::AudioFilter(const AudioFxState& fx)
{
    std::wstring chain;
    if (fx.bass.on) chain += L"bass=g=" + std::to_wstring(6.0 * fx.bass.amount) + L",";
    if (fx.treble.on) chain += L"treble=g=" + std::to_wstring(6.0 * fx.treble.amount) + L",";
    if (fx.muffle.on) chain += L"lowpass=f=" + std::to_wstring(2500 - 1700 * fx.muffle.amount) + L",";
    if (fx.echo.on) chain += L"aecho=0.8:0.88:60:0.25,";
    if (fx.crush.on) chain += L"acrusher=bits=" + std::to_wstring(16 - static_cast<int>(10 * fx.crush.amount)) + L":mode=log,";
    if (!chain.empty() && chain.back() == L',') chain.pop_back();
    return chain;
}
