# Mapping from the supplied HTML editor to native C++

| Supplied HTML module | Native C++ module |
| --- | --- |
| Media library | `MediaLibrary.*` + `MainWindow.*` |
| MOTIONS / TRANS / LOOKS / CAPS / FITS / AFX | `Catalogs.*` |
| Timeline clips | `Timeline.*` |
| Project JSON | `ProjectStore.*` |
| Preview / playback | `Preview.*` |
| FFmpeg render | `Renderer.*` + `FFmpegRunner.*` |
| Video FX / audio FX | `EffectsEngine.*` |
| Clip / FX / Audio / Text inspector | `MainWindow::RefreshInspector()` |
| Local file picker + drag/drop | `MainWindow::ChooseFiles()` / `WM_DROPFILES` |
