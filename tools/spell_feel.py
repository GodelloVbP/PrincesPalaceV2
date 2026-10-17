#!/usr/bin/env python3
"""Measures a spell runtime capture: what the cast does over time, and a
warning when it breaks one of three principles that hold for any spell.

WHY A MODULE NEXT TO capture_strip.py. The strip and the GIF are shared: this
imports build_strip and write_gif from there. What is new is analysis (curves,
beat timings, warnings, comparison), which is not strip-shaped, and
capture_strip's own main walks <label>/ folders that a flat spell series is not.

Input is what `tools/spell_feel.ps1` (or `tools/screenshot.ps1 -Runtime -Spell
<id>` with PP_RUNTIME_VERSUS=<enemy id>) leaves behind, copied into one folder:

    spell_<id>_f00.png .. spell_<id>_fNN.png   60 fps, frame 0 = cast first drawn
    spell_<id>_timing.json                     fps, frame count, hitFrame (the
        first picture the authored hit cue shows on; -1 if never crossed)

Usage:
    python tools/spell_feel.py report  --frames DIR --spell ID [--versus ENEMY] [--out DIR]
    python tools/spell_feel.py compare DIR_A DIR_B [--out DIR]     (two report folders)

Writes into the report folder:
    <spell>_vs_<enemy>.gif   960 px, 30 fps from 60 (real-time durations), 96
                             colours, no dither, loops, last frame held 1 s
    strip.png                every frame stamped with index and ms; contact ringed
    stills/                  gather, release, contact, residue, clean at full res
    report.json              curves, beat timings, authored values, warnings

THE MEASURES.
  activity  mean absolute RGB difference of the stage region from the LAST
            frame, per frame. The last frame is the capture's own clean end
            (the series records past the cast's cleared time).
  hot       share of stage pixels whose three channels are all >= HOT_LEVEL.
  floor     median activity of the final 10% of frames: what "clean" reads as,
            idle sprite noise included.
  peak      maximum activity. Every threshold is relative to (peak - floor),
            so a dim spell and a bright one are judged against themselves.
  release   frame of the largest single-frame rise before the hit frame.
  contact   the recorded hit frame; the largest rise after release is the
            visual contact.
  clean     first frame after which activity stays within CLEAN_FRAC of
            (peak - floor) above the floor.

DESCRIPTIVE FIRST. Gather length, travel length, their ratio, peak, residue and
clean time are printed as measurements; none is graded, because spells are not
meant to share one rhythm. docs/SPELL_FEEL.md says what Blackglass Spear did
and why. WARN fires only on three principles, each with a default that is a
parameter:

  residue tail    activity still above RESIDUE_FRAC of its own peak
                  (over the floor) more than --residue-limit seconds
                  (default 0.5) after contact.
  no accent       no single moment clearly above the rest: the peak, over the
                  floor, is less than --accent-ratio (default 1.5) times the
                  median activity of the cast (frame 0 to clean), over the floor.
                  A two-beat cast (release flash, then contact) passes: the
                  test is a spike against the cast's own level, not against
                  its second-best spike.
  contact desync  the recorded or the visual contact frame is more than
                  --desync-frames (default 2) from the frame the authored
                  hitCueSeconds says.

Disagreements between the authored skills.json values and the measured ones
are listed as NOTE, not WARN: they are worth a look, not a verdict.

THE STAGE REGION is the whole frame minus the HUD, which is static chrome whose
bars and numbers change with damage and mana and would read as effect. The
rectangles are in the 1920x1080 reference (Editor/CaptureReferenceScreen.cs) and
were read off a captured fight: top banner and labels, turn-order strip, the
enemy plate block, the party card, the command band. They are fractions of the
frame, so a different capture size still lands. If FightScreen moves a panel,
re-read them off a capture; tools/measure_stage.py reads the same plates from
source if that becomes worth wiring.

CALIBRATION. Tuned on blackglass_spear vs treant and rat:
HOT_LEVEL 235, RESIDUE_FRAC 0.25, CLEAN_FRAC 0.10. Both read no WARN, with gather
0.6 s, travel 0.1 s, clean at 1.1 s, residue 0.2-0.4 s. The shape to expect is
a spike at contact and a clean drop.
"""

import argparse
import json
import os
import re
import sys
import time

try:
    import numpy as np
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("Pillow and numpy are required: pip install Pillow numpy")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capture_strip  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKILLS_JSON = os.path.join(ROOT, "Assets", "_Project", "ContentData", "skills.json")

REFERENCE = (1920, 1080)

# x0, y0, x1, y1 in the 1920x1080 reference.
HUD_RECTS = (
    (0, 0, 1920, 100),       # banner text, ENEMIES / STANDING labels
    (0, 100, 540, 240),      # turn-order strip
    (1300, 100, 1920, 420),  # enemy plates (three enemies stack down this far)
    (0, 920, 520, 1080),     # party card
    (0, 1030, 1920, 1080),   # command / skill / target hint band
)

HOT_LEVEL = 235
RESIDUE_FRAC = 0.25
CLEAN_FRAC = 0.10
DEFAULT_RESIDUE_LIMIT = 0.5
DEFAULT_ACCENT_RATIO = 1.5
DEFAULT_DESYNC_FRAMES = 2

# Analysis runs on frames reduced by this factor: a mean over a million pixels
# does not need all of them, and a 109-frame series fits in memory.
REDUCE = 4

GIF_WIDTH = 960
GIF_COLORS = 96
GIF_STEP = 2
GIF_HOLD_MS = 1000


# ---- curve math (pure; the unit tests drive these on synthetic arrays) -----

def stage_mask(shape, rects=HUD_RECTS, reference=REFERENCE):
    """Boolean (H, W) mask, True on the stage, for a frame of `shape` (H, W)."""
    h, w = shape
    mask = np.ones((h, w), dtype=bool)
    for x0, y0, x1, y1 in rects:
        mask[int(y0 * h / reference[1]):int(round(y1 * h / reference[1])),
             int(x0 * w / reference[0]):int(round(x1 * w / reference[0]))] = False
    return mask


def activity_curve(frames, mask):
    """Mean absolute RGB difference from the last frame, over the mask."""
    end = frames[-1].astype(np.int16)
    return [float(np.abs(f.astype(np.int16) - end)[mask].mean()) for f in frames]


def hot_curve(frames, mask, level=HOT_LEVEL):
    """Share of masked pixels whose three channels are all >= level."""
    return [float((f.min(axis=2) >= level)[mask].mean()) for f in frames]


def _runs_above(values, threshold, merge_gap=2):
    """Contiguous index runs above threshold, runs closer than merge_gap merged."""
    runs = []
    for i, v in enumerate(values):
        if v <= threshold:
            continue
        if runs and i - runs[-1][1] <= merge_gap:
            runs[-1][1] = i
        else:
            runs.append([i, i])
    return runs


def analyse(activity, hot, fps, hit_frame, authored_hit_cue=None,
            residue_limit=DEFAULT_RESIDUE_LIMIT, accent_ratio=DEFAULT_ACCENT_RATIO,
            desync_frames=DEFAULT_DESYNC_FRAMES, ritual=False):
    """Beat timings and the three principle checks, from the two curves alone.

    ritual: the cast has no gather (its first layer plays at release), so frame 0
    is the start and the recorded hit frame is the contact. The release/contact
    inference below would pick the cast's own first rise and a later hurt flash
    as "visual contact", and report a desync that is not there; the visual
    contact is left unmeasured instead."""
    n = len(activity)
    tail = max(5, n // 10)
    floor = float(np.median(activity[-tail:]))
    peak = max(activity)
    peak_frame = activity.index(peak)
    span = max(peak - floor, 1e-9)

    def rise(i):
        return activity[i] - activity[i - 1]

    result = {"frames": n, "fps": fps, "floor": floor, "peak": peak,
              "peak_frame": peak_frame, "peak_s": peak_frame / fps}

    hot_peak = max(hot)
    result["hot_peak"] = hot_peak
    result["hot_peak_frame"] = hot.index(hot_peak)

    has_hit = hit_frame is not None and (0 <= hit_frame < n if ritual else 1 < hit_frame < n)
    release = None
    visual = None
    if has_hit and ritual:
        release = 0
    elif has_hit:
        release = max(range(1, hit_frame), key=rise)
        later = range(release + 1, n)
        visual = max(later, key=rise) if len(later) else None

    result["hit_frame"] = hit_frame if has_hit else None
    result["release_frame"] = release
    result["visual_contact_frame"] = visual
    result["gather_s"] = release / fps if release is not None else None
    result["travel_s"] = (hit_frame - release) / fps if release is not None else None
    result["gather_to_travel"] = (
        result["gather_s"] / result["travel_s"]
        if release is not None and hit_frame > release and not ritual else None)
    result["ritual"] = ritual

    anchor = hit_frame if has_hit else peak_frame
    residue_threshold = floor + RESIDUE_FRAC * span
    last_above = None
    for i in range(anchor + 1, n):
        if activity[i] > residue_threshold:
            last_above = i
    result["residue_s"] = (last_above - anchor) / fps if last_above is not None else 0.0

    clean_threshold = floor + CLEAN_FRAC * span
    clean = n - 1
    for i in range(n - 1, anchor - 1, -1):
        if activity[i] > clean_threshold:
            break
        clean = i
    result["clean_frame"] = clean
    result["clean_s"] = clean / fps
    result["clean_after_contact_s"] = (clean - anchor) / fps

    warnings = []

    if result["residue_s"] > residue_limit:
        warnings.append(
            "residue tail: effect is above %d%% of its peak %.2fs after contact (limit %.2fs)"
            % (round(RESIDUE_FRAC * 100), result["residue_s"], residue_limit))

    active = activity[:clean + 1]
    typical = float(np.median(active))
    result["typical"] = typical
    if span < accent_ratio * max(typical - floor, 1e-9):
        warnings.append(
            "no accent: peak %.1f over floor %.1f is under %.1fx the typical level of the cast (%.1f)"
            % (peak, floor, accent_ratio, typical))

    if authored_hit_cue is not None and has_hit:
        authored_frame = int(round(authored_hit_cue * fps))
        result["authored_hit_frame"] = authored_frame
        if abs(hit_frame - authored_frame) > desync_frames:
            warnings.append(
                "contact desync: hit cue shows on frame %d, authored hitCueSeconds %.2f is frame %d"
                % (hit_frame, authored_hit_cue, authored_frame))
        if visual is not None and abs(visual - authored_frame) > desync_frames:
            warnings.append(
                "contact desync: biggest rise after release is frame %d, authored hit cue is frame %d"
                % (visual, authored_frame))

    result["warnings"] = warnings
    return result


def replace_file(path, write, attempts=8, delay=0.25):
    """Write `path` via a temp file in the same folder, then swap it in.

    Opening an existing file for truncation fails with Errno 22 on Windows while
    anything (Explorer preview, a thumbnailer, an image viewer) holds it mapped.
    Replacing instead of truncating succeeds as soon as the mapping lets go, and
    a short retry covers the transient ones. `write(tmp_path)` must write the
    whole file; the temp name keeps the real extension so PIL picks the format.
    """
    root, ext = os.path.splitext(path)
    tmp = root + ".tmp" + ext
    try:
        write(tmp)
        for attempt in range(attempts):
            try:
                os.replace(tmp, path)
                return
            except OSError as err:
                if attempt == attempts - 1:
                    raise OSError(err.errno, "cannot overwrite %s (held open by another "
                                  "program; close any preview or viewer): %s" % (path, err.strerror))
                time.sleep(delay)
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)


# ---- authored values --------------------------------------------------------

def authored_values(spell_id, path=SKILLS_JSON):
    """What skills.json says the cast should do, or None if it has no such spell."""
    if not os.path.isfile(path):
        return None
    with open(path, "r", encoding="utf-8") as handle:
        skills = json.load(handle).get("skills", [])
    for skill in skills:
        if skill.get("id") != spell_id:
            continue
        vfx = skill.get("vfx") or {}
        layers = []
        for layer in vfx.get("layers", []):
            layers.append({k: layer[k] for k in
                           ("id", "render", "at", "offset", "travelSeconds", "seconds",
                            "fps", "fade", "until", "punch", "glow") if k in layer})
        travellers = [l for l in layers if l.get("travelSeconds")]
        projectile = max(travellers, key=lambda l: l.get("travelSeconds", 0)) if travellers else None
        return {
            "hitCueSeconds": vfx.get("hitCueSeconds"),
            "release_s": projectile.get("offset", 0.0) if projectile else None,
            "travel_s": projectile.get("travelSeconds") if projectile else None,
            "layers": layers,
        }
    return None


def is_ritual(authored):
    """No gather: the first layer plays at release or at the hit and nothing is delayed or travels."""
    layers = (authored or {}).get("layers") or []
    if not layers or layers[0].get("at") not in ("release", "hit"):
        return False
    return not any((l.get("offset") or 0) > 0 or l.get("travelSeconds") for l in layers)


# The analysis window ends this long after the cast's own cleared time. The
# capture records past it so the series has a clean end; a later event (a DoT
# tick on the next turn) must not be measured as part of the cast.
CLEARED_MARGIN_S = 0.1


def window_end(frame_count, fps, cleared_seconds):
    """Frames to analyse: up to the cast's cleared time plus CLEARED_MARGIN_S."""
    if not cleared_seconds or cleared_seconds <= 0:
        return frame_count
    return max(2, min(frame_count, int(round((cleared_seconds + CLEARED_MARGIN_S) * fps)) + 1))


def scale_authored(authored, speed):
    """Authored seconds divided by the battle speed they were recorded at."""
    if not authored or not speed or speed == 1:
        return authored
    out = dict(authored)
    for key in ("hitCueSeconds", "release_s", "travel_s"):
        if out.get(key) is not None:
            out[key] = out[key] / speed
    return out


def disagreements(measured, authored, fps, tolerance_frames=2):
    """NOTE lines where a measured beat is more than tolerance from the authored one."""
    notes = []
    if not authored:
        return notes
    tol = tolerance_frames / fps
    pairs = (("gather_s", "release_s", "gather"), ("travel_s", "travel_s", "travel"))
    for m_key, a_key, label in pairs:
        m, a = measured.get(m_key), authored.get(a_key)
        if m is not None and a is not None and abs(m - a) > tol:
            notes.append("%s measured %.2fs, authored %.2fs" % (label, m, a))
    return notes


# ---- IO ---------------------------------------------------------------------

def find_series(frames_dir, spell):
    pattern = re.compile(r"^spell_%s_f(\d+)\.png$" % re.escape(spell))
    found = []
    for name in os.listdir(frames_dir):
        m = pattern.match(name)
        if m:
            found.append((int(m.group(1)), os.path.join(frames_dir, name)))
    return [p for _, p in sorted(found)]


def read_timing(frames_dir, spell):
    path = os.path.join(frames_dir, "spell_%s_timing.json" % spell)
    if not os.path.isfile(path):
        return {}
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def load_series(paths):
    """(gif-size RGB frames, reduced arrays for analysis), one frame in memory at full size."""
    shown, small = [], []
    for path in paths:
        with Image.open(path) as im:
            rgb = im.convert("RGB")
            half = rgb.resize((GIF_WIDTH, int(round(rgb.size[1] * GIF_WIDTH / rgb.size[0]))),
                              Image.LANCZOS)
            shown.append(half)
            small.append(np.asarray(rgb.reduce(REDUCE)))
    return shown, small


def key_frames(analysis, fps, hot):
    contact = analysis["hit_frame"] if analysis["hit_frame"] is not None else analysis["peak_frame"]
    release = analysis["release_frame"]
    last = analysis["frames"] - 1
    if release is None:
        release = max(0, contact - 6)
    hot_window = range(max(0, release - 1), min(last, contact) + 1)
    return {
        "gather": max(0, release // 2),
        "release": max(hot_window, key=lambda i: hot[i]),
        "contact": contact,
        "residue": min(last, contact + int(round(0.2 * fps))),
        "clean": analysis["clean_frame"],
    }


def strip_impact_frame(hit_frame):
    """The strip's contact marker, -1 when no contact was found. Frame 0 is a
    real contact (a ritual hit lands on the first frame)."""
    return -1 if hit_frame is None else hit_frame


def run_report(frames_dir, spell, versus, out_dir, params):
    paths = find_series(frames_dir, spell)
    if not paths:
        sys.exit("no spell_%s_fNN.png in %s" % (spell, frames_dir))
    timing = read_timing(frames_dir, spell)
    fps = float(timing.get("fps", 60))
    hit = timing.get("hitFrame", -1)
    hit = hit if hit is not None and hit >= 0 else None

    speed = float(timing.get("speed", 1) or 1)

    shown, small = load_series(paths)
    end = window_end(len(small), fps, timing.get("clearedSeconds"))
    shown, small = shown[:end], small[:end]
    if hit is not None and hit >= end:
        hit = None
    mask = stage_mask(small[0].shape[:2])
    activity = activity_curve(small, mask)
    hot = hot_curve(small, mask)

    authored_raw = authored_values(spell)
    ritual = is_ritual(authored_raw)
    authored = scale_authored(authored_raw, speed)
    cue = authored.get("hitCueSeconds") if authored else None
    if ritual and authored["layers"][0].get("at") == "hit":
        # Frame 0 is the first drawn frame, which for an at-hit cast IS the
        # hit cue; the authored seconds are on the cast clock, not on frame 0's.
        cue = None
    result = analyse(activity, hot, fps, hit, cue, ritual=ritual, **params)
    result["notes"] = disagreements(result, authored, fps)
    stills = key_frames(result, fps, hot)

    os.makedirs(os.path.join(out_dir, "stills"), exist_ok=True)
    name = "%s_vs_%s" % (spell, versus) if versus else spell
    interval = 1000.0 / fps

    replace_file(os.path.join(out_dir, name + ".gif"),
                 lambda tmp: capture_strip.write_gif(
                     shown, {"captureIntervalMs": interval}, tmp,
                     width=GIF_WIDTH, colors=GIF_COLORS,
                     hold_last_ms=GIF_HOLD_MS, step=GIF_STEP, disposal=0))

    strip_timing = {"captureIntervalMs": interval, "impactFrame": strip_impact_frame(result["hit_frame"]),
                    "settledFrame": result["clean_frame"]}
    title = "%s | %d frames @ %.1fms | contact f%s | clean f%d" % (
        name, len(shown), interval, result["hit_frame"], result["clean_frame"])
    strip_image = capture_strip.build_strip(shown, strip_timing, title)
    replace_file(os.path.join(out_dir, "strip.png"), strip_image.save)

    for label, index in stills.items():
        with Image.open(paths[index]) as im:
            still = im.convert("RGB")
            replace_file(os.path.join(out_dir, "stills", "%s_f%02d.png" % (label, index)), still.save)

    report = {
        "spell": spell, "versus": versus, "fps": fps, "speed": speed,
        "presentation_only": bool(timing.get("presentationOnly", False)),
        "analysed_frames": end, "recorded_frames": len(paths),
        "region": {"hud_rects_1920x1080": [list(r) for r in HUD_RECTS], "hot_level": HOT_LEVEL},
        "params": params,
        "measured": {k: v for k, v in result.items() if k not in ("warnings", "notes")},
        "authored": authored,
        "stills": stills,
        "warnings": result["warnings"],
        "notes": result["notes"],
        "activity": [round(a, 3) for a in activity],
        "hot": [round(h, 5) for h in hot],
    }
    def write_report(tmp):
        with open(tmp, "w", encoding="utf-8") as handle:
            json.dump(report, handle, indent=1)
    replace_file(os.path.join(out_dir, "report.json"), write_report)

    print(summary(report))
    return report


def summary(report):
    m = report["measured"]
    f = lambda v, fmt="%.2f": "n/a" if v is None else fmt % v
    lines = [
        "%s vs %s  (%d frames @ %.0f fps)" % (report["spell"], report["versus"] or "default",
                                              m["frames"], m["fps"]),
        "  gather %ss  travel %ss  gather:travel %s" % (
            f(m["gather_s"]), f(m["travel_s"]), f(m["gather_to_travel"], "%.1f")),
        "  contact f%s (authored f%s, visual f%s)" % (
            m["hit_frame"], m.get("authored_hit_frame", "?"), m["visual_contact_frame"]),
        "  peak %.1f at %.2fs over floor %.1f   hot peak %.2f%% at f%d" % (
            m["peak"], m["peak_s"], m["floor"], m["hot_peak"] * 100, m["hot_peak_frame"]),
        "  residue %.2fs after contact   clean at %.2fs (%.2fs after contact)" % (
            m["residue_s"], m["clean_s"], m["clean_after_contact_s"]),
    ]
    if report.get("speed", 1) != 1:
        lines.append("  speed %.2fx: authored seconds divided by it" % report["speed"])
    if report.get("presentation_only"):
        lines.append("  presentation only: played through the module, no damage number or status")
    for note in report["notes"]:
        lines.append("  NOTE  " + note)
    if report["warnings"]:
        for warning in report["warnings"]:
            lines.append("  WARN  " + warning)
    else:
        lines.append("  no warnings")
    return "\n".join(lines)


# ---- comparison -------------------------------------------------------------

def _gif_frames(path):
    out = []
    with Image.open(path) as im:
        durations = []
        for i in range(im.n_frames):
            im.seek(i)
            out.append(im.convert("RGB"))
            durations.append(im.info.get("duration", 30))
    return out, durations


def curve_chart(reports, labels, path):
    w, h, pad = 960, 360, 48
    img = Image.new("RGB", (w, h), (24, 20, 28))
    draw = ImageDraw.Draw(img)
    font = capture_strip.load_font(14)
    colours = [(120, 190, 255), (255, 170, 80)]
    top = max(max(r["activity"]) for r in reports) * 1.1
    seconds = max(len(r["activity"]) / r["fps"] for r in reports)
    px = lambda t: pad + (w - 2 * pad) * t / seconds
    py = lambda a: h - pad - (h - 2 * pad) * a / top

    draw.line([(pad, h - pad), (w - pad, h - pad)], fill=(140, 135, 130))
    draw.line([(pad, pad), (pad, h - pad)], fill=(140, 135, 130))
    for s in range(int(seconds) + 1):
        draw.text((px(s) - 4, h - pad + 4), "%ds" % s, fill=(200, 195, 190), font=font)

    for i, (report, label) in enumerate(zip(reports, labels)):
        colour = colours[i % len(colours)]
        points = [(px(k / report["fps"]), py(a)) for k, a in enumerate(report["activity"])]
        draw.line(points, fill=colour, width=2)
        hit = report["measured"].get("hit_frame")
        if hit is not None:
            x = px(hit / report["fps"])
            draw.line([(x, pad), (x, h - pad)], fill=colour, width=1)
        draw.text((pad + 8 + i * 300, 10), "%s  (activity, contact line)" % label,
                  fill=colour, font=font)
    replace_file(path, img.save)


def run_compare(dir_a, dir_b, out_dir):
    reports, gifs, labels = [], [], []
    for d in (dir_a, dir_b):
        with open(os.path.join(d, "report.json"), "r", encoding="utf-8") as handle:
            reports.append(json.load(handle))
        gif = [f for f in os.listdir(d) if f.endswith(".gif")]
        if not gif:
            sys.exit("no gif in %s" % d)
        gifs.append(os.path.join(d, gif[0]))
        labels.append(os.path.basename(os.path.normpath(d)))

    os.makedirs(out_dir, exist_ok=True)
    curve_chart(reports, labels, os.path.join(out_dir, "curves.png"))

    (fa, da), (fb, db) = _gif_frames(gifs[0]), _gif_frames(gifs[1])
    count = max(len(fa), len(fb))
    stacked, durations = [], []
    for i in range(count):
        a, b = fa[min(i, len(fa) - 1)], fb[min(i, len(fb) - 1)]
        sheet = Image.new("RGB", (max(a.size[0], b.size[0]), a.size[1] + b.size[1]))
        sheet.paste(a, (0, 0))
        sheet.paste(b, (0, a.size[1]))
        stacked.append(sheet.quantize(colors=128, method=Image.MEDIANCUT, dither=Image.NONE))
        durations.append(max(da[min(i, len(da) - 1)], db[min(i, len(db) - 1)]))
    replace_file(os.path.join(out_dir, "before_vs_after.gif"),
                 lambda tmp: stacked[0].save(tmp, save_all=True, append_images=stacked[1:],
                                             duration=durations, loop=0))

    keys = ("gather_s", "travel_s", "gather_to_travel", "peak", "residue_s", "clean_s")
    print("%-18s %14s %14s" % ("", labels[0][-14:], labels[1][-14:]))
    for k in keys:
        va, vb = (r["measured"].get(k) for r in reports)
        print("%-18s %14s %14s" % (k, "n/a" if va is None else "%.2f" % va,
                                   "n/a" if vb is None else "%.2f" % vb))
    for label, r in zip(labels, reports):
        print("%s warnings: %s" % (label, "; ".join(r["warnings"]) or "none"))
    print("-> %s" % out_dir)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    rp = sub.add_parser("report")
    rp.add_argument("--frames", required=True, help="folder with spell_<id>_fNN.png and timing json")
    rp.add_argument("--spell", required=True)
    rp.add_argument("--versus", default="")
    rp.add_argument("--out", default=None)
    rp.add_argument("--residue-limit", type=float, default=DEFAULT_RESIDUE_LIMIT)
    rp.add_argument("--accent-ratio", type=float, default=DEFAULT_ACCENT_RATIO)
    rp.add_argument("--desync-frames", type=int, default=DEFAULT_DESYNC_FRAMES)

    cp = sub.add_parser("compare")
    cp.add_argument("a")
    cp.add_argument("b")
    cp.add_argument("--out", default=None)

    args = ap.parse_args()
    if args.cmd == "report":
        name = "%s_vs_%s" % (args.spell, args.versus) if args.versus else args.spell
        out = args.out or os.path.join(ROOT, "docs", "captures", "spell_feel", name)
        run_report(args.frames, args.spell, args.versus, out,
                   {"residue_limit": args.residue_limit, "accent_ratio": args.accent_ratio,
                    "desync_frames": args.desync_frames})
    else:
        out = args.out or os.path.join(
            os.path.dirname(os.path.normpath(args.a)),
            "compare_%s_vs_%s" % (os.path.basename(os.path.normpath(args.a)),
                                  os.path.basename(os.path.normpath(args.b))))
        run_compare(args.a, args.b, out)


if __name__ == "__main__":
    main()
