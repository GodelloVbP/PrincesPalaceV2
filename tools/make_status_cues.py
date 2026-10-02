#!/usr/bin/env python3
"""Cut the status-application cues from Leonardo Calvo's Holy Spell Pack.

Usage, from the project root:
    python tools/make_status_cues.py            # write the wavs, print the table
    python tools/make_status_cues.py --dry-run

Then re-run tools/measure_audio_levels.py so the gain table covers them.
Which clip serves which status lives in Core/StatusCues.cs; this file only
produces the wavs it names.

Each clip is cut like the spell impacts: 44.1 kHz mono 16-bit, started at the
first transient, ended where it decays below -40 dB or at CAP, with a fade-out.
"""

import os
import sys
import wave

import numpy as np
from scipy.io import wavfile
from scipy.signal import resample_poly

SOURCE = "C:/Games/Sounds/Prince's Palace/Spells/Holy Spell Pack/96000kHz/wav"
OUT_ROOT = "Assets/_Project/Resources/Audio/Sfx"
RATE = 44100
CAP = 1.2
FADE_OUT = 0.15
FLOOR_DB = -40.0
HOP = 0.01

CUES = [
    ("status_ward_1", "Holy Spell Protect Cast 01 HQ.wav"),
    ("status_ward_2", "Holy Spell Protect Cast 02 HQ.wav"),
    ("status_ward_3", "Holy Spell Protect Cast 03 HQ.wav"),
    ("status_regen_1", "Holy Spell Heal Cast Bright 01 HQ.wav"),
    ("status_regen_2", "Holy Spell Heal Cast Bright 02 HQ.wav"),
    ("status_regen_3", "Holy Spell Heal Cast Bright 03 HQ.wav"),
]


def load(path):
    rate, raw = wavfile.read(path)
    scale = {"int16": 32768.0, "int32": 2147483648.0}.get(str(raw.dtype), 1.0)
    x = raw.astype(np.float64) / scale
    if x.ndim > 1:
        x = x.mean(axis=1)
    g = np.gcd(rate, RATE)
    return resample_poly(x, RATE // g, rate // g)


def cut(x):
    n = int(RATE * HOP)
    k = len(x) // n
    rms = np.sqrt((x[: k * n].reshape(k, n) ** 2).mean(axis=1) + 1e-12)
    db = 20 * np.log10(rms / rms.max())
    first = int(np.argmax(db >= -20.0))          # first transient
    live = np.nonzero(db[first:] > FLOOR_DB)[0]
    last = first + (live[-1] + 1 if len(live) else 1)
    seconds = min((last - first) * HOP, CAP)
    piece = x[first * n: first * n + int(seconds * RATE)].copy()
    fo = min(int(FADE_OUT * RATE), len(piece))
    piece[-fo:] *= np.linspace(1.0, 0.0, fo)
    top = np.abs(piece).max()
    if top > 0.98:
        piece *= 0.98 / top
    return piece, first * HOP


def main():
    dry = "--dry-run" in sys.argv
    for out, name in CUES:
        piece, start = cut(load(os.path.join(SOURCE, name)))
        print("%-16s %-40s start %.2fs  len %.2fs" % (out, name, start, len(piece) / RATE))
        if dry:
            continue
        pcm = (np.clip(piece, -1, 1) * 32767).astype(np.int16)
        with wave.open(os.path.join(OUT_ROOT, out + ".wav"), "wb") as h:
            h.setnchannels(1); h.setsampwidth(2); h.setframerate(RATE)
            h.writeframes(pcm.tobytes())


if __name__ == "__main__":
    main()
