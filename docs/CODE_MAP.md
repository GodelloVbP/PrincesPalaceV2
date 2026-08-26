# Code Map

Screens/systems → files, so a change starts with "read this one file" instead
of grepping a giant one. Written after Phases 3-4 of the workflow-standards
restructure split `SceneBuilder.cs` and `FightController.cs` into
per-topic partial-class files.

**Update rule: adding a screen, system, or partial-class part file without
touching this map is an incomplete change.** See `docs/WORKFLOW.md` §11's
doc update-rules index.

---

## Screens → files

Each screen is: a tree in `Domain/UiKit/Screens/` that declares it, a
controller (+ its own parts, for Fight), its tests, and its content data
(where applicable). Wiring for every screen goes through `ScreenRegistry.cs`
(see "The UI construction layer" below) — the `Run Map` row still names v1's
per-screen `SceneBuilder.*.cs` files, which is a known, deliberately
unrepaired staleness (see the note further down this file).

| Screen | Screen tree | Controller | Content |
|---|---|---|---|
| Main Menu / Save Slots | `Domain/UiKit/Screens/MainMenuScreen.cs` + `MainMenuAmbience.cs` | `MainMenuController.cs`, `SaveSlotController.cs` | `characters.json` |
| Run Map | `SceneBuilder/SceneBuilder.Map.cs` | `DescentMapView.cs`, `MapController` (see `GameplayManager.cs`) | `enemies.json` (room pools) |
| Fight (combat) | `Domain/UiKit/Screens/FightScreen.cs` | `FightController.cs` (root) + its 4 parts, see below | `skills.json`, `spells.json`, `enemies.json`, `weapons.json` |
| Rewards / Item Choice ("The Reckoning") | `Domain/UiKit/Screens/ReckoningScreen.cs` (wired as `fight.reckoning` inside the Fight scene via `ScreenRegistry.cs`) | `ReckoningController.cs` | `items.json`, `itemsets.json` |
| Character Dossier (sheet + bag + paperdoll) | `Domain/UiKit/Screens/CharacterDossierScreen.cs` (wired as a System Menu tab via `ScreenRegistry.cs`) | `CharacterDossierController.cs` | `characters.json`, `items.json` |
| Shop / Store | — (unbuilt; `RoomResolver` clears Shop rooms while saying so, see "Rooms that are not fights" below) | — | `items.json` (Upgrades/Consumables) |
| Hub | `Domain/UiKit/Screens/HubScreen.cs` + `HubAmbience.cs` | `HubController.cs`, `HubBuildingLooper.cs` | — |
| Talents | `Domain/UiKit/Screens/TalentScreen.cs` + `Domain/UiKit/ConstellationLayout.cs` | `TalentController.cs` + `.Motion.cs` | `talents.json` |
| Relics (start-of-run draft) | `Domain/UiKit/Screens/RelicDraftScreen.cs` (wired into Hub via `ScreenRegistry.cs`) | `RelicDraftController.cs` | `relics.json` |
| System Menu (Pause replacement — tabs: Dossier/RewardTrack/Options/RunStats/Exits) | `Domain/UiKit/Screens/SystemMenuScreen.cs` (wired into Fight/Map/Hub via `ScreenRegistry.cs`) | `SystemMenuController.cs` | — |

Generic building-block primitives (`CreateButtonStrip`, `AssertColumnClears`,
`CreateFramedPanel`, the `Hud*`/`Suite*` color palette, `CreateGearCell`/`Icon`/`Text`, etc.) live
in `SceneBuilder/SceneBuilder.Widgets.cs` — check there before adding a new
one, per `docs/CODE_STANDARDS.md` §2.

## The UI construction layer

v2 has no `SceneBuilder` partial-class family. v1's was 7,608 lines across 14
parts, and all four of its documented failure modes came from layout arithmetic
living at construction sites. There are no construction sites now.

**`Domain/UiKit/` — engine-free, so screens are testable without a scene:**

`UiVec` · `UiRect` · `Place` · `UiSize` · `UiPad` · `UiAlign` · `UiTextAlign` ·
`UiNode` ·
`NodeRef` · `Ui` (the factories) · `UiString` · `UiStrings` · `UiSolver` ·
`SolvedNode` · `UiAudit` · `UiAuditError` · `UiFrames` · `FightSubmenuLayout`

Screens live in `Domain/UiKit/Screens/` — one class per screen returning a tree
plus typed handles (`MainMenuScreen`, `MainMenuAmbience`).

**`Editor/SceneBuilder/` — the only code that touches GameObjects:**

`SceneBuilder.cs` (camera, global Volume, canvas, EventSystem, sprite loading,
`BuildAllScenes`) · `UiEmitter.cs` · `UiEmitResult.cs` · `ScreenRegistry.cs` ·
`UiTextFitAudit.cs` (E1) · `UiCountAudit.cs` (E4) · `UiWiringSweep.cs` (E3) ·
`ScreenshotTool.cs` · `TmpBootstrap.cs`

Plus `Editor/PipelineBuilder.cs`, which generates the URP asset, the Renderer 2D
and the post-processing profile under `Assets/_Project/Rendering/`.

**To add a screen:** write its tree in `Domain/UiKit/Screens/`, add a `ScreenDef`
to `ScreenRegistry.All`. That one entry gives it scene building, build-time
audits and screenshot support — there is no second list to update.

## `FightController` — current part list

> This section described a pre-rebuild shape (`.Encounter.cs`/`.Actions.cs`/
> `.Turns.cs`/`.Outcome.cs`/`.HudMenu.cs`/`.Beats.cs`/`.Talents.cs`) that no
> longer exists on disk — the Fight-screen rebuild documented in "The Fight
> screen (v2)" below moved that logic to Domain. Corrected to the actual
> current files rather than left to rot beside its own replacement section.

Root stays at `Assets/_Project/Scripts/Core/FightController.cs` — this IS a
MonoBehaviour the generated scene binds by this file's script GUID, so its
name/path/`.meta` are frozen. Parts live beside it, same folder:

| Part | Covers |
|---|---|
| `FightController.Hud.cs` | Painting, and nothing else |
| `.Input.cs` | Clicks in, session commands out; `CanAct` asked in ONE place |
| `.SpellVfx.cs` | Spell VFX playback — see "Spell VFX" below |
| `.StageVisuals.cs` | Actors, poses and grounding — see "The stage" below |

The menu/turn/encounter/outcome/talent logic this table used to list here now
lives in Domain (`Domain/Combat/Session/FightSession*.cs`,
`Domain/.../FightMenuState.cs`, `Domain/.../FightHudModel.cs`) and in
`Core/FightBeatPlayer.cs` — see "The Fight screen (v2)" below for the full,
current breakdown.

`namespace PrincesPalace { public partial class FightController }` (root adds
`: MonoBehaviour`). Both asmdef-visible from `PrincesPalace.Core`.

---

## Domain map

`Assets/_Project/Scripts/Domain/` (`PrincesPalace.Domain`, engine-free,
EditMode-testable):

| Folder | Covers |
|---|---|
| `Audio/` | Adaptive music: `MusicIntensity` (the four tiers), the `music_layers.json` raw shapes and `MusicLayerResolver`, the resolved `MusicLayerSet`/`MusicLayerLibrary`, and `MusicClock` (bar-boundary arithmetic) |
| `Combat/` | `CombatMath` (armour is diminishing returns via `Mitigate`, not a subtraction — see its header for the floor-one-boss-took-130-turns bug this replaced), `CombatEncounter`, `CombatantState`, damage/effectiveness formulas, plus the talent-rework additions: `TalentEffect` (the closed rule vocabulary a talent can grant), `TalentEffectSet` (a character's rules, flattened once per fight) and `Transformation`/`TransformGrant` (Black Ram Mode) |
| `Content/` | Raw/resolved content shapes + `*EntryResolver`s (validation) for every JSON-authored content type, plus the content enums they parse (`CharacterRole`, `RelicEffect`) |
| `Dungeon/` | `DifficultyCurve`, room/map generation logic |
| `Economy/` | `Wallet`, `CurrencyType` |
| `Equipment/` | `EquipmentSlot(s)`, `EquipmentLoadout` |
| `Relics/` | `RelicLoadout` (party-wide relic ownership/assignment) |
| `Rewards/` | `CombatReward`, `CharacterReward`, offer tables |
| `Rng/` | `SeededRandom` (built, not yet wired — see `AUDIT.md`) |
| `Stage/` | Stage-side/depth/layout pure geometry, `SpriteFacing`, `StanceManifest` (authored ground lines + stance timing + per-actor breath), `BreathCurve` (the continuous scale transform every idle figure gets, sheet-driven or not — see its own header) |
| `Stats/` | `StatBlock`, `StatType`, `AbilityDerivation` |

## Core map

`Assets/_Project/Scripts/Core/` (`PrincesPalace.Core`) — 59 files. Beyond the
per-screen controllers in the table above: `Content/` (the `*Definition`
ScriptableObject types), `Data/` (`Character`, `SaveData`, `RunState`),
`ContentDatabase.cs` (the single content lookup point — split into a root
plus `.Validation.cs` (`ValidateContent`) and `.Effective.cs`
(`EffectiveStats`/`EffectiveAbilityScores`/`ActiveLoadout`/etc, the "what
does this character actually have right now" family), same
`public static partial class ContentDatabase` in all three, no namespace-
binding constraint since it's a static class, not a MonoBehaviour),
`GameplayManager.cs` (run lifecycle, screen switching via `OverlayState`),
`ItemIcons.cs` (shared id→sprite lookup, also used for portraits — see
`docs/CODE_STANDARDS.md` §2), `RarityColors.cs`, VFX primitives (`RadialGlowImage`,
`BeaconPulse`, `SolidCircleImage`, `SpellVfxPlayer`), the ambient-motion
primitives (`StarTwinkle`, `LanternFlicker`, `SlowDrift`, `MoteDrift`,
`KenBurnsDrift` — see `docs/CODE_STANDARDS.md` §2), and
`FrameSequenceLoader.cs` (the one f0..fN Resources probe behind
`StanceAnimationLibrary`, `SpellVfxPlayer` and `HubBuildingAnimator`).

Audio is the one family that does NOT go through `ContentDatabase`, because
`MusicController` and `SoundController` self-bootstrap before any scene loads
and therefore before it exists. Its two config tables are read by their own
loaders instead, both the same shape (Resources path, `JsonUtility`, cache,
`Reset()` seam, every failure degrading rather than throwing): `AudioLevels.cs`
reads `audio_levels.json` (per-clip loudness correction) and `MusicLayers.cs`
reads `music_layers.json` (which stems make up a floor's song, and which of
them each intensity plays).

## Editor & tools map

`Assets/_Project/Scripts/Editor/` (`PrincesPalace.Editor`, one asmdef):
`SceneBuilder.cs` + `SceneBuilder/` (above), `ContentBuilder.cs` (generates
every ScriptableObject from `ContentData/*.json`), `ScreenshotTool.cs`
(headless panel capture, `KnownPanels` table — keep `tools/screenshot.ps1`'s
usage text in sync with it), `EnemySpriteImportPostprocessor.cs`,
`StanceSpriteImporter.cs` (forces Sprite import under `Resources/Enemies`,
`Resources/Characters` AND `Resources/Spells` — anything runtime-loaded as a
Sprite must be listed there or it silently loads as null).

`tools/` (all PowerShell/Python, see `docs/WORKFLOW.md` §8 for when to use
which):
- `run_tests.ps1` — original serial full-suite runner, still works
- `run_tests_parallel.ps1` — the "before committing" runner (~90-100s), two
  isolated copies in parallel, `-BuildContent`/`-BuildScenes`/`-SkipSync`,
  the `Assert-GuidsMatch` hardening (see `AUDIT.md` #24), and a test-area
  gate that refuses to run at all if a class matches no area or was never
  discovered (see `test_areas.ps1` below)
- `test.ps1` — the fast edit-run-edit loop (~12s for one class, more for a
  broad area; `-Changed` maps uncommitted files to the areas/classes they
  affect)
- `test_areas.ps1` — single source of truth for test-class discovery and the
  `combat`/`hub`/`content`/`run`/`ui`/`art`/`rng` area patterns, shared by
  both scripts above
- `screenshot.ps1` — headless screenshots. `-Panel`/`-All` capture the STATIC
  scene as `SceneBuilder` authored it (Edit Mode, no `Update()`); `-Runtime`
  captures the game ACTUALLY RUNNING by driving `RuntimeScreenshotTests`
  through the PlayMode test runner, which is what finally makes the ambient
  animators visible. All three omit `-nographics` — no device, no pixels
- `key_green_screen.py` — multi-kit green-screen/black-FX art keyer, see
  `docs/ART_PIPELINE.md`
- `process_map_icons.py`, `remove_portrait_backgrounds.py`,
  `slice_actor_sheet.py`, `slice_item_sheet.py`, `slice_spell_sheet.py` —
  art-pipeline slicers/keyers, one per asset category. `slice_actor_sheet.py`
  covers BOTH sides of the fight stage (`Resources/Enemies/<id>` and
  `Resources/Characters/<id>`) from one `ACTORS` manifest, since both resolve
  through one runtime path
- `sheet_slicing.py` — cell-cutting geometry shared by the two grid slicers
  (not run directly)
- `actor_stance_qa.py` — visual QA contact sheets + onion skins for actor
  stance art, rendered offline from the committed PNGs without booting Unity
  (`screenshot.ps1 -Runtime` covers animation that only exists in-engine);
  also reports the **redraw ratio** — churn per step over how far the figure
  actually moves — and ranks the roster by it, which is the check that names
  a sheet whose frames are redrawn rather than animated
- `trim_wav.py` — audio trimming utility

---

## Tests map

`Assets/_Project/Scripts/Tests/EditMode/` — 39 test classes, Domain-only.
`Assets/_Project/Scripts/Tests/PlayMode/` — 48 test classes (`GameplayTestBase.cs`
is the shared base, not a suite itself). Core+Domain. Class counts are from
`tools/test.ps1 -List`, the live, authoritative source — trust it over a
file count, since a single file can hold more than one test fixture.


## The Fight screen (v2)

Rebuilt across steps 0–10 of the Fight-screen plan. The shape is the whole
point, so it is worth stating once: **~34% of v1's `FightController` was never
about Unity**, and all of it now lives in Domain where it is tested in
milliseconds rather than by loading a scene.

### Domain — `Domain/Combat/Session/`

| File | What it owns |
|---|---|
| `FightSession.cs` | the session: kits, queries, the plain attack, the generic Skill verb, Hold Back |
| `FightSession.Beats.cs` | beat RECORDING, the retro-attach rule (AUDIT #13), stance and voice capture |
| `FightSession.Riders.cs` | Brave / Trample / Bloodlust, turn-start bookkeeping, victory resolution |
| `FightSession.Enemies.cs` | intents, the telegraph, the two skip paths, taunt redirection, the status rider |
| `FightSession.Skills.cs` | the fourteen-effect dispatch, role riders, the queue push |
| `FightSession.Talents.cs` | wool engines, wards, Shatter, Gifts, splash, the transform, Provoke |
| `DamagePipeline.cs` | the damage funnel, and the only place its composition order is stated |
| `VictoryRewards.cs` | the payout arithmetic (elite × depth), drop rolls, who earns what |
| `FightHudSpec.cs` | HUD capacities both the tree and the session read |
| `FightTuning.cs` | balance constants, out of the controller |
| `CombatBeat.cs` / `Vitals.cs` / `VoiceLine.cs` / `CombatantKit.cs` | the engine-free beat vocabulary |

`Domain/Stage/FightStageAnchors.cs` holds the stage's pixel anchors, and
**formally supersedes** `StageLayout`'s header note that anchors stay in
SceneBuilder — that note predates screen trees living in Domain.

### Domain — the screen tree

`Domain/UiKit/Screens/FightScreen.cs` declares the whole screen: two mirrored
stages, the initiative row, bark, enemy plates, party plate, three command
columns, and the only two genuine `Ui.Pool` sites in the game (the spell VFX
rect and the damage popups). `FightHudPalette.cs` holds the colours, which in
v1 were Editor-assembly `Color` fields the runtime controller could not read
and therefore restated.

### Core

`Core/FightController.cs` — references, the background swap, the static HUD
paint, and `AnchorSubmenuRows`, which re-anchors an open submenu through the
**same** `FightSubmenuLayout.RowY` the build used. v1 kept a second copy of
those constants in Core and its design preview drew rows at 8-slot positions.

### Tests

`FightSessionTests`, `TurnRiderTests`, `EnemyAiTests`, `EnemyIntentTests`,
`SkillDispatchTests`, `FightTalentTests`, `FightRewardsTests`,
`DamagePipelineTests`, `CombatBeatTests`, `FightScreenTests`,
`FightCapacityPinTests` — all EditMode, all sub-second.

### The runtime view (steps 11-13)

`FightMenuState` and `FightHudModel` are **Domain**, which is the one real
departure from the plan and the reason it is worth naming. v1 kept both on the
MonoBehaviour, so "does BACK out of a target land on the right depth" and "what
does this row cost" could only be checked by loading a scene and clicking. They
are plain logic; only the PAINTING needs Unity.

| File | What it owns |
|---|---|
| `Domain/.../FightMenuState.cs` | the five menu edges, the selection, the mana preview |
| `Domain/.../FightHudModel.cs` | submenu rows, the detail panel, the breadcrumb, the standing count |
| `Core/FightController.Hud.cs` | painting, and nothing else |
| `Core/FightController.Input.cs` | clicks in, session commands out; `CanAct` asked in ONE place |
| `Core/FightBeatPlayer.cs` | playback, paint-first-then-move, `Flush` reclaims |
| `Core/DamagePopup.cs` | the rise-and-fade, with `Reclaim` |
| `Core/StageHitFlash.cs` | the white silhouette, over `Resources/Shaders/UIHitFlash.shader` |

Two measurements the plan said to make rather than predict, both now made --
and the first one found a defect that had made every previous visual judgement
in this project worthless.

**Post-processing was never running.** `ScriptableObject.CreateInstance<Renderer2DData>()`
leaves `m_PostProcessData` null; only the Assets > Create path assigns the
package default. A renderer with no post-processing shaders is a legitimate
configuration, so URP warned about nothing -- it simply skipped bloom, vignette
and colour grading. The symptom was indistinguishable from success: screenshots
rendered, the Volume profile held the tuned values, and nothing looked broken.

It was caught by a measurement designed so it *could* fail: capture the party
plate with the Volume on and again with it off. The two came back byte-identical
(contrast 10.45:1 both ways, 100% brightness retained), which is impossible if
grading is running. `PipelineBuilder.EnsurePostProcessData` now assigns it, on
both the fresh-build and already-exists paths.

Worth naming as a method, not just a fix: **the earlier version of this
measurement compared the party plate against mid-screen and reported the plate
was darker.** That is true, and says nothing about the vignette, because those
are different content. A dark plate on a bright forest is dark with no
post-processing at all -- which is exactly the situation that turned out to be
the case. Only same-pixels, one-variable-changed could have caught it.

**Legibility survives, now that grading actually happens.** Measured on the
party plate, ungraded against graded:

| | ungraded | graded | |
|---|---|---|---|
| plate mean luminance | 0.1455 | 0.1190 | 81.8% retained |
| HP readout contrast | 11.84:1 | 12.03:1 | WCAG AA is 4.5:1 |

**No second root-level Overlay canvas is needed** -- the risky mitigation stays
unbuilt. `PostProcessingLegibilityTests` pins both numbers, so a future vignette
tweak that crosses either threshold fails rather than merely looking worse.

**The hit-flash shader survives URP.** `HitFlashPixelTests` renders a
deliberately dark half-opaque sprite through `CanvasCapture` and asserts white
where the sprite is opaque and clear where it is not. It passes against a real
graphics device, so the UIEffect fill-mode fallback is not needed.

All three are graphics-gated and self-skip under the headless commit gate. Run
them with `tools/graphics_tests.ps1`, which drives the isolated runner copy --
and note it needs the Unity editor CLOSED, since the licensing client will not
hand a second instance a token while the editor holds one.

### The stage (`FightController.StageVisuals.cs`)

Actors, poses and grounding. Ported with its two hard-won rules intact, both of
which were playtest bugs that read as art problems:

- **The ground line comes from the manifest, never from measuring alpha.**
  Delivered art disagrees about where feet sit inside the canvas, per FRAME.
  Pinning the raw canvas to the floor made the golem jump 52px going
  idle -> attack -- the "golem flies upwards in its attack" report. The runtime
  used to scan for the lowest opaque pixel, which found the golem's earth spike
  and Shawn's staff instead of their feet and hoisted both into the air. One
  authored value per actor; `StanceManifestValidationTests` fails if it stops
  matching the pixels, which is the case a measurement can never report because
  it just believes whatever it finds.
- **The shadow's X is still measured, and only from the idle frame**, so a pose
  that swings an arm out cannot drag the ring sideways. Horizontal centring has
  never caused a bug -- an arm's width moves a ring a few pixels, where a
  mistaken floor moves a whole creature off the stage.

`FightBeatPlayer` drives it: each beat applies the poses the session recorded (to
every combatant it names, not just the actor), moves the actor per its
`StageApproach` (`Hold`/`Lunge`/`Close`/`Charge` — a charge crosses most of the
way and arrives on the impact frame so the bump lands with the blow), flashes the
target if something landed, then returns everyone to idle so a pose belongs to the
blow that caused it. The defeated stay defeated -- that is read from `IsAlive`, not
from the beat. A skill authors its approach; a monster's plain attack authors one
too via `attackApproach` on the enemy entry (both parse through
`StageApproaches.Parse`). The idle's continuous `BreathCurve` scale and its
per-actor `endHold` peak dwell (a beat held longer at the top of the ping-pong)
are both driven from `.StageVisuals`'s idle stepper.

Party art is a **parallel map** (`BindPartyArt`), deliberately not part of
`PlayerKit`. The kit is what combat needs and a sprite folder is not that; v1
kept both in one `_playerOwners` dictionary, which is a large part of why its
combat logic could not leave the controller.

`FightStageVisualTests` covers the view side; the rules themselves already had
EditMode tests.

### Spell VFX (`SpellVfxPlayer` + `FightController.SpellVfx.cs`)

One Image, re-pointed frame by frame. No Animator: that would put the timing in
an asset instead of beside the spell that owns it, and `vfxSeconds` is authored
in skills.json next to the damage, which is where someone tuning the spell is
looking.

Almost all of the controller half is one question -- **where is the bottom of
the effect** -- and each answer came from an effect erupting somewhere
anatomically wrong:

1. **Aim at the SLOT, not the sprite Image.** The Image is the art's raw canvas
   and its bottom edge is wherever the sheet was cut; the slot's bottom edge is
   the stage's ground line. Reading the Image put a strike below the feet by
   whatever padding the sheet carried -- and a *moving* amount, since the
   grounding offset changes per frame.
2. **Correct the letterbox.** The box is square and the art is not always;
   `preserveAspect` fits art inside it, so a wide sheet floats with dead space
   beneath. Shawn's spells are 512x512 and hid this completely. Boulder Slam is
   598x433, renders 380x275 in a 380 box, and its ground spike erupted around the
   target's midriff.
3. **Correct the art's own bottom margin, at full extent.** The margin is
   animation -- a bolt strikes to its canvas floor and its afterglow retracts
   upward -- so the fix is not a per-frame correction (which would drag the effect
   down the screen as it faded) but the MINIMUM margin across the sheet. A blank
   wind-up frame reports "unmeasurable" rather than 0, because claiming the effect
   reaches the floor before it has appeared is confidently wrong.

**`PreSnapshot` finally does its job.** Playback now paints what stood BEFORE the
blow when a beat opens, waits `VfxSeconds * ImpactFraction`, then lands the
after-state, the flash and the floating number together. Zero delay for a plain
swing, so all of it is invisible for the overwhelming majority of beats. Until
now both snapshots existed and only one was ever painted, which showed a spell's
damage before the bolt had left the ceiling.

### Playable (`FightEncounterAdapter`, `FightBootstrap`, frame stepping)

**The adapter is the seam the whole decomposition exists for.** Above it is
engine-free Domain that knows nothing about ScriptableObjects; below it is Core
reading Resources. `EnemyDefinition -> ResolvedEnemy` and
`SkillDefinition -> ResolvedSkill` are both mechanical, field for field, because
the Resolved types were designed as the shape the definitions already had.

One rule worth naming, applied in both conversions: **`hasStatus` is the
authoring gate, and it is not the same question as "is a status type set".**
`appliesStatus` is a plain enum with a valid zero value, so every definition has
one whether anyone meant it or not. Reading the flag is what stops every monster
in the game inflicting the first entry.

`FightBootstrap` starts a fight when the scene opens, and takes **one of two
paths** depending on whether a descent is happening.

*In a run*, `BuildRoomFight` asks `RunEncounter` what this room contains: the
whole surviving squad (`EncounterRoll.FieldableParty`, which drops anyone
recorded at 0 HP) against enemies rolled by `EncounterRoll.Roll` from a stream
keyed to `(runSeed, Fight, step, currentNodeId)`. Position keys the stream, not
a fight counter, so the same room fields the same monsters across a quit and
reload rather than rerolling for an easier draw. Boss rooms field the run's
declared `bossEnemyId`; elite rooms field two; ordinary rooms one or two from
the non-boss pool.

*With no run*, `BuildPlaceholderFight` still picks the first character and first
three monsters **that have battle art**. That path is the tooling's, not the
game's: opening the Fight scene directly has to produce a stage that looks the
same every time or `screenshot.ps1` cannot compare captures. (Filtering on art
also sidesteps `sortOrder` being unset across all of enemies.json -- CLAUDE.md
gotcha 4. The in-run path deliberately does **not** filter on art, or
enemies.json would decide the encounter table by which sheets were finished.)

### Rooms that are not fights (`RoomResolution`, `RoomResolver`)

`MapController.Walk.cs`'s `Arrive` sends fight rooms to the Fight scene and
hands everything else to `RoomResolver.Resolve`, which applies what
`RoomResolution.Resolve` decided: treasure pays 15-30 gold from a `Treasure`
stream keyed to the node (so re-entering finds the same stash), rest restores
the whole squad to `EffectiveStats().maxHealth`, and Shop/Event/ItemSpawn/
Unknown clear while **saying** they are unbuilt.

That last part is a rule, not a courtesy: a room that does nothing without
explaining itself reads as a bug, and v2 had regressed to exactly that. The map
shows the line through `MapRoomMessageLabel`, and `RoomResolver.TryMessage`
hands back a `UiString` plus args rather than finished text because
`UiKitLintTests` fails the build on a direct `.text` assignment.

Party HP persists in `RunSnapshot.currentHealth`, written by
`RunEncounter.WriteBackHealth` on the way out of every fight and read by
`ApplyStartingHealth` on the way into the next. Nothing wrote it before; every
room opened at full health, which also left Rest with nothing to restore.

> The screen table at the top of this file still names v1's
> `SceneBuilder.Map.cs`, `DescentMapView.cs` and `GameplayManager.cs`, none of
> which exist in v2 -- screens are declared in `Domain/UiKit/Screens/` and wired
> in `ScreenRegistry.cs`. Recorded rather than rewritten here because it is a
> whole-table job, not a line.

**Frame stepping** closes the last gap. A beat now walks its actor through the
sheet: frames `[0, impact)` before the blow, `[impact, count)` after, both at the
sheet's own authored pace. The alternative -- stretching the remaining frames to
fill the hold -- is what v1 did and is visibly wrong: the Giant Rat's six-frame
swing ran its first three at 0.08s and its last three at 0.15s, one animation
changing speed halfway through, which is the "feels a bit blocky" report.
`ImpactFrame` is 1-based, matching the `vfxImpactFrame` convention the content
files already use.

`FightPlayableTests` opens the scene the way a player does and asserts a fight
came out of authored content: real sprites rather than fallback plates, the
manifest's ground line applied, a turn that can be taken end to end, and an actor
that actually advances past frame 0 mid-swing (sampled DURING the beat -- the
round ends back on idle at frame 0, so checking afterwards would pass either way).

### The loop closes (outcome, navigation, the run, the map)

**Outcome.** `VictoryRewards` and its 19 tests already existed; nothing called
them. A payout now settles on every path a fight can end -- including the enemy
loop's own exit, which is how a DEFEAT ends and never passes through the riders.
Null until the fight is over, because "not settled yet" and "settled at nothing"
are different answers.

**Navigation** holds the scene names once. `LoadOverride` lets a test assert
where a button *would* go without tearing down the scene its assertions are
about -- four existing save-slot tests broke the moment picking a slot started
loading the hub.

**`RunManager`** is static, not a MonoBehaviour: a run outlives every scene it
passes through, and the state lives in the SAVE, so this is a thin accessor
rather than a second copy that could disagree with disk. The map is
**regenerated from the seed**, never serialised -- a serialised map is a second
representation that drifts from the generator that made it.

The correction that mattered: **a leg is not a run.** A leg is 8 steps and bosses
fall every 16, so leg 0 ends at an ELITE. Treating "no choices left" as the end
of the run would have stopped every descent there.

**The map screen** is the first real `Ui.Pool` outside the fight, sized from
`MapLayout.Capacity` = columns x max width: every position a leg *could* use.
Which are occupied is runtime; where they sit is not, so the pool gets real
audited coordinates. The controller re-anchors each column to its true width
through the same `MapLayout.RowY` the tree placed it with, so a two-room column
sits centred rather than leaving a gap where the third would have been.

The audit refused the first version with 232 problems across four frames, all
legitimate: the pool container spans the panel, and the caption and you-are-here
pip deliberately sit outside their node. Three `AllowOverlap`/`AllowOverflow`
declarations with reasons, not a suppression.

Playable end to end: **MainMenu -> slot -> Hub -> gate -> Map -> room -> Fight ->
Continue -> Map**, with gold banked, rooms marked cleared, legs advancing, and a
loss ending the run.

### The Divine Principality (the hub)

Five equal rectangles on a nebula became a staged place: the descent gate
largest and centre on the ground, four annexes floating over the void at four
depths, each smaller and higher than the last.

| File | What it owns |
|---|---|
| `Domain/UiKit/HubAnchors.cs` | depth, lateral, size, and where each nameplate hangs |
| `Domain/UiKit/Screens/HubScreen.cs` | the tree, with a `HubWorld` wrapper splitting world from chrome |
| `Domain/UiKit/Screens/HubAmbience.cs` | where every light sits — **data, no new curves** |
| `Core/HubController.cs` | the wallet, the gate caption, dimming the unbuilt |
| `Core/HubBuildingLooper.cs` | revives the talents tree's f1/f2, dead since the art landed |

Three decisions worth knowing:

- **`HubAnchors` lives under `UiKit/`, not `Stage/`.** The `Stage/` path maps to
  the combat and art test areas, so anchors there would run the wrong suites and
  miss their own. A `$PathAreas` row now sends hub-tree edits to the hub suite.
- **The world is a FIXED 1920×1080 stage, not a stretch.** The composition is
  authored in reference coordinates; stretched to fit, a shorter window pushed
  the far buildings out of their own parent. Fixed, it crops like its own
  full-bleed background does.
- **Nameplates are placed from MEASURED art bounds.** Every sheet pads
  differently — 0.944 tree, 0.908 stall, 0.944 book, 0.916 plot, 0.885 gate —
  so "half the box" put one caption under the book, one across the stall and one
  above the shrine. The fraction rides on the `Plot` so a building cannot be
  handed another's padding.

The audit caught three real defects here: near buildings stealing far buildings'
clicks (uGUI gives the later sibling the press), the world escaping its own
frame, and the gate caption filling the arch's void.

### Ambience curves are Domain now

All six animators kept their components in Core and moved their *arithmetic* to
`Domain/Ambience/` (`FlickerCurve`, `AmbienceCurves`). They were written as pure
seams with comments saying a test could pin them — and the EditMode suite is
Domain-only, so none ever could. 27 tests now cover them, including the ones
that matter: Ken Burns never scaling below 1 (it would show bare camera colour
down the sides), three curves degrading instead of dividing by a zero period,
and mote alpha clamping rather than inverting a colour.

`FlickerCurve` is data — a centre plus a list of `(amplitude, rate, phase)`
terms — so retuning a flame is editing numbers, not arithmetic.

### Still to come

**The hub's look is unverified.** `tools/screenshots/HubPanel.png` has not
changed across three revisions of the tree, including the nameplate fix. The
built scene demonstrably contains `HubWorld`, `HubAmbience` and
`StartRunGateCaption`, and the suite is green — so the tree is right, but the
render is not showing it. Chase that before trusting anything visual here.
`ScreenshotTool.Capture` rendering the first canvas rather than the named panel
is one candidate.

Also open: the forecourt painting (the hub still stands on the placeholder
nebula), a real Relics building so `empty_plot` can go back to meaning "future
construction", and the five sub-screens themselves — Talents, Store, Equipment,
Character Sheet, Relics — which is why four of the hub's five buildings are
dimmed and unpressable.
