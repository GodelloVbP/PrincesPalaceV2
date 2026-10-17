"""Give each scan row its slice id, or its lane when no slice covers the location.

Reads <out>/scan/X*.tsv and <out>/lanes.tsv, plus SLICES.md from the repo; writes
<out>/scan/tagged.tsv. A row whose lane column is already set (X6 OWNER, X9 L6) keeps it.
"""
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C


def main():
    C.utf8_stdout()
    ap = C.std_parser(__doc__)
    ap.add_argument("--slices", help="default <repo>/%s" % C.SLICES_REL)
    args = ap.parse_args()
    slices_path = args.slices or os.path.join(args.repo, C.SLICES_REL)
    try:
        slices = C.parse_slices(open(slices_path, encoding="utf-8").read())
    except OSError:
        slices = []
    lanes = C.read_lanes_tsv(os.path.join(args.out, "lanes.tsv"))
    by_path = {}
    for s in slices:
        for p, a, b in s["files"]:
            by_path.setdefault(p, []).append((a, b, s["id"]))
    out, per = [], {}
    for path in sorted(glob.glob(os.path.join(args.out, "scan", "X*.tsv"))):
        for line in open(path, encoding="utf-8"):
            parsed = C.parse_row(line)
            if not parsed:
                continue
            row, extra = parsed
            m = re.match(r"^(.*?):(\d+)@", row["loc"])
            fpath, ln = (m.group(1), int(m.group(2))) if m else (row["loc"], 0)
            sid = next((i for a, b, i in by_path.get(fpath, ()) if a <= ln <= b), None)
            lane = row["lane"] or lanes.get(fpath, ("", ""))[0]
            row["lane"] = lane
            row["slice"] = sid or lane or "UNOWNED"
            out.append(row)
            per[row["id"].split("-")[0]] = per.get(row["id"].split("-")[0], 0) + 1
    if not out:
        print("0 rows")
        return 0
    with open(os.path.join(args.out, "scan", "tagged.tsv"), "w", encoding="utf-8", newline="\n") as f:
        for r in out:
            f.write(C.row_line(r) + "\n")
    in_slice = sum(1 for r in out if re.match(r"^(S|C|G)\d+$", r["slice"]))
    print("%d rows tagged: %d by slice, %d by lane" % (len(out), in_slice, len(out) - in_slice))
    print(", ".join("%s %d" % (k, per[k]) for k in sorted(per)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
