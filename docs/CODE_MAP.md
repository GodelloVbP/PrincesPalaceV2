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
| Main Menu / Save Slots | `MainMenuScreen.cs`, `MainMenuAmbience.cs` | `Core/MainMenuController.cs`, `Core/SaveSlotController.cs` | — | `MainMenu()` scene | `MainMenu`, `SaveSlot` |
| Hub | `HubScreen.cs`, `HubAmbience.cs` | `Core/HubController.cs`, `Core/HubBuildingLooper.cs` | `HubAnchors.cs` | `Hub()` scene, `DressHub` | `Hub` |
| Relic draft (in Hub) | `RelicDraftScreen.cs` | `Core/RelicDraftController.cs` | `OfferRowLayout.cs` | `WireRelicDraft` | `RelicDraft` |
| Debug menu (in Hub) | `DebugMenuScreen.cs` | `Core/DebugMenuController.cs` | — | `WireDebugMenu` | `DebugMenu` |
| Talents | `TalentScreen.cs` | `Core/TalentController.cs` (+`.Motion`) | `ConstellationLayout.cs` | `Talents()` scene | `Talent` |
| Run Map | `MapScreen.cs` | `Core/MapController.cs` (+`.Walk`, `.Event`, `.Heart`) | `MapLayout.cs` (pixels, camera, trails), `MapGraph.cs` (a realm placed: node slot = id, footprints, heart slots), `MapWalk.cs`, `MapNodeCard.cs` (room words), `Core/MapNodePreview.cs` (decided facts) | `Map()` scene | `Map` |
| Shop (in Map) | `ShopScreen.cs` | `Core/ShopController.cs` | `ItemComparisonPanel.cs` | `WireShop` | `Shop` |
| Event room (in Map) | `EventScreen.cs` | `Core/EventController.cs` | `DialogueStageLayout.cs` | `WireEvent` | `Event` |
| Fight | `FightScreen.cs` | `Core/FightController.cs` (+6 parts, below) | `FightSubmenuLayout.cs`, `FightHudPalette.cs`, `FightRoundPresentation.cs`, `PcPlateArt.cs` | `Fight()` scene | `Fight` |
| Reckoning (rewards, in Fight) | `ReckoningScreen.cs` | `Core/ReckoningController.cs` | `OfferRowLayout.cs` (`MaxCards`: a page shows k + 2 cards) | `WireReckoning` | `Reckoning` |
| Defeat (in Fight) | `DefeatScreen.cs` | `Core/DefeatController.cs` | — | `WireDefeat` | `Defeat` |
| System Menu (Fight/Map/Hub) | `SystemMenuScreen.cs` | `Core/SystemMenuController.cs` | `SystemMenuLayout.cs`, `SystemMenuTabs.cs` | `WireSystemMenu` | `SystemMenu` |
| CHARACTER (tab: strip, rail, LOADOUT/ATTRIBUTES/SKILLS/REWARD TRACK) | `CharacterDossierScreen.cs` | `Core/CharacterDossierController.cs` (+`.Loadout`, `.Sections`, `.Skills`), `Core/EdgeScroll.cs`, `Core/Content/SourcedSkill.cs` (`ContentDatabase.SkillSourcesFor`) | `DossierLayout.cs`, `SheetStats.cs`; models `CharacterSection.cs`, `CharacterAttention.cs` (pips; fed by `Core/CharacterAttentionFeed.cs`, also the map's `MapAttentionLabel`, worded by `AttentionHint.cs`), `CharacterStrip.cs`, `LoadoutPack.cs`, `LoadoutPending.cs`, `LoadoutCompare.cs`, `SkillsListModel.cs`, `SkillListing.cs`, `Domain/Content/SkillSource.cs` | `WireDossier`, `WireCharacterOverlay` | `Dossier`, `CharacterDossier`, `CharacterPane`, `JourneyCharacterLoadout` |
| Reward track (CHARACTER section) | `RewardTrackScreen.cs` | `Core/RewardTrackController.cs` (+`.Input`, `.Motion`) | `RewardTrackLayout.cs` | `WireRewardTrack` | `RewardTrack` |
| Party (tab: seats with HP bar and defences, carry preview, roster only while someone sits out) | `PartyScreen.cs` | `Core/PartyController.cs` (`FactsFor` reads the save, the run and `SystemMenuController.FightSource`), `Core/PartyDragSource.cs` | `PartyLayout.cs`; models `Domain/UiKit/PartyMemberFacts.cs` (which source wins per context), `Domain/Party/` (`PartyFormation.RosterVisible`) | `WireParty` | `Party` |
| Options (tab) | `OptionsScreen.cs` | `Core/OptionsController.cs`, `Core/OptionRow.cs`; settings `Core/GameSettings.cs` | `OptionsLayout.cs`, `OptionRows.cs` | `WireOptions` | `Options` |
| Run (tab) | `RunScreen.cs` | `Core/RunController.cs` | `RunLayout.cs`, `RunStatRows.cs`, `RunEffectRows.cs` | `WireRun` | `SystemMenuRun` |
| Glossary (tab) | `GlossaryScreen.cs` | `Core/GlossaryController.cs`, `Core/GlossaryEntries.cs` | `GlossaryLayout.cs`, `Paging.cs`; model `Domain/Glossary/GlossaryCatalog.cs`, `Domain/Combat/Session/StatusDefinitions.cs` | `WireGlossary` | `Glossary` |
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
| Spell kick (stage push toward the target on the cue) | `vfx.kick`: `Domain/Content/SpellKick.cs`, rules in `SpellLayerRules.CheckKick` → `SpellPerformancePlayer.StageKick` (and applied to the cast's own layers in `Paint`) | Laid on the stage racks by `FightController.ApplySpellStage` through `StageShake.SetNudge`; never the HUD. |
| Spell gloom (stage darkening) | `vfx.gloom`: `Domain/Content/SpellGloom.cs` (curve, `StageDarkness.Compose`), rules in `SpellLayerRules.CheckGloom` → `SpellPerformancePlayer.GloomAlpha` | Drawn only by `FightController.ApplyStageDarkness` (`Core/FightController.Rounds.cs`) on the one `roundDarkness` image, max with the event's darkness. |
| Contact FX | `Core/ContactCues.cs`, `Core/StaticSwing.cs` | Melee arc/burst/thud asset paths and timings. |
| Run rules | `Core/Bot/RunOrchestrator.cs` (+ parts below) | The one rulebook for a descent; screens and the bot both call it. |
| Run lifecycle | `Core/RunManager.cs`, `Data/RunSnapshot.cs`, `Core/RealmOpening.cs` | Static accessor over the save; the realm regenerated from seed and saved profile. |
| Realm map | `Domain/Dungeon/RealmMap.cs`, `RealmMapGenerator.cs`, `RealmMapRules.cs`, `RealmLayout.cs`, `RealmProfile.cs`, `PrizeKind.cs`, `NodeStrength.cs`, `RealmPrize.cs`; anchor roll `EncounterRoll.RollAnchor` (via `RunEncounter`); heart `Domain/Dungeon/HeartAnchors.cs`, `RunEncounter.LivingAnchors`, `RunOrchestrator.PayHeartAnchors`; chips paid `Core/RealmPrizes.cs`; save 8 -> 9 `Data/RealmMigration.cs` | Plan: `docs/PLAN_RUN_MAP.md`. Strength, profile and name frozen per realm at open. Designed realms: `ContentData/realms.json` -> `RealmEntryResolver` -> `RealmDefinition` -> `ContentDatabase.Realms`; `RealmProfiles.Resolve` picks a floor's row or the default; the header reads `RealmOpening.NameOf(run)`. |
| Non-fight rooms | `Domain/Dungeon/RoomResolution.cs` → `Core/RoomResolver.cs` | Every room that does nothing says so. |
| Cache room (bank gold) | `RunOrchestrator.EnsureCache` + `bankGold` in `RunOrchestrator.Event.cs`, `the_cache` in `events.json`, `RunSettlement.Settle` | Event page with a fixed pick; the bank pays the hub wallet at run end. |
| Events | `Core/Bot/RunOrchestrator.Event.cs`, `Domain/Events/`, `Core/RunEventContext.cs` | Authoring: `docs/EVENTS.md`. |
| Shop stock/pricing | `Core/Bot/RunOrchestrator.Shop.cs`, `Domain/Rewards/ShopStock.cs`, `ShopPricing.cs` | |
| Effects: what relics, affixes and track terms do in a fight | `Domain/Combat/EffectKind.cs`, `EffectCatalog.cs` (per-kind facts), `Effect.cs`, `EffectSet.cs` (held, stacked; `CombatantState.Effects`), `EffectFold.cs` (stats); `Domain/Content/EffectEntryResolver.cs`; built in `Core/FightEncounterAdapter.cs` | Hooks ask `actor.Effects.Has/Magnitude`; headline numbers in `relics.json`, secondary ones in `FightTuning`. Talents not yet on it. |
| Equip / talents / item preview | `Core/EquipmentOps.cs`, `Core/TalentOps.cs`, `Core/ItemDescription.cs` | One rule each, shared by screens and bot. |
| Gear stats (a copy is a base id + tier) | `Domain/Content/GearCurve.cs` (`At(tier)` -> `GearStats`), `Core/Content/ContentDatabase.Effective.cs` (`Effective(copy, honing)`, the one stat accessor; `Gear`), `Domain/Equipment/SlotHoning.cs` (honing per character and slot, on `RunSnapshot.slotHoning`), `Domain/Content/ItemQuality.cs` (`Quality` from affix count) | Readers never read stat fields off `ItemDefinition`; they ask `Effective`. Prices: `Domain/Rewards/ShopPricing.cs` (`ScalePrice`). |
| Menu permissions (a fight is read-only) | `Domain/UiKit/MenuContext.cs`, `MenuAction.cs`, `MenuPermissions.cs`, `Domain/Party/PartyModes.cs` | One table (`docs/PLAN_SYSTEM_MENU.md` C3). Controls and the Core ops (`EquipmentOps`, `TalentOps`, `RunOrchestrator.Spells`, `RewardTracks.Claim`) both ask it. |
| Progression | `Domain/Progression/`, `Core/RewardTracks.cs`, `Core/CharacterIdentity.cs`, `Core/Achievements.cs` | |
| Content pipeline | `Assets/_Project/ContentData/*.json` → `Domain/Content/*EntryResolver.cs` → `Editor/ContentBuilder.cs` → `Core/Content/ContentDatabase.cs` | Field reference: `docs/CONTENT_SCHEMA.md` (`tools/content_schema.ps1`). |
| Save | `Core/SaveSystem.cs`, `Data/SaveData.cs`, `Core/SaveSlotManager.cs` | Versioned `Migrate()` + additive `Reconcile()`. |
| Scene switching | `Core/Navigation.cs` | Scene names in one place; `LoadOverride` test seam. |
| Menu lintel pill, notice line and prompt footer | `Core/MenuNotice.cs` (the one notice, `SystemMenuController.ShowNotice` / `Refuses`), `Core/MenuPromptFooter.cs`, `Core/IMenuPromptSource.cs` (dossier, party and reward track implement it), `Domain/UiKit/MenuPrompt.cs`, `PromptGlyphs.cs`, `MenuFooterLayout.cs`, `MenuModePill.cs` | Every refused press says `MenuPermissions.Reason`; the footer's prompts come from the focus and pending state and are marked locked from the same table (`docs/PLAN_SYSTEM_MENU.md` C10-C12; AUDIT #259). New glyph family = one arm in `PromptGlyphs`. |
| Menu type floor | `Domain/UiKit/MenuTypeFloor.cs`, `UiNode.AllowSmallText`; lint in `Tests/EditMode/Ui/MenuTypeLintTests.cs` | Mixed case 20, all caps 24 (a line of capitals and digits, like `HP 180 / 240`, counts as caps), every label carries a role. The lint walks the whole `SystemMenuScreen` tree, so a new pane is held without being listed; `Scoped` names only the rows other screens borrow (Reckoning relics, draft held strip, the map's hint) (C14). |
| Fight values in the menu (health, pools, temporary modifiers) | `Core/IFightMenuSource.cs`, `Core/FightController.MenuSource.cs` (records what the HUD painted where it paints), `Domain/UiKit/FightMemberFacts.cs`, `Domain/Combat/Session/FightModifiers.cs` | In a fight the menu reads the HUD's painted numbers, never `RunSnapshot.currentHealth` or the session's lead (`docs/PLAN_SYSTEM_MENU.md` C4). `FightController.Bind` hands itself to `SystemMenuController.FightSource`; CHARACTER and PARTY (`PartyMemberFacts.Resolve`) both read it. |
| Menu entry, hotkeys and reopen rule | `Core/SystemMenuController.cs` (`Open(tab, target)`), `Core/MenuHotkeys.cs`, `Core/SheetPanel.cs`, `Domain/UiKit/MenuTarget.cs`, `MenuHotkey.cs`, `MenuMemory.cs`, `SystemMenuTabs.cs` | One entry, one hotkey owner (`NavigationInputModule.DispatchMenuHotkeys`, read off Input Manager buttons `HotkeyC/I/P/R` and `View`), one reopen memory (`docs/PLAN_SYSTEM_MENU.md` C6, C7, C9). |
| One relic presenter (Reckoning RELICS, RUN, relic draft held strip) | `Domain/UiKit/RelicListModel.cs` (`RelicRow`, order and paging), `Core/RelicRows.cs` (the one `ContentDatabase.GetRelic` lookup, unknown-id row), `Core/RelicListPresenter.cs` (paints a model into a controller's widgets) | Each surface hands the presenter its own widget arrays; the icon table is `ScreenRegistry.RelicIconTable()`. New relic surface = a widget set, not a lookup (`docs/PLAN_SYSTEM_MENU.md` C24). |
| Relic receipts: "Gained" chips (Reckoning above Continue, event page above the rows) | `Domain/UiKit/Screens/RelicChipTree.cs` (strip + tooltip nodes), `Domain/UiKit/RelicChipLayout.cs`, `Core/RelicChipStrip.cs` (the chip widget set on the relic presenter seam), `RunSnapshot.pendingRelicReceipts`, `RunOrchestrator.PendingRelicReceipts` / `AcknowledgeRelicReceipts` | Appended by the event `Relic` effect with what it applied; a surface acknowledges only the ids it displayed, on leaving (`docs/PLAN_BUGS_QA.md` C2). Chips join a screen's nav only when its primary rows do. |
| Reckoning pages (gear page, relic page) | `Core/RewardPages.cs` (settle, take, hone instead, leave), `Domain/Rewards/GearPage.cs`, `RelicPage.cs`, `PendingPage.cs`, `ContentData/rewards.json`; card text `Core/RewardCardFace.cs`; one settle seam for the screen and the bot `RunOrchestrator.SettleRewardPages`; bot play `BotRunDriver.Offer`, `GearEvaluator.ScoreCard` / `ScoreHone` | A won fight rolls its pages once on `RngStreams.Reward` and queues them on `run.pendingPages` (memory only). The screen resolves them in place, one page at a time, and saves after each action; opening it again resumes at the first page with choices left (`docs/PLAN_ITEMS.md` 1.2). |
| Traits, affixes, amulet | `ContentData/modifiers.json` (`trait`, `pool`, `requires`), `Domain/Content/TraitRequirement.cs`, `TraitStacking.cs`, `Domain/Combat/EffectCatalog.cs` (Trait holder), trait draw in `Domain/Rewards/GearPage.cs` step 8, catalog side `Core/RewardPages.cs` (`TraitsFor`), worn traits `ContentDatabase.CharacterEffects` / `InactiveTraitSlots`, rules text `Domain/Combat/ModifierEffectText.cs`; amulet set in `itemsets.json` |
| Where a fight ends | `Domain/Rewards/FightEndRouting.cs` (table over `FightEndReason`), `Domain/Rewards/ReckoningRequest.cs` (full / outcome form), `Core/FightController.Input.cs` `RouteFightEnd` | Reads the settlement and `FightController.BelongsToRun` (captured at fight start), never `RunManager.HasRun` (C1). |
| RUN pane effects | `Domain/UiKit/RunEffectRows.cs` | One definition per `EventBuffs` kind; `RunEffectRowTests` fails on a kind without one. |
| Gamepad navigation | `Core/NavigationInputModule.cs`, `Core/RuntimeNavWiring.cs`, `Domain/UiKit/UiNavLinkBuilder.cs`, `NavContext.cs`, `NavContextStack.cs` | Wired at runtime by each controller's `RefreshNavigation`/`WireNavigation`, never at build. |
| Focus marker | `Core/FocusMarker.cs`, `Domain/UiKit/FocusMarkerPlacement.cs` | One arrow for every screen; Fight answers via `IFightNavigationTarget`. |
| Tooltips | `Domain/UiKit/TooltipFocus.cs`, `TooltipPlacement.cs`, `Core/TooltipFocusRouter.cs`, `Core/TooltipFit.cs` | Focus outranks hover; one show/hide per screen. |
| UI kit (declare/solve/audit) | `Domain/UiKit/Ui.cs`, `UiNode.cs`, `UiSolver.cs`, `UiAudit.cs`, `UiStrings.cs` | Engine-free; `UiAudit` runs at four aspects. |
| UI emit (scene build) | `Editor/SceneBuilder/SceneBuilder.cs`, `UiEmitter.cs`, `ScreenRegistry.cs` | Build audits: `UiTextFitAudit`, `UiCountAudit`, `UiWiringSweep`, `UiNavControlsAudit`, `UiBindingAudit`. |
| Typography | `Domain/UiKit/Typography.cs`, `Editor/SceneBuilder/TmpBootstrap.Typography.cs`, `SceneBuilder.Typography.cs` | |
| Description tokens (`**bold**`, `{icon}`) | `Domain/Content/DescriptionMarkup.cs` (grammar, icon table, Validate/Expand), `Core/DescriptionRenderer.cs`, `Core/TmpAssetResolver.cs` (bold/grade fonts, icon sprite asset), `Core/UiText.cs` (`SetContent`), `Editor/SceneBuilder/TmpBootstrap.DescriptionIcons.cs` | Grammar and icon table: `docs/CONTENT_SCHEMA.md`; validated by the skill/talent/relic resolvers. |
| UI art kit shape | `Domain/UiKit/ButtonPlateArt.cs`, `ContainerArt.cs`, `PcPlateArt.cs`, `PcTheme.cs`, `IdentityMetals.cs` | Measured by `tools/measure_ui_kit.py`, `tools/normalize_pc_plates.py`. |
| Audio | `Core/SoundController.cs`, `Core/Sound.cs`, `Core/AudioLevels.cs`, `Domain/Audio/` | Self-bootstraps before any scene; not read through `ContentDatabase`. |
| Settings | `Core/GameSettings.cs` | PlayerPrefs. |
| RNG | `Domain/Rng/RngStreams.cs`, `SeededRandom.cs` | New stream = new constant. |
| Ambience | `Domain/Ambience/`, `Core/StarTwinkle.cs`, `LanternFlicker.cs`, `SlowDrift.cs`, `MoteDrift.cs`, `KenBurnsDrift.cs` | |
| Balance bot | `Domain/Bot/`, `Core/Bot/BotRunDriver.cs`, `Editor/Bot/BalanceBotRunner.cs`, `tools/bot.ps1` | Plays whole runs through `RunOrchestrator`. Node choice: `Domain/Bot/MapView.cs` + `PathRule.cs`, built by `Core/Bot/BotMapView.cs`. |
| Preview | `tools/preview.ps1`, `Editor/PreviewRequestWatcher.cs`, `Domain/Preview/PreviewProtocol.cs`, `Core/PreviewFight.cs` | One authored id → a fight to look at. |
| Render pipeline | `Editor/PipelineBuilder.cs` | Generates URP asset, Renderer 2D, post profile. |
| Screenshots | `Editor/SceneBuilder/ScreenshotTool.cs`, `tools/screenshot.ps1` | `KnownPanels` and the script's usage text move together. |
| Art import | `Editor/StanceSpriteImporter.cs`, `Editor/*ImportPostprocessor.cs` | Runtime-loaded sprite folders must be listed in the importer. |

## Partial-class families

- `Core/FightController` → `.Hud`, `.Input`, `.MenuSource`, `.Rounds`, `.SpellVfx`, `.StageVisuals`
- `Domain/Combat/Session/FightSession` → `.Beats`, `.Cooldowns`, `.Enemies`, `.Items`, `.Kinship`, `.Ledger`, `.Outcome`, `.Potency`, `.RelicMechanics`, `.Relics`, `.Riders`, `.Rounds`, `.Schedule`, `.Skills`, `.SpeedBuffs`, `.Talents`
- `Core/Bot/RunOrchestrator` → `Core/Bot/RunOrchestrator.{Event,Shelf,Shop}.cs`, `Core/RunOrchestrator.Spells.cs`
- `Core/Content/ContentDatabase` → `.Effective`, `.Validation`
- `Domain/Content/EventEntryResolver` → `.Dialogue`, `.Fights`, `.Shelves`
- `Core/CharacterDossierController` → `.Loadout`, `.Sections`, `.Skills`
- `Core/MapController` → `.Walk`, `.Event`, `.Heart`
- `Core/RewardTrackController` → `.Input`, `.Motion`
- `Core/TalentController` → `.Motion`
- `Editor/SceneBuilder/SceneBuilder` → `.Typography`; `Editor/SceneBuilder/TmpBootstrap` → `.Typography`, `.DescriptionIcons`

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
