"""Helpers shared by the sweep scripts. Python 3 stdlib only; run with python -I."""
import argparse
import os
import re
import subprocess
import sys

OUT_DEFAULT = r"C:\Games\sweep-2026-10-17"
REV = "c8d0c631"
BASE = "bc867981"
# The fixtures hold deliberate violations; no real scan or slice may see them.
FIXTURE_PREFIX = "docs/handoffs/cleanup_sweep/scripts/fixtures/"
SLICES_REL = "docs/handoffs/cleanup_sweep/SLICES.md"
SCRIPTS = "Assets/_Project/Scripts/"

ROW_FIELDS = ["id", "slice", "loc", "cat", "sev", "state", "grade", "contract",
              "failure", "fix", "lane"]


def utf8_stdout():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass


def std_parser(desc):
    p = argparse.ArgumentParser(description=desc)
    p.add_argument("--repo", default=os.getcwd())
    p.add_argument("--out", default=OUT_DEFAULT)
    p.add_argument("--no-git", action="store_true",
                   help="walk the disk instead of asking git (fixtures)")
    return p


def git(repo, *args):
    r = subprocess.run(["git", "-C", repo, "-c", "core.quotepath=false", *args],
                       capture_output=True)
    if r.returncode != 0:
        raise RuntimeError("git %s failed: %s" % (" ".join(args), r.stderr.decode("utf-8", "replace")))
    return r.stdout


def has_git(repo, no_git):
    return not no_git and os.path.exists(os.path.join(repo, ".git"))


def list_files(repo, no_git=False):
    """Tracked repo-relative posix paths that exist on disk, fixtures excluded."""
    if has_git(repo, no_git):
        raw = git(repo, "ls-files", "-z").decode("utf-8", "replace")
        out = [p for p in raw.split("\0") if p]
    else:
        out = []
        for d, dirs, fs in os.walk(repo):
            dirs[:] = [x for x in dirs if x != ".git"]
            for f in fs:
                out.append(os.path.relpath(os.path.join(d, f), repo).replace(os.sep, "/"))
    out = [p for p in out if not p.startswith(FIXTURE_PREFIX)]
    return sorted(p for p in out if os.path.isfile(os.path.join(repo, p)))


def read_text(repo, rel):
    try:
        with open(os.path.join(repo, rel), "rb") as f:
            return f.read().decode("utf-8", "replace")
    except OSError:
        return ""


def read_bytes(repo, rel):
    try:
        with open(os.path.join(repo, rel), "rb") as f:
            return f.read()
    except OSError:
        return b""


def ensure_dir(p):
    os.makedirs(p, exist_ok=True)
    return p


def _skip_string(s, i):
    """Index of the last char of the string literal starting at i."""
    n = len(s)
    j = i
    verbatim = False
    while j < n and s[j] in "@$":
        if s[j] == "@":
            verbatim = True
        j += 1
    j += 1
    while j < n:
        if verbatim:
            if s[j] == '"':
                if s[j + 1:j + 2] == '"':
                    j += 2
                    continue
                break
        else:
            if s[j] == "\\":
                j += 2
                continue
            if s[j] == '"' or s[j] == "\n":
                break
        j += 1
    return min(j, n - 1)


def _is_string_start(s, i):
    c = s[i]
    if c == '"':
        return True
    if c in "@$":
        k = i
        while k < len(s) and s[k] in "@$":
            k += 1
        return k < len(s) and s[k] == '"'
    return False


def _blank(seg):
    return "".join("\n" if ch == "\n" else " " for ch in seg)


def _scan_cs(text, blank_strings, comments_only=False):
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        two = text[i:i + 2]
        if two == "//":
            j = i
            while j < n and text[j] != "\n":
                j += 1
            out.append(text[i:j] if comments_only else _blank(text[i:j]))
            i = j
        elif two == "/*":
            j = text.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(text[i:j] if comments_only else _blank(text[i:j]))
            i = j
        elif _is_string_start(text, i):
            j = _skip_string(text, i) + 1
            seg = text[i:j]
            out.append(_blank(seg) if (blank_strings or comments_only) else seg)
            i = j
        elif c == "'":
            j = i + 1
            while j < n and text[j] != "'" and text[j] != "\n":
                j += 2 if text[j] == "\\" else 1
            seg = text[i:j + 1]
            out.append(_blank(seg) if (blank_strings or comments_only) else seg)
            i = j + 1
        else:
            out.append(("\n" if c == "\n" else " ") if comments_only else c)
            i += 1
    return "".join(out)


def strip_cs(text):
    """Comments blanked, strings kept; newlines survive so line numbers hold."""
    return _scan_cs(text, False)


def comment_text_cs(text):
    """Only the comments survive; everything else is blanked, newlines kept."""
    return _scan_cs(text, False, True)


def mask_cs(text):
    """Comments and strings blanked: safe for brace matching and declaration regexes."""
    return _scan_cs(text, True)


def line_of(text, pos):
    return text.count("\n", 0, pos) + 1


def parse_row(line):
    """The 11 row fields from a tab row or a ' | ' row, else None."""
    line = line.rstrip("\r\n")
    if "\t" in line:
        parts = line.split("\t")
    elif line.count(" | ") >= 10:
        parts = line.split(" | ", 10)
    else:
        return None
    parts = [x.strip() for x in parts]
    if len(parts) < 11 or not re.match(r"^[A-Za-z0-9]+-\d+$", parts[0]):
        return None
    return dict(zip(ROW_FIELDS, parts[:11])), parts[11:]


def row_line(d, extra=()):
    return "\t".join([d.get(k, "") for k in ROW_FIELDS] + list(extra))


def clean(s):
    return re.sub(r"[\t\r\n]+", " ", s)


def make_row(rid, loc, cat, sev, contract, failure, fix, lane="", slice_="-"):
    return {"id": rid, "slice": slice_, "loc": loc, "cat": cat, "sev": sev,
            "state": "candidate", "grade": "static", "contract": clean(contract),
            "failure": clean(failure), "fix": clean(fix), "lane": lane}


def parse_slices(md_text):
    """Slices from SLICES.md: dicts with id, tier, wave, dgroup, files[(path,a,b)]."""
    out = []
    for line in md_text.splitlines():
        if not re.match(r"^\| (S|C|G)\d+ \|", line):
            continue
        parts = [x.strip() for x in line.strip().strip("|").split(" | ", 8)]
        if len(parts) < 9:
            continue
        files = []
        for ent in parts[8].split("; "):
            m = re.match(r"^(.*):(\d+)-(\d+)$", ent.strip())
            if m:
                files.append((m.group(1), int(m.group(2)), int(m.group(3))))
        out.append({"id": parts[0], "tier": parts[1], "wave": parts[2], "why": parts[3], "dgroup": parts[4],
                    "files": files})
    return out


def read_lanes_tsv(path):
    res = {}
    try:
        with open(path, encoding="utf-8") as f:
            for line in f:
                p = line.rstrip("\n").split("\t")
                if len(p) >= 3 and p[0] != "path":
                    res[p[0]] = (p[1], p[2])
    except OSError:
        pass
    return res
