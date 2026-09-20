# ReelForge — Full C++ Windows Desktop

This version is intentionally **100% native C++**.

## Removed

- HTML editor
- JavaScript editor
- WebView2
- localhost server
- - HTTP upload endpoints
- browser-style media handling
- C# / WPF host

## Native stack

- Win32 windowing and controls
- Common Controls
- Windows file picker (`GetOpenFileNameW`)
- Win32 drag & drop (`WM_DROPFILES`)
- native C++ project/timeline/media modules
- FFmpeg as a local executable for rendering
- Windows `ShellExecute` / ffplay for local playback

## Local drives

Media remains at the original path:

```text
C:\Videos\clip.mp4
E:\Projects\photos\photo1.jpg
F:\Music\song.mp3
```

Nothing is uploaded to a server and nothing is converted into a URL.

ReelForge only writes app data under:

```text
data\
  projects\
  cache\
  exports\
```

## Source-derived module names

The native inspector keeps the same Motion, Transition, Look, Caption, Fit and
Audio FX catalogs as the supplied editor source. The source defines the
15 motions, 10 transitions, 8 looks, 4 caption styles, 3 fit modes and 5 audio
effects used by the editor.

## Build

Visual Studio 2022 + Desktop development with C++ + Windows 10/11 SDK.

```powershell
msbuild ReelForgeNative.sln /m /p:Configuration=Release /p:Platform=x64
```

Put `ffmpeg.exe` beside the EXE or in PATH.
