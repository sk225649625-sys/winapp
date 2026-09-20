#include <windows.h>
#include <objbase.h>
#include "App.h"

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, PWSTR, int nCmdShow)
{
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    App app;
    const int result = app.Run(hInstance, nCmdShow);
    CoUninitialize();
    return result;
}
