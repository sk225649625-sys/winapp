#include "Catalogs.h"

namespace
{
    const std::vector<CatalogItem> g_motions = {
        {L"kenIn", L"Ken Burns zoom in"},
        {L"kenOut", L"Ken Burns zoom out"},
        {L"panLR", L"Pan left→right"},
        {L"panRL", L"Pan right→left"},
        {L"tiltUp", L"Crane tilt up"},
        {L"tiltDown", L"Tilt down"},
        {L"handheld", L"Handheld camera"},
        {L"punch", L"Impact punch"},
        {L"drift", L"Dutch drift"},
        {L"float", L"Floating card"},
        {L"spiral", L"Spiral reveal"},
        {L"dolly", L"Slow push"},
        {L"tilt3d", L"3D tilt swing"},
        {L"slideIn", L"Slide in"},
        {L"focus", L"Focus pull"}
    };

    const std::vector<CatalogItem> g_transitions = {
        {L"none", L"Hard cut"},
        {L"fade", L"Cross fade"},
        {L"zoom", L"Zoom through"},
        {L"whip", L"Whip pan"},
        {L"push", L"Push up"},
        {L"flash", L"Flash"},
        {L"spin", L"Spin close"},
        {L"iris", L"Iris reveal"},
        {L"blur", L"Dissolve"},
        {L"leak", L"Light leak"}
    };

    const std::vector<CatalogItem> g_looks = {
        {L"none", L"Original"},
        {L"warm", L"Warm sunset"},
        {L"cool", L"Cool blue"},
        {L"cine", L"Teal & orange"},
        {L"bw", L"Black & white"},
        {L"vintage", L"Vintage film"},
        {L"vivid", L"Vivid pop"},
        {L"moody", L"Dark moody"}
    };

    const std::vector<CatalogItem> g_captions = {
        {L"fadeup", L"Fade up"},
        {L"type", L"Typewriter"},
        {L"pop", L"Pop"},
        {L"slide", L"Slide in"}
    };

    const std::vector<CatalogItem> g_fits = {
        {L"auto", L"Auto"},
        {L"cover", L"Frame bharo"},
        {L"contain", L"Blur background"}
    };

    const std::vector<CatalogItem> g_audiofx = {
        {L"bass", L"Bass boost"},
        {L"treble", L"Treble boost"},
        {L"muffle", L"Phone / muffle"},
        {L"echo", L"Echo / space"},
        {L"crush", L"Lo-fi crush"}
    };

    const std::vector<std::wstring> g_defaultMotions = {
        L"kenIn",L"panLR",L"handheld",L"punch",L"float",L"tiltUp",
        L"dolly",L"panRL",L"spiral",L"drift",L"kenOut",L"tiltDown"
    };

    const std::vector<std::wstring> g_defaultTransitions = {
        L"fade",L"zoom",L"whip",L"leak",L"push",L"blur",L"flash",L"iris"
    };
}

namespace Catalogs
{
    const std::vector<CatalogItem>& Motions() { return g_motions; }
    const std::vector<CatalogItem>& Transitions() { return g_transitions; }
    const std::vector<CatalogItem>& Looks() { return g_looks; }
    const std::vector<CatalogItem>& Captions() { return g_captions; }
    const std::vector<CatalogItem>& Fits() { return g_fits; }
    const std::vector<CatalogItem>& AudioFx() { return g_audiofx; }
    const std::vector<std::wstring>& DefaultMotions() { return g_defaultMotions; }
    const std::vector<std::wstring>& DefaultTransitions() { return g_defaultTransitions; }
}
