#!/usr/bin/env python3
"""Cut every spell's castSfxPath clip from Leonardo Calvo's Magic Spell SFX pack
(and Cinderfault's from the owner's own recording).

Usage, from the project root:
    python tools/make_spell_cast_cues.py            # write the wavs, print the table
    python tools/make_spell_cast_cues.py --dry-run  # print the table only

Then re-run tools/measure_audio_levels.py so the gain table covers them.

THE WINDOW IS THE RISER'S TAIL, NOT ITS HEAD. A charge builds toward a peak, so
its first half is the quiet half; cutting from sample 0 keeps exactly the part
nobody hears. Each cue is the `length` seconds that END where the clip's build
first reaches -3 dB of its loudest 30 ms (`build_peak`), with a 50 ms fade-in.
That instant is what lands on the blow. The source's own impact, when it has
one, starts there and is left behind: the spell's impact cue carries on.

`length` IS THE SPELL'S hitCueSeconds. A spell is a Hold beat with no windup
pose, so the cast cue fires when the beat opens and the blow lands
hitCueSeconds later (FightBeatPlayer: PlayClip(castSfxPath) ... WaitForSeconds
(ImpactDelayFor(beat))). Change a spell's hitCueSeconds and this table with it;
MeleeSoundOverrideTests.TheSpellCastCuesFitTheirBlows pins the pairing.

A peak earlier than `length` (Crownfall's thunder) is padded with silence at the
head rather than shortened, so the peak still lands on the blow.

Needs numpy and scipy (resampling 48/96 kHz to 44.1 kHz).
"""

import glob
import os
import sys
import wave

import numpy as np
from scipy.io import wavfile
from scipy.signal import resample_poly

SOURCE_ROOT = "C:/Games/Sounds/Prince's Palace/Spells"
OUT_ROOT = "Assets/_Project/Resources/Audio/Sfx"
RATE = 44100

HOP = 0.01          # envelope resolution
GUARD = 0.25        # a hit inside the first 250 ms is a click, not the build
PEAK_WITHIN_DB = 3  # "reached the peak" = within this of the loudest 30 ms
FADE_IN = 0.05
FADE_OUT = 0.008    # a click guard, not a decay: the source's next sound is cut

# (output name, element pack or None for an absolute source, source file, length[, after])
# `after` (default 0) is how many seconds past the build peak the window ends.
CUES = [
    ("winters_rebuke_cast", "Ice", "Ice_Spell_charge_impact_09.wav", 0.34),
    ("vipers_bite_cast", "Acid", "acid_spell_cast_charge_splash_impact_03.wav", 0.40),
    ("spore_cloud_cast", "Acid", "acid_spell_cast_riser_liquid_impact_03.wav", 0.35),
    ("ashen_reckoning_cast", "Acid", "acid_spell_cast_charge_splash_impact_02.wav", 0.60),
    ("censer_of_embers_cast", "Fire", "fire_spell_charge_explosion_wet_06.wav", 1.00),
    ("blackglass_spear_cast", "Dark", "dark_spell_cast_energy_riser_03.wav", 0.70),
    ("death_knell_cast", "Dark", "dark_spell_cast_energy_riser_01.wav", 0.50),
    ("court_of_whispers_cast", "Dark", "dark_spell_cast_energy_ball_01.wav", 0.70),
    ("palace_passage_cast", "Dark", "dark_spell_cast_energy_riser_metallic_02.wav", 0.30),
    ("thorn_tithe_cast", "Earth", "earth_spell_cast_rise_ground_04.wav", 0.30),
    # The only Lightning clip with a rumble, and its crack IS the peak: a window
    # ending at the build peak keeps the rumble and cuts the crack off at its
    # first 10 ms. `after` lets the window run on so the crack sits just before
    # the blow instead of on its last sample.
    ("crownfall_cast", "Lightning", "lighting_spell_electric_impact_thunder_dark_dry_02.wav", 0.34, 0.03),
    ("borrowed_moment_cast", "Wind", "wind_spell_cast_air_short_rise_whoosh_01.wav", 0.30),
    ("velvet_shackles_cast", "Dark", "dark_spell_cast_energy_riser_metallic_01.wav", 0.30),
    ("gilded_aegis_cast", "Holy", "Holy Spell Wave Cast 04 HQ.wav", 0.30),
    ("prismatic_orb_earth_cast", "Earth", "earth_spell_cast_magic_whoosh_02.wav", 0.3467),
    ("prismatic_orb_fire_cast", "Fire", "fire_spell_charge_explosion_wet_03.wav", 0.4167),
    ("prismatic_orb_wind_cast", "Wind", "wind_spell_cast_air_rise_sharp_shoot_04.wav", 0.4167),
    # The owner's recording, not Calvo's. Its build reaches the plateau at ~0.49 s
    # and its loudest 20 ms is at ~1.02 s; the window ends at the former, because
    # what follows is a steady roar with no build left in it.
    ("cinderfault_startup", None, "C:/Games/Sounds/Prince's Palace/Spells/cinderfault_startup.wav", 0.2167),
]


def find_source(pack, name):
    if pack is None:
        return name
    hits = glob.glob("%s/%s Spell Pack/**/%s" % (SOURCE_ROOT, pack, name), recursive=True)
    hits = [h for h in hits if "peaks" not in h.replace("\\", "/") and "/ogg" not in h.replace("\\", "/")]
    if not hits:
        sys.exit("source not found: %s / %s" % (pack, name))
    # The 48 kHz folder where the pack has one, else the 96 kHz one.
    return sorted(hits, key=lambda h: "48" not in h)[0]


def load_mono_44k(path):
    rate, raw = wavfile.read(path)
    scale = {"int16": 32768.0, "int32": 2147483648.0}.get(str(raw.dtype), 1.0)
    data = raw.astype(np.float64) / scale
    if data.ndim > 1:
        data = data.mean(axis=1)
    g = np.gcd(rate, RATE)
    return resample_poly(data, RATE // g, rate // g)


def build_peak(x):
    """Seconds at which the build first reaches the peak, after the click guard."""
    n = int(RATE * HOP)
    k = len(x) // n
    power = (x[: k * n].reshape(k, n) ** 2).mean(axis=1) + 1e-12
    env = np.sqrt(np.convolve(power, np.ones(3) / 3.0, "same"))
    g = int(GUARD / HOP)
    db = 20.0 * np.log10(env / env[g:].max())
    return (g + int(np.argmax(db[g:] >= -PEAK_WITHIN_DB))) * HOP, db


def cut(x, peak, length, after=0.0):
    peak += after
    end = int(round(peak * RATE))
    size = int(round(length * RATE))
    start = end - size
    # Silence pads either side the source cannot fill: the head when the peak is
    # earlier than `length`, the tail when `after` runs past the source's end.
    # The result is always exactly `size` samples.
    piece = x[max(start, 0):min(end, len(x))]
    piece = np.concatenate([np.zeros(max(-start, 0)), piece, np.zeros(max(end - len(x), 0))])
    piece = piece.copy()
    fi = int(FADE_IN * RATE)
    fo = int(FADE_OUT * RATE)
    piece[:fi] *= np.linspace(0.0, 1.0, fi)
    piece[-fo:] *= np.linspace(1.0, 0.0, fo)
    top = np.abs(piece).max()
    if top > 0.98:
        piece *= 0.98 / top
    return piece


def write_wav(path, samples):
    pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).astype(np.int16)
    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(pcm.tobytes())


def main():
    dry = "--dry-run" in sys.argv
    if not os.path.isdir("Assets/_Project"):
        sys.exit("run this from the project root (Assets/_Project not found)")

    print("%-28s %-58s %6s %-15s %s" % ("cue", "source", "peak", "window", "rise dB"))
    for out, pack, name, length, *rest in CUES:
        after = rest[0] if rest else 0.0
        path = find_source(pack, name)
        x = load_mono_44k(path)
        peak, db = build_peak(x)
        stop = peak + after
        a = stop - length
        first = db[min(max(int(round(a / HOP)), 0), len(db) - 1):][:10].mean()
        k = min(int(round(stop / HOP)), len(db))
        last = db[max(k - 10, 0):k].mean() if k > 0 else float("-inf")
        pad = " (+%.3fs silence)" % -a if a < 0 else ""
        tail = len(x) / RATE
        if stop > tail:
            pad += " (+%.3fs tail silence)" % (stop - tail)
        print("%-28s %-58s %6.2f [%5.3f,%5.3f] %+.1f -> %+.1f%s" % (
            out, os.path.basename(path), peak, max(a, 0), stop, first, last, pad))
        if not dry:
            write_wav(os.path.join(OUT_ROOT, out + ".wav"), cut(x, peak, length, after))


if __name__ == "__main__":
    main()
