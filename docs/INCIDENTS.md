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

## A child placed in its parent's frame, and an exemption that hid it (2026-08-22)

The talent tree's edges are three nodes: a dim limb, a lit glow, and a bright
core running down the middle of the glow. The core started as a SIBLING of the
glow and was later moved to be its CHILD, so that switching the glow off would
take the core with it. Its placement was not moved with it:

```csharp
Place.At((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f)   // the edge's absolute midpoint
.Rotated(angle)                                     // the edge's absolute angle
```

Both are correct for a sibling. As a child, a transform composes with its
parent's — so every lit edge drew its core at **twice** the midpoint, turned to
**twice** the angle: a second gold hairline somewhere else in the sky, with no
stone at either end. The spark beside it, added at the same time, was already
written as `Place.At(-length * 0.5f, 0f)` — local — and was the clue.

**It survived because the check that would have caught it was waived on
purpose.** `UiAudit`'s ChildContainment measures exactly this: a child that
escapes its parent's rect. The core declares

```csharp
.AllowOverflow("a rotated edge's axis-aligned box is wider than the line inside it")
```

which is a true statement about a rotated quad and the reason the exemption is
legitimate. It is also a blanket waiver, so it silenced a 900px escape as
readily as the 4px one it was written for.

Found by looking at a runtime screenshot, not by a test — the built scene said
nothing was wrong, and every test agreed. What it looks like on a still frame is
loose diagonal lines in empty sky, which reads as a stray art asset rather than
as a transform bug, and cost a detour through the backdrop layers first.

Two things came out of it. `ConstellationScreenTests.EveryEdgesLitCoreSitsOnThe\
EdgeItLights` asserts against the built tree that a `*Core` under a `*Glow` has
no offset and no rotation of its own — the specific claim, in the one place the
generic check cannot make it. And the general lesson, which is the reason this
is written down: **an `AllowOverflow`/`AllowOverlap` reason describes why SOME
overflow is expected, never how much.** Where a node is exempt from the generic
check, the specific property it still has to satisfy needs its own test, or that
node has no layout coverage at all.

## A rule written, commented, and called by nothing (2026-08-22)

`ContentDatabase.MinSpentMet` enforces the talent tree's two point-gates — 9
spent on a path before its convergence opens, 20 before its capstone. It carries
this comment:

> One function rather than four checks the caller composes, because there are
> now four of them and they have to agree between the talent screen's colouring
> pass, its click handler and its tests.

It had **no callers**. `TalentPage` was written later to own the "can this be
kindled" question, took prerequisites and cost across, and did not take the gate.
The convergence and the capstone are also the only two orbs priced at zero
embers, so their gate was their entire price — and it was not being asked.

It went unnoticed for the same reason it was safe to turn back on: a merge
requires all three tiers beneath it, which is ten orbs by the time the 9-gate
applies, so the parent chain very nearly pays the gate on its own.
`TalentGateTests.TheAuthoredGatesArePaidByTheirOwnAncestry` pins that
near-redundancy, and will fail the day a skeleton change makes the gate bite for
the first time — which is a balance decision, and should arrive as a failing
test rather than as a quiet change in what a player can buy.

The transferable part is the comment. It described four agreeing callers with
enough confidence that nobody checked, and `git log -S` would have shown they
never existed. **A comment that asserts who calls something is a claim about the
world, and it decays silently.** `grep` for the symbol is cheap; the comment is
not evidence.

---

## Irreplaceable art used as a test fixture for the guard meant to protect it (2026-08-23)

`tools/slice_spell_sheet.py` grew a `--new <id> --sheet <file>` flag that cuts a
sheet nobody has cut yet and prints the manifest and `skills.json` blocks to
paste. It guarded against scaffolding over an id already in the `VFX` manifest.

`golem_boulder` is not in `VFX`. It is in `HAND_ASSEMBLED`, the register
declared immediately below it, whose only purpose is naming sequences the tool
cannot reproduce: `f2==f3` is a held peak, `f4`/`f5` are a stepped alpha fade,
`f0` is a deliberately blank wind-up, and the source sheet does not even share a
grid with the result.

The new flag was then pointed at `golem_boulder` **to check whether the guard
worked**, and cut six cells of an unrelated sheet over all six frames.

`git checkout` restored them exactly and nothing damaged was ever committed,
which is luck about timing rather than anything the design earned.

### The mistake is not the missing branch

The obvious reading is "the guard checked one register and there were two", and
the obvious fix is the second check. Both are true and both are now done —
three guards: the manifest, the register, and a flat refusal to write into any
directory that already holds frames, which is the one that would have caught
this without knowing why.

But the register had been read earlier in the same session. What
`golem_boulder` was, was known. The actual error was **choosing live,
irreplaceable data as the fixture for an unverified destructive tool**, when a
copied sheet under a throwaway id would have proved exactly as much. A guard's
first execution is the least trustworthy moment it will ever have, and that is
precisely when it was aimed at something that could not be regenerated.

### What enforces it now

`Assets/_Project/Art/Sheets/hand_assembled.json` is the register, and both sides
read it: the slicer refuses to write into anything named there, and
`HandAssembledArtTests` pins every listed sequence by SHA-256 so a clobber fails
the suite **whatever caused it** — a different tool, a bad merge, a stray copy.
Verified by clobbering a frame on purpose and watching it fail with the file
named and the recovery command in the message.

The register is a JSON file under `Assets/` rather than a dict inside the Python
tool, and that placement is the point rather than tidiness: the headless
TestRunner mirrors `Assets` and nothing else, so a register living beside the
tool is unreadable from the suite — which would leave the single list of what is
irreplaceable legible only to the tool most likely to overwrite it.

A pin can be updated. Art is meant to change; it is meant to cost one deliberate
edit in the same commit as the art, rather than happening while nobody chose it.

## Two sessions on `static-pilot`, same working tree, same day (2026-09-04)

The rig-removal and static-art work landed as two sessions committing to the
same branch and the same working tree at once, and none of `docs/WORKFLOW.md`
§4's rules held for either of them.

`d761b0d` (the treant's six-still delivery, from the other session) landed
between two of this session's own commits, `a9c0d49` and `5e7d24e`. Both
sessions were rewriting `tools/slice_actor_sheet.py` the same afternoon --
from a multi-sheet frame slicer to a single-sheet key-pose slicer -- and the
version that shipped in `d761b0d` already contains that rewrite: `5e7d24e`'s
own commit message describes making the same cut, but its diff never touches
the file, because there was nothing left in it to change. This session's
in-progress rework was overwritten on disk before it was ever committed, with
no merge and no conflict to notice it by. WORKFLOW.md §4's "one dedicated
branch per session" is the rule that was skipped; two branches would have
turned this into a reviewable merge instead of a silent loss.

Two `run_tests_parallel.ps1` gate runs came back unusable mid-session,
clobbered by a concurrent Unity batchmode run from the other session.
WORKFLOW.md §4's "check the TestRunner lock before test runs" is the rule
that would have caught it before the run started rather than after it
finished.

An untracked file, `Art/Rigs/rat/full_body_for_skinning.png`, went with
`Art/Rigs/` when `a9c0d49` deleted the directory. It was never committed, so
the deletion left nothing in history to recover it from -- unrecoverable, not
merely inconvenient. WORKFLOW.md §4's "nothing uncommitted at session end" is
the rule that would have kept a git-recoverable copy of it past the moment
its own directory was removed.

None of these is a new failure mode. They are three rules `docs/WORKFLOW.md`
§4 already states, not followed by both sessions on the same day -- worth
recording because the two-sessions-one-tree hazard has cost real work before
(see "`git add -A` deleted another session's work (once)", above) and will
again the next time a session assumes it has the tree to itself without
checking.

## The ring at canvas centre (2026-09-07)

The party stage's foot-shadow ring sat at canvas centre under every figure
although the compensation code that was supposed to move it existed and ran
without error. Cause, in `FightController.StageVisuals.cs`: stance PNGs
import with Mesh Type Tight, so `sprite.textureRect` is a trimmed, OFFSET
sub-window of the authored canvas rather than the whole thing starting at
(0,0). The old measurement called `sprite.texture.GetPixels(textureRect.x,
textureRect.y, ...)`, got back pixel coordinates that were already relative
to the crop, and then divided by the crop's own width -- dropping the crop's
offset entirely. For any tight crop that division is ~0 by construction:
whatever the true silhouette centre was, the arithmetic could only ever
report "centred," and every stance on every actor looked plausible because
every ring rendered at the same wrong place.

It survived because nothing compared the ring's rendered position against a
literal expectation -- the code ran, produced a number, and the number never
looked obviously broken; a ring at canvas centre is a valid position for a
ring to be at, just not the right one for this pose. Found by re-deriving
the transform by hand against a raw `GetPixels` dump of the real imported
asset, not by a test catching a mismatch, because no test asserted where the
ring should land.

The rule: **a `GetPixels` coordinate is in crop space, and has to be mapped
back into canvas space (`+ textureRect.x` / `.y`) before it is compared
against or divided by anything measured in canvas units (`sprite.rect.width`,
never `textureRect.width`).** `FootBandCentreFraction` does this now, and
also narrowed its scan from the whole silhouette to the 24-row foot band
above the authored ground line, so an outstretched staff or wing cannot drag
the ring sideways. Pinned by a PlayMode assertion on every slot plus a
literal guard so "expected" and "actual" cannot both be computed the same
wrong way again. `ContentTop` (`FightController.StageVisuals.cs`) has the
identical crop-offset bug and was deliberately left for its own pass --
tracked in `AUDIT.md`.

## A readonly struct that serialises as its default (2026-09-07)

`Reach` (`Domain/Combat/Reach.cs`) was sketched as a `readonly struct` in the
positions plan. Written that way, it would have shipped a bug invisible to
the whole EditMode suite: Unity's serializer skips `readonly` fields
outright, and `Reach` lives inside `ResolvedSkill`, which is stored on a
`SkillDefinition` `ScriptableObject`. A readonly `Reach` round-trips through
that generated content tree as `default(Reach)`, which reads as
`Reach.Any` -- every melee restriction and every authored rank restriction
(`reachSlots` in `skills.json`) would silently disappear at runtime, and
every skill would become legal to aim anywhere. EditMode tests would not
catch it: they build a `Reach` in memory and assert against it directly,
never going through `ContentBuilder`'s asset-generation step, so the readonly
version and the working version return identical answers to every test that
exists.

The rule, already stated once for `ResolvedSkill` and now restated here
because it nearly got relearned the hard way: **a Domain record that gets
stored inside a `ScriptableObject` is mutable by construction and immutable
only by convention** -- `readonly` is not available as a real guarantee for
anything Unity has to serialize, so the discipline has to be "nothing after
the resolver writes it," enforced by review and by the type only ever being
constructed once per resolve, not by the compiler. `Reach` shipped as a
plain mutable `struct` with that comment inline, the same bargain
`ResolvedSkill.cs:25` and `SpellPresentation.cs:29` already document for
themselves.
