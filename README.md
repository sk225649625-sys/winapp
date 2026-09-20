# ReelForge — Native Windows App

ReelForge is a Windows desktop video editor shell built with C#/.NET 8, WPF and WebView2. The supplied `editor.html` is kept unchanged.

## What is fixed in this package

- Explicit `System.IO` imports where filesystem APIs are used, removing the GitHub Actions `Path` / `File` / `Directory` compiler failures.
- A valid x64 solution configuration for the `win-x64` publish.
- WebView2 local-host mapping for `https://reelforge.local/editor.html`.
- `/api/hello` now returns the media/project data expected by the existing editor.
- `/api/delete` is implemented for media deletion.
- Upload handling matches the existing editor: raw file body + `X-Name` and optional `X-Kind` headers.
- Project listing returns objects with `name`, `mtime`, `clips`, `voice`, and `dur`, matching the editor UI.
- Media kind metadata is preserved for music / SFX / voice uploads.
- Working folders are created beside the EXE under `data\`.
- GitHub Actions builds, publishes and packages a self-contained Windows x64 ZIP.

## Folder layout at runtime

```text
ReelForge.exe
wwwroot\
  editor.html
  ...
data\
  media\
  projects\
  exports\
  cache\
  webview2\
```

## FFmpeg

Place `ffmpeg.exe` beside `ReelForge.exe`, or put FFmpeg in PATH.
`ffprobe.exe` is optional but helps the media library show audio duration.

The app also checks the common path:

`C:\ffmpeg-8.1.1-essentials_build\bin\ffmpeg.exe`

## Local build

Install Visual Studio 2022 with the .NET desktop workload and the Microsoft WebView2 Runtime.

```powershell
dotnet restore ReelForge.sln -p:Platform=x64
dotnet build ReelForge.sln -c Release -p:Platform=x64
dotnet publish ReelForge\ReelForge.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true
```

## GitHub Actions

`.github/workflows/build-exe.yml` produces:

`ReelForge-windows-x64.zip`

The artifact contains the self-contained EXE, `wwwroot`, runtime data folders, and README. Tag pushes such as `v1.0.0` also create a GitHub release asset.

## Scope

This is a native C# baseline renderer that keeps the existing editor page and local API contract intact. The renderer currently converts timeline images into an MP4. Exact pixel-level parity for every legacy Python effect requires the original Python rendering engine source; that source was not part of the supplied files, so this package does not claim effect-for-effect parity that cannot be verified.
