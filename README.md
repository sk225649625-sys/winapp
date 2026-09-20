# ReelForge — Native Windows App

ReelForge is a Windows desktop video editor built with C#/.NET 8, WPF and WebView2.

## No Python

There is no Python runtime, Python server, Flask server, or Python engine in this project.

## Architecture

- WPF native Windows shell
- WebView2 hosts the existing `editor.html`
- C# intercepts the editor's `/api/*`, `/media/*`, `/thumb/*` and `/exports/*` requests
- Projects and media stay on the local Windows PC
- FFmpeg is launched directly as a child process by C#
- No cloud backend is required

## Local data

Data is stored in:

`%LOCALAPPDATA%\ReelForge\`

with:

- `media`
- `projects`
- `exports`
- `cache`

## FFmpeg

Put `ffmpeg.exe` and optionally `ffprobe.exe` beside `ReelForge.exe`, or install FFmpeg in PATH.

The project also checks:

`C:\ffmpeg-8.1.1-essentials_build\bin\ffmpeg.exe`

## Build locally

Install Visual Studio 2022 with the .NET desktop workload and WebView2 Runtime.

```powershell
dotnet restore ReelForge.sln
dotnet build ReelForge.sln -c Release
dotnet publish ReelForge\ReelForge.csproj -c Release -r win-x64 --self-contained true
```

## GitHub Actions

Push the repository to GitHub. The workflow in `.github/workflows/build-exe.yml` builds a Windows x64 ZIP artifact.

## Important scope note

The included native renderer is a clean C# baseline that preserves the editor's project/media API shape. Exact pixel-for-pixel parity for every legacy Python render effect requires the original `reelforge_engine.py` source; that source was not present in the files available for this build, so this package does not falsely claim a complete effect-for-effect port.
