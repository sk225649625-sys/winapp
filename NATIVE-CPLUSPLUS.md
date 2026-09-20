# Native C++ conversion notes

The supplied editor source was used for its module/catalog terminology:

- Media library: Photos, Music, SFX, Voice over
- Clip / Video FX / Audio / Text inspector tabs
- Ratio / Quality / FPS controls
- Preview / Export actions
- Timeline clips, audio lanes and voice sequence
- Motion, transition, looks, captions, fit and audio-FX options

The new implementation removes the browser transport layer and stores actual
Windows filesystem paths in the C++ model. It is a native Win32 rewrite, not a
web wrapper.

The renderer currently implements the native baseline for image/video concat,
scaling, basic motion/look treatments, global vignette/letterbox/fade and the
first audio track. The rest of the editor model is retained so additional
effects/transitions can be filled in as native FFmpeg filters.
