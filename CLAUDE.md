# Prince's Palace — v2. THIS is the live project.

> ## Before your first edit: confirm which tree you are in
>
> There are two Prince's Palace checkouts and **only this one is live**:
>
> | | |
> |---|---|
> | `C:\Games\Prince's Palace-v2` | **live. Work here.** |
> | `C:\Games\Prince's Palace` | v1. Abandoned. Do not change it. |
>
> ```bash
> test -d Assets/_Project/Scripts/Domain/UiKit && echo "v2 - correct" || echo "STOP: this is v1"
> ```
>
> Other one-glance tells: v2 has `HANDOVER.md` at the root, TextMeshPro, URP,
> and no `SetField`. v1 has `SceneBuilder.FightStage.cs`, legacy `Text`, and a
> `SetField(controller, "fieldName", ...)` idiom at 330 sites.
>
> **Nothing inside v1 tells you it is stale.** Its own `CLAUDE.md` opens with the
> same "this project is standalone" line as the paragraph below and never mentions
> v2, so it reads as authoritative from the inside. On 2026-08-12 a full session —
> diagnosis, layout fix, a new test class, a new tool, three repainted backdrops,
> four commits — went into v1 believing it was this project. Only the PNGs were
> salvageable; none of the code ports, because v1 has no UiKit. The tooling has
> been bitten too: `run_tests_parallel.ps1` once named v1's TestRunner
> directories, so a v2 test run built into and tested v1.
>
> If you are reading this file from a session whose working directory is v1, you
> are in the wrong tree. Stop and say so rather than working around it.

Unity 6 (6000.5.7f1) on URP with a Renderer 2D, C#, turn-based roguelike
dungeon crawler. uGUI with **TextMeshPro** (`TMP_Text`/`Button`/`Image`).
Hades-style painterly art direction.

> This header used to say "legacy `Text`, not TextMeshPro" and named v1's
> editor version. Both were true of v1 and neither is true here — v2 was
> TMP-based from M4 onward, and the editor moved with the URP migration.

**No shared code with any other game in `C:\Games\`.** If you find yourself
referencing Catan, Godot, or a milestone numbering scheme like "M137", you are in
the wrong project's context — stop and re-read this file. The one exception is
v1 above: same game, earlier codebase, occasionally worth reading for a
decision's history, never worth copying from.

**Detail lives in `docs/`** — this file stays deliberately short. See:
`docs/WORKFLOW.md` (session rituals, parallel-session rules, test-run/commit
conventions), `docs/CODE_STANDARDS.md` (layering, reuse-first helpers, Unity
lifecycle notes, partial-class rules), `docs/ART_PIPELINE.md` (keying
conventions, kit registry), `docs/CODE_MAP.md` (screens/systems → files),
`docs/HANDOFF_TEMPLATE.md` (design-handoff shape).

---

## The two rules that matter most

### 1. Scenes are generated, never hand-authored

`Assets/_Project/Scripts/Editor/SceneBuilder/SceneBuilder.cs` is the *only* thing
that writes scenes. Never edit a scene in the Unity Editor and save it — that
work is destroyed on the next build.

**But you almost never change `SceneBuilder` itself.** In v2 it knows nothing
about layout: it makes the camera, the global Volume, the canvas and the
EventSystem, then hands each screen's declared tree to `UiEmitter`. To change UI
you change the screen's tree in `Domain/UiKit/Screens/`, and its wiring in
`Editor/SceneBuilder/ScreenRegistry.cs`. Screens are listed in `ScreenRegistry`
— that one list drives scene building, the build-time audits, and the
screenshot tool.

A consequence worth internalising: a scene diff of thousands of lines in both
directions is normal. A full regeneration reassigns every `fileID`.

Layout is checked at BUILD time, not by eye: `UiAudit` re-solves every screen at
four canvas aspects and refuses overlaps, overflow, duplicate names and
zero-sized graphics. Every exemption has to state a reason
(`AllowOverlap("...")` / `AllowOverflow("...")`), so they stay greppable.

### 2. Content is generated, never hand-edited

`Assets/_Project/Scripts/Editor/ContentBuilder.cs` generates every ScriptableObject
under `Assets/_Project/Resources/Content/`. Regeneration is **destructive** — it
deletes the whole tree first. Never hand-author an asset in there.

> Known hazard (`AUDIT.md`, "Also worth knowing"): all five `*Definition` types
> carry `[CreateAssetMenu]`, which actively invites authoring into the folder
> `ContentBuilder` wipes. The "hand-authored assets belong in a folder this tool
> does not own" mitigation named in its own header comment does not actually exist.

---

## Verification, in brief

Tests run headless against isolated sibling copies at
`C:\Games\Prince's Palace-v2-TestRunner` and `-v2-TestRunner2`. The scripts
derive those paths from `$PSScriptRoot`, not from a constant — an earlier copy
of them still named v1's directories, which meant a v2 test run would have
built into and tested v1. Full decision table: `docs/WORKFLOW.md` §7.

**While iterating** — one class in ~12s, an area or `-Changed` slice scales
with how much it covers:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 wool
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Changed
```
Fuzzy class name, comma-separated list, or a named area (`combat`, `hub`,
`content`, `run`, `ui`, `art`, `rng` — defined in `tools/test_areas.ps1`).
`-Changed` maps whatever is uncommitted to the areas/classes it touches and
runs just that. `-List` shows everything, including any class an area
pattern has drifted out of sync with (`run_tests_parallel.ps1` refuses to
run at all while one exists — fix the pattern, don't bypass it). No argument
runs the full suite.

**Before committing** — everything, ~90-100s:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1
```
Add `-BuildContent` and/or `-BuildScenes` when content or scenes changed —
that path also syncs the generated assets back to main automatically.

> **Keep the PowerShell scripts pure ASCII.** They have no BOM, so PowerShell
> 5.1 reads them as Windows-1252 — and the third byte of a UTF-8 em-dash
> decodes to `"`, which terminates a string literal early and produces a parse
> error pointing at a completely unrelated line. Em-dashes inside `#` comments
> are harmless; one inside a string is not.

Commit only test-passing checkpoints.

## Never `git add -A`

This repo is sometimes worked on by two sessions at once, sharing one working
tree and one pair of TestRunner copies. `git add -A`/`git add .`/`git commit -a`
have already swept another session's uncommitted work into a commit and then
deleted it via `git checkout`, once. **Stage by explicit path, always.** Full
parallel-session protocol: `docs/WORKFLOW.md` §3.

---

## Five gotchas, each found the hard way

1. **Sync rebuilt scenes back immediately.** After `SceneBuilder.BuildAllScenes`
   in the TestRunner, copy the two `.unity` files (and `Resources/Content/` if
   `ContentBuilder` ran) back to main **before** any subsequent `robocopy` from
   main → TestRunner. Otherwise stale files clobber what you just built.
   (`run_tests_parallel.ps1 -BuildContent -BuildScenes` does this for you.)

2. **New files need their `.meta` copied back too**, in the same pass, before
   committing. Miss it and the GUID silently regenerates on the next fresh
   `Library` rebuild, orphaning every asset that referenced it.

3. **Diff `Art/` after a scene build**, not just `Scenes/` and `Resources/`:
   `LoadSprite()` silently flips a texture's importer settings (Default → Sprite)
   and generates a fresh `.meta` for any newly-referenced asset. Both must sync
   back, or the committed scene points at a Sprite sub-asset that does not exist
   in main's copy. Two real instances: `b16ca7d` (Shawn portraits), `52e90a8`
   (`dialogue_box.png`).

4. **Ordering needs an explicit `sortOrder` field**, sorted for in
   `ContentDatabase`. `Resources.LoadAll` returns incidental alphabetical order,
   not authoring order.

5. **Never let a test recompute a production formula to build its own expected
   value** — that makes it a tautology. `Mathf.RoundToInt` (banker's/ToEven) and
   `Math.Round`/`MidpointRounding.AwayFromZero` disagree at `.5` after float32
   precision loss, a real flake this masked once. Pin formula tests with literal
   expected values instead.

Unity lifecycle notes (Start()-after-SetActive timing, etc.):
`docs/CODE_STANDARDS.md` §5.

> **`SetField` is gone.** v1 wired controllers through
> `SetField(controller, "fieldName", value)` — reflection over a *string*, at
> 330 sites, where a typo produced no compile error and no runtime error, just a
> null field that failed somewhere else later. v2 assigns fields directly
> (controller UI refs are `internal` + `[SerializeField]`, with
> `InternalsVisibleTo("PrincesPalace.Editor")`), and `UiWiringSweep` fails the
> build on any serialized reference left null. A lint test refuses the old
> idiom if anyone reintroduces it.

---

## Read `AUDIT.md` before planning work

`AUDIT.md` is the project's living debt register — a five-reviewer audit at
commit `6fb70d0`, file:line-verified, plus everything found since (findings
get struck through with a commit sha when fixed; nothing is deleted). It is
the best available map of what is actually wrong. Recorded, not
auto-fixed, on purpose — several findings are design decisions that belong
to the author, not something to silently resolve mid-unrelated-task.

---

## Conventions

- Commit messages explain *reasoning and tradeoffs*, not just the change — the
  git log is the real history of this project, deliberately, because chat context
  does not survive. Match that standard.
- Prefer fixing the class of bug over the instance.
- Graceful degradation on missing content is the house style.

Full conventions, session rituals, and the doc-update-rules index:
`docs/WORKFLOW.md`.
