# Code Map

Screen or system → the files to open first. Paths are relative to
`Assets/_Project/Scripts/` unless they start with `Assets/`, `tools/` or `docs/`.
Layering and generated artifacts: `docs/ARCHITECTURE.md`.

**Update rule: adding a screen, system, or partial-class part without updating this map is incomplete.**

## Screens → files

Every screen is a tree in `Domain/UiKit/Screens/`, a controller in `Core/`, and
a wiring method in `Editor/SceneBuilder/ScreenRegistry.cs` (the `ScreenDef` for
a scene, or a `Wire*` method for a panel nested in one). Tests are found by
prefix: `Tests/{EditMode,PlayMode}/<area>/<Prefix>*Tests.cs`.

| Screen | Tree | Controller | Layout helpers (`Domain/UiKit/`) | Wiring (`ScreenRegistry`) | Test prefix |
|---|---|---|---|---|---|
| Main Menu / Save Slots | `MainMenuScreen.cs`, `MainMenuAmbience.cs` | `Core/MainMenuController.cs`, `Core/SaveSlotController.cs`, `Core/ResetProgressController.cs` | — | `MainMenu()` scene | `MainMenu`, `SaveSlot` |
| Hub | `HubScreen.cs`, `HubAmbience.cs` | `Core/HubController.cs`, `Core/HubBuildingLooper.cs` | `HubAnchors.cs` | `Hub()` scene, `DressHub` | `Hub` |
| Relic draft (in Hub) | `RelicDraftScreen.cs` | `Core/RelicDraftController.cs` | `OfferRowLayout.cs` | `WireRelicDraft` | `RelicDraft` |
| Glossary (in Hub) | `GlossaryScreen.cs` | `Core/GlossaryController.cs`, `Core/GlossaryEntries.cs` | `Paging.cs` | `WireGlossary` | `Glossary` |
| Debug menu (in Hub) | `DebugMenuScreen.cs` | `Core/DebugMenuController.cs` | — | `WireDebugMenu` | `DebugMenu` |
| Talents | `TalentScreen.cs` | `Core/TalentController.cs` (+`.Motion`) | `ConstellationLayout.cs` | `Talents()` scene | `Talent` |
| Run Map | `MapScreen.cs` | `Core/MapController.cs` (+`.Walk`, `.Event`) | `MapLayout.cs`, `MapWalk.cs` | `Map()` scene | `Map` |
| Shop (in Map) | `ShopScreen.cs` | `Core/ShopController.cs` | `ItemComparisonPanel.cs` | `WireShop` | `Shop` |
| Event room (in Map) | `EventScreen.cs` | `Core/EventController.cs` | `DialogueStageLayout.cs` | `WireEvent` | `Event` |
| Fight | `FightScreen.cs` | `Core/FightController.cs` (+5 parts, below) | `FightSubmenuLayout.cs`, `FightHudPalette.cs`, `FightRoundPresentation.cs`, `PcPlateArt.cs` | `Fight()` scene | `Fight` |
| Reckoning (rewards, in Fight) | `ReckoningScreen.cs` | `Core/ReckoningController.cs` | `OfferRowLayout.cs` | `WireReckoning` | `Reckoning` |
| Defeat (in Fight) | `DefeatScreen.cs` | `Core/DefeatController.cs` | — | `WireDefeat` | `Defeat` |
| System Menu (Fight/Map/Hub) | `SystemMenuScreen.cs` | `Core/SystemMenuController.cs` | `SystemMenuLayout.cs`, `SystemMenuTabs.cs` | `WireSystemMenu` | `SystemMenu` |
| Character Dossier (tab) | `CharacterDossierScreen.cs` | `Core/CharacterDossierController.cs` | `DossierLayout.cs`, `SheetStats.cs` | `WireDossier`, `WireCharacterOverlay` | `Dossier`, `CharacterDossier` |
| Reward track (tab) | `RewardTrackScreen.cs` | `Core/RewardTrackController.cs` (+`.Input`, `.Motion`) | `RewardTrackLayout.cs` | `WireRewardTrack` | `RewardTrack` |
| Party (tab) | `PartyScreen.cs` | `Core/PartyController.cs`, `Core/PartyDragSource.cs`, `Core/PartyToast.cs` | `PartyLayout.cs`; model in `Domain/Party/` | `WireParty` | `Party` |
| Options (tab) | `OptionsScreen.cs` | `Core/OptionsController.cs`, `Core/OptionRow.cs`; settings `Core/GameSettings.cs` | `OptionsLayout.cs`, `OptionRows.cs` | `WireOptions` | `Options` |
| Run stats (tab) | `RunStatsScreen.cs` | `Core/RunStatsController.cs` | `RunStatsLayout.cs`, `RunStatRows.cs` | `WireRunStats` | `SystemMenuRunStats` |
| Exits (tab) | `ExitsScreen.cs` | `Core/ExitsController.cs` | `ExitsLayout.cs` | `WireExits` | `Exits` |

**To add a screen:** tree in `Domain/UiKit/Screens/`, strings in
`Domain/UiKit/UiStrings.cs`, controller in `Core/` with `internal`
`[SerializeField]` fields, a `ScreenDef` in `ScreenRegistry.All` (or a `Wire*`
call from its host scene) with its `CountBindings`, and a test in an area
folder. `ScreenRegistry.All` is the only screen list: scene build, audits and
screenshots all read it.

## Systems → entry points

| System | Start here | Note |
|---|---|---|
| Fight rules | `Domain/Combat/Session/FightSession.cs` (+ parts below) | All combat rules live here; `FightController` only paints and forwards input. |
| Damage | `Domain/Combat/Session/DamagePipeline.cs` | The only statement of damage composition order. |
| Fight payout | `Domain/Combat/Session/VictoryRewards.cs`, `FightSession.Outcome.cs` | Payout arithmetic, drops, who earns what. |
| Fight menu + HUD model | `Domain/Combat/Session/FightMenuState.cs`, `FightHudModel.cs`, `FightHudSpec.cs` | Engine-free; capacities shared by tree and session. |
| Beats (record → play) | `Domain/Combat/Session/CombatBeat.cs`, `FightSession.Beats.cs` → `Core/FightBeatPlayer.cs` | Session records beats; the player paints first, then moves. |
| Fight setup | `Core/FightBootstrap.cs`, `Core/FightEncounterAdapter.cs`, `Core/EncounterRequest.cs`, `Core/RunEncounter.cs` | The adapter is the `*Definition` → `Resolved*` seam. |
| Fight stage | `Domain/Stage/FightStageAnchors.cs`, `StageFormation.cs`, `StageStandOff.cs` → `Core/FightController.StageVisuals.cs`, `Core/StageActorAnimator.cs` | Rank (where it stands) and slot (which figure) are separate. |
| Stances | `Core/StanceManifestLoader.cs`, `Domain/Stage/StanceManifest.cs`, `Assets/_Project/Resources/StanceManifest.json` | Ground line, breath, hover per actor. |
| Spell VFX | `Domain/Content/SpellPresentation.cs`, `SpellLayer*.cs` → `Domain/Combat/Presentation/SpellPerformance.cs` → `Core/SpellPerformancePlayer.cs` | Placement only in `Core/FightController.SpellVfx.cs`. |
| Contact FX | `Core/ContactCues.cs`, `Core/StaticSwing.cs` | Melee arc/burst/thud asset paths and timings. |
| Run rules | `Core/Bot/RunOrchestrator.cs` (+ parts below) | The one rulebook for a descent; screens and the bot both call it. |
| Run lifecycle | `Core/RunManager.cs`, `Data/RunSnapshot.cs`, `Domain/Dungeon/DescentMap.cs` | Static accessor over the save; map regenerated from seed. |
| Non-fight rooms | `Domain/Dungeon/RoomResolution.cs` → `Core/RoomResolver.cs` | Every room that does nothing says so. |
| Events | `Core/Bot/RunOrchestrator.Event.cs`, `Domain/Events/`, `Core/RunEventContext.cs` | Authoring: `docs/EVENTS.md`. |
| Shop stock/pricing | `Core/Bot/RunOrchestrator.Shop.cs`, `Domain/Rewards/ShopStock.cs`, `ShopPricing.cs` | |
| Equip / talents / item preview | `Core/EquipmentOps.cs`, `Core/TalentOps.cs`, `Core/ItemDescription.cs` | One rule each, shared by screens and bot. |
| Progression | `Domain/Progression/`, `Core/RewardTracks.cs`, `Core/CharacterIdentity.cs`, `Core/Achievements.cs` | |
| Content pipeline | `Assets/_Project/ContentData/*.json` → `Domain/Content/*EntryResolver.cs` → `Editor/ContentBuilder.cs` → `Core/Content/ContentDatabase.cs` | Field reference: `docs/CONTENT_SCHEMA.md` (`tools/content_schema.ps1`). |
| Save | `Core/SaveSystem.cs`, `Data/SaveData.cs`, `Core/SaveSlotManager.cs` | Versioned `Migrate()` + additive `Reconcile()`. |
| Scene switching | `Core/Navigation.cs` | Scene names in one place; `LoadOverride` test seam. |
| Gamepad navigation | `Core/NavigationInputModule.cs`, `Core/RuntimeNavWiring.cs`, `Domain/UiKit/UiNavLinkBuilder.cs`, `NavContext.cs`, `NavContextStack.cs` | Wired at runtime by each controller's `RefreshNavigation`/`WireNavigation`, never at build. |
| Focus marker | `Core/FocusMarker.cs`, `Domain/UiKit/FocusMarkerPlacement.cs` | One arrow for every screen; Fight answers via `IFightNavigationTarget`. |
| Tooltips | `Domain/UiKit/TooltipFocus.cs`, `TooltipPlacement.cs`, `Core/TooltipFocusRouter.cs`, `Core/TooltipFit.cs` | Focus outranks hover; one show/hide per screen. |
| UI kit (declare/solve/audit) | `Domain/UiKit/Ui.cs`, `UiNode.cs`, `UiSolver.cs`, `UiAudit.cs`, `UiStrings.cs` | Engine-free; `UiAudit` runs at four aspects. |
| UI emit (scene build) | `Editor/SceneBuilder/SceneBuilder.cs`, `UiEmitter.cs`, `ScreenRegistry.cs` | Build audits: `UiTextFitAudit`, `UiCountAudit`, `UiWiringSweep`, `UiNavControlsAudit`, `UiBindingAudit`. |
| Typography | `Domain/UiKit/Typography.cs`, `Editor/SceneBuilder/TmpBootstrap.Typography.cs`, `SceneBuilder.Typography.cs` | |
| UI art kit shape | `Domain/UiKit/ButtonPlateArt.cs`, `ContainerArt.cs`, `PcPlateArt.cs`, `PcTheme.cs`, `IdentityMetals.cs` | Measured by `tools/measure_ui_kit.py`, `tools/normalize_pc_plates.py`. |
| Audio | `Core/SoundController.cs`, `Core/Sound.cs`, `Core/AudioLevels.cs`, `Domain/Audio/` | Self-bootstraps before any scene; not read through `ContentDatabase`. |
| Settings | `Core/GameSettings.cs` | PlayerPrefs. |
| RNG | `Domain/Rng/RngStreams.cs`, `SeededRandom.cs` | New stream = new constant. |
| Ambience | `Domain/Ambience/`, `Core/StarTwinkle.cs`, `LanternFlicker.cs`, `SlowDrift.cs`, `MoteDrift.cs`, `KenBurnsDrift.cs` | |
| Balance bot | `Domain/Bot/`, `Core/Bot/BotRunDriver.cs`, `Editor/Bot/BalanceBotRunner.cs`, `tools/bot.ps1` | Plays whole runs through `RunOrchestrator`. |
| Preview | `tools/preview.ps1`, `Editor/PreviewRequestWatcher.cs`, `Domain/Preview/PreviewProtocol.cs`, `Core/PreviewFight.cs` | One authored id → a fight to look at. |
| Render pipeline | `Editor/PipelineBuilder.cs` | Generates URP asset, Renderer 2D, post profile. |
| Screenshots | `Editor/SceneBuilder/ScreenshotTool.cs`, `tools/screenshot.ps1` | `KnownPanels` and the script's usage text move together. |
| Art import | `Editor/StanceSpriteImporter.cs`, `Editor/*ImportPostprocessor.cs` | Runtime-loaded sprite folders must be listed in the importer. |

## Partial-class families

- `Core/FightController` → `.Hud`, `.Input`, `.Rounds`, `.SpellVfx`, `.StageVisuals`
- `Domain/Combat/Session/FightSession` → `.Beats`, `.Cooldowns`, `.Enemies`, `.Items`, `.Kinship`, `.Ledger`, `.Outcome`, `.Potency`, `.RelicMechanics`, `.Relics`, `.Riders`, `.Rounds`, `.Schedule`, `.Skills`, `.SpeedBuffs`, `.Talents`
- `Core/Bot/RunOrchestrator` → `Core/Bot/RunOrchestrator.{Event,Shelf,Shop}.cs`, `Core/RunOrchestrator.Spells.cs`
- `Core/Content/ContentDatabase` → `.Effective`, `.Validation`
- `Domain/Content/EventEntryResolver` → `.Dialogue`, `.Fights`, `.Shelves`
- `Core/MapController` → `.Walk`, `.Event`
- `Core/RewardTrackController` → `.Input`, `.Motion`
- `Core/TalentController` → `.Motion`
- `Editor/SceneBuilder/SceneBuilder` → `.Typography`; `Editor/SceneBuilder/TmpBootstrap` → `.Typography`

## Tests

`Tests/EditMode/` references Domain only; `Tests/PlayMode/` references Core +
Domain. Both split into area folders `Combat/ Hub/ Content/ Run/ Ui/ Art/ Rng/`
plus `Shared/` (helpers). The folder is the area; `tools/test_areas.ps1`'s header
is the placement guide and `tools/test.ps1 -List` the class list. Shared
PlayMode harness: `Tests/PlayMode/Shared/JourneyFixture.cs` (scripted pad and
mouse input), `Tests/PlayMode/Shared/NavSceneReuse.cs`.

## Tools

| Tool | Use |
|---|---|
| `tools/test.ps1` | Fast loop: one area/class, or `-Changed`. |
| `tools/run_tests_parallel.ps1` | Commit gate; `-BuildContent`/`-BuildScenes` sync artifacts back. |
| `tools/test_areas.ps1` | Area discovery, structural gate, `$PathAreas` map for `-Changed`. |
| `tools/build_content.ps1` | Batchmode content build against the main tree. |
| `tools/content_schema.ps1` | Regenerates `docs/CONTENT_SCHEMA.md`. |
| `tools/preview.ps1` | Build/photograph/launch one enemy, spell or character. |
| `tools/screenshot.ps1` | Static panels (`-Panel`/`-All`) or running game (`-Runtime`). |
| `tools/unity_lock.ps1` | Dot-sourced: is an Editor holding the project; runner claims. |
| `tools/domain-tests/` | dotnet build/test of Domain without Unity. |
| `tools/slice_*.py`, `tools/key_green_screen.py`, `tools/sheet_slicing.py` | Art slicing/keying; see `docs/ART_PIPELINE.md`. |
| `tools/actor_stance_qa.py`, `tools/capture_strip.py`, `tools/static_pilot_qa.ps1` | Visual QA sheets and capture strips. |
| `tools/githooks/` | pre-commit, staging and agent-routing hooks. |
