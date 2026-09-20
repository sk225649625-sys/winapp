# Windows build requirements

Visual Studio 2022:
- Desktop development with C++
- MSVC v143
- Windows 10/11 SDK
- .NET 8 SDK

Then:

```powershell
msbuild ReelForge.sln /m /p:Configuration=Release /p:Platform=x64
dotnet publish ReelForge/ReelForge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Place `ffmpeg.exe` next to ReelForge.exe or ensure `ffmpeg.exe` is in PATH.
