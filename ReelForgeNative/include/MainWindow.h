#pragma once
#include <windows.h>
#include <commctrl.h>
#include <string>
#include <vector>
#include "Catalogs.h"
#include "Models.h"
#include "MediaLibrary.h"
#include "Timeline.h"
#include "ProjectStore.h"
#include "Renderer.h"
#include "Preview.h"

class MainWindow
{
public:
    MainWindow();
    bool Create(HINSTANCE hInstance);
    int RunMessageLoop();

private:
    static LRESULT CALLBACK WndProcStatic(HWND, UINT, WPARAM, LPARAM);
    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM);

    void BuildUi();
    void Layout();
    void RefreshLibrary();
    void RefreshTimeline();
    void RefreshInspector();
    void SetStatus(const std::wstring& text);
    void AddFiles(const std::wstring& mode);
    std::vector<std::wstring> ChooseFiles(const std::wstring& filter);
    void AddToTimelineSelected();
    void Export(bool preview);
    void Save();
    void LoadProjectDialog();
    void OpenExports();
    void DrawStage(HDC hdc, const RECT& rc);
    void DrawTimeline(HDC hdc, const RECT& rc);
    void SelectClip(int index);
    void SelectMedia(int index);

    HWND m_hwnd = nullptr;

    HWND m_ratio = nullptr;
    HWND m_quality = nullptr;
    HWND m_fps = nullptr;
    HWND m_btnProjects = nullptr;
    HWND m_btnFolder = nullptr;
    HWND m_btnPreview = nullptr;
    HWND m_btnExport = nullptr;

    HWND m_libList = nullptr;
    HWND m_timelineList = nullptr;
    HWND m_tabs = nullptr;
    HWND m_status = nullptr;
    HWND m_progress = nullptr;
    HWND m_inspector = nullptr;
    HWND m_stage = nullptr;

    HWND m_addImage = nullptr;
    HWND m_addMusic = nullptr;
    HWND m_addSfx = nullptr;
    HWND m_addVoice = nullptr;
    HWND m_addTimeline = nullptr;
    HWND m_removeClip = nullptr;
    HWND m_applyAll = nullptr;

    HBITMAP m_stageBitmap = nullptr;
    std::wstring m_stagePath;

    MediaLibrary m_media;
    Timeline m_timeline;
    ProjectStore m_store;
    Renderer m_renderer;
    Preview m_preview;
    std::wstring m_appDir;
    int m_selectedMedia = -1;
    int m_selectedClip = -1;
    int m_activeTab = 0;
    ProjectModel m_project;
};
