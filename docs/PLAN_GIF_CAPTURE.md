# Plan: one GIF recorder for every art subject

Rev 4, for independent review. Rev 4 answers the second review (findings 1, 3,
4, 7, 9). Rev 3 answered the first review (findings 6-9) and reorders the work as it recommended:
1. a reliable cold path and an explicit scenario manifest;
2. then a warm host, proven equal to cold;
3. then hot reload, proven equal to cold.

Every repo claim was read in the tree on 2026-10-03.

## 0. Problem

Recording motion today is one Unity boot per subject.
- `tools/screenshot.ps1 -Runtime` mirrors the project into the TestRunner copy,
  boots Unity, runs one PlayMode capture class and exits
  (`tools/screenshot.ps1:156-228`).
- It wipes the previous run's output (`:197-198`).

Each subject kind has its own hand-written class:
- `SpellRuntimeCaptureTests`;
- `AttackMotionCaptureTests` (four fixed series on three enemies, `:249-253, 278-281`);
- `EnemyStanceCaptureTests` (stills);
- about 30 per-screen classes.

Relics have no capture path. GIFs exist only for spells
(`tools/spell_feel.py:443-450`) and sliced spell sheets
(`tools/slice_spell_sheet.py:1092`).

**Goal.**
- Any subject, by id, into a GIF.
- Many subjects in one Unity boot.
- No per-subject code, and no stolen focus.
- Later, seconds per GIF from a warm Unity.

## 1. Decisions taken

1. **Cold batch first.** One Unity boot runs any number of scenarios from a
   request file. This is the path baselines and the Unity upgrade rely on.
   - The warm host (phase C) and hot reload (phase D) are speed work layered on
     top.
   - Neither ships until it reproduces the cold output (contract 2.8).
2. **An explicit scenario manifest is the unit of coverage.** A one-off CLI
   request is normalized into the same scenario shape.
   - "All passed" means all *listed* scenarios passed.
   - Every run prints the coverage table (2.9), including what is not covered.
3. **Its own project copy, `<project>-Capture`, with its own Library.** It never
   claims a TestRunner copy, so gates and captures never wait on each other. Cost:
   one full import the first time.
4. **Launches only through `Start-UnityQuiet`** (`tools/unity_path.ps1:85-107`),
   on the hidden `WinSta0\PPHeadless` desktop.
5. **Scenario code is shared, not copied.** The setup in
   `SpellRuntimeCaptureTests.StandTheFightUp` (`:210-312`) and its stop rule
   (`:466-522`) move into one scenario runner, which the existing test and the
   batch both call.
6. **One encoder:** `capture_strip.write_gif` (PIL, `tools/capture_strip.py:151`).
   ffmpeg (8.1, installed) only if encoding is measured above 1 s per GIF.

## 2. Contracts

### 2.1 Command line

```
tools/gif.ps1 <kind> <id> [-Vs <enemy>] [-Formation lone|full] [-Speed <x>]
                          [-PreStatus <type>] [-Skill <id>] [-Crop subject|stage|full]
                          [-Seconds <s>]
tools/gif.ps1 -Manifest <name>     # run a named scenario set (e.g. baseline) in one boot
tools/gif.ps1 -Compare <runA> <runB>
tools/gif.ps1 -Stop                # warm host only (phase C)
```

- Unknown ids are refused before launch, by checking them against the content
  JSON.
- The exit code is 0 only when this request's own result file says ok (2.5).
  An output file that already exists is never taken as evidence.

### 2.2 Scenario shape and kinds

A scenario is `{kind, id, vs, formation, speed, preStatus, skill, crop, seconds}`,
normalized: defaults are filled in, and keys are sorted. Its **scenario key** is
a short hash of that normalized form, so two requests that differ in any field
never share a key.

| Kind | Id source | Setup | Start | Stop rule | Default crop |
|---|---|---|---|---|---|
| `spell` | `skills.json` | `PreviewFight.ForSpell` (`Core/PreviewFight.cs:210-214`); `vs` default rat; `formation` lone or full | `ForceFirstAction` | cleared time + 0.25 s, cap 4 s (`SpellRuntimeCaptureTests.cs:469`) | stage |
| `attack` | `enemies.json` with art; `skill` default its basic attack | that enemy vs Shawn | the forced skill. The enemy's own first intent may be a buff, so it is forced as `FightBootstrap.DevForcedEnemyScript` forces one | beat player idle + 0.25 s, cap 4 s | stage |
| `relic` | `relics.json` entries with `vfx` (today only `toll_of_the_flock`, `:374-405`) | fight with `relicIds: [id]` | presentation-only beat with the relic's `vfx` (`PlaySpellVfxForTest`, `:415`) | cleared time + 0.25 s | stage |
| `status` | `StatusEffectType` | `PP_RUNTIME_PRESTATUS` path (`:295-304`) | first idle frame | `seconds`, default 2 | subject |
| `stance` | `Characters/<id>`, `Enemies/<id>` | Python, sliced stills on their shared canvas | n/a | 400 ms per still | subject |
| `sheet` | `Resources/Spells/<id>` | Python, frames at the layer's authored fps | n/a | one pass | full |
| `still` | a named PlayMode capture class plus one PNG it writes | that class, run **in the Capture copy** by the same batch launcher (its own `-testFilter`), never through `screenshot.ps1` | n/a | n/a | as the class writes |

- A relic without `vfx` is refused, with the list of relics that have one.
- **`still` runs inside the recorder's isolation.**
  - The class runs in `<project>-Capture` under the recorder's own claim.
  - Its output folder is moved into the immutable run directory (2.4) as soon as
    the process exits, before anything else can clear it.
  - Each PNG the class writes becomes one scenario output, keyed
    `still/<Class>/<file name>`. Comparisons pair by that key.
  - `screenshot.ps1` and the TestRunner copy are not involved.
- **What existing fixtures cover is stated, not assumed.**
  - `ReckoningTooltipCaptureTests` shows a full Reckoning with item offers
    (`Tests/PlayMode/Run/ReckoningTooltipCaptureTests.cs:109`).
  - `EventsM6CaptureTests` shows dialogue and legacy event pages.
  - `RelicReceiptCaptureTests` (`Tests/PlayMode/Run/`) shows the outcome-form
    Reckoning and relic receipts: full form with offers, Fell, 0 and 2 chips,
    a chip tooltip by pad, and an event page with a receipt (legacy, staged).

### 2.3 Determinism

- Fixed for every scenario:
  - `Time.captureFramerate` 60;
  - RNG seed 20260908 (`:283-289`);
  - party and backdrop.
- Each scenario loads the Fight scene fresh.
- **Gate:** the same scenario run twice gives the same frame count and under 1%
  of pixels differing per frame. Byte identity is not required: GPU output is not
  guaranteed bit-exact. Anything above that is a nondeterminism bug, not a
  tolerance to raise.

### 2.4 Output: immutable, per request

`tools/screenshots/gif/runs/<utc-timestamp>_<requestId>/<scenarioKey>/` holds:
- `anim.gif`, `strip.png`, `frames/`, `timing.json`;
- `scenario.json`: the normalized scenario plus the **identity** (2.6).

Rules:
- A run directory is written once and never overwritten.
- `tools/screenshots/gif/latest.json` maps scenario key to the last *successful*
  run directory. It is updated by write-temp-then-rename after the result is
  published.
- Old runs are pruned only by an explicit `-Prune <days>`.

### 2.5 Results and liveness

- Each request has a `requestId`.
- The result is written to `result.tmp`, then renamed to `result.json`, and it
  echoes the `requestId`. A client accepts only a result whose id matches its
  own.
- **Cold batch: two stages, two deadlines, a diagnostic for each.**
  - **Startup** runs from launch until the batch writes `ready.json`: editor
    loaded, assets imported, PlayMode entered.
    - Deadline 90 minutes when the copy has no Library (first import), else
      10 minutes. Phase A measures both, then tightens them.
    - The launcher reports the stage reached: process started, import running
      (Library growing), compile, PlayMode entered.
  - **Each scenario** has 60 s from its start marker to its result marker.
- **On any timeout** the process is killed. The report names the stage or
  scenario that timed out, the scenarios already finished, and the log tail.
- **Warm host:** it writes a heartbeat file every 2 s (request id, scenario
  index, frame).
  - A heartbeat older than 20 s, or a request past its bound, makes the client
    kill the host and retry once cold.
  - A second failure is reported with the log tail.

### 2.6 Identity and comparison eligibility

These are two different questions, kept apart.

**Operational identity** (which tree and engine made this capture; recorded in
every `scenario.json`):
- the canonical project path (resolved, case-folded);
- the editor version from that tree's `ProjectVersion.txt`;
- the git HEAD sha;
- a **content fingerprint**: the SHA-256 of a manifest listing the content hash
  of every file under `Assets/`, `Packages/` and `ProjectSettings/`.
  - Tracked, unmodified files take their git blob hash.
  - Modified and untracked files are hashed from their contents.
  - Sizes and mtimes are never used for provenance.

The full per-file manifest is stored beside the run, so two runs can be diffed
file by file.

**Warm-host refusal** (2.5) uses path and editor version. A 6.6 clone can never
use a 6.5 host.

**Comparison eligibility** (`-Compare A B`):
- Default: the two content manifests must be identical, apart from the paths
  named in an `-AllowDiff <list>`.
  - The list is a reviewed file of paths, with globs for generated trees such as
    `Assets/_Project/Resources/Content/**`.
  - Any difference outside it refuses the comparison, naming the offending
    paths.
- Project path and editor version may differ; they are printed side by side.
- For the Unity upgrade, the allow-list is the upgrade diff: `ProjectVersion.txt`,
  `Packages/manifest.json`, `Packages/packages-lock.json`, the regenerated scene
  and content trees, and any reviewed code fixes.
  - It is written once and reviewed by me before the comparison runs.
  - It is committed with the upgrade.

### 2.7 Change detection and reload (phases C and D)

**Watched roots:** `Assets/`, `Packages/`, `ProjectSettings/`.
- Detection compares paths, sizes and mtimes against the last sync with
  `robocopy /L` (a listing; copies nothing). Additions, deletions and renames
  count as changes; a rename is a delete plus an add.
- `robocopy /MIR` then applies them, deletions included.

| Changed | Host action, with the Fight scene unloaded |
|---|---|
| Nothing | none |
| Only images or audio, and their `.meta`, under `Assets/` (added, changed or deleted) | import or delete each; then **reset every static asset cache** (below) |
| `ContentData/*.json` | rebuild content with the ContentBuilder the Editor already runs; reset the caches |
| Anything else (scripts, asmdefs, scenes, materials, shaders, prefabs, `Packages/`, `ProjectSettings/`) | restart the host |

**Static caches.**
- `FrameSequenceLoader._cache` (`Core/FrameSequenceLoader.cs:34`) holds whole
  frame arrays by folder, so a reloaded scene would not see frames added or
  removed.
- Phase D starts by listing every static cache under `Assets/_Project/Scripts`
  (a grep for `static readonly Dictionary` and `static` fields holding assets).
  Each gets a reset hook that the host calls.
- A cache the audit misses is caught by the equivalence tests below, not by
  hope.

### 2.8 Equivalence: warm and hot must match cold

These gates are the condition for phases C and D shipping:
- **Warm equals cold:** the baseline manifest run warm and run cold. Same frame
  counts; pixel differences within 2.3.
- **Hot reload equals cold:**
  - Change a spell's frames A → B (B with a *different frame count*), capture
    warm.
  - Then B → A, capture warm.
  - Each capture matches a cold capture of the same state.
  - The same test is run for a `skills.json` timing edit, and for an added and a
    deleted frame file.

A failing row demotes that change type to "restart" in the table, and the
report says so.

### 2.9 Coverage report

Every `-Manifest` run ends with a table:
- each manifest dimension: spells × {rat, treant} × {lone, full} × {1x, 1.5x}
  for the baseline; attacks per enemy; relics with `vfx`; statuses; `still`
  classes;
- scenarios run, passed, failed;
- **dimensions known but not covered**, listed by name. For example: a spell
  never captured at 1.5x, an enemy with art but no attack scenario, a
  state-dependent screen with no `still` class.

## 3. Phases, each with its gate

| Phase | Lands | Gate |
|---|---|---|
| A | Scenario runner (extracted), cold batch, CLI, kinds `spell` `attack` `relic` `status`, immutable output, identity, coverage report | **Preservation before and after the extraction:** spells covering every layer render type, captured with today's `SpellRuntimeCaptureTests` *before* the extraction and again after it; same frame count, `timing.json` equal, pixels within 2.3. Existing spell capture tests green; determinism gate; `tools/focus_check.ps1 -Mode Command` clean; startup and per-scenario times **measured and reported** |
| B | Kinds `stance`, `sheet` (Python); `still`; the `baseline` manifest | Python unit tests (order, durations, crop); `-Manifest baseline` completes with its coverage table; I look at a sample from every kind |
| C | Warm host: lock, handshake, heartbeat, restart-on-any-change | Warm equals cold (2.8); 100-request soak with memory logged every 25 |
| D | Hot reload (2.7) | Hot reload equals cold (2.8) |
| E | `spell_feel.ps1` routed through the scenario runner | The routing change only: 3 spells' reports equal to the committed ones within `spell_feel`'s own tolerance |

What depends on what:
- `PLAN_UNITY_6_6.md` depends on **A and B only**.
- `PLAN_BUGS_QA.md` C11 (the Toll of the Flock GIF in a fight) needs A.
- C and D are speed work, and nothing else waits on them.

Agents: A, C and D go to an implementer; B and E to an implementer or a fixer,
by size.

## 4. Risks

- **The extraction can drift existing captures.** Phase A's preservation gate is
  the check, before anything depends on it.
- **Unity held in batchmode PlayMode for an hour or more** is unmeasured; phase C
  soaks it.
- **A static cache missed by the phase D audit:** caught by 2.8, by
  construction.
- **How the owner sees GIFs:** I send them with SendUserFile, after looking at
  `strip.png` myself. A GIF over about 15 MB is re-encoded at a lower width.
