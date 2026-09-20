# ReelForge native architecture

The application is desktop-only.

- C# WPF owns the editor UI and Windows file dialogs.
- C++ Win32 DLL owns native rendering process orchestration and filesystem helpers.
- FFmpeg is an external local executable.
- Media stays at its original Windows path (for example `C:\...`, `E:\...`, `F:\...`).
- Project JSON stores those local paths.
- No HTTP server, localhost listener, browser URL, WebView, or upload endpoint is required.
