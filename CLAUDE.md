# Prince's Palace — v2. THIS is the live project.

> ## One Prince's Palace under `C:\Games\`, and this is it
>
> v1 was moved out on 2026-08-15, to
> `C:\Games\Backup Princes palace\Prince's Palace` along with its two
> TestRunner copies. Tab-completing `Prince` under `C:\Games\` can now only
> reach this tree. That move is the actual fix; the warnings that used to fill
> this box were compensating for the two trees being siblings, and could only
> ever be read *after* the wrong one had already been opened.
>
> If you are ever in a Prince's Palace tree and unsure which:
>
> ```bash
> test -d Assets/_Project/Scripts/Domain/UiKit && echo "v2 - correct" || echo "STOP: this is v1"
> ```
>
> v1 is kept for reading history and migration only — never edit it. Nothing
> inside it says it is stale, which cost a full session once and nearly cost
> another even after both trees carried warnings. See `docs/INCIDENTS.md`.

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

> This box used to record a hazard that **does not exist in this tree**: "all
> five `*Definition` types carry `[CreateAssetMenu]`, inviting authoring into
> the folder `ContentBuilder` wipes." There are **nine** `*Definition` types and
> **none of them carries the attribute** — verified 2026-08-20 across all nine
> files and against `git log -S`, which shows the string entering at this
> repository's first commit and never moving, consistent with it only ever
> appearing in comments here. It was a v1 condition, carried across with the
> rest of this file.
>
> Kept rather than deleted because two production comments still defend against
> it (`ContentDatabase.Validation.cs`, `CharacterEntryResolver.cs`), and a
> reader who meets those needs to know what they are guarding. The underlying
> rule is unchanged and does not depend on the attribute: **never hand-author an
> asset under `Resources/Content/`.** Regeneration deletes the tree.

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

**Before committing** — everything, ~120-130s:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1
```
**This builds the scenes.** They are generated artifacts, so a run against
whatever is on disk tests whatever the source looked like the last time somebody
remembered a flag — and it skips `UiAudit`, which is the only check that sees
overlap, overflow and duplicate names in what was actually emitted. `-NoScenes`
opts out and saves ~25s.

It does **not** write them back to main, because a rebuild with no source change
still reassigns every `fileID`: 165,849 lines out and the same number back,
meaning nothing. Add `-BuildScenes` when you intend to **commit** the scenes —
that is what syncs them (and `-BuildContent` likewise for content).

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
   in the TestRunner, copy **every** `.unity` file (and `Resources/Content/` if
   `ContentBuilder` ran) back to main **before** any subsequent `robocopy` from
   main → TestRunner, or stale files clobber what you just built.
   (`run_tests_parallel.ps1 -BuildContent -BuildScenes` does this for you.)
   This said "the two `.unity` files" until 2026-08-20; there are five, and the
   count is not worth writing down again — `ScreenRegistry.All`'s distinct
   `ScenePath`s are the only list of them.

2. **New files need their `.meta` copied back too**, in the same pass. Miss it
   and the GUID silently regenerates on the next fresh `Library` rebuild,
   orphaning every asset that referenced it. Enforced at commit time by
   `tools/githooks/pre-commit`.

3. **Diff `Art/` after a scene build**, not just `Scenes/` and `Resources/`:
   `LoadSprite()` silently flips a texture's importer settings (Default →
   Sprite) and generates a fresh `.meta` for any newly-referenced asset. Both
   must sync back.

4. **Ordering is now a compile error to forget**, so this gotcha is mostly
   history: `ContentDatabase.LoadOrdered<T>` is constrained on
   `IOrderedContent`, and a lint keeps `Resources.LoadAll` from being called
   anywhere else. A new content type says how it is ordered or does not build.
   The hazard it replaced is still worth knowing, because it is what the type
   is for: `Resources.LoadAll` returns incidental alphabetical order, and the
   resulting list is plausible but wrong rather than obviously broken.

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

Its companion is `architecture_audit.md` (2026-08-20, at `b407dbd`): a map of
the system as it actually is — assemblies, the four generated artifacts, where
state lives, the five-tier verification stack — plus the rules for adding to it,
thirteen verified findings, and cost-to-extend tables. `AUDIT.md` is what is
already broken; that is the architecture itself and what to avoid breaking next.
Read its Part I before working in an area you do not know.

---

## Conventions

- **Challenge the ask, then state the intent.** First: does this actually help?
  Say the concern in a sentence and keep building — the rule fires the moment
  you notice something is wrong and are about to conclude it is fine. Then
  three lines, cheap to veto: what changes, what must not change, and how we
  will know it worked. See `docs/WORKFLOW.md` §2.
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
