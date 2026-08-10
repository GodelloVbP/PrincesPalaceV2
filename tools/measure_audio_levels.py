#!/usr/bin/env python3
"""Measure every audio asset's loudness and emit a committed gain table.

The problem this exists for, measured before it was written: across the 31
clips under `Resources/Audio/`, integrated loudness spanned **14.6 dB**
(-22.5 to -7.9 LUFS) and RMS spanned **20.8 dB**. Shawn's voice lines sat
around -8 dB while the UI sounds sat near -29 dB, so the same master volume
made one deafening and the other inaudible. Several clips also peaked ABOVE
full scale (`shawn_hit_attack` at +1.9 dBTP), which is already clipping.

## Why a gain table and not normalised files

Rewriting the assets in place would be the obvious move, and `trim_wav.py`
sets a precedent for editing audio destructively. It is the wrong one here:
four of the music tracks are `.ogg` and two effects are `.mp3`, and applying
gain to a lossy file means decoding and re-encoding it. That is the same
compounding-loss mistake `slice_actor_sheet.py` refuses outright when it
declines to read its own output back in. A gain applied at playback costs
nothing, touches no source bytes, works the same for wav/ogg/mp3, and shows
up in a diff as numbers a human can argue with.

## Which metric, and why not just one

Two are used, each where it is actually valid:

* **Music -> integrated LUFS (EBU R128).** Long-form program material is
  exactly what R128 was designed to measure.
* **SFX and voice -> RMS.** R128 gates on 400ms blocks, so a 0.25s button
  click or a 0.33s grunt measures as `-inf` and cannot be normalised by it at
  all. Nine of this project's clips are in that boat. RMS has no gate and is
  the honest measure for a transient.

Mixing metrics is deliberate and the reason is above; each category is only
ever compared against its own target, never across.

## Guards

* Gain is clamped to +-`MAX_GAIN_DB` so one pathological file cannot be
  boosted until its noise floor is audible.
* Gain is further reduced so no clip's true peak can exceed `PEAK_CEILING_DB`
  -- which also pulls the already-clipping files back down under full scale.
* Requires ffmpeg on PATH and says so plainly if it is missing.

Usage:
    python tools/measure_audio_levels.py            # measure + write table
    python tools/measure_audio_levels.py --check    # measure, write nothing,
                                                    # exit 1 if the committed
                                                    # table is out of date
"""

import argparse
import json
import os
import re
import subprocess
import sys

AUDIO_ROOT = "Assets/_Project/Resources/Audio"
OUTPUT_PATH = os.path.join(AUDIO_ROOT, "audio_levels.json")
EXTENSIONS = (".wav", ".ogg", ".mp3")

# Targets are per-category and are NOT meant to make music and effects equally
# loud -- GameSettings keeps MusicVolume and SoundVolume separate precisely so
# the player sets that balance. These only make each category internally
# consistent, so one voice line is not five times another.
MUSIC_TARGET_LUFS = -18.0
SFX_TARGET_RMS_DB = -18.0

# True-peak ceiling. Below 0 so that resampling and lossy decode, both of
# which can overshoot the sample values actually stored, still land inside
# full scale.
PEAK_CEILING_DB = -1.0

# No clip is trusted with more than this much correction. A file needing more
# is a mastering problem to fix at the source, not something to paper over by
# amplifying its hiss.
MAX_GAIN_DB = 12.0


def ffmpeg_available():
    try:
        subprocess.run(["ffmpeg", "-version"], capture_output=True, check=True)
        return True
    except (OSError, subprocess.CalledProcessError):
        return False


def _run_filter(path, audio_filter):
    return subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostats", "-i", path,
         "-af", audio_filter, "-f", "null", "-"],
        capture_output=True, text=True).stderr


def measure_lufs(path):
    """(integrated LUFS, true peak dB) or (None, true peak) when the clip is
    too short for R128's 400ms gate."""
    err = _run_filter(path, "loudnorm=I=%.1f:TP=%.1f:print_format=json"
                      % (MUSIC_TARGET_LUFS, PEAK_CEILING_DB))
    match = re.search(r'\{[^{}]*"input_i"[^{}]*\}', err, re.S)
    if not match:
        return None, None
    parsed = json.loads(match.group(0))
    lufs = float(parsed["input_i"])
    peak = float(parsed["input_tp"])
    if lufs < -70.0:  # -inf: gated out entirely
        return None, peak
    return lufs, peak


def measure_rms(path):
    """(mean RMS dBFS, max sample dBFS). Works at any length."""
    err = _run_filter(path, "volumedetect")
    mean = re.search(r"mean_volume:\s*(-?[\d.]+) dB", err)
    peak = re.search(r"max_volume:\s*(-?[\d.]+) dB", err)
    if not mean or not peak:
        return None, None
    return float(mean.group(1)), float(peak.group(1))


def category_of(rel_path):
    return "music" if rel_path.lower().startswith("music/") else "sfx"


def gain_for(rel_path, path):
    """(gain_db, detail dict) -- the correction this clip needs."""
    category = category_of(rel_path)
    rms_db, rms_peak = measure_rms(path)
    lufs, true_peak = measure_lufs(path)

    # Prefer the true peak loudnorm reports (it accounts for inter-sample
    # overshoot); fall back to the raw sample peak when it is unavailable.
    peak_db = true_peak if true_peak is not None else rms_peak
    if peak_db is None:
        return None, {"error": "ffmpeg reported no peak"}

    if category == "music" and lufs is not None:
        metric, measured, target = "lufs", lufs, MUSIC_TARGET_LUFS
    elif rms_db is not None:
        metric, measured, target = "rms", rms_db, SFX_TARGET_RMS_DB
    else:
        return None, {"error": "no usable loudness measurement"}

    wanted = target - measured
    clamped = max(-MAX_GAIN_DB, min(MAX_GAIN_DB, wanted))
    # ...and never so much that the clip would peak over the ceiling. This is
    # what also drags the already-over-full-scale files back down.
    headroom = PEAK_CEILING_DB - peak_db
    final = min(clamped, headroom)

    return final, {
        "metric": metric,
        "measured": round(measured, 1),
        "peak": round(peak_db, 1),
        "wanted": round(wanted, 1),
        "clamped_by": ("peak-ceiling" if final < clamped - 0.05
                       else ("max-gain" if abs(clamped - wanted) > 0.05 else "none")),
    }


def collect(verbose=True):
    entries = []
    for root, _dirs, files in os.walk(AUDIO_ROOT):
        for name in sorted(files):
            if not name.lower().endswith(EXTENSIONS):
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, AUDIO_ROOT).replace(os.sep, "/")
            gain_db, detail = gain_for(rel, path)
            if gain_db is None:
                print("  WARNING: %s -- %s" % (rel, detail.get("error")))
                continue

            # Keyed WITHOUT the extension: everything at runtime addresses
            # audio the way Resources.Load does, by extensionless path
            # ("Audio/Sfx/button click"), and that is the only key a caller
            # can produce without knowing how the file was encoded.
            key = os.path.splitext(rel)[0]
            entries.append({
                "path": key,
                "gainDb": round(gain_db, 2),
                "gain": round(10.0 ** (gain_db / 20.0), 4),
                "metric": detail["metric"],
                "measuredDb": detail["measured"],
                "peakDb": detail["peak"],
            })
            if verbose:
                note = "" if detail["clamped_by"] == "none" else "  (limited by %s)" % detail["clamped_by"]
                print("  %-44s %-5s %7.1f dB -> gain %+6.2f dB%s"
                      % (key[:44], detail["metric"], detail["measured"], gain_db, note))

    entries.sort(key=lambda e: e["path"])
    return entries


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true",
                        help="measure but write nothing; exit 1 if the committed table is stale")
    args = parser.parse_args()

    if not ffmpeg_available():
        sys.exit("ffmpeg is required and was not found on PATH.")

    if not os.path.isdir(AUDIO_ROOT):
        sys.exit("No audio at %s -- run this from the repository root." % AUDIO_ROOT)

    print("Measuring %s ..." % AUDIO_ROOT)
    entries = collect()
    if not entries:
        sys.exit("No audio files measured.")

    table = {
        "_comment": ("Generated by tools/measure_audio_levels.py -- do not hand-edit. "
                     "gain is a linear multiplier applied at playback; see the tool's "
                     "docstring for why the assets themselves are left untouched."),
        "musicTargetLufs": MUSIC_TARGET_LUFS,
        "sfxTargetRmsDb": SFX_TARGET_RMS_DB,
        "peakCeilingDb": PEAK_CEILING_DB,
        "entries": entries,
    }
    rendered = json.dumps(table, indent=2) + "\n"

    gains = [e["gainDb"] for e in entries]
    print()
    print("%d clip(s). Correction spans %.1f .. %+.1f dB." % (len(entries), min(gains), max(gains)))

    if args.check:
        current = None
        if os.path.exists(OUTPUT_PATH):
            with open(OUTPUT_PATH, "r", encoding="utf-8") as handle:
                current = handle.read()
        if current != rendered:
            sys.exit("%s is out of date -- rerun tools/measure_audio_levels.py "
                     "(audio was added, removed or re-mastered)." % OUTPUT_PATH)
        print("%s is up to date." % OUTPUT_PATH)
        return

    with open(OUTPUT_PATH, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(rendered)
    print("Wrote %s" % OUTPUT_PATH)


if __name__ == "__main__":
    main()
