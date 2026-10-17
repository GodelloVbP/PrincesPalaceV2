"""Build LEDGER.tsv from <out>/notes/*.md rows and <out>/ledger/*.tsv rows.

Steps: parse rows, dedupe by symbol + contract, mark rows whose symbol or excerpt is gone
at HEAD as stale, tag place (wt or main) and lane, write <out>/LEDGER.tsv, print a summary.
LEDGER.tsv is generated; never edit it by hand.
"""
import glob
import os
import re
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

LEDGER_EXTRA = ["place", "head", "dups"]


def read_rows(out):
    rows = []
    for kind, pat in (("notes", "notes/*.md"), ("ledger", "ledger/*.tsv")):
        for path in sorted(glob.glob(os.path.join(out, pat))):
            for line in open(path, encoding="utf-8"):
                line = line.rstrip("\n")
                if kind == "notes" and line.lstrip().startswith("|"):
                    line = line.strip().strip("|").strip()
                parsed = C.parse_row(line)
                if parsed:
                    rows.append((kind, parsed[0]))
    return rows


def symbol_of(loc, fold=True):
    m = re.search(r"\(([^()]*(?:\([^()]*\)[^()]*)*)\)\s*$", loc)
    sym = (m.group(1) if m else loc).strip()
    return sym.lower() if fold else sym


def locations(loc):
    """(path, line) pairs in a location cell; several may be joined by '; '."""
    out = []
    for part in re.split(r";\s+", loc):
        m = re.match(r"^(.*?):(\d+)@", part.strip())
        if m:
            out.append((m.group(1), int(m.group(2))))
    return out


def contract_key(c):
    return re.sub(r"\W+", " ", c.lower()).strip()


SEV = {"S1": 1, "S2": 2, "S3": 3, "S4": 4}


def better(new, old):
    if old["state"] == "candidate" and new["state"] != "candidate":
        return True
    if new["state"] == "candidate" and old["state"] != "candidate":
        return False
    return SEV.get(new["sev"], 9) < SEV.get(old["sev"], 9)


def dedupe(rows):
    kept, order = {}, []
    for kind, r in rows:
        key = (symbol_of(r["loc"]), contract_key(r["contract"]))
        if key not in kept:
            kept[key] = [r, []]
            order.append(key)
        else:
            cur = kept[key]
            if better(r, cur[0]):
                cur[1].append(cur[0]["id"])
                cur[0] = r
            else:
                cur[1].append(r["id"])
    return [(kept[k][0], kept[k][1]) for k in order]


def is_stale(repo, r):
    locs = locations(r["loc"])
    if not locs:
        return False
    sym = re.match(r"\s*([A-Za-z_][\w.]*)", symbol_of(r["loc"], False))
    name = sym.group(1).split(".")[-1] if sym else None
    if name and len(name) < 2:
        name = None
    for path, _ in locs:
        text = C.read_text(repo, path)
        if not text:
            return True
        if name and not re.search(r"\b%s\b" % re.escape(name), text):
            return True
    flat = lambda s: re.sub(r"\s+", " ", s)
    for chunk in re.findall(r"`([^`]{6,})`", r["failure"] + " " + r["fix"]):
        if not any(flat(chunk) in flat(C.read_text(repo, p)) for p, _ in locs):
            return True
    return False


def load_main_only(out):
    names = set()
    try:
        for line in open(os.path.join(out, "main-only.txt"), encoding="utf-8"):
            line = line.split("#")[0].strip()
            if line:
                names.add(line)
    except OSError:
        pass
    return names


def place_of(r, lanes, main_only):
    locs = locations(r["loc"])
    if not locs:
        return "main"
    for path, _ in locs:
        hint = lanes.get(path, (None, None))[1]
        if hint is None:
            hint = "wt" if path.startswith((C.SCRIPTS + "Domain/", C.SCRIPTS + "Tests/EditMode/")) else "main"
        stem = os.path.splitext(os.path.basename(path))[0]
        if hint != "wt" or path in main_only or stem in main_only:
            return "main"
    return "wt"


def main():
    C.utf8_stdout()
    ap = C.std_parser(__doc__)
    args = ap.parse_args()
    rows = read_rows(args.out)
    if not rows:
        print("0 rows")
        return 0
    lanes = C.read_lanes_tsv(os.path.join(args.out, "lanes.tsv"))
    main_only = load_main_only(args.out)
    merged = dedupe(rows)
    out_rows = []
    for r, dups in merged:
        r = dict(r)
        locs = locations(r["loc"])
        lane = lanes.get(locs[0][0], ("", ""))[0] if locs else ""
        if lane and r["lane"] not in ("OWNER", "ORCH"):
            r["lane"] = lane
        head = "stale" if is_stale(args.repo, r) else "ok"
        out_rows.append((r, [place_of(r, lanes, main_only), head, ",".join(dups)]))
    path = os.path.join(args.out, "LEDGER.tsv")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\t".join(C.ROW_FIELDS + LEDGER_EXTRA) + "\n")
        for r, extra in out_rows:
            f.write(C.row_line(r, extra) + "\n")
    print("%d rows from %d inputs (%d duplicates folded) -> %s" % (len(out_rows), len(rows), len(rows) - len(out_rows), path))
    for label, fn in (("cat", lambda r, e: r["cat"]), ("sev", lambda r, e: r["sev"]), ("state", lambda r, e: r["state"]),
                      ("lane", lambda r, e: r["lane"] or "-"), ("place", lambda r, e: e[0]), ("head", lambda r, e: e[1])):
        c = Counter(fn(r, e) for r, e in out_rows)
        print("  %-5s %s" % (label, ", ".join("%s %d" % (k, c[k]) for k in sorted(c))))
    return 0


if __name__ == "__main__":
    sys.exit(main())
