# Windows build quick check

After extracting the repository on a Windows PC with .NET 8 SDK:

```powershell
dotnet restore ReelForge.sln -p:Platform=x64
dotnet build ReelForge.sln -c Release -p:Platform=x64
dotnet publish ReelForge\ReelForge.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o publish
```

For GitHub Actions, push to `main`/`master` or run the workflow manually from Actions.
