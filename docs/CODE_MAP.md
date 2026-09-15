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
(see "The UI construction layer" below).

| Screen | Screen tree | Controller | Content |
|---|---|---|---|
| Main Menu / Save Slots | `Domain/UiKit/Screens/MainMenuScreen.cs` + `MainMenuAmbience.cs` | `MainMenuController.cs`, `SaveSlotController.cs` | `characters.json` |
| Run Map | `Domain/UiKit/Screens/MapScreen.cs` | `MapController.cs` + `MapController.Walk.cs` | `enemies.json` (room pools) |
| Fight (combat) | `Domain/UiKit/Screens/FightScreen.cs` | `FightController.cs` (root) + its 4 parts, see below | `skills.json`, `spells.json`, `enemies.json`, `weapons.json` |
| Rewards / Item Choice ("The Reckoning") | `Domain/UiKit/Screens/ReckoningScreen.cs` (wired as `fight.reckoning` inside the Fight scene via `ScreenRegistry.cs`) | `ReckoningController.cs` | `items.json`, `itemsets.json` |
| Character Dossier (sheet + bag + paperdoll) | `Domain/UiKit/Screens/CharacterDossierScreen.cs` (wired as a System Menu tab via `ScreenRegistry.cs`) | `CharacterDossierController.cs` | `characters.json`, `items.json` |
| Shop / Store | `Domain/UiKit/Screens/ShopScreen.cs` (nested panel inside the Map scene, wired via `ScreenRegistry.cs`; laid out to `docs/handoffs/shop_v2/Shop Screen v2.dc.html`) | `ShopController.cs` | `items.json`, `relics.json`, `skills.json` |
| Hub | `Domain/UiKit/Screens/HubScreen.cs` + `HubAmbience.cs` | `HubController.cs`, `HubBuildingLooper.cs` | — |
| Talents | `Domain/UiKit/Screens/TalentScreen.cs` + `Domain/UiKit/ConstellationLayout.cs` | `TalentController.cs` + `.Motion.cs` | `talents.json` |
| Relics (start-of-run draft) | `Domain/UiKit/Screens/RelicDraftScreen.cs` (wired into Hub via `ScreenRegistry.cs`) | `RelicDraftController.cs` | `relics.json` |
| System Menu (Pause replacement — tabs: Dossier/RewardTrack/Party/Options/RunStats/Exits) | `Domain/UiKit/Screens/SystemMenuScreen.cs` (wired into Fight/Map/Hub via `ScreenRegistry.cs`) | `SystemMenuController.cs` | — |
| Party (roster & formation, a System Menu tab) | `Domain/UiKit/Screens/PartyScreen.cs` + `Domain/UiKit/PartyLayout.cs`, model in `Domain/Party/` (`PartyFormation.cs`, `PartyMode.cs`, `PartyOutcome.cs`, `PartyCardState.cs`, `PartyRosterEntry.cs`, `PartySeat.cs`, `PartySelectionSource.cs`), wired via `ScreenRegistry.cs`'s `WireParty`; handoff + gap audit at `docs/handoffs/party_screen/` | `PartyController.cs`, `PartyDragSource.cs`, `PartyToast.cs` | `characters.json` |
| Options (a System Menu tab — Audio/Display two-column-0, Gameplay alone in column 1 as of `docs/PLAN_BATTLE_SPEED.md`) | `Domain/UiKit/Screens/OptionsScreen.cs` + `Domain/UiKit/OptionsLayout.cs`, rows declared in `Domain/UiKit/OptionRows.cs`, wired via `ScreenRegistry.cs`'s `WireOptions` | `Core/OptionsController.cs`, backed by `Core/GameSettings.cs` (PlayerPrefs; the one row's setting that reaches gameplay — `SetBattleSpeed` — is `GameSettings.BattleSpeed`, an in-fight-only preset display number consumed by `FightBeatPlayer.PlayerSpeedSource`) | — |

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
`NodeRef` · `Ui` (the factories — `ContainerKey(theme, ratio)` is the one place
the kit's `container_<theme>_<ratio>` path format lives; it went callerless on
2026-09-10 when the fight HUD's party plate stopped being a themed 2:1
container, and is kept because it is the format's only statement.
`RimEdgesOf(box)` hands back an `OutlineBox`'s four `{name}Rim{Top,Bottom,Left,
Right}` children in that order, throwing on anything but four — so a caller
that recolours a rim at runtime does not restate `Rim`'s naming convention) ·
`PcTheme`/`PcColours` (what each `ButtonTheme` LOOKS like once it reaches a
fight-HUD card: a rim hex at 0.70 alpha and a name hex, six explicit arms and
`default: throw`. This is the other half of `ResolvedCharacter.PlateTheme` —
content says which theme, this says what the theme is) ·
`PcPlateArt` (the MEASURED SHAPE of a per-PC fight-HUD plate — aspect, safe
interior, the fraction of the right end the embossed head owns, the acting-glow
decal's key and pad, and the three baked defaults an unrefreshed scene shows.
Every number pasted from `tools/normalize_pc_plates.py` and re-derived off the
committed PNGs by `PcPlateArtTests`. WHICH plate a character wears is not here:
that is `characters.json`'s `plateArt`, loaded at runtime by
`Core/PcPlateSprites.cs`) ·
`IdentityMetals` (what SILVER and GOLD are, once, for every surface that draws
progression v2's levels 31-40 -- the rim decal's tint, the emboss laid ON the
plate art, the portrait frame round a roster card's art slot, and the identity
line's ink. Same argument `PcTheme` makes for the six per-character colours, at
the scale of two metals; WHICH of them a character has collected is
`Core/CharacterIdentity.LookFor`) · `UiString` ·
`UiStrings` ·
`UiSolver` ·
`SolvedNode` · `UiAudit` · `UiAuditError` · `UiFrames` · `FightSubmenuLayout`
(`VisibleBottomLine` — `CommandBottom` plus the verb row's own bottom PAINT
pad, not its rect — is what the submenu frame and the party plate both
bottom-anchor against, because every `Processed/` kit PNG carries a
transparent halo outside its painted border and a rect-to-rect flush reads as
misaligned even when the rects agree exactly) ·
`ButtonPlateArt` (the button-plate kit's shape/selection rule — since the
2026-09-07 regeneration all four shapes are at true nominal aspect, so Legacy
and ThreeByOne both read 3.0 and are byte-identical files, and `ShapeFor`'s
tie between them goes to Legacy; `VisiblePad(shape)` is the measured
paint-vs-rect fraction on each edge) ·
`ContainerArt` (the container/flag-banner kit's aspect/inset, one
`ContainerSpec` table keyed on kind+ratio — `ContainerRatio` covers
ThreeByFour/NineBySixteen/ThreeByTwo/TwoByOne/FiveByOne, the last of which is
container-only and has no caller yet; `VisiblePad(kind, ratio)` is
`ButtonPlateArt.VisiblePad`'s counterpart, both fed by `tools/measure_ui_kit.py`
— it scans every `Processed/` PNG per edge at alpha thresholds 8/32/128,
asserts the six themes agree within 1px at 32, and prints a C#-pasteable
threshold-32 block; four edges of the 2026-09-07 delivery fail that assertion
for real, and both `ContainerArt` and `UiKitVisiblePadTests` name them) ·
`Typography` (the `TypographyRole` vocabulary and each role's
`TypographySpec`, including `ResolveSizeRange` — the explicit-literal-vs-
role-band autosize precedence `UiEmitter.ApplyTypography` calls into)

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
| `Combat/` | `Reach.cs` (`ReachKind` — `Any`/`Melee`/`ExplicitRanks` — plus a rank mask; the KIND is load-bearing, not just the mask: an authored front-only restriction, `Reach.FromContent`/`Reach.Ranks`, carries the same mask as `Melee` but is deliberately a different kind, because a relic that lifts the front-rank RULE — striking past a bodyguard — must not also unlock an authored aim restriction. `Reach` is a mutable struct on purpose: it lives inside `ResolvedSkill`, which is serialized onto a `ScriptableObject`, and Unity's serializer skips `readonly` fields — a `readonly Reach` would round-trip as `default` = `Any`, silently dropping every restriction while EditMode stayed green, since tests never cross the asset boundary); `CombatMath` (armour is diminishing returns via `Mitigate`, not a subtraction — see its header for the floor-one-boss-took-130-turns bug this replaced), `CombatEncounter`, `CombatantState`, damage/effectiveness formulas, plus the talent-rework additions: `TalentEffect` (the closed rule vocabulary a talent can grant), `TalentEffectSet` (a character's rules, flattened once per fight) and `Transformation`/`TransformGrant` (Black Ram Mode); the balance-pass status/gate primitives `Marks` (spend-on-a-later-hit debuff), `Fear` (Stunned+Vulnerable for a fixed duration), `FallingOffStacks` (a stack pile where every stack carries its own expiry), `Session/CombatLocks` (a once-per-X gate keyed by an arbitrary string) and `Session/ConvergenceGate` (whether a relic wanting "a party member has a convergence ability" should be offered); `AllyTargeting` (the one predicate per `SkillEffect` saying who on the caster's own side a single-ally cast may be aimed at -- read by the party plates, by `CastSkill`'s refusal and by the bot alike, so the picker and the cast cannot disagree; AUDIT #147); `Session/FightSession.RelicMechanics.cs` (the non-numeric relic mechanics from both balance passes — `NoteDeliberateMove` is the field-position half, fired only by `Move`, twice per call (the mover and the partner it displaced), separate from the turn-order sites that used to share its old name) |
| `Content/` | Raw/resolved content shapes + `*EntryResolver`s (validation) for every JSON-authored content type, plus the content enums they parse (`CharacterRole`, `RelicEffect`); `ContentDocAttribute` + `ContentSchema` (reflects over every `Raw*Entry` to generate `docs/CONTENT_SCHEMA.md` — see `ContentSchemaTests.cs`); `ResolvedCharacter.PlateTheme` (the character's IDENTITY COLOUR on every fight-HUD card that stands for them — rim and name, via `Domain/UiKit/PcTheme.cs`; `characters.json`'s `plateTheme` parsed case-insensitively against `UiKit.ButtonTheme` by `CharacterEntryResolver.TryResolveOne`, REQUIRED since 2026-09-10: an empty or unknown value refuses the build naming the character and the six valid theme names, because every valid value is some other character's colour; pinned by `CharacterEntryResolverTests`); the RESOURCE POOL type (`pools.json` -> `RawPoolEntry`/`PoolEntryResolver`/`ResolvedPool`, the three closed vocabularies in `PoolRules.cs`, and `Core/Content/PoolDefinition.cs` beside it) -- one pool is one clamped counter with a capacity, three gain triggers and a decay, so mana, a signature resource and Bjorn's Fury are the same runtime thing with different authored numbers rather than parallel systems; `capacityRule` is what separates "the authored number is a base every Max Mana source adds to" (mana) from "the authored number is the whole capacity from every source" (a flat 0..100 bar), and a character names its pool with `primaryPoolId`, defaulted to `"mana"` and refused unless `pools.json` defines it (`ContentBuilder` builds pools BEFORE characters and hands the ids down, the `RelicEntryResolver`/`achievementIds` shape); `PoolPrecedence.cs` is the RULE those two answers turn into (which sources a capacity and an income listen to), with `ContentDatabase.BuildPrimaryPool` summing the character-shaped terms it is fed. Phase B: every combatant's `CombatantState.PrimaryPool` is built from a row, `Domain/Combat/ResourcePool.cs` (was `SignatureResource.cs`) is its runtime, and `GameplayConstants.DefaultMaxMana` is gone -- `pools.json` is the only place the number 30 is written |
| `DebugMenu/` | `DebugItem`, one row in the debug item picker — `Kind` is an int rather than `ItemKind` because `ItemKind` is a Core content enum this Domain type cannot see; the controller translates where it already has an `ItemDefinition` in hand |
| `Dungeon/` | `DifficultyCurve`, room/map generation logic |
| `Economy/` | `Wallet`, `CurrencyType` |
| `Equipment/` | `EquipmentSlot(s)`, `EquipmentLoadout` |
| `Glossary/` | `GlossaryCategory`, the rail order the glossary screen builds one button per — appending a value adds a button and nothing else, a new category is a value here plus a case in the Core adapter, never a new screen |
| `Inventory/` | `InventoryEntry` (one stack of a held item, keyed by `(itemId, plus, modifierIds, riftTier)` — not by id alone, so two differently-rolled copies never silently merge) and `InventoryOps` (add/count/remove over a `List<InventoryEntry>` in one place, shared by `RunState.inventory` and `SaveData.stockpiledItems` so the stash uses the identical rules rather than a second copy) |
| `Party/` | The Party screen's model, engine-free: `PartyFormation` (the 3-seat state machine — click-to-select-then-place, seats are mechanical since squad index 0 is the front rank, `PartyFormation.Drop` is the drag entry point a later package uses), `PartyMode` (Camp/Run/ViewOnly), `PartyOutcome`/`PartyToastKind` (what a command did, as data — display copy lives in `UiStrings`), `PartyCardState`/`PartySeatBadge`, `PartyRosterEntry` (id/name/`HasArt`, supplied by the caller — Domain cannot see `Resources`), `PartySeat` (Front/Middle/Rear constants), `PartySelectionSource` (Roster vs. a seat index) |
| `Progression/` | `CarriedHealth` (what happens to current health when the maximum it is measured against moves under it — equipping +20 max health at 50/100 must not silently become 50/120), `LevelCurve` (what a level costs, AND how many fights the next level is -- `FightsToGo` divides the remainder by one average NORMAL fight at a depth the caller supplies, which is the run's own step in a run and 0 in the hub; kept in Domain rather than on `Character` so EditMode can pin it without Core), `RewardTrack`/`RewardTrackDefinition` (a reward track materialised for one character — milestones plus the filler mix an author, or `Default`, described), `RewardTrackNames` (display copy for a `TrackReward`, beside the table for the same reason as `RelicRarityNames`), `TrackNodeState` (the four states a node reads as, out of two integers — `level` and `claimedTrackLevel` — the distinction between a reward REACHED and one COLLECTED) |
| `Relics/` | `RelicLoadout` (party-wide relic ownership/assignment) |
| `Rewards/` | `CombatReward`, `CharacterReward`, offer tables |
| `Rng/` | `SeededRandom` (built, not yet wired — see `AUDIT.md`) |
| `Stage/` | Stage-side/depth/layout pure geometry, `SpriteFacing`, `StanceManifest` (ground line + breath + hover, one row per actor — no per-stance timing, because a stance is one drawing; `groundLineSource` says whether the slicer or a person owns the number, absent meaning authored, and `StanceManifestValidationTests` re-measures the committed stills against it), `BreathCurve` (the continuous scale transform every idle figure gets, and the only thing that moves a figure between blows — see its own header), `HoverCurve` + `HoverSpec` (the altitude an airborne actor rides at and bobs around, authored per actor as the manifest's `hover` block; Odette is the one flyer) |
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
`RunManager.cs` (run lifecycle) and `Navigation.cs` (screen switching —
scene-name constants in one place rather than string literals at each call
site; deliberately thin, not a state machine) now cover what this row used
to credit to a `GameplayManager.cs` that no longer exists in the tree,
`ItemIcons.cs` (shared id→sprite lookup, also used for portraits — see
`docs/CODE_STANDARDS.md` §2), `PreviewFight.cs` (everything
`tools/preview.ps1 -Spell`/`-Character` decides about one throwaway fight —
who casts it, how many enemies it needs to be visible against, what has to be
waived and said out loud, and which setups are refused by name rather than
approximated; read by BOTH routes, the Editor one through `FightBootstrap`'s
`DevForced*` keys and the headless one through `PreviewCaptureTests`, so a
photograph is of the fight `-Launch` would field), `RarityColors.cs`, VFX primitives (`RadialGlowImage`,
`BeaconPulse`, `SolidCircleImage`, `SpellVfxPlayer`), the ambient-motion
primitives (`StarTwinkle`, `LanternFlicker`, `SlowDrift`, `MoteDrift`,
`KenBurnsDrift` — see `docs/CODE_STANDARDS.md` §2), and
`FrameSequenceLoader.cs` (the one f0..fN Resources probe, behind
`SpellVfxPlayer` and `HubBuildingAnimator` — spell and ambient effects still
ship frame sequences; actor stances do not), and
`ThemedButtonState.cs` (what `FightController.RefreshVerbs` distinguishes
about a themed root's runtime state — hover/press/disabled — separate from
the plate art `Ui.ApplyTheme` already baked in at build time).

Audio is the one family that does NOT go through `ContentDatabase`, because
`MusicController` and `SoundController` self-bootstrap before any scene loads
and therefore before it exists. Its two config tables are read by their own
loaders instead, both the same shape (Resources path, `JsonUtility`, cache,
`Reset()` seam, every failure degrading rather than throwing): `AudioLevels.cs`
reads `audio_levels.json` (per-clip loudness correction) and
`Domain/Audio/MusicLayerResolver.cs` reads `music_layers.json` (which stems
make up a floor's song, and which of them each intensity plays) — renamed
from `MusicLayers.cs` and moved into `Domain/Audio/` alongside `MusicClock.cs`.

## Editor & tools map

`Assets/_Project/Scripts/Editor/` (`PrincesPalace.Editor`, one asmdef):
`SceneBuilder.cs` + `SceneBuilder/` (above), `ContentBuilder.cs` (generates
every ScriptableObject from `ContentData/*.json`; nine of the twelve types go
through one generic `Build<TRaw, TResolved, TDef>` that reads the file, calls
the type's resolver and writes `asset.SetData(record)` -- items, weapons and item
sets stay bespoke because all three expand into `ItemDefinition` and need their
own collision checks and sort offsets), `ScreenshotTool.cs`
(headless panel capture, `KnownPanels` table — keep `tools/screenshot.ps1`'s
usage text in sync with it), `EnemySpriteImportPostprocessor.cs`,
`StanceSpriteImporter.cs` (forces Sprite import under `Resources/Enemies`,
`Resources/Characters` AND `Resources/Spells` — anything runtime-loaded as a
Sprite must be listed there or it silently loads as null),
`PreviewRequestWatcher.cs` (the open-Editor half of `tools/preview.ps1`:
Unity locks a project's `Library` exclusively, so batchmode cannot run while
the Editor is open — this `[InitializeOnLoad]` watcher polls
`Temp/pp_request.json` on the update tick and answers on
`Temp/pp_result.json`. A thin shell: the documents themselves -- which
actions exist, which id each needs, what a result carries, the
busy/ok/failed states -- live in `Domain/Preview/PreviewProtocol.cs`, where
`PreviewProtocolTests` can reach them without an Editor. What stays here is
the poll, the two files, the two questions about Editor state, and the four
pieces of work; it only ever writes `SessionState`, never a save),
`QuickFightMenu.cs` (one menu click into a fight against the last
previewed id, or against nothing in particular when the caller passes none),
`SceneBuilder/SceneBuilder.Typography.cs` (`FontFor`/`MaterialFor`: resolves
a `TypographyRole` to the generated font/material assets `TmpBootstrap.
Typography.cs` produced, null on a not-yet-generated asset rather than
throwing) and `SceneBuilder/TmpBootstrap.Typography.cs` (generates the
static-weight SDF font assets and per-role TMP material presets themselves).

`tools/` (all PowerShell/Python, see `docs/WORKFLOW.md` §8 for when to use
which):
- `preview.ps1` — from an authored row to something you can look at, and the
  everyday loop. `-Build` regenerates content; `-Enemy <id>` photographs a
  mob's stances and one frame per turn of its kit; `-Spell <id>` casts one
  spell and photographs the frame before impact, the impact and the one after
  (`FightController.ImpactDelayFor` decides when, the damage popup is logged
  beside it as an independent reading); `-Character <id>` fields them alone
  and photographs the map figure, the dossier portrait and the fight stage.
  `-Launch` plays any of them in the Editor instead. It looks at
  `Temp\UnityLockfile` AND the process table and picks batchmode or the
  open-Editor watcher itself — which door is not the author's problem.
  **Never runs the full suite**; verification stays `run_tests_parallel.ps1`
- `build_content.ps1` — one batchmode Unity against **main**,
  `-executeMethod GenerationRun.RunAll -ppSteps content`, in place. No runner
  copies and no sync-back to get wrong. Refuses while the Editor holds the
  project and says which route to use instead; a lockfile with no Unity
  behind it is recognised as debris and cleared
- `unity_lock.ps1` — dot-sourced by both of the above, never run directly.
  "Is a Unity Editor actually holding this project" answered from the process
  table rather than from a zero-byte file that outlives a crash
- `content_schema.ps1` — regenerates `docs/CONTENT_SCHEMA.md` from the
  `Raw*Entry` types themselves, so the per-field reference cannot drift from
  the fields. Run it whenever a content field is added or its `[ContentDoc]`
  changes
- `run_tests_parallel.ps1` — the "before committing" runner (~90-100s), two
  isolated copies in parallel, `-BuildContent`/`-BuildScenes`/`-SkipSync`,
  the `Assert-GuidsMatch` hardening (see `AUDIT.md` #24), and a structural
  gate that refuses to run at all if a test file sits outside an area folder
  or its class was never discovered (see `test_areas.ps1` below)
- `test.ps1` — the fast edit-run-edit loop (~12s for one class, more for a
  broad area; `-Changed` maps uncommitted files to the areas/classes they
  affect)
- `test_areas.ps1` — single source of truth for test-class discovery, the
  structural gate, and the placement guide for the seven areas
  (`combat`/`hub`/`content`/`run`/`ui`/`art`/`rng`), which are folders under
  `Tests/EditMode/` and `Tests/PlayMode/`, not name patterns. Shared by both
  scripts above. Also holds `$PathAreas`, which maps *production* paths to
  areas for `-Changed`
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
  `Resources/Characters/<id>`), since both resolve through one runtime path;
  it writes `Art/<Enemies|Characters>/<id>/recipe.json` (the full argv, the
  tool hash, the library versions, the measured ground line) and `--recipe`
  replays it. It also owns `Resources/StanceManifest.json`'s `groundLine` for
  any actor whose entry is absent or says `groundLineSource: "slicer"`, and
  leaves an `authored` one alone with the delta printed.
  `slice_spell_sheet.py`'s recipes live under `Art/Sheets/recipes/<id>.json`
  — the recipe says how frames are PRODUCED, the skill's `vfx` block says how
  they PLAY, and `SpellVfxRecipeDriftTests` checks only the arithmetic
  between them
- `sheet_slicing.py` — cell-cutting geometry shared by the two grid slicers
  (not run directly)
- `actor_stance_qa.py` — visual QA contact sheets for actor stance STILLS,
  rendered offline from the committed PNGs without booting Unity
  (`screenshot.ps1 -Runtime` covers animation that only exists in-engine);
  each cell shows the canvas bounds, the ground-line guide, the alpha
  centroid and a scale caption; a folder still holding old per-frame
  sequences is skipped with a notice rather than misread as a stance
- `capture_strip.py` — assembles one PlayMode frame-series capture into a
  stamped contact strip and a real-time GIF, plus a `before_vs_after.png`
  once both labels exist; frame discovery is self-contained, inlined when
  `rig_clip_qa.py` was retired
- `static_pilot_qa.ps1` — the static-combat pilot's one-command loop:
  `graphics_tests.ps1 -Filter StaticPilotStageCaptureTests -Label <label>`,
  copies the frames back from the runner copy, then runs `capture_strip.py`
- `make_contact_fx.py` — generates the melee contact effects procedurally
  rather than cutting them from a delivered sheet: `Resources/Vfx/slash_arc/`
  and `Resources/Vfx/impact_burst/` (six frames each, alpha from the luminance
  falloff, impact dead-centre because `FightController.PlayContactFx` applies
  no impact-point correction), plus `--audio` for the two synthesised
  **placeholder** one-shots the same cues play. Paths and durations live in
  `Core/ContactCues.cs`
- `trim_wav.py` — audio trimming utility

---

## Tests map

`Assets/_Project/Scripts/Tests/EditMode/` — Domain-only.
`Assets/_Project/Scripts/Tests/PlayMode/` — Core+Domain.

Both are split one folder per area: `Combat/`, `Hub/`, `Content/`, `Run/`,
`Ui/`, `Art/`, `Rng/`, plus `Shared/` for helpers with no tests of their own.
**The folder is the area** — that is the whole mechanism, and
`run_tests_parallel.ps1` refuses to run while a test file sits outside one.
`tools/test_areas.ps1`'s header is the placement guide; read it before adding
a file rather than guessing from the neighbours.

Class counts come from `tools/test.ps1 -List`, the live, authoritative source
— trust it over a file count, since a single file can hold more than one test
fixture.


## The Fight screen (v2)

Rebuilt across steps 0–10 of the Fight-screen plan. The shape is the whole
point, so it is worth stating once: **~34% of v1's `FightController` was never
about Unity**, and all of it now lives in Domain where it is tested in
milliseconds rather than by loading a scene.

### Domain — `Domain/Combat/Session/`

| File | What it owns |
|---|---|
| `FightSession.cs` | the session: kits, reach queries (`CanReachEnemy`/`CanReachAlly`/`EligibleTargets`/`EligibleAllies`), the plain attack, Move/`CanMove` (a solo party has nowhere to step and a rooted character cannot step at all — the never-empty-legal-actions guarantee moved to Attack because of this) |
| `FightSession.Beats.cs` | beat RECORDING, the retro-attach rule (AUDIT #13), stance and voice capture |
| `FightSession.Riders.cs` | Brave / Trample / Bloodlust, turn-start bookkeeping, victory resolution |
| `FightSession.Enemies.cs` | intents, the telegraph, the two skip paths, taunt redirection, the status rider |
| `FightSession.Skills.cs` | the fourteen-effect dispatch, role riders, the queue push |
| `FightSession.Talents.cs` | wool engines, wards, Shatter, Gifts, splash, the transform, Provoke |
| `FightSession.Cooldowns.cs` | skill cooldowns, counted in the caster's own turns rather than rounds |
| `FightSession.Items.cs` | using something out of the satchel |
| `FightSession.Ledger.cs` | damage and kill attribution, settling a death |
| `FightSession.Outcome.cs` | the payout on a win, what a loss says |
| `FightSession.Potency.cs` | the every-Nth-action bonus, and what "harder" is measured against |
| `FightSession.Relics.cs` | WHEN a relic gets to act — the hooks, in one place |
| `FightSession.RelicMechanics.cs` | WHAT each relic does — fourteen effects too specific for a shared table |
| `FightSession.SpeedBuffs.cs` | speed changes mid-fight, kept honest with the turn order |
| `DamagePipeline.cs` | the damage funnel, and the only place its composition order is stated |
| `VictoryRewards.cs` | the payout arithmetic (elite × depth), drop rolls, who earns what |
| `FightHudSpec.cs` | HUD capacities both the tree and the session read |
| `FightTuning.cs` | balance constants, out of the controller |
| `CombatBeat.cs` (`TurnOrder` is READ by the view — `FightBeatPlayer.PaintTurnOrder` hands each beat's queue to `FightController.PaintTurnOrder`, and `CombatBeat.QueueToShow` states the recorded-or-live rule `RefreshInitiative` paints by; before that it was recorded and read by nothing but its own tests, so the tracker painted live `UpcomingTurns` through a playback whose round had already finished. `Formation` records list ORDER — copied with `ToArray` at `CommitBeat`, not `BeginBeat`, because a `Move` writes the swap between the two — so the view, not `CombatEncounter.LivingRankOf`, decides who still occupies a rank; a formation of the living alone would compact the line the instant a kill lands, before the corpse's own death fade has started) / `Vitals.cs` / `VoiceLine.cs` / `CombatantKit.cs` | the engine-free beat vocabulary |

`Domain/Stage/FightStageAnchors.cs` holds the stage's pixel anchors, and
**formally supersedes** `StageLayout`'s header note that anchors stay in
SceneBuilder — that note predates screen trees living in Domain.
`StageSize` has widened 1200 → 1260 → 1500 → 1700, each time so the outermost
slot still fits inside the frame it is declared in
(`FightStageAnchorsTests.EveryStageSlotFitsInsideTheStageRect` is what keeps
finding it).

A SIDE'S LINE IS DATA, not a scalar tweak on one shared line:
`Domain/Stage/StageFormation.cs` is a `Near`/`Far` pair authored as
magnitudes out from stage centre, and `FightStageAnchors` holds one per side
(`Enemy` 300/-218 → 660/-125, `Party` 320/-218 → 810/-64, mirrored).
`PartyRetreat`, the single "+60 on the mirrored X" scalar the party's
endpoints used to be derived from, is **gone** — the two sides differ in
both axes and by different amounts, and one number could no longer say so.
The party's line is the steeper of the two because the HUD stands on its
half of the floor: the roster block occupies x −920..−540 up to y −161 and
draws over the stage, and a shared ground line put the middle party slot
inside it. It is also the wider of the two, because three similar bipeds
197-228px wide at the old 180px slot pitch overlapped by a third each and
read as one clump; 245 per step makes them touch instead, and 810 is capped
by the 4:3 canvas edge against the widest-left drawing the back slot can
hold (Shawn's idle, 126.5 stage px left of its own mark).
`SlotOffset` is still the one place a slot position is computed.

`Domain/Stage/StageStandOff.cs` answers the other half — where an attacker
stops. `FightBeatPlayer` used to travel a fixed FRACTION of the X gap
(0.70 lunge / 0.78 close / 0.86 charge), which left a residual that grew
with the distance and knew nothing about how far the attacker's own weapon
reached. It now arrives at a point derived from the two bodies: the target's
near opaque edge, backed off by the attacker's forward reach in the widest
drawing it will wear, plus a 12px gap (0 for a charge), on the target's
ground line. `StageActorAnimator.SpanForStance` measures the drawings (tight
sprite meshes, so `textureRectOffset` + `textureRect` is the opaque box);
`FightController.StageVisuals` hands it the art folder beside the Image.

### Domain — the screen tree

`Domain/UiKit/Screens/FightScreen.cs` declares the whole screen: two mirrored
stages, the initiative row, bark, enemy plates, the three PC plates, three
command columns, and the only two genuine `Ui.Pool` sites in the game (the
spell VFX rect and the damage popups).

**The HUD column is three identical PC plates** (`BuildPcPlates`, 2026-09-10),
one per party seat, 452 wide and `452 / PcPlateArt.Aspect` tall, stacked
bottom-up from `FightSubmenuLayout.VisibleBottomLine` with a 4px gap. Each is
the occupant's own leather plate art with their head embossed at the right end
— identity is the ART, so the ACTING character is a tinted halo
(`PcPlate{i}Highlight`) plus a brighter name rather than a promotion to a
bigger card. This replaced a taller `PartyPlate` for whoever was acting plus
two smaller `Roster{i}` cards for everyone else: two anatomies, two ledgers,
two paint routines, and a roster half structurally unable to show what the
party half could. Names are 16pt and every number 14pt, drawn ON 20px meters
with `TypographyRole.TacticalData`'s outlined material. `FightHudPalette.cs` holds the colours, which in
v1 were Editor-assembly `Color` fields the runtime controller could not read
and therefore restated.

### Core

`Core/FightController.cs` — references, the background swap, the static HUD
paint, and `AnchorSubmenuRows`, which re-anchors an open submenu through the
**same** `FightSubmenuLayout.RowY` the build used. v1 kept a second copy of
those constants in Core and its design preview drew rows at 8-slot positions.

**Stable slot identity.** Where a figure STANDS (rank) and WHICH figure it is
(slot) are two different facts, kept apart on purpose after a bug shipped
from conflating them: `SlotFor`/`AnimatorFor`/`HitFlashFor`/`DeathFadeFor` used
to scan `Encounter.PlayerParty` for the combatant and return the handle at the
same list INDEX, correct only while the party never moved — a `Move` swaps two
members' list positions and swapped their sprites, nameplates, hit flashes,
death fades and animators along with their places. `_slotOf`, seeded once per
fight from the opening lists (and extended for a summon, which appends and so
is still its own list index), is the one map every per-combatant handle reads
through `HandleFor<T>`/`SlotIndexOf`; `RefreshStage` walks RANK for position
and SLOT for everything else. `GlideTo` writes only a figure's mark over
0.35s scaled on the beat clock, deliberately not authoritative the way
`Rehome` is (`Rehome` stops in-flight travel, hover, stretch and breath — a
corpse finishing its fade three slots away would cancel the swing that killed
it if it used `Rehome`). `_enemyMarks` is the enemy-side counterpart the
status strip nudges itself against, reading the mark a repaint actually used
rather than the slot index that used to double as the rank.

### Tests

`FightSessionTests`, `TurnRiderTests`, `EnemyAiTests`, `EnemyIntentTests`,
`SkillDispatchTests`, `FightTalentTests`, `FightRewardsTests`,
`DamagePipelineTests`, `CombatBeatTests`, `FightScreenTests`,
`FightCapacityPinTests`, `ReachTests` (the `ReachKind`/mask primitives),
`MoveCommandTests` (`CanMove`/`Move`, the front-rank rule), `EnemyReachTests`
(the enemy side of the same rule, plus the no-committed-intent fallback) —
all EditMode, all sub-second. `StageFormationTests` (PlayMode — `Formation`
holding a rank through a corpse's fade, the slide waiting on `HoldsRank`) is
the one presentation test in this list that needs the player loop, so it is
not sub-second. `UiKitVisiblePadTests.cs` (`Tests/EditMode/Ui/`, `[D]`) is
measured-vs-literal: it re-scans the committed kit PNGs independently of the
C# literals in `ContainerArt`/`ButtonPlateArt` and is expected to go red on
the next kit regeneration, same as `StanceManifestValidationTests`;
`Tests/EditMode/Shared/ButtonPlateArtProbe.cs` is its (and other UI-kit
tests') internals-visible probe, not a test itself.

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
| `Core/FightBeatPlayer.cs` | playback, paint-first-then-move, `Flush` reclaims; per-target numbers and recoils; the three "which moment" delegates — `PaintVitals`, `PaintFormation`, `PaintTurnOrder` — all fired from the same per-beat point and all cleared to live state when playback ends |
| `Domain/Combat/Session/BeatTargetResult.cs` | what one combatant of several took, for a beat that landed on more than one |
| `Core/DamagePopup.cs` | the rise-and-fade, with `Reclaim` |
| `Core/StageHitFlash.cs` | the white silhouette, over `Resources/Shaders/UIHitFlash.shader` |
| `Core/ContactCues.cs` | the melee contact cues' asset paths, durations and box size — one home shared by `StaticSwing` (the wind-up whoosh) and `FightController.PlayContactFx` (the arc, the burst, the thud) |

**The static-art pilot (recommendation adopted from the now-archived
`docs/archive/STATIC_COMBAT_ART_DEEP_DIVE.md`; its still-cited rule lives on
in `docs/ART_PIPELINE.md`) — the five cues a single-drawing actor's swing was
missing.** Every actor is a single
drawing per stance now, so the gate is purely the CLASS of beat, never a
creature: a `StageApproach.Lunge` at a target that is somebody else.

| File | What it adds |
|---|---|
| `Core/StaticSwing.cs` | the wind-up a still cannot draw — reports anticipation + travel as one number, which is what moves the impact instant to when the figure actually arrives, and plays the whoosh at the top of the crouch |
| `Core/StageActorAnimator.cs` | `Play`'s optional `leadSeconds` (the crouch before the snap, `Anticipate`), and one afterimage at the contact position when `TweenBack` opens |
| `Core/FightBeatPlayer.cs` | `IsStaticSwing`, the `PlayContactFx` delegate and `WantsContactFx` |
| `Core/FightController.SpellVfx.cs` | `PlayContactFx`/`ContactBoxFor` — the arc and the burst as a two-layer presentation through the same allocator every spell uses (they used to be pool members 0 and 1, taken by index) |
| Tests | `FightContactCueTests` (which beats get the effects), the `StaticSwing` timing pin in `FightBeatPacingTests`, `AnAnticipatedLungeStillEndsExactlyOnItsMark` in `StageAnimationTests`, and `StaticPilotStageCaptureTests` (the whole beat, sampled and photographed) |

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

`StaticPilotStageCaptureTests` is the fourth, and the only one that photographs
MOTION rather than a still: it plays one beat of the Bog Witch's plain swing at
`BeatSpeedMultiplier` 1 and samples the stage every 1/30s of game time
(`Time.captureDeltaTime`), writing `f{i}.png` + `timing.json` under
`tools/screenshots/runtime/static_pilot/<PP_CAPTURE_LABEL>/` so the same fixture
records a before and an after. Its plain swing is forced by handing `EnemyKit` a
one-entry ability pool, not by a seed. Run it through `tools/static_pilot_qa.ps1
-Label <label>`.

`PartyFormationCaptureTests` photographs the real three-member party at rest
instead of a swing -- four real-time samples (`WaitForSecondsRealtime`, not
`Time.captureDeltaTime`) under `tools/screenshots/runtime/party_formation/<label>/`
plus a `slots.json` of each party slot's position, animator home, sprite rect
and foot-shadow position at the first and last frame, so a human can judge
whether the far-slot flyer (Odette) clears the two figures standing in front
of her and whether her shadow stays grounded while she hovers.

### The stage (`FightController.StageVisuals.cs`)

Actors, poses and grounding. Ported with its two hard-won rules intact, both of
which were playtest bugs that read as art problems:

- **The ground line comes from the manifest, never from measuring alpha.**
  Delivered art does not put the feet on the canvas bottom. Pinning the raw
  canvas to the floor made the golem jump 52px going idle -> attack -- the
  "golem flies upwards in its attack" report. The runtime used to scan for the
  lowest opaque pixel, which found the golem's earth spike and Shawn's staff
  instead of their feet and hoisted both into the air. One authored value per
  actor, which only works because all of an actor's drawings share one canvas
  (`EnemyStanceCaptureTests.EveryStanceOfAnActorSharesOneCanvas`).
- **The shadow's X is still measured, and only from the idle drawing**, so a
  pose that swings an arm out cannot drag the ring sideways. Horizontal centring has
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
`StageApproaches.Parse`). Between blows the only motion on stage is the
continuous `BreathCurve` swell, driven per idle figure from `.StageVisuals`'s
`IdleBreathing` loop — a transform write, not a repaint.

A beat can carry **three actor poses, keyed by phase** — `ActorApproachStance`
worn while travelling, `ActorWindupStance` between arrival and impact,
`Stances[Actor]` as the strike from the impact instant. The precedence table
and its fallbacks are two pure functions on `CombatBeat`
(`OpenStanceFor`/`ArrivalStanceFor`) so an EditMode test can reach them;
`FightBeatPlayer` owns only WHEN each moment falls, and a beat that authored
neither new field makes exactly the one `SetStance` call it always did.
Authoring a wind-up on a `Hold` or a `Close` buys the beat a wait of
`StaticSwing.WindupSeconds`, charged to the beat's own `spent` so the settle
gives it straight back. Content vocabulary: `docs/ART_PIPELINE.md` §4c.

**Which stance FOLDER a combatant is drawn from** has one seam,
`.StageVisuals`'s `SpriteFolderFor` — every other question the stage asks (the
sprite, the ground line, the canvas top, the breath, the hover, the ring, the
intent badge) reaches a folder through it. A `TransformGrant.spritePath` swaps
that one answer, so the whole figure re-lays out against the form's own
`StanceManifest` entry. The change is RECORDED on the beat (`CombatBeat.Forms`)
rather than read off live `Transformation`, for the reason the vitals snapshot
is: the round has already resolved by the time a beat plays, so live state
would draw the new form from the beat's first frame. `FightBeatPlayer.WearForm`
applies it at the impact instant (silhouette flash, authored `shake`);
`ResyncForms`, fired where `PaintFormation(null)` is, puts every figure back on
live state — which is also the only place the REVERT can happen, because a
transform expires at its holder's turn start and that opens no beat.

Party art is a **parallel map** (`BindPartyArt`), deliberately not part of
`PlayerKit`. The kit is what combat needs and a sprite folder is not that; v1
kept both in one `_playerOwners` dictionary, which is a large part of why its
combat logic could not leave the controller.

`FightStageVisualTests` covers the view side; the rules themselves already had
EditMode tests.

### Spell VFX (the layered presentation)

A spell is an **ordered array of layers**, each independently choosing what
renders, where it belongs and when it runs. Four files hold the model and three
hold the runtime; nothing else in the game knows a spell has layers at all.

| File | What it owns |
|---|---|
| `Domain/Content/SpellLayer.cs` | `SpellLayer` and `SpellEmitter` — the authored shape |
| `Domain/Content/SpellLayerNames.cs` | the six discriminators (`render`, `place`, `at`, `until`, `facing`, `sort`), each a closed enum with a `Parse`/`IsKnown` pair, `SpellAnchorNames`' shape exactly. `SpellWord.Is` compares an authored word without normalising a copy of it — these are computed properties read several times per layer per frame, and `ToLowerInvariant` allocated a string on every one |
| `Domain/Content/SpellLayerRules.cs` | every validation rule, collected in one pass, refused at content build with the skill, the layer and the field named |
| `Domain/Content/SpellPresentation.cs` | `layers`/`layerFormat`/`hitCueSeconds`, and `ToLayers(frameCount)` — the pre-layer format said in layers, at runtime, never stored |
| `Domain/Combat/Presentation/SpellPerformance.cs` | the resolved model: fan-out, absolute times, the lifetime table, `ArrivalSeconds`, `HitCueSeconds`, and `PositionOf`/`VelocityOf` — the ONE evaluation of an eased flight |
| `Domain/Combat/Presentation/SpellSchedule.cs` | `Crossed(previous, now]` — pure, half-open, each cue delivered exactly once whatever the clock did |
| `Domain/Combat/Presentation/SpellFrameCursor.cs` | which drawing is on screen at time t, and how far into its dissolve |
| `Domain/Combat/Presentation/SpellEmitterSim.cs` | ballistic drops as a closed form over `(spec, seed, index, age)` |
| `Core/SpellPerformancePlayer.cs` | the module: owns the clock (`Time.time`), the handles, the schedule tick, and the two renderer bands plus the particle pool |
| `Core/SpellVfxPlayer.cs` | the sprite/still renderer, and nothing else — `Show(frame, next, blend, alpha, at, size)` |
| `Core/SpellParticleRenderer.cs` | N pooled `Image`s driven from the sim. `Show` activates the member's own GameObject as well as enabling its Image — every pool member is built `Inactive()`, and enabling half the switch draws nothing while reporting itself as drawing |
| `Core/FightController.SpellVfx.cs` | placement only: where the bottom of an effect is, which is the question every bug in it came from |

**The pilot that consumes it (M5).** `prismatic_orb`'s **Water element** is the
first layered spell in the game: five layers, three renderer
kinds, two scopes and three schedule points, authored under
`elements[].vfx` in `skills.json` rather than on the skill, because an element
brings its own art.

**The second one (M6).** `cinderfault` is authored as two layers -- a
`formation`-placed ground sequence instanced once per cast and a `target`-placed
plume per struck slot, both opening at release, both running 0.78s, with
`hitCueSeconds: 0.43333334` stated outright where the pre-layer block derived it
from `impactFrame` over nine frames on disk. It is the case that proves the
model EXPRESSES what the adapter already produced: same instant, same members,
same `timing.json` before and after.

**The folder name is the artist's and the ids are the game's.** The delivery
lives under `Art/Sheets/Spells/prismatic_bolt/water/` while every runtime id is
`prismatic_orb_*`; nothing is renamed, because the recipe already bridges them —
`SpellVfxRecipeDriftTests` matches a played folder's LAST path segment against
recipe filenames, so a source sheet's directory name is invisible to it. (Those
source PNGs are on disk and **not in git**, which is true of the whole
`Art/Sheets/Spells/` tree and predates the pilot.)

| File | What it owns |
|---|---|
| `Art/Sheets/recipes/prismatic_orb_water_{core,contact,wake,drops}.json` | one recipe per layer, each with its own provenance. `prismatic_orb_water.json` and its 15-frame folder stay on disk, unplayed, as the single-sequence baseline the layered version is measured against |
| `Resources/Spells/prismatic_orb_water_core` | 6 frames, 512², the flight loop |
| `Resources/Spells/prismatic_orb_water_contact` | 9 frames, 512², the contact burst; the splash layer plays 8 of them from `startFrame: 2` |
| `Resources/Spells/prismatic_orb_water_wake` | 1 frame, 2027×410, the rear-attached ribbon |
| `Resources/Spells/prismatic_orb_water_drops` | 8 stills, 444², the droplet atlas both emitters throw |
| `Tests/EditMode/Shared/SpellVfxJson.cs` | the ONE reader of an authored `vfx` block, by reflection over the target type rather than a hand-written field list. Two fixtures ask it what a spell draws — the pool pin and the recipe drift lint — and a second parser is a second thing that can fall behind the format |
| `Tests/PlayMode/Combat/SpellEmitterCastTests.cs` | that a live cast's emitters put sprites on the canvas. Everything else about the sim is EditMode arithmetic, and arithmetic cannot see a pool member that was never activated |
| `Tests/PlayMode/Art/SpellRuntimeCaptureTests.cs` | three real-time recordings at `Time.captureFramerate = 60`: the pilot alone (45 frames, frame 0 the release), two consecutive casts with the first's droplets still owned when the second opens (75), and a cinderfault with a water cast landing on one of its targets (75). The pilot recording Ignores itself under `-nographics`; the two overlap fixtures ASSERT there and write pictures only where there is a device |
| `Tests/PlayMode/Combat/SpellTwoBurstTests.cs` | the synthetic third proof: two instances of one renderer kind, on a caster anchor, at different offsets, overlapping. Fixture-only — it authors nothing into `skills.json` and plays art that already ships (`Vfx/impact_burst`) |
| `Tests/PlayMode/Combat/SpellAllocationTests.cs` | what a cast costs a frame. `ProfilerRecorder(Memory, "GC Allocated In Frame")` over 120 frames idle and 120 during two overlapping casts, plus `Is.Not.AllocatingGCMemory()` on the module's own tick. It is what found the parse allocation above |

Design and milestones: `docs/PLAN_SPELL_LAYERS.md`. Handoff:
`docs/handoffs/spell_layers/BRIEF.md`.

**Three properties worth knowing before changing any of it.** The schedule is a
function of a WINDOW rather than of "is now past t", so a long frame or a test
at 60x delivers every cue it crossed, once, in order — the coroutines this
replaced could not state that, and `AUDIT.md` #61 is what it cost. Ownership is
the CAST rather than the stage slot, so two casts on one target coexist and a
second cast never restarts the first's renderer. And the clock is `Time.time`
with one conversion (`FightBeatPlayer.Scaled`), so a pause freezes the beat and
the spell together.

`FightBeatPlayer.Scaled`'s own product grew a second factor as of
`docs/PLAN_BATTLE_SPEED.md`: `Pace = BeatSpeedMultiplier * PlayerSpeedMultiplier`,
the second being the player's own in-fight preset (`FightBeatPlayer.
PlayerSpeedSource`, installed by `FightBootstrap.Start` from `GameSettings.
BattleSpeed`). A cast does NOT read the live `Pace` after it begins —
`SpellPerformancePlayer.Begin` captures `Cast.PaceAtStart` once, and `Tick`
converts age against that frozen value — so a preset stepped mid-cast (the
system menu holds `Time.timeScale = 0` while it is open) cannot retroactively
retime a spell already drawing. `docs/PLAN_SPELL_LAYERS.md`'s C1 contract has
an UPDATE note recording this as the third reader of the product.

One Image per sprite layer, re-pointed frame by frame. No Animator: that would
put the timing in an asset instead of beside the spell that owns it, and a
spell's `seconds` is authored in skills.json next to the damage, which is where
someone tuning the spell is looking.

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

**The shared ground layer (Cinderfault).** A cast can draw a sheet once behind
the whole enemy formation rather than once per target. In the layered format
that is `place: formation`, `sort: ground` and nothing else — instancing is
decided by the placement word, which is what keeps a spell id out of
orchestration entirely. The pre-layer fields below still exist and still work
for any block that authors them; the adapter turns `groundPath` into exactly
that layer. Since M6 no shipped spell authors them.

| File | What it owns |
|---|---|
| `Domain/Content/SpellPresentation.cs` | `groundPath`/`groundSeconds`/`groundImpactFrame`/`groundAspect`/`groundImpactY`/`castSfxPath`, and `HasGroundLayer` — the pre-layer form, read by the adapter |
| `Domain/UiKit/Screens/FightScreen.cs` | `BuildSpellGroundVfx` — a band of `FightHudSpec.SpellGroundRenderers`, declared *before* the racks so uGUI draws it behind the figures standing on it |
| `Core/FightController.SpellVfx.cs` | `PlaceOnFormation`/`GroundBoxFor`/`StruckBy` — the fault sized to the slots the living targets actually occupy, not to a slot and not to a constant |

Wired in `ScreenRegistry.cs` to the same `SpellVfxPlayer` the per-target pool
uses; the differences are its depth in the tree and that its size is recomputed
per cast. A SECOND member exists because ownership is per cast: a fault opening
while the previous one is still cooling must get its own. `StopSpellVfx` is
`SpellPerformancePlayer.CancelAll`, wired to `FightBeatPlayer.EndFight` rather
than `Flush` — abandoning a fight cancels visuals, superseding a playback does
not, because a tail is allowed to outlive the beat that cast it.

**Per-target results (`Domain/Combat/Session/BeatTargetResult.cs`).** A sweep
records what EACH enemy took, beside the beat's single `Amount` rather than
instead of it. `FightBeatPlayer.ShowAmount`/`Recoil` and
`FightController.FlashCombatant` read the list when there is one. Before it, an
all-enemies cast showed the largest single hit, once, over the beat's primary —
so three enemies with three different resistances were described by one number
that matched at most one of them.

**`PreSnapshot` finally does its job.** Playback now paints what stood BEFORE the
blow when a beat opens, waits `VfxSeconds * ImpactFraction`, then lands the
after-state, the flash and the floating number together. Zero delay for a plain
swing, so all of it is invisible for the overwhelming majority of beats. Until
now both snapshots existed and only one was ever painted, which showed a spell's
damage before the bolt had left the ceiling.

### Playable (`FightEncounterAdapter`, `FightBootstrap`, frame stepping)

**The adapter is the seam the whole decomposition exists for.** Above it is
engine-free Domain that knows nothing about ScriptableObjects; below it is Core
reading Resources. `EnemyDefinition -> ResolvedEnemy` is mechanical, field for
field, because the Resolved types were designed as the shape the definitions
already had.

**`SkillDefinition -> ResolvedSkill` is no longer a conversion.** It was the
same 34-line hand copy, and it dropped a field twice -- `transform`, then
`bookOnly`/`bookTier`. `ResolvedSkill` is now a `[Serializable]` class and
`SkillDefinition` is `{ ResolvedSkill Data; string id; int SortOrder; }`, so
`Resolve(definition)` returns `definition.Data` and adding a field to
skills.json touches `RawSkillEntry`, `SkillEntryResolver` and `ResolvedSkill`
only. Nine types are shaped this way now (upgrades were the last), and on all
nine the backing field is `[SerializeField] private` behind a `Data` getter and
an `internal SetData` -- ContentBuilder is the only caller, because Core grants
`InternalsVisibleTo` to the Editor assembly alone. Items, weapons and item sets
still restate their field lists, all three being `ItemDefinition`.

One rule worth naming, and still live in the enemy conversion: **`hasStatus` is
the authoring gate, and it is not the same question as "is a status type set".**
`appliesStatus` is a plain enum with a valid zero value, so every definition has
one whether anyone meant it or not. Reading the flag is what stops every monster
in the game inflicting the first entry. `ResolvedSkill` carries the same pair as
`Status`/`HasStatus` for the same reason -- Unity does not serialize a nullable
enum -- with `AppliesStatus` the computed nullable reading over it.

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

`FightBootstrap.DevForcedEnemyId` overrides the placeholder pick with exactly
one named enemy, and is what `Editor/QuickFightMenu.cs`'s **Prince's Palace >
Dev > Fight Giant Rat** menu item sets before opening `Fight.unity` in Play
mode -- a one-click way to see a specific enemy's combat art without a save
slot or a run. Backed by `SessionState`, not a plain static field, because
entering Play mode runs a domain reload that would otherwise wipe the value
between the menu item setting it and `Start()` reading it. Editor-only
(`#if UNITY_EDITOR`); a player build always takes the normal sortOrder pick.

### One rulebook for a run (`Core/Bot/RunOrchestrator.cs`)

**Where the rules of a descent live now.** `RunOrchestrator` is a plain static
class in Core -- not a MonoBehaviour -- holding the bodies that used to sit
inside screens: `ArriveAt` (was `MapController.Walk`'s `Arrive`), `BuildFight`
and `BuildSatchel` and `SettleFight` (were `FightBootstrap`), `RollOffers` (was
`FightController.Input`), `TakeOffer` (was `ReckoningController.Take`), and
`RelicDraftOffer`/`TakeRelic`/`FinishDraft` (were `RelicDraftController`).

It exists because those rules are about to have a **second caller**: the
headless balance bot of `docs/PLAN_BALANCE_BOT.md` (F2). A bot that
re-implements "what a won fight does to the run" measures its own copy rather
than the game, and `SettleFight` in particular is a fifteen-call ordering where
every line carries a comment about a bug. So the bodies moved down and the
screens became thin callers; the extraction is behaviour-preserving and pinned
by `FightSettlementTests`, which drives the real `FightBootstrap` door.

What each screen kept is exactly its screen work: `MapController` keeps which
scene to load and the repaint, `FightBootstrap` keeps the placeholder fight and
the two statics the Reckoning and defeat screens read, `FightController` keeps
the choice of `UnityEngine.Random` for the offer roll (deliberately unseeded --
see `PLAN_BALANCE_BOT.md` F3), `ReckoningController` keeps its take-once guard
and repaint, `RelicDraftController` keeps paging and selection.

### Two more one-rulebook seams (`Core/EquipmentOps.cs`, `Core/TalentOps.cs`)

Same extraction, same reason, one layer smaller. **`EquipmentOps`** holds the
save-side half of putting gear on and taking it off: read max health, call
`EquipMove` (Domain, which owns every rule about what can be worn), then
`RunEncounter.ScaleCarriedHealth` -- carried health is a fraction of a max that
gear moves. That pair had three copies (`CharacterDossierController`'s
`EquipFromPack` and `UnequipSlot`, `RunOrchestrator.AutoEquipIntoAnEmptySlot`)
and the bot is the fourth caller. It deliberately does not persist; the three
callers honestly differ on when they write.

**`TalentOps`** holds `BuildTree` and the kindle rule (`TalentPage.CanInvest`,
write the content's own id, subtract the cost) out of `TalentController`, which
keeps the beat, the save write and the repaint. Without it nothing that is not
a MonoBehaviour could buy a talent, which is why the bot reported
`distinctTalentSets: 1` for every cell.

**`ItemDescription.SimulateEquip`** is the third: the clone-and-resolve out of
`Compare`, made public so a caller can read numbers `ItemComparison` does not
carry -- specifically a weapon's, since `EffectiveStats` zeroes gear's attack
contribution and the weapon reaches combat as `WeaponPower` at the
`FightEncounterAdapter` seam.

Pinned by the characterization tests that already drove the real screens:
`DossierEquipTests`, `TalentInvestmentTests`, `DossierPackCaptureTests`.

### The balance bot (`Domain/Bot`, `Core/Bot`, `Editor/Bot`)

`Domain/Bot` -- engine-free brains: `IFightPolicy`/`IRunPolicy` and the
archetypes (`RandomLegalPolicy`, `GreedyAggressivePolicy`,
`GreedyDefensivePolicy`, `Lookahead2Policy`, `ProtectTheFrontPolicy` (moves a
threatened front-rank member back before falling to greedy) and
`MoveThenGreedyPolicy`, registered in `Archetypes.cs`),
`AllyTargetSelection` (WHICH squadmate a thinking policy aims a `SingleAlly` cast at -- the three orderings that used to be engine rules before the ally picker landed: emptiest mana bar for Gift: Mana, the first living ally for Fury/Haste, his own back for a Ward. `Narrow` collapses `LegalActions`' one-action-per-eligible-ally fan-out to the preferred one, and only the deciding archetypes call it, so `RandomLegal` stays uniform over the real menu), `FightRunner` (plays one `FightSession` to its end with one policy;
`PartyOrderIntact` is one of `FightInvariants`' checks, asserting a policy's
`Move` never leaves `Encounter.PlayerParty` in a shape the slot-identity map
cannot account for), `FightInvariants`/`InvariantHit` (the fight-level half of
the plan's bug list), `RunTrace`, and
`GearWeights`/`StatDeltas`/`StatOption`/`TalentOption` -- the archetype's
out-of-fight PREFERENCES, as a weight vector, because Domain cannot resolve an
item id into what wearing it would do. A 200-run/cell batch on `f2945c1b`
(`reports/bot/20260907-114015`, gitignored) found `ProtectTheFrontPolicy`
losing to both greedy archetypes rather than beating them -- 35 deaths and a
0.2188 `doomedShare` against `GreedyAggressive`'s 30/0.0714 and
`GreedyDefensive`'s 13/0.0769, roughly 3x the doomed share of the aggressive
baseline it was meant to improve on. See `AUDIT.md`.

`Core/Bot/GearEvaluator.cs` is the other side of that wall: it applies an
archetype's weights to the character sheet's own numbers
(`ItemDescription.SimulateEquip`, `ContentDatabase.EffectiveStats`,
`EquippedWeaponPower` through `WeaponPower.DisplayDamage`, `ActiveLoadout` for
legality) and answers three questions -- what is worth wearing, where a stat
point should go, and what one talent would derive. `Core/Bot/ProfilePresets.cs`
uses it to build Mid/Late: level, claim the track, spend every point, then
spend an ember budget counted off the live boss definitions.

`Core/Bot/BotRunDriver.cs` plays one whole run through `RunOrchestrator`'s
doors (draft, walk, fight, settle, offer) and adds the run-level invariants,
the equip passes (after the draft and after every `TakeOffer`) and the level-up
collection after every won fight. `Editor/Bot/BalanceBotRunner.cs` is the `-executeMethod`
batch entry `tools/bot.ps1` launches. See `docs/PLAN_BALANCE_BOT.md` for the
architecture and `docs/PLAN_BALANCE_BOT.md`'s "How to run and read it" section
for the day-to-day commands.

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
