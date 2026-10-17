"""Map every tracked in-scope file to exactly one lane (PLAN_CLEANUP_SWEEP.md section 7).

Rule precedence: an exact file entry, then the longest matching prefix. A "direct"
rule matches only files sitting directly in that directory. Two different lanes at
the winning specificity count as double-owned.
"""
import os
import re
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

S = C.SCRIPTS

# (kind, pattern, lane). kind: exact | prefix | direct. Patterns are repo-relative.
RULES = []


def rule(kind, pat, lane):
    RULES.append((kind, pat, lane))


# L1 / L2 around Domain/Combat
rule("prefix", S + "Domain/Combat/", "L1")
rule("prefix", S + "Domain/Combat/Presentation/", "L2")
rule("prefix", S + "Domain/Combat/Session/FightHud", "L2")
rule("prefix", S + "Domain/Stage/", "L2")
for n in ("Fight", "Stage", "Spell", "Stance", "Focus", "FrameSequenceLoader", "ContactCues",
          "SquadTrack", "PreviewFight", "DamagePopup", "StatusCues"):
    rule("prefix", S + "Core/" + n, "L2")
# L3 run loop
for n in ("RunController", "RunEncounter", "RunEventContext", "RunLedger", "RunManager", "RunSettlement",
          "Map", "Reckoning", "Reward", "Relic", "Shop", "Event", "RoomResolver", "ItemOfferRoll", "Hone",
          "RealmOpening", "RealmPrizes"):
    rule("prefix", S + "Core/" + n, "L3")
for d in ("Rewards", "Dungeon", "Events", "Economy", "Relics"):
    rule("prefix", S + "Domain/%s/" % d, "L3")
# L4 hub and menus
rule("direct", S + "Core/", "L4")
for d in ("Party", "Talents", "Progression"):
    rule("prefix", S + "Domain/%s/" % d, "L4")
# L5 screens
rule("prefix", S + "Domain/UiKit/", "L5")
rule("prefix", S + "Editor/SceneBuilder/", "L5")
rule("prefix", "Assets/_Project/Scenes/", "L5")
# L6 content
rule("prefix", S + "Domain/Content/", "L6")
rule("prefix", S + "Core/Content/", "L6")
rule("prefix", "Assets/_Project/ContentData/", "L6")
rule("exact", S + "Editor/ContentBuilder.cs", "L6")
# L7 saves
rule("prefix", S + "Data/", "L7")
rule("exact", S + "Core/SaveSystem.cs", "L7")
rule("prefix", S + "Core/SaveSlot", "L7")
# L8 bot
rule("prefix", S + "Core/Bot/", "L8")
rule("prefix", S + "Core/RunOrchestrator", "L8")
rule("prefix", S + "Domain/Bot/", "L8")
rule("prefix", S + "Editor/Bot/", "L8")
# L9a tools, L9b gate infra
rule("prefix", "tools/", "L9a")
for f in ("test.ps1", "run_tests_parallel.ps1", "test_select.ps1", "test_areas.ps1", "playmode_shards.ps1",
          "runner_sync.ps1", "unity_lock.ps1", "unity_path.ps1"):
    rule("exact", "tools/" + f, "L9b")
rule("prefix", "tools/githooks/", "L9b")
rule("prefix", "tools/domain-tests/", "L9b")
# L10 shared tests (production-type matching for the rest happens in resolve_tests)
rule("prefix", S + "Tests/", "L10")
rule("prefix", S + "Tests/EditMode/Shared/", "L10")
rule("prefix", S + "Tests/PlayMode/Shared/", "L10")
# L11 docs, ORCH owns the sweep folder
rule("prefix", "docs/", "L11")
rule("prefix", "docs/handoffs/cleanup_sweep/", "ORCH")
rule("exact", "AUDIT.md", "L11")
rule("exact", "CLAUDE.md", "L11")
# L12 foundation
rule("direct", S + "Domain/", "L12")
for d in ("Equipment", "Stats", "Inventory", "Rng", "Glossary", "Preview", "Audio", "Ambience", "DebugMenu"):
    rule("prefix", S + "Domain/%s/" % d, "L12")
# L13 editor and project
rule("direct", S + "Editor/", "L13")
rule("prefix", "Packages/", "L13")
rule("prefix", "ProjectSettings/", "L13")
rule("prefix", "Assets/_Project/Shaders/", "L13")

TESTS = S + "Tests/"


def in_scope(p):
    if p.endswith(".meta"):
        p = p[:-5]
    return (p.startswith((S, "Assets/_Project/ContentData/", "Assets/_Project/Scenes/", "Assets/_Project/Shaders/",
                          "tools/", "docs/", "Packages/", "ProjectSettings/"))
            or p in ("AUDIT.md", "CLAUDE.md")
            or p.endswith((".shader", ".hlsl", ".asmdef")))


def lanes_for(path):
    """Winning lanes (a set) for a path; empty when no rule matches."""
    if path.endswith(".asmdef") or path.endswith((".shader", ".hlsl")):
        return {"L13"}
    best, lanes = -1, set()
    for kind, pat, lane in RULES:
        if kind == "exact":
            ok, spec = path == pat, 10 ** 6
        elif kind == "prefix":
            ok, spec = path.startswith(pat), len(pat)
        else:
            ok = path.startswith(pat) and "/" not in path[len(pat):]
            spec = len(pat)
        if not ok:
            continue
        if spec > best:
            best, lanes = spec, {lane}
        elif spec == best:
            lanes.add(lane)
    return lanes


TYPE_RE = re.compile(r"\b(?:class|struct|enum|interface|record)\s+([A-Za-z_]\w*)")
IDENT_RE = re.compile(r"[A-Za-z_]\w*")


def production_types(repo, files, lane_of):
    """type name -> lane, skipping names declared in more than one lane."""
    seen = defaultdict(set)
    for p in files:
        if not p.startswith(S) or p.startswith(TESTS) or not p.endswith(".cs"):
            continue
        lane = lane_of.get(p)
        if not lane:
            continue
        for m in TYPE_RE.finditer(C.strip_cs(C.read_text(repo, p))):
            seen[m.group(1)].add(lane)
    return {t: next(iter(ls)) for t, ls in seen.items() if len(ls) == 1}


def test_lane(repo, path, types):
    if "/Shared/" in path or not path.endswith(".cs"):
        return "L10"
    idents = set(IDENT_RE.findall(C.strip_cs(C.read_text(repo, path))))
    votes = Counter(types[t] for t in idents if t in types)
    if not votes:
        return "L10"
    top = votes.most_common(2)
    if len(top) > 1 and top[0][1] == top[1][1]:
        return "L10"
    return top[0][0]


def compute(repo, no_git=False):
    """Return (lane per path, unowned list, double list)."""
    files = [p for p in C.list_files(repo, no_git) if in_scope(p)]
    base = [p for p in files if not p.endswith(".meta")]
    lane_of, unowned, double = {}, [], []
    for p in base:
        ls = lanes_for(p)
        if not ls:
            unowned.append(p)
        elif len(ls) > 1:
            double.append(p)
        else:
            lane_of[p] = next(iter(ls))
    types = production_types(repo, base, lane_of)
    for p in base:
        if p.startswith(TESTS) and p in lane_of and p.endswith(".cs"):
            lane_of[p] = test_lane(repo, p, types)
    # a .meta follows its asset; a folder .meta follows the plurality of what sits under it
    fileset = set(files)
    for p in files:
        if not p.endswith(".meta"):
            continue
        b = p[:-5]
        if b in lane_of:
            lane_of[p] = lane_of[b]
        elif b in fileset:
            continue
        else:
            votes = Counter(l for q, l in lane_of.items() if q.startswith(b + "/") and not q.endswith(".meta"))
            if votes:
                best = max(votes.values())
                lane_of[p] = sorted(l for l, v in votes.items() if v == best)[0]
            else:
                unowned.append(p)
    return lane_of, unowned, double


def place_hint(path):
    return "wt" if path.startswith((S + "Domain/", S + "Tests/EditMode/")) else "main"


def main():
    C.utf8_stdout()
    ap = C.std_parser(__doc__)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--lane-of", metavar="PATH")
    args = ap.parse_args()
    lane_of, unowned, double = compute(args.repo, args.no_git)
    if args.lane_of:
        p = args.lane_of.replace("\\", "/")
        if p in lane_of:
            print(lane_of[p])
            return 0
        ls = lanes_for(p[:-5] if p.endswith(".meta") else p)
        if len(ls) == 1:
            print(next(iter(ls)))
            return 0
        print("unowned" if not ls else "double-owned: " + ",".join(sorted(ls)))
        return 1
    if not lane_of and not unowned and not double:
        print("0 rows")
        return 0
    C.ensure_dir(args.out)
    with open(os.path.join(args.out, "lanes.tsv"), "w", encoding="utf-8", newline="\n") as f:
        f.write("path\tlane\tplace-hint\n")
        for p in sorted(lane_of):
            f.write("%s\t%s\t%s\n" % (p, lane_of[p], place_hint(p)))
    counts = Counter(lane_of.values())
    print("%d files; " % len(lane_of) + ", ".join("%s %d" % (k, counts[k]) for k in sorted(counts)))
    for p in unowned[:50]:
        print("unowned:", p)
    for p in double[:50]:
        print("double-owned:", p, sorted(lanes_for(p)))
    if args.check:
        print("%d unowned, %d double-owned" % (len(unowned), len(double)))
        return 1 if unowned or double else 0
    return 0


if __name__ == "__main__":
    sys.exit(main())
