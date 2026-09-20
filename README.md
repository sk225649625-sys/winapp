# ReelForge — Fully Native Windows Editor

This branch is a **real Windows desktop app**. It does not use:

- WebView2
- HTML/JavaScript editor
- localhost server
- `/api/*` upload endpoints
- browser-style media uploads

## Architecture

- **C# / WPF**: native editor UI, project system, file dialogs, preview.
- **C++ / Win32 DLL**: native render launcher + fast local filesystem helpers.
- **FFmpeg executable**: external local render engine.
- **Windows drives**: media can stay on `C:\`, `D:\`, `E:\`, `F:\`, removable drives, etc.
- Project JSON stores the actual local file paths. Files are not uploaded to a server.

## Local data

Only project/cache/export app data is beside the EXE:

```text
ReelForge.exe
ReelForge.Native.dll
data/
  projects/
  cache/
  exports/
  logs/
```

Media files do **not** have to be copied into the app folder.

## Add media

Use the native **+ Image / Video**, **+ Music**, **+ SFX**, or **+ Voice** buttons.
The normal Windows file picker opens. Select a file from any local drive.

Double-click a library item or press `+ timeline` to put it on the timeline.

## Build

Open `ReelForge.sln` in Visual Studio 2022 with Desktop C++ + .NET 8 installed.

Or push to GitHub and let `.github/workflows/build.yml` produce the x64 ZIP.
