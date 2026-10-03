#!/usr/bin/env python3
"""The Python half of tools/gif.ps1 (docs/PLAN_GIF_CAPTURE.md).

tools/gif.ps1 owns the Unity launch; everything that needs no Unity lives here
so it can be tested alone (tools/gif_test.py):

    prepare   normalize a request into scenarios, refuse unknown ids, make the
              immutable run directory, write request.json, identity.json and
              manifest.json into it
    finish    turn the frames Unity wrote into anim.gif / strip.png and write
              scenario.json, latest.json and the coverage table
    compare   two run directories: content-manifest eligibility (2.6), then
              frame counts and per-frame pixel differences
    framediff two folders of PNGs, for the extraction's preservation gate
"""

import argparse
import fnmatch
import hashlib
import json
import os
import re
import subprocess
import sys
import time
import uuid

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(TOOLS)
sys.path.insert(0, TOOLS)

CONTENT = os.path.join(ROOT, "Assets", "_Project", "ContentData")
STATUS_SOURCE = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Domain", "Combat", "StatusEffect.cs")
RUNS = os.path.join(TOOLS, "screenshots", "gif", "runs")
LATEST = os.path.join(TOOLS, "screenshots", "gif", "latest.json")

KINDS = ("spell", "attack", "relic", "status")
FORMATIONS = ("lone", "full")
CROPS = ("subject", "stage", "full")

# The stage is StageCaptureRig's crop: FightStageAnchors.StageSize (1700x600)
# centred in the 1920x1080 reference canvas.
STAGE_BOX = (110, 240, 1810, 840)

GIF_WIDTH = 960
GIF_STEP = 2          # 60 fps captured, 30 fps shown
GIF_HOLD_MS = 500
CAPTURE_FPS = 60

# A pixel differs when any channel differs at all; the gate is on how many do.
PIXEL_LIMIT_PERCENT = 1.0

WATCHED_ROOTS = ("Assets", "Packages", "ProjectSettings")


def load_content(name, key):
    with open(os.path.join(CONTENT, name), encoding="utf-8") as handle:
        return json.load(handle)[key]


def status_names(path=STATUS_SOURCE):
    """StatusEffectType's members, read from the enum source so a new status is
    valid here on the same edit."""
    with open(path, encoding="utf-8") as handle:
        text = handle.read()
    body = text[text.index("enum StatusEffectType"):]
    body = body[body.index("{") + 1:body.index("}")]
    body = re.sub(r"//[^\n]*", "", body)
    names = []
    for token in body.split(","):
        token = token.strip().split("=")[0].strip()
        if token:
            names.append(token)
    return names


# ---- scenarios ---------------------------------------------------------------

def normalize(raw, skills=None):
    """Fills every default and sorts the keys, so two requests that differ in
    any field never share a key (2.2)."""
    skills = skills if skills is not None else {s["id"]: s for s in load_content("skills.json", "skills")}
    kind = raw["kind"]
    out = {
        "kind": kind,
        "id": raw["id"],
        "vs": raw.get("vs") or "rat",
        "formation": raw.get("formation") or "",
        "speed": float(raw.get("speed") or 1.0),
        "preStatus": raw.get("preStatus") or "",
        "skill": raw.get("skill") or "",
        "crop": raw.get("crop") or "",
        "seconds": float(raw.get("seconds") or 0.0),
    }
    if not out["formation"]:
        # An all-target spell is only visible against a rack.
        aims_all = kind == "spell" and skills.get(out["id"], {}).get("targeting") == "AllEnemies"
        out["formation"] = "full" if aims_all else "lone"
    if not out["crop"]:
        out["crop"] = "subject" if kind == "status" else "stage"
    if kind == "status" and out["seconds"] <= 0:
        out["seconds"] = 2.0
    if kind != "status":
        out["seconds"] = 0.0
    out["key"] = scenario_key(out)
    return dict(sorted(out.items()))


def scenario_key(scenario):
    body = {k: v for k, v in scenario.items() if k != "key"}
    blob = json.dumps(body, sort_keys=True, separators=(",", ":"))
    return hashlib.sha1(blob.encode("utf-8")).hexdigest()[:10]


def known_ids():
    skills = load_content("skills.json", "skills")
    enemies = load_content("enemies.json", "enemies")
    relics = load_content("relics.json", "relics")
    return {
        "spell": [s["id"] for s in skills if s.get("vfx")],
        "allSkills": [s["id"] for s in skills],
        "attack": [e["id"] for e in enemies if e.get("spritePath")],
        "enemies": [e["id"] for e in enemies],
        "enemyAbilities": {e["id"]: [a["skillId"] for a in e.get("abilities", []) or []] for e in enemies},
        "relic": [r["id"] for r in relics if r.get("vfx")],
        "allRelics": [r["id"] for r in relics],
        "status": status_names(),
    }


def refusals(scenario, ids=None):
    """Why a scenario cannot be launched, checked against the content JSON so a
    bad id costs no Unity boot."""
    ids = ids or known_ids()
    kind, sid = scenario["kind"], scenario["id"]
    out = []
    if kind not in KINDS:
        return ["kind '%s' is not one of %s (stance/sheet/still arrive in phase B)" % (kind, ", ".join(KINDS))]
    if scenario["formation"] not in FORMATIONS:
        out.append("formation '%s' is not lone or full" % scenario["formation"])
    if scenario["crop"] not in CROPS:
        out.append("crop '%s' is not one of %s" % (scenario["crop"], ", ".join(CROPS)))
    if scenario["speed"] <= 0:
        out.append("speed must be positive")
    if kind == "spell":
        if sid not in ids["allSkills"]:
            out.append("spell '%s' is not in skills.json" % sid)
        elif sid not in ids["spell"]:
            out.append("spell '%s' has no vfx to record" % sid)
    elif kind == "attack":
        if sid not in ids["enemies"]:
            out.append("enemy '%s' is not in enemies.json" % sid)
        elif sid not in ids["attack"]:
            out.append("enemy '%s' has no art; with art: %s" % (sid, ", ".join(ids["attack"])))
        elif scenario["skill"] and scenario["skill"] not in ids["enemyAbilities"][sid]:
            out.append("'%s' has no ability '%s'; it has: %s" % (
                sid, scenario["skill"], ", ".join(ids["enemyAbilities"][sid]) or "none (plain attack only)"))
    elif kind == "relic":
        if sid not in ids["allRelics"]:
            out.append("relic '%s' is not in relics.json" % sid)
        elif sid not in ids["relic"]:
            out.append("relic '%s' has no vfx; relics with vfx: %s" % (sid, ", ".join(ids["relic"])))
    elif kind == "status":
        if sid not in ids["status"]:
            out.append("status '%s' is not a StatusEffectType: %s" % (sid, ", ".join(ids["status"])))
    if kind in ("spell", "relic", "status") and scenario["vs"] not in ids["enemies"]:
        out.append("vs '%s' is not in enemies.json" % scenario["vs"])
    if scenario["preStatus"] and scenario["preStatus"] not in ids["status"]:
        out.append("preStatus '%s' is not a StatusEffectType" % scenario["preStatus"])
    return out


# ---- identity (2.6) ----------------------------------------------------------

def git(*args, root=ROOT):
    return subprocess.run(["git", "-C", root] + list(args), capture_output=True, check=True).stdout


def blob_hash(path):
    """git's own blob hash of a file's bytes, so a modified or untracked file is
    hashed in the same form as a clean tracked one and the two compare."""
    with open(path, "rb") as handle:
        data = handle.read()
    return hashlib.sha1(b"blob %d\0" % len(data) + data).hexdigest()


def content_manifest(root=ROOT):
    """{relative path: git blob hash} for every file under Assets, Packages and
    ProjectSettings. Clean tracked files take the index's hash; modified and
    untracked files are hashed from their contents. Sizes and mtimes are never
    used."""
    tracked = {}
    for line in git("ls-files", "-s", "-z", "--", *WATCHED_ROOTS, root=root).split(b"\0"):
        if not line:
            continue
        meta, path = line.split(b"\t", 1)
        tracked[path.decode("utf-8")] = meta.split()[1].decode("ascii")
    modified = set(p.decode("utf-8") for p in git("ls-files", "-m", "-z", "--", *WATCHED_ROOTS, root=root).split(b"\0") if p)

    manifest = {}
    for top in WATCHED_ROOTS:
        base = os.path.join(root, top)
        for folder, _dirs, files in os.walk(base):
            for name in files:
                full = os.path.join(folder, name)
                rel = os.path.relpath(full, root).replace(os.sep, "/")
                if rel in tracked and rel not in modified:
                    manifest[rel] = tracked[rel]
                else:
                    manifest[rel] = blob_hash(full)
    return dict(sorted(manifest.items()))


def fingerprint(manifest):
    blob = "\n".join("%s %s" % (k, v) for k, v in sorted(manifest.items()))
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()


def editor_version(root=ROOT):
    with open(os.path.join(root, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("m_EditorVersion:"):
                return line.split(":", 1)[1].strip()
    return ""


def identity(root=ROOT, manifest=None):
    manifest = manifest if manifest is not None else content_manifest(root)
    return {
        "path": os.path.normcase(os.path.realpath(root)),
        "editorVersion": editor_version(root),
        "headSha": git("rev-parse", "HEAD", root=root).decode("ascii").strip(),
        "fingerprint": fingerprint(manifest),
        "files": len(manifest),
    }


def manifest_diff(a, b, allow=()):
    """Paths whose hash differs or that exist on one side only, minus any that
    match an allow-listed glob."""
    diff = sorted(p for p in set(a) | set(b) if a.get(p) != b.get(p))
    return [p for p in diff if not any(fnmatch.fnmatch(p, g) for g in allow)]


def read_allow_list(path):
    if not path:
        return []
    with open(path, encoding="utf-8") as handle:
        return [l.strip() for l in handle if l.strip() and not l.lstrip().startswith("#")]


# ---- atomic files ------------------------------------------------------------

def write_json_atomic(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    temp = path + ".tmp"
    with open(temp, "w", encoding="utf-8") as handle:
        json.dump(data, handle, indent=2, sort_keys=True)
    os.replace(temp, path)


def read_json(path, default=None):
    try:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return default


# ---- prepare -----------------------------------------------------------------

def cmd_prepare(args):
    raws = [{"kind": args.kind, "id": args.id, "vs": args.vs, "formation": args.formation,
             "speed": args.speed, "preStatus": args.prestatus, "skill": args.skill,
             "crop": args.crop, "seconds": args.seconds}]
    ids = known_ids()
    scenarios = [normalize(r) for r in raws]
    problems = [(s["kind"], s["id"], m) for s in scenarios for m in refusals(s, ids)]
    if problems:
        for kind, sid, message in problems:
            print("REFUSED %s %s: %s" % (kind, sid, message), file=sys.stderr)
        return 2

    request_id = args.request_id or uuid.uuid4().hex[:12]
    stamp = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime())
    run_dir = os.path.join(RUNS, "%s_%s" % (stamp, request_id))
    os.makedirs(run_dir)

    manifest = content_manifest()
    write_json_atomic(os.path.join(run_dir, "manifest.json"), manifest)
    ident = identity(manifest=manifest)
    write_json_atomic(os.path.join(run_dir, "identity.json"), ident)
    write_json_atomic(os.path.join(run_dir, "request.json"),
                      {"requestId": request_id, "outDir": run_dir, "scenarios": scenarios})
    print(json.dumps({"requestId": request_id, "runDir": run_dir, "scenarios": len(scenarios),
                      "fingerprint": ident["fingerprint"]}))
    return 0


# ---- frames, crop, encode ----------------------------------------------------

def list_frames(frames_dir):
    names = sorted(n for n in os.listdir(frames_dir) if n.lower().endswith(".png"))
    return [os.path.join(frames_dir, n) for n in names]


def crop_box(crop, size, motion=None):
    if crop == "full":
        return (0, 0, size[0], size[1])
    x0, y0, x1, y1 = STAGE_BOX
    if crop == "stage":
        return (x0, y0, min(x1, size[0]), min(y1, size[1]))
    if motion is None:
        return (x0, y0, min(x1, size[0]), min(y1, size[1]))
    mx0, my0, mx1, my1 = motion
    pad = 60
    return (max(x0, mx0 - pad), max(y0, my0 - pad), min(x1, mx1 + pad), min(y1, my1 + pad))


def motion_bbox(paths):
    """The stage-box region where any frame differs from the first, for the
    `subject` crop: what moved is the subject."""
    import numpy as np
    from PIL import Image
    x0, y0, x1, y1 = STAGE_BOX
    with Image.open(paths[0]) as im:
        first = np.asarray(im.convert("RGB").crop(STAGE_BOX), dtype=np.int16)
    moved = np.zeros(first.shape[:2], dtype=bool)
    for p in paths[1::4]:
        with Image.open(p) as im:
            cur = np.asarray(im.convert("RGB").crop(STAGE_BOX), dtype=np.int16)
        moved |= (np.abs(cur - first).max(axis=2) > 12)
    ys, xs = np.nonzero(moved)
    if len(xs) == 0:
        return None
    return (x0 + int(xs.min()), y0 + int(ys.min()), x0 + int(xs.max()) + 1, y0 + int(ys.max()) + 1)


def encode(scenario_dir, scenario, timing):
    """anim.gif and strip.png from frames/. Returns encode seconds."""
    from PIL import Image
    import capture_strip

    started = time.perf_counter()
    paths = list_frames(os.path.join(scenario_dir, "frames"))
    with Image.open(paths[0]) as im:
        size = im.size
    motion = motion_bbox(paths) if scenario["crop"] == "subject" and len(paths) > 1 else None
    box = crop_box(scenario["crop"], size, motion)

    shown = []
    for p in paths[::GIF_STEP]:
        with Image.open(p) as im:
            shown.append(im.convert("RGB").crop(box))

    interval = 1000.0 / CAPTURE_FPS * GIF_STEP
    capture_strip.write_gif(shown, {"captureIntervalMs": interval}, os.path.join(scenario_dir, "anim.gif"),
                            width=GIF_WIDTH, colors=None, hold_last_ms=GIF_HOLD_MS, step=1, disposal=0)

    hit = timing.get("hitFrame", -1)
    strip_timing = {"captureIntervalMs": interval, "impactFrame": hit // GIF_STEP if hit >= 0 else -1}
    title = "%s %s | %d frames @ %dfps" % (scenario["kind"], scenario["id"], len(paths), CAPTURE_FPS)
    capture_strip.build_strip(shown, strip_timing, title).save(os.path.join(scenario_dir, "strip.png"))
    return time.perf_counter() - started


# ---- coverage (2.9) ----------------------------------------------------------

def coverage_table(scenarios, outcomes, ids=None):
    ids = ids or known_ids()
    lines = ["COVERAGE", "  requested %d scenario(s): %d passed, %d failed" % (
        len(scenarios), sum(1 for o in outcomes.values() if o), sum(1 for o in outcomes.values() if not o))]
    for s in scenarios:
        lines.append("    %-6s %-22s vs=%-6s %-4s x%-4g %s" % (
            s["kind"], s["id"], s["vs"], s["formation"], s["speed"], "ok" if outcomes.get(s["key"]) else "FAILED"))
    lines.append("  known but not covered:")
    for kind in KINDS:
        have = {s["id"] for s in scenarios if s["kind"] == kind}
        missing = [i for i in ids[kind] if i not in have]
        shown = ", ".join(missing[:12]) + (" ... (+%d)" % (len(missing) - 12) if len(missing) > 12 else "")
        lines.append("    %-6s %d of %d not requested%s" % (kind, len(missing), len(ids[kind]), ": " + shown if missing else ""))
    for s in (x for x in scenarios if x["kind"] == "spell"):
        lines.append("    spell %s: only %s %s at %gx (baseline is rat+treant x lone+full x 1x+1.5x)" % (
            s["id"], s["vs"], s["formation"], s["speed"]))
    lines.append("    not covered at all (phase B): stance, sheet, still")
    return "\n".join(lines)


# ---- finish ------------------------------------------------------------------

def cmd_finish(args):
    run_dir = os.path.abspath(args.run_dir)
    request = read_json(os.path.join(run_dir, "request.json"))
    ident = read_json(os.path.join(run_dir, "identity.json"), {})
    batch = read_json(os.path.join(run_dir, "result.json"), {})
    scenarios = request["scenarios"]
    outcomes = {}
    encode_seconds = {}
    latest = read_json(LATEST, {})

    for s in scenarios:
        sdir = os.path.join(run_dir, s["key"])
        result = read_json(os.path.join(sdir, "result.json"), {})
        timing = read_json(os.path.join(sdir, "timing.json"), {})
        ok = bool(result.get("ok")) and os.path.isdir(os.path.join(sdir, "frames")) and bool(list_frames(os.path.join(sdir, "frames")))
        outcomes[s["key"]] = ok
        record = {"scenario": s, "identity": ident, "requestId": request["requestId"], "result": result, "timing": timing}
        if ok:
            encode_seconds[s["key"]] = round(encode(sdir, s, timing), 3)
            record["encodeSeconds"] = encode_seconds[s["key"]]
            latest[s["key"]] = run_dir
        else:
            print("FAILED %s %s: %s" % (s["kind"], s["id"], result.get("error") or "no result/frames written"))
        write_json_atomic(os.path.join(sdir, "scenario.json"), record)

    if latest:
        write_json_atomic(LATEST, latest)
    print(coverage_table(scenarios, outcomes))
    for s in scenarios:
        r = read_json(os.path.join(run_dir, s["key"], "result.json"), {})
        print("  %-6s %-22s run %.1fs  encode %.2fs  frames %s" % (
            s["kind"], s["id"], r.get("seconds", 0), encode_seconds.get(s["key"], 0), r.get("frames", "?")))
    print("  batch ok=%s requestId=%s" % (batch.get("ok"), batch.get("requestId")))
    return 0 if outcomes and all(outcomes.values()) else 1


# ---- pixel comparison --------------------------------------------------------

def frame_difference(path_a, path_b):
    """Percent of pixels where any channel differs, strict and past +-2."""
    import numpy as np
    from PIL import Image
    with Image.open(path_a) as a, Image.open(path_b) as b:
        x = np.asarray(a.convert("RGB"), dtype=np.int16)
        y = np.asarray(b.convert("RGB"), dtype=np.int16)
    if x.shape != y.shape:
        return 100.0, 100.0
    delta = np.abs(x - y).max(axis=2)
    total = delta.size
    return 100.0 * np.count_nonzero(delta) / total, 100.0 * np.count_nonzero(delta > 2) / total


def compare_series(paths_a, paths_b):
    """(frames_a, frames_b, worst strict %, worst tolerant %) over aligned frames."""
    worst = worst_tol = 0.0
    for a, b in zip(paths_a, paths_b):
        strict, tol = frame_difference(a, b)
        worst, worst_tol = max(worst, strict), max(worst_tol, tol)
    return len(paths_a), len(paths_b), worst, worst_tol


def cmd_framediff(args):
    paths_a = sorted(p for p in (os.path.join(args.a, n) for n in os.listdir(args.a)) if p.lower().endswith(".png"))
    paths_b = sorted(p for p in (os.path.join(args.b, n) for n in os.listdir(args.b)) if p.lower().endswith(".png"))
    na, nb, worst, tol = compare_series(paths_a, paths_b)
    print("frames %d vs %d | worst frame: %.4f%% pixels differ (%.4f%% past +-2)" % (na, nb, worst, tol))
    return 0 if na == nb and worst < PIXEL_LIMIT_PERCENT else 1


def resolve_run(name):
    return name if os.path.isdir(name) else os.path.join(RUNS, name)


def cmd_compare(args):
    run_a, run_b = resolve_run(args.a), resolve_run(args.b)
    ident_a = read_json(os.path.join(run_a, "identity.json"), {})
    ident_b = read_json(os.path.join(run_b, "identity.json"), {})
    man_a = read_json(os.path.join(run_a, "manifest.json"), {})
    man_b = read_json(os.path.join(run_b, "manifest.json"), {})
    for label, ident in (("A", ident_a), ("B", ident_b)):
        print("%s: %s | editor %s | HEAD %s | content %s (%s files)" % (
            label, ident.get("path"), ident.get("editorVersion"), (ident.get("headSha") or "")[:10],
            (ident.get("fingerprint") or "")[:12], ident.get("files")))

    offenders = manifest_diff(man_a, man_b, read_allow_list(args.allow_diff))
    if offenders:
        print("REFUSED: the two runs were made from different content. Differences outside the allow-list (%d):" % len(offenders))
        for p in offenders[:20]:
            print("  " + p)
        return 2

    req_a = read_json(os.path.join(run_a, "request.json"), {"scenarios": []})
    req_b = read_json(os.path.join(run_b, "request.json"), {"scenarios": []})
    keys_b = {s["key"] for s in req_b["scenarios"]}
    failed = False
    for s in req_a["scenarios"]:
        if s["key"] not in keys_b:
            print("  %s %s: not in B" % (s["kind"], s["id"]))
            continue
        fa, fb = list_frames(os.path.join(run_a, s["key"], "frames")), list_frames(os.path.join(run_b, s["key"], "frames"))
        na, nb, worst, tol = compare_series(fa, fb)
        verdict = "ok" if na == nb and worst < PIXEL_LIMIT_PERCENT else "DIFFERS"
        failed |= verdict != "ok"
        print("  %-6s %-22s frames %d vs %d | worst frame %.4f%% (%.4f%% past +-2) %s" % (
            s["kind"], s["id"], na, nb, worst, tol, verdict))
    return 1 if failed else 0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("prepare")
    p.add_argument("kind")
    p.add_argument("id")
    p.add_argument("--vs", default="")
    p.add_argument("--formation", default="")
    p.add_argument("--speed", type=float, default=1.0)
    p.add_argument("--prestatus", default="")
    p.add_argument("--skill", default="")
    p.add_argument("--crop", default="")
    p.add_argument("--seconds", type=float, default=0.0)
    p.add_argument("--request-id", default="")
    p.set_defaults(fn=cmd_prepare)

    p = sub.add_parser("finish")
    p.add_argument("run_dir")
    p.set_defaults(fn=cmd_finish)

    p = sub.add_parser("compare")
    p.add_argument("a")
    p.add_argument("b")
    p.add_argument("--allow-diff", default="")
    p.set_defaults(fn=cmd_compare)

    p = sub.add_parser("framediff")
    p.add_argument("a")
    p.add_argument("b")
    p.set_defaults(fn=cmd_framediff)

    args = parser.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
