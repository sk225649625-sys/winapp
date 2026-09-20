#include "MainWindow.h"
#include <windowsx.h>
#include <shlobj.h>
#include <shellapi.h>
#include <filesystem>
#include <fstream>
#include <algorithm>

namespace fs = std::filesystem;

static const wchar_t* kClass = L"ReelForgeNativeWindow";

static HWND MakeButton(HWND parent, const wchar_t* text, int x, int y, int w, int h, int id)
{
    return CreateWindowExW(0, L"BUTTON", text, WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON,
        x, y, w, h, parent, reinterpret_cast<HMENU>(id), GetModuleHandleW(nullptr), nullptr);
}

static HWND MakeLabel(HWND parent, const wchar_t* text, int x, int y, int w, int h)
{
    return CreateWindowExW(0, L"STATIC", text, WS_CHILD | WS_VISIBLE,
        x, y, w, h, parent, nullptr, GetModuleHandleW(nullptr), nullptr);
}

MainWindow::MainWindow()
{
    wchar_t exe[MAX_PATH] = {};
    GetModuleFileNameW(nullptr, exe, MAX_PATH);
    m_appDir = fs::path(exe).parent_path().wstring();
    m_store = ProjectStore(m_appDir);
    m_renderer = Renderer(m_appDir);
    m_preview = Preview((fs::path(m_appDir) / L"data" / L"cache").wstring());
    m_project.name = L"last.json";
}

bool MainWindow::Create(HINSTANCE hInstance)
{
    WNDCLASSEXW wc{ sizeof(wc) };
    wc.lpfnWndProc = &MainWindow::WndProcStatic;
    wc.hInstance = hInstance;
    wc.lpszClassName = kClass;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    RegisterClassExW(&wc);

    m_hwnd = CreateWindowExW(
        WS_EX_APPWINDOW,
        kClass,
        L"ReelForge — Native C++",
        WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
        CW_USEDEFAULT, CW_USEDEFAULT, 1500, 920,
        nullptr, nullptr, hInstance, this);

    return m_hwnd != nullptr;
}

LRESULT CALLBACK MainWindow::WndProcStatic(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    MainWindow* self = reinterpret_cast<MainWindow*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (msg == WM_NCCREATE)
    {
        auto* cs = reinterpret_cast<CREATESTRUCTW*>(lp);
        self = reinterpret_cast<MainWindow*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    return self ? self->WndProc(hwnd, msg, wp, lp) : DefWindowProcW(hwnd, msg, wp, lp);
}

void MainWindow::BuildUi()
{
    m_ratio = CreateWindowExW(0, L"COMBOBOX", L"", WS_CHILD|WS_VISIBLE|CBS_DROPDOWNLIST,
        160, 34, 90, 180, m_hwnd, reinterpret_cast<HMENU>(101), GetModuleHandleW(nullptr), nullptr);
    for (auto x : {L"16:9",L"9:16",L"1:1",L"4:5",L"21:9"}) SendMessageW(m_ratio, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(x));
    SendMessageW(m_ratio, CB_SETCURSEL, 0, 0);

    m_quality = CreateWindowExW(0, L"COMBOBOX", L"", WS_CHILD|WS_VISIBLE|CBS_DROPDOWNLIST,
        270, 34, 112, 180, m_hwnd, reinterpret_cast<HMENU>(102), GetModuleHandleW(nullptr), nullptr);
    for (auto x : {L"480p draft",L"720p",L"1080p",L"1440p"}) SendMessageW(m_quality, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(x));
    SendMessageW(m_quality, CB_SETCURSEL, 2, 0);

    m_fps = CreateWindowExW(0, L"COMBOBOX", L"", WS_CHILD|WS_VISIBLE|CBS_DROPDOWNLIST,
        400, 34, 80, 180, m_hwnd, reinterpret_cast<HMENU>(103), GetModuleHandleW(nullptr), nullptr);
    for (auto x : {L"24",L"30",L"60"}) SendMessageW(m_fps, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(x));
    SendMessageW(m_fps, CB_SETCURSEL, 1, 0);

    MakeLabel(m_hwnd, L"Ratio", 160, 15, 80, 18);
    MakeLabel(m_hwnd, L"Quality", 270, 15, 80, 18);
    MakeLabel(m_hwnd, L"FPS", 400, 15, 60, 18);

    m_btnProjects = MakeButton(m_hwnd, L"Projects", 1030, 26, 90, 30, 110);
    m_btnFolder = MakeButton(m_hwnd, L"Exports folder", 1125, 26, 110, 30, 111);
    m_btnPreview = MakeButton(m_hwnd, L"Preview banao", 1240, 26, 120, 30, 112);
    m_btnExport = MakeButton(m_hwnd, L"Export karo", 1365, 26, 110, 30, 113);

    m_addImage = MakeButton(m_hwnd, L"+ Image / Video", 10, 95, 128, 30, 120);
    m_addMusic = MakeButton(m_hwnd, L"+ Music", 10, 130, 80, 30, 121);
    m_addSfx = MakeButton(m_hwnd, L"+ SFX", 95, 130, 70, 30, 122);
    m_addVoice = MakeButton(m_hwnd, L"+ Voice", 170, 130, 80, 30, 123);
    m_addTimeline = MakeButton(m_hwnd, L"Timeline me daalo", 10, 165, 130, 30, 124);
    m_removeClip = MakeButton(m_hwnd, L"Remove", 145, 165, 80, 30, 125);
    m_applyAll = MakeButton(m_hwnd, L"Sab clips par", 10, 200, 120, 30, 126);

    m_libList = CreateWindowExW(WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"",
        WS_CHILD|WS_VISIBLE|LVS_REPORT|LVS_SINGLESEL|LVS_SHOWSELALWAYS,
        10, 245, 240, 470, m_hwnd, reinterpret_cast<HMENU>(140), GetModuleHandleW(nullptr), nullptr);
    ListView_SetExtendedListViewStyle(m_libList, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);
    LVCOLUMNW col{ LVCF_TEXT|LVCF_WIDTH };
    col.cx = 220; col.pszText = const_cast<wchar_t*>(L"Local media");
    ListView_InsertColumn(m_libList, 0, &col);

    m_stage = CreateWindowExW(WS_EX_CLIENTEDGE, L"STATIC", L"Timeline khaali hai",
        WS_CHILD|WS_VISIBLE|SS_CENTER|SS_OWNERDRAW, 270, 95, 700, 520,
        m_hwnd, reinterpret_cast<HMENU>(150), GetModuleHandleW(nullptr), nullptr);

    m_timelineList = CreateWindowExW(WS_EX_CLIENTEDGE, WC_LISTVIEWW, L"",
        WS_CHILD|WS_VISIBLE|LVS_REPORT|LVS_SINGLESEL|LVS_SHOWSELALWAYS,
        270, 625, 700, 145, m_hwnd, reinterpret_cast<HMENU>(160), GetModuleHandleW(nullptr), nullptr);
    ListView_SetExtendedListViewStyle(m_timelineList, LVS_EX_FULLROWSELECT | LVS_EX_DOUBLEBUFFER);
    LVCOLUMNW tc{ LVCF_TEXT|LVCF_WIDTH };
    tc.cx = 400; tc.pszText = const_cast<wchar_t*>(L"Timeline");
    ListView_InsertColumn(m_timelineList, 0, &tc);
    tc.cx = 90; tc.pszText = const_cast<wchar_t*>(L"Duration");
    ListView_InsertColumn(m_timelineList, 1, &tc);
    tc.cx = 120; tc.pszText = const_cast<wchar_t*>(L"Motion");
    ListView_InsertColumn(m_timelineList, 2, &tc);

    m_tabs = CreateWindowExW(0, WC_TABCONTROLW, L"",
        WS_CHILD|WS_VISIBLE|TCS_TABS, 990, 95, 480, 32,
        m_hwnd, reinterpret_cast<HMENU>(170), GetModuleHandleW(nullptr), nullptr);
    for (auto x : {L"Clip",L"Video FX",L"Audio",L"Text"})
    {
        TCITEMW ti{ TCIF_TEXT };
        ti.pszText = const_cast<wchar_t*>(x);
        TabCtrl_InsertItem(m_tabs, TabCtrl_GetItemCount(m_tabs), &ti);
    }

    m_inspector = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
        WS_CHILD|WS_VISIBLE|ES_MULTILINE|ES_AUTOVSCROLL|ES_READONLY|WS_VSCROLL,
        990, 132, 480, 638, m_hwnd, reinterpret_cast<HMENU>(171), GetModuleHandleW(nullptr), nullptr);

    m_progress = CreateWindowExW(0, PROGRESS_CLASSW, L"", WS_CHILD|WS_VISIBLE,
        350, 785, 620, 18, m_hwnd, reinterpret_cast<HMENU>(180), GetModuleHandleW(nullptr), nullptr);

    m_status = MakeLabel(m_hwnd, L"Ready • fully local C++ desktop", 10, 785, 330, 22);

    DragAcceptFiles(m_hwnd, TRUE);
    RefreshLibrary();
    RefreshTimeline();
    RefreshInspector();
}

void MainWindow::Layout()
{
    RECT rc{};
    GetClientRect(m_hwnd, &rc);
    const int W = rc.right, H = rc.bottom;
    const int leftW = 255, rightW = 480, top = 78, bottom = 120;

    SetWindowPos(m_libList, nullptr, 10, 245, leftW-20, H-bottom-245, SWP_NOZORDER);
    SetWindowPos(m_stage, nullptr, leftW+15, top, W-leftW-rightW-30, H-bottom-top-35, SWP_NOZORDER);
    SetWindowPos(m_timelineList, nullptr, leftW+15, H-bottom+5, W-leftW-rightW-30, bottom-45, SWP_NOZORDER);
    SetWindowPos(m_tabs, nullptr, W-rightW+10, top, rightW-20, 32, SWP_NOZORDER);
    SetWindowPos(m_inspector, nullptr, W-rightW+10, top+37, rightW-20, H-bottom-top-42, SWP_NOZORDER);
    SetWindowPos(m_progress, nullptr, 350, H-27, std::max(200, W-720), 16, SWP_NOZORDER);
    SetWindowPos(m_status, nullptr, 10, H-29, 320, 20, SWP_NOZORDER);
    InvalidateRect(m_stage, nullptr, FALSE);
}

void MainWindow::RefreshLibrary()
{
    ListView_DeleteAllItems(m_libList);
    for (size_t i = 0; i < m_media.Items().size(); ++i)
    {
        const auto& m = m_media.Items()[i];
        LVITEMW it{ LVIF_TEXT };
        it.iItem = static_cast<int>(i);
        it.pszText = const_cast<wchar_t*>(m.name.c_str());
        ListView_InsertItem(m_libList, &it);
    }
}

void MainWindow::RefreshTimeline()
{
    ListView_DeleteAllItems(m_timelineList);
    for (size_t i = 0; i < m_timeline.Clips().size(); ++i)
    {
        const auto& c = m_timeline.Clips()[i];
        LVITEMW it{ LVIF_TEXT };
        it.iItem = static_cast<int>(i);
        it.pszText = const_cast<wchar_t*>(fs::path(c.file).filename().wstring().c_str());
        ListView_InsertItem(m_timelineList, &it);
        ListView_SetItemText(m_timelineList, static_cast<int>(i), 1, const_cast<wchar_t*>(std::to_wstring(c.dur).c_str()));
        ListView_SetItemText(m_timelineList, static_cast<int>(i), 2, const_cast<wchar_t*>(c.motion.c_str()));
    }
    InvalidateRect(m_stage, nullptr, FALSE);
}

void MainWindow::RefreshInspector()
{
    std::wstringstream s;
    if (m_activeTab == 0)
    {
        s << L"CLIP\n\n";
        if (m_selectedClip < 0 || m_selectedClip >= static_cast<int>(m_timeline.Clips().size()))
            s << L"Timeline me kisi clip ko select karo.\n";
        else
        {
            auto& c = m_timeline.Clips()[m_selectedClip];
            s << L"File: " << c.file << L"\n";
            s << L"Duration: " << c.dur << L"s\n";
            s << L"Motion: " << c.motion << L"\n";
            s << L"Transition: " << c.transition << L"\n";
            s << L"Look: " << c.look << L"\n";
            s << L"Fit: " << c.fit << L"\n";
            s << L"Caption: " << c.text << L"\n\n";
            s << L"Available motions:\n";
            for (auto& x : Catalogs::Motions()) s << L"• " << x.label << L" [" << x.key << L"]\n";
            s << L"\nAvailable transitions:\n";
            for (auto& x : Catalogs::Transitions()) s << L"• " << x.label << L" [" << x.key << L"]\n";
        }
    }
    else if (m_activeTab == 1)
    {
        s << L"VIDEO FX\n\n";
        s << L"Grain: " << (m_project.fxg.grain ? L"ON" : L"OFF") << L"\n";
        s << L"Vignette: " << (m_project.fxg.vignette ? L"ON" : L"OFF") << L"\n";
        s << L"Letterbox: " << (m_project.fxg.letterbox ? L"ON" : L"OFF") << L"\n";
        s << L"Fade: " << (m_project.fxg.fade ? L"ON" : L"OFF") << L"\n\n";
        for (auto& x : Catalogs::Looks()) s << L"• " << x.label << L" [" << x.key << L"]\n";
    }
    else if (m_activeTab == 2)
    {
        s << L"AUDIO\n\n";
        s << L"Music/SFX tracks: " << m_timeline.Audio().size() << L"\n";
        s << L"Voice sequence: " << m_timeline.Voice().size() << L"\n\n";
        s << L"Audio FX:\n";
        for (auto& x : Catalogs::AudioFx()) s << L"• " << x.label << L" [" << x.key << L"]\n";
    }
    else
    {
        s << L"TEXT\n\n";
        s << L"Overlays: " << m_project.overlays.size() << L"\n\n";
        for (auto& x : Catalogs::Captions()) s << L"• " << x.label << L" [" << x.key << L"]\n";
    }
    SetWindowTextW(m_inspector, s.str().c_str());
}

void MainWindow::SetStatus(const std::wstring& text)
{
    SetWindowTextW(m_status, text.c_str());
}

std::vector<std::wstring> MainWindow::ChooseFiles(const std::wstring& filter)
{
    wchar_t buffer[65536] = {};
    OPENFILENAMEW ofn{ sizeof(ofn) };
    ofn.hwndOwner = m_hwnd;
    ofn.lpstrFilter = filter.c_str();
    ofn.lpstrFile = buffer;
    ofn.nMaxFile = static_cast<DWORD>(std::size(buffer));
    ofn.Flags = OFN_EXPLORER | OFN_ALLOWMULTISELECT | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY;

    std::vector<std::wstring> out;
    if (!GetOpenFileNameW(&ofn)) return out;

    const wchar_t* p = buffer;
    std::wstring first(p);
    p += first.size() + 1;

    if (*p == 0)
    {
        out.push_back(first);
        return out;
    }

    std::wstring dir = first;
    while (*p)
    {
        std::wstring name(p);
        out.push_back((fs::path(dir) / name).wstring());
        p += name.size() + 1;
    }
    return out;
}

void MainWindow::AddFiles(const std::wstring& mode)
{
    std::wstring filter;
    if (mode == L"image")
    {
        filter = L"Photos and video";
        filter.push_back(L'\\0');
        filter += L"*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.wmv";
        filter.push_back(L'\\0');
        filter += L"All files";
        filter.push_back(L'\\0');
        filter += L"*.*";
        filter.push_back(L'\\0');
        filter.push_back(L'\\0');
    }
    else
    {
        filter = L"Audio";
        filter.push_back(L'\\0');
        filter += L"*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac;*.wma";
        filter.push_back(L'\\0');
        filter += L"All files";
        filter.push_back(L'\\0');
        filter += L"*.*";
        filter.push_back(L'\\0');
        filter.push_back(L'\\0');
    }

    auto paths = ChooseFiles(filter);
    if (paths.empty()) return;
    m_media.AddPaths(paths, mode == L"image" ? L"" : mode);
    RefreshLibrary();
    SetStatus(std::to_wstring(paths.size()) + L" local file(s) added from your drive");
}

void MainWindow::AddToTimelineSelected()
{
    auto sel = ListView_GetNextItem(m_libList, -1, LVNI_SELECTED);
    if (sel < 0 || sel >= static_cast<int>(m_media.Items().size()))
        return;

    const auto& m = m_media.Items()[sel];
    if (m.type == L"audio")
    {
        m_timeline.AddAudio(m, m_timeline.VideoLength());
        SetStatus(L"Audio timeline par add ho gaya.");
    }
    else
    {
        m_timeline.AddSelectedImages(m_media, {m.name});
        SetStatus(L"Clip timeline me add ho gaya.");
    }

    m_project = m_timeline.Project();
    RefreshTimeline();
    RefreshInspector();
    m_store.Save(m_project);
}

void MainWindow::SelectClip(int index)
{
    m_selectedClip = index;
    RefreshInspector();
    InvalidateRect(m_stage, nullptr, FALSE);

    if (index >= 0 && index < static_cast<int>(m_timeline.Clips().size()))
    {
        const auto& c = m_timeline.Clips()[index];
        if (c.file.size())
            m_preview.LoadImage(m_stage, c.file);
        SetWindowTextW(m_stage, fs::path(c.file).filename().c_str());
    }
}

void MainWindow::SelectMedia(int index)
{
    m_selectedMedia = index;
    if (index < 0 || index >= static_cast<int>(m_media.Items().size())) return;
    const auto& m = m_media.Items()[index];

    if (m.type == L"image")
    {
        m_stagePath = m.fullPath;
        m_preview.LoadImage(m_stage, m.fullPath);
        SetWindowTextW(m_stage, m.name.c_str());
        InvalidateRect(m_stage, nullptr, TRUE);
    }
    else if (m.type == L"video")
    {
        m_preview.OpenVideo(m.fullPath);
        SetStatus(L"Video external native player me khul gaya.");
    }
}

void MainWindow::Save()
{
    m_project = m_timeline.Project();
    auto ratioIdx = static_cast<int>(SendMessageW(m_ratio, CB_GETCURSEL, 0, 0));
    static const wchar_t* ratios[] = {L"16:9",L"9:16",L"1:1",L"4:5",L"21:9"};
    if (ratioIdx >= 0 && ratioIdx < 5) m_project.ratio = ratios[ratioIdx];

    const int q = static_cast<int>(SendMessageW(m_quality, CB_GETCURSEL, 0, 0));
    m_project.quality = q == 0 ? 480 : q == 1 ? 720 : q == 2 ? 1080 : 1440;
    const int f = static_cast<int>(SendMessageW(m_fps, CB_GETCURSEL, 0, 0));
    m_project.fps = f == 0 ? 24 : f == 1 ? 30 : 60;
    m_project.name = L"last.json";
    m_store.Save(m_project);
    SetStatus(L"Project saved locally.");
}

void MainWindow::LoadProjectDialog()
{
    std::wstring filter = L"ReelForge project";
    filter.push_back(L'\\0');
    filter += L"*.json";
    filter.push_back(L'\\0');
    filter += L"All files";
    filter.push_back(L'\\0');
    filter += L"*.*";
    filter.push_back(L'\\0');
    filter.push_back(L'\\0');
    auto files = ChooseFiles(filter);
    if (files.empty()) return;
    const auto name = fs::path(files.front()).filename().wstring();

    if (m_store.Load(name, m_project))
    {
        m_timeline.Project() = m_project;
        RefreshTimeline();
        RefreshInspector();
        SetStatus(L"Project loaded locally.");
    }
}

void MainWindow::OpenExports()
{
    const auto dir = m_store.ExportsDir();
    ShellExecuteW(nullptr, L"open", dir.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}

void MainWindow::Export(bool preview)
{
    Save();

    std::wstring out = m_store.ExportsDir() + L"\\" +
        (preview ? L"preview-" : L"export-") + std::to_wstring(GetTickCount64()) + L".mp4";

    SetStatus(preview ? L"Preview render ho raha hai…" : L"Export render ho raha hai…");
    SendMessageW(m_progress, PBM_SETPOS, 5, 0);

    std::wstring error;
    bool ok = m_renderer.Render(m_project, out, error, [&](double p)
    {
        SendMessageW(m_progress, PBM_SETPOS, static_cast<WPARAM>(p * 100), 0);
    });

    if (!ok)
    {
        SetStatus(L"Render failed: " + error);
        MessageBoxW(m_hwnd, error.c_str(), L"ReelForge", MB_ICONERROR);
        return;
    }

    SendMessageW(m_progress, PBM_SETPOS, 100, 0);
    SetStatus(L"Render complete: " + out);

    if (preview)
        m_preview.OpenVideo(out);
    else
        ShellExecuteW(nullptr, L"open", out.c_str(), nullptr, nullptr, SW_SHOWNORMAL);
}

void MainWindow::DrawStage(HDC hdc, const RECT& rc)
{
    HBRUSH bg = CreateSolidBrush(RGB(5,7,11));
    FillRect(hdc, &rc, bg);
    DeleteObject(bg);

    RECT text = rc;
    SetTextColor(hdc, RGB(145,155,175));
    SetBkMode(hdc, TRANSPARENT);

    if (m_stagePath.empty() || !m_preview.Bitmap())
    {
        DrawTextW(hdc, L"Timeline khaali hai\n\nLibrary se local files select karke\n\"Timeline me daalo\" dabao.",
            -1, &text, DT_CENTER | DT_VCENTER | DT_WORDBREAK);
        return;
    }

    HDC mem = CreateCompatibleDC(hdc);
    HGDIOBJ old = SelectObject(mem, m_preview.Bitmap());

    SIZE sz = m_preview.BitmapSize();
    const int boxW = rc.right - rc.left;
    const int boxH = rc.bottom - rc.top;
    const double scale = (std::min)(
        boxW / static_cast<double>((std::max)(1L, sz.cx)),
        boxH / static_cast<double>((std::max)(1L, sz.cy)));
    const int dw = (std::max)(1, static_cast<int>(sz.cx * scale));
    const int dh = (std::max)(1, static_cast<int>(sz.cy * scale));
    const int dx = (boxW - dw) / 2;
    const int dy = (boxH - dh) / 2;

    SetStretchBltMode(hdc, HALFTONE);
    StretchBlt(hdc, dx, dy, dw, dh, mem, 0, 0, sz.cx, sz.cy, SRCCOPY);

    SelectObject(mem, old);
    DeleteDC(mem);
}

void MainWindow::DrawTimeline(HDC hdc, const RECT& rc)
{
    DrawTextW(hdc, L"Timeline", -1, const_cast<RECT*>(&rc), DT_LEFT | DT_TOP);
}

LRESULT MainWindow::WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_CREATE:
        BuildUi();
        return 0;

    case WM_SIZE:
        Layout();
        return 0;

    case WM_COMMAND:
        switch (LOWORD(wp))
        {
        case 120: AddFiles(L"image"); break;
        case 121: AddFiles(L"music"); break;
        case 122: AddFiles(L"sfx"); break;
        case 123: AddFiles(L"voice"); break;
        case 124: AddToTimelineSelected(); break;
        case 125:
            if (m_selectedClip >= 0)
            {
                m_timeline.RemoveClip(static_cast<size_t>(m_selectedClip));
                m_selectedClip = -1;
                m_project = m_timeline.Project();
                RefreshTimeline(); RefreshInspector(); Save();
            }
            break;
        case 126:
            if (m_selectedClip >= 0)
            {
                m_timeline.ApplyClipToAll(static_cast<size_t>(m_selectedClip));
                RefreshTimeline(); RefreshInspector(); Save();
            }
            break;
        case 110: LoadProjectDialog(); break;
        case 111: OpenExports(); break;
        case 112: Export(true); break;
        case 113: Export(false); break;
        }
        return 0;

    case WM_NOTIFY:
    {
        auto* nm = reinterpret_cast<NMHDR*>(lp);
        if (nm->idFrom == 140 && nm->code == LVN_ITEMCHANGED)
        {
            auto* x = reinterpret_cast<NMLISTVIEW*>(lp);
            if ((x->uNewState & LVIS_SELECTED) && !(x->uOldState & LVIS_SELECTED))
                SelectMedia(x->iItem);
        }
        else if (nm->idFrom == 160 && nm->code == LVN_ITEMCHANGED)
        {
            auto* x = reinterpret_cast<NMLISTVIEW*>(lp);
            if ((x->uNewState & LVIS_SELECTED) && !(x->uOldState & LVIS_SELECTED))
                SelectClip(x->iItem);
        }
        else if (nm->idFrom == 170 && nm->code == TCN_SELCHANGE)
        {
            m_activeTab = TabCtrl_GetCurSel(m_tabs);
            RefreshInspector();
        }
        return 0;
    }

    case WM_DROPFILES:
    {
        HDROP hDrop = reinterpret_cast<HDROP>(wp);
        UINT n = DragQueryFileW(hDrop, 0xFFFFFFFF, nullptr, 0);
        std::vector<std::wstring> paths;
        wchar_t buffer[32768];
        for (UINT i = 0; i < n; ++i)
        {
            DragQueryFileW(hDrop, i, buffer, 32768);
            paths.emplace_back(buffer);
        }
        DragFinish(hDrop);

        m_media.AddPaths(paths);
        RefreshLibrary();
        SetStatus(std::to_wstring(paths.size()) + L" dropped local file(s) added");
        return 0;
    }

    case WM_DRAWITEM:
    {
        auto* d = reinterpret_cast<DRAWITEMSTRUCT*>(lp);
        if (d->hwndItem == m_stage)
        {
            DrawStage(d->hDC, d->rcItem);
            return TRUE;
        }
        break;
    }

    case WM_DESTROY:
        Save();
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hwnd, msg, wp, lp);
}

int MainWindow::RunMessageLoop()
{
    ShowWindow(m_hwnd, SW_SHOW);
    UpdateWindow(m_hwnd);

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return static_cast<int>(msg.wParam);
}
