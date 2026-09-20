#pragma once
#include <windows.h>
#include "MainWindow.h"

class App
{
public:
    int Run(HINSTANCE instance, int show);

private:
    MainWindow m_main;
};
