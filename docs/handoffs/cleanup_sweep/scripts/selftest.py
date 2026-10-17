"""Run every sweep script against its fixture; exit non-zero on any mismatch.

The fixture repo under fixtures/repo carries a .fx suffix on every file so nothing in it
is a live .cs or .ps1 (hooks and scans would otherwise judge the deliberate violations).
Files that cannot be stored literally (a BOM, a 3,500-line file) are built here.
"""
import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common as C
import scan as SCAN

FX = os.path.join(HERE, "fixtures")
P = C.SCRIPTS
FAILS = []


def check(cond, what):
    if not cond:
        FAILS.append(what)
        print("FAIL", what)


def run(script, *args):
    r = subprocess.run([sys.executable, "-I", os.path.join(HERE, script), *args], capture_output=True)
    return r.returncode, r.stdout.decode("utf-8", "replace") + r.stderr.decode("utf-8", "replace")


def materialise(dst):
    src = os.path.join(FX, "repo")
    for d, _, fs in os.walk(src):
        for f in fs:
            rel = os.path.relpath(os.path.join(d, f), src)
            target = os.path.join(dst, rel[:-3] if rel.endswith(".fx") else rel)
            os.makedirs(os.path.dirname(target), exist_ok=True)
            data = open(os.path.join(d, f), "rb").read().replace(b"@@NA@@", "é".encode("utf-8"))
            open(target, "wb").write(data)
    big = os.path.join(dst, P, "Domain/Combat/Big.cs")
    os.makedirs(os.path.dirname(big), exist_ok=True)
    with open(big, "w", newline="\n") as f:
        f.write("class Big { void Run() {} }\n")
        for i in range(3499):
            f.write("// filler %d\n" % i)
    with open(os.path.join(dst, "tools/bom.ps1"), "wb") as f:
        f.write(b"\xef\xbb\xbfWrite-Output 'x'\n")


def rows_of(out, x):
    path = os.path.join(out, "scan", x + ".tsv")
    res = []
    for line in open(path, encoding="utf-8"):
        p = C.parse_row(line)
        if p:
            res.append(p[0])
    return res


def symbols(rows):
    return [re.search(r"\((.*)\)$", r["loc"]).group(1) for r in rows]


def test_empty(tmp):
    repo = os.path.join(tmp, "empty")
    out = os.path.join(tmp, "out_empty")
    shutil.copytree(os.path.join(FX, "empty"), repo)
    os.makedirs(out)
    empty_slices = os.path.join(tmp, "EMPTY_SLICES.md")
    open(empty_slices, "w").write("# no slices\n")
    cases = [("slices.py", "--repo", repo, "--no-git", "--out", out, "--slices", os.path.join(tmp, "x.md")),
             ("lanes.py", "--repo", repo, "--no-git", "--out", out),
             ("scan.py", "--repo", repo, "--no-git", "--out", out),
             ("tag.py", "--repo", repo, "--no-git", "--out", os.path.join(tmp, "nothing"), "--slices", empty_slices),
             ("merge.py", "--repo", repo, "--no-git", "--out", os.path.join(tmp, "nothing")),
             ("coverage.py", "notes", "--repo", repo, "--out", out, "--slices", empty_slices)]
    for case in cases:
        rc, text = run(*case)
        check(rc == 0, "%s on empty input exits 0 (got %d: %s)" % (case[0], rc, text.strip()[:120]))
        check(text.strip().splitlines()[-1:] == ["0 rows"], "%s on empty input prints '0 rows' (got %r)" % (case[0], text.strip()[-60:]))
    os.makedirs(os.path.join(out, "scan"), exist_ok=True)
    for x in SCAN_REQUIRED:
        open(os.path.join(out, "scan", x + ".tsv"), "w").write("0 rows: nothing to find\n")
    rc, text = run("coverage.py", "scan", "--out", out)
    check(rc == 0, "coverage scan passes when every file says '0 rows: <pattern>' (%s)" % text.strip())


SCAN_REQUIRED = ["X%d" % i for i in range(1, 10)] + ["H5", "H6a", "H9"]


def test_slices(tmp, repo, out):
    md = os.path.join(tmp, "SLICES.md")
    rc, text = run("slices.py", "--repo", repo, "--no-git", "--out", out, "--slices", md, "--churn", os.path.join(FX, "churn.tsv"))
    check(rc == 0, "slices.py runs on the fixture: " + text.strip()[:200])
    sl = C.parse_slices(open(md, encoding="utf-8").read())
    big = [s for s in sl if any(p.endswith("Combat/Big.cs") for p, _, _ in s["files"])]
    check(sorted((a, b) for s in big for _, a, b in s["files"]) == [(1, 1750), (1751, 3500)], "3,500-line file splits into two equal ranges")
    check(all(s["tier"] == "T1" and s["wave"] == "W1" and s["why"] == "yield" for s in big), "Domain/Combat slices are T1 W1 yield")
    fight = [s for s in sl if any(p.endswith("Core/FightA.cs") for p, _, _ in s["files"])]
    check(len(fight) == 1 and fight[0]["why"] == "yield" and fight[0]["wave"] == "W1", "Core Fight* file is a W1 yield slice")
    core = [s for s in sl if any(p.endswith("Core/Plain.cs") for p, _, _ in s["files"])]
    check(len(core) == 1 and core[0]["tier"] == "T1" and core[0]["why"] == "churn" and core[0]["wave"] == "W2", "churned Core slice is T1 churn W2")
    tools = [s for s in sl if any(p == "tools/runner.ps1" for p, _, _ in s["files"])]
    check(len(tools) == 1 and tools[0]["tier"] == "T2" and tools[0]["dgroup"] == "D1", "quiet tools slice is T2 in D1")
    check(any(p == "tools/githooks/pre-commit" for s in tools for p, _, _ in s["files"]), "extensionless pre-commit is sliced")
    check(not any("/Tests/" in p for s in sl for p, _, _ in s["files"]), "Tests/ is excluded")
    check(any(s["id"] == "C1" and s["files"][0][0].endswith("foo.json") for s in sl), "content slice C1 covers ContentData")
    g1 = [s for s in sl if s["id"] == "G1"]
    check(len(g1) == 1 and any(p.endswith(".asmdef") for p, _, _ in g1[0]["files"]) and
          any(p == "Packages/manifest.json" for p, _, _ in g1[0]["files"]), "G1 holds asmdefs and the package manifest")
    return md


def test_lanes(tmp, repo, out):
    want = {P + "Core/FightA.cs": "L2", P + "Core/Plain.cs": "L4", P + "Domain/Combat/Big.cs": "L1",
            P + "Core/RunOrchestrator.Spells.cs": "L8", P + "Tests/EditMode/FightATests.cs": "L2",
            P + "Tests/EditMode/LiveTestsUse.cs": "L4", P + "Tests/EditMode/TieTests.cs": "L10",
            P + "Tests/EditMode/Shared/Helper.cs": "L10", P + "Domain/Dom.cs": "L12",
            P + "Domain/PrincesPalace.Domain.asmdef": "L13", "docs/README.md": "L11",
            P + "Domain/Content/FooResolver.cs": "L6", "tools/githooks/pre-commit": "L9b", "tools/x.ps1": "L9a"}
    for path, lane in sorted(want.items()):
        rc, text = run("lanes.py", "--repo", repo, "--no-git", "--out", out, "--lane-of", path)
        check(rc == 0 and text.strip() == lane, "lane-of %s is %s (got %r)" % (path, lane, text.strip()))
    rc, text = run("lanes.py", "--repo", repo, "--no-git", "--out", out, "--check")
    check(rc == 0 and "0 unowned, 0 double-owned" in text, "lanes --check is clean on the fixture: " + text.strip()[-80:])
    hints = C.read_lanes_tsv(os.path.join(out, "lanes.tsv"))
    check(hints.get(P + "Domain/Dom.cs", ("", ""))[1] == "wt" and hints.get(P + "Core/Plain.cs", ("", ""))[1] == "main",
          "place hints: Domain is wt, Core is main")
    weird = os.path.join(repo, P + "Weird/x.cs")
    os.makedirs(os.path.dirname(weird))
    open(weird, "w").write("class W {}\n")
    rc, text = run("lanes.py", "--repo", repo, "--no-git", "--out", out, "--check")
    check(rc == 1 and "1 unowned, 0 double-owned" in text, "lanes --check fails on an unowned file: " + text.strip()[-60:])
    shutil.rmtree(os.path.dirname(weird))
    run("lanes.py", "--repo", repo, "--no-git", "--out", out)


def test_scan(tmp, repo, out):
    rc, text = run("scan.py", "--repo", repo, "--no-git", "--out", out)
    check(rc == 0, "scan.py runs on the fixture: " + text.strip()[:200])
    x1 = symbols(rows_of(out, "X1"))
    check("DeadThing" in x1, "X1 reports the dead field")
    check(not [s for s in x1 if s.startswith("Live") or s == "liveScene"], "X1 reports no live example: %s" % [s for s in x1 if s.lower().startswith("live")])
    ctx = SCAN.Ctx(repo, True)
    idx = SCAN.build_x1_indexes(ctx)
    forms = {"LiveCsharp": "csharp", "LiveUiBind": "uibind", "liveScene": "scene", "LiveResources": "resources",
             "LiveContent": "content", "LiveReflection": "reflection", "LiveTests": "tests", "LiveTools": "tools"}
    for name, form in forms.items():
        decls = idx["decl"][name]
        hit = sorted(f for f, fn in SCAN.FORMS if fn(idx, name, decls))
        check(hit == [form], "X1 name %s is rescued by exactly the %s form (got %s)" % (name, form, hit))
    check(not [f for f, fn in SCAN.FORMS if fn(idx, "DeadThing", idx["decl"]["DeadThing"])], "X1 dead name hits no form")
    x2 = symbols(rows_of(out, "X2"))
    check(x2 == ["used to"], "X2 flags only the history comment (got %s)" % x2)
    x3 = rows_of(out, "X3")
    check(sorted(int(re.search(r":(\d+)@", r["loc"]).group(1)) for r in x3) == [8, 9], "X3 flags the two Update lines past a string brace and a nested block, not Other()")
    x4 = symbols(rows_of(out, "X4"))
    check(x4 == ["Clamp01(int)"], "X4 groups the two private Clamp01(int) helpers only (got %s)" % x4)
    x5 = symbols(rows_of(out, "X5"))
    check(x5 == ["orphan.py"], "X5 reports only the unreferenced script (got %s)" % x5)
    x6 = rows_of(out, "X6")
    check(sorted(symbols(x6)) == ["orphan.png", "stray.png.meta"] and all(r["lane"] == "OWNER" for r in x6), "X6 lists the orphan asset and orphan meta for OWNER (got %s)" % symbols(x6))
    x7 = rows_of(out, "X7")
    check(len(x7) == 3, "X7 reports UnityEngine in Domain plus two asmdef findings (got %d)" % len(x7))
    x8 = symbols(rows_of(out, "X8"))
    check(sorted(x8) == ["BOM", "U+00E9 col 10"], "X8 flags the code character and the BOM, not the comments (got %s)" % x8)
    x9 = rows_of(out, "X9")
    check(sorted(symbols(x9)) == ["RawFooEntry.unreadField", "ignoredKey"], "X9 flags the unread field and the ignored key (got %s)" % symbols(x9))
    check(all(r["cat"] == "BUG" and r["sev"] == "S2" and r["lane"] == "L6" for r in x9), "X9 rows are BUG S2 lane L6")
    check(all(r["state"] == "candidate" and r["grade"] == "static" for x in range(1, 10) for r in rows_of(out, "X%d" % x)), "scan rows are candidate/static")


def test_tag(tmp, repo, out, md):
    rc, text = run("tag.py", "--repo", repo, "--out", out, "--slices", md)
    check(rc == 0, "tag.py runs: " + text.strip()[:200])
    tagged = [C.parse_row(l)[0] for l in open(os.path.join(out, "scan", "tagged.tsv"), encoding="utf-8")]
    hist = [r for r in tagged if "Core/Hist.cs" in r["loc"]]
    check(len(hist) == 1 and re.match(r"^S\d+$", hist[0]["slice"]), "tag gives a code row its slice id")
    owner = [r for r in tagged if r["id"].startswith("X6")]
    check(owner and all(r["slice"] == "OWNER" for r in owner), "tag falls back to the lane when no slice covers a row")


def test_coverage(tmp, repo, out, md):
    for x in ("H5", "H6a", "H9"):
        open(os.path.join(out, "scan", x + ".tsv"), "w").write("0 rows: nothing\n")
    rc, text = run("coverage.py", "scan", "--out", out)
    check(rc == 0, "coverage scan passes with every file present: " + text.strip())
    os.remove(os.path.join(out, "scan", "X5.tsv"))
    rc, text = run("coverage.py", "scan", "--out", out)
    check(rc == 1 and "X5: missing" in text, "coverage scan fails on a missing X file")
    notes = os.path.join(out, "notes")
    os.makedirs(notes, exist_ok=True)
    for s in C.parse_slices(open(md, encoding="utf-8").read()):
        open(os.path.join(notes, s["id"] + ".md"), "w").write("coverage: full\n0 rows\n")
    rc, text = run("coverage.py", "notes", "--repo", repo, "--out", out, "--slices", md)
    check(rc == 0, "coverage notes passes when every slice says 'coverage: full': " + text.strip()[:200])
    open(os.path.join(notes, "S01.md"), "w").write("coverage: partial\n")
    rc, text = run("coverage.py", "notes", "--repo", repo, "--out", out, "--slices", md)
    check(rc == 1 and "S01" in text, "coverage notes fails and names the slice that is not full")


def test_merge(tmp, repo, out):
    mout = os.path.join(tmp, "out_merge")
    shutil.copytree(os.path.join(FX, "merge"), mout)
    shutil.copy(os.path.join(out, "lanes.tsv"), mout)
    rc, text = run("merge.py", "--repo", repo, "--no-git", "--out", mout)
    check(rc == 0, "merge.py runs: " + text.strip()[:200])
    lines = open(os.path.join(mout, "LEDGER.tsv"), encoding="utf-8").read().splitlines()
    head = lines[0].split("\t")
    rows = [dict(zip(head, l.split("\t"))) for l in lines[1:]]
    check(len(rows) == 4, "merge folds 6 inputs into 4 rows (got %d)" % len(rows))
    by = {symbols([r])[0]: r for r in rows}
    tick = by.get("Plain.Tick", {})
    check(tick.get("state") == "fixed" and tick.get("head") == "ok" and set(tick.get("dups", "").split(",")) == {"N1-0001", "N1-0002"},
          "dedupe keeps the ledger's fixed row and records the folded ids: %s" % tick)
    check(by.get("Plain.Gone", {}).get("head") == "stale", "a symbol missing at HEAD is stale")
    check(by.get("Big.Run", {}).get("place") == "wt" and by["Big.Run"]["lane"] == "L1", "Domain row is place wt, lane L1")
    check(by.get("FightATests.T", {}).get("place") == "main", "a main-only test class forces place main")
    check(by.get("Plain.Tick", {}).get("place") == "main", "a Core row is place main")


def main():
    C.utf8_stdout()
    tmp = tempfile.mkdtemp(prefix="sweep-selftest-")
    try:
        test_empty(tmp)
        repo, out = os.path.join(tmp, "repo"), os.path.join(tmp, "out")
        materialise(repo)
        os.makedirs(out)
        md = test_slices(tmp, repo, out)
        test_lanes(tmp, repo, out)
        test_scan(tmp, repo, out)
        test_tag(tmp, repo, out, md)
        test_coverage(tmp, repo, out, md)
        test_merge(tmp, repo, out)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    if FAILS:
        print("selftest: %d failures" % len(FAILS))
        return 1
    print("selftest: ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
