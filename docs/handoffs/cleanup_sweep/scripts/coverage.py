"""Coverage gates: `scan` (P1) and `notes` (P2).

scan:  every X1-X9, H5, H6*, H9 file under <out>/scan exists and holds rows or a "0 rows: <pattern>" line.
notes: every slice in SLICES.md has <out>/notes/<slice>.md whose first line is "coverage: full".
"""
import glob
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

REQUIRED = ["X%d" % i for i in range(1, 10)] + ["H5", "H6*", "H9"]


def file_ok(path):
    """(ok, reason) for one scan file."""
    try:
        lines = [l for l in open(path, encoding="utf-8").read().splitlines() if l.strip()]
    except OSError:
        return False, "unreadable"
    if not lines:
        return False, "empty"
    if lines[0].startswith("0 rows:"):
        return True, ""
    if any(C.parse_row(l) for l in lines):
        return True, ""
    return False, "no rows and no '0 rows: <pattern>' line"


def check_scan(out):
    failures = []
    for name in REQUIRED:
        hits = sorted(glob.glob(os.path.join(out, "scan", name + ".tsv")))
        if not hits:
            failures.append("%s: missing" % name)
            continue
        for h in hits:
            ok, why = file_ok(h)
            if not ok:
                failures.append("%s: %s" % (os.path.basename(h), why))
    return failures


def check_notes(out, slices_path):
    try:
        slices = C.parse_slices(open(slices_path, encoding="utf-8").read())
    except OSError:
        return None, ["SLICES.md unreadable: %s" % slices_path]
    failures = []
    for s in slices:
        p = os.path.join(out, "notes", s["id"] + ".md")
        try:
            first = open(p, encoding="utf-8").readline().strip()
        except OSError:
            failures.append("%s: no notes file" % s["id"])
            continue
        if first != "coverage: full":
            failures.append("%s: first line is %r, not 'coverage: full'" % (s["id"], first))
    return len(slices), failures


def main():
    C.utf8_stdout()
    ap = C.std_parser(__doc__)
    ap.add_argument("gate", choices=["scan", "notes"])
    ap.add_argument("--slices", help="default <repo>/%s" % C.SLICES_REL)
    args = ap.parse_args()
    if args.gate == "scan":
        failures = check_scan(args.out)
        print("scan coverage: %d failures" % len(failures))
    else:
        n, failures = check_notes(args.out, args.slices or os.path.join(args.repo, C.SLICES_REL))
        if n == 0:
            print("0 rows")
            return 0
        print("notes coverage: %d failures" % len(failures))
    for f in failures:
        print("  FAIL", f)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
