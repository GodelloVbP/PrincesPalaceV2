"""Script scans X1-X9 (PLAN_CLEANUP_SWEEP.md section 4, P1).

Each scan writes <out>/scan/X<n>.tsv in the BRIEF_COMMON row format (tab separated),
or one "0 rows: <pattern>" line. Locations carry the base revision tag even though the
files are read from the working tree; the sweep keeps the tree at that revision.
"""
import json
import os
import re
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

S = C.SCRIPTS
TAG = "@" + C.REV
OWNER_EXCLUDED = ("Assets/_Project/Art/Sheets/Spells/Velvet Shackles/", "docs/captures/run_up/")
UNITY_MESSAGES = set("""Awake Start Update LateUpdate FixedUpdate OnEnable OnDisable OnDestroy OnGUI OnValidate Reset
OnApplicationQuit OnApplicationFocus OnApplicationPause OnPointerEnter OnPointerExit OnPointerDown OnPointerUp
OnPointerClick OnBeginDrag OnDrag OnEndDrag OnDrop OnScroll OnSelect OnDeselect OnSubmit OnCancel OnMove
OnUpdateSelected OnInitializePotentialDrag OnRectTransformDimensionsChange OnPreRender OnPostRender OnRenderImage
OnDrawGizmos OnDrawGizmosSelected OnTriggerEnter OnTriggerExit OnTriggerStay OnCollisionEnter OnCollisionExit
OnMouseDown OnMouseUp OnMouseEnter OnMouseExit OnTransformChildrenChanged OnDidApplyAnimationProperties
OnCanvasGroupChanged OnBecameVisible OnBecameInvisible Main Dispose ToString Equals GetHashCode""".split())
KEYWORDS = set("if for foreach while switch using lock catch fixed return when nameof else new await yield case throw "
               "in out ref typeof sizeof default checked unchecked".split())


class Ctx:
    def __init__(self, repo, no_git):
        self.repo, self.no_git = repo, no_git
        self.files = [p for p in C.list_files(repo, no_git) if not p.startswith(OWNER_EXCLUDED)]
        self._cache = {}

    def text(self, p):
        if p not in self._cache:
            self._cache[p] = C.read_text(self.repo, p)
        return self._cache[p]

    def prod_cs(self):
        return [p for p in self.files if p.startswith(S) and p.endswith(".cs") and not p.startswith(S + "Tests/")]

    def test_cs(self):
        return [p for p in self.files if p.startswith(S + "Tests/") and p.endswith(".cs")]


class Rows:
    def __init__(self, xid):
        self.xid, self.rows = xid, []

    def add(self, loc, cat, sev, contract, failure, fix, lane=""):
        rid = "%s-%04d" % (self.xid, len(self.rows) + 1)
        self.rows.append(C.make_row(rid, loc, cat, sev, contract, failure, fix, lane))


def loc(path, line, symbol):
    return "%s:%d%s (%s)" % (path, line, TAG, symbol)


def match_brace(masked, open_idx):
    """Index of the brace closing the one at open_idx, in comment- and string-free text."""
    depth = 0
    for i in range(open_idx, len(masked)):
        c = masked[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
    return len(masked) - 1


# ---------------------------------------------------------------- X1

TYPE_DECL = re.compile(r"\b(?:class|struct|enum|interface|record)\s+([A-Za-z_]\w*)")
DELEGATE_DECL = re.compile(r"\bdelegate\s+[\w.<>\[\]?,\s]+?\s+([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(")
TYPE_PAT = r"[\w.]+(?:<[^;(){}=]*?>)?(?:\[[,\s]*\])*\??"
MODS = r"(?:public|private|protected|internal|static|readonly|const|virtual|abstract|sealed|async|new|unsafe|extern|partial|override|volatile)"
METHOD_DECL = re.compile(r"^[ \t]*(?:\[[^\n\]]*\][ \t]*)*((?:%s\s+)+)%s\s+([A-Za-z_]\w*)\s*(?:<[^>(]*>)?\s*\(" % (MODS, TYPE_PAT), re.M)
FIELD_DECL = re.compile(r"^[ \t]*(?:\[[^\n\]]*\][ \t]*)*((?:%s\s+)+)%s\s+([A-Za-z_]\w*)\s*(?:=(?!>)|;|\{)" % (MODS, TYPE_PAT), re.M)
IDENT = re.compile(r"[A-Za-z_]\w*")
STRING_LIT = re.compile(r'@?"((?:[^"\\\n]|\\.)*)"')


def declarations(masked):
    """(name, kind, pos) for types, methods, fields/properties and enum members."""
    out = []
    for m in TYPE_DECL.finditer(masked):
        out.append((m.group(1), "type", m.start(1)))
        if masked[m.start():m.start(1)].startswith("enum"):
            ob = masked.find("{", m.end())
            if ob >= 0:
                cb = match_brace(masked, ob)
                body = masked[ob + 1:cb]
                for em in re.finditer(r"(?:^|,)\s*(?:\[[^\]]*\]\s*)*([A-Za-z_]\w*)\s*(?:=[^,]*)?(?=,|$)", body.strip("\n") if False else body):
                    out.append((em.group(1), "enum member", ob + 1 + em.start(1)))
    for m in DELEGATE_DECL.finditer(masked):
        out.append((m.group(1), "delegate", m.start(1)))
    for m in METHOD_DECL.finditer(masked):
        if "override" in m.group(1) or m.group(2) in KEYWORDS:
            continue
        out.append((m.group(2), "method", m.start(2)))
    for m in FIELD_DECL.finditer(masked):
        if "override" in m.group(1) or m.group(2) in KEYWORDS:
            continue
        out.append((m.group(2), "member", m.start(2)))
    return out


def string_literals(stripped):
    return [m.group(1) for m in STRING_LIT.finditer(stripped)]


def words(s):
    return IDENT.findall(s)


def build_x1_indexes(ctx):
    prod, tests = ctx.prod_cs(), ctx.test_cs()
    idx = {"code": Counter(), "test": Counter(), "decl": defaultdict(list), "lit": Counter(),
           "lit_ui": set(), "lit_path": set(), "scene": [], "content": set(), "tools": set(),
           "resfiles": set()}
    for p in prod:
        raw = ctx.text(p)
        masked, stripped = C.mask_cs(raw), C.strip_cs(raw)
        idx["code"].update(IDENT.findall(masked))
        for name, kind, pos in declarations(masked):
            idx["decl"][name].append((p, C.line_of(masked, pos), kind, masked[:pos].count("\n")))
        ui = p.startswith(S + "Domain/UiKit/Screens/") or p.startswith(S + "Editor/SceneBuilder/")
        for lit in string_literals(stripped):
            if ui:
                idx["lit_ui"].add(lit)
            else:
                idx["lit"][lit] += 1
            if "/" in lit:
                for seg in re.split(r"[/.\\]", lit):
                    if seg:
                        idx["lit_path"].add(seg)
    for p in tests:
        idx["test"].update(IDENT.findall(C.mask_cs(ctx.text(p))))
    for p in ctx.files:
        low = p.lower()
        if low.endswith((".unity", ".prefab")):
            idx["scene"].append(ctx.text(p))
        elif p.startswith("Assets/_Project/ContentData/") and low.endswith(".json"):
            idx["content"].update(IDENT.findall(ctx.text(p)))
        elif p.startswith("tools/") and not low.endswith((".png", ".jpg", ".wav", ".ogg", ".mp3", ".gif", ".meta")):
            idx["tools"].update(IDENT.findall(ctx.text(p)))
        if "/Resources/" in p and not p.endswith(".meta"):
            idx["resfiles"].add(os.path.splitext(os.path.basename(p))[0])
    keys = set()
    for t in idx["scene"]:
        keys.update(re.findall(r"^\s*(?:- )?([A-Za-z_]\w*):", t, re.M))
        keys.update(re.findall(r"m_MethodName: ([A-Za-z_]\w*)", t))
    idx["scene_keys"] = keys
    return idx


# One predicate per "dead" form in BRIEF_COMMON; a name is dead only when all are False.
def form_csharp(idx, name, decls):
    return idx["code"][name] - len(decls) > 0


def form_uibind(idx, name, decls):
    return name in idx["lit_ui"]


def form_scene(idx, name, decls):
    return name in idx["scene_keys"]


def form_resources(idx, name, decls):
    return name in idx["lit_path"] or name in idx["resfiles"]


def form_content(idx, name, decls):
    return name in idx["content"]


def form_reflection(idx, name, decls):
    return idx["lit"][name] > 0


def form_tests(idx, name, decls):
    return idx["test"][name] > 0


def form_tools(idx, name, decls):
    return name in idx["tools"]


FORMS = [("csharp", form_csharp), ("uibind", form_uibind), ("scene", form_scene), ("resources", form_resources),
         ("content", form_content), ("reflection", form_reflection), ("tests", form_tests), ("tools", form_tools)]


def scan_x1(ctx):
    r = Rows("X1")
    idx = build_x1_indexes(ctx)
    for name in sorted(idx["decl"]):
        decls = idx["decl"][name]
        if len(decls) != 1 or len(name) < 3 or name in UNITY_MESSAGES:
            continue
        if any(fn(idx, name, decls) for _, fn in FORMS):
            continue
        p, line, kind, _ = decls[0]
        r.add(loc(p, line, name), "SIMP", "S3",
              "zero references in C#, ui binding, scenes, Resources paths, ContentData, reflection, tests and tools (BRIEF_COMMON)",
              "n/a: %s declared once and referenced nowhere" % kind, "delete %s" % name)
    return r, "identifier declared once, referenced in no dead-form corpus"


# ---------------------------------------------------------------- X2

PHRASES = [
    r"\bused to\b", r"\bno longer\b", r"\bpreviously\b", r"\bformerly\b", r"\boriginally\b",
    r"\bwas (?:removed|deleted|renamed|replaced|moved|added|changed)\b",
    r"\bphase [A-Z0-9]\b", r"\bM\d{1,2}\b", r"\b20\d\d-\d\d-\d\d\b",
    r"\bowner (?:said|asked|wants|wanted|decided)\b", r"\b(?:TODO|FIXME|HACK|XXX)\b", r"\bfor now\b",
    r"\btemporar(?:y|ily)\b", r"\bold (?:version|behaviou?r|code|path)\b", r"\blegacy\b", r"\bdeprecated\b",
    r"\bnothing (?:reads|uses|calls) (?:this|it)\b", r"\bnot yet (?:wired|authored|implemented|used)\b",
    r"\bstill (?:unused|unwired)\b",
]
PHRASE_RE = re.compile("|".join("(%s)" % p for p in PHRASES), re.I)


def comments_cs(text):
    """(line, comment text) for every comment line of a C# file."""
    out = []
    for i, l in enumerate(C.comment_text_cs(text).split("\n"), 1):
        if l.strip():
            out.append((i, l.strip()))
    return out


def comments_hash(text):
    out = []
    for i, l in enumerate(text.splitlines(), 1):
        j = 0
        while True:
            j = l.find("#", j)
            if j < 0:
                break
            if l[:j].count('"') % 2 == 0 and l[:j].count("'") % 2 == 0:
                out.append((i, l[j + 1:]))
                break
            j += 1
    return out


def scan_x2(ctx):
    r = Rows("X2")
    for p in ctx.files:
        if p.startswith(S + "Tests/"):
            continue
        if p.startswith(S) and p.endswith(".cs"):
            cs = comments_cs(ctx.text(p))
        elif p.startswith("tools/") and p.endswith((".py", ".ps1", ".psm1")):
            cs = comments_hash(ctx.text(p))
        else:
            continue
        for line, text in cs:
            hits = []
            for m in PHRASE_RE.finditer(text):
                hits.append(m.group(0))
            if hits:
                r.add(loc(p, line, "; ".join(sorted(set(h.lower() for h in hits)))), "DRIFT", "S4",
                      "comments say why, in present tense; history belongs in the commit (CLAUDE.md)",
                      "state: comment carries history or a claim to verify -> may be false today",
                      "verify the claim, then rewrite or delete the comment")
    return r, "history or false-comment phrases in comments"


# ---------------------------------------------------------------- X3

HOT_RE = re.compile(r"\bvoid\s+(Update|LateUpdate|FixedUpdate|OnGUI)\s*\(\s*\)\s*(\{|=>)")
GEN = r"<[^;(){}]*>"
HOT_PATTERNS = [
    (re.compile(r"\bnew\s+(?:List|Dictionary|HashSet|Queue|Stack|SortedDictionary|StringBuilder|Material|Texture2D|"
                r"RenderTexture|GameObject|WaitForSeconds|WaitForEndOfFrame|Action|Func|Predicate)\b"), "allocation"),
    (re.compile(r"\bnew\s+[\w.]+\s*\["), "array allocation"),
    (re.compile(r"\.(?:ToList|ToArray|ToDictionary|ToHashSet|Select|Where|OrderBy|OrderByDescending|Concat|Distinct|"
                r"GroupBy|SelectMany|Zip|Skip|Take)\s*\("), "LINQ"),
    (re.compile(r"\bGameObject\.Find\w*\s*\("), "lookup"),
    (re.compile(r"\bFind(?:Object|Objects|FirstObject|AnyObject)(?:OfType|sOfType|ByType|sByType)?\s*[<(]"), "lookup"),
    (re.compile(r"\bGetComponent(?:s)?(?:InChildren|InParent)?\s*[<(]"), "lookup"),
    (re.compile(r"\bCamera\.main\b"), "lookup"),
    (re.compile(r"\bResources\.(?:Load|LoadAll|FindObjectsOfTypeAll)\b"), "lookup"),
    (re.compile(r"\.Find(?:Child)?\s*\("), "lookup"),
    (re.compile(r"\bShader\.Find\b"), "lookup"),
    (re.compile(r"\bSendMessage\s*\("), "lookup"),
    (re.compile(r"\bInstantiate\s*[<(]"), "allocation"),
    (re.compile(r"\bstring\.(?:Format|Concat|Join)\s*\("), "string allocation"),
    (re.compile(r"\.ToString\s*\("), "string allocation"),
    (re.compile(r"\bEnum\.GetValues\b"), "allocation"),
]


def hot_bodies(masked):
    """(method, body_start, body_end) for each per-frame method, found by brace matching."""
    for m in HOT_RE.finditer(masked):
        if m.group(2) == "{":
            start = m.end() - 1
            end = match_brace(masked, start)
        else:
            start = m.end()
            end = masked.find(";", start)
            end = len(masked) if end < 0 else end
        yield m.group(1), start, end


def scan_x3(ctx):
    r = Rows("X3")
    for p in ctx.prod_cs():
        if not p.startswith((S + "Core/", S + "Domain/UiKit/")):
            continue
        raw = ctx.text(p)
        masked, stripped = C.mask_cs(raw), C.strip_cs(raw)
        for meth, a, b in hot_bodies(masked):
            body = masked[a:b + 1]
            cls = "?"
            for cm in re.finditer(r"\b(?:class|struct)\s+(\w+)", masked[:a]):
                cls = cm.group(1)
            seen = set()
            for rx, kind in HOT_PATTERNS:
                for m in rx.finditer(body):
                    ln = C.line_of(masked, a + m.start())
                    key = (ln, m.group(0))
                    if key in seen:
                        continue
                    seen.add(key)
                    r.add(loc(p, ln, "%s.%s: %s" % (cls, meth, m.group(0).strip())), "PERF", "S3",
                          "per-frame methods allocate nothing and look nothing up (CODE_STANDARDS)",
                          "state: %s runs every frame (%s) -> GC or lookup cost per frame" % (kind, meth),
                          "cache the result or hoist it out of %s" % meth)
            for m in re.finditer(r'\$@?"', stripped[a:b + 1]):
                ln = C.line_of(stripped, a + m.start())
                if masked[a + m.start():a + m.start() + 1] != " ":
                    continue
                if (ln, "$") in seen:
                    continue
                seen.add((ln, "$"))
                r.add(loc(p, ln, "%s.%s: interpolated string" % (cls, meth)), "PERF", "S3",
                      "per-frame methods allocate nothing and look nothing up (CODE_STANDARDS)",
                      "state: string interpolation runs every frame (%s) -> allocation per frame" % meth,
                      "format only when the value changes")
    return r, "allocating or lookup calls inside Update/LateUpdate/FixedUpdate/OnGUI bodies in Core and UiKit"


# ---------------------------------------------------------------- X4

HELPER_DECL = re.compile(r"^[ \t]*(?:\[[^\n\]]*\][ \t]*)*((?:%s\s+)*)(%s)\s+([A-Za-z_]\w*)\s*(?:<[^>(]*>)?\s*\(([^()]*)\)\s*(?:where\s[^{;]*?)?\s*(?:\{|=>)" % (MODS, TYPE_PAT), re.M)


def norm_params(raw):
    parts, depth, cur = [], 0, ""
    for ch in raw:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    types = []
    for p in parts:
        p = re.sub(r"=.*$", "", p, flags=re.S).strip()
        p = re.sub(r"^\[[^\]]*\]\s*", "", p)
        p = re.sub(r"^(?:this|ref|out|in|params)\s+", "", p)
        p = re.sub(r"\s+[A-Za-z_]\w*$", "", p)
        types.append(re.sub(r"\s+", "", p))
    return ",".join(types)


def scan_x4(ctx):
    r = Rows("X4")
    groups = defaultdict(list)
    for p in ctx.prod_cs():
        masked = C.mask_cs(ctx.text(p))
        for m in HELPER_DECL.finditer(masked):
            mods, rtype, name, params = m.group(1), m.group(2), m.group(3), m.group(4)
            if name in KEYWORDS or rtype in KEYWORDS or name in UNITY_MESSAGES or len(name) < 4:
                continue
            if "override" in mods or re.search(r"\b(public|protected|internal)\b", mods) and "static" not in mods:
                continue
            if re.search(r"\b(public|protected)\b", mods):
                continue
            groups[(name, norm_params(params))].append((p, C.line_of(masked, m.start(3))))
    for (name, sig), occ in sorted(groups.items()):
        files = sorted(set(p for p, _ in occ))
        if len(files) < 2:
            continue
        p, line = sorted(occ)[0]
        r.add(loc(p, line, "%s(%s)" % (name, sig)), "SIMP", "S3",
              "one home per fact (CLAUDE.md)",
              "state: same-named helper declared in %d files: %s" % (len(files), ", ".join(os.path.basename(f) for f in files)),
              "extract one shared helper if the bodies agree")
    return r, "same-named private or static helpers in two or more files"


# ---------------------------------------------------------------- X5

TEXT_EXT = (".cs", ".ps1", ".psm1", ".py", ".md", ".json", ".txt", ".yml", ".yaml", ".asmdef", ".props", ".slnx", ".csproj", ".toml", ".cfg")


def scan_x5(ctx):
    r = Rows("X5")
    cands = [p for p in ctx.files if p.startswith("tools/") and p.endswith((".ps1", ".py", ".psm1"))
             and not p.startswith(("tools/githooks/", "tools/domain-tests/"))
             and "__pycache__" not in p and "_fixture" not in p and not p.endswith("_test.py")]
    corpus = {}
    for p in ctx.files:
        base = os.path.basename(p)
        if p.endswith(TEXT_EXT) or "." not in base:
            if p.startswith(("docs/captures/", "Assets/_Project/Art/", "Assets/_Project/Resources/")):
                continue
            corpus[p] = ctx.text(p)
    for p in cands:
        base = os.path.basename(p)
        stem = os.path.splitext(base)[0]
        used = False
        imp = re.compile(r"(?:^|\n)\s*(?:from|import)\s+%s\b" % re.escape(stem)) if p.endswith(".py") else None
        for q, t in corpus.items():
            if q == p:
                continue
            if base in t or (imp and imp.search(t)):
                used = True
                break
        if not used:
            r.add(loc(p, 1, base), "SIMP", "S3", "a tool is run by a person, a hook, a gate or another tool",
                  "n/a: no file names this script", "delete it, or document who runs it")
    return r, "tools/ scripts named by no other file"


# ---------------------------------------------------------------- X6

GUID_RE = re.compile(r"[0-9a-f]{32}")
GUID_SCAN_EXT = (".unity", ".prefab", ".asset", ".mat", ".anim", ".controller", ".json", ".cs", ".shader", ".meta",
                 ".overrideController", ".mixer", ".playable", ".txt")


def scan_x6(ctx):
    r = Rows("X6")
    roots = ("Assets/_Project/Art/", "Assets/_Project/Resources/")
    fileset = set(ctx.files)
    cands = [p for p in ctx.files if p.startswith(roots) and not p.endswith(".meta") and (p + ".meta") in fileset]
    guid_files = defaultdict(set)
    literals = set()
    for p in ctx.files:
        if p.endswith(GUID_SCAN_EXT) and not p.startswith(("docs/", "Library/")):
            t = ctx.text(p)
            for g in set(GUID_RE.findall(t)):
                guid_files[g].add(p)
            if p.endswith(".cs"):
                literals.update(string_literals(C.strip_cs(t)))
            elif p.startswith("Assets/_Project/ContentData/") and p.endswith(".json"):
                literals.update(re.findall(r'"([^"\n]+)"', t))
    words_all = set()
    for lit in literals:
        words_all.update(re.split(r"[/\\]", lit))
        words_all.add(lit)
    dir_prefixes = [l for l in literals if l.endswith("/") and len(l) >= 6]
    for p in cands:
        meta = ctx.text(p + ".meta")
        gm = re.search(r"guid:\s*([0-9a-f]{32})", meta)
        by_guid = gm and any(q != p + ".meta" for q in guid_files.get(gm.group(1), ()))
        rel = p[len("Assets/_Project/Resources/"):] if p.startswith("Assets/_Project/Resources/") else p[len("Assets/_Project/"):]
        rel_ne = os.path.splitext(rel)[0]
        stem = os.path.splitext(os.path.basename(p))[0]
        by_path = (rel in literals or rel_ne in literals or any(rel_ne.startswith(d) for d in dir_prefixes)
                   or stem in words_all or any(rel_ne in l or l.endswith(rel_ne) for l in literals if len(l) > 6 and "/" in l))
        if not by_guid and not by_path:
            r.add(loc(p, 1, os.path.basename(p)), "SIMP", "S3", "an asset is loaded by path, GUID or scene reference",
                  "n/a: no GUID reference and no path or stem string names it (owner digest only)",
                  "owner decides: delete or keep", "OWNER")
    for p in ctx.files:
        if p.endswith(".meta") and p.startswith("Assets/"):
            base = p[:-5]
            if base not in fileset and not any(q.startswith(base + "/") for q in fileset):
                r.add(loc(p, 1, os.path.basename(p)), "SIMP", "S3", "every .meta pairs with an asset (pre-commit hook)",
                      "state: orphan .meta -> Unity warns and regenerates", "delete the .meta", "OWNER")
    return r, "Art/Audio/Resources assets with no GUID or path reference, and orphan .meta files"


# ---------------------------------------------------------------- X7

def scan_x7(ctx):
    r = Rows("X7")
    for p in ctx.prod_cs():
        if not p.startswith(S + "Domain/"):
            continue
        masked = C.mask_cs(ctx.text(p))
        for m in re.finditer(r"^\s*using\s+(?:static\s+)?(UnityEngine|UnityEditor|TMPro)\b[\w.]*\s*;|\bUnityEngine\.\w+", masked, re.M):
            r.add(loc(p, C.line_of(masked, m.start()), m.group(0).strip()), "ARCH", "strong",
                  "Domain is engine-free (asmdef noEngineReferences)",
                  "state: Domain code names the engine -> Domain cannot move to the dotnet loop", "move the engine call behind a Core adapter")
    asm = {}
    for p in ctx.files:
        if p.endswith(".asmdef") and "TextMesh Pro" not in p:
            try:
                asm[p] = json.loads(ctx.text(p))
            except ValueError:
                continue

    def rank(name, path):
        if name.endswith(".Domain.Tests") or ".Domain" in name and "Tests" in name:
            return 0
        if "Tests" in name:
            return 1
        if name.endswith(".Domain"):
            return 0
        if name.endswith(".Core"):
            return 1
        if name.endswith(".Editor"):
            return 2
        return None
    for p, d in sorted(asm.items()):
        name = d.get("name", "")
        me = rank(name, p)
        if me == 0 and not d.get("noEngineReferences", False) and not name.endswith("Tests"):
            r.add(loc(p, 1, name), "ARCH", "strong", "Domain is engine-free (asmdef noEngineReferences)",
                  "state: noEngineReferences is false -> engine types compile into Domain", "set noEngineReferences true")
        for ref in d.get("references", []):
            other = rank(ref, p)
            if me is not None and other is not None and other > me:
                r.add(loc(p, 1, "%s -> %s" % (name, ref)), "ARCH", "strong",
                      "layers point down: Domain, then Core, then Editor",
                      "state: lower layer references a higher one -> inverted dependency", "drop the reference")
    return r, "UnityEngine in Domain, and asmdef references against the layer order"


# ---------------------------------------------------------------- X8

def ps1_violations(text):
    """(line, column, char) of non-ASCII characters outside comments."""
    out = []
    in_block = False
    for ln, line in enumerate(text.split("\n"), 1):
        sq = dq = False
        i = 0
        while i < len(line):
            c = line[i]
            if in_block:
                if line[i:i + 2] == "#>":
                    in_block = False
                    i += 2
                    continue
                i += 1
                continue
            if not sq and not dq:
                if line[i:i + 2] == "<#":
                    in_block = True
                    i += 2
                    continue
                if c == "#":
                    break
                if c == "`":
                    i += 2
                    continue
            if c == "'" and not dq:
                sq = not sq
            elif c == '"' and not sq:
                dq = not dq
            elif c == "`" and dq:
                i += 2
                continue
            if ord(c) > 127:
                out.append((ln, i + 1, c))
            i += 1
    return out


def scan_x8(ctx):
    r = Rows("X8")
    for p in ctx.files:
        if not p.endswith(".ps1"):
            continue
        data = C.read_bytes(ctx.repo, p)
        if data.startswith(b"\xef\xbb\xbf"):
            r.add(loc(p, 1, "BOM"), "BUG", "S2", "ps1 files are pure ASCII outside comments, no BOM (CLAUDE.md)",
                  "state: BOM present -> hook and PS 5.1 disagree on encoding", "strip the BOM")
        for ln, col, ch in ps1_violations(data.decode("utf-8-sig", "replace")):
            r.add(loc(p, ln, "U+%04X col %d" % (ord(ch), col)), "BUG", "S2",
                  "ps1 files are pure ASCII outside comments, no BOM (CLAUDE.md)",
                  "state: non-ASCII character in code -> PS 5.1 misparses the string", "replace with an ASCII form")
    return r, ".ps1 files with non-ASCII outside comments, or a BOM"


# ---------------------------------------------------------------- X9

CONTENT_DIRS = (S + "Domain/Content/", S + "Core/Content/")
CONTENT_TYPE = re.compile(r"^(Raw\w*Entry|Resolved\w+|\w+Definition(?:Asset)?)$")
ITEMISH = re.compile(r"Item|SetPiece")
MEMBER_RE = re.compile(r"^[ \t]*((?:\[[^\n\]]*\][ \t]*)*)((?:%s\s+)+)(%s)\s+([A-Za-z_]\w*)\s*(=(?!>)|;|\{)" % (MODS, TYPE_PAT), re.M)


def class_fields(masked, raw):
    """(class, field, pos) for instance fields at class-body depth, public or [SerializeField]."""
    out = []
    for cm in re.finditer(r"\b(?:class|struct)\s+([A-Za-z_]\w*)[^{;]*\{", masked):
        name = cm.group(1)
        ob = cm.end() - 1
        cb = match_brace(masked, ob)
        body = masked[ob + 1:cb]
        depth = 0
        depth_at = []
        for ch in body:
            depth_at.append(depth)
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
        for m in MEMBER_RE.finditer(body):
            if depth_at[m.start(4)] != 0:
                continue
            mods = m.group(2)
            if re.search(r"\b(static|const)\b", mods):
                continue
            attrs = raw[ob + 1 + m.start(1):ob + 1 + m.end(1)]
            if "public" not in mods and "SerializeField" not in attrs:
                continue
            out.append((name, m.group(4), ob + 1 + m.start(4)))
    return out


def json_keys(text):
    """(key, nondefault) for every object key anywhere in a JSON document."""
    try:
        doc = json.loads(text)
    except ValueError:
        return []
    out = []

    def walk(v):
        if isinstance(v, dict):
            for k, x in v.items():
                out.append((k, x not in (None, "", 0, 0.0, False, [], {})))
                walk(x)
        elif isinstance(v, list):
            for x in v:
                walk(x)
    walk(doc)
    return out


def lower_first(s):
    return s[:1].lower() + s[1:]


def scan_x9(ctx):
    r = Rows("X9")
    content = [p for p in ctx.prod_cs() if p.startswith(CONTENT_DIRS)]
    fields, all_names = [], set()
    for p in content:
        raw = ctx.text(p)
        masked = C.mask_cs(raw)
        for cls, fname, pos in class_fields(masked, raw):
            all_names.add(fname)
            all_names.add(lower_first(fname))
            if CONTENT_TYPE.match(cls):
                fields.append((p, cls, fname, C.line_of(masked, pos)))
    reads = Counter()          # reads outside the content folders
    reads_by_file = defaultdict(Counter)
    for p in ctx.prod_cs():
        if p.startswith(CONTENT_DIRS) or p == S + "Editor/ContentBuilder.cs":
            if p.startswith(CONTENT_DIRS):
                for m in re.finditer(r"\.\s*([A-Za-z_]\w*)\b(?!\s*=(?![=>]))", C.mask_cs(ctx.text(p))):
                    reads_by_file[p][m.group(1)] += 1
            continue
        for m in re.finditer(r"\.\s*([A-Za-z_]\w*)\b(?!\s*=(?![=>]))", C.mask_cs(ctx.text(p))):
            reads[m.group(1)] += 1
    jfiles = [p for p in ctx.files if p.startswith("Assets/_Project/ContentData/") and p.endswith(".json")]
    nondefault = defaultdict(set)   # key -> json file names that set it to a non-default value
    keys_by_file = {}
    for p in jfiles:
        ks = json_keys(ctx.text(p))
        keys_by_file[p] = ks
        for k, nd in ks:
            if nd:
                nondefault[k].add(os.path.basename(p))
    for p, cls, fname, line in fields:
        n = reads[fname]
        if cls.startswith("Raw"):
            n += sum(c[fname] for q, c in reads_by_file.items() if q != p)
        if n:
            continue
        files = nondefault.get(lower_first(fname), set()) | nondefault.get(fname, set())
        live_files = {f for f in files if f not in ("items.json", "itemsets.json")}
        itemish = bool(ITEMISH.search(cls)) or bool(ITEMISH.search(os.path.basename(p)))
        if live_files and not itemish:
            r.add(loc(p, line, "%s.%s" % (cls, fname)), "BUG", "S2",
                  "a field accepted from content is read by something (CONTENT_SCHEMA)",
                  "state: %s sets it to a non-default value -> authored value has no effect" % ", ".join(sorted(live_files)),
                  "wire a reader or drop the field and the JSON key", "L6")
        else:
            r.add(loc(p, line, "%s.%s" % (cls, fname)), "SIMP", "S3",
                  "a field accepted from content is read by something (CONTENT_SCHEMA)",
                  "n/a: no reads outside the content folders and no live non-default JSON value", "delete the field", "L6")
    for p in jfiles:
        seen = set()
        itemfile = os.path.basename(p) in ("items.json", "itemsets.json")
        text = ctx.text(p)
        for k, _ in keys_by_file[p]:
            if k.startswith("_") or k in seen or k in all_names:
                continue
            seen.add(k)
            m = re.search(r'"%s"\s*:' % re.escape(k), text)
            ln = text.count("\n", 0, m.start()) + 1 if m else 1
            if itemfile:
                r.add(loc(p, ln, k), "SIMP", "S3", "every authored key is read by the parser (CONTENT_SCHEMA)",
                      "state: key matches no content field -> ignored at parse", "remove the key or add the field", "L6")
            else:
                r.add(loc(p, ln, k), "BUG", "S2", "every authored key is read by the parser (CONTENT_SCHEMA)",
                      "state: key matches no content field -> silently ignored at parse", "remove the key or add the field", "L6")
    return r, "content fields never read outside the content folders, and JSON keys no field accepts"


SCANS = {"X1": scan_x1, "X2": scan_x2, "X3": scan_x3, "X4": scan_x4, "X5": scan_x5,
         "X6": scan_x6, "X7": scan_x7, "X8": scan_x8, "X9": scan_x9}


def main():
    C.utf8_stdout()
    ap = C.std_parser(__doc__)
    ap.add_argument("--only", help="comma list, e.g. X1,X3")
    args = ap.parse_args()
    ctx = Ctx(args.repo, args.no_git)
    outdir = C.ensure_dir(os.path.join(args.out, "scan"))
    want = args.only.split(",") if args.only else sorted(SCANS)
    total = 0
    for xid in want:
        rows, pattern = SCANS[xid](ctx)
        path = os.path.join(outdir, xid + ".tsv")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            if rows.rows:
                for d in rows.rows:
                    f.write(C.row_line(d) + "\n")
            else:
                f.write("0 rows: %s\n" % pattern)
        print("%s: %d rows" % (xid, len(rows.rows)))
        total += len(rows.rows)
    if total == 0:
        print("0 rows")
    return 0


if __name__ == "__main__":
    sys.exit(main())
