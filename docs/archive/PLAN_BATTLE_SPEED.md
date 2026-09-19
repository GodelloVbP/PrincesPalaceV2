# Plan: Battle speed as a preset table (in-fight only) — revision 3

Branch `prismatic-orb`. Design gate. Nothing built. Revision 1 went to the owner's critics
(six points, two corrections: answered in revision 2). Revision 2 came back approved in
direction with six targeted corrections; revision 3 makes them.

## Revision 3 — the six corrections

1. **Test isolation: pin before the first fixture, and T6 uses the real source.** `TestGlobals.ResetAll`
   is cleanup, not initialisation. The source seam's static default is `() => 1f`; production
   installs the settings-backed source in the fight scene's own entry point, `FightBootstrap.Start`
   (`Assets/_Project/Scripts/Core/FightBootstrap.cs:229-314`). Timing fixtures load the fight through
   the shared PlayMode helper (`LoadFight` in `Tests/PlayMode/Shared/`); that helper's setup re-pins
   the source to `() => 1f` and adopts it after the scene loads, before any beat, so the first
   fixture is deterministic and no fixture reads `GameSettings` for pace. `ResetAll` restores both
   the source and the adopted factor. T6 explicitly installs the production settings-backed source
   after loading, so stepping the real Options row changes what the fight reads, and restores
   both in its teardown. T8 is demoted to what it proves: that the pin and reset work.
2. **T4 tests the spell clock by casting.** The existing arithmetic comparison
   (`SpellRendererAndClockTests.cs:169-195`) is kept as a unit check of the conversion only. The
   new T4 begins a cast at each matrix pace, advances the held clock to just before and just after
   the hit cue, and observes cue delivery once. T5 triggers adoption explicitly after changing the
   source (an `AdoptPlayerSpeedForTest()` seam, or a real beat start); changing the delegate alone
   changes nothing. T6 adds an observation after resuming the interrupted beat: its impact fires at
   the time its original pace predicts, and the next beat's at the new pace.
3. **Persistence guarantee restated.** Existing stored values survive insertions unchanged;
   a removed value migrates by one documented rule: nearest row by absolute difference of the
   display number, a tie going to the slower row; a value below the first row or above the last
   snaps to that end row; a non-finite or absent value takes the default. Contract 7 and `Nearest`
   now say the same thing, and each branch has a test.
4. **Read-only table done properly.** `Rows` returns a cached `ReadOnlyCollection<Preset>`
   (`Array.AsReadOnly` held in a static readonly field), not the array. Validation checks the
   baseline, every display and every computed multiplier for finite and positive.
5. **Popup lifetime through both paths, measured from activation in game time.** `PopNumber`
   (`FightBeatPlayer.cs:668-677`) calls `Play` or `PlayMiss`; both take the scaled life. Contract 9
   reads "reclaimed at that time unless reclaimed earlier by `Flush`" (`Flush` already reclaims
   popups when a playback is superseded). T7 measures natural expiry in a single-beat playback
   nothing supersedes, from the popup's activation, on game time, so a pause does not count;
   a separate assertion checks `Flush` reclaims early.
6. **G5 produces timed playback.** Each capture series is assembled into a playable file at the
   captured frame rate (the runtime capture writes numbered frames; `ffmpeg -framerate 60` or a
   GIF at 60 fps), the preset named in the filename and shown in the frame by the fight's own log
   line. Static strips are supplementary.

Also: the sentence claiming difficulty "lands at the estimate's upper bound" is removed. An
estimate is not a fallback.

## Context

Fights read as too fast. The fight has one pacing conversion, `FightBeatPlayer.Scaled`
(`FightBeatPlayer.cs:105-106`) over the static test seam `BeatSpeedMultiplier` (`:103`), with
`Unscaled` (`:120`) its inverse. Every fight-stage duration converts through it at its call site
into a local (`FightBeatPlayer.cs:486,555,576,589,710,772,820,822,898`, `StageActorAnimator.cs:574,623,628,634,849`,
`StageDeathFade.cs:147,153`, `StageHitFlash.cs:106,109`, `StageShake.cs:113`, `StaticSwing.cs:47,62`,
`FightController.Hud.cs:1229`, `FightController.StageVisuals.cs:421-422`). Spell casts convert their
age on every tick with the current factor (`SpellPerformancePlayer.cs:196-208`) and their tails
outlive beats and rounds by design (`FightBeatPlayer.cs:305-312`), which is the hole revision 1
had and contract 3 closes. The system menu restores the time scale it found "because battle speed
is a stated Options control" (`SystemMenuController.cs:234-237`).

Owner's requirements: in-fight only; steps 0.5, 1, 1.5, 2 with today's pace labelled 1.5x;
"add 3x" is one setting; default 1x.

## Behavioural contracts (trigger → condition → outcome)

1. **Adoption at a beat's start.** Trigger: the beat loop enters a beat (`FightBeatPlayer.cs:334`).
   Outcome: `PlayerSpeedMultiplier` becomes the source's value before any `Scaled` call in the
   body; every conversion in that beat sees one product.
2. **Adoption when a fight opens.** Trigger: `FightBeatPlayer.OnEnable` (new, beside `OnDisable`
   `:325`), after `FightBootstrap.Start` installed the production source. Outcome: the opening
   glide and first badge pop run at the chosen speed.
3. **A cast keeps the pace it was born at.** Trigger: `SpellPerformancePlayer.Begin` (`:152-164`).
   Outcome: the cast records `PaceAtStart`; its `Tick` converts age as `(now - StartedAt) * PaceAtStart`;
   a later adoption in either direction leaves a live cast's age monotone and continuous, delivers
   each of its cues once, and lets it clean up at its own end. `Begin` refuses a pace ≤ 0 and
   logs it, the same posture as missing art.
4. **Changed while paused mid-beat.** Trigger: the row stepped with the menu holding
   `Time.timeScale = 0` (`SystemMenuController.cs:225-237`) and a beat between its top and its gap
   wait (`:589`). Outcome: the setting and label update at once; the interrupted beat finishes on
   the pace adopted at its top, its impact landing when that pace predicts; live casts finish on
   theirs; the next beat adopts the new pace.
5. **Changed in the hub or between rounds.** Outcome: stored; applies from the next beat or the
   next fight's `OnEnable`. The only between-beat reader, the badge pop (`Hud.cs:1229`), may lag
   one beat. Stated, not fixed.
6. **Restore defaults.** Outcome: the setting becomes "1x"; adoption per 1 and 2.
7. **Stored value on load.** Trigger: `GameSettings.Load` (`:96-104`). Outcome: absent or
   non-finite → the default; otherwise `Nearest`: the row with the smallest absolute difference in
   display number, ties to the slower row, values beyond either end to that end row. Existing
   values survive insertions unchanged; a removed value migrates by exactly this rule.
8. **Degenerate factor.** Condition: the product ≤ 0. Outcome: `Scaled` returns 0, today's
   `:105-106` behaviour on the product. Table validation makes a player factor ≤ 0 unauthorable.
9. **Popup lifetime scales.** Trigger: a beat lands a number or a miss (`PopNumber`,
   `FightBeatPlayer.cs:668-677`, both `Play` and `PlayMiss`). Outcome: the popup lives
   `Scaled(DamagePopup.LifeSeconds)` of game time from its activation and is reclaimed by the pool
   then, unless `Flush` reclaims it earlier. Today `DamagePopup.cs:17,152-177` ticks 0.85 s of plain
   `Time.deltaTime`, the only unscaled constant in the fight; idle breath and hover stay unscaled
   by design (`StageVisuals.cs:911-921,1034-1039,1064-1066`).
10. **Writers and readers.** The Options pane writes `GameSettings` only. `FightBeatPlayer`
    writes `PlayerSpeedMultiplier` at contracts 1 and 2 only. The product is read by `Scaled`,
    `Unscaled`, and the per-cast capture at `Begin`, nowhere else.

## Precedence and ownership

| concern | owner | writers | readers |
|---|---|---|---|
| rows, baseline, label number | `BattleSpeed` (Domain, engine-free) | nobody | `GameSettings`, `OptionsController` |
| the stored choice (display number) | `GameSettings.BattleSpeed` | `SetBattleSpeed` (pane, restore defaults) | the production source |
| the source seam | `FightBeatPlayer.PlayerSpeedSource` (`Func<float>`, static default `() => 1f`) | `FightBootstrap.Start` installs the settings-backed source; the shared fight-loading helper and `ResetAll` pin `() => 1f`; T6 installs production explicitly | `AdoptPlayerSpeed` |
| the adopted factor | `FightBeatPlayer.PlayerSpeedMultiplier` | `AdoptPlayerSpeed` at contracts 1 and 2 (and its `ForTest` seam) | `Scaled`, `Unscaled`, `SpellPerformancePlayer.Begin` |
| a cast's pace | `Cast.PaceAtStart` | `Begin` once | that cast's `Tick` |
| the test seam | `BeatSpeedMultiplier` (`:103`) | ~30 fixtures, `TestGlobals.cs:65` | the product |
| pause | `SystemMenuController` | the menu | Unity's `WaitForSeconds` |

```
private static float Pace => BeatSpeedMultiplier * PlayerSpeedMultiplier;
Scaled(s)   => Pace <= 0f ? 0f : s / Pace;
Unscaled(e) => e * Pace;
```

Writing `BeatSpeedMultiplier` from settings stays rejected (fixture ownership, `TestGlobals`,
`GlobalStateLintTests.cs:52-54`, and it conflates the suite's speed with the player's).

## The preset table

`Assets/_Project/Scripts/Domain/Combat/Session/BattleSpeed.cs`, engine-free beside `HitStop.cs`.

```csharp
public static class BattleSpeed
{
    public const float TodaysPaceDisplay = 1.5f;             // internal 1.0, relabelled
    public const float DefaultDisplay = 1f;                   // owner's call

    public readonly struct Preset
    {
        public readonly float Display;                         // the ONE authored number
        public float Multiplier => Display / TodaysPaceDisplay;
        public string DisplayNumber => Display.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static readonly Preset[] _rows = { new(0.5f), new(1f), new(1.5f), new(2f) };
    private static readonly ReadOnlyCollection<Preset> _readOnly = Array.AsReadOnly(_rows);
    public static IReadOnlyList<Preset> Rows => _readOnly;

    // Nearest by |display - stored|; ties to the slower row; beyond an end -> that end;
    // non-finite -> the default row.
    public static Preset Nearest(float display);
}
```

"Add 3x" is `new(3f)`: one line, no string, no case, no test edit, no saved value changed.
The row label is one templated string, `UiStrings.OptionsBattleSpeedValue = "{0}x"`, sample "1.5x".

Rule test (`Tests/EditMode/Combat/BattleSpeedTests.cs`, `[D]`): baseline finite and > 0; every
display finite and > 0; every computed multiplier finite and > 0; displays strictly ascending;
display numbers unique; the default display is a row; some row's multiplier is exactly 1;
`Rows` cannot be cast to the array (a `Preset[]` cast is null); `Nearest`: a midpoint tie goes
to the slower row, below the first → first, above the last → last, NaN → default, an exact
value → itself.

## Failure handling

- Malformed table: the `[D]` rule test fails the build.
- Stored value absent, non-finite, between rows, or beyond an end: contract 7; no throw.
- A row with no controller case: `StepperLabel`/`Step` throw (`OptionsController.cs:180-182,207-209`)
  and `OptionsPaneTests.EveryRowResolvesToASetting` (`:40-47`) walks every row.
- A row that does not fit: `OptionsScreen.Build` throws naming the column heights (`:60-69`).
- A fixture that flips the source or the factor and forgets: the shared helper re-pins on the
  next fixture's setup, `ResetAll` restores, and `GlobalStateLintTests` carries an entry for each.
- A cast begun at pace ≤ 0: refused and logged at `Begin`.

## Seams

- `FightBeatPlayer.cs:103-120`: `PlayerSpeedMultiplier`, `PlayerSpeedSource`, `Pace` (read-only
  accessor for the capture), `Scaled`/`Unscaled` over the product; `:334` `AdoptPlayerSpeed()`
  first in the body, plus `AdoptPlayerSpeedForTest()`; `:325` new `OnEnable`; a `BeatStarted`
  test seam (`Action<int beat, float pace>`), same shape as `IsPlaying` and `WireStageForTest`;
  `:668-677` `Scaled(DamagePopup.LifeSeconds)` into both `Play` and `PlayMiss`. Headers per
  `CODE_STANDARDS.md` §7 for each new public static.
- `FightBootstrap.cs:229-314`: install the settings-backed source before `fight.Bind`.
- `SpellPerformancePlayer.cs:98-105,152-164,196-208`: `Cast.PaceAtStart`, captured at `Begin`,
  used in `Tick`; `Begin` refuses pace ≤ 0.
- `DamagePopup.cs:17,152-177`: `LifeSeconds` stays the authored constant; `Play`/`PlayMiss` take
  the engine-second life; `Rise` measures from activation on game time (curves are functions of
  normalised `t`, `DamagePopupCurveTests.cs:20-60`).
- `Core/GameSettings.cs`: key, `BattleSpeed` (float display), `SetBattleSpeed` (Nearest → `Save`),
  one line each in `Load`/`Save`; no `Apply()` hook.
- `Domain/UiKit/OptionRows.cs:82-97`: group Gameplay, stepper `battlespeed`, note "Applies from
  the next action"; the cut-list comment at `:60-77` loses its line.
- `Core/OptionsController.cs:173-183,188-210,217-225`: label and clamped step cases; restore.
- `Domain/UiKit/UiStrings.cs:731-747`: `OptionsGameplay`, `OptionsBattleSpeed`,
  `OptionsBattleSpeedValue` "{0}x" (sample "1.5x"), `OptionsBattleSpeedNote`; the footer reworded
  to "Changes apply immediately. Battle speed applies from the next action."
- `Domain/UiKit/OptionsLayout.cs`: per the layout section.
- `Tests/PlayMode/Shared/` fight-loading helper setup; `TestGlobals.cs:65`; `GlobalStateLintTests.cs:52-54`.
- Wiring: none (`OptionsScreen.cs:222-247`, `ScreenRegistry.cs:984-1001`).

## Layout: two honest options, judged by a screenshot

Read: usable height 707.56; column 0 today 694 (`OptionsLayout.cs:26-27,35-36,66-67,80-96,143-161`).
A sixth row needs 78, a third card 202. The formulas honour `ColumnCount` (`:54,60-64`), giving
720 px columns at two, but the label plus control block is 940 (`:109,116`), so controls must
narrow for B.

- **A, one column, rows to 58** (`CardPadY 24→18`, `HeadingHeight 40→36`, `CardGap 36→28`,
  `RowHeight 78→58`): 704 of 707.56; every existing row shorter; smallest diff.
- **B, two columns, controls narrowed**: Audio and Display left, Gameplay right; row height
  untouched; slider track shorter.

Build A, photograph the pane at the narrowest aspect `UiAudit` solves, judge label, note,
stepper buttons and restore-button spacing by eye; if A reads cramped, build B. Neither is
"the price"; the screenshot decides.

## Tests

| id | test | asserts |
|---|---|---|
| T1 | `BattleSpeedTests` (`[D]`) | the rule list above, including read-only exposure and every `Nearest` branch |
| T2 | `GameSettingsTests` (capture/restore pattern `:26-39`) | `SetBattleSpeed(2)` → save → load → 2; stored 1.2 → 1; stored 1.25 → 1 (tie to slower); stored 0.1 → 0.5; stored 9 → 2; NaN and absent → default |
| T3 | existing `OptionsPaneTests` (`:40,49,70,88,106`), one to two lines each | binding, keying, fit, clamp at both ends, restore lands on the default |
| T4 | `ACastFiresItsCueOnceAtEveryPace` (new, PlayMode Combat), plus the kept arithmetic check at `SpellRendererAndClockTests.cs:169-195` | for each pace in `BeatSpeedMultiplier {1, 60}` × player `{1/3, 2/3, 1, 4/3}`: begin a real Water cast, hold the clock one tick before the hit cue (no cue), one tick after (cue once), at its end (cleaned up) |
| T5 | `AnOlderCastKeepsItsOwnPaceAcrossAPresetChange` (new, PlayMode Combat) | cast A begun at 1/3; source set to 4/3 and adoption triggered; cast B begun; source set back to 1/3 and adopted; under a held clock A's authored age is monotone and continuous across both adoptions, A's cues fire once at A's pace, B's once at B's, both clean up at their own ends |
| T6 | `TheBeatFinishesOnThePaceItStarted` (new, PlayMode Combat) | production source installed; a real fight; menu opened mid-beat via `SystemMenuController`, the row stepped via the real `OptionsController.Step`, menu closed; `BeatStarted` shows the interrupted beat's pace unchanged and the next beat's equal to the new row; the interrupted beat's impact fires at the time its original pace predicts, the next beat's at the new pace; source and factor restored in teardown |
| T7 | `APopupIsReclaimedWhenItsBeatSays` (new, PlayMode Combat) | a single-beat playback nothing supersedes, at player 1/3 and 4/3, for a hit and for a miss: the pool member is reclaimed at `Scaled(LifeSeconds)` ± one frame measured from activation on game time, with a pause inside the window not counting; separately, `Flush` reclaims a live popup early |
| T8 | `TheSharedHelperPinsThePace` (new, PlayMode Ui) | after the shared helper's setup the source returns 1 and the adopted factor is 1; after `ResetAll` likewise |
| T9 | source lint in `UiKitLintTests` (secondary guard only) | `PlayerSpeedMultiplier`/`PlayerSpeedSource` occur only in `FightBeatPlayer.cs`, `FightBootstrap.cs`, `SpellPerformancePlayer.cs`; the two hub multipliers occur in no fight file |

## Gates and evidence

- G1 `dotnet build tools/domain-tests` and the `[D]` slice: T1 green without Unity.
- G2 `tools/test.ps1 ui`: T2, T3, T8, `UiKitStringsTests`, `SystemMenuPaneTests` green.
- G3 `tools/test.ps1 combat`: T4–T7 green, and a review of every fixture that sets
  `BeatSpeedMultiplier` for a fixed wall-clock wait; any found is fixed. Fixture changes are
  listed in the report, not claimed absent.
- G4 `tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests` at the narrowest
  audited aspect: the pane reading "1x", judged by eye. Decides A or B.
- G5 (mandatory) playable recordings from a fixture parameterised by preset in
  `SpellRuntimeCaptureTests`' pattern: a plain melee beat and Water at 0.5x, 1x, 2x, and one
  sequence with a Water tail live across a pause-step-resume from 0.5x to 2x; each frame series
  assembled at its captured frame rate into a playable file named by preset, the preset visible
  in the frame via the fight's log line. The owner reviews them for feel. This is the evidence
  that "1x" answers "fights read as too fast".
- G6 `tools/run_tests_parallel.ps1 -BuildScenes`: scenes regenerate and commit with the change.
  Docs: `REMAINING.md` row, `PLAN_SPELL_LAYERS.md` C1 wording (the product and the per-cast
  capture), `CODE_MAP.md`.

## Sequencing

First, outside this plan: commit the finished, green `-Element` preview work (AUDIT #107,
uncommitted since plan mode froze its agent) and implement #108. Then this plan, one Opus agent,
commits per gate.

## Estimate

10 to 14 agent hours. The range covers T5 and T6, which drive real menus and real casts under a
held clock, and the layout judgement, which may mean building B after A.

## Not in scope

Hub screens keep their own multipliers (`ReckoningController.cs:178-186`,
`TalentController.Motion.cs:26-30`, reward track, hub); idle breath and hover stay unscaled; the
balance bot never reaches presentation (`Domain/Bot/FightRunner.cs:113`); `Time.timeScale` stays
the pause mechanism; no per-spell content numbers change; no keyboard shortcut. Next step if
wanted: the same setting as a HUD control in the fight, one more caller of `SetBattleSpeed`.

## Observed limitations vs assumptions

- Verified by reading: every `Scaled` call site; the per-tick age conversion at
  `SpellPerformancePlayer.cs:203` and tails surviving rounds at `FightBeatPlayer.cs:305-312`;
  `PopNumber`'s two paths; no production writer of `BeatSpeedMultiplier`; the layout constants,
  column formulas and control widths; the controller switches; the settings load/save pattern;
  the string sample rule; `DamagePopup` as the only unscaled constant in the fight.
- Assumed: the system menu can be opened and closed from a PlayMode fixture in the Fight scene
  the way `OptionsPaneTests` does in the Hub (T6); the shared fight-loading helper is the one
  entry every timing fixture uses (if some wire a fight by hand, they pin explicitly and T9's
  scope grows to name them).
- Known residual: the badge pop's one-beat lag (contract 5).
- Known cost under A: every option row shorter; under B: a shorter slider track. G4 decides.

## Deviations (implementation, one Sonnet agent, commits `8f598ee1`..`b9ffa4ad`)

Every place the delivered code reads differently from this document as
written, and why. Nothing here weakens a contract or a test; each is either
an assumption in the plan that did not hold once read against the tree, or a
call this document explicitly left to the implementer (G4's A/B fork).

1. **Contract 2's ordering claim does not hold under Unity's own lifecycle.**
   "`FightBeatPlayer.OnEnable`... after `FightBootstrap.Start` installed the
   production source" assumes `Start` can run before another object's
   `OnEnable` on the same scene load. Unity runs every object's `OnEnable`
   before ANY object's `Start`, so `FightBeatPlayer.OnEnable` cannot
   literally observe `FightBootstrap.Start`'s install on the SAME load.
   `OnEnable`'s adoption is still correct and still worth having — it
   prevents a fresh fight from opening on a STALE value the previous fight
   left in `PlayerSpeedMultiplier` (a static that outlives a scene) — but the
   "opening glide runs at the chosen speed" claim is honoured from the first
   BEAT onward (contract 1's own per-beat adoption), not from the literal
   instant `OnEnable` fires. Recorded in `FightBeatPlayer.OnEnable`'s own
   comment.

2. **No shared PlayMode fight-loading helper existed.** Revision 3 point 1
   said "the shared PlayMode helper (`LoadFight` in `Tests/PlayMode/Shared/`
   — find it)" as though one already did; none does — every one of the ~30
   existing Fight-scene PlayMode fixtures (Combat and Art) defines its own
   private, unshared `LoadFight`-shaped coroutine. Retrofitting all of them
   onto one helper is an unrelated multi-file refactor with its own review
   burden (`docs/WORKFLOW.md` §4's freeze protocol would apply to a change
   that size). `Tests/PlayMode/Shared/FightSceneFixture.cs` is new, built
   only for this plan's own new timing fixtures (T4/T5/T6/T8); the other ~30
   fixtures are untouched and still safe, because each one that needed a pin
   for its OWN timing assumptions got one directly (see item 8).

3. **T4 needed a production seam outside the plan's stated line ranges.**
   Proving "the hit cue fires once" needed an observable nothing in the
   architecture provides: `SpellPerformancePlayer.Advance`'s `HitCue` case
   was, and remains, a no-op in production (the beat player owns the impact
   block through its own independent wait). Added `HitCueCrossedForTest`, a
   public test-only hook invoked from that existing case — `Advance` sits
   outside the plan's own Seams section (`98-105,152-164,196-208`), so this
   is new territory, not a line range the plan already claimed.

4. **`OptionsController.Step` promoted `private` → `public`.** Needed so T3
   and T6 can step a row directly rather than finding and clicking its
   `Button` (PlayMode has no `InternalsVisibleTo` grant to reach it at
   `internal`). Not named in the plan's own Seams section for
   `OptionsController.cs`, which only mentions "label and clamped step cases;
   restore" — the promotion is a natural consequence of testing them for
   real rather than a design change.

5. **`DamagePopup.LifeSeconds` promoted `private` → `internal`**, not its
   value — `FightBeatPlayer.PopNumber` needs to read it to compute
   `Scaled(LifeSeconds)`; both classes are in the same assembly, so
   `internal` is the narrowest accessibility that compiles.

6. **G4: option A was built first, and rejected.** The plan's own layout
   section led with option A (one column, every row shrunk to 58px) and
   described its arithmetic as fitting (704 of 707.56). It does fit by
   `CardsFit()`'s own check — but a screenshot against a freshly built scene
   (option A's own `UiKitAuditTests`/`CardsFit` pass had only ever run
   against the SCREEN TREE in code, not the checked-in scene, which needed
   `-BuildScenes` to reflect any of this) showed the restore button and its
   reworded, longer footer note ("...Battle speed applies from the next
   action.") only 3.56px apart — the footer text rendered visibly UNDER the
   button. Option B (two columns, Gameplay alone in column 1, every OTHER
   row at its original size) ships instead; both photographs are in this
   session's scratchpad. This is the fork the plan's own G4 instruction
   named ("if A reads cramped, build B"), decided in the direction the plan
   left open.

7. **G3's fixture review scope: 7 of 31 fixtures needed a fix, not all 31.**
   Read each of the 31 PlayMode fixtures that set `BeatSpeedMultiplier`
   before deciding: a fixture using a POLLING wait (a deadline loop against
   `Time.realtimeSinceStartup`) self-corrects at any pace and needed
   nothing; several waits turned out to run through `Time.unscaledDeltaTime`
   (`ThemedButtonState`'s fades) rather than `FightBeatPlayer.Scaled` at all,
   confirmed by reading `ThemedButtonState.cs`, not assumed from the word
   "BeatSpeedMultiplier" appearing nearby. `EnemyStanceCaptureTests.cs`,
   `PreviewCaptureTests.cs`, `StageAnimationTests.cs`,
   `CinderfaultSpellCaptureTests.cs`, `StaticPilotStageCaptureTests.cs`,
   `FightBeatPacingTests.cs` and `SpellVfxTests.cs` all pin
   `FightBeatPlayer.PlayerSpeedSource = () => 1f` now, alongside their
   existing `BeatSpeedMultiplier` assignment. Full reasoning per file: commit
   `6b20a05c`.

8. **G5's three rows (0.5x/1x/2x), not all four.** The plan says "0.5x, 1x,
   2x" for the Water captures; the table's fourth row (1.5x, today's
   relabelled pace) is deliberately not a separate capture, since the
   deliverable is evidence of the NEW range this plan adds, not a fourth
   near-duplicate of the shipped pace under a new name.

9. **G5 needed a new public test seam**, `FightController.
   PushLogLineForTest` (wraps the existing internal `PushLogLine`), to
   satisfy "the preset visible in the frame via the fight's log line" —
   literally burning the preset name onto the fight's own visible bark
   banner rather than only into the Unity console log. Not in the plan's own
   Seams section, which did not anticipate this literal a reading of "the
   fight's log line."

10. **`tools/screenshot.ps1`'s own runtime copy-back does not recurse.** G5's
    fixture first wrote into a `battle_speed/` subfolder for tidiness;
    `screenshot.ps1`'s success check (`Get-ChildItem $runnerOut -Filter
    *.png`, no `-Recurse`) could not see it and reported "No runtime
    captures were produced" even though all five tests passed and wrote 500
    frames. Not a battle-speed bug — worked around by writing flat into
    `tools/screenshots/runtime/`, the convention every other `*CaptureTests`
    fixture already follows, rather than patching the tool.

11. **Found and committed separately, outside this plan, per its own
    Sequencing note:** the `-Element` preview-capture coverage
    (`PreviewCaptureTests.cs`) was sitting uncommitted in the tree before
    this session started (`AUDIT.md` #107's own test-side half). Committed
    as `d8754775` before this plan's own G3 review touched the same file, so
    the two pieces of work do not land in one commit.

12. **`SystemMenuCaptureTests.CaptureTheSkeletonOnEachTab` fails under real
    graphics, pre-existing and unrelated to battle speed** ("the pack click
    changed nothing on the character") — invisible to every gate this plan
    runs (`-nographics` skips it before reaching that assertion). Extracted
    the Options capture into its own test method, `CaptureTheOptionsPane`,
    so this unrelated failure cannot block G4's own evidence; flagged
    separately as `task_d0555f54` rather than fixed here (out of scope).
