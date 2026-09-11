# Prince's Palace — v2. THIS is the live project.

> ## One Prince's Palace under `C:\Games\`, and this is it
>
> v1 was moved to `C:\Games\Backup Princes palace\Prince's Palace`. v1 is kept
> for reading history and migration only — never edit it.
>
> If you are ever in a Prince's Palace tree and unsure which:
>
> ```bash
> test -d Assets/_Project/Scripts/Domain/UiKit && echo "v2 - correct" || echo "STOP: this is v1"
> ```

Unity 6 (6000.5.7f1) on URP with a Renderer 2D, C#, turn-based roguelike
dungeon crawler. uGUI with **TextMeshPro** (`TMP_Text`/`Button`/`Image`).
Hades-style painterly art direction.

Talking Behavior:
Lead every response with the concrete next action or answer — no preamble, no "let me...", no context-setting first.
Number steps for multi-step tasks. End with one concrete next action if anything is unresolved.
Give specific time/effort estimates for tasks, not vague ones.
No recap after finishing something, no closing pleasantries ("let me know if...", "hope this helps").
State problems plainly: cause, then fix. Skip "uh oh" / soft openers.
Keep lists short (~5 items); if longer, split into must/nice-to-have or now/later.
One issue at a time — mention a second issue only after resolving the first.

Exceptions: explain fully when asked to explain/walk through something. Confirm before destructive or irreversible actions. If something hasn't worked after ~3 attempts, stop and ask a diagnostic question instead of retrying blindly. Ask one clarifying question on genuine ambiguity instead of guessing.

**No shared code with any other game in `C:\Games\`.** If you find yourself
referencing Catan, Godot, or a milestone scheme like "M137", you are in the
wrong project's context — stop and re-read this file.

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

Looking up a `Raw*Entry` field, its default, or the valid names for the enum
it parses against: `docs/CONTENT_SCHEMA.md`, generated from the types
themselves so it cannot drift.

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
`content`, `run`, `ui`, `art`, `rng`). **A test's area is the folder it sits
in** — `Tests/EditMode/<Area>/` and `Tests/PlayMode/<Area>/`, with a `Shared/`
beside them for helpers that carry no tests. There is no pattern to keep in
sync: `run_tests_parallel.ps1` refuses to run at all while any test file sits
outside an area folder or deeper than one folder inside it, while a testable
file sits in `Shared/`, while two files declare the same class name, or while
a test file declares a class discovery never saw — the fix for most of them is
a `git mv`, and `tools/test.ps1 -List -SelfCheck` proves those refusals still
fire by running them against a deliberately broken tree under
`tools/test_areas_fixture/`. `tools/test_areas.ps1`'s header says what belongs
in each area. `-Changed` maps whatever is uncommitted to the areas/classes it
touches. `-List` shows every class with its area and host. No argument runs
the full suite.

**To look at ONE thing by id** — `tools/preview.ps1 -Enemy|-Spell|-Character
<id>` validates the id against its JSON, rebuilds content if it is stale
(picking batchmode or the open Editor for you), and writes the pictures under
`tools/screenshots/preview/`; `-Launch` plays it in the Editor instead. **To
rebuild content and nothing else** — `tools/build_content.ps1`, ~14s, in place.
Neither is a gate: preview runs one capture fixture and no part of the suite,
so a green preview says the picture came out, not that anything still passes.

**Before committing** — everything, ~340s (measured 2026-09-11: 17s sync, then
PlayMode's 311s dominates the wall clock):
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1
```
**By default this does not build the scenes.** They are generated artifacts —
a rebuild with no source change still reassigns every `fileID` — so building
them on every run was tried and dropped: the per-screen EditMode tests already
exercise `UiAudit` against a freshly solved layout, which is most of what a
rebuild would have bought. What the default run skips is auditing the actual
`Scenes/*.unity` files SceneBuilder emits, and writing anything back to main.
Add `-BuildScenes` whenever a `[SerializeField]` or a screen tree under
`Domain/UiKit/Screens/` changed, and again before you **commit** such a
change: it builds the scenes, runs `UiAudit` against what was actually
emitted, and syncs them back to main in one go (`-BuildContent` likewise for
content). `-NoScenes` is no longer needed — nothing built by default means
nothing to opt out of — but the switch is kept accepted as a no-op (see
`run_tests_parallel.ps1`'s own header) so old muscle memory doesn't hard-fail.

**Keep the PowerShell scripts pure ASCII.** No BOM means PowerShell 5.1 reads
them as Windows-1252, and a UTF-8 em-dash inside a string produces a parse
error. Enforced by `tools/githooks/pre-commit`.

Commit only test-passing checkpoints.

## Never `git add -A`

**Stage by explicit path, always** — `git add -A`/`git add .`/`git add -u`/
`git commit -a` are banned; two sessions can share one working tree, and a
broad stage can sweep another session's uncommitted work into a commit.
Enforced by `tools/githooks/deny_broad_staging.py` via `.claude/settings.json`.
Full parallel-session protocol: `docs/WORKFLOW.md` §4.

---

## Five gotchas

1. **Sync rebuilt scenes back immediately.** After `SceneBuilder.BuildAllScenes`
   in the TestRunner, copy every `.unity` file (check `ScreenRegistry.All` for
   the current list) and `Resources/Content/` (if `ContentBuilder` ran) back to
   main before any subsequent `robocopy` from main → TestRunner, or stale files
   clobber what you just built. (`run_tests_parallel.ps1 -BuildContent
   -BuildScenes` does this for you.)

2. **New files need their `.meta` copied back too**, in the same pass. Miss it
   and the GUID silently regenerates on the next fresh `Library` rebuild,
   orphaning every asset that referenced it. Enforced at commit time by
   `tools/githooks/pre-commit`.

3. **Diff `Art/` after a scene build**, not just `Scenes/` and `Resources/`:
   `LoadSprite()` silently flips a texture's importer settings (Default →
   Sprite) and generates a fresh `.meta` for any newly-referenced asset. Both
   must sync back.

4. **New content types must declare ordering** — `ContentDatabase.LoadOrdered<T>`
   is constrained on `IOrderedContent`, and a lint keeps `Resources.LoadAll`
   from being called anywhere else. A type says how it is ordered or does not
   build.

5. **Never let a test recompute a production formula to build its own expected
   value** — that makes it a tautology. Pin formula tests with literal
   expected values.

Unity lifecycle notes (Start()-after-SetActive timing, etc.):
`docs/CODE_STANDARDS.md` §8. The stories behind 2, 3 and 5: `docs/INCIDENTS.md`.

---

## Read `AUDIT.md` before planning work

`AUDIT.md` is the project's living debt register — file:line-verified,
findings struck through with a commit sha when fixed, nothing deleted. Its
companion is `architecture_audit.md`: a map of the system as it actually is —
assemblies, generated artifacts, where state lives, the verification stack.
Read `architecture_audit.md` Part I before working in an area you don't know,
then check `AUDIT.md`'s open findings for known debt in that area. (`AUDIT.md`
findings #1–36 are archived at `docs/AUDIT_V1_ARCHIVE.md` — written against v1,
not this tree; the live register starts at #37.)

---

## Conventions

- **Challenge the ask, then state the intent.** Say the concern in a sentence,
  then three lines: what changes, what must not change, how we'll know it
  worked. See `docs/WORKFLOW.md` §2.
- Commit messages explain *reasoning and tradeoffs*, not just the change — the
  git log is the real history of this project.
- Prefer fixing the class of bug over the instance. Mechanise rules where
  possible: `UiAudit`, `UiWiringSweep`, the area-coverage refusal in
  `run_tests_parallel.ps1`, and the two git hooks all exist for this reason.
- Graceful degradation on missing content is the house style.
- **Build the model, not the patch.** Before extending a system, check
  whether its model still fits the request and the known next ones; if it
  does not, say so and propose a proportionate change rather than adding a
  flag, a hardcoded slot, a special case or a parallel implementation.
  Reusable behaviour is code, combinations of it are content. Validate a
  model against two materially different uses, define ownership, lifecycle
  and failure, keep one implementation per shared rule, count total cost
  (code, art, content, tests, rework), and never quietly shrink the asked-for
  experience to fit an inadequate implementation. Routine changes skip this;
  repeated special cases trigger it. Full text: `docs/CODE_STANDARDS.md` §10.
- The `ui-ugui` skill's scene/prefab-editing steps are overridden here by rule 1 above: never edit a scene or prefab directly, edit the screen's tree in `Domain/UiKit/Screens/` and its wiring in `ScreenRegistry.cs` instead.
- The `anti-ui-slop` skill is written for web/iOS coding agents with a UIZZE MCP; it is inert here without that MCP, and its "never report missing evidence" policy is not followed in this project — missing evidence gets reported, per this file's pushback rules.

Full conventions, session rituals, and the doc-update-rules index:
`docs/WORKFLOW.md`.