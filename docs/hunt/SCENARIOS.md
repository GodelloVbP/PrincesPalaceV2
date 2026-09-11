# Stage 3b: runtime lifecycle scenarios

The scenario list the plan requires to exist BEFORE any lifecycle finder runs,
so that the hunt's coverage of runtime behaviour is decided in advance instead
of being whatever the finders happened to think of.

**Every row here is an obligation whose required evidence kind is `asserted`.**
A scenario is discharged by a PlayMode test that was seen to fail when the
behaviour was broken -- not by reading the coroutine, and not by a capture. A
capture is evidence for a VISUAL outcome only. A scenario that cannot be built
today is marked `not buildable` with the reason, and it stays in the record as
`unresolved`; it is not quietly dropped.

Row ids are stable. Cite them from the manifest's `evidence` cells
(`asserted: A2 / StageAnimationTests.Whatever`).

## The inventory these scenarios cover

`tools/hunt_manifest.py` flags 59 Core files `lifecycle`. Six of those match
only inside COMMENT text (`CanvasCapture.cs`, `ContentDatabase.Effective.cs`,
`EscapeKey.cs`, `RunEncounter.cs`, `SheetPanel.cs`, `Show.cs`) -- the flag is a
lexical over-approximation and is left that way deliberately, because a flag
that reads comments is cheap and a flag that parses C# is not. That leaves
**53 files with a real Unity message or coroutine**: 47 declaring at least one
Unity message, and 6 partials or helpers that only own coroutines
(`FightController.Hud.cs`, `FightController.StageVisuals.cs`,
`MapController.Walk.cs`, `PartyToast.cs`, `RewardTrackController.Motion.cs`,
`StaticSwing.cs`). The plan's inventory says 49 controllers; the difference is
partials counted separately here. Every one of the 53 appears in the coverage
table at the end, with a scenario or with a stated reason for having none.

## What exists to drive them

The cost every scenario below pays, stated once:

- `Tests/PlayMode/Shared/FightSceneFixture.LoadFight()` is the ONLY shared
  scene-load helper, and it loads the Fight scene only. Its own header records
  that roughly 30 Fight fixtures each still define a private `LoadFight`-shaped
  coroutine, and that consolidating them was deliberately not done. **There is
  no shared loader for Hub, Map, Reckoning, MainMenu or Talents** -- every
  fixture for those rolls its own, so a scenario on a non-Fight screen starts
  by copying one (`MapFlowTests`, `HubDescentTransitionTests`,
  `ReckoningPhaseTests`, `TalentInvestmentTests`, `SaveSlotFlowTests` are the
  ones worth copying from).
- `Tests/PlayMode/Shared/TestGlobals.ResetAll()` resets 18 statics. Call it in
  `[TearDown]`; `GlobalStateLintTests` enforces the pairing for a global a test
  WRITES, but not for one production writes during a test (see E6).
- `Tests/PlayMode/Shared/StageCaptureRig` and `HudCaptureRig` for pictures,
  `PlayModeSparkFixture` for a castable skill, `SquadFixture.FirstLiveMember()`
  for the roster.
- `UseAThrowawaySaveRoot` / `Restore` is the `[SetUp]`/`[TearDown]` pair every
  save-touching fixture already writes; copy it verbatim rather than pointing
  a scenario at the player's real `persistentDataPath`.

## Family A: interrupt

A second beat, press or navigation arriving while a coroutine or a hold is
already live.

| id | controller | trigger | expected state after | cleanup expected | how to drive | already asserted | buildable |
|---|---|---|---|---|---|---|---|
| A1 | `FightBeatPlayer` | a second beat pushed while `PlayBeats` is mid-flight | the first beat's actors end on their marks; exactly one `PlayBeats` handle live (the class holds one, 1 start / 1 stop) | no in-flight popup left; `Flush` reclaims | `FightSceneFixture.LoadFight()`, then two attacks without awaiting the first, as `FightFlowTests.AnAttackReachesTheSessionAndMovesTheEnemy` drives one | no -- `FightFlowTests.FlushReclaimsEveryInFlightPopup` asserts the reclaim, not the double beat | yes |
| A2 | `StageActorAnimator` | `Play()` called again while the first tween is mid-flight | no home drift: `_home` untouched, `_travel`/`_stretch` zeroed before the new coroutine, every tween ending on `SetTravel(Vector2.zero)` | the 7 coroutine handles left with at most one live per actor | `StageAnimationTests`'s existing no-scene rig (`FightBeatPlayerFixtureTests` shows the minimal shape) | no -- cleared by STATIC READ only (ledger G4); `ReHomingAFigureMidBreathDoesNotFoldTheBreathIntoItsSize` is the nearest and does not interrupt | yes |
| A3 | `StageActorAnimator` | a `Play()` landing on a flyer mid-`Gliding` | the glide's hover composition survives (`_home + _travel + hover`), the lunge still ends exactly on the mark | one `Gliding` handle | `StageAnimationTests.AFlyerRidesItsHoverThroughALungeAndBack` extended to interrupt | partly -- that test plays one lunge, uninterrupted | yes |
| A4 | `StageHitFlash` | a second hit while `Run` is fading the first | the flash restarts at full and still wears off; no two `Run` coroutines writing one colour | 2 stops for 2 starts | `StageAnimationTests.TakingAHitFlashesTheTarget` + `TheFlashWearsOff` | no | yes |
| A5 | `StageShake` | a second kick while `Kicking` is running | one shake, ending at rest, not double amplitude | rack returns to its rest position exactly | `StageAnimationTests.BothStageRacksCanBeShaken` | no | yes |
| A6 | `StageDeathFade` | a revive (second life) arriving mid-`FadeRoutine` | the figure ends fully opaque, not stuck at the interrupted alpha | 3 stops guard 1 start | `StageAnimationTests.AFreshFightUnfadesWhoeverDiedInTheLastOne` extended to interrupt mid-fade | no -- and this is the live path for a second life, so it is worth more than its size suggests | yes |
| A7 | `DamagePopup` | the pool re-issues a popup whose `Rise` is still running | the reissued popup starts from full alpha and full punch, not from the interrupted curve | `Rise` stopped before restart (2 stops, 1 start) | `FightSceneFixture.LoadFight()` + `FightFlowTests`'s popup path | partly -- `BattleSpeedPopupLifetimeTests.FlushReclaimsALivePopupEarly` reclaims, it does not re-issue | yes |
| A8 | `PartyToast` | a second toast while `HoldThenFade` is holding | the message swaps and the timer restarts from the top | one handle | `SystemMenuPartyTests.TheToastFadesOutOverTime` extended to a second refusal | no | yes |
| A9 | `SpellPerformancePlayer` | a second cast on the same slot while one is live | the second cast gets its own member; the first is not superseded silently | `CancelAll` leaves no schedule cursor behind | `SpellRendererAndClockTests` | partly -- `ASecondFaultGetsItsOwnMemberRatherThanSupersedingTheFirst` and `ACinderfaultOverALiveTailSharesNoRendererWithIt` cover the renderer half, not the schedule half | yes |
| A10 | `SpellVfxPlayer` | `Play` while a previous effect is still drawing | the previous effect is cleared, not layered | `StopImmediately` leaves no frame behind | `SpellVfxTests` | partly -- `StoppingImmediatelyClearsAnEffectInFlight` stops explicitly rather than interrupting | yes |
| A11 | `HoldToConfirm` | `Begin()` called a second time while `_held` | the `if (_held) return` guard holds: `_elapsed` is NOT reset, so a mashed key cannot extend a hold forever | `Cancel()` still zeroes it | `SystemMenuExitsTests` (abandon) or `SaveSlotFlowTests` (delete) | no -- both fixtures assert whole holds and early releases, never a repeated `Begin` | yes |
| A12 | `ExitsController` | the OTHER exit pressed while one is armed | the first disarms, the second arms, fill resets to 0 | one armed exit at a time | `SystemMenuExitsTests` | YES -- `ArmingOneExitDisarmsTheOther` | asserted |
| A13 | `ReckoningController` | Continue pressed while `Sweep`/`SweepToSummary` is mid-flight | the summary lands at rest, in one place, not part-way through a second sweep | 2 stops for 4 starts -- which of the 5 coroutines is unguarded is the thing to find | `ReckoningPhaseTests` | partly -- `TheSummaryLandsAtRestRatherThanPartWayThroughTheSweep` asserts the end of an UNinterrupted sweep | yes |
| A14 | `ReckoningController` | a second reward chosen while `FillBar`/`Flash` runs | one choice takes; the bar ends on the real number | `NothingIsLeftSmearedOnceTheSweepHasLanded` still holds | `ReckoningTabTests.TheGainCounterEndsOnTheRealNumber` extended | no | yes |
| A15 | `RewardTrackController.Motion` | a second node pressed while `Glide`/`AdvanceRail`/`CardSwap` is live | one glide; the rail ends at the pressed node | 4 stops + 1 `StopAllCoroutines` cover 1 start -- assert nothing is left running | `RewardTrackClaimTests` | partly -- `PressingAnUnreachedNodeGlidesRatherThanClaiming` presses once | yes |
| A16 | `MapController.Walk` | a second room clicked while `WalkAndArrive` is walking | exactly one arrival is written, by `Arrive(target)`, for the room actually walked to | `PanTo` not left running against a stale target | `MapFlowTests.TheFigureWalksTheTrailAndArrivesStandingInTheRoom` | no -- `AnUnreachableRoomIsNotWalkedTo` refuses before starting, which is a different case | yes |
| A17 | `HubController` | the descent gate pressed twice | the gate is already non-interactable after the first press, so the second cannot start a second `BeginDescentTransition` | one `FadeToBlack` | `HubDescentTransitionTests` | YES -- `PressingTheGateDisablesItAndVisiblyMovesThePanelBeforeAnythingElseHappens` | asserted |
| A18 | `TalentController.Motion` | a second stone kindled while the overshoot settle is running | both stones end settled at their rest scale | no Update-driven settle left owning a stone | `TalentInvestmentTests.KindlingOvershootsTheStoneAndSettlesItBack` extended to two | no | yes |
| A19 | `ThemedButtonState` | hover leaves while `FadeColor`/`FadeAlpha`/`FadeScale` is mid-fade | the button ends at the rest colour, alpha and scale for its CURRENT state, not at the interrupted value | 3 stops for 3 starts | any screen fixture with a themed button (`SystemMenuTests`, `FightFlowTests`) | no | yes |
| A20 | `ColumnOpenAnimator` | the column re-shown while its slide is playing | `Show.SetShown`'s guard means an already-active object is not re-activated, so the slide does NOT replay from the left | one slide per open | `FightFlowTests.PressingSkillOpensTheColumnWithTheAuthoredSkillInIt` extended to a re-show | no -- `Show.cs`'s header names this exact hazard and nothing pins it | yes |
| A21 | `DefeatController` | a press arriving while `PlayIn` is running | the press is either ignored or ends the intro cleanly; the screen never lands half-played | 1 stop for 1 start | `DefeatScreenWiringTests` | no | yes |
| A22 | `FightController.Hud` | a second `AppearancePopRoutine` on the same plate | one pop; the plate ends at its rest scale | 2 stops for 1 start | `FightFlowTests` | no | yes |
| A23 | `StaticSwing` | a second `Windup` before the first fires its impact | the two impacts do not collapse into one frame; the beat player's wait and the swing's report stay the one number | no orphan wait | `FightBeatPlayerFixtureTests`'s no-scene rig | no | yes |
| A24 | `SystemMenuController` | Escape pressed twice within one frame | the `EscapeKey` frame stamp means exactly one component acts | `_consumedFrame` cleared for the next frame | `SystemMenuTests` | YES -- `EscapeOverTheGlossaryDoesNotAlsoOpenTheMenu` | asserted |
| A25 | `FightController.Input` | a verb pressed while the previous action is still resolving | the detail card hides at commit, and no second action reaches the session | submenu closes once | `FightFlowTests` | partly -- `TheDetailCardHidesTheInstantAnActionCommits_NotAfterTheAnimationFinishes` and `TheMenuClosesItselfOnEveryResolution` | yes |

## Family B: disable and re-enable

Every `OnEnable`/`OnDisable` pair restores state and does not accumulate
subscriptions. The shared shape in this codebase is a `Wire()` guarded by a
`_wired` bool called from `OnEnable`, with NO unsubscribe in `OnDisable` -- so
the contract to assert is "listeners are added exactly once across N enables",
and the failure mode is a click firing twice.

| id | controller | trigger | expected state after | cleanup expected | how to drive | already asserted | buildable |
|---|---|---|---|---|---|---|---|
| B1 | `FightController.StageVisuals` | disable then re-enable the FightController while the stage is idling | figures breathe again | **`_idling` is never nulled or stopped** (`:1006,1036,1038`): Unity stops the coroutine on disable but the handle stays non-null, so `StartIdleBreathing` takes its early return and the stage stays still. The expected state is "breathing resumes"; write the test to that, not to the code | `StageAnimationTests.AnIdleFigureBreathesEvenThoughItsDrawingCannot` measures the breath already -- disable/re-enable around it | no | yes, and it is the cheapest high-value row in this file |
| B2 | `RewardTrackController` | disable mid-fly-in, re-enable | `OnDisable`'s `StopAllCoroutines` leaves nothing running; `OnEnable` re-wires (once), refreshes and replays `BeginFlyIn` from rest | burst roots reset; `_hovered` back to -1 | `RewardTrackClaimTests` | partly -- `ClosingTheTrackWhileANodeIsHoveredDoesNotThrow` covers the hover half only | yes |
| B3 | `ExitsController` | disable with an exit armed, re-enable | arrives disarmed, hold cancelled, fill 0, context re-applied | no armed exit survives the cycle | `SystemMenuExitsTests` | partly -- `SwitchingTabsForgetsAnArmedExit` is the same mechanism through the tab bar | yes |
| B4 | `ThemedButtonState` | disable while hovered, then re-enable | not hovering, not selected, not pressed, and the glow colour keeps its ORIGINALLY captured hue -- `CaptureThemeGlowColor` is idempotent by construction ("RGB never changes once captured (only .a ever does)"), so `OnEnable`'s call to it is not a re-capture against a current theme; the game has no runtime theme swap for a later capture to differ against | `ApplyImmediate` leaves no fade running | a fixture that poisons the glow while the button is down, disables, re-enables, and asserts the hue is unchanged -- no theme swap needed, since none is live | no | yes |
| B5 | `CharacterDossierController` | enable/disable three times, then click Next once | the handler fires exactly once (the `_wired` guard is the whole defence) | no duplicate listener | `DossierEquipTests` / `DossierSpellSlotsTests` | no | yes |
| B6 | `PartyController` | same shape: three enables, one seat click | one swap persisted, not three | drag state (`_dragResolvedFrame`) back to -1 | `SystemMenuPartyTests` | no | yes |
| B7 | `OptionsController` | same shape, one slider drag | one settings write | -- | `OptionsPaneTests` | no | yes |
| B8 | `GlossaryController` | same shape, one category click | one selection | -- | `GlossaryTests` | no | yes |
| B9 | `DebugMenuController` | same shape, one Give Gold press | one grant reaches the wallet AND disk, not three | -- | `DebugMenuTests.GoldLandsOnTheBankedWalletAndReachesDisk` | no | yes |
| B10 | `SaveSlotController` | same shape, one slot press | one `EnterSlot` | -- | `SaveSlotFlowTests` | no | yes |
| B11 | `ResetProgressController` | same shape, one delete confirm | one delete; the confirm hold is NOT cancelled by disabling the manage-saves panel -- `ResetConfirmPanel` is a SIBLING of the manage-saves modal under `MainMenuPanel`, not a child, so closing manage-saves disables neither the confirmation nor its hold. Containment is by the confirmation's own full-screen scrim covering the Back button underneath, not by hierarchy/disable (`ResetProgressController`'s own comment claims the disable "covers every path OUT of this panel"; it covers every path a PLAYER can take, not every disable) | a half-finished delete is forgotten by every way OUT of the confirmation itself, not by a disable it does not receive | `SaveSlotFlowTests.ConfirmingActuallyDeletes_AndCancellingDoesNot` | no | yes |
| B12 | `RunStatsController` | re-enable after the run's gold moves | `OnEnable => Refresh()` repaints every value; a null `values`/`valueKeys` is tolerated | -- | `SystemMenuRunStatsTests` | partly -- that fixture asserts the figures, not the re-enable | yes |
| B13 | `HubController` | re-enable after a wallet or roster change | `OnEnable => Refresh()` repaints the currency line and the six destinations | -- | `ScreenWiringTests.TheCurrencyLine_IsFormattedFromTheSameTemplateTheBuilderUsed` | no | yes |
| B14 | `SystemMenuController` | disable the menu object while it is paused | `OnDisable => Resume()` gives the clock back | `Time.timeScale` is 1 | `SystemMenuTests` | YES -- `ClosingRestoresTheClockItFound`, `OpeningOnAStoppedClockStillGivesTheGameBack`, `SystemMenuExitsTests.LeavingThroughAnExitPutsTheClockBack` | asserted |
| B15 | `FightBeatPlayer` | disable mid-fight | `OnDisable => EndFight()`; popups reclaimed | pool whole for the next fight | `FightFlowTests` | YES -- `DisablingThePlayerReclaimsToo` and `ThePoolSurvivesRepeatedFights` | asserted |
| B16 | `SpellParticleRenderer` | disable mid-cast | `OnDisable => ReleaseAll()`; every renderer back in the pool | no renderer left claimed | `SpellRendererAndClockTests` | partly -- `EveryRendererComesBackWholeWhenItsCastEnds` covers the end of a cast, not a disable | yes |
| B17 | `SpellVfxPlayer` | disable mid-effect | `OnDisable => StopImmediately()`; no frame left drawn | dissolve layer cleared too | `SpellVfxTests.StoppingClearsTheDissolveLayerTooNotJustTheFrame` | partly, same distinction as B16 | yes |
| B18 | `SpellPerformancePlayer` | disable mid-performance | `OnDisable => CancelAll()` | `ClockOverride` untouched (it is a test seam, not per-cast state) | `SpellRendererAndClockTests` | no | yes |
| B19 | `HoverIndex` | the object disabled (including during scene teardown) | `OnDisable` fires `Changed?.Invoke(Index, false)` -- every subscriber must tolerate being called during teardown, when siblings may already be destroyed | no `MissingReferenceException` logged | any screen with a hover row; `FightFlowTests` submenu rows | no | yes |
| B20 | `SubtleHoverScale` | disabled mid-hover | `localScale` back to `_baseScale` exactly, `_isHovering` false | -- | any button fixture | no | yes |
| B21 | `ButtonPressAnimator` | disabled mid-press | `localScale` back to `_baseScale`, `_isPressed` and `_isHovering` false | -- | any button fixture | no | yes |
| B22 | `EmberFlare` | re-enabled after being disabled mid-flare | base scale, position and colour restored by `OnEnable` before the next flare | -- | `TalentEdgeEnergyTests` / `TalentKindleGlowTests` | no | yes |
| B23 | `ColumnOpenAnimator` | re-enabled by a genuine `SetActive(true)` (not a guarded re-show) | the slide replays from `-SlideDistance` and alpha 0, exactly once | `_playing` false at rest | `FightFlowTests` | no | yes, and it is the counterpart to A20 |
| B24 | `MapController` | disabled and re-enabled mid-scene | **no `OnEnable`/`OnDisable` exists**, so Update-driven pan state is whatever it was. Expected: either the controller is never disabled in this scene (state the argument) or it restores | -- | `MapFlowTests` | no | yes, but the useful half is the reachability argument, not the test |
| B25 | `PlaytimeTracker` | disabled and re-enabled | accumulated time is not double-counted or lost across the gap | -- | any Hub fixture | no | yes |
| B26 | `CursorController` | disabled while the cursor is in the click state | the cursor is not left stuck on the click texture for the next screen | -- | `CursorControllerTests` | partly -- `ClickBeatsHover_AndHoverBeatsIdle` is precedence, not teardown | yes |
| B27 | `TalentController` / `.Motion` | the talents screen re-entered after a kindle | constellations settle where the first one sits; no motion state carried in | -- | `TalentInvestmentTests.EveryConstellationSettlesWhereTheFirstOneSits` | partly | yes |
| B28 | `FightController` / `.Hud` / `.Input` | the fight object disabled and re-enabled between encounters | HUD repaint is complete (`_pulsingMeters` cleared once per full repaint), input not double-wired | see B1 for the stage half | `FightSceneFixture.LoadFight()` twice | no | yes |
| B29 | the seven `Time.time` ambience movers (`BeaconPulse`, `HubBuildingLooper`, `KenBurnsDrift`, `LanternFlicker`, `MoteDrift`, `SlowDrift`, `StarTwinkle`) | disabled and re-enabled | each recomputes from its Awake-captured base rather than from wherever it stopped, so re-enable does not snap | base transform restored | `HubAmbienceTests`, `ScreenWiringTests.TheAmbientLayerIsFullyPopulated` | no | yes, one parameterised test over the seven |
| B30 | `TalentEdgeCrackle`, `TalentEdgeSpark` (`Time.unscaledTime`) | disabled and re-enabled while the game is PAUSED | they keep moving on the unscaled clock, which is the intent for an overlay -- assert that, so nobody "fixes" it to `Time.time` | -- | `TalentEdgeEnergyTests` | no | yes |

## Family C: unload during callback

A scene load arriving while a tween, popup or cast is mid-flight. AUDIT #106
already records one live instance of this family ("loading a fight scene over a
live one logs an error, because a status badge pops on a panel that is already
inactive"), so it is not hypothetical.

| id | controller | trigger | expected state after | cleanup expected | how to drive | already asserted | buildable |
|---|---|---|---|---|---|---|---|
| C1 | `DamagePopup` | `LoadSceneAsync("Hub")` while `Rise` is running | no `MissingReferenceException` logged; the popup pool is whole for the NEXT fight | `LogAssert.NoUnexpectedReceived()` in `[TearDown]` | `FightSceneFixture.LoadFight()`, deal damage, load Hub mid-rise | no -- `ThePoolSurvivesRepeatedFights` reloads between fights, not during a rise | yes |
| C2 | `StageDeathFade` | scene load mid-`FadeRoutine` | no error; the next fight's slots start unfaded | `AFreshFightUnfadesWhoeverDiedInTheLastOne` still passes afterwards | as C1 | no | yes |
| C3 | `StageActorAnimator` | scene load mid-tween (any of its 7 coroutines) | no error; no transform written after destroy | -- | as C1 | no | yes |
| C4 | `PartyToast` | scene load mid-`HoldThenFade` | no error; no toast text on the next screen | -- | `SystemMenuPartyTests` + a load | no | yes |
| C5 | `SpellVfxPlayer` + `SpellParticleRenderer` | scene load mid-cast | renderers released or destroyed together; the next scene's first cast still gets a renderer | `AnEmitterWithNoFramesDrawsNothingAndDoesNotStarveTheNextCast`'s property must survive a load | `SpellVfxTests` / `SpellRendererAndClockTests` + a load | no | yes |
| C6 | `FightBeatPlayer` | a fight scene loaded OVER a live fight, mid-`PlayBeats` | this is AUDIT #106's exact shape: a badge popping on an already-inactive panel logs an error | no unexpected log | `FightSceneFixture.LoadFight()` twice without settling | no -- AUDIT #106 is filed and open | yes, and it should reproduce #106 rather than avoid it |
| C7 | `HubController` | a SECOND scene load arriving while `FadeToBlack`/`BeginDescentTransition` is running | one destination is reached, and the descent is not half-settled | `RunManager` state consistent afterwards | `HubDescentTransitionTests.TheTransitionStillReachesTheRelicDraftAFreshRunUsedToReachDirectly` extended | no | yes |
| C8 | `ReckoningController` | scene load mid-`Sweep`/`SweepToSummary` | no error; the reward is either fully applied or not applied, never half | the run's ledger consistent | `ReckoningPhaseTests` + a load | no | yes |
| C9 | `RewardTrackController.Motion` | scene load mid-`Glide`/`ClaimBursts` | claims are idempotent (ledger R9), so the reload must not double-pay | `StopAllCoroutines` in `OnDisable` is the only defence; `OnDestroy` has none | `RewardTrackClaimTests.TheSameLevelsAreNeverPaidTwice` extended across a load | partly -- idempotence is asserted, the load is not | yes |
| C10 | `DefeatController` | scene load mid-`PlayIn` | no error | -- | `DefeatScreenWiringTests` | no | yes |
| C11 | `MapController.Walk` | scene load mid-`WalkAndArrive` (entering a fight room while walking) | the fight loads from the room actually arrived at | see D1 for the save half | `MapFlowTests.EnteringAFightRoomLoadsTheFight` | no | yes |
| C12 | `StaticSwing` | scene load between the wind-up and the impact | the impact does not fire into a destroyed target | -- | `FightBeatPlayerFixtureTests` rig + a load | no | yes |

## Family D: restore during transition

A save, a Continue or a quit arriving while a walk, descent or settle coroutine
is running. This family is where the two hunts' most expensive bugs lived
(`95c0b8b3`, `45be6e6a`), so the scenarios are written against run state, not
against pixels.

| id | controller | trigger | expected state after | cleanup expected | how to drive | already asserted | buildable |
|---|---|---|---|---|---|---|---|
| D1 | `MapController.Walk` | `SaveCurrent()` while `WalkAndArrive` is walking, then reload | the save records the room the party is IN, not the one it is walking to; on reload the figure stands in the recorded room | no half-written `legStartStep` | `MapFlowTests` + `SaveSlotFlowTests`'s throwaway root | no | yes |
| D2 | `HubController` | quit (or `Navigation.QuitOverride`) during `BeginDescentTransition`, then `Continue` | equipment and the stockpile kept, `lifetimeRunsEnded` unchanged -- the `45be6e6a` contract, driven through the TRANSITION rather than through `RunManager` directly | `SettleAnyRunAPreviousSessionLeftBehind` leaves the roster whole | `HubDescentTransitionTests` + `SaveSlotFlowTests`'s root | partly -- `RunEndingTests` asserts the domain half at the seam, never through the screen | yes |
| D3 | `ReckoningController` | save during the sweep, reload | the reward is applied once; `deepestStep` and the ledger agree with the map header | -- | `ReckoningPhaseTests` + a reload | no -- and `deepestStep`'s disagreement with the map header was a real bug (`d529a375`) | yes |
| D4 | `RewardTrackController` | save during `ClaimBursts`, reload, claim again | the same levels are never paid twice | -- | `RewardTrackClaimTests.TheSameLevelsAreNeverPaidTwice` across a reload | partly | yes |
| D5 | `TalentController` | save during a kindle settle, reload | the ember spend and the talent id are both on disk, or neither | carried health rescaled (`33d88bcc`) | `TalentInvestmentTests` + `CarriedHealthOnRespecTests` | no | yes |
| D6 | `SaveSlotController` / `ResetProgressController` | delete the slot currently open, while its `OnEnable` refresh is pending | the slot does not come back on the next save; Continue disappears | `SaveSlotManager` cache dropped | `SaveSlotFlowTests` | YES -- `DeletingTheSlotYouAreIn_DoesNotComeBackOnTheNextSave`, `DeletingTheContinueSlot_HidesContinueOnceItIsGone` | asserted |
| D7 | `FightController` | save (autosave or system menu) while a beat is mid-flight | carried health written back is the settled value, not a mid-beat one | `RunEncounter.WriteBackHealth` keyed by `characterId` (ledger R8) | `FightSceneFixture.LoadFight()` + throwaway save root | no | yes |
| D8 | `SystemMenuController` | the menu opened (clock paused) at the moment a scene load starts | the clock is given back on the other side; no scene runs at `timeScale` 0 | `TestGlobals.ResetAll()` would mask this -- assert BEFORE it | `SystemMenuExitsTests.LeavingThroughAnExitPutsTheClockBack` extended to a load mid-pause | partly | yes |

## Family E: repeat input during a hold, and static clocks leaking

| id | controller | trigger | expected state after | cleanup expected | how to drive | already asserted | buildable |
|---|---|---|---|---|---|---|---|
| E1 | `HoldToConfirm` | the key mashed during a hold | `_elapsed` advances monotonically; a repeat `Begin` neither restarts nor extends it | `Cancel()` on release zeroes it and reports | `SystemMenuExitsTests.AbandonNeedsTheWholeHold` extended with repeats | no (see A11 -- same guard, different lens) | yes |
| E2 | `HoldToConfirm` | release early, press again immediately | the second hold starts from zero, not from the abandoned progress | -- | `SaveSlotFlowTests.ReleasingTheHoldEarly_DeletesNothing` extended | no | yes |
| E3 | `ExitsController` | a third press after the second has already left | nothing fires twice; the exit is gone with the scene | -- | `SystemMenuExitsTests.TheSecondPressIsWhatLeaves` extended | no | yes |
| E4 | `ExitsController` | an armed exit left alone past its forget timer, then pressed | the press ARMS rather than leaves | `AnArmedExitForgetsItselfAfterAWhile` is the first half | `SystemMenuExitsTests` | partly | yes |
| E5 | the nine unasserted statics in `TestGlobals.ResetAll` (`ReckoningController.SpeedMultiplier`, `RewardTrackController.SpeedMultiplier`, `TalentController.MotionSpeedMultiplier`, `HubController.MotionSpeedMultiplier`, `FightController.BreathSpeedMultiplier`, `SpellPerformancePlayer.ClockOverride`, `Time.timeScale`, `RequirementCurve.Percent`, `RequirementCurve.GearRequirementsEnabled`) | set each, load a scene, assert the new scene reads the SHIPPED default only after `ResetAll` | one parameterised test per knob | `GlobalStateLintTests` catches a missing teardown at WRITE time; this catches a missing entry in `ResetAll` itself | `BattleSpeedFixtureIsolationTests` is the pattern for exactly two of the eleven | partly -- `TheSharedHelperPinsThePace` and `ResetAllPinsThePaceToo` cover the battle-speed pair only | yes |
| E6 | `EscapeKey` | a PlayMode test that presses Escape, followed by another test's first frame | `_consumedFrame` must not still read as consumed. **`EscapeKey.Reset()` exists for exactly this reason and is called in `SystemMenuTests` only -- it is NOT in `TestGlobals.ResetAll`, and `GlobalStateLintTests` cannot see it because no test WRITES the field; production writes it via `Consume()` during the test** | either `ResetAll` gains the call or the lint gains a production-writes-it rule | any two adjacent PlayMode tests, the first pressing Escape | no | yes; the finding to raise is the missing `ResetAll` entry, not the test |
| E7 | the seven `Time.time` ambience movers | two scenes loaded at different session ages | each mover's phase is a function of ABSOLUTE session time, so a capture comparing frames is not reproducible unless the clock is pinned | any capture fixture over ambience must pin a clock or compare phase-independently | `HubAmbienceTests`, `ScreenWiringTests.TheAmbientLayerIsFullyPopulated` | no | yes for the phase-dependence assertion; NOT buildable as a byte-identical capture without a clock seam these movers do not have -- see "not buildable" below |
| E8 | `SystemMenuController` | the menu paused (`timeScale` 0) while the seven `Time.time` movers are running and the three `Time.unscaledTime` ones are not | the scaled movers freeze and the unscaled ones do not, deliberately | -- | `SystemMenuTests.OpeningPausesAndClosingResumes` extended to the ambience | no | yes |
| E9 | `PlaytimeTracker` | the game paused, then resumed | `Time.unscaledDeltaTime` means paused time IS counted -- assert the intended answer rather than leaving it undecided | -- | any Hub fixture with a pause | no | yes, but the expected value is an OWNER decision (does time in the menu count as playtime?) -- record it as `owner decision` if the answer is not already written down |

## Scenarios that cannot be built today

| id | why not | what would unblock it |
|---|---|---|
| E7 (capture half) | the ambience movers read `Time.time` directly with no injectable clock, so no PlayMode test can make two runs produce the same frame | a clock seam on the movers, the shape `SpellPerformancePlayer.ClockOverride` already has |
| any scenario on `UiWiringSweep`, `UiCountAudit`, `UiBindingAudit` or `UiAudit`'s driver | they live in the Editor assembly, which neither EditMode's Domain-only asmdef nor PlayMode may reference (`CODE_STANDARDS.md` Sec1) | an Editor-assembly test host -- manifest lead L10 |
| `B24` as a real test | `MapController` may never actually be disabled in its scene; the useful artefact is the reachability ARGUMENT, and an unreachable-path test is a tautology | nothing; record the argument as `static read` evidence and close the row |
| `A6` at the second-life boundary | reaching a second life mid-fade needs a fixture that grants `SecondLife` and kills the holder on a specific beat; `RewardTrackClaimTests.ASecondLifeIsCountedPerCollectingCharacter` grants it but does not stage the death | a fight fixture that can force a death on a chosen beat |

## Coverage of the 53 lifecycle files

| file | scenarios | note |
|---|---|---|
| `BeaconPulse.cs` | B29, E7, E8 | ambience mover, `Time.time` |
| `ButtonPressAnimator.cs` | B21 | |
| `CharacterDossierController.cs` | B5 | `Wire()`-once + `Update` |
| `ColumnOpenAnimator.cs` | A20, B23 | the guarded-vs-real re-show pair |
| `CursorController.cs` | B26 | |
| `DamagePopup.cs` | A7, C1 | pooled; `Flush` is the existing seam |
| `DebugMenuController.cs` | B9 | |
| `DefeatController.cs` | A21, C10 | |
| `EmberFlare.cs` | B22 | `Time.unscaledTime` |
| `ExitsController.cs` | A12, B3, E3, E4 | best-covered controller in the tree |
| `FightBeatPlayer.cs` | A1, B15, C6 | B15 is fully asserted |
| `FightController.Hud.cs` | A22, B28 | |
| `FightController.Input.cs` | A25, B28 | |
| `FightController.StageVisuals.cs` | B1, E8 | B1 is the highest-value unasserted row here |
| `FightController.cs` | B28, D7 | |
| `GlossaryController.cs` | B8 | |
| `HoldToConfirm.cs` | A11, E1, E2 | |
| `HoverIndex.cs` | B19 | fires an event from `OnDisable` |
| `HubBuildingLooper.cs` | B29, E7 | `never-named` in the manifest too |
| `HubController.cs` | A17, B13, C7, D2 | D2 is the `45be6e6a` shape |
| `KenBurnsDrift.cs` | B29, E7 | |
| `LanternFlicker.cs` | B29, E7 | |
| `MapController.Walk.cs` | A16, C11, D1 | |
| `MapController.cs` | B24 | no `OnEnable`/`OnDisable` at all |
| `MoteDrift.cs` | B29, E7 | |
| `OptionsController.cs` | B7 | |
| `PartyController.cs` | B6 | |
| `PartyToast.cs` | A8, C4 | |
| `PlaytimeTracker.cs` | B25, E9 | E9 needs an owner answer |
| `ReckoningController.cs` | A13, A14, C8, D3 | 5 coroutines, 4 starts, 2 stops -- the widest gap between starts and stops in Core |
| `ResetProgressController.cs` | B11, D6 | |
| `RewardTrackController.Input.cs` | A15 | `Update`-driven; `never-named` |
| `RewardTrackController.Motion.cs` | A15, C9 | |
| `RewardTrackController.cs` | B2, C9, D4 | |
| `RunStatsController.cs` | B12 | |
| `SaveSlotController.cs` | B10, D6 | |
| `SlowDrift.cs` | B29, E7 | |
| `SpellParticleRenderer.cs` | B16, C5 | |
| `SpellPerformancePlayer.cs` | A9, B18 | `ClockOverride` is the clock seam the movers lack |
| `SpellVfxPlayer.cs` | A10, B17, C5 | |
| `StageActorAnimator.cs` | A2, A3, C3 | 7 coroutines, 4 starts, 7 stops |
| `StageDeathFade.cs` | A6, C2 | |
| `StageHitFlash.cs` | A4 | |
| `StageShake.cs` | A5 | |
| `StarTwinkle.cs` | B29, E7 | |
| `StaticSwing.cs` | A23, C12 | |
| `SubtleHoverScale.cs` | B20 | |
| `SystemMenuController.cs` | A24, B14, D8, E8 | the clock owner |
| `TalentController.Motion.cs` | A18, B27 | |
| `TalentController.cs` | B27, D5 | |
| `TalentEdgeCrackle.cs` | B30 | `Time.unscaledTime`, deliberately |
| `TalentEdgeSpark.cs` | B30 | `Time.unscaledTime`, deliberately |
| `ThemedButtonState.cs` | A19, B4 | |

Comment-only lifecycle matches, no scenario owed: `CanvasCapture.cs`,
`ContentDatabase.Effective.cs`, `EscapeKey.cs` (its STATIC is E6's subject, but
the file declares no Unity message), `RunEncounter.cs`, `SheetPanel.cs`,
`Show.cs`.

## Tally

**84 scenarios**: A 25 (interrupt), B 30 (disable/re-enable), C 12 (unload
during callback), D 8 (restore during transition), E 9 (repeat input and static
clocks).

**81 buildable as written. 3 carry a stated blocker** -- A6 at the second-life
boundary, B24 as a test rather than as a reachability argument, and the capture
half of E7 -- plus one whole CLASS with no scenario at all, the Editor-assembly
audits, because this tree has nowhere to put their fixture (manifest lead L10).

**6 are already fully asserted** by an existing test and are recorded so nobody
writes them twice: A12, A17, A24, B14, B15, D6. Fourteen more are marked
`partly` -- an existing test covers the uninterrupted or unloaded case and stops
short of the scenario, which is the shape most of this list has.

**1 needs an owner answer before it can have an expected value**: E9 (does time
spent in the system menu count as playtime?).
