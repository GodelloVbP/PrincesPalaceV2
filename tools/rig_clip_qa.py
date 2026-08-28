#!/usr/bin/env python3
"""Assembles a rig's own captured clips into things that can actually be
WATCHED -- an animated GIF per stance -- plus a contact strip and an onion
skin, from the real per-frame PNGs RigCaptureTests already renders.

WHY THIS EXISTS. RigCaptureTests + tools/rig_qa.ps1 render every sampled
pose of every clip to tools/screenshots/rigs/<root>/<id>/<stance>/f0..fN.png
-- real SpriteSkin-deformed frames, not a simulation. That answers "did the
bones move" (a still grid answers that fine) but not "does the MOTION read
as breathing or as a drunk sway" -- that is a question about time, and a
grid of stills cannot answer it. This is the missing half: assemble the
same frames into an actual loop.

Deliberately NOT a simulation the way slice_spell_sheet.py's write_preview
is one (see that file's own header for why it has to simulate -- a spell's
frames are never captured from a running fight at all). A rig clip's frames
already ARE the real render; this only sequences them at the real pace.

Usage:
    python tools/rig_clip_qa.py --root tools/screenshots/rigs
    python tools/rig_clip_qa.py --root tools/screenshots/rigs --only rat
"""

import argparse
import json
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip install Pillow")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from actor_stance_qa import onion_skin  # reuse, not a second implementation

# tools/screenshots/ is already .gitignore'd (see slice_spell_sheet.py's own
# PREVIEW_DIR comment for why a preview never lives beside shipped art).
DEFAULT_ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "screenshots", "rigs")

# The content this tool has to agree with is Resources/Rigs/<root>/<id>/
# animations.json, not the capture output -- RigCaptureTests samples AT
# SamplesPerSecond=12 from that same file's durationSeconds, so reading
# duration from animations.json (not guessing from frame count alone) is
# what keeps this synced if that sampling rate ever changes.
CONTENT_ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                             "Assets", "_Project", "Resources", "Rigs")

# How long a one-shot's LAST frame is held before the GIF loops back to the
# first -- long enough to actually read the landed pose, short enough that
# the loop does not feel like it stalled. A looping clip (idle) gets none of
# this: its own last-vs-first frame is already the wrap.
ONE_SHOT_HOLD_SECONDS = 0.5


def clip_timing(root, actor_id, stance):
    """(duration_seconds, loops) from the actor's own animations.json, or
    None if this stance/actor has no authored clip -- the caller falls back
    to a flat guess rather than failing the whole run over one gap.
    """
    path = os.path.join(CONTENT_ROOT, root, actor_id, "animations.json")
    if not os.path.isfile(path):
        return None

    with open(path, encoding="utf-8") as handle:
        raw = json.load(handle)

    for clip in raw.get("clips", []):
        if clip.get("stance") == stance:
            return float(clip.get("durationSeconds", 0.0)), bool(clip.get("loop", False))

    return None


def discover_frames(stance_dir):
    frames = []
    i = 0
    while True:
        path = os.path.join(stance_dir, f"f{i}.png")
        if not os.path.isfile(path):
            break
        frames.append(Image.open(path).convert("RGBA"))
        i += 1
    return frames


def write_stance_artifacts(actor_id, stance, stance_dir, out_dir, root):
    frames = discover_frames(stance_dir)
    if len(frames) < 1:
        print(f"  {stance}: no f0.png in {stance_dir} -- skipped")
        return

    canvas = frames[0].size
    timing = clip_timing(root, actor_id, stance)
    if timing is None:
        # No content entry found -- hold a flat, clearly-arbitrary pace
        # rather than guessing something that could pass for authored.
        per_frame = 1.0 / 12.0
        loops = False
        print(f"  {stance}: no '{stance}' clip in animations.json -- pacing at a flat "
              f"{per_frame:.3f}s/frame guess")
    else:
        duration, loops = timing
        per_frame = duration / max(1, len(frames) - 1) if len(frames) > 1 else duration

    os.makedirs(out_dir, exist_ok=True)

    # ---- the GIF ----------------------------------------------------------
    backdrop = Image.new("RGBA", canvas, (24, 20, 28, 255))
    shots = []
    for frame in frames:
        flat = backdrop.copy()
        flat.alpha_composite(frame)
        shots.append(flat.convert("P", palette=Image.ADAPTIVE))

    durations = [max(1, round(per_frame * 1000))] * len(shots)
    if not loops and len(shots) > 0:
        durations[-1] += round(ONE_SHOT_HOLD_SECONDS * 1000)

    gif_path = os.path.join(out_dir, f"{stance}.gif")
    shots[0].save(gif_path, save_all=True, append_images=shots[1:],
                  duration=durations, loop=0, disposal=2)

    # ---- the strip, for reading one frame rather than the timing ---------
    strip = Image.new("RGB", (canvas[0] * len(frames), canvas[1]), (24, 20, 28))
    for i, frame in enumerate(frames):
        strip.paste(frame, (i * canvas[0], 0), frame)
    strip_path = os.path.join(out_dir, f"{stance}_frames.png")
    strip.save(strip_path)

    # ---- the onion skin, for silhouette drift/spread at a glance ---------
    skin = onion_skin(frames, canvas)
    flat_skin = backdrop.copy()
    flat_skin.alpha_composite(skin)
    skin_path = os.path.join(out_dir, f"{stance}_onion.png")
    flat_skin.convert("RGB").save(skin_path)

    kind = "loops" if loops else f"holds {ONE_SHOT_HOLD_SECONDS}s"
    print(f"  {stance}: {len(frames)} frames @ {per_frame*1000:.0f}ms ({kind}) -> {gif_path}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=DEFAULT_ROOT,
                    help="Path to the rig capture tree (RigCaptureTests' output, "
                         "already copied back from the -TestRunner copy by rig_qa.ps1). "
                         "Defaults to tools/screenshots/rigs.")
    ap.add_argument("--only", nargs="*", default=None, help="Limit to these actor ids")
    args = ap.parse_args()

    if not os.path.isdir(args.root):
        sys.exit(f"Not a directory: {args.root}\n"
                  f"Run tools/rig_qa.ps1 first -- this reads its output, it does not render anything itself.")

    found_any = False
    for root_name in sorted(os.listdir(args.root)):
        root_dir = os.path.join(args.root, root_name)
        if not os.path.isdir(root_dir):
            continue

        for actor_id in sorted(os.listdir(root_dir)):
            if args.only and actor_id not in args.only:
                continue

            actor_dir = os.path.join(root_dir, actor_id)
            if not os.path.isdir(actor_dir):
                continue

            print(f"[{root_name}/{actor_id}]")
            for stance in sorted(os.listdir(actor_dir)):
                stance_dir = os.path.join(actor_dir, stance)
                if not os.path.isdir(stance_dir):
                    continue
                write_stance_artifacts(actor_id, stance, stance_dir, stance_dir, root_name)
                found_any = True

    if not found_any:
        sys.exit(f"No captured stances found under {args.root} -- run tools/rig_qa.ps1 first.")


if __name__ == "__main__":
    main()
