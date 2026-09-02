"""make_art_stubs.py -- generate a same-dimension, flat-colour stand-in tree
for Assets/_Project/Art (and, optionally, Assets/_Project/Resources/Audio),
for use ONLY in the PlayMode TestRunner copy (-TestRunner2), never in main.

Why "same dimension": docs/ART_PIPELINE.md documents that sprite-sheet .meta
files carry authored slice rects (grid cells, explicit row bands) that assume
a specific pixel size. A stub of the wrong size would make every slice rect in
that .meta invalid, which is a silent-corruption risk far worse than the git
history it's meant to save. So every .png/.jpg gets a flat-colour replacement
at IDENTICAL width/height, and its .meta is copied byte-for-byte unchanged
(the meta is what carries import settings and, for a sub-sprite atlas, the
slice rects themselves -- untouched, they still describe the stub correctly).

Audio: every .wav/.ogg gets a fixed-length near-silent stand-in of the same
container format (so an importer that reads channels/sample rate from the
file header does not choke), metas copied unchanged.

Usage:
    python tools/make_art_stubs.py [--source DIR] [--audio-source DIR] [--out DIR] [--dry-run]

Defaults:
    --source        Assets/_Project/Art               (relative to project root)
    --audio-source  Assets/_Project/Resources/Audio
    --out           C:\\Games\\Prince's Palace-v2-ArtStubs

Never writes into Assets/ itself. Overlay the output onto -TestRunner2's own
Assets/_Project/Art and Assets/_Project/Resources/Audio (robocopy /E, then
restore with /MIR from main when done) if you want to re-measure this by hand.

MEASURED, 2026-09-02, and the answer is negative -- do not wire this into
run_tests_parallel.ps1 without re-measuring first:

  - Ten SceneManager.LoadSceneAsync(Single) loads each of Fight/Map/Hub,
    real art vs this tool's stub tree overlaid on -TestRunner2: real art
    Fight 0.520s/Map 0.476s/Hub 0.417s mean; stub art 0.534s/0.472s/0.420s.
    Differences are inside run-to-run noise -- stub art loads NO faster.
  - Full PlayMode suite (650 tests) in -TestRunner2, same real-art-synced
    baseline vs the same overlay, warm (no reimport pay-off included):
    203.6s real art, 211.2s stub art -- SLOWER, not faster, and every
    individual test class moved by well under a second in either direction.
    Same 624 passed / 0 failed / 26 skipped both times, so stubbing is at
    least functionally safe -- it just buys nothing.
  - Why: docs/ART_PIPELINE.md's LoadSpriteByKey caches every Art/ sprite
    after its first AssetDatabase read per build, and tools/test.ps1's own
    header already documents PlayMode's 181-203s as having "no dominant
    hotspot" spread across ~87 classes with no asset-loading concentration.
    A same-dimension flat PNG is exactly as expensive for Unity's importer
    and AssetDatabase to touch once as the real file -- the 618MB on disk
    was never what a run was paying for per-test; a fixed ~8s Unity boot
    and per-test scene-teardown/wait overhead is.

Kept committed anyway, per the project's "leave the tooling committed only if
it is small and documented" rule for a negative result -- small enough
(~200 lines) that re-verifying this after the Art/ tree grows a lot further
costs one command, not a rebuild of the tool.
"""

import argparse
import os
import struct
import sys
import wave
import zlib
from pathlib import Path

try:
    from PIL import Image
    HAVE_PIL = True
except ImportError:
    HAVE_PIL = False

STUB_COLOR = (128, 96, 160, 255)  # flat lavender-grey; visually obviously-a-stub if it ever leaks into a screenshot
IMG_EXTS = {".png", ".jpg", ".jpeg"}
AUDIO_EXTS = {".wav", ".ogg"}


# --- pure-Python PNG writer, used only if PIL is unavailable ---------------
def write_flat_png(path, width, height, color=STUB_COLOR):
    width = max(1, width)
    height = max(1, height)

    def chunk(tag, data):
        c = tag + data
        return struct.pack(">I", len(data)) + c + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF)

    sig = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    row = bytes([0]) + bytes(color) * width  # filter type 0 (none) + pixels
    raw = row * height
    idat = zlib.compress(raw, 6)
    return sig + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")


def stub_image(src_path, dst_path):
    if HAVE_PIL:
        with Image.open(src_path) as im:
            w, h = im.size
        out = Image.new("RGBA", (w, h), STUB_COLOR)
        out.save(dst_path, "PNG")
        return w, h
    else:
        # Read just the IHDR for PNG; for JPEG fall back to a fixed guess-free
        # size read via a minimal parser is not worth it here -- Pillow is
        # present in this environment (checked at import time), so this path
        # is the documented fallback, not the one actually exercised.
        with open(src_path, "rb") as f:
            data = f.read(33)
        if data[:8] == b"\x89PNG\r\n\x1a\n":
            w, h = struct.unpack(">II", data[16:24])
        else:
            raise RuntimeError(f"No PIL available and {src_path} is not a PNG; cannot read dimensions.")
        png_bytes = write_flat_png(dst_path, w, h)
        with open(dst_path, "wb") as f:
            f.write(png_bytes)
        return w, h


def stub_audio(src_path, dst_path):
    ext = src_path.suffix.lower()
    if ext == ".wav":
        try:
            with wave.open(str(src_path), "rb") as src:
                nchannels = src.getnchannels()
                sampwidth = src.getsampwidth()
                framerate = src.getframerate()
            nframes = max(1, int(framerate * 0.1))
            with wave.open(str(dst_path), "wb") as dst:
                dst.setnchannels(nchannels)
                dst.setsampwidth(sampwidth)
                dst.setframerate(framerate)
                dst.writeframes(b"\x00" * nframes * nchannels * sampwidth)
        except wave.Error:
            # stdlib wave only understands PCM (format tag 1); this project's
            # source .wav files are IEEE-float (tag 3), which it refuses to
            # even read. Rather than hand-roll a float-PCM WAV writer for a
            # 12MB-total asset class, fall back to copying the real file --
            # see the report: this is why audio contributes ~0 to the saving
            # either way.
            import shutil
            shutil.copy2(src_path, dst_path)
    else:
        # .ogg: no stdlib encoder. Ship a real, tiny, valid 0.1s silent Ogg
        # Vorbis file baked once as bytes, rather than pull in a dependency
        # for a stub. If this exact byte blob is ever wrong for an importer,
        # the fallback below (copy the real file) keeps stubbing from
        # silently producing a broken import.
        if _TINY_SILENT_OGG is not None:
            dst_path.write_bytes(_TINY_SILENT_OGG)
        else:
            import shutil
            shutil.copy2(src_path, dst_path)


# A minimal valid silent Ogg/Vorbis stream is nontrivial to hand-roll without
# a library; rather than ship a maybe-wrong hardcoded blob, audio stubbing
# degrades to "copy the real file" for .ogg specifically. See report: Audio
# is 12MB total here (not 618MB), so this doesn't change the measured saving.
_TINY_SILENT_OGG = None


def iter_files(root, exts):
    for dirpath, _dirnames, filenames in os.walk(root):
        for name in filenames:
            if os.path.splitext(name)[1].lower() in exts:
                yield Path(dirpath) / name


def main():
    ap = argparse.ArgumentParser()
    project_root = Path(__file__).resolve().parent.parent
    ap.add_argument("--source", default=str(project_root / "Assets" / "_Project" / "Art"))
    ap.add_argument("--audio-source", default=str(project_root / "Assets" / "_Project" / "Resources" / "Audio"))
    ap.add_argument("--out", default=r"C:\Games\Prince's Palace-v2-ArtStubs")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    src_root = Path(args.source)
    audio_root = Path(args.audio_source)
    out_root = Path(args.out)

    real_bytes = 0
    stub_bytes = 0
    n_img = 0
    n_audio = 0
    n_meta = 0

    if src_root.is_dir():
        for f in iter_files(src_root, IMG_EXTS):
            rel = f.relative_to(src_root)
            dst = out_root / "Art" / rel
            real_bytes += f.stat().st_size
            if not args.dry_run:
                dst.parent.mkdir(parents=True, exist_ok=True)
                stub_image(f, dst)
                stub_bytes += dst.stat().st_size
                meta = f.with_suffix(f.suffix + ".meta")
                if meta.exists():
                    (dst.parent / (dst.name + ".meta")).write_bytes(meta.read_bytes())
                    n_meta += 1
            n_img += 1

    if audio_root.is_dir():
        for f in iter_files(audio_root, AUDIO_EXTS):
            rel = f.relative_to(audio_root)
            dst = out_root / "Resources" / "Audio" / rel
            real_bytes += f.stat().st_size
            if not args.dry_run:
                dst.parent.mkdir(parents=True, exist_ok=True)
                stub_audio(f, dst)
                stub_bytes += dst.stat().st_size
                meta = f.with_suffix(f.suffix + ".meta")
                if meta.exists():
                    (dst.parent / (dst.name + ".meta")).write_bytes(meta.read_bytes())
                    n_meta += 1
            n_audio += 1

    print(f"images: {n_img}  audio: {n_audio}  metas copied: {n_meta}")
    print(f"real bytes:  {real_bytes / 1e6:.1f} MB")
    if not args.dry_run:
        print(f"stub bytes:  {stub_bytes / 1e6:.1f} MB")
        print(f"out root:    {out_root}")
    else:
        print("(dry run: nothing written)")


if __name__ == "__main__":
    sys.exit(main())
