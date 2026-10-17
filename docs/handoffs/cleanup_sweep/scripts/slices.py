"""Regenerate SLICES.md: finder slices over production code, content and misc files.

Line numbers are those of --rev, churn is lines added between --base and --rev.
"""
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

CAP = 3000
D_CAP = 1500
YIELD_CORE = ("Fight", "Stage", "Spell", "Party", "CharacterDossier", "PcPlate")
BOT_DIRS = ("Core/Bot/", "Domain/Bot/", "Editor/Bot/")
W2_DOMAIN = ("Domain/UiKit/", "Domain/Rewards/", "Domain/Dungeon/", "Domain/Events/")


def classify(path):
    """(layer, label, wave, is_yield) for a candidate production or tools file."""
    if path.startswith(C.SCRIPTS):
        rel = path[len(C.SCRIPTS):]
        if rel.startswith("Domain/Combat/"):
            return "Domain", "Combat", "W1", True
        if rel.startswith(BOT_DIRS):
            return rel.split("/")[0], "Bot", "W2", False
        if rel.startswith("Core/Content/"):
            return "Core", "Content", "W3", False
        if rel.startswith("Core/") and rel.count("/") == 1:
            if os.path.basename(rel).startswith(YIELD_CORE):
                return "Core", "CoreYield", "W1", True
            return "Core", "Core", "W2", False
        if rel.startswith("Core/"):
            return "Core", "CoreSub", "W3", False
        if rel.startswith(W2_DOMAIN):
            return "Domain", "RunDomain" if not rel.startswith("Domain/UiKit/") else "UiKit", "W2", False
        if rel.startswith("Domain/Content/"):
            return "Domain", "Content", "W3", False
        if rel.startswith("Data/"):
            return "Data", "Data", "W2", False
        if rel.startswith("Domain/"):
            return "Domain", "Domain", "W3", False
        if rel.startswith("Editor/"):
            return "Editor", "Editor", "W3", False
        return "Scripts", "Scripts", "W3", False
    return "tools", "tools", "W3", False


def is_candidate(path):
    if path.startswith(C.SCRIPTS):
        rel = path[len(C.SCRIPTS):]
        if not path.endswith(".cs") or rel.startswith("Tests/"):
            return False
        return "/bin/" not in "/" + rel and "/obj/" not in "/" + rel
    if path.startswith("tools/"):
        if path.startswith("tools/domain-tests/") or "/bin/" in path or "/obj/" in path:
            return False
        if "/__pycache__/" in path:
            return False
        base = os.path.basename(path)
        if path.startswith("tools/githooks/") and "." not in base:
            return True
        return base.endswith((".ps1", ".py", ".psm1"))
    return False


class Source:
    """Line counts at a git revision, or from the disk in fixture mode."""

    def __init__(self, repo, rev, no_git):
        self.repo, self.rev = repo, rev
        self.git = C.has_git(repo, no_git)
        self.proc = None
        if self.git:
            self.proc = subprocess.Popen(["git", "-C", repo, "cat-file", "--batch"],
                                         stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        self.no_git = no_git

    def files(self):
        if not self.git:
            return C.list_files(self.repo, True)
        raw = C.git(self.repo, "ls-tree", "-r", "-z", "--name-only", self.rev).decode("utf-8", "replace")
        return sorted(p for p in raw.split("\0") if p and not p.startswith(C.FIXTURE_PREFIX))

    def lines(self, path):
        if not self.git:
            data = C.read_bytes(self.repo, path)
        else:
            self.proc.stdin.write(("%s:%s\n" % (self.rev, path)).encode("utf-8"))
            self.proc.stdin.flush()
            head = self.proc.stdout.readline().decode().split()
            if len(head) < 3:
                return 0
            size = int(head[2])
            data = self.proc.stdout.read(size)
            self.proc.stdout.read(1)
        if not data:
            return 0
        return data.count(b"\n") + (0 if data.endswith(b"\n") else 1)

    def close(self):
        if self.proc:
            self.proc.stdin.close()
            self.proc.wait()


def load_churn(args):
    churn = {}
    if args.churn:
        text = open(args.churn, encoding="utf-8").read()
    elif C.has_git(args.repo, args.no_git):
        text = C.git(args.repo, "diff", "--numstat", "--no-renames", args.base, args.rev).decode("utf-8", "replace")
    else:
        text = ""
    for line in text.splitlines():
        p = line.split("\t")
        if len(p) == 3 and p[0].isdigit():
            churn[p[2]] = int(p[0])
    return churn


def pack(entries):
    """entries: [(path, lines, churn)] in order -> list of lists of (path, a, b, churn)."""
    slices, cur, tot = [], [], 0
    for path, n, ch in entries:
        if n <= 0:
            continue
        if n > CAP:
            if cur:
                slices.append(cur)
                cur, tot = [], 0
            k = (n + CAP - 1) // CAP
            step = (n + k - 1) // k
            for i in range(k):
                a, b = i * step + 1, min(n, (i + 1) * step)
                slices.append([(path, a, b, ch // k)])
            continue
        if tot + n > CAP and cur:
            slices.append(cur)
            cur, tot = [], 0
        cur.append((path, 1, n, ch))
        tot += n
    if cur:
        slices.append(cur)
    return slices


def short_dirs(files):
    seen = []
    for p, *_ in files:
        d = os.path.dirname(p)
        d = d[len(C.SCRIPTS):] if d.startswith(C.SCRIPTS) else d
        d = d or "Scripts"
        if d not in seen:
            seen.append(d)
    return ", ".join(seen)


def build(args):
    src = Source(args.repo, args.rev, args.no_git)
    churn = load_churn(args)
    allfiles = src.files()
    groups = {}
    for p in allfiles:
        if is_candidate(p):
            layer, label, wave, y = classify(p)
            groups.setdefault((wave, layer, label, y), []).append(p)
    wave_order = {"W1": 1, "W2": 2, "W3": 3}
    units = []  # dict(kind, tier, wave, why, files, lines, churn)
    for key in sorted(groups, key=lambda k: (wave_order[k[0]], k[1], k[2])):
        wave, layer, label, y = key
        entries = [(p, src.lines(p), churn.get(p, 0)) for p in sorted(groups[key])]
        for sl in pack(entries):
            lines = sum(b - a + 1 for _, a, b, _ in sl)
            ch = sum(c for *_, c in sl)
            if y:
                tier, why = "T1", "yield"
            elif ch * 4 >= lines:
                tier, why = "T1", "churn"
            else:
                tier, why = "T2", ""
            units.append({"tier": tier, "wave": wave, "why": why, "files": [(p, a, b) for p, a, b, _ in sl],
                          "lines": lines, "churn": ch, "dgroup": ""})
    # content slices
    cfiles = [p for p in allfiles if p.startswith("Assets/_Project/ContentData/") and p.endswith(".json")]
    centries = [(p, src.lines(p), churn.get(p, 0)) for p in cfiles]
    cslices = []
    for i, sl in enumerate(pack(centries), 1):
        cslices.append({"id": "C%d" % i, "tier": "T1", "wave": "W3", "why": "content",
                        "files": [(p, a, b) for p, a, b, _ in sl],
                        "lines": sum(b - a + 1 for _, a, b, _ in sl), "churn": sum(c for *_, c in sl), "dgroup": ""})
    # misc slice
    misc = [p for p in allfiles if (p.startswith("Assets/_Project/") and p.endswith((".shader", ".hlsl", ".cginc")))
            or p == "tools/githooks/pre-commit" or p == "Packages/manifest.json"
            or (p.endswith(".asmdef") and "TextMesh Pro" not in p and not p.startswith("Library/"))]
    mentries = [(p, src.lines(p), churn.get(p, 0)) for p in sorted(misc)]
    gslices = []
    mfiles = [(p, 1, n) for p, n, _ in mentries if n > 0]
    if mfiles:
        gslices.append({"id": "G1", "tier": "T1", "wave": "W3", "why": "misc", "files": mfiles,
                        "lines": sum(b for _, _, b in mfiles), "churn": sum(c for *_, c in mentries), "dgroup": ""})
    src.close()
    # ids and D-groups
    for i, u in enumerate(units, 1):
        u["id"] = "S%02d" % i
    dgroups, cur, tot = [], [], 0
    for u in units:
        if u["tier"] != "T2":
            continue
        if cur and tot + u["churn"] > D_CAP:
            dgroups.append(cur)
            cur, tot = [], 0
        cur.append(u)
        tot += u["churn"]
    if cur:
        dgroups.append(cur)
    for i, g in enumerate(dgroups, 1):
        for u in g:
            u["dgroup"] = "D%d" % i
    return units + cslices + gslices, dgroups


def render(slices, dgroups, args):
    out = ["# Cleanup sweep slices", "",
           "Generated by `scripts/slices.py` at %s (churn = lines added since %s). " % (args.rev, args.base) +
           "T1 = full read: churn >= 25 percent of the slice's lines, or a past-yield area "
           "(`Domain/Combat/`, Core `Fight*`/`Stage*`/`Spell*`/`Party*`/`CharacterDossier*`/`PcPlate*`). "
           "T2 = diff-scoped read, bundled into D-groups of <= %d churned lines. " % D_CAP +
           "Production code and tools only; tests are covered by lane H6. "
           "Files over %d lines are split into equal ranges. Do not hand-edit." % CAP, "",
           "| id | tier | wave | why | D-group | folders | lines | churn | files |",
           "|---|---|---|---|---|---|---|---|---|"]
    for s in slices:
        wave = "W3" if s["tier"] == "T2" else s["wave"]
        files = "; ".join("%s:%d-%d" % f for f in s["files"])
        out.append("| %s | %s | %s | %s | %s | %s | %d | %d | %s |" % (
            s["id"], s["tier"], wave, s["why"], s["dgroup"], short_dirs(s["files"]), s["lines"], s["churn"], files))
        s["wave_eff"] = wave
    out += ["", "## D-groups (finder units for T2 slices)", "", "| id | wave | slices | churn |", "|---|---|---|---|"]
    for i, g in enumerate(dgroups, 1):
        out.append("| D%d | W3 | %s | %d |" % (i, ", ".join(u["id"] for u in g), sum(u["churn"] for u in g)))
    counts = {}
    for s in slices:
        if s["tier"] == "T1":
            counts[(s["wave"], "T1")] = counts.get((s["wave"], "T1"), 0) + 1
    counts[("W3", "T2")] = len(dgroups)
    out += ["", "## Finder units", "", "| wave | T1 slices | D-groups | total |", "|---|---|---|---|"]
    total = 0
    for w in ("W1", "W2", "W3"):
        t1, d = counts.get((w, "T1"), 0), counts.get((w, "T2"), 0)
        total += t1 + d
        out.append("| %s | %d | %d | %d |" % (w, t1, d, t1 + d))
    out.append("| all | %d | %d | %d |" % (sum(v for (w, t), v in counts.items() if t == "T1"), len(dgroups), total))
    return "\n".join(out) + "\n", counts, total


def main():
    C.utf8_stdout()
    ap = C.std_parser(__doc__)
    ap.add_argument("--rev", default=C.REV)
    ap.add_argument("--base", default=C.BASE)
    ap.add_argument("--churn", help="numstat file replacing the git diff (fixtures)")
    ap.add_argument("--slices", help="output path, default <repo>/%s" % C.SLICES_REL)
    args = ap.parse_args()
    slices, dgroups = build(args)
    if not slices:
        print("0 rows")
        return 0
    text, counts, total = render(slices, dgroups, args)
    path = args.slices or os.path.join(args.repo, C.SLICES_REL)
    C.ensure_dir(os.path.dirname(os.path.abspath(path)))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    t1 = sum(1 for s in slices if s["tier"] == "T1")
    t2 = sum(1 for s in slices if s["tier"] == "T2")
    print("%d slices (%d T1, %d T2), %d D-groups, %d finder units" % (len(slices), t1, t2, len(dgroups), total))
    for w in ("W1", "W2", "W3"):
        print("  %s: T1 slices %d, D-groups %d" % (w, counts.get((w, "T1"), 0), counts.get((w, "T2"), 0)))
    print("wrote", path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
