#!/usr/bin/env python3
"""Cut the Reckoning's sound cues (docs/PLAN_REWARD_SCREENS.md C11).

Usage, from the project root:
    python tools/make_reckoning_cues.py             # write the wavs
    python tools/make_reckoning_cues.py --dry-run   # report only

Then run tools/measure_audio_levels.py so the gain table covers them. Which
clip serves which moment lives in Core/Sound.cs (SoundLibrary.PathOf); this
file only produces the wavs it names.

Each row: output name, source (under SOUNDS, or the repo when it starts with
"repo:"), start seconds (or "onset" = first sample above ONSET_DB), length,
fade in, fade out, optional low-pass Hz, optional options dict. Options:
"layers" = extra (source, start, length, delay, gain) mixed under the same
fades; "tail" = seconds of soft decaying-noise reverb rung out after the
faded dry signal. Output is 44.1 kHz mono 16-bit, peak-normalised to
PEAK_DB; the loudness table sets the playback level.
"""

import argparse
import glob
import os
import subprocess
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, sosfilt

SOUNDS = os.environ.get("PP_SOUNDS", "C:/Games/Sounds")
OUT_DIR = "Assets/_Project/Resources/Audio/Sfx/Reckoning"
RATE = 44100
PEAK_DB = -3.0
ONSET_DB = -30.0
PRE_ROLL = 0.005
REVERB_WET = 0.35

FILMCOW = "FilmCow Recorded SFX/"
DUCK = "rubberduck 80 CC0 RPG SFX/"
SKY = "Fantasy UI SFX/Skyward Hero/SkywardHero_UI (%d).wav"
VOL2 = "Fantasy Vol.2 (atelier magicae)/"
SONNISS = "Sonniss/"

# name, source, start, length, fade_in, fade_out, lowpass_hz[, options]
CUES = [
    # Onset is 0.115 s into the source; start sits 5 ms before it so it lands at frame 0.
    ("card_deal", SONNISS + "344 Audio - Casino Cards Vol. 1/GAMECas_Dealing 3_344 Audio_Casino Cards Vol 1.wav",
     0.11, 0.34, 0.0, 0.10, None),
    ("ember_light", "Free Fantasy SFX Pack By TomMusic/WAV Files/SFX/Torch/Light Torch 1.wav", 0.0, 1.2, 0.01, 0.30, None),
    ("ember_snuff", "Free Fantasy SFX Pack By TomMusic/WAV Files/SFX/Torch/Light Torch 2.wav", 0.0, 0.60, 0.01, 0.25, 2500),
    ("card_flip", FILMCOW + "swoosh 6.wav", 0.0, 0.50, 0.004, 0.12, None),
    ("chevron_note", FILMCOW + "glass ding 11.wav", 0.0, 0.40, 0.004, 0.15, 3500),
    ("take", FILMCOW + "book pick up 5.wav", 0.10, 0.62, 0.004, 0.12, None),
    ("legendary_bell", VOL2 + "Fantasy UI Vol (50).wav", 0.0, 2.20, 0.004, 0.80, None),
    ("boss_peak", SKY % 40, "onset", 1.60, 0.003, 0.50, None),
    ("coin_tick", DUCK + "item_coins_04.ogg", 0.03, 0.28, 0.004, 0.10, 5500),
    ("potion_land", FILMCOW + "glass clink 2.wav", "onset", 0.35, 0.002, 0.15, 6000),
    ("book_land", FILMCOW + "book throw 5.wav", 0.20, 0.60, 0.004, 0.15, 3200),
    ("hone_scrape", SONNISS + "David Dumais Audio - Melee Weapons Sound Effects Pack 2/"
     "METLFric_SWING SCRAPE Swift Melee Weapon Swing With A Long Blade 14_DDUMAIS_MWP2.wav",
     0.03, 1.10, 0.004, 0.30, None),
    ("xp_hum", SKY % 25, "onset", 1.40, 0.10, 0.40, None),
    ("level_bell", FILMCOW + "ding 3.wav", "onset", 1.40, 0.002, 0.60, None),
    ("fell", SONNISS + "The Noisery - Moaning Metal/"
     "DSGNTonl_Designed Metal Bowed Screech Tonal Reverb 7_The Noisery_Moaning Metal.wav",
     0.90, 1.70, 0.25, 0.60, None),
    ("endured", VOL2 + "Fantasy UI Vol (14).wav", 0.0, 1.50, 0.15, 0.50, None),
    # Soft swell: 0.15 s in, 0.2 s out, then a 0.4 s decaying-noise tail so it
    # rings out under the reveal instead of stopping dead.
    ("reveal_riser", SKY % 25, 0.33, 0.65, 0.15, 0.20, None, {"tail": 0.4}),
    ("reveal_ring", VOL2 + "Crystal UI/Fantasy UI  - Crystal (2).wav", 0.0, 1.6, 0.004, 0.50, None),
    # Boom with the crystal sparkle laid on as its tail.
    ("reveal_impact", SONNISS + "Federico Soler - Effective Trailer Booms Vol. 2/EffectiveTrailer_Booms_Vol2_214.wav",
     0.2, 1.3, 0.004, 0.70, None,
     {"layers": [(VOL2 + "Fantasy UI Vol (27).wav", 0.0, 1.2, 0.12, 0.45)]}),
    ("reveal_rumble", SONNISS + "Jake Fielding - Interior Wind Rain and Storms/"
     "THUN_Interior Thunder Rumble_JF_INT Storm_01.wav", 7.5, 1.0, 0.15, 0.40, None),
    ("cloth_unfold", FILMCOW + "flag 7.wav", 0.0, 1.6, 0.004, 0.30, None),
]


def resolve(src):
    """SOUNDS-relative path; Sonniss files sit one bundle folder deeper."""
    if src.startswith("repo:"):
        return src[5:]
    path = os.path.join(SOUNDS, src)
    if not os.path.exists(path) and src.startswith(SONNISS):
        hits = glob.glob(os.path.join(SOUNDS, SONNISS, "*", src[len(SONNISS):]))
        if hits:
            return hits[0]
    return path


def decode(path):
    out = subprocess.run(
        ["ffmpeg", "-v", "quiet", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(RATE), "-"],
        capture_output=True, check=True).stdout
    return np.frombuffer(out, dtype=np.float32).astype(np.float64)


def lowpassed(d, hz):
    return sosfilt(butter(4, hz, btype="low", fs=RATE, output="sos"), d) if hz else d


def tail_reverb(dry, seconds):
    """Dry signal followed by `seconds` of soft, dark, decaying-noise reverb."""
    n = len(dry) + int(seconds * RATE)
    t = np.arange(int(seconds * RATE)) / RATE
    ir = np.random.default_rng(7).standard_normal(len(t)) * np.exp(-t * 6.9 / seconds)
    ir = lowpassed(ir, 4000)
    ir /= np.sqrt(np.sum(ir ** 2))
    wet = fftconvolve(np.concatenate([dry, np.zeros(n - len(dry))]), ir)[:n]
    return np.concatenate([dry, np.zeros(n - len(dry))]) + REVERB_WET * wet


def render(row):
    name, src, start, length, fade_in, fade_out, lowpass = row[:7]
    opts = row[7] if len(row) > 7 else {}
    d = decode(resolve(src))
    if start == "onset":
        floor = np.abs(d).max() * 10 ** (ONSET_DB / 20)
        hits = np.where(np.abs(d) > floor)[0]
        s = max(0, int(hits[0]) - int(PRE_ROLL * RATE)) if len(hits) else 0
    else:
        s = int(start * RATE)
    d = lowpassed(d[s:s + int(length * RATE)], lowpass)
    for lsrc, lstart, llen, delay, gain in opts.get("layers", []):
        x = decode(resolve(lsrc))[int(lstart * RATE):int((lstart + llen) * RATE)]
        o = int(delay * RATE)
        mix = np.zeros(max(len(d), o + len(x)))
        mix[:len(d)] += d
        mix[o:o + len(x)] += x * gain
        d = mix
    n = len(d)
    env = np.ones(n)
    fi, fo = min(n, int(fade_in * RATE)), min(n, int(fade_out * RATE))
    if fi:
        env[:fi] = np.linspace(0, 1, fi)
    if fo:
        env[n - fo:] *= np.linspace(1, 0, fo)
    d = d * env
    if opts.get("tail"):
        d = tail_reverb(d, opts["tail"])
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
