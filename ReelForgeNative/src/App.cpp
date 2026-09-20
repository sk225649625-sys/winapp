#include "App.h"

int App::Run(HINSTANCE instance, int show)
{
    INITCOMMONCONTROLSEX icc{ sizeof(icc), ICC_WIN95_CLASSES | ICC_LISTVIEW_CLASSES | ICC_TAB_CLASSES | ICC_PROGRESS_CLASS };
    InitCommonControlsEx(&icc);

    if (!m_main.Create(instance))
        return -1;

    ShowWindow(GetActiveWindow(), show);
    UpdateWindow(GetActiveWindow());
    return m_main.RunMessageLoop();
}
