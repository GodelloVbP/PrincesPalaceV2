#!/usr/bin/env python3
"""Cut spell impact (`sfxPath`) clips so the transient sits at the clip's start.

Usage, from the project root:
    python tools/make_impact_cues.py            # write the wavs, print the table
    python tools/make_impact_cues.py --dry-run

Then re-run tools/measure_audio_levels.py so the gain table covers them.

An impact cue fires on the payoff frame (FightBeatPlayer, hitCueSeconds), so a
clip whose loudest moment comes later than its first few ms lands late by that
much: at 60 fps, 0.2 s is 12 frames. Each cue starts PREROLL before the loudest
sample found in the first SEARCH seconds of the source, with a short
fade-in as a click guard and a longer fade-out.

Cast risers, whose peak belongs at the END of the clip, are cut by
tools/make_spell_cast_cues.py instead. Shares its loader and writer.
"""

import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_spell_cast_cues import OUT_ROOT, RATE, find_source, load_mono_44k, write_wav  # noqa: E402

SEARCH = 0.5     # the strike is in the source's first half second
PREROLL = 0.010
FADE_IN = 0.006
FADE_OUT = 0.15

# (output name, pack, source file, length)
CUES = [
    ("thorn_tithe_impact", "Earth", "earth_spell_cast_morph_rocks_02.wav", 0.90),
    ("borrowed_moment_impact", "Wind", "wind_spell_cast_air_short_spacious_whoosh_04.wav", 0.60),
    ("gilded_aegis_impact", "Holy", "Holy Spell Cast Blast 04 HQ.wav", 0.90),
]


def strike(x):
    """Time of the loudest sample in the first SEARCH seconds."""
    return int(np.argmax(np.abs(x[: int(SEARCH * RATE)]))) / RATE


def cut(x, length):
    t = strike(x)
    start = max(int(round((t - PREROLL) * RATE)), 0)
    piece = x[start:start + int(round(length * RATE))].copy()
    fi = int(FADE_IN * RATE)
    fo = min(int(FADE_OUT * RATE), len(piece))
    piece[:fi] *= np.linspace(0.0, 1.0, fi)
    piece[-fo:] *= np.linspace(1.0, 0.0, fo)
    top = np.abs(piece).max()
    if top > 0.98:
        piece *= 0.98 / top
    return piece, t


def main():
    dry = "--dry-run" in sys.argv
    if not os.path.isdir("Assets/_Project"):
        sys.exit("run this from the project root (Assets/_Project not found)")
    for out, pack, name, length in CUES:
        x = load_mono_44k(find_source(pack, name))
        piece, t = cut(x, length)
        peak = np.abs(piece).argmax() / RATE
        print("%-24s %-52s strike %.3fs  len %.2fs  peak in clip %.3fs" % (
            out, name, t, len(piece) / RATE, peak))
        if not dry:
            write_wav(os.path.join(OUT_ROOT, out + ".wav"), piece)


if __name__ == "__main__":
    main()
