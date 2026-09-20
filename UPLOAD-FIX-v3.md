# ReelForge upload fix v3

The native upload route now runs before `ReadBody(req)` in `ApiRouter`.
The previous code consumed `CoreWebView2WebResourceRequest.Content` before
`Upload(req)` attempted to copy it, so browser-side uploads arrived empty.

This version keeps `editor.html` unchanged and fixes the native C# backend.
