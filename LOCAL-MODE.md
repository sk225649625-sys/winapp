# ReelForge Local Mode

ReelForge now uses a native Windows file picker for media import.

- No localhost/TCP server is started.
- No `/api/upload` HTTP upload is used.
- Photos/audio are copied directly by C# into `data/media` beside the EXE.
- Project JSON stays in `data/projects`.
- Exports stay in `data/exports`.
- WebView2 is only the local UI shell/resource viewer.
- JavaScript app commands use `window.chrome.webview.postMessage` -> native WPF C#.
- Large files do not get converted to base64 or passed through JavaScript memory.

Use the **+ Add** buttons in the left library for images, music, SFX, and voice.
