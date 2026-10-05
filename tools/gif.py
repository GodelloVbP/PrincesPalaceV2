#!/usr/bin/env python3
"""The Python half of tools/gif.ps1 (docs/PLAN_GIF_CAPTURE.md).

tools/gif.ps1 owns the Unity launch; everything that needs no Unity lives here
so it can be tested alone (tools/gif_test.py):

    prepare   normalize a request (or a named manifest, tools/gif_manifests/)
              into scenarios, refuse unknown ids, make the immutable run
              directory, write request.json, unity_request.json, identity.json
              and manifest.json into it
    finish    turn the frames Unity wrote into anim.gif / strip.png, build the
              stance and sheet GIFs (no Unity), collect the still PNGs, and
              write scenario.json, latest.json and the coverage table, then
              delete frames/ of every run but the newest two (--keep-frames
              keeps them)
    stamp     re-fingerprint a prepared run from the Capture copy it renders from
    trim      the same deletion on its own (--keep <runs>)
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
import shutil
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

UNITY_KINDS = ("spell", "attack", "relic", "status")   # one GifBatchCaptureTests boot
PY_KINDS = ("stance", "sheet")                         # no Unity at all
KINDS = UNITY_KINDS + PY_KINDS + ("still",)            # still: a PlayMode capture class
FORMATIONS = ("lone", "full")
MAX_RACK = 3
MAX_PARTY = 3
CROPS = ("subject", "stage", "full")

RESOURCES = os.path.join(ROOT, "Assets", "_Project", "Resources")
PLAYMODE_TESTS = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Tests", "PlayMode")
MANIFESTS = os.path.join(TOOLS, "gif_manifests")

# The stance GIF shows each key pose this long, once round (plan 2.2).
STANCE_MS = 400
STANCE_ORDER = ("idle", "attack", "cast", "hurt", "defeated", "victory")
STANCE_PAD = 24
ALPHA_THRESHOLD = 8
BACKDROP = (24, 26, 32)

# A sheet GIF plays at the fps of the layer that uses the folder; a folder no
# sprite layer names has no authored rate, so this is stated and warned about.
SHEET_DEFAULT_FPS = 12.0

# The baseline manifest's spell matrix (plan 2.9).
SELF_SIDE = ("Self", "Party", "SingleAlly")

# The stage is StageCaptureRig's crop: FightStageAnchors.StageSize (1700x600)
# centred in the 1920x1080 reference canvas.
STAGE_BOX = (110, 240, 1810, 840)

ASPECT_HEIGHT = 1080  # -Aspect keeps the reference height and sets the width
GIF_WIDTH = 960
GIF_STEP = 2          # 60 fps captured, 30 fps shown
GIF_HOLD_MS = 500
# anim_preview.gif: for a phone. Narrow, fewer frames, a small palette; a
# capture that is still over the byte limit steps down the ladder below.
PREVIEW_WIDTH = 480
PREVIEW_MAX_BYTES = 5 * 1000 * 1000
PREVIEW_LADDER = ((2, 64), (2, 32), (3, 32), (4, 16), (6, 16))  # (frame step, palette colours)
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

def aspect_size(aspect, height=ASPECT_HEIGHT):
    """'4:3' -> (1440, 1080). '' -> (0, 0), the canvas default (1920x1080)."""
    if not aspect:
        return 0, 0
    m = re.fullmatch(r"(\d+):(\d+)", aspect.strip())
    if not m or int(m.group(1)) <= 0 or int(m.group(2)) <= 0:
        raise ValueError("aspect '%s' is not <w>:<h>, e.g. 4:3" % aspect)
    return int(round(height * int(m.group(1)) / int(m.group(2)))), height


def character_ids(content=None):
    """{lowercase id or displayName: id}, so -Party shawn,odette reads the way the owner says it."""
    out = {}
    for c in content if content is not None else load_content("characters.json", "characters"):
        out[c["id"].lower()] = c["id"]
        if c.get("displayName"):
            out[c["displayName"].lower()] = c["id"]
    return out


def csv_list(value):
    """'a, b' or ['a', 'b'] -> ['a', 'b']; empties dropped."""
    if isinstance(value, str):
        # PowerShell joins an unquoted `a,b` argument bound to a [string] with a space.
        value = re.split(r"[,\s]+", value)
    return [str(v).strip() for v in (value or []) if str(v).strip()]


def normalize(raw, skills=None, characters=None):
    """Fills every default and sorts the keys, so two requests that differ in
    any field never share a key (2.2)."""
    skills = skills if skills is not None else {s["id"]: s for s in load_content("skills.json", "skills")}
    kind = raw["kind"]
    raw_id = stance_actor(raw["id"]) if kind == "stance" else raw["id"]
    # -Vs rat,treant,beetle is a formation in slot order (front first); one id
    # keeps its meaning. The party and the target are the owner's words for
    # characters, resolved to ids here so a key never depends on the spelling.
    vs_list = csv_list(raw.get("vs")) or ["rat"]
    party_in = csv_list(raw.get("party"))
    target_in = str(raw.get("target") or "").strip().lower()
    if party_in or target_in:
        names = characters if characters is not None else character_ids()
        party_in = [names.get(p.lower(), p) for p in party_in]
        target_in = names.get(target_in, target_in)
    out = {
        "kind": kind,
        "id": raw_id,
        "vs": ",".join(vs_list),
        "party": ",".join(party_in),
        "target": target_in,
        "formation": raw.get("formation") or "",
        "speed": float(raw.get("speed") or 1.0),
        "preStatus": raw.get("preStatus") or "",
        "skill": raw.get("skill") or "",
        "crop": raw.get("crop") or "",
        "seconds": float(raw.get("seconds") or 0.0),
        # 0 x 0 is the canvas default; the runner passes both to CanvasCapture.
        "width": int(raw.get("width") or 0),
        "height": int(raw.get("height") or 0),
        "repeat": int(raw.get("repeat") or 1),
        "darkness": float(raw.get("darkness") or 0.0),
    }
    if raw.get("aspect") and kind in UNITY_KINDS:
        # Stance, sheet and still have no canvas to resize; their size is the art's.
        out["width"], out["height"] = aspect_size(raw["aspect"])
        if not out["crop"]:
            out["crop"] = "full"
    if kind not in UNITY_KINDS:
        # Python and still scenarios have no fight: the fight fields are pinned
        # so the key depends only on what the scenario is.
        out.update({"vs": "", "party": "", "target": "", "formation": "", "speed": 1.0, "preStatus": "", "skill": "", "seconds": 0.0,
                    "width": 0, "height": 0, "repeat": 1, "darkness": 0.0})
        if not out["crop"]:
            out["crop"] = "subject" if kind == "stance" else "full"
        out["key"] = scenario_key(out)
        return dict(sorted(out.items()))
    if len(vs_list) > 1 and not out["formation"]:
        out["formation"] = "full"
    if not out["formation"]:
        # An all-target spell is only visible against a rack.
        aims_all = kind == "spell" and skills.get(out["id"], {}).get("targeting") == "AllEnemies"
        out["formation"] = "full" if aims_all else "lone"
    if not out["crop"]:
        out["crop"] = "subject" if kind == "status" else "stage"
    if kind == "status" and out["seconds"] <= 0:
        out["seconds"] = 2.0
    if kind not in ("status", "spell"):
        # A spell's seconds is a floor on the film's length: the fight plays on
        # after the cast so a status can be filmed running out.
        out["seconds"] = 0.0
    out["key"] = scenario_key(out)
    return dict(sorted(out.items()))


# Fields added after the first baseline; a default value stays out of the key
# so every scenario recorded before them keeps its key.
KEY_DEFAULTS = {"width": 0, "height": 0, "repeat": 1, "darkness": 0.0, "party": "", "target": ""}


def scenario_key(scenario):
    body = {k: v for k, v in scenario.items() if k != "key" and KEY_DEFAULTS.get(k, object()) != v}
    blob = json.dumps(body, sort_keys=True, separators=(",", ":"))
    return hashlib.sha1(blob.encode("utf-8")).hexdigest()[:10]


def stance_actors(resources=RESOURCES):
    """'Characters/<id>' and 'Enemies/<id>' for every folder holding an idle still."""
    out = []
    for root in ("Characters", "Enemies"):
        base = os.path.join(resources, root)
        if not os.path.isdir(base):
            continue
        for name in sorted(os.listdir(base)):
            if os.path.isfile(os.path.join(base, name, "idle.png")):
                out.append("%s/%s" % (root, name))
    return out


def stance_actor(raw_id, resources=RESOURCES):
    """'beetle' -> 'Enemies/beetle'; a full 'Characters/sheep' is kept."""
    raw_id = raw_id.replace("\\", "/").strip("/")
    if "/" in raw_id:
        return raw_id
    for root in ("Enemies", "Characters"):
        if os.path.isdir(os.path.join(resources, root, raw_id)):
            return "%s/%s" % (root, raw_id)
    return raw_id


def sheet_folders(resources=RESOURCES):
    base = os.path.join(resources, "Spells")
    if not os.path.isdir(base):
        return []
    out = []
    for name in sorted(os.listdir(base)):
        folder = os.path.join(base, name)
        if os.path.isdir(folder) and any(f.lower().endswith(".png") for f in os.listdir(folder)):
            out.append(name)
    return out


def still_classes(tests=PLAYMODE_TESTS):
    """Every PlayMode capture class, by class name (the file stem)."""
    out = []
    for _folder, _dirs, files in os.walk(tests):
        for name in files:
            if name.endswith("CaptureTests.cs"):
                out.append(name[:-3])
    return sorted(out)


def caster_of(skill, characters):
    """Who the preview fight casts a skill as, mirroring PreviewFight.ChooseCaster:
    the authored owner when the roster has them, else (for a skill that spends a
    resource) the first character carrying the owner's signature resource, else
    the roster lead with battle art. '' when no character can cast it."""
    owner = skill.get("characterId") or ""
    if owner and any(c["id"] == owner for c in characters):
        return owner
    if (skill.get("resourceCost") or 0) > 0:
        owned = next((c for c in characters if c["id"] == owner), None)
        wanted = (owned or {}).get("signatureId") or None
        payer = next((c for c in characters if c.get("signatureId") and (wanted is None or c["signatureId"] == wanted)), None)
        return payer["id"] if payer else ""
    lead = next((c for c in characters if c.get("battleSpritePath")), None) or (characters[0] if characters else None)
    return lead["id"] if lead else ""


def needs_depth(skill):
    """True when the skill's reach excludes the front seat (reachSlots without 1),
    so the runner fields a rack of three behind which it can land."""
    slots = skill.get("reachSlots") or []
    return bool(slots) and 1 not in slots


def known_ids():
    skills = load_content("skills.json", "skills")
    enemies = load_content("enemies.json", "enemies")
    relics = load_content("relics.json", "relics")
    enemy_owned = {a["skillId"] for e in enemies for a in e.get("abilities", []) or []}
    vfx_skills = {s["id"] for s in skills if s.get("vfx")}
    characters = load_content("characters.json", "characters")
    return {
        "spell": [s["id"] for s in skills if s.get("vfx")],
        "playerSpell": [s["id"] for s in skills if s.get("vfx") and s["id"] not in enemy_owned],
        "enemyVfxAbility": sorted(s["id"] for s in skills if s.get("vfx") and s["id"] in enemy_owned),
        "abilityPairs": [[e["id"], a["skillId"]] for e in enemies if e.get("spritePath")
                         for a in e.get("abilities", []) or [] if a["skillId"] in vfx_skills],
        "targeting": {s["id"]: s.get("targeting") or "" for s in skills},
        "casters": {s["id"]: caster_of(s, characters) for s in skills},
        "needsDepth": {s["id"]: needs_depth(s) for s in skills},
        "characters": [c["id"] for c in characters],
        "stance": stance_actors(),
        "sheet": sheet_folders(),
        "stillClasses": still_classes(),
        "allSkills": [s["id"] for s in skills],
        "attack": [e["id"] for e in enemies if e.get("spritePath")],
        "enemies": [e["id"] for e in enemies],
        "enemyAbilities": {e["id"]: [a["skillId"] for a in e.get("abilities", []) or []] for e in enemies},
        "relic": [r["id"] for r in relics if r.get("vfx")],
        "allRelics": [r["id"] for r in relics],
        "status": status_names(),
    }


def aim_suffix(s):
    """' party=... target=...' for a scenario that sets them, so two casts differing only in aim read apart."""
    return "".join(" %s=%s" % (k, s[k]) for k in ("party", "target") if s.get(k))


def formation_refusals(scenario, ids):
    """The enemy rack, the party and the aimed target of a Unity scenario."""
    out = []
    foes = csv_list(scenario["vs"])
    party = csv_list(scenario["party"])
    target = scenario["target"]
    kind = scenario["kind"]
    if len(foes) > MAX_RACK:
        out.append("vs lists %d enemies; the rack holds %d" % (len(foes), MAX_RACK))
    if len(foes) > 1 and scenario["formation"] == "lone":
        out.append("vs lists %d enemies, which is not a lone formation" % len(foes))
    if len(foes) > 1 and kind != "spell":
        out.append("a vs list is a spell's formation; %s fields one enemy (use -Formation full for three)" % kind)
    if (party or target) and kind != "spell":
        out.append("party and target aim a spell; %s has neither" % kind)
        return out
    if kind != "spell":
        return out
    known = ids.get("characters", [])
    for member in party:
        if known and member not in known:
            out.append("party member '%s' is not a character; characters: %s" % (member, ", ".join(known)))
    if len(set(party)) != len(party):
        out.append("party names a character twice")
    if len(party) > MAX_PARTY:
        out.append("party lists %d characters; a squad is %d" % (len(party), MAX_PARTY))
    caster = ids.get("casters", {}).get(scenario["id"], "")
    # The runner asserts CasterId == party[0] after a Unity boot; the caster is
    # resolved here the way PreviewFight does, so a wrong lead costs no boot.
    if party and caster and party[0] != caster:
        out.append("party leads with its caster: '%s' casts %s, so it goes first" % (caster, scenario["id"]))
    aim = ids.get("targeting", {}).get(scenario["id"], "")
    if not target:
        return out
    if aim == "SingleAlly":
        # With no -Party the squad is the preview stage's, which only the
        # runner knows; the named ally must at least be a character.
        if party and target not in party:
            out.append("target '%s' is not in the party (%s)" % (target, ",".join(party)))
        elif not party and known and target not in known:
            out.append("target '%s' is not a character; characters: %s" % (target, ", ".join(known)))
    elif aim in ("AllEnemies", "Self", "Party"):
        out.append("%s aims at %s; it has no target to pick" % (scenario["id"], aim))
    else:
        # The runner fields three when the rack is full or the skill's reach
        # excludes the front seat; a lone body otherwise.
        deep = scenario["formation"] == "full" or ids.get("needsDepth", {}).get(scenario["id"], False)
        rack = len(foes) if len(foes) > 1 else (3 if deep else 1)
        if not target.isdigit() or not 1 <= int(target) <= rack:
            out.append("target '%s' is not an enemy slot 1..%d (1 is the front)" % (target, rack))
    return out


def refusals(scenario, ids=None):
    """Why a scenario cannot be launched, checked against the content JSON so a
    bad id costs no Unity boot."""
    ids = ids or known_ids()
    kind, sid = scenario["kind"], scenario["id"]
    out = []
    if kind not in KINDS:
        return ["kind '%s' is not one of %s" % (kind, ", ".join(KINDS))]
    if kind in UNITY_KINDS:
        if scenario["formation"] not in FORMATIONS:
            out.append("formation '%s' is not lone or full" % scenario["formation"])
        if scenario["speed"] <= 0:
            out.append("speed must be positive")
        if (scenario["width"] > 0) != (scenario["height"] > 0):
            out.append("width and height are set together")
        if scenario["width"] > 0 and scenario["crop"] != "full":
            out.append("the stage crop is the 1920x1080 layout's; a custom aspect records crop full")
        if scenario["repeat"] < 1:
            out.append("repeat must be at least 1")
        if scenario["repeat"] > 1 and kind != "spell":
            out.append("repeat casts a spell; %s has nothing to repeat" % kind)
        if not 0.0 <= scenario["darkness"] <= 1.0:
            out.append("darkness is an opacity from 0 to 1")
        out.extend(formation_refusals(scenario, ids))
    if scenario["crop"] not in CROPS:
        out.append("crop '%s' is not one of %s" % (scenario["crop"], ", ".join(CROPS)))
    if kind in ("sheet", "still") and scenario["crop"] != "full":
        out.append("%s has no stage to crop; crop is always full" % kind)
    if kind == "stance":
        if scenario["crop"] == "stage":
            out.append("stance has no stage; crop is subject or full")
        if sid not in ids.get("stance", []):
            out.append("stance actor '%s' has no stills; actors: %s" % (sid, ", ".join(ids.get("stance", []))))
    elif kind == "sheet":
        if sid not in ids.get("sheet", []):
            out.append("no frame folder Resources/Spells/%s" % sid)
    elif kind == "still":
        cls, _, name = sid.partition("/")
        if not name.lower().endswith(".png"):
            out.append("still id is '<CaptureClass>/<file>.png', got '%s'" % sid)
        if cls not in ids.get("stillClasses", []):
            out.append("'%s' is not a PlayMode capture class; classes: %s" % (cls, ", ".join(ids.get("stillClasses", []))))
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
    if kind in ("spell", "relic", "status"):
        for foe in csv_list(scenario["vs"]):
            if foe not in ids["enemies"]:
                out.append("vs '%s' is not in enemies.json" % foe)
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


def content_manifest(root=ROOT, use_git=True):
    """{relative path: git blob hash} for every file under Assets, Packages and
    ProjectSettings. Clean tracked files take the index's hash; modified and
    untracked files are hashed from their contents. Sizes and mtimes are never
    used. use_git=False hashes every file, for a tree that is not a checkout
    (the Capture copy)."""
    tracked, modified = {}, set()
    if use_git:
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


def identity(root=ROOT, manifest=None, head_root=None):
    """head_root names the checkout whose HEAD describes root when root is a
    mirror of it rather than a checkout itself."""
    manifest = manifest if manifest is not None else content_manifest(root)
    return {
        "path": os.path.normcase(os.path.realpath(root)),
        "editorVersion": editor_version(root),
        "headSha": git("rev-parse", "HEAD", root=head_root or root).decode("ascii").strip(),
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
    temp = "%s.%d.tmp" % (path, os.getpid())
    with open(temp, "w", encoding="utf-8") as handle:
        json.dump(data, handle, indent=2, sort_keys=True)
    replace_retrying(temp, path)


def replace_retrying(temp, path, attempts=50):
    """os.replace that rides out Windows' transient PermissionError, raised
    while another handle (a scanner, a reader) briefly holds the target."""
    for attempt in range(attempts):
        try:
            os.replace(temp, path)
            return
        except PermissionError:
            if attempt == attempts - 1:
                raise
            time.sleep(0.02)


def update_json_locked(path, updates, wait=30.0, stale=60.0):
    """Merges `updates` into the JSON object at `path` under a lock file, so
    concurrent sessions each keep their entries. The lock is a file created
    exclusively; one older than `stale` seconds is a crashed holder's and is
    taken over."""
    lock = path + ".lock"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    deadline = time.time() + wait
    while True:
        try:
            os.close(os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY))
            break
        except (FileExistsError, PermissionError):
            # Windows reports a lock whose delete is still pending as
            # PermissionError, not FileExistsError: both mean "held, retry".
            try:
                if time.time() - os.path.getmtime(lock) > stale:
                    os.remove(lock)
                    continue
            except OSError:
                continue
            if time.time() > deadline:
                raise TimeoutError("could not lock %s within %ds" % (path, wait))
            time.sleep(0.05)
    try:
        merged = read_json(path, {})
        merged.update(updates)
        write_json_atomic(path, merged)
    finally:
        try:
            os.remove(lock)
        except OSError:
            pass


def read_json(path, default=None):
    try:
        with open(path, encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return default


# ---- manifests (2.2, 2.9) ----------------------------------------------------

def spell_matrix(spell_id, targeting, vs_list, formations, speeds):
    """(scenarios, skipped) for one spell across the baseline dimensions.

    A combination is skipped, and named in the coverage table, when the spell's
    targeting makes it show nothing new:
      AllEnemies  needs a rack, so `lone` is skipped.
      Self, Party, SingleAlly  hit the caster's own side; the enemy and the
        formation do not change what is drawn, so only the first enemy, lone.
    """
    kept, skipped = [], []
    for vs in vs_list:
        for formation in formations:
            for speed in speeds:
                combo = {"vs": vs, "formation": formation, "speed": float(speed)}
                reason = ""
                if targeting == "AllEnemies" and formation == "lone":
                    reason = "an all-enemy spell needs a rack"
                elif targeting in SELF_SIDE and (vs != vs_list[0] or formation != "lone"):
                    reason = "%s targets its own side; enemy and formation do not change the picture" % targeting
                if reason:
                    skipped.append(dict(combo, spell=spell_id, reason=reason))
                else:
                    kept.append(combo)
    return kept, skipped


def load_manifest(name):
    path = name if os.path.isfile(name) else os.path.join(MANIFESTS, name + ".json")
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def expand_manifest(spec, ids=None, skills=None):
    """A manifest's dimensions into normalized scenarios.

    Returns {"scenarios": [...], "skipped": [...], "gaps": [...]}: the skipped
    list is spell combinations a targeting rule removed, the gaps are the
    manifest's own `notCovered` entries (cases that do not exist yet).
    """
    ids = ids or known_ids()
    skills = skills if skills is not None else {s["id"]: s for s in load_content("skills.json", "skills")}
    raws, skipped = [], []

    block = spec.get("spells")
    if block:
        chosen = block.get("ids", "all")
        spell_ids = ids["playerSpell"] if chosen == "all" else list(chosen)
        vs_list = block.get("vs", ["rat", "treant"])
        formations = block.get("formations", ["lone", "full"])
        speeds = block.get("speeds", [1.0, 1.5])
        for sid in spell_ids:
            kept, dropped = spell_matrix(sid, ids["targeting"].get(sid, ""), vs_list, formations, speeds)
            skipped.extend(dropped)
            for combo in kept:
                raws.append(dict(combo, kind="spell", id=sid))

    # Scenarios written out whole, for a set that is not a matrix (the
    # spell-rework before/after captures: darkness, repeat, a pre-status).
    for raw in spec.get("scenarios", []) or []:
        raws.append(dict(raw))

    if spec.get("abilities") == "all":
        for enemy, skill_id in ids["abilityPairs"]:
            raws.append({"kind": "attack", "id": enemy, "skill": skill_id})

    for kind, key, pool in (("attack", "attacks", "attack"), ("relic", "relics", "relic"),
                            ("status", "statuses", "status"), ("stance", "stances", "stance"),
                            ("sheet", "sheets", "sheet")):
        chosen = spec.get(key)
        if not chosen:
            continue
        for sid in (ids[pool] if chosen == "all" else list(chosen)):
            raws.append({"kind": kind, "id": sid})

    for entry in spec.get("stills", []) or []:
        for name in entry["files"]:
            raws.append({"kind": "still", "id": "%s/%s" % (entry["class"], name)})

    scenarios, seen = [], set()
    for raw in raws:
        scenario = normalize(raw, skills)
        if scenario["key"] not in seen:
            seen.add(scenario["key"])
            scenarios.append(scenario)
    return {"scenarios": scenarios, "skipped": skipped, "gaps": list(spec.get("notCovered", []) or [])}


# ---- prepare -----------------------------------------------------------------

def cmd_prepare(args):
    ids = known_ids()
    skipped, gaps, manifest_name = [], [], ""
    if args.manifest:
        spec = load_manifest(args.manifest)
        try:
            expanded = expand_manifest(spec, ids)
        except ValueError as e:  # a bad aspect or number in a manifest row
            print("REFUSED manifest %s: %s" % (args.manifest, e), file=sys.stderr)
            return 2
        scenarios, skipped, gaps = expanded["scenarios"], expanded["skipped"], expanded["gaps"]
        manifest_name = spec.get("name") or args.manifest
    else:
        if not args.kind or not args.id:
            print("prepare needs <kind> <id> or --manifest <name>", file=sys.stderr)
            return 2
        raws = [{"kind": args.kind, "id": args.id, "vs": args.vs, "formation": args.formation,
                 "speed": args.speed, "preStatus": args.prestatus, "skill": args.skill,
                 "crop": args.crop, "seconds": args.seconds, "aspect": args.aspect,
                 "repeat": args.repeat, "darkness": args.darkness,
                 "party": args.party, "target": args.target}]
        try:
            scenarios = [normalize(r) for r in raws]
        except ValueError as e:
            print("REFUSED %s" % e, file=sys.stderr)
            return 2
    problems = [(s["kind"], s["id"], m) for s in scenarios for m in refusals(s, ids)]
    if problems:
        for kind, sid, message in problems[:30]:
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
                      {"requestId": request_id, "outDir": run_dir, "scenarios": scenarios,
                       "manifest": manifest_name, "skipped": skipped, "gaps": gaps})
    # GifBatchCaptureTests runs every scenario of the file it is given as a
    # fight, so it gets only the fight kinds.
    unity = [s for s in scenarios if s["kind"] in UNITY_KINDS]
    if unity:
        write_json_atomic(os.path.join(run_dir, "unity_request.json"),
                          {"requestId": request_id, "outDir": run_dir, "scenarios": unity})
    still_classes_needed = sorted({s["id"].split("/", 1)[0] for s in scenarios if s["kind"] == "still"})
    print(json.dumps({"requestId": request_id, "runDir": run_dir, "scenarios": len(scenarios),
                      "unity": len(unity), "stillClasses": still_classes_needed,
                      "fingerprint": ident["fingerprint"]}))
    return 0


def cmd_stamp(args):
    """Re-fingerprints a prepared run from the tree Unity will actually render,
    the Capture copy after its sync (or as it stands under -SkipSync). prepare
    fingerprints the source tree, which can change during a claim wait and is
    not what -SkipSync renders. A missing tree leaves a fingerprint-less
    identity, which compare refuses."""
    run_dir = os.path.abspath(args.run_dir)
    root = os.path.abspath(args.root)
    if not os.path.isfile(os.path.join(root, "ProjectSettings", "ProjectVersion.txt")):
        write_json_atomic(os.path.join(run_dir, "identity.json"), {"path": root, "fingerprint": "", "files": 0})
        print("stamp: %s holds no project; the run cannot be compared" % root, file=sys.stderr)
        return 0
    manifest = content_manifest(root, use_git=False)
    write_json_atomic(os.path.join(run_dir, "manifest.json"), manifest)
    write_json_atomic(os.path.join(run_dir, "identity.json"), identity(root, manifest, head_root=ROOT))
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


def write_preview(shown, interval_ms, out_path, max_bytes=PREVIEW_MAX_BYTES, width=PREVIEW_WIDTH, hold_last_ms=0, drop_frames=True):
    """The phone copy of a capture: at most `width` px wide, under `max_bytes`.
    Drops frames (unless `drop_frames` is off: stance poses must all show) and
    palette colours until it fits; timing stays true because write_gif
    accumulates the longer interval. Returns the bytes written."""
    import capture_strip

    width = min(width, shown[0].size[0])
    size = 0
    for step, colors in PREVIEW_LADDER:
        capture_strip.write_gif(shown, {"captureIntervalMs": interval_ms}, out_path, width=width, colors=colors,
                                hold_last_ms=hold_last_ms, step=step if drop_frames else 1, disposal=0)
        size = os.path.getsize(out_path)
        if size < max_bytes:
            break
    return size


def encode(scenario_dir, scenario, timing, preview=True):
    """anim.gif, anim_preview.gif and strip.png from frames/. Returns encode seconds."""
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
    if preview:
        write_preview(shown, interval, os.path.join(scenario_dir, "anim_preview.gif"), hold_last_ms=GIF_HOLD_MS)

    hit = timing.get("hitFrame", -1)
    strip_timing = {"captureIntervalMs": interval, "impactFrame": hit // GIF_STEP if hit >= 0 else -1}
    title = "%s %s | %d frames @ %dfps" % (scenario["kind"], scenario["id"], len(paths), CAPTURE_FPS)
    capture_strip.build_strip(shown, strip_timing, title).save(os.path.join(scenario_dir, "strip.png"))
    return time.perf_counter() - started


# ---- stance, sheet and still: no fight to record -----------------------------

BASELINE_VS = ("rat", "treant")
BASELINE_FORMATIONS = ("lone", "full")
BASELINE_SPEEDS = (1.0, 1.5)


def natural_key(name):
    return [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", name)]


def stance_order(names):
    """The key poses in the order a fight meets them (idle, attack, cast, hurt,
    defeated, victory), then any other stance by name."""
    known = [n for n in STANCE_ORDER if n in names]
    return known + sorted(n for n in names if n not in STANCE_ORDER)


def flatten(path, backdrop=BACKDROP):
    """(RGB over the backdrop, alpha) of one transparent PNG."""
    from PIL import Image
    with Image.open(path) as im:
        rgba = im.convert("RGBA")
    flat = Image.new("RGBA", rgba.size, tuple(backdrop) + (255,))
    flat.alpha_composite(rgba)
    return flat.convert("RGB"), rgba.getchannel("A")


def alpha_box(alpha):
    return alpha.point(lambda v: 255 if v > ALPHA_THRESHOLD else 0).getbbox()


def union_box(boxes):
    boxes = [b for b in boxes if b]
    if not boxes:
        return None
    return (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))


def padded(box, size, pad):
    return (max(0, box[0] - pad), max(0, box[1] - pad), min(size[0], box[2] + pad), min(size[1], box[3] + pad))


def write_series(scenario_dir, shown, interval_ms, title, preview=True):
    """frames/, anim.gif and strip.png for a list of RGB images: the one writer
    the stance and sheet kinds share with the Unity kinds' encoder."""
    import capture_strip

    frames_dir = os.path.join(scenario_dir, "frames")
    os.makedirs(frames_dir, exist_ok=True)
    for i, image in enumerate(shown):
        image.save(os.path.join(frames_dir, "f%04d.png" % i))
    width = min(GIF_WIDTH, shown[0].size[0])
    capture_strip.write_gif(shown, {"captureIntervalMs": interval_ms}, os.path.join(scenario_dir, "anim.gif"),
                            width=width, colors=None, hold_last_ms=0, step=1, disposal=0)
    if preview:
        write_preview(shown, interval_ms, os.path.join(scenario_dir, "anim_preview.gif"), drop_frames=False)
    capture_strip.build_strip(shown, {"captureIntervalMs": interval_ms, "impactFrame": -1}, title).save(
        os.path.join(scenario_dir, "strip.png"))


def render_stance(scenario, scenario_dir, resources=RESOURCES):
    """One loop of an actor's key-pose stills, STANCE_MS each, on their shared
    canvas. `subject` crops to where any stance has ink, so the box is the same
    for every still and the actor does not jump between poses."""
    actor_dir = os.path.join(resources, *scenario["id"].split("/"))
    names = stance_order([n[:-4] for n in os.listdir(actor_dir) if n.lower().endswith(".png")])
    if not names:
        raise ValueError("no stance stills in %s" % actor_dir)
    flats, boxes, sizes = [], [], set()
    for name in names:
        flat, alpha = flatten(os.path.join(actor_dir, name + ".png"))
        flats.append(flat)
        boxes.append(alpha_box(alpha))
        sizes.add(flat.size)
    if len(sizes) != 1:
        raise ValueError("stances of %s are not on one shared canvas: %s" % (scenario["id"], sorted(sizes)))
    size = flats[0].size
    box = (0, 0, size[0], size[1])
    if scenario["crop"] == "subject":
        union = union_box(boxes)
        if union:
            box = padded(union, size, STANCE_PAD)
    shown = [f.crop(box) for f in flats]
    write_series(scenario_dir, shown, STANCE_MS, "stance %s | %d stills @ %dms" % (scenario["id"], len(shown), STANCE_MS))
    return {"frames": len(shown), "order": names, "cropBox": list(box), "frameMs": STANCE_MS, "canvas": list(size)}


def sheet_fps(folder, frame_count, skills, relics):
    """(fps, source, warning) for a Resources/Spells folder: the rate of the
    first sprite layer in skills.json or relics.json that plays it, else the
    default with a warning."""
    path = "Spells/" + folder
    for owner in list(skills) + list(relics):
        vfx = owner.get("vfx") or {}
        for layer in vfx.get("layers", []) or []:
            if layer.get("render") != "sprite" or layer.get("path") != path:
                continue
            fps = float(layer.get("fps") or 0)
            if fps > 0:
                return fps, "%s layer '%s' fps" % (owner["id"], layer.get("id")), ""
            seconds = float(layer.get("seconds") or 0)
            if seconds > 0:
                return frame_count / seconds, "%s layer '%s': %d frames over %gs" % (
                    owner["id"], layer.get("id"), frame_count, seconds), ""
    return SHEET_DEFAULT_FPS, "default", "no sprite layer plays Spells/%s; using the default %g fps" % (folder, SHEET_DEFAULT_FPS)


def render_sheet(scenario, scenario_dir, resources=RESOURCES, skills=None, relics=None):
    skills = skills if skills is not None else load_content("skills.json", "skills")
    relics = relics if relics is not None else load_content("relics.json", "relics")
    folder = os.path.join(resources, "Spells", scenario["id"])
    pngs = [n for n in os.listdir(folder) if n.lower().endswith(".png")]
    numbered = [n for n in pngs if re.fullmatch(r"f\d+\.png", n, re.I)]
    names = sorted(numbered or pngs, key=natural_key)
    if not names:
        raise ValueError("no frames in %s" % folder)
    fps, source, warning = sheet_fps(scenario["id"], len(names), skills, relics)
    if warning:
        print("  WARNING sheet %s: %s" % (scenario["id"], warning))
    shown = [flatten(os.path.join(folder, n))[0] for n in names]
    interval = 1000.0 / fps
    write_series(scenario_dir, shown, interval, "sheet %s | %d frames @ %.1ffps" % (scenario["id"], len(shown), fps))
    return {"frames": len(shown), "fps": fps, "fpsSource": source, "warning": warning, "order": names}


def find_still(run_dir, scenario):
    """The PNG a capture class wrote for this scenario, in the run's still_raw
    tree (the launcher moves the Capture copy's output there on exit)."""
    wanted = scenario["id"].split("/", 1)[1].lower()
    hits = []
    for folder, _dirs, files in os.walk(os.path.join(run_dir, "still_raw")):
        for name in files:
            if name.lower() == wanted:
                hits.append(os.path.join(folder, name))
    return sorted(hits)[0] if hits else None


def collect_still(run_dir, scenario, scenario_dir):
    source = find_still(run_dir, scenario)
    if not source:
        raise ValueError("%s wrote no %s" % (scenario["id"].split("/", 1)[0], scenario["id"].split("/", 1)[1]))
    os.makedirs(os.path.join(scenario_dir, "frames"), exist_ok=True)
    shutil.copyfile(source, os.path.join(scenario_dir, "frames", "f0000.png"))
    shutil.copyfile(source, os.path.join(scenario_dir, "still.png"))
    return {"frames": 1, "label": "still/" + scenario["id"]}


# ---- coverage (2.9) ----------------------------------------------------------

def _names(items, limit=12):
    items = list(items)
    return ", ".join(items[:limit]) + (" ... (+%d)" % (len(items) - limit) if len(items) > limit else "")


def coverage_table(scenarios, outcomes, ids=None, skipped=(), gaps=(), manifest=""):
    ids = ids or known_ids()
    passed = sum(1 for s in scenarios if outcomes.get(s["key"]))
    lines = ["COVERAGE" + (" (manifest %s)" % manifest if manifest else ""),
             "  requested %d scenario(s): %d passed, %d failed" % (len(scenarios), passed, len(scenarios) - passed)]
    for kind in KINDS:
        of_kind = [s for s in scenarios if s["kind"] == kind]
        if of_kind:
            lines.append("    %-6s %3d scenario(s), %d ok" % (kind, len(of_kind), sum(1 for s in of_kind if outcomes.get(s["key"]))))
    for s in scenarios:
        if not outcomes.get(s["key"]):
            lines.append("    FAILED %-6s %s vs=%s %s x%g%s" % (s["kind"], s["id"], s["vs"] or "-", s["formation"] or "-", s["speed"], aim_suffix(s)))
    if len(scenarios) <= 12:
        for s in scenarios:
            if outcomes.get(s["key"]):
                lines.append("    ok     %-6s %-22s vs=%-6s %-4s x%-4g%s" % (s["kind"], s["id"], s["vs"] or "-", s["formation"] or "-", s["speed"], aim_suffix(s)))

    lines.append("  known but not covered:")
    pools = {"spell": ids.get("playerSpell", ids["spell"]), "attack": ids["attack"], "relic": ids["relic"],
             "status": ids["status"], "stance": ids.get("stance", []), "sheet": ids.get("sheet", [])}
    for kind, pool in pools.items():
        have = {s["id"] for s in scenarios if s["kind"] == kind}
        missing = [i for i in pool if i not in have]
        lines.append("    %-6s %d of %d not requested%s" % (kind, len(missing), len(pool), ": " + _names(missing) if missing else ""))
    if manifest:
        classes_have = {s["id"].split("/", 1)[0] for s in scenarios if s["kind"] == "still"}
        missing_classes = [c for c in ids.get("stillClasses", []) if c not in classes_have]
        lines.append("    still  %d of %d capture classes have no requested still%s" % (
            len(missing_classes), len(ids.get("stillClasses", [])), ": " + _names(missing_classes, 40) if missing_classes else ""))

    have_attack = {(s["id"], s["skill"]) for s in scenarios if s["kind"] == "attack"}
    unrecorded = ["%s -Skill %s" % (e, k) for e, k in ids.get("abilityPairs", []) if (e, k) not in have_attack]
    if unrecorded:
        lines.append("    enemy abilities with vfx not recorded as attacks: %s" % _names(unrecorded))

    requested_spells = sorted({s["id"] for s in scenarios if s["kind"] == "spell"})
    for sid in requested_spells:
        want, _drop = spell_matrix(sid, ids.get("targeting", {}).get(sid, ""), BASELINE_VS, BASELINE_FORMATIONS, BASELINE_SPEEDS)
        have = {(s["vs"], s["formation"], s["speed"]) for s in scenarios if s["kind"] == "spell" and s["id"] == sid}
        absent = ["%s/%s/%gx" % (c["vs"], c["formation"], c["speed"]) for c in want if (c["vs"], c["formation"], c["speed"]) not in have]
        if absent:
            lines.append("    spell %s: baseline combinations not recorded: %s" % (sid, _names(absent, 8)))

    by_spell = {}
    for entry in skipped:
        by_spell.setdefault((entry["spell"], entry["reason"]), []).append("%s/%s/%gx" % (entry["vs"], entry["formation"], entry["speed"]))
    if by_spell:
        lines.append("  skipped by targeting (not meaningful, so not recorded):")
        for (spell, reason), combos in sorted(by_spell.items()):
            lines.append("    %s (%d): %s -- %s" % (spell, len(combos), _names(combos, 4), reason))

    if gaps:
        lines.append("  known gaps, not covered:")
        for gap in gaps:
            lines.append("    %s: %s" % (gap.get("name", "?"), gap.get("why", "")))
    return "\n".join(lines)


# ---- finish ------------------------------------------------------------------

def run_python_scenario(run_dir, scenario, sdir):
    """Builds a stance, sheet or still scenario's outputs and returns its
    result record. Never raises: a bad scenario is a failed row, as in the
    Unity batch."""
    started = time.perf_counter()
    try:
        if scenario["kind"] == "stance":
            result = render_stance(scenario, sdir)
        elif scenario["kind"] == "sheet":
            result = render_sheet(scenario, sdir)
        else:
            result = collect_still(run_dir, scenario, sdir)
        result.update({"ok": True, "key": scenario["key"], "kind": scenario["kind"], "id": scenario["id"]})
    except Exception as error:  # noqa: BLE001 -- recorded, the run goes on
        result = {"ok": False, "key": scenario["key"], "kind": scenario["kind"], "id": scenario["id"],
                  "error": "%s: %s" % (type(error).__name__, error), "frames": 0}
    result["seconds"] = round(time.perf_counter() - started, 3)
    write_json_atomic(os.path.join(sdir, "result.json"), result)
    return result


# Frames are the bulk of a run (a 1920x1080 PNG each, thousands per run) and
# regenerable; anim.gif, strip.png and the JSON are what a reader keeps. Only
# the newest runs keep theirs, which is all -Compare and -Determinism need.
FRAME_RUNS_KEPT = 2
# A run whose scenarios have not all written scenario.json is still recording
# (python-only requests finish in seconds, a Unity run in minutes), so its
# frames stay until it is a day stale. A finished run is also left alone for a
# few minutes: -Determinism's second boot and the -Compare right after a
# record read the run that finished just before.
FRAME_GRACE_SECONDS = 600
UNFINISHED_STALE_SECONDS = 86400
KEEP_FRAMES_MARKER = "keep_frames"  # written by `finish --keep-frames`; trim never touches that run


def run_state(run):
    """(finished, newest mtime) of a run directory. Finished means every
    scenario of its request has a scenario.json; no readable request is unfinished."""
    request = read_json(os.path.join(run, "request.json"), None)
    newest = 0.0
    try:
        newest = os.path.getmtime(os.path.join(run, "request.json"))
    except OSError:
        pass
    if not request or not request.get("scenarios"):
        return False, newest
    finished = True
    for s in request["scenarios"]:
        record = os.path.join(run, s["key"], "scenario.json")
        try:
            newest = max(newest, os.path.getmtime(record))
        except OSError:
            finished = False
    return finished, newest


def run_has_frames(run):
    return any(os.path.isdir(os.path.join(run, d, "frames")) for d in os.listdir(run) if os.path.isdir(os.path.join(run, d)))


def trim_frames(runs_root=None, keep=FRAME_RUNS_KEPT, now=None):
    """Deletes frames/ under every run but the newest `keep` that still have
    frames, skipping runs still recording, runs younger than the grace period
    and runs marked keep_frames. Returns (runs trimmed, bytes freed). Run
    directories sort by their UTC timestamp prefix. A frame folder that cannot
    be removed (an open Explorer preview) is reported and left."""
    runs_root = runs_root or RUNS
    if not os.path.isdir(runs_root):
        return 0, 0
    now = time.time() if now is None else now
    runs = sorted((d for d in os.listdir(runs_root)
                   if os.path.isdir(os.path.join(runs_root, d)) and run_has_frames(os.path.join(runs_root, d))), reverse=True)
    trimmed, freed = 0, 0
    for name in runs[max(0, keep):]:
        run = os.path.join(runs_root, name)
        if os.path.exists(os.path.join(run, KEEP_FRAMES_MARKER)):
            continue
        finished, newest = run_state(run)
        if now - newest < (FRAME_GRACE_SECONDS if finished else UNFINISHED_STALE_SECONDS):
            continue
        hit = False
        for scenario in os.listdir(run):
            frames = os.path.join(run, scenario, "frames")
            if not os.path.isdir(frames):
                continue
            try:
                size = sum(os.path.getsize(os.path.join(root, f)) for root, _dirs, files in os.walk(frames) for f in files)
                shutil.rmtree(frames)
            except OSError as error:
                print("could not trim %s: %s" % (frames, error))
                continue
            freed += size
            hit = True
        trimmed += hit
    return trimmed, freed


RUNS_CAP_ENV = "GIF_RUNS_CAP_GB"
RUNS_CAP_DEFAULT_GB = 5.0


def runs_cap_bytes(override_gb=None):
    """The size cap for the runs folder: an explicit value, else the
    GIF_RUNS_CAP_GB environment variable, else 5 GB."""
    if override_gb is None:
        try:
            override_gb = float(os.environ.get(RUNS_CAP_ENV, RUNS_CAP_DEFAULT_GB))
        except ValueError:
            override_gb = RUNS_CAP_DEFAULT_GB
    return int(override_gb * 1e9)


def dir_size(path):
    return sum(os.path.getsize(os.path.join(root, f)) for root, _dirs, files in os.walk(path) for f in files)


def enforce_runs_cap(runs_root=None, cap_bytes=None, protect=(), now=None):
    """Deletes whole run directories, oldest first, until the folder is under
    the cap. Never deletes a run named in `protect` (the one just finished), one
    still recording, one younger than the grace period, or one carrying the
    keep_frames marker. Run names sort by their UTC timestamp prefix. Returns
    (names removed, bytes freed); a run that cannot be removed is reported and
    skipped."""
    runs_root = runs_root or RUNS
    cap_bytes = runs_cap_bytes() if cap_bytes is None else cap_bytes
    if not os.path.isdir(runs_root):
        return [], 0
    now = time.time() if now is None else now
    sizes = {d: dir_size(os.path.join(runs_root, d)) for d in os.listdir(runs_root)
             if os.path.isdir(os.path.join(runs_root, d))}
    total = sum(sizes.values())
    removed, freed = [], 0
    for name in sorted(sizes):
        if total <= cap_bytes:
            break
        run = os.path.join(runs_root, name)
        if name in protect or os.path.exists(os.path.join(run, KEEP_FRAMES_MARKER)):
            continue
        finished, newest = run_state(run)
        if not finished or now - newest < FRAME_GRACE_SECONDS:
            continue
        try:
            shutil.rmtree(run)
        except OSError as error:
            print("could not remove %s: %s" % (run, error))
            continue
        removed.append(name)
        freed += sizes[name]
        total -= sizes[name]
    return removed, freed


def cmd_trim(args):
    trimmed, freed = trim_frames(keep=args.keep)
    print("trimmed frames of %d run(s), freed %.1f GB (kept the newest %d)" % (trimmed, freed / 1e9, args.keep))
    return 0


def cmd_finish(args):
    run_dir = os.path.abspath(args.run_dir)
    request = read_json(os.path.join(run_dir, "request.json"))
    ident = read_json(os.path.join(run_dir, "identity.json"), {})
    batch = read_json(os.path.join(run_dir, "result.json"), {})
    scenarios = request["scenarios"]
    outcomes = {}
    encode_seconds = {}
    latest = {}

    for s in scenarios:
        sdir = os.path.join(run_dir, s["key"])
        os.makedirs(sdir, exist_ok=True)
        if s["kind"] in UNITY_KINDS:
            result = read_json(os.path.join(sdir, "result.json"), {})
            ok = bool(result.get("ok")) and bool(list_frames_or_empty(os.path.join(sdir, "frames")))
        else:
            result = run_python_scenario(run_dir, s, sdir)
            ok = bool(result.get("ok"))
        timing = read_json(os.path.join(sdir, "timing.json"), {})
        outcomes[s["key"]] = ok
        record = {"scenario": s, "identity": ident, "requestId": request["requestId"], "result": result, "timing": timing}
        if ok:
            if s["kind"] in UNITY_KINDS:
                encode_seconds[s["key"]] = round(encode(sdir, s, timing, preview=not args.no_preview), 3)
                record["encodeSeconds"] = encode_seconds[s["key"]]
            latest[s["key"]] = run_dir
        else:
            print("FAILED %s %s: %s" % (s["kind"], s["id"], result.get("error") or "no result/frames written"))
        write_json_atomic(os.path.join(sdir, "scenario.json"), record)

    if latest:
        update_json_locked(LATEST, latest)
    print(coverage_table(scenarios, outcomes, skipped=request.get("skipped", []), gaps=request.get("gaps", []),
                         manifest=request.get("manifest", "")))
    if args.keep_frames:
        open(os.path.join(run_dir, KEEP_FRAMES_MARKER), "w").close()
    else:
        trimmed, freed = trim_frames()
        if trimmed:
            print("trimmed frames of %d older run(s), freed %.1f GB (-KeepFrames keeps them)" % (trimmed, freed / 1e9))
    cap = runs_cap_bytes(args.cap_gb)
    removed, freed = enforce_runs_cap(cap_bytes=cap, protect=(os.path.basename(run_dir),))
    if removed:
        print("runs over the %.1f GB cap: removed %d oldest run(s) (%s), freed %.1f GB"
              % (cap / 1e9, len(removed), ", ".join(removed), freed / 1e9))

    totals = {}
    for s in scenarios:
        r = read_json(os.path.join(run_dir, s["key"], "result.json"), {})
        spent = r.get("seconds", 0) + encode_seconds.get(s["key"], 0)
        count, total = totals.get(s["kind"], (0, 0.0))
        totals[s["kind"]] = (count + 1, total + spent)
        if len(scenarios) <= 12:
            print("  %-6s %-22s run %.1fs  encode %.2fs  frames %s" % (
                s["kind"], s["id"], r.get("seconds", 0), encode_seconds.get(s["key"], 0), r.get("frames", "?")))
    for kind, (count, total) in totals.items():
        print("  TIME %-6s %3d scenario(s) %7.1fs total (run + encode), %6.2fs each" % (kind, count, total, total / count))
    if batch:
        print("  batch ok=%s requestId=%s" % (batch.get("ok"), batch.get("requestId")))
    return 0 if outcomes and all(outcomes.values()) else 1


def list_frames_or_empty(frames_dir):
    return list_frames(frames_dir) if os.path.isdir(frames_dir) else []


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

    if not ident_a.get("fingerprint") or not ident_b.get("fingerprint"):
        print("REFUSED: a run has no content fingerprint (no Capture copy was rendered from), so it cannot be compared.")
        return 2
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
        fa, fb = list_frames_or_empty(os.path.join(run_a, s["key"], "frames")), list_frames_or_empty(os.path.join(run_b, s["key"], "frames"))
        if not fa or not fb:
            print("  %-6s %-22s no frames to compare (trimmed; re-record, or record with -KeepFrames)" % (s["kind"], s["id"]))
            failed = True
            continue
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
    p.add_argument("kind", nargs="?", default="")
    p.add_argument("id", nargs="?", default="")
    p.add_argument("--manifest", default="")
    p.add_argument("--vs", default="")
    p.add_argument("--formation", default="")
    p.add_argument("--speed", type=float, default=1.0)
    p.add_argument("--prestatus", default="")
    p.add_argument("--skill", default="")
    p.add_argument("--crop", default="")
    p.add_argument("--seconds", type=float, default=0.0)
    p.add_argument("--aspect", default="")
    p.add_argument("--repeat", type=int, default=1)
    p.add_argument("--darkness", type=float, default=0.0)
    p.add_argument("--party", default="")
    p.add_argument("--target", default="")
    p.add_argument("--request-id", default="")
    p.set_defaults(fn=cmd_prepare)

    p = sub.add_parser("stamp")
    p.add_argument("run_dir")
    p.add_argument("--root", required=True)
    p.set_defaults(fn=cmd_stamp)

    p = sub.add_parser("finish")
    p.add_argument("run_dir")
    p.add_argument("--keep-frames", action="store_true")
    p.add_argument("--no-preview", action="store_true", help="skip anim_preview.gif")
    p.add_argument("--cap-gb", type=float, default=None,
                   help="size cap for the runs folder in GB (default $GIF_RUNS_CAP_GB, else 5)")
    p.set_defaults(fn=cmd_finish)

    p = sub.add_parser("trim")
    p.add_argument("--keep", type=int, default=FRAME_RUNS_KEPT)
    p.set_defaults(fn=cmd_trim)

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
