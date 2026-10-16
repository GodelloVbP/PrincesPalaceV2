#!/usr/bin/env python3
"""Cut the Reckoning's sound cues (docs/PLAN_REWARD_SCREENS.md C11).

Usage, from the project root:
    python tools/make_reckoning_cues.py             # write the wavs
    python tools/make_reckoning_cues.py --dry-run   # report only

Then run tools/measure_audio_levels.py so the gain table covers them. Which
clip serves which moment lives in Core/Sound.cs (SoundLibrary.PathOf); this
file only produces the wavs it names. CacheOpen plays Sfx/open_the_pack as
it is and has no row here.

Each row: output name, source (under SOUNDS, or the repo when it starts with
"repo:"), start seconds (or "onset" = first sample above ONSET_DB), length,
fade in, fade out, optional low-pass Hz. Output is 44.1 kHz mono 16-bit,
peak-normalised to PEAK_DB; the loudness table sets the playback level.
"""

import argparse
import os
import subprocess
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SOUNDS = os.environ.get("PP_SOUNDS", "C:/Games/Sounds")
OUT_DIR = "Assets/_Project/Resources/Audio/Sfx/Reckoning"
RATE = 44100
PEAK_DB = -3.0
ONSET_DB = -30.0
PRE_ROLL = 0.005

FILMCOW = "FilmCow Recorded SFX/"
DUCK = "rubberduck 80 CC0 RPG SFX/"
SKY = "Fantasy UI SFX/Skyward Hero/SkywardHero_UI (%d).wav"

# name, source, start, length, fade_in, fade_out, lowpass_hz
CUES = [
    ("card_deal", FILMCOW + "cloth mask removed 4.wav", 0.10, 0.50, 0.03, 0.20, 6000),
    ("ember_light", FILMCOW + "air duster 3.wav", 0.0, 0.60, 0.12, 0.25, 2500),
    ("ember_snuff", FILMCOW + "air duster 1.wav", 0.0, 0.40, 0.01, 0.15, 2000),
    ("card_flip", DUCK + "book_01.ogg", "onset", 0.25, 0.005, 0.12, 7000),
    ("chevron_note", SKY % 11, "onset", 0.42, 0.003, 0.10, None),
    ("take", SKY % 24, "onset", 0.60, 0.003, 0.20, None),
    ("legendary_bell", "repo:Assets/_Project/Resources/Audio/Sfx/Events/bell_in_the_fog/toll.wav",
     0.0, 2.20, 0.0, 0.80, None),
    ("boss_peak", SKY % 40, "onset", 1.60, 0.003, 0.50, None),
    ("coin_tick", DUCK + "item_coins_01.ogg", "onset", 0.16, 0.002, 0.06, None),
    ("potion_land", FILMCOW + "glass clink 2.wav", "onset", 0.35, 0.002, 0.15, 6000),
    ("book_land", FILMCOW + "book pick up 3.wav", 0.0, 0.50, 0.005, 0.15, 4000),
    ("hone_scrape", DUCK + "stones_02.ogg", 0.10, 0.60, 0.03, 0.15, None),
    ("xp_hum", SKY % 25, "onset", 1.40, 0.10, 0.40, None),
    ("level_bell", FILMCOW + "ding 3.wav", "onset", 1.40, 0.002, 0.60, None),
    ("fell", SKY % 26, "onset", 1.40, 0.02, 0.60, None),
    ("endured", SKY % 31, "onset", 1.40, 0.02, 0.60, None),
    # Placeholders for the one-by-one reveal, cut from shipped clips until the
    # real ones are picked: a cast riser, a ward chime, an impact, a slam.
    ("reveal_riser", "repo:Assets/_Project/Resources/Audio/Sfx/court_of_whispers_cast.wav", 0.0, 0.70, 0.05, 0.10, None),
    ("reveal_ring", "repo:Assets/_Project/Resources/Audio/Sfx/status_ward_1.wav", "onset", 0.90, 0.003, 0.40, None),
    ("reveal_impact", "repo:Assets/_Project/Resources/Audio/Sfx/crownfall_impact.wav", "onset", 0.80, 0.002, 0.30, None),
    ("reveal_rumble", "repo:Assets/_Project/Resources/Audio/Sfx/boulder_slam_impact.wav", "onset", 0.39, 0.002, 0.15, 900),
]


def decode(path):
    out = subprocess.run(
        ["ffmpeg", "-v", "quiet", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(RATE), "-"],
        capture_output=True, check=True).stdout
    return np.frombuffer(out, dtype=np.float32).astype(np.float64)


def render(row):
    name, src, start, length, fade_in, fade_out, lowpass = row
    path = src[5:] if src.startswith("repo:") else os.path.join(SOUNDS, src)
    d = decode(path)
    if start == "onset":
        floor = np.abs(d).max() * 10 ** (ONSET_DB / 20)
        hits = np.where(np.abs(d) > floor)[0]
        s = max(0, int(hits[0]) - int(PRE_ROLL * RATE)) if len(hits) else 0
    else:
        s = int(start * RATE)
    d = d[s:s + int(length * RATE)]
    if lowpass:
        d = sosfilt(butter(4, lowpass, btype="low", fs=RATE, output="sos"), d)
    n = len(d)
    env = np.ones(n)
    fi, fo = min(n, int(fade_in * RATE)), min(n, int(fade_out * RATE))
    if fi:
        env[:fi] = np.linspace(0, 1, fi)
    if fo:
        env[n - fo:] *= np.linspace(1, 0, fo)
    d = d * env
    d *= 10 ** (PEAK_DB / 20) / max(np.abs(d).max(), 1e-9)
    return (np.clip(d, -1, 1) * 32767).astype(np.int16)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    os.makedirs(OUT_DIR, exist_ok=True)
    for row in CUES:
        pcm = render(row)
        print("%-16s %5.2fs  <- %s" % (row[0], len(pcm) / RATE, row[1]))
        if not args.dry_run:
            wavfile.write(os.path.join(OUT_DIR, row[0] + ".wav"), RATE, pcm)


if __name__ == "__main__":
    sys.exit(main())
