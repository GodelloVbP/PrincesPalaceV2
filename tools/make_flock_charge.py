#!/usr/bin/env python3
"""Generate Toll of the Flock's sfx clip, flock_charge.wav: a muffled bell strike
at 0.56 s under a rising noise rush.

Usage, from the project root:
    python tools/make_flock_charge.py              # write the wav
    python tools/make_flock_charge.py --out X.wav

Then re-run tools/measure_audio_levels.py so audio_levels.json covers it.

The strike lands at STRIKE_AT because relics.json toll_of_the_flock fires its
hit cue at hitCueSeconds 0.55; moving one moves the other.

Pure synthesis, deterministic (fixed seed), so a rerun is byte-identical.
The bell voice is the same one the Bell in the Fog event's toll uses; it was
split out of output/event_art/generate_bell_event_audio.py, which is untracked.
"""

import argparse
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT_DEFAULT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio", "Sfx", "Events",
                           "bell_in_the_fog", "flock_charge.wav")
RATE = 44100
SECONDS = 5.5
STRIKE_AT = 0.56
PEAK = 0.90


def one_pole(signal, coefficient):
    out = np.empty_like(signal)
    out[0] = signal[0]
    for index in range(1, len(signal)):
        out[index] = out[index - 1] + coefficient * (signal[index] - out[index - 1])
    return out


def fade_edges(samples, attack=0.008, release=0.035):
    a = min(len(samples), int(RATE * attack))
    r = min(len(samples), int(RATE * release))
    samples[:a] *= np.sin(np.linspace(0.0, np.pi / 2.0, a)) ** 2
    samples[-r:] *= np.cos(np.linspace(0.0, np.pi / 2.0, r)) ** 2
    return samples


def bell_voice(seconds, strike_at=0.115, gain=1.0):
    """Heavy brass with an intentionally delayed, soft-edged attack."""
    n = int(RATE * seconds)
    t = np.arange(n, dtype=np.float64) / RATE
    age = np.maximum(0.0, t - strike_at)
    active = (t >= strike_at).astype(np.float64)
    attack = np.clip(age / 0.055, 0.0, 1.0) ** 1.6
    body = np.zeros(n)
    for frequency, amplitude, decay in (
        (82.0, 1.00, 0.62), (119.0, 0.70, 0.76), (191.0, 0.48, 1.05),
        (283.0, 0.32, 1.35), (421.0, 0.19, 1.75), (617.0, 0.09, 2.35),
    ):
        wobble = 0.0025 * np.sin(2.0 * np.pi * 0.61 * age)
        body += amplitude * np.sin(2.0 * np.pi * frequency * age * (1.0 + wobble)) * np.exp(-age * decay)
    noise = one_pole(np.random.default_rng(2026092801).standard_normal(n), 0.075)
    mixed = (body + 0.12 * noise * np.exp(-age * 24.0)) * active * attack
    return one_pole(one_pole(mixed, 0.19), 0.19) * gain


def flock_charge():
    """The muffled toll over a rush that swells for 3 s and is gone by 5.5 s."""
    n = int(RATE * SECONDS)
    out = bell_voice(SECONDS, strike_at=STRIKE_AT, gain=0.31)
    t = np.arange(n) / RATE
    rush = one_pole(np.random.default_rng(2026092803).standard_normal(n), 0.055)
    envelope = np.clip((t - 0.2) / 2.8, 0.0, 1.0) * np.clip((5.2 - t) / 0.8, 0.0, 1.0)
    return fade_edges(out + rush * envelope * 0.055, 0.015, 0.16)


def master(samples, peak):
    maximum = float(np.max(np.abs(samples)))
    if maximum <= 1e-12:
        raise ValueError("cannot master silent audio")
    return samples / maximum * peak


def write_wav(path, samples):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    pcm = np.rint(np.clip(samples, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(pcm.tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", default=OUT_DEFAULT)
    args = parser.parse_args()
    write_wav(args.out, master(flock_charge(), PEAK))
    print("%s  %.2fs" % (args.out, SECONDS))


if __name__ == "__main__":
    sys.exit(main())
