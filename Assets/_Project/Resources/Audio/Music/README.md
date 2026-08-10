# Music

Long-form loops and ambience go here. See `../README.md` for how the two
tracks currently here are wired (`MusicController`, `MusicTrack`).

`Battle_music_for_now.mpeg` is raw source material, not a usable clip -- it
imports as a `VideoClipImporter`, not an `AudioClip` (Unity treats `.mpeg` as
video), and nothing loads it. `Battle_theme.ogg` is the actual game asset cut
from it (first 14s, normalized, light reverb) -- re-cut from this source if
the timing or effect needs revisiting, rather than re-deriving it from the
finished `.ogg`.

Set a music clip's import mode to **Streaming** rather than Decompress On
Load, or a few minutes of audio sits decompressed in memory for the whole
session.
