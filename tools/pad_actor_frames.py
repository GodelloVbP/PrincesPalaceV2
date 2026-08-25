"""Delivers a hand-cropped stance kit to Resources on ONE canvas per actor.

WHY THIS EXISTS ALONGSIDE slice_actor_sheet.py, which is the tool for this
job. That one cuts an actor's stances out of its source sheets and is right
whenever the sheets are the truth. Beetle and Treant arrived already keyed
and already cut, as Art/Enemies/<actor>/<stance>_frames/f0..fN, by a process
that survives nowhere -- and the Beetle's idle animation matches no committed
sheet at all, so re-slicing from what exists would deliver a still idle and
lose six frames of breathing. The frames are good. What is wrong with them is
the shape of their canvases.

EVERY FRAME IS CROPPED TO ITS OWN CONTENT, 435x314 to 512x470 within one
actor. That matters because of one line in FightController.StageVisuals:

    slotRect.sizeDelta = sprite.rect.size

The slot IS the sprite's canvas, its pivot is bottom-centre, and
GroundTheFigure nudges it down by ONE authored number for the whole actor --
StanceManifest's groundLine, "how far up its own canvas this creature's feet
are". Per-frame crops make that number a different lie in every frame: the
feet sit wherever the crop left them, so the creature bobs and slides as it
animates, by an amount nobody chose. The Forest Troll shipped this way and
its manifest entry admits it: "a small vertical pop between stances is
possible and worth an eyeball pass".

THE OFFSETS ARE RECOVERABLE, WHICH IS THE WHOLE TRICK. A crop taken from a
sheet cell has lost where it sat -- but the sheets are committed beside the
crops, every one a 1536x1024 grid of 512x512 cells, and a frame's dimensions
match its own cell's alpha bounds exactly. Treant's thirty-six frames all
match at 1.000; most of Beetle's match to within a pixel or two. So for those
frames this recovers the true position rather than inventing one, and the
result is the cell grid the artist actually drew: relative motion intact, a
lunge that lunges, feet that stay put.

AND OFFSETS ARE USED WITHIN A STANCE ONLY. Each stance was generated on its
own sheet, and the sheets do not agree with each other -- nobody was holding
the figure to a common position while drawing six separate images. Recovered
offsets used raw left Beetle's sealed shell floating 66px above the floor its
walking pose stands on, and the creature bobbing between stances by up to
108px. So each stance is placed as a unit: sat on the floor, centred on the
axis, with its frames keeping their offsets relative to each other. Motion
inside a stance survives exactly -- a lunge still lunges, a collapse still
collapses -- and drift between stances, which is an artefact of how the art
was commissioned rather than anything anybody chose, does not.

WHAT IS MEASURED WHEN RECOVERY FAILS. Beetle's idle matches nothing
committed, so those six frames are positioned by their own boxes. Within a
stance that is a CHOICE and not a recovery; it is nearly free here because
the six idle poses vary by under 20px in either axis, and it would not be on
a stance that moves.

DELIBERATELY NOT ground-contact alignment, which is what slice_actor_sheet.py
uses and what this tried first. Its reasoning is sound on sheet cells and
wrong on pre-cropped frames: the bottom 8% of an already-tight crop catches
whichever single limb is lowest, so the measured foot line swung 370px across
Treant's frames and 340 across Beetle's -- it was aligning to the claw that
happened to reach furthest down, not to the creature. Cell offsets have no
such problem because they are not a measurement.

Usage:
    py tools/pad_actor_frames.py            # every actor in ACTORS
    py tools/pad_actor_frames.py beetle     # one

Prints the canvas and the ground line per actor. The ground line is what goes
in Resources/StanceManifest.json, and it is one number for every stance by
construction rather than by hope.
"""
import os
import sys

from PIL import Image

from sheet_slicing import opaque_mask

SOURCE_ROOT = "Assets/_Project/Art/Enemies"
OUTPUT_ROOT = "Assets/_Project/Resources/Enemies"

# Air around the figure, so no art is flush with its own canvas edge -- a
# sprite whose content touches the edge picks up clamped-sampling artefacts
# the moment the stage scales it for depth.
PAD = 12

# WHAT COUNTS AS OPAQUE comes from sheet_slicing, not from a number here. It
# had its own ALPHA_FLOOR = 8 against the shared ALPHA_THRESHOLD = 16, which
# meant two tools writing into the same Resources/Enemies/<actor>/ folders
# disagreed about where a figure ends -- the kind of difference that shows up
# as one creature sitting a few pixels off the line the others stand on.

# Both kits' sheets are 1536x1024 grids of six poses.
SHEET_GRID = (2, 3)

ACTORS = {
    # Stance -> folder under Art/Enemies/<actor>/. The runtime name is the
    # KEY: "attack" is the stance the fight asks for, "attack_frames" is
    # merely where the art happens to sit.
    #
    # shell_closed, turtle_up, trunk_slam and cast are not stances the fight
    # drives on its own -- they are reached by authoring them on a skill, the
    # same route the Forest Troll's roar takes to attack_roar. See
    # ContentData/skills.json.
    "beetle": {
        # HOW BIG IT IS, and it has to be authored because nothing else says.
        # FightController.StageVisuals sizes each stage slot to the sprite's
        # own canvas, so a creature's size on screen IS its art's size in
        # pixels -- there is no per-enemy scale in content. Both these kits
        # were generated on the same 512px cells, so delivered raw the beetle
        # came out as tall as the golem and very nearly as tall as the Elder
        # Treant, which is the wrong way round for a bug and a tree.
        #
        # Measured against the roster rather than picked: the delivered idle
        # content heights are rat 286, bog_witch 313, golem 335, forest_warden
        # 473. 0.72 puts the beetle at ~243 -- lower than a rat and much wider,
        # which is what a long armoured insect should read as.
        "delivery_scale": 0.72,
        "stances": {
            "idle": "idle_frames",
            "attack": "attack_frames",
            "turtle_up": "turtle_up_frames",
            "shell_closed": "shell_closed_frames",
            "hurt": "hurt_frames",
            "defeated": "defeated_frames",
        },
    },
    "treant": {
        # ~440 against the Forest Troll's 473: the biggest regular on the
        # roster and still visibly under the floor-1 boss, which is where a
        # boss should stay.
        "delivery_scale": 0.88,
        "stances": {
            "idle": "idle_frames",
            "attack": "attack_frames",
            "trunk_slam": "trunk_slam_frames",
            "cast": "cast_frames",
            "hurt": "hurt_frames",
            "defeated": "defeated_frames",
        },
    },
}


def opaque_box(image):
    """The figure's own bounds, ignoring the feathered nothing around it."""
    return opaque_mask(image).getbbox()


def frame_paths(actor, folder):
    directory = os.path.join(SOURCE_ROOT, actor, folder)
    if not os.path.isdir(directory):
        sys.exit(f"{actor}: no such stance folder '{directory}'")

    names = [n for n in os.listdir(directory) if n.startswith("f") and n.endswith(".png")]
    if not names:
        sys.exit(f"{actor}/{folder}: no f*.png frames")

    return [os.path.join(directory, n) for n in sorted(names, key=lambda n: int(n[1:-4]))]


def sheet_cell_boxes(actor):
    """Every committed sheet cell's content box, in CELL coordinates.

    Cell coordinates rather than sheet coordinates because that is the space
    every pose shares: cell (r, c) of any sheet is the same 512x512 stage the
    artist drew each pose on, so a box measured inside one is directly
    comparable with a box measured inside another.
    """
    rows, cols = SHEET_GRID
    boxes = []

    directory = os.path.join(SOURCE_ROOT, actor)
    for name in sorted(os.listdir(directory)):
        if not (name.startswith("_") and name.endswith("sheet_source.png")):
            continue

        sheet = Image.open(os.path.join(directory, name)).convert("RGBA")
        width, height = sheet.size
        cell_w, cell_h = width // cols, height // rows

        for r in range(rows):
            for c in range(cols):
                cell = sheet.crop((c * cell_w, r * cell_h, (c + 1) * cell_w, (r + 1) * cell_h))
                box = opaque_box(cell)
                if box is not None:
                    boxes.append(box)

    return boxes


# How far a crop's dimensions may differ from a cell's content box and still
# be called the same pose. Two, because the crops were taken with a slightly
# different alpha threshold than this file uses and land a pixel or so out;
# far enough below the gap between distinct poses (tens of pixels) that a
# wrong match is not a near miss.
DIMENSION_SLACK = 2


def locate(frame_box, cell_boxes):
    """Where this crop sat in its cell, or None if no cell claims it."""
    fw = frame_box[2] - frame_box[0]
    fh = frame_box[3] - frame_box[1]

    best = None
    for box in cell_boxes:
        dw = abs((box[2] - box[0]) - fw)
        dh = abs((box[3] - box[1]) - fh)
        if dw <= DIMENSION_SLACK and dh <= DIMENSION_SLACK:
            error = dw + dh
            if best is None or error < best[0]:
                best = (error, box)

    return None if best is None else best[1]


def scaled(image, factor):
    """Resize PREMULTIPLIED, then undo it.

    Straight LANCZOS on non-premultiplied RGBA blends the backdrop's colour out
    of the transparent pixels and into the edge -- both these sheets sit on a
    near-black field, so a naive downscale hangs a dark fringe on every
    silhouette. It is the same failure mud_burst shipped twice (see
    docs/ART_PIPELINE.md, "Drawn borders survive an authored alpha"), and the
    only difference is that there it was a white box and here it would be a
    dark halo nobody would think to blame on a resize.

    "RGBa" IS PIL's PREMULTIPLIED MODE, lower-case a, and converting in and out
    of it is exactly premultiply-resize-unpremultiply. This was twenty-five
    lines of hand-rolled per-pixel division before somebody noticed; the two
    agree to within one part in 255 on every visible pixel and this runs about
    ten times faster.
    """
    if factor == 1.0:
        return image

    size = (max(1, int(round(image.width * factor))),
            max(1, int(round(image.height * factor))))

    return image.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")


def deliver(actor, spec, verbose=True):
    cell_boxes = sheet_cell_boxes(actor)
    factor = spec.get("delivery_scale", 1.0)

    frames = []
    for stance, folder in spec["stances"].items():
        for path in frame_paths(actor, folder):
            image = Image.open(path).convert("RGBA")
            box = opaque_box(image)
            if box is None:
                sys.exit(f"{path} is entirely transparent")

            frames.append({
                "stance": stance,
                "name": os.path.basename(path),
                "image": image,
                "box": box,
                "at": locate(box, cell_boxes),
            })

    if not any(f["at"] is not None for f in frames):
        sys.exit(f"{actor}: no frame matched any committed sheet cell, so there is nothing "
                 f"to recover an offset from")

    # ---- per-stance placement, and the reason it is per stance -----------
    #
    # EACH STANCE WAS GENERATED ON ITS OWN SHEET, and the sheets do not agree
    # with each other. Recovered cell offsets are exactly right WITHIN a
    # sheet -- that is the artist's own frame-to-frame motion -- and mean
    # nothing ACROSS sheets, because nobody was holding the figure to a
    # common position while drawing six separate images. Used raw, Beetle's
    # sealed shell floated 66px above the floor its walking pose stands on
    # and the whole creature bobbed between stances by up to 108px.
    #
    # So: offsets inside a stance, and the stance as a whole moved to sit on
    # the floor and centre on the axis. Intra-stance motion survives intact
    # (a lunge still lunges, a collapse still collapses); inter-stance drift
    # -- which is an artefact of how the art was commissioned, not a
    # decision anybody made -- does not.
    stance_box = {}
    for frame in frames:
        # A recovered cell position where there is one; the frame's own box
        # otherwise, which is the same thing measured in a poorer space.
        box = frame["at"] if frame["at"] is not None else frame["box"]

        # Every position below is in DELIVERED pixels, so the scale is
        # applied once here rather than remembered at each use. PAD is not
        # scaled: it is air in the output canvas, not part of the figure.
        box = tuple(int(round(v * factor)) for v in box)
        frame["place"] = box

        known = stance_box.get(frame["stance"])
        stance_box[frame["stance"]] = box if known is None else (
            min(known[0], box[0]), min(known[1], box[1]),
            max(known[2], box[2]), max(known[3], box[3]))

    # The canvas holds the widest stance and the tallest. Measured over whole
    # STANCES rather than single frames because each stance is centred as a
    # unit -- sizing to the widest frame would leave a stance that reaches
    # further one way than the other hanging off its own canvas.
    width = max(b[2] - b[0] for b in stance_box.values()) + PAD * 2
    height = max(b[3] - b[1] for b in stance_box.values()) + PAD * 2

    out_root = os.path.join(OUTPUT_ROOT, actor)
    guessed = sum(1 for f in frames if f["at"] is None)

    for frame in frames:
        cut = scaled(frame["image"].crop(frame["box"]), factor)
        canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))

        whole = stance_box[frame["stance"]]
        box = frame["place"]

        # HORIZONTALLY from the recovered offset, so the lunge survives: a
        # pose that reaches forward was drawn forward in its cell, and that
        # is the one axis where the cell position is signal.
        x = (width - (whole[2] - whole[0])) // 2 + (box[0] - whole[0])

        # VERTICALLY every frame is sat on the floor, and the cell offset is
        # thrown away. It is noise on this axis and large: the Beetle's six
        # attack cells place it across a 178px band, which is a lunging
        # insect levitating half its own height, not an animation. The
        # Treant's is only 19-33px because a tall figure fills its cell, but
        # it is the same noise and there is no pose in either kit that
        # leaves the ground on purpose -- so nothing true is lost, and
        # groundLine becomes exactly PAD for every frame by construction
        # rather than approximately PAD by measurement.
        #
        # The cost, stated: a kit with a genuine jump would land flat here
        # and wants slice_actor_sheet.py's authored answer instead.
        # From the RESIZED crop rather than the scaled box, so rounding
        # cannot leave a frame a pixel off the floor the others stand on.
        y = height - PAD - cut.height

        canvas.paste(cut, (x, y))

        directory = os.path.join(out_root, frame["stance"])
        os.makedirs(directory, exist_ok=True)
        canvas.save(os.path.join(directory, frame["name"]))

    if verbose:
        print(f"{actor}: {len(frames)} frames -> {out_root}")
        print(f"  canvas {width}x{height}   groundLine {PAD}  "
              f"(StanceManifest.json wants that number)")
        print(f"  {len(frames) - guessed} placed from sheet cells, {guessed} by their own box")
        print(f"  stances: {', '.join(spec['stances'])}")


def main():
    wanted = sys.argv[1:] or list(ACTORS)
    for actor in wanted:
        if actor not in ACTORS:
            sys.exit(f"Unknown actor '{actor}'. Known: {', '.join(sorted(ACTORS))}")
        deliver(actor, ACTORS[actor])


if __name__ == "__main__":
    main()
