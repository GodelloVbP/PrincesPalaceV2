#!/usr/bin/env python3
"""Generate the melee contact effects: a slash arc, an impact burst, two sounds.

These are the house's DEFAULT contact language for a plain swing -- the beat
that authors no spell of its own. Every other effect in the game is delivered
art that a slicer cuts up (see slice_spell_sheet.py, which this deliberately
does not duplicate: there is no sheet to cut here and nothing to key). These
are drawn from arithmetic instead, for one reason: a slash arc is a shape with
no character in it, and commissioning one costs a round trip for something a
polar-coordinate falloff describes exactly.

## What comes out

    Assets/_Project/Resources/Vfx/slash_arc/f0..f5.png       256x256 RGBA
    Assets/_Project/Resources/Vfx/impact_burst/f0..f5.png    256x256 RGBA
    Assets/_Project/Resources/Audio/Sfx/melee_whoosh_placeholder.wav
    Assets/_Project/Resources/Audio/Sfx/melee_thud_placeholder.wav

The f0..fN folder layout is FrameSequenceLoader's, shared with every stance
sheet and every spell. Six frames because that is what the rest of the game
plays and what the dissolve in SpellVfxPlayer is tuned against.

## Conventions the runtime depends on

* ALPHA IS THE LUMINANCE FALLOFF, exactly as slice_spell_sheet.py synthesises
  it for delivered art. A glow has no clean edge, so a hard cutout leaves a
  visible rectangle around it.

* THE IMPACT SITS AT THE EXACT CENTRE OF THE FRAME, on both axes, in every
  frame of both sequences. FightController.PlayContactFx aims the box centre
  at the target's content centre and applies no impact-point correction, so a
  sequence whose bright part drifted off centre would land off the body.

* THE ARC IS DRAWN SWEEPING LEFT TO RIGHT. A monster's blow mirrors the whole
  sheet (SpellVfxPlayer.SetFacing), which only works while the sheet has a
  direction to mirror.

## The audio is a PLACEHOLDER and says so in its filename

Two synthesised one-shots, because nothing in Resources/Audio/Sfx fits: it
holds button clicks, Shawn's voice takes and two spell clips. Synthesis is not
a claim that these are good; it is a claim that a silent hit is worse than a
provisional one, and that the timing work should not be blocked on a
recording. Replace them and delete the "_placeholder" from the two paths in
Core/ContactCues.cs.

Usage:
    python tools/make_contact_fx.py            # the twelve PNGs
    python tools/make_contact_fx.py --audio    # the two WAVs as well
    python tools/make_contact_fx.py --audio-only
"""

import argparse
import math
import os
import sys
import wave

try:
    import numpy as np
except ImportError:
    sys.exit("numpy is required: pip install numpy")

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")


VFX_ROOT = "Assets/_Project/Resources/Vfx"
AUDIO_ROOT = "Assets/_Project/Resources/Audio/Sfx"

# 256 square. Big enough that the arc's thin tapered ends survive being fitted
# into a ~200px box on screen, small enough that twelve of them are a rounding
# error next to one stance sheet.
SIZE = 256

# Six, matching every other sequence in the game -- see the module docstring.
FRAMES = 6

SAMPLE_RATE = 44100


# ---------------------------------------------------------------------------
# shared helpers


def polar():
    """Radius (0..1 at the frame edge) and angle for every pixel, centred.

    Centred on the exact middle of the frame, which is the contract the
    runtime placement depends on.
    """
    axis = (np.arange(SIZE) + 0.5) / SIZE * 2.0 - 1.0
    y, x = np.meshgrid(axis, axis, indexing="ij")
    # Screen y grows downward; flip it so positive angles read anticlockwise
    # the way the trigonometry below assumes.
    return np.hypot(x, y), np.arctan2(-y, x)


def write_rgba(path, rgb, alpha):
    """Write one frame. RGB is 0..1 per channel, alpha 0..1."""
    a = np.clip(alpha, 0.0, 1.0)
    out = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    for c in range(3):
        out[:, :, c] = np.clip(rgb[c], 0.0, 1.0) * 255.0
    out[:, :, 3] = a * 255.0
    Image.fromarray(out, "RGBA").save(path)


def ensure(path):
    os.makedirs(path, exist_ok=True)
    return path


def angular_distance(theta, centre):
    """Shortest signed angle from `centre` to `theta`, in radians."""
    d = theta - centre
    return (d + math.pi) % (2.0 * math.pi) - math.pi


# ---------------------------------------------------------------------------
# the slash arc


# The full sweep at its widest, in degrees.
ARC_DEGREES = 150.0

# The blade's reach: the distance from the swing's pivot to the cutting edge,
# in half-frame units. The pivot itself is placed this far to the LEFT of the
# frame centre so that the middle of the arc passes through the centre exactly
# -- see slash_frame. At the widest sweep the tapered ends reach x = -0.56 and
# y = +-0.72 in half-frame units, so the whole crescent stays clear of the
# frame edge (a shape cut square by the edge reads as a rendering fault).
ARC_RADIUS = 0.75


def slash_frame(i):
    """One frame of the arc: a tapered crescent that opens, then thins away."""
    t = i / (FRAMES - 1.0)

    # THE PIVOT IS OFF-FRAME TO THE LEFT, and this is the placement contract.
    # A crescent drawn about the frame's own centre puts its bright part off
    # to one side, and FightController.PlayContactFx aims the box CENTRE at
    # the body -- so the cut would land beside the target rather than on it.
    # Curving about (-ARC_RADIUS, 0) puts the middle of the sweep on (0, 0) in
    # every frame, whatever the sweep is doing.
    axis = (np.arange(SIZE) + 0.5) / SIZE * 2.0 - 1.0
    y, x = np.meshgrid(axis, axis, indexing="ij")
    dx = x + ARC_RADIUS
    dist = np.hypot(dx, y)
    theta = np.arctan2(-y, dx)

    # THE SWEEP OPENS AND THE BLADE THINS, which is the whole read. A crescent
    # that simply faded would look like a light turning off; one that extends
    # while narrowing looks like something passing through.
    span = math.radians(ARC_DEGREES) * 0.5 * (0.30 + 0.70 * min(1.0, t * 1.35))
    thickness = 0.105 * (1.0 - 0.60 * t) + 0.012

    signed = np.clip(theta / max(span, 1e-4), -1.0, 1.0)
    along = np.abs(signed)

    # Taper: nothing at either end of the sweep, so it reads as a blade path
    # rather than a ring segment.
    #
    # ASYMMETRIC, and that is what stops it reading as a crescent moon. A
    # smear left by a blade is heavy where the edge is NOW and thin where it
    # has already been, so one end has to carry more weight than the other.
    # A symmetric taper draws the same shape both ways round and the eye reads
    # it as a lit shape rather than as something moving.
    lead = 0.52 + 0.48 * (0.5 - signed * 0.5)
    taper = np.clip(1.0 - along ** 3.0, 0.0, 1.0) * lead

    band = np.abs(dist - ARC_RADIUS) / np.maximum(thickness * taper, 1e-4)

    # Gaussian across the band rather than a hard edge: this IS the soft
    # falloff the alpha is taken from.
    body = np.exp(-(band ** 2) * 1.5)
    body = np.where(along >= 1.0, 0.0, body)

    # A near-white core bleeding out into the swing's own colour. Warm rather
    # than neutral so the arc separates from the white hit flash firing on the
    # same instant.
    hot = np.clip(body ** 2.0, 0.0, 1.0)
    rgb = (
        np.clip(0.86 + 0.14 * hot, 0.0, 1.0),
        np.clip(0.70 + 0.30 * hot, 0.0, 1.0),
        np.clip(0.52 + 0.46 * hot, 0.0, 1.0),
    )

    # Fades over the back half only: the sweep has to be at full strength
    # while it is still crossing, or the eye never catches the direction.
    fade = 1.0 if t < 0.45 else 1.0 - 0.88 * (t - 0.45) / 0.55
    return rgb, np.clip(body * 1.6, 0.0, 1.0) * fade


# ---------------------------------------------------------------------------
# the impact burst


# Six shards. Enough to read as fragmentation, few enough that each one is
# still an individual line at the size this is drawn on screen.
SHARDS = 6

# Where the shards point. Deliberately not evenly spaced -- a perfect star
# reads as a sparkle asset, and an uneven one reads as debris.
SHARD_DEGREES = [18.0, 74.0, 137.0, 196.0, 259.0, 316.0]


# How far the outermost shard tip reaches at the last frame, in half-frame
# units. Under 1.0 on purpose: a shard that runs off the edge of the frame is
# cut square, and a square end is the one thing that reads as a rendering
# artefact rather than as debris.
BURST_REACH = 0.80

# Shard thickness, in half-frame units and CONSTANT along the streak. Held in
# distance rather than in degrees because a fixed angular width draws a wedge
# -- a triangle fanning out from the middle, which is what the first pass drew
# and why it read as six blobs rather than six shards.
SHARD_HALF_WIDTH = 0.020


def burst_frame(i):
    """One frame: a hard white core that expands and breaks into thin shards."""
    r, theta = polar()
    t = i / (FRAMES - 1.0)

    # THE CORE IS ONLY THERE AT THE START. A blow's first frame is the flash
    # itself; everything after it is what the flash threw off. Solid to its
    # inner radius rather than Gaussian all the way down, so frame 0 has a
    # hard white centre instead of a soft dot.
    core_radius = 0.10 + 0.13 * t
    core_strength = max(0.0, 1.0 - t * 2.3)
    core = np.clip(1.35 - (r / max(core_radius, 1e-4)) ** 2, 0.0, 1.0) * core_strength

    shards = np.zeros_like(r)
    for k, deg in enumerate(SHARD_DEGREES):
        # Staggered lengths, so they do not all arrive at the rim together.
        tip = BURST_REACH * t * (0.72 + 0.28 * ((k * 7 % 5) / 4.0)) + 0.10
        # The shard is the LEADING part of its own path: it pulls away from
        # the middle as it travels, rather than staying a spoke anchored there.
        inner = tip * (0.30 + 0.42 * t)

        d_ang = np.abs(angular_distance(theta, math.radians(deg)))

        # Perpendicular distance from the shard's axis, to first order. r *
        # d_ang is an arc length, so dividing by a constant gives a streak of
        # constant thickness.
        across = np.exp(-((r * d_ang / SHARD_HALF_WIDTH) ** 2) * 0.9)

        # Along the streak: nothing before `inner`, rising to the tip, gone
        # past it. The bright end leads, which is what gives the burst
        # direction rather than making it a star.
        span = max(tip - inner, 1e-4)
        along = np.clip((r - inner) / span, 0.0, 1.0)
        along = np.where(r > tip, np.clip(1.0 - (r - tip) / 0.05, 0.0, 1.0), along ** 0.6)

        shards = np.maximum(shards, across * along)

    lit = np.clip(core + shards * (1.0 - 0.25 * t), 0.0, 1.0)

    # Hard and white, which is the point: the arc carries the colour, this
    # carries the shock.
    rgb = (
        np.clip(0.92 + 0.08 * lit, 0.0, 1.0),
        np.clip(0.90 + 0.10 * lit, 0.0, 1.0),
        np.clip(0.84 + 0.16 * lit, 0.0, 1.0),
    )

    fade = 1.0 - (t ** 1.9) * 0.90
    return rgb, lit * fade


# ---------------------------------------------------------------------------
# a worn form's own contact burst
#
# THE SAME ARITHMETIC, A DIFFERENT DELIVERABLE. slash_arc and impact_burst are
# the HOUSE's contact language and belong to no skill, which is why they are
# written straight into Resources/Vfx/ with no recipe (docs/ART_PIPELINE.md
# 5c). A transform's hit cue is authored CONTENT on a skill, so its folder
# lives under Resources/Spells/ and has to have a recorded provenance --
# SpellVfxRecipeDriftTests refuses a played folder no recipe explains.
#
# So this writes a SHEET rather than frames, and slice_spell_sheet.py cuts it
# the way it cuts every delivered sheet:
#
#   python tools/make_contact_fx.py --form-sheet
#   python tools/slice_spell_sheet.py --new black_ram_impact \
#       --sheet vfx_masters/ram_impact_8f.png --rows 2 --cols 4
#
# The recipe then explains the cut and this function explains the sheet, which
# is the whole of the provenance chain for a burst nobody drew.
#
# IT IS A PLACEHOLDER AND SAYS SO. Polar arithmetic can describe a shockwave
# and a scatter; it cannot draw a ram. What this delivers is the WEIGHT --
# bigger, darker and slower than the house burst -- against the day a
# commissioned sheet replaces it, at which point only the recipe changes.

FORM_SHEET_ROOT = "Assets/_Project/Art/Sheets/vfx_masters"

# 320 rather than the house burst's 256, and the cells are cut at that size --
# this is drawn into a bigger box on screen and a sheet cut smaller than it is
# drawn is the one way to lose detail that was never there to lose.
RAM_CELL = 320

# Eight over 4x2. Six is the house count; eight buys the extra two frames the
# tail needs, because the read being sold here is a heavy thing settling
# rather than a light one snapping off.
RAM_COLS = 4
RAM_ROWS = 2
RAM_FRAMES = RAM_COLS * RAM_ROWS

# Bone and char. Deliberately NOT the house burst's near-white: that one is
# the shock itself and has to separate from the white hit flash firing on the
# same instant. This one is the opposite statement -- something heavy and
# unlit went through, and the only bright part is what it threw up.
RAM_BONE = (0.93, 0.89, 0.79)
RAM_CHAR = (0.05, 0.04, 0.06)

# Where the grit goes. Uneven, for the reason SHARD_DEGREES above is uneven.
RAM_GRIT_DEGREES = [24.0, 61.0, 118.0, 152.0, 208.0, 249.0, 297.0, 333.0]


def _ram_polar():
    axis = (np.arange(RAM_CELL) + 0.5) / RAM_CELL * 2.0 - 1.0
    y, x = np.meshgrid(axis, axis, indexing="ij")
    return np.hypot(x, y), np.arctan2(-y, x)


def _edge(field, softness=0.035):
    """A HARD edge with one pixel-ish of ramp on it, not a Gaussian.

    THE HOUSE STYLE IS CEL, and two passes of this sheet were rejected for
    exactly that: everything drawn with a Gaussian falloff came out soft, and
    a soft dark blob with soft pale petals around it reads as a flower or a
    gear rather than as something breaking. `field` is positive inside the
    shape and negative outside; this turns it into 0..1 with a narrow ramp, so
    a shard has a boundary the eye can find.
    """
    return np.clip(field / softness, 0.0, 1.0)


def ram_frame(i):
    """One frame: a black mass punching out through a broken bone shockwave."""
    r, theta = _ram_polar()
    t = i / (RAM_FRAMES - 1.0)

    # THE MASS, and it is the half that makes this read as heavy. A dark blot
    # at its densest on the first frames, still there at two thirds -- the eye
    # reads it as the blow itself rather than as what the blow threw off,
    # which is what the house burst's white core says instead.
    #
    # A SEVEN-POINTED STAR RATHER THAN A DISC. A circle of any size reads as a
    # hole punched in the frame; a lobed one reads as something with sides to
    # it, and the lobes line up with the streaks below so the mass looks like
    # their root rather than like a separate shape they pass through.
    lobed = 0.26 + 0.13 * t + 0.075 * np.cos(7.0 * theta + 0.4)
    mass = _edge(lobed - r, 0.045) * np.clip(1.0 - 0.92 * t, 0.0, 1.0)

    # THE SHOCKWAVE, eased out so it is fastest on the first two frames. A
    # linear ring expands like a diagram; a decelerating one expands like
    # something that was hit.
    #
    # AND IT IS BROKEN, which is the whole difference between this and a smoke
    # ring. A continuous annulus is a hoop -- the first pass drew one and it
    # read as a soap bubble. Gated by a lumpy angular mask so roughly half the
    # circumference is missing at any moment and the surviving arcs are
    # different lengths, the front stops being a shape and starts being
    # debris that happens to be travelling together.
    front = 0.14 + 0.66 * (1.0 - (1.0 - t) ** 2.0)

    # THE SHARDS, and there is no ring at all any more. Two passes drew one --
    # a continuous annulus, then a broken one -- and both read as a smoke ring
    # because a circle is a circle however it is gated. What a heavy blow
    # actually leaves is a handful of hard wedges of different lengths thrown
    # out of the same point, and that is what these are: a straight-sided
    # tapered wedge each, hard-edged, longest where the blow was going.
    #
    # BIASED ALONG THE BLOW. `RAM_GRIT_DEGREES` are the directions; the length
    # multiplier below is largest near 0 degrees and smallest behind, so the
    # spray leans the way the strike travelled instead of being a symmetrical
    # star. SpellLayer.facing mirrors the whole sheet for a blow coming the
    # other way, which is the same contract slash_arc relies on.
    shards = np.zeros_like(r)
    for k, deg in enumerate(RAM_GRIT_DEGREES):
        lean = 0.62 + 0.38 * math.cos(math.radians(deg))
        reach = front * lean * (0.80 + 0.40 * ((k * 5 % 7) / 6.0)) + 0.07
        inner = reach * (0.26 + 0.34 * t)
        half = 0.075 + 0.045 * ((k * 3 % 4) / 3.0)

        d_ang = np.abs(angular_distance(theta, math.radians(deg)))

        # A WEDGE, not a streak: the half-width tapers to nothing at the tip,
        # so the shape has two straight sides meeting at a point.
        along = np.clip((r - inner) / max(reach - inner, 1e-4), 0.0, 1.0)
        width = half * (1.0 - 0.85 * along)
        inside = np.minimum(width - r * d_ang, np.minimum(r - inner, reach - r))

        shards = np.maximum(shards, _edge(inside, 0.030))

    # THE TWO HORNS, kept because they are the one gesture in here that is
    # about a ram rather than about an impact: two short heavy arcs low and
    # wide, inside the shard field rather than out past it.
    horns = np.zeros_like(r)
    for deg in (150.0, 210.0):
        d_ang = np.abs(angular_distance(theta, math.radians(deg)))
        band = np.minimum(0.055 - np.abs(r - front * 0.58), math.radians(26.0) - d_ang)
        horns = np.maximum(horns, _edge(band, 0.030) * (1.0 - 0.6 * t))

    lit = np.clip(shards + horns, 0.0, 1.0)

    # ALPHA IS AUTHORED HERE, NOT TAKEN FROM LUMINANCE, and that is the one
    # convention this pack breaks on purpose. The house rule (2 in
    # ART_PIPELINE) synthesises alpha from brightness because a glow has no
    # clean edge -- but the darkest part of this sheet is its most solid, and
    # a luminance alpha would make the mass the most transparent thing in the
    # frame. The recipe therefore cuts it with `keyed: false`.
    density = np.clip(mass * 1.05 + lit, 0.0, 1.0)

    # A slower tail than the house burst's, matching the eight frames.
    fade = 1.0 - (t ** 2.1) * 0.88
    alpha = density * fade

    # Bone where the light is, char where the mass is, mixed by which of the
    # two owns the pixel.
    weight = lit / np.maximum(lit + mass, 1e-4)
    rgb = tuple(
        RAM_BONE[c] * weight + RAM_CHAR[c] * (1.0 - weight)
        for c in range(3)
    )

    return rgb, alpha


def build_form_sheet():
    """The eight frames laid out 4x2 on one sheet, for the slicer to cut."""
    ensure(FORM_SHEET_ROOT)

    sheet = Image.new("RGBA", (RAM_CELL * RAM_COLS, RAM_CELL * RAM_ROWS), (0, 0, 0, 0))

    for i in range(RAM_FRAMES):
        rgb, alpha = ram_frame(i)
        cell = np.zeros((RAM_CELL, RAM_CELL, 4), dtype=np.uint8)
        for c in range(3):
            cell[:, :, c] = np.clip(rgb[c], 0.0, 1.0) * 255.0
        cell[:, :, 3] = np.clip(alpha, 0.0, 1.0) * 255.0
        sheet.paste(Image.fromarray(cell, "RGBA"),
                    ((i % RAM_COLS) * RAM_CELL, (i // RAM_COLS) * RAM_CELL))

    path = os.path.join(FORM_SHEET_ROOT, "ram_impact_8f.png")
    sheet.save(path)
    print("  " + path.replace("\\", "/"))
    print("  now: python tools/slice_spell_sheet.py --new black_ram_impact "
          "--sheet vfx_masters/ram_impact_8f.png --rows 2 --cols 4")


# ---------------------------------------------------------------------------
# the placeholder audio


def write_wav(path, samples):
    """44.1 kHz, 16-bit, mono, through the stdlib. No encoder dependency."""
    clipped = np.clip(samples, -1.0, 1.0)
    pcm = (clipped * 32767.0).astype(np.int16)

    with wave.open(path, "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(pcm.tobytes())


def whoosh(seconds=0.12):
    """Band-passed noise that swells and dies -- air moving, not a tone.

    Trimmed at the head by construction (the envelope starts at zero and
    rises within a couple of milliseconds), because a one-shot with lead-in
    reads as input lag and no code can take it back out -- Resources/Audio/
    README.md's own rule.
    """
    n = int(SAMPLE_RATE * seconds)
    t = np.arange(n) / SAMPLE_RATE
    rng = np.random.default_rng(20260904)
    noise = rng.standard_normal(n)

    # A one-pole pair standing in for a band-pass: low-passed twice to kill
    # the hiss, then the DC-ish part subtracted back out to open the bottom.
    # Cheap, and the exact shape does not matter for a placeholder.
    low = np.zeros(n)
    lower = np.zeros(n)
    a_hi, a_lo = 0.34, 0.06
    for i in range(1, n):
        low[i] = low[i - 1] + a_hi * (noise[i] - low[i - 1])
        lower[i] = lower[i - 1] + a_lo * (low[i] - lower[i - 1])
    band = low - lower

    # Swell into the strike: slow rise, fast decay, so the loudest moment is
    # at the END of the clip and lands on the blow rather than before it.
    p = t / seconds
    envelope = (p ** 1.8) * np.exp(-((p - 1.0) ** 2) * 3.0)
    envelope /= max(envelope.max(), 1e-9)

    return band / max(np.abs(band).max(), 1e-9) * envelope * 0.7


def thud(seconds=0.15):
    """A low sine thump with a click transient on the front."""
    n = int(SAMPLE_RATE * seconds)
    t = np.arange(n) / SAMPLE_RATE

    # A falling pitch is what makes a sine read as an impact rather than a
    # note: 150Hz down to 55Hz over the clip.
    freq = 150.0 * np.exp(-t * 7.0) + 55.0
    phase = 2.0 * np.pi * np.cumsum(freq) / SAMPLE_RATE
    body = np.sin(phase) * np.exp(-t * 22.0)

    # THE CLICK IS WHAT SELLS THE CONTACT. A pure low thump has no leading
    # edge, so at combat pace it arrives as a rumble with no moment in it.
    click_n = int(SAMPLE_RATE * 0.006)
    rng = np.random.default_rng(20260905)
    click = np.zeros(n)
    click[:click_n] = rng.standard_normal(click_n) * np.exp(
        -np.arange(click_n) / (click_n / 3.0))

    mixed = body * 0.85 + click * 0.35
    return mixed / max(np.abs(mixed).max(), 1e-9) * 0.85


# ---------------------------------------------------------------------------


def build_sequence(name, frame_fn):
    folder = ensure(os.path.join(VFX_ROOT, name))
    for i in range(FRAMES):
        rgb, alpha = frame_fn(i)
        path = os.path.join(folder, "f{0}.png".format(i))
        write_rgba(path, rgb, alpha)
        print("  " + path.replace("\\", "/"))


def build_audio():
    ensure(AUDIO_ROOT)
    for name, samples in (
        ("melee_whoosh_placeholder.wav", whoosh()),
        ("melee_thud_placeholder.wav", thud()),
    ):
        path = os.path.join(AUDIO_ROOT, name)
        write_wav(path, samples)
        print("  " + path.replace("\\", "/"))


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--audio", action="store_true",
                        help="also write the two placeholder WAVs")
    parser.add_argument("--audio-only", action="store_true",
                        help="write the WAVs and nothing else")
    parser.add_argument("--form-sheet", action="store_true",
                        help="write the worn-form contact SHEET and nothing else; "
                             "slice_spell_sheet.py cuts it (see the section header)")
    args = parser.parse_args()

    if not os.path.isdir("Assets/_Project"):
        sys.exit("run this from the project root (Assets/_Project not found)")

    if args.form_sheet:
        print("ram_impact sheet:")
        build_form_sheet()
        return

    if not args.audio_only:
        print("slash_arc:")
        build_sequence("slash_arc", slash_frame)
        print("impact_burst:")
        build_sequence("impact_burst", burst_frame)

    if args.audio or args.audio_only:
        print("audio (PLACEHOLDERS):")
        build_audio()
        print("\nnow re-run tools/measure_audio_levels.py so the gain table "
              "covers the new clips.")


if __name__ == "__main__":
    main()
