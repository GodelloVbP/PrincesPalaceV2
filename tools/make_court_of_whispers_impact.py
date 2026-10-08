#!/usr/bin/env python3
"""Build Court of Whispers' impact clip: the dry strike plus a long whispering tail.

Usage, from the project root:
    python tools/make_court_of_whispers_impact.py              # write the wav
    python tools/make_court_of_whispers_impact.py --out X.wav  # write elsewhere

Then re-run tools/measure_audio_levels.py so audio_levels.json covers it.

Inputs live in tools/audio_sources/court_of_whispers/:
    dry_impact.wav   the clip as shipped before the tail (Calvo Dark curse_03, 1.2 s)
    lines/*.wav      spoken lines from the Super Dialogue Audio Pack v1 ("Step 2 -
                     Audio Files/6 - Miscellaneous"), the whisper bed's raw material

The clip is dry + tail. The dry strike (transient and body) is copied unchanged;
everything added starts at or after 40 ms, so the head matches the source:
    * two detuned copies of the body (+-12 cents) under the dry,
    * a noise-IR reverb (RT60 1.8 s, 40 ms pre-delay) and two echo taps (120/240 ms),
    * a whisper bed entering 0.15 s in and decaying with the reverb.
A closing fade takes the tail to silence, well under -60 dB, at the clip's end.

The bed is whisperised dialogue: each line is resynthesised with noise-excited LPC,
which keeps the formants and removes the voicing, then layered at different speeds
and offsets, half of it reversed, so no word survives.

Deterministic: fixed seeds, no clock, so a rerun is byte-identical.
Needs numpy and scipy.
"""

import argparse
import glob
import os
import sys
import wave
from fractions import Fraction

import numpy as np
from scipy.linalg import solve_toeplitz
from scipy.signal import butter, fftconvolve, lfilter, resample_poly, sosfilt

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SOURCE_DIR = os.path.join(HERE, "audio_sources", "court_of_whispers")
OUT_DEFAULT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio", "Sfx",
                           "court_of_whispers_impact.wav")
RATE = 44100

LENGTH = 2.3          # seconds, whole clip
HEAD_EXACT = 0.040    # seconds of the clip that is the dry source, untouched
RT60 = 1.8
PREDELAY = 0.040
ECHOES = ((0.120, 0.30), (0.240, 0.18))   # (seconds, gain)
WET_GAIN = 0.40       # L2-normalised IR scale
DETUNE_CENTS = 12.0
DETUNE_GAIN = 0.14    # each copy, linear
BED_ENTRY = 0.15
BED_RATIO = 0.40      # bed RMS over its first 0.3 s, relative to the dry body's
CLOSE_FADE = 0.60     # final fade to silence

# line stem -> (speed, reversed, start seconds, gain). Speed < 1 is slower and lower.
BED_PLAN = {
    "miscellaneous_16_meghan": (0.86, True, 0.00, 1.00),
    "miscellaneous_9_meghan": (1.12, False, 0.10, 0.85),
    "miscellaneous_20_alex": (0.78, True, 0.25, 0.90),
    "miscellaneous_9_alex": (0.93, False, 0.40, 0.80),
    "miscellaneous_9_ian": (1.22, True, 0.55, 0.75),
    "miscellaneous_12_sean": (1.05, False, 0.70, 0.70),
}

LPC_ORDER = 18
FRAME = 1024
HOP = 256


def read_wav(path):
    with wave.open(path, "rb") as w:
        assert w.getnchannels() == 1 and w.getsampwidth() == 2 and w.getframerate() == RATE, path
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0


def write_wav(path, samples):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    pcm = np.rint(np.clip(samples, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


def whisperize(x, seed):
    """Noise-excited LPC resynthesis: formants kept, voicing gone."""
    rng = np.random.default_rng(seed)
    x = np.append(x[0], x[1:] - 0.95 * x[:-1])             # pre-emphasis
    pad = np.zeros(FRAME)
    x = np.concatenate([pad, x, pad])
    window = np.hanning(FRAME + 1)[:-1]                     # periodic: sums to 2 at 75% overlap
    out = np.zeros(len(x))
    for start in range(0, len(x) - FRAME, HOP):
        frame = x[start:start + FRAME] * window
        energy = float(np.sqrt(np.mean(frame ** 2)))
        if energy < 1e-5:
            continue
        r = np.correlate(frame, frame, "full")[FRAME - 1:FRAME + LPC_ORDER]
        r[0] *= 1.0001
        r *= np.exp(-0.5 * (2 * np.pi * 60.0 / RATE * np.arange(LPC_ORDER + 1)) ** 2)  # lag window
        a = np.concatenate([[1.0], -solve_toeplitz(r[:LPC_ORDER], r[1:LPC_ORDER + 1])])
        y = lfilter([1.0], a, rng.standard_normal(FRAME))
        y *= window * energy / (float(np.sqrt(np.mean((y * window) ** 2))) + 1e-12)
        out[start:start + FRAME] += y
    out = out[FRAME:-FRAME] / 2.0
    out = sosfilt(butter(2, 180.0 / (RATE / 2), "highpass", output="sos"), out)
    return out / (float(np.sqrt(np.mean(out ** 2))) + 1e-12)


def change_speed(x, speed):
    f = Fraction(speed).limit_denominator(100)
    return resample_poly(x, f.denominator, f.numerator)


def whisper_bed(n):
    bed = np.zeros(n)
    for index, path in enumerate(sorted(glob.glob(os.path.join(SOURCE_DIR, "lines", "*.wav")))):
        stem = os.path.splitext(os.path.basename(path))[0]
        speed, reverse, start, gain = BED_PLAN[stem]
        line = change_speed(whisperize(read_wav(path), 2026100300 + index), speed)
        if reverse:
            line = line[::-1]
        fade = int(0.08 * RATE)
        line = line.copy()
        line[:fade] *= np.linspace(0.0, 1.0, fade)
        line[-fade:] *= np.linspace(1.0, 0.0, fade)
        s = int(start * RATE)
        piece = line[: max(n - s, 0)]
        bed[s:s + len(piece)] += piece * gain
    t = np.arange(n) / RATE
    since = np.maximum(t - BED_ENTRY, 0.0)
    envelope = np.where(t >= BED_ENTRY, np.minimum(since / 0.12, 1.0), 0.0) * np.exp(-6.9078 * since / RT60)
    return bed * envelope


def reverb_ir(n):
    rng = np.random.default_rng(2026100399)
    t = np.arange(n) / RATE
    ir = rng.standard_normal(n) * np.exp(-6.9078 * t / RT60)
    ir = sosfilt(butter(2, 5200.0 / (RATE / 2), "lowpass", output="sos"), ir)
    ir[: int(PREDELAY * RATE)] = 0.0
    return ir / np.sqrt(np.sum(ir ** 2))


def detuned(body, cents, n, start):
    f = Fraction(2.0 ** (cents / 1200.0)).limit_denominator(2000)
    shifted = resample_poly(body, f.denominator, f.numerator)
    shifted = shifted.copy()
    fade = int(0.03 * RATE)
    shifted[:fade] *= np.linspace(0.0, 1.0, fade)
    out = np.zeros(n)
    piece = shifted[: n - start]
    out[start:start + len(piece)] = piece
    return out


def build():
    n = int(round(LENGTH * RATE))
    dry_src = read_wav(os.path.join(SOURCE_DIR, "dry_impact.wav"))
    dry = np.zeros(n)
    dry[: len(dry_src)] = dry_src

    mix = dry.copy()
    start = int(HEAD_EXACT * RATE)
    body = dry_src[start:]
    for cents in (DETUNE_CENTS, -DETUNE_CENTS):
        mix += DETUNE_GAIN * detuned(body, cents, n, start)

    soft = lambda x: sosfilt(butter(2, 3200.0 / (RATE / 2), "lowpass", output="sos"), x)  # noqa: E731
    muffled = soft(dry)
    for seconds, gain in ECHOES:
        d = int(seconds * RATE)
        mix[d:] += gain * muffled[: n - d]

    mix += WET_GAIN * fftconvolve(dry, reverb_ir(n))[:n]

    bed = whisper_bed(n)
    window = slice(int(BED_ENTRY * RATE), int((BED_ENTRY + 0.3) * RATE))
    ref = np.sqrt(np.mean(dry[window] ** 2))
    mix += bed * (BED_RATIO * ref / (np.sqrt(np.mean(bed[window] ** 2)) + 1e-12))

    close = int(CLOSE_FADE * RATE)
    mix[-close:] *= np.cos(np.linspace(0.0, np.pi / 2.0, close)) ** 2
    top = float(np.max(np.abs(mix)))
    if top > 0.95:
        mix *= 0.95 / top
    return mix


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=OUT_DEFAULT)
    args = parser.parse_args()
    mix = build()
    write_wav(args.out, mix)
    print("%s  %.2fs  peak %.1f dBFS" % (args.out, len(mix) / RATE, 20 * np.log10(np.max(np.abs(mix)))))


if __name__ == "__main__":
    sys.exit(main())
