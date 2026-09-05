#!/usr/bin/env python3
"""Synthesise Cinderfault's two PLACEHOLDER cues.

    Assets/_Project/Resources/Audio/Sfx/cinderfault_pressure.wav
    Assets/_Project/Resources/Audio/Sfx/cinderfault_impact.wav

WHY A SECOND AUDIO TOOL rather than two more functions in make_contact_fx.py.

That tool is named for what it builds: the house's own contact language, the
slash arc and the impact burst that punctuate a swing with no authored art. Its
two WAVs belong to those two effects and are addressed through ContactCues
beside them. Cinderfault's cues belong to one spell and are addressed through
that spell's own presentation in skills.json. Folding them in would have made
"contact fx" mean "and also whatever else needed a placeholder", which is how a
file stops being findable by its name.

The synthesis idiom IS copied deliberately -- numpy plus the stdlib `wave`
module, 44.1 kHz 16-bit mono, an envelope that starts at zero so there is no
lead-in. See make_contact_fx.py's `whoosh` for the reasoning on that last one:
a one-shot with lead-in reads as input lag and no code can take it back out.

THE TWO CUES ARE ONE PHRASE, and their lengths are the contract rather than a
preference. The spell runs for CinderfaultSeconds and ruptures at frame
RuptureFrame of FrameCount, so everything before the rupture is pressure and
everything after is a cooling tail. The pressure cue is cut to end just short of
the rupture instant; the impact cue fits inside the tail. Both numbers are
derived from the spell's own timing below rather than typed twice.

BOTH ARE PLACEHOLDERS and are meant to be replaced by recordings. Recorded as
such in Resources/Audio/README.md.

Usage:
    python tools/make_cinderfault_audio.py

Then re-run tools/measure_audio_levels.py so the gain table covers them.
"""

import os
import sys
import wave

try:
    import numpy as np
except ImportError:
    sys.exit("numpy is required: pip install numpy")

SAMPLE_RATE = 44100
AUDIO_ROOT = "Assets/_Project/Resources/Audio/Sfx"

# THE SPELL'S OWN TIMING, restated here because Python cannot read a C# const
# and skills.json is the authority for neither -- these three ARE what
# skills.json authors, and CinderfaultSpellTests pins the C# side against the
# same three numbers. Change one and this comment is how you find the other two.
#
#   seconds       0.78   how long the whole sequence runs
#   impactFrame   5      1-based, the rupture
#   frames        9      composed by slice_spell_sheet.py, both layers
CINDERFAULT_SECONDS = 0.78
RUPTURE_FRAME = 5
FRAME_COUNT = 9

# When the blow lands, in seconds from the top of the beat. The same arithmetic
# CombatBeat.ImpactFraction does: frame 5 of 9 means five ninths of the way in.
RUPTURE_AT = CINDERFAULT_SECONDS * RUPTURE_FRAME / FRAME_COUNT

# The pressure stops SHORT of the rupture rather than running into it, so the
# transient arrives out of a gap instead of on top of a rumble that is still
# going. 40ms is about the shortest silence that reads as one at combat pace.
PRESSURE_TAIL_GAP = 0.04

PRESSURE_SECONDS = round(RUPTURE_AT - PRESSURE_TAIL_GAP, 3)

# The cooling tail is what the impact cue has to fit inside, so it does not
# still be sounding when the next actor moves.
COOLING_SECONDS = CINDERFAULT_SECONDS - RUPTURE_AT
IMPACT_SECONDS = round(COOLING_SECONDS, 3)


def write_wav(path, samples):
    """44.1 kHz, 16-bit, mono, through the stdlib. No encoder dependency."""
    clipped = np.clip(samples, -1.0, 1.0)
    pcm = (clipped * 32767.0).astype(np.int16)

    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(pcm.tobytes())


def one_pole(signal, coefficient):
    """A single-pole low-pass, written out because scipy is not a dependency."""
    out = np.zeros_like(signal)
    for i in range(1, len(signal)):
        out[i] = out[i - 1] + coefficient * (signal[i] - out[i - 1])
    return out


def pressure(seconds=PRESSURE_SECONDS):
    """Rock under strain: a sub tone climbing, with grit rising over it.

    THREE PARTS, and the climb is the one that carries the meaning. A rumble at
    a fixed pitch is ambience -- it says a thing is happening and not that it is
    about to stop. A sub sliding UP says something is being loaded, which is the
    whole job of a cue that has to make the player expect a rupture that has not
    happened yet.

    No transient anywhere in it: every attack here is slow on purpose, so the
    only sharp edge in the whole spell is the rupture.
    """
    n = int(SAMPLE_RATE * seconds)
    t = np.arange(n) / SAMPLE_RATE
    p = t / seconds

    # 36Hz to 66Hz. Deep enough to read as mass rather than as a note, and the
    # top end stays under the fundamental of the impact's own boom so the two
    # do not sound like the same object.
    freq = 36.0 + 30.0 * (p ** 1.5)
    sub = np.sin(2.0 * np.pi * np.cumsum(freq) / SAMPLE_RATE)

    # The second harmonic, quiet, so small speakers that cannot reproduce 36Hz
    # still hear something move.
    upper = np.sin(2.0 * np.pi * np.cumsum(freq * 2.0) / SAMPLE_RATE) * 0.30

    # Grit: low-passed noise, gated so it only appears in the second half. Rock
    # does not start grinding at the same instant it starts being pushed.
    rng = np.random.default_rng(20260905)
    grit = one_pole(one_pole(rng.standard_normal(n), 0.22), 0.22)
    grit /= max(np.abs(grit).max(), 1e-9)
    grit *= np.clip((p - 0.35) / 0.65, 0.0, 1.0) ** 2 * 0.45

    # Rises to a hard stop rather than fading: the silence before the rupture is
    # doing work, and a cue that decays first spends that silence sounding like
    # it finished on its own.
    envelope = p ** 1.25
    envelope[-int(SAMPLE_RATE * 0.008):] *= np.linspace(1.0, 0.0, int(SAMPLE_RATE * 0.008))

    mixed = (sub + upper + grit) * envelope
    return mixed / max(np.abs(mixed).max(), 1e-9) * 0.72


def impact(seconds=IMPACT_SECONDS):
    """One rupture: a crack, a boom under it, and gravel falling out of it.

    THE CRACK IS THE PART THAT HAS TO BE FIRST. Basalt splitting is a
    high-frequency event and the boom is the room answering it -- inverted, the
    two read as a drum hit rather than as stone breaking.
    """
    n = int(SAMPLE_RATE * seconds)
    t = np.arange(n) / SAMPLE_RATE
    rng = np.random.default_rng(20260906)

    # THE CRACK. Broadband noise with a 6ms decay, kept bright: the difference
    # between "stone splits" and "something is dropped" is entirely up here.
    crack_n = int(SAMPLE_RATE * 0.012)
    crack = np.zeros(n)
    raw = rng.standard_normal(crack_n)
    crack[:crack_n] = raw * np.exp(-np.arange(crack_n) / (crack_n / 5.0))

    # THE BOOM. 120Hz falling to 34Hz -- a falling pitch is what makes a sine
    # read as an impact rather than as a note, the same rule make_contact_fx's
    # `thud` records.
    freq = 120.0 * np.exp(-t * 9.0) + 34.0
    boom = np.sin(2.0 * np.pi * np.cumsum(freq) / SAMPLE_RATE) * np.exp(-t * 11.0)

    # THE GRAVEL. Band-ish noise that arrives just after the crack and rattles
    # out over the cooling tail, so the cue ENDS by describing debris rather
    # than by fading.
    rubble = one_pole(rng.standard_normal(n), 0.55)
    rubble -= one_pole(rubble, 0.05)
    rubble /= max(np.abs(rubble).max(), 1e-9)
    arrival = np.clip((t - 0.02) / 0.05, 0.0, 1.0)
    rubble *= arrival * np.exp(-t * 6.5) * 0.40

    mixed = crack * 0.55 + boom * 0.85 + rubble
    return mixed / max(np.abs(mixed).max(), 1e-9) * 0.90


def main():
    if not os.path.isdir("Assets/_Project"):
        sys.exit("run this from the project root (Assets/_Project not found)")

    os.makedirs(AUDIO_ROOT, exist_ok=True)

    print("cinderfault audio (PLACEHOLDERS):")
    print("  rupture lands %.3fs into a %.2fs cast" % (RUPTURE_AT, CINDERFAULT_SECONDS))

    for name, samples in (
        ("cinderfault_pressure.wav", pressure()),
        ("cinderfault_impact.wav", impact()),
    ):
        path = os.path.join(AUDIO_ROOT, name)
        write_wav(path, samples)
        print("  %s  %.3fs" % (path.replace("\\", "/"), len(samples) / SAMPLE_RATE))

    print("\nnow re-run tools/measure_audio_levels.py so the gain table "
          "covers the new clips.")


if __name__ == "__main__":
    main()
