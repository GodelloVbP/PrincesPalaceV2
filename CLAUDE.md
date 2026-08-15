# Prince's Palace — v2. THIS is the live project.

> ## Before your first edit: confirm which tree you are in
>
> | | |
> |---|---|
> | `C:\Games\Prince's Palace-v2` | **live. Work here.** |
> | `C:\Games\Prince's Palace` | v1. Abandoned. Read for history, never edit. |
>
> ```bash
> test -d Assets/_Project/Scripts/Domain/UiKit && echo "v2 - correct" || echo "STOP: this is v1"
> ```
>
> Nothing inside v1 tells you it is stale — its own `CLAUDE.md` reads as
> authoritative from the inside. A full session has already been lost to this,
> and a session has opened in v1 *after* both banners went up: see
> `docs/INCIDENTS.md`. If your working directory is v1, stop and say so rather
> than working around it.

Unity 6 (6000.5.7f1) on URP with a Renderer 2D, C#, turn-based roguelike
dungeon crawler. uGUI with **TextMeshPro** (`TMP_Text`/`Button`/`Image`).
Hades-style painterly art direction.

**No shared code with any other game in `C:\Games\`.** If you find yourself
referencing Catan, Godot, or a milestone scheme like "M137", you are in the
wrong project's context — stop and re-read this file. The one exception is v1
above: same game, earlier codebase, occasionally worth reading for a decision's
history, never worth copying from.

**Detail lives in `docs/`** — this file stays deliberately short, and anything
retrospective belongs in `docs/INCIDENTS.md` rather than here. See:
`docs/WORKFLOW.md` (session rituals, parallel-session rules, briefing and
commit conventions), `docs/CODE_STANDARDS.md` (layering, reuse-first helpers,
Unity lifecycle notes, partial-class rules), `docs/ART_PIPELINE.md` (keying
conventions, kit registry), `docs/CODE_MAP.md` (screens/systems → files),
`docs/HANDOFF_TEMPLATE.md` (design-handoff shape), `docs/INCIDENTS.md` (why the
rules here say what they say).

---

## The two rules that matter most

### 1. Scenes are generated, never hand-authored

`Assets/_Project/Scripts/Editor/SceneBuilder/SceneBuilder.cs` is the *only*
thing that writes scenes. Never edit a scene in the Unity Editor and save it —
that work is destroyed on the next build.

**But you almost never change `SceneBuilder` itself.** It knows nothing about
layout: it makes the camera, the global Volume, the canvas and the EventSystem,
then hands each screen's declared tree to `UiEmitter`. To change UI you change
the screen's tree in `Domain/UiKit/Screens/`, and its wiring in
`Editor/SceneBuilder/ScreenRegistry.cs`. That one registry drives scene
building, the build-time audits, and the screenshot tool.

A consequence worth internalising: a scene diff of thousands of lines in both
directions is normal. A full regeneration reassigns every `fileID`.

Layout is checked at BUILD time, not by eye: `UiAudit` re-solves every screen at
four canvas aspects and refuses overlaps, overflow, duplicate names and
zero-sized graphics. Every exemption states a reason (`AllowOverlap("...")` /
`AllowOverflow("...")`), so they stay greppable.

### 2. Content is generated, never hand-edited

`Assets/_Project/Scripts/Editor/ContentBuilder.cs` generates every
ScriptableObject under `Assets/_Project/Resources/Content/`. Regeneration is
**destructive** — it deletes the whole tree first. Never hand-author an asset
in there.

> Known hazard: all five `*Definition` types carry `[CreateAssetMenu]`, which
> invites authoring into the folder `ContentBuilder` wipes. The mitigation named
> in its own header comment does not exist. Recorded in `AUDIT.md`, unfixed.

---

## Verification, in brief

Tests run headless against isolated sibling copies at
`C:\Games\Prince's Palace-v2-TestRunner` and `-v2-TestRunner2`. Full decision
table: `docs/WORKFLOW.md` §8.

**While iterating** — one class in ~12s, an area or `-Changed` slice scales with
what it covers:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 wool
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Changed
```
Fuzzy class name, comma-separated list, or a named area (`combat`, `hub`,
`content`, `run`, `ui`, `art`, `rng` — defined in `tools/test_areas.ps1`).
`-Changed` maps whatever is uncommitted to the areas/classes it touches.
`-List` shows everything, including any class an area pattern has drifted out
of sync with (`run_tests_parallel.ps1` refuses to run at all while one exists —
fix the pattern, don't bypass it). No argument runs the full suite.

**Before committing** — everything, ~90-100s:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1
```
Add `-BuildContent` and/or `-BuildScenes` when content or scenes changed — that
path also syncs the generated assets back to main automatically.

> **Keep the PowerShell scripts pure ASCII.** No BOM means PowerShell 5.1 reads
> them as Windows-1252, and a UTF-8 em-dash inside a string produces a parse
> error pointing at an unrelated line. Em-dashes in `#` comments are fine.
> Enforced by `tools/githooks/pre-commit`.

Commit only test-passing checkpoints.

## Never `git add -A`

Two sessions sometimes share one working tree. `git add -A`/`git add .`/`git
add -u`/`git commit -a` have already swept another session's uncommitted work
into a commit and then deleted it. **Stage by explicit path, always.**

Enforced by `tools/githooks/deny_broad_staging.py` via `.claude/settings.json`,
which refuses those commands before they run. Full parallel-session protocol:
`docs/WORKFLOW.md` §4.

---

## Five gotchas, each found the hard way

1. **Sync rebuilt scenes back immediately.** After `SceneBuilder.BuildAllScenes`
   in the TestRunner, copy the two `.unity` files (and `Resources/Content/` if
   `ContentBuilder` ran) back to main **before** any subsequent `robocopy` from
   main → TestRunner, or stale files clobber what you just built.
   (`run_tests_parallel.ps1 -BuildContent -BuildScenes` does this for you.)

2. **New files need their `.meta` copied back too**, in the same pass. Miss it
   and the GUID silently regenerates on the next fresh `Library` rebuild,
   orphaning every asset that referenced it. Enforced at commit time by
   `tools/githooks/pre-commit`.

3. **Diff `Art/` after a scene build**, not just `Scenes/` and `Resources/`:
   `LoadSprite()` silently flips a texture's importer settings (Default →
   Sprite) and generates a fresh `.meta` for any newly-referenced asset. Both
   must sync back.

4. **Ordering needs an explicit `sortOrder` field**, sorted for in
   `ContentDatabase`. `Resources.LoadAll` returns incidental alphabetical
   order, not authoring order.

5. **Never let a test recompute a production formula to build its own expected
   value** — that makes it a tautology, and it has already masked a real
   rounding flake. Pin formula tests with literal expected values.

Unity lifecycle notes (Start()-after-SetActive timing, etc.):
`docs/CODE_STANDARDS.md` §5. The stories behind 2, 3 and 5:
`docs/INCIDENTS.md`.

---

## Read `AUDIT.md` before planning work

`AUDIT.md` is the project's living debt register — a five-reviewer audit at
commit `6fb70d0`, file:line-verified, plus everything found since (findings get
struck through with a commit sha when fixed; nothing is deleted). It is the
best available map of what is actually wrong. Recorded, not auto-fixed, on
purpose — several findings are design decisions that belong to the author.

---

## Conventions

- **State the intent before non-trivial work**: what changes, what must not
  change, and how we will know it worked. Three lines, cheap to veto. See
  `docs/WORKFLOW.md` §2.
- Commit messages explain *reasoning and tradeoffs*, not just the change — the
  git log is the real history of this project, deliberately, because chat
  context does not survive. Match that standard.
- Prefer fixing the class of bug over the instance. Where a rule can be
  mechanised, mechanise it: `UiAudit`, `UiWiringSweep`, the area-coverage
  refusal in `run_tests_parallel.ps1` and the two git hooks all exist because a
  written rule is only as strong as whoever happens to be reading it.
- Graceful degradation on missing content is the house style.

Full conventions, session rituals, and the doc-update-rules index:
`docs/WORKFLOW.md`.
