# Incidents

Why the rules in `CLAUDE.md` say what they say. Each entry is something that
actually went wrong, kept because a rule with its reason attached survives
contact with a tired reader and a bare rule does not.

This file exists so `CLAUDE.md` can stay a list of rules. That file is read in
full at the start of every session, and it had grown to the point where roughly
a fifth of it was narration about things that had already been fixed. The
narration is worth keeping; it is not worth re-reading every session. Rules
live there, reasons live here, and each rule that has a story points at it.

Nothing here is ever deleted. When a hazard gets mechanised, the entry gains a
line saying what now enforces it, and stays.

---

## A full session went into the wrong checkout (2026-08-12)

There are two Prince's Palace checkouts. Only `-v2` is live.

A complete session - diagnosis, a layout fix, a new test class, a new tool,
three repainted backdrops, four commits - went into `C:\Games\Prince's Palace`
believing it was this project. Only the PNGs were salvageable. None of the code
ported, because v1 has no UiKit; its UI is wired through `SetField` over string
field names.

What made it possible is that **nothing inside v1 says it is stale**. Its own
`CLAUDE.md` opened with the same "this project is standalone" framing as this
one and never mentioned v2, so from the inside it read as authoritative. Both
files now carry a banner naming the other tree.

The banners were a partial fix and worth being honest about: they are read
*after* the working directory has already been chosen. On 2026-08-15 a session
opened in v1 again, three days later, with the banner in place. What caught it
that time was the memory index, not the banner.

**Resolved, mechanically, later that same day.** v1 was moved out of
`C:\Games\` to `C:\Games\Backup Princes palace\Prince's Palace`, together with
its `-TestRunner` and `-TestRunner2` copies. `C:\Games\` now holds exactly one
Prince's Palace, so tab-completing the shorter name cannot land in the wrong
tree — the two checkouts being siblings under one parent was the whole
mechanism, and no amount of documentation inside either of them could address
it. This is the pattern the rest of this file keeps arriving at: a rule that
can only be read after the mistake has been made is not a control.

A related piece of debris from the same era, deleted on 2026-08-15:
`C:\Games\Prince's` was a 2.4 KB truncated Unity batchmode log — a file, not a
directory. Something redirected output to a path under
`C:\Games\Prince's Palace\` without quoting it, so the shell split the argument
at the space and wrote to a file named after the first half. It sat unnoticed
from 2026-07-27, looking like a third checkout in a directory whose whole
problem was too many similar names.

Worth generalising, because the cause is permanent and still live: **every path
in this project contains both a space and an apostrophe.** Any unquoted path in
a script or shell command silently splits, and single-quoting is not available
as the escape. The same hazard has a second face inside the repo — asset names
like `ChakraPetch-Regular SDF.asset` are why `tools/githooks/pre-commit` splits
`git diff --name-status` output on tab rather than on whitespace. Quote every
path; split on tab, never on space.

The tooling has been bitten by the same confusion: `run_tests_parallel.ps1` once
named v1's TestRunner directories, which meant a v2 test run built into and
tested v1. It now derives them from `$PSScriptRoot`. `docs/WORKFLOW.md` section
1 carried the same stale pair in its lockfile-check instruction until
2026-08-15, pointing at two directories that do not exist.

One-glance tells, if you are ever unsure:

```bash
test -d Assets/_Project/Scripts/Domain/UiKit && echo "v2 - correct" || echo "STOP: this is v1"
```

v2 has `HANDOVER.md` at the root, TextMeshPro, URP, and no `SetField`. v1 has
`SceneBuilder.FightStage.cs`, legacy `Text`, and `SetField(controller,
"fieldName", ...)` at 330 sites.

## `git add -A` deleted another session's work (once)

This repo is sometimes worked on by two sessions at once, sharing one working
tree and one pair of TestRunner copies.

One session ran `git add -A` and swept roughly 58 files of the other session's
uncommitted work into its own branch commit, then ran `git checkout main`,
deleting all of it from disk. It was recoverable only because it had landed in
the other branch's commit history first. Had that session used `git stash` or
made no commit at all, the work would simply have been gone.

Worth noting which flag did the damage: those 58 files were largely *edits to
already-tracked files*, which means `git add -u` would have swept them just as
`-A` did.

**Now enforced.** `tools/githooks/deny_broad_staging.py`, wired as a PreToolUse
hook in `.claude/settings.json`, refuses `git add -A/-u/.`, `git stage -A`, and
`git commit -a/--all` before they run. It tokenises the command rather than
regex-matching it, so `git commit -m "add -a note"` is correctly allowed.

## The project header described the wrong project

`CLAUDE.md` used to say "legacy `Text`, not TextMeshPro" and named an older
editor version. Both were true of v1 and neither was true here - v2 was
TMP-based from M4 onward, and the editor moved with the URP migration.

A header that confidently describes the wrong codebase is worse than no header,
because it is the thing a session reads first and trusts most.

## `SetField` and the class of bug that retired it

v1 wired controllers through `SetField(controller, "fieldName", value)` -
reflection over a *string*, at 330 sites. A typo there produced no compile
error and no runtime error at the call site. It produced a null field that
failed somewhere else entirely, later.

v2 assigns fields directly: controller UI references are `internal` +
`[SerializeField]`, with `InternalsVisibleTo("PrincesPalace.Editor")`.

**Now enforced twice.** `UiWiringSweep` fails the build on any serialized
reference left null, and a lint test refuses the `SetField` idiom if anyone
reintroduces it.

## Sprite importer flips that broke committed scenes

`LoadSprite()` silently changes a texture's importer settings from Default to
Sprite and generates a fresh `.meta` for any newly-referenced asset. Both have
to sync back from the TestRunner copy, or the committed scene points at a
Sprite sub-asset that does not exist in main's copy.

Two real instances: `b16ca7d` (Shawn portraits) and `52e90a8`
(`dialogue_box.png`).

This is why the rule is "diff `Art/` after a scene build", not just `Scenes/`
and `Resources/`.

## A rounding flake hidden by a tautological test

A test recomputed a production formula to build its own expected value, which
makes it a tautology - it passes whatever the formula does.

It masked a real disagreement: `Mathf.RoundToInt` uses banker's rounding
(to-even) and `Math.Round` with `MidpointRounding.AwayFromZero` does not, and
after float32 precision loss the two disagree at `.5`.

Formula tests are pinned with literal expected values now.

## PowerShell scripts and the em-dash

The scripts have no BOM, so PowerShell 5.1 reads them as Windows-1252. The
third byte of a UTF-8 em-dash decodes to a double quote, which terminates a
string literal early - and the resulting parse error points at a completely
unrelated line.

Em-dashes inside `#` comments are harmless, and `run_tests_parallel.ps1` has
seven of them today.

**Now enforced.** `tools/githooks/pre-commit` rejects non-ASCII outside a
comment in any staged `.ps1`.

## Nine stray logs and no `.gitattributes` (2026-08-15)

Found during a process review rather than by failing:

- Nine untracked `.log` files had accumulated at the repo root - batchmode
  transcripts plus ad-hoc `e1`/`m5`/`tmp-bootstrap` ones. `.gitignore` covered
  `Logs/` but not `*.log`, so this was precisely the debris a broad `git add`
  would have committed. Now ignored.
- The repo had no `.gitattributes`, so how every text file was stored and
  checked out depended on each machine's local `core.autocrlf`. Now pinned to
  `text=auto`, which matches the behaviour the repo already had - deliberately
  *not* `eol=lf`, which would flip every file to LF once and have Unity rewrite
  them to CRLF forever.

Worth recording because the first theory was wrong: the huge symmetric scene
diffs were suspected to be line-ending noise. They are not. Every file in the
working tree is CRLF, `core.autocrlf=true` was normalising correctly, and the
diffs are real fileID reassignment from `SceneBuilder` regeneration, exactly as
`CLAUDE.md` says.

## A spec number read as a centre when it was an edge (2026-08-22)

The reward track's seal pip is given by its design handoff as 15px at
`+(d/2 - 8)` on both axes. Read as the pip's CENTRE, that lands a 15px pip half
a pixel past the middle of the 15px reward mark it is meant to sit beside - on
a 26px filler disc, which is 87 of the track's 99 nodes.

The first build measured exactly that, from a runtime capture, and fixed the
symptom: it shrank the pip to 9px on filler nodes and wrote down that the
handoff's single size could not be right for both discs. The pip was also drawn
as a knockout - the check cut OUT of a dark disc so the gold underneath shows
through - and at 9px that check is a one-pixel crack. The result reads as a
smudge at four o'clock on every collected node.

The number was an EDGE. In the prototype the pip is positioned by its top-left
corner with no centring transform, unlike the halo and the pulse ring beside it,
which both carry `translate(-50%,-50%)`. `d/2 - 8` puts its left edge there and
its centre 7.5 further out, on the rim - where a 15px pip clears the mark by ten
pixels and one size does serve both discs.

Two things this cost, both of which looked like progress at the time: a measured
deviation written into the handoff record as though the design were wrong, and a
"fix" that made the thing less legible than the bug.

**What would have caught it.** Reading the prototype rather than the handoff
tables. The tables carry a position but not what it is measured from; the
running code carries both, and it is in the same folder of the same design
project. Nine other differences came out of the same reading - a flat band that
should be a gradient, a halo four times its node, a centred caption block that
should hang from its lower edge.

## Two seams set the same importer field, and the later one won (2026-08-22)

Every UI sprite in the game was importing with mipmaps off, which is the usual
advice for UI and the wrong one here: the art is authored two to ten times
larger than it is drawn, so the sampler took four texels out of every twenty-five
and dropped the rest. That is what "grainy" was, and no canvas anti-aliasing
setting could have reached it - the detail is gone during sampling, before there
is anything to smooth.

The fix went in at the two seams every sprite passes through:
`ProceduralSpriteBaker.WritePng` for the generated shapes and
`SceneBuilder.LoadSpriteByKey` for the painted art. The baker also turns mipmaps
OFF again for the fourteen sprites drawn as BARS - a 64px texture stretched to
1512x1 picks its mip level off the squashed axis, lands on a single texel, and
the gradient that was the whole point of it comes back flat.

Both seams then ran in the same build, in that order, and the second one set
`mipmapEnabled = true` on everything it loaded - including the baked sprites the
first had just turned it off for. Nothing failed. The build was green, the
scenes were correct, and the setting was simply not what the baker had asked
for.

Found by reading the `.meta` files afterwards to check the change had landed,
which is the only reason it was found at all.

**The rule, since it will come up again:** a generated asset's import settings
belong to its generator. `LoadSpriteByKey` now skips anything under
`Art/Generated/` for everything except the textureType check that stops
`LoadAssetAtPath<Sprite>` silently returning null.
