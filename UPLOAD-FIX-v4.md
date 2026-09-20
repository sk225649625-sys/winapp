# ReelForge upload fix v4

Root cause fixed: the WebView2 `WebResourceRequested` handler now uses an
explicit request deferral for its entire asynchronous lifetime. This keeps the
POST request body stream alive while the native backend copies the uploaded
file.

The previous versions handled `/api/upload` before `ReadBody`, but the request
itself could still complete before the awaited stream copy finished. v4 fixes
that lifecycle issue.

`editor.html` remains unchanged.
