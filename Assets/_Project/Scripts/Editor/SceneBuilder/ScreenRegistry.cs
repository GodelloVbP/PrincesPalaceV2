using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

public sealed class ScreenDef
{
    // The node name of the screen's root, and the handle the screenshot tool
    // and PlayMode tests look it up by.
    public string PanelName;

    public string ScenePath;

    // Builds the declared tree. Called fresh each time so a screen can close
    // over live content counts at build time.
    public Func<UiNode> BuildTree;

    // Attaches and binds controllers, and applies engine dressing.
    public Action<UiEmitResult> Wire;

    // Declares "this controller array should be exactly as long as these nodes"
    // for the E4 count audit. Optional, but any screen binding an array should
    // state it -- that pairing is the whole defence against the shipped Store
    // bug, where a strip was sized from one collection and filled from another.
    public Func<IEnumerable<UiCountAudit.Binding>> CountBindings;
}

// The ONE list of screens in the game.
//
// It drives scene assembly, the build-time audits, and the screenshot tool. v1
// had a separate hand-maintained KnownPanels table in ScreenshotTool plus a
// third copy in screenshot.ps1's usage text, with a documented "nothing keeps
// these in sync" hazard. There is one list now, so a screen cannot exist in the
// game and be invisible to its own tooling.
public static class ScreenRegistry
{
    public const string MainMenuScene = SceneBuilder.ScenesDir + "/MainMenu.unity";

    // One scene per screen while the rebuild is in progress, so each is
    // independently buildable, screenshot-able and testable. v1 kept every
    // in-game screen as a panel inside one Gameplay scene toggled by
    // GameplayManager; consolidating back to that is a one-line change here,
    // because this ScenePath is the only thing that decides which scene a
    // screen lands in.
    public const string HubScene = SceneBuilder.ScenesDir + "/Hub.unity";
    public const string FightScene = SceneBuilder.ScenesDir + "/Fight.unity";
    public const string MapScene = SceneBuilder.ScenesDir + "/Map.unity";
    public const string TalentScene = SceneBuilder.ScenesDir + "/Talents.unity";

    public static readonly List<ScreenDef> All = new List<ScreenDef>
    {
        MainMenu(),
        Hub(),
        Map(),
        Talents(),
        Fight(),
    };

    // The battle screen. The largest tree in the game, and the first to bind
    // eight indexed arrays -- which is why every one of them is declared to E4
    // below rather than trusted to have been built off the right list.
    private static ScreenDef Fight()
    {
        FightScreen screen = null;
        FightController fight = null;

        return new ScreenDef
        {
            PanelName = "FightPanel",
            ScenePath = FightScene,
            BuildTree = () =>
            {
                screen = FightScreen.Build();
                return screen.Root;
            },
            Wire = result =>
            {
                fight = result.Attach<FightController>(screen.Root);

                fight.backgroundImage = result.Image(screen.Background);
                fight.normalBackground = fight.backgroundImage.sprite;
                fight.eliteBackground = SceneBuilder.LoadSpriteByKey(FightScreen.EliteBackgroundKey);
                fight.bossBackground = SceneBuilder.LoadSpriteByKey(FightScreen.BossBackgroundKey);

                fight.enemySlots = screen.EnemySlots.Select(result.Rect).ToArray();
                fight.enemyWorldSlots = screen.EnemyWorldSlots.Select(result.Rect).ToArray();

                // ONE SHAKER PER RACK, not one for the whole screen. The two
                // stages are separate containers and the HUD is neither of
                // them, which is the point -- see StageShake on why the
                // painted frame has to stay nailed down while the fight moves.
                //
                // FOUR, not two -- the world-space racks get their own
                // shakers too. Before this a kick moved a rig-resolved
                // combatant's (hidden) shadow/nameplate rack while the rig
                // itself, the only thing actually visible, stood nailed
                // down -- the same defect Recoil/Punch/TravelFor's own
                // world-animator wiring below exists to fix, one layer up.
                fight.stageShakes = new[]
                {
                    result.Attach<StageShake>(screen.EnemyStage),
                    result.Attach<StageShake>(screen.PartyStage),
                    result.Attach<StageShake>(screen.EnemyWorldStage),
                    result.Attach<StageShake>(screen.PartyWorldStage),
                };
                fight.enemySprites = screen.EnemySprites.Select(result.Image).ToArray();
                fight.enemyHitFlashes = screen.EnemyHitFlashes.Select(result.Image).ToArray();
                fight.enemyBlends = screen.EnemyBlends.Select(result.Image).ToArray();
                fight.enemyNameplates = screen.EnemyNameplates.Select(result.Tmp).ToArray();
                fight.enemyFootShadows = screen.EnemyFootShadows.Select(result.Image).ToArray();

                fight.partySlots = screen.PartySlots.Select(result.Rect).ToArray();
                fight.partyWorldSlots = screen.PartyWorldSlots.Select(result.Rect).ToArray();
                fight.partySprites = screen.PartySprites.Select(result.Image).ToArray();
                fight.partyHitFlashes = screen.PartyHitFlashes.Select(result.Image).ToArray();
                fight.partyBlends = screen.PartyBlends.Select(result.Image).ToArray();
                fight.partyNameplates = screen.PartyNameplates.Select(result.Tmp).ToArray();
                fight.partyFootShadows = screen.PartyFootShadows.Select(result.Image).ToArray();

                fight.initiativeIcons = screen.InitiativeIcons.Select(result.Image).ToArray();
                fight.initiativeRings = screen.InitiativeRings.Select(result.Image).ToArray();
                fight.initiativeLabels = screen.InitiativeLabels.Select(result.Tmp).ToArray();

                fight.barkPortrait = result.Image(screen.BarkPortrait);
                fight.barkLabel = result.Tmp(screen.BarkLabel);

                fight.lowHpVignette = result.Go(screen.LowHpVignette);

                fight.enemiesHint = result.Tmp(screen.EnemiesHint);
                fight.enemyPlates = screen.EnemyPlates.Select(result.Button).ToArray();
                fight.enemyPlateIcons = screen.EnemyPlateIcons.Select(result.Image).ToArray();
                fight.enemyPlateNames = screen.EnemyPlateNames.Select(result.Tmp).ToArray();
                fight.enemyPlateHps = screen.EnemyPlateHps.Select(result.Tmp).ToArray();
                fight.enemyPlateHpFills = screen.EnemyPlateHpFills.Select(result.Image).ToArray();
                fight.enemyPlateTags = screen.EnemyPlateTags.Select(result.Tmp).ToArray();
                fight.enemyPlateReticles = screen.EnemyPlateReticles.Select(result.Go).ToArray();
                fight.enemyPlateBreakTracks = screen.EnemyPlateBreakTracks.Select(result.Go).ToArray();
                fight.enemyPlateBreakFills = screen.EnemyPlateBreakFills.Select(result.Image).ToArray();

                fight.partyPortrait = result.Image(screen.PartyPortrait);
                fight.partyName = result.Tmp(screen.PartyName);
                fight.partyClass = result.Tmp(screen.PartyClass);
                fight.partyHpFill = result.Image(screen.PartyHpFill);
                fight.partyHpValue = result.Tmp(screen.PartyHpValue);
                fight.partyMpFill = result.Image(screen.PartyMpFill);
                fight.partyMpValue = result.Tmp(screen.PartyMpValue);
                fight.partyBuffIcons = screen.PartyBuffIcons.Select(result.Go).ToArray();
                fight.partyBuffTooltip = result.Go(screen.PartyBuffTooltip);
                fight.partyBuffTooltipText = result.Tmp(screen.PartyBuffTooltipText);
                fight.woolPips = screen.WoolPips.Select(result.Image).ToArray();
                fight.woolValue = result.Tmp(screen.WoolValue);
                fight.secondLifeBadge = result.Go(screen.SecondLifeBadge);
                fight.transformStrip = result.Go(screen.TransformStrip);
                fight.transformStripText = result.Tmp(screen.TransformStripText);
                fight.rosterPlates = screen.RosterPlates.Select(result.Go).ToArray();
                fight.rosterNames = screen.RosterNames.Select(result.Tmp).ToArray();
                fight.rosterHpValues = screen.RosterHpValues.Select(result.Tmp).ToArray();
                fight.rosterHpFills = screen.RosterHpFills.Select(result.Image).ToArray();

                fight.verbButtons = screen.VerbButtons.Select(result.Button).ToArray();
                fight.verbLabels = screen.VerbLabels.Select(result.Tmp).ToArray();
                fight.verbCarets = screen.VerbCarets.Select(result.Go).ToArray();
                fight.breadcrumb = result.Tmp(screen.Breadcrumb);
                fight.continueButton = result.Button(screen.ContinueButton);

                fight.enemyIntentIcons = screen.EnemyIntentIcons.Select(result.Go).ToArray();
                fight.enemyHitAreas = screen.EnemyHitAreas.Select(result.Button).ToArray();
                fight.targetCancelButton = result.Button(screen.TargetCancelButton);
                // The reward screen owns Escape while it is up: opening this
                // menu over a reckoning the player is trying to dismiss is the
                // same wrong-thing-on-Escape the consumer list exists to stop.
                WireSystemMenu(result, screen.Root, screen.SystemMenu, lockedForFight: true,
                    inDescent: true, screen.Reckoning.Root);

                fight.intentTooltip = result.Go(screen.IntentTooltip);
                fight.intentTooltipText = result.Tmp(screen.IntentTooltipText);

                fight.submenuColumn = result.Go(screen.SubmenuColumn);
                fight.submenuTitle = result.Tmp(screen.SubmenuTitle);
                fight.submenuHint = result.Tmp(screen.SubmenuHint);
                fight.submenuBackButton = result.Button(screen.SubmenuBackButton);
                fight.submenuRows = screen.SubmenuRows.Select(result.Button).ToArray();
                fight.submenuRowRects = screen.SubmenuRows.Select(result.Rect).ToArray();
                fight.submenuViewport = result.Rect(screen.SubmenuViewport);
                fight.submenuContent = result.Rect(screen.SubmenuContent);
                fight.submenuScrollTrack = result.Rect(screen.SubmenuScrollTrack);
                fight.submenuScrollThumb = result.Rect(screen.SubmenuScrollThumb);
                fight.submenuNames = screen.SubmenuNames.Select(result.Tmp).ToArray();

                fight.detailColumn = result.Go(screen.DetailColumn);
                fight.detailName = result.Tmp(screen.DetailName);
                fight.detailKind = result.Tmp(screen.DetailKind);
                fight.detailBody = result.Tmp(screen.DetailBody);
                fight.detailStatValues = screen.DetailStatValues.Select(result.Tmp).ToArray();
                fight.detailStatKeys = screen.DetailStatKeys.Select(result.Tmp).ToArray();
                fight.detailDamageType = result.Tmp(screen.DetailDamageType);

                fight.targetPrompt = result.Go(screen.TargetPrompt);
                fight.targetPromptLabel = result.Tmp(screen.TargetPromptLabel);

                fight.reckoning = WireReckoning(result, screen.Reckoning);
                fight.defeat = WireDefeat(result, screen.Defeat);

                // Readable mid-fight, inert mid-fight. Same tree as the hub's,
                // built with the lock on.
                fight.characterSheetPanel = result.Go(screen.SystemMenu.Root);

                // ONE PLAYER PER POOL MEMBER. The player lives on the VFX
                // node itself, so disabling that node is a real stop rather
                // than something the controller has to remember -- the same
                // arrangement FightBeatPlayer has with the popup pool.
                fight.spellVfx = screen.SpellVfx.Select(result.Image).ToArray();
                fight.spellVfxPlayers = screen.SpellVfx
                    .Select((node, i) =>
                    {
                        var player = result.Attach<SpellVfxPlayer>(node);
                        player.image = fight.spellVfx[i];

                        // Never eats a click meant for an enemy behind it. The
                        // effect is drawn ABOVE the stage, so without this a
                        // spell in flight would swallow the next target
                        // selection.
                        player.image.raycastTarget = false;
                        player.image.preserveAspect = true;
                        player.image.enabled = false;

                        // The dissolve layer. Same settings as the frame it
                        // fades over, because it IS that frame one step later
                        // -- a different preserveAspect between the two would
                        // swap the effect's shape halfway through every
                        // transition.
                        player.fade = result.Image(screen.SpellVfxNext[i]);
                        player.fade.raycastTarget = false;
                        player.fade.preserveAspect = true;
                        player.fade.enabled = false;

                        return player;
                    })
                    .ToArray();

                fight.damagePopups = screen.DamagePopups.Select(result.Go).ToArray();
                fight.damagePopupLabels = screen.DamagePopupLabels.Select(result.Tmp).ToArray();

                // The popups own their own rise-and-fade, so each gets its
                // component and its label here rather than being animated by the
                // controller. The player lives on the POOL node, which is what
                // makes OnDisable a real reclaim path: hiding the pool hides
                // every popup with it.
                var popupComponents = new List<DamagePopup>();
                for (int i = 0; i < screen.DamagePopups.Count; i++)
                {
                    var popup = result.Attach<DamagePopup>(screen.DamagePopups[i]);
                    popup.label = result.Tmp(screen.DamagePopupLabels[i]);
                    popupComponents.Add(popup);
                }

                // The flash overlays: one component per stage slot, all sharing
                // the ONE committed material. Sharing it matters -- a material
                // per slot would be six identical assets and six draw-call
                // batches where one will do.
                var flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(PipelineBuilder.HitFlashMaterialPath);
                foreach (var flashRef in screen.EnemyHitFlashes.Concat(screen.PartyHitFlashes))
                {
                    var flash = result.Attach<StageHitFlash>(flashRef);
                    flash.image = result.Image(flashRef);
                    flash.image.material = flashMaterial;
                    flash.image.raycastTarget = false;
                }

                // The plate a combatant with no battle art falls back to. Loaded
                // through the same LoadSprite the tree uses, so the importer
                // flip to Sprite happens here too rather than being a thing
                // someone has to remember (CLAUDE.md gotcha 3).
                fight.enemyFallbackSprite = SceneBuilder.LoadSpriteByKey(FightScreen.FallbackPlateKey);

                // Something has to actually START a fight, or the scene opens on
                // an empty stage and the screen cannot be looked at. Placeholder
                // until a descent exists to hand a room's roster over -- see
                // FightBootstrap's own header.
                var bootstrap = result.Attach<FightBootstrap>(screen.Root);
                bootstrap.fight = fight;

                // The lunge/recoil animator, one per stage slot.
                //
                // WITHOUT THIS NOTHING MOVES. FightBeatPlayer.Lunge does a
                // GetComponent on the slot and returns silently when it finds
                // nothing, so the whole feature was a no-op that no test noticed:
                // the class existed, the call site existed, and the two were
                // never introduced.
                //
                // WORLD SLOTS TOO, now -- the exact same silent no-op this
                // comment already warns about, one layer up: a rig-resolved
                // combatant's world slot had no StageActorAnimator at all,
                // so FightController.WorldAnimatorFor's GetComponent always
                // returned null and every hit reaction landed only on the
                // (hidden) uGUI slot's shadow/nameplate rack while the rig
                // itself stood bolt still.
                foreach (var slotRef in screen.EnemySlots.Concat(screen.PartySlots)
                             .Concat(screen.EnemyWorldSlots).Concat(screen.PartyWorldSlots))
                {
                    result.Attach<StageActorAnimator>(slotRef);
                }

                // The death fade reaches TWO sibling images -- the figure and
                // its ground shadow -- because a corpse whose shadow stayed put
                // reads as the sprite failing to draw rather than as a death.
                for (int i = 0; i < screen.EnemySlots.Count; i++)
                {
                    var fade = result.Attach<StageDeathFade>(screen.EnemySlots[i]);
                    fade.sprite = result.Image(screen.EnemySprites[i]);
                    fade.shadow = result.Image(screen.EnemyFootShadows[i]);
                }

                for (int i = 0; i < screen.PartySlots.Count; i++)
                {
                    var fade = result.Attach<StageDeathFade>(screen.PartySlots[i]);
                    fade.sprite = result.Image(screen.PartySprites[i]);
                    fade.shadow = result.Image(screen.PartyFootShadows[i]);
                }

                var player = result.Attach<FightBeatPlayer>(screen.DamagePopupPool);
                player.popups = popupComponents.ToArray();
                fight.beatPlayer = player;
            },

            // E4. Eight indexed arrays, every one declared. Building each
            // straight off its own list already makes them agree; stating it
            // means a future edit that binds an array some OTHER way fails the
            // build instead of shipping a strip whose length nobody re-checked.
            CountBindings = () => new[]
            {
                Count("FightController.enemySlots", screen.EnemySlots, () => fight.enemySlots.Length),
                Count("FightController.partySlots", screen.PartySlots, () => fight.partySlots.Length),
                Count("FightController.enemyWorldSlots", screen.EnemyWorldSlots, () => fight.enemyWorldSlots.Length),
                Count("FightController.partyWorldSlots", screen.PartyWorldSlots, () => fight.partyWorldSlots.Length),
                Count("FightController.initiativeIcons", screen.InitiativeIcons, () => fight.initiativeIcons.Length),
                Count("FightController.enemyPlates", screen.EnemyPlates, () => fight.enemyPlates.Length),
                Count("FightController.woolPips", screen.WoolPips, () => fight.woolPips.Length),
                Count("FightController.verbButtons", screen.VerbButtons, () => fight.verbButtons.Length),
                Count("FightController.submenuRows", screen.SubmenuRows, () => fight.submenuRows.Length),
                Count("FightController.damagePopups", screen.DamagePopups, () => fight.damagePopups.Length),
                Count("FightBeatPlayer.popups", screen.DamagePopups, () => fight.beatPlayer.popups.Length),
            },
        };
    }

    private static UiCountAudit.Binding Count(string label, IReadOnlyList<NodeRef> declared, Func<int> bound) =>
        new UiCountAudit.Binding { Label = label, Declared = declared, BoundLength = bound };

    private static ScreenDef Hub()
    {
        HubScreen screen = null;

        return new ScreenDef
        {
            PanelName = "HubPanel",
            ScenePath = HubScene,
            BuildTree = () =>
            {
                screen = HubScreen.Build();
                return screen.Root;
            },
            Wire = result =>
            {
                var hub = result.Attach<HubController>(screen.Root);
                hub.talentsButton = result.Button(screen.TalentsButton);
                hub.principalityButton = result.Button(screen.PrincipalityButton);
                hub.characterSheetButton = result.Button(screen.CharacterSheetButton);
                hub.relicsButton = result.Button(screen.RelicsButton);
                hub.startRunButton = result.Button(screen.StartRunButton);
                hub.mainMenuButton = result.Button(screen.MainMenuButton);
                hub.currencyLabel = result.Tmp(screen.CurrencyLabel);
                DressHub(result, screen);
                hub.startRunCaption = result.Tmp(screen.StartRunCaption);

                // The ONE whose screen does not exist yet. This array is the one
                // place that list lives, and it shrinks as they land -- the
                // character overlay took CharacterSheet out of it, and the
                // glossary has now taken Relics.
                hub.unbuiltButtons = new[]
                {
                    result.Button(screen.PrincipalityButton),
                };

                WireCharacterOverlay(result, screen, hub);
                WireRelicDraft(result, screen, hub);
                WireGlossary(result, screen, hub);
                WireDebugMenu(result, screen, hub);
            },
        };
    }

    private static ScreenDef MainMenu()
    {
        MainMenuScreen screen = null;
        SaveSlotController slots = null;
        ResetProgressController reset = null;

        return new ScreenDef
        {
            PanelName = "MainMenuPanel",
            ScenePath = MainMenuScene,
            BuildTree = () =>
            {
                // The slot count comes from SaveSystem, not from a constant
                // restated in Domain. Three separate things are built off it.
                screen = MainMenuScreen.Build(new MainMenuInputs(SaveSystem.SlotCount));
                return screen.Root;
            },
            Wire = result =>
            {
                // Direct assignment through typed lookups. A typo or a type
                // mismatch here is a compile error, which is the whole point of
                // retiring SetField(controller, "playButton", ...).
                var menu = result.Attach<MainMenuController>(screen.Root);
                menu.playButton = result.Button(screen.PlayButton);
                menu.exitButton = result.Button(screen.ExitButton);
                menu.closeSaveSlotButton = result.Button(screen.CloseSaveSlotButton);
                menu.saveSlotPanel = result.Go(screen.SaveSlotPanel);
                menu.continueButton = result.Button(screen.ContinueButton);

                slots = result.Attach<SaveSlotController>(screen.SaveSlotPanel);
                slots.slotButtons = screen.SlotButtons.Select(result.Button).ToArray();
                slots.slotNumbers = screen.ChooseCards.Select(c => result.Tmp(c.Number)).ToArray();
                slots.slotTops = screen.ChooseCards.Select(c => result.Tmp(c.Top)).ToArray();
                slots.slotDetails = screen.ChooseCards.Select(c => result.Tmp(c.Detail)).ToArray();
                slots.slotGolds = screen.ChooseCards.Select(c => result.Tmp(c.Gold)).ToArray();
                slots.manageSavesButton = result.Button(screen.ManageSavesButton);
                slots.managePanel = result.Go(screen.ManageSavesPanel);

                reset = result.Attach<ResetProgressController>(screen.ManageSavesPanel);
                reset.deleteButtons = screen.DeleteButtons.Select(result.Button).ToArray();
                reset.slotNumbers = screen.ManageCards.Select(c => result.Tmp(c.Number)).ToArray();
                reset.slotTops = screen.ManageCards.Select(c => result.Tmp(c.Top)).ToArray();
                reset.slotDetails = screen.ManageCards.Select(c => result.Tmp(c.Detail)).ToArray();
                reset.slotFilledWashes = screen.ManageCards.Select(c => result.Go(c.FilledWash)).ToArray();
                reset.slotEmptyWashes = screen.ManageCards.Select(c => result.Go(c.EmptyWash)).ToArray();
                reset.confirmPanel = result.Go(screen.ResetConfirmPanel);
                reset.confirmLabel = result.Tmp(screen.ResetConfirmLabel);
                reset.confirmYesButton = result.Button(screen.ResetConfirmYesButton);
                reset.confirmFill = result.Rect(screen.ResetConfirmYesFill);
                reset.confirmNoButton = result.Button(screen.ResetConfirmNoButton);
                reset.backButton = result.Button(screen.CloseManageSavesButton);
                reset.saveSlotPanel = result.Go(screen.SaveSlotPanel);

                // NOT subscribing SaveSlotController.Refresh to a C# event here.
                // This Wire step runs at BUILD time and delegates do not
                // serialise, so the subscription would silently not exist in the
                // shipped scene -- and the wiring sweep could not catch it,
                // because it only sees serialized object references. The Play
                // panel refreshes in OnEnable instead, which covers the only
                // path that matters: delete in Manage Saves, reopen Play. The
                // same reasoning is why ResetProgressController reaches
                // MainMenuController.RefreshContinue with GetComponentInParent
                // at runtime rather than a delegate wired here.
                DressAmbience(result, screen);
            },

            // E4. Building each array straight off its declared list already
            // makes these agree; stating it means a future edit that binds an
            // array some OTHER way fails the build instead of shipping a strip
            // whose length nobody re-checked.
            CountBindings = () => new[]
            {
                new UiCountAudit.Binding
                {
                    Label = "SaveSlotController.slotButtons",
                    Declared = screen.SlotButtons,
                    BoundLength = () => slots.slotButtons.Length,
                },
                new UiCountAudit.Binding
                {
                    Label = "ResetProgressController.deleteButtons",
                    Declared = screen.DeleteButtons,
                    BoundLength = () => reset.deleteButtons.Length,
                },
            },
        };
    }

    // Engine dressing: the animator components whose types Domain cannot name.
    //
    // This is the bounded escape hatch the design allows, used in exactly one
    // place per screen. Note what it does NOT do: it never positions anything.
    // Every coordinate came from the declared tree.
    private static void DressAmbience(UiEmitResult result, MainMenuScreen screen)
    {
        var ambience = screen.Ambience;

        // No RadialGlowImage any more: the glow is a committed PNG the nodes
        // reference by SpriteKey. That component assigned a runtime-generated
        // sprite from [ExecuteAlways], which made the Edit Mode scene build
        // serialise the texture into the scene itself.
        foreach (var star in ambience.Stars)
        {
            result.Attach<StarTwinkle>(star);
        }

        // Windows breathe on long, offset periods. Sixteen of them sharing one
        // period and one phase would read as a flashing sign rather than a
        // building with people in it - which is why BeaconPulse gained
        // per-instance period and phase for this screen.
        for (int i = 0; i < ambience.Windows.Count; i++)
        {
            var pulse = result.Attach<BeaconPulse>(ambience.Windows[i]);
            pulse.BaseColor = result.Image(ambience.Windows[i]).color;
            pulse.MinAlpha = 0.62f;
            pulse.MaxAlpha = 1f;
            pulse.PeriodSeconds = 6.5f + i % 5 * 1.1f;
            pulse.PhaseSeconds = MainMenuAmbience.Repeat01(i * MainMenuAmbience.GoldenStride) * pulse.PeriodSeconds;
        }

        foreach (var lantern in ambience.Lanterns)
        {
            result.Attach<LanternFlicker>(lantern).BaseColor = result.Image(lantern).color;
        }

        // Not LanternFlicker: the gazebo is a lit interior seen through arches
        // from a long way off, not an open flame, so it breathes.
        for (int i = 0; i < ambience.GazeboGlows.Count; i++)
        {
            var pulse = result.Attach<BeaconPulse>(ambience.GazeboGlows[i]);
            pulse.BaseColor = result.Image(ambience.GazeboGlows[i]).color;
            pulse.MinAlpha = 0.7f;
            pulse.MaxAlpha = 1f;
            pulse.PeriodSeconds = 9f + i * 1.7f;
            pulse.PhaseSeconds = i * 3.1f;
        }

        for (int i = 0; i < ambience.Motes.Count; i++)
        {
            var origin = MainMenuAmbience.MoteOrigins[i];
            var mote = result.Attach<MoteDrift>(ambience.Motes[i]);
            mote.BaseColor = result.Image(ambience.Motes[i]).color;
            mote.TravelHeight = MainMenuAmbience.MoteTravel(origin.Y);
            mote.SwayAmplitude = 18f + i % 3 * 9f;
            mote.SwayCycles = 1.2f + i % 4 * 0.35f;
            mote.LifeSeconds = 13f + i % 6 * 1.6f;
            // The pool is evenly spread on the very first frame rather than all
            // launching together and slowly decohering.
            mote.StartProgress = MainMenuAmbience.Repeat01(i * MainMenuAmbience.GoldenStride);
        }

        for (int i = 0; i < ambience.Drifts.Count; i++)
        {
            var drift = result.Attach<SlowDrift>(ambience.Drifts[i]);
            drift.Amplitude = i < MainMenuAmbience.NebulaHaze.Length
                ? new Vector2(46f, 10f)
                : new Vector2(70f, 14f);
            drift.PeriodSeconds = i < MainMenuAmbience.NebulaHaze.Length
                ? MainMenuAmbience.NebulaHaze[i].Period
                : MainMenuAmbience.MistBanks[i - MainMenuAmbience.NebulaHaze.Length].Period;
        }

        // On the PARENT of the background and the ambience, never on the
        // background alone: zooming the painting out from under the glows would
        // slide every one off the lantern it belongs to within seconds.
        result.Attach<KenBurnsDrift>(screen.SceneLayer);
    }

    private static ScreenDef Map()
    {
        MapScreen screen = null;

        return new ScreenDef
        {
            PanelName = "MapPanel",
            ScenePath = MapScene,
            BuildTree = () =>
            {
                screen = MapScreen.Build();
                return screen.Root;
            },
            Wire = result =>
            {
                var map = result.Attach<MapController>(screen.Root);
                map.viewport = result.Rect(screen.Viewport);
                map.content = result.Rect(screen.Content);
                map.backdrops = screen.Backdrops.Select(result.Image).ToArray();
                map.fog = result.Rect(screen.Fog);
                map.walker = result.Image(screen.Walker);
                map.nodeButtons = screen.NodeButtons.Select(result.Button).ToArray();
                map.nodeIcons = screen.NodeIcons.Select(result.Image).ToArray();
                map.nodeLabels = screen.NodeLabels.Select(result.Tmp).ToArray();
                map.nodeMarkers = screen.NodeMarkers.Select(result.Image).ToArray();
                map.trailSegments = screen.TrailSegments.Select(result.Image).ToArray();
                map.trailCores = screen.TrailCores.Select(result.Image).ToArray();
                map.depthLabel = result.Tmp(screen.DepthLabel);
                map.goldLabel = result.Tmp(screen.GoldLabel);
                map.roomMessageLabel = result.Tmp(screen.RoomMessageLabel);
                map.abandonButton = result.Button(screen.AbandonButton);

                // Unlocked, unlike the fight's copy: the map is where
                // changing gear between rooms is supposed to happen.
                map.characterSheetPanel = result.Go(screen.SystemMenu.Root);

                WireSystemMenu(result, screen.Root, screen.SystemMenu, lockedForFight: false,
                    inDescent: true);

                // The painted room icons. Bound here rather than in the
                // controller because "Assets/..." is an editor-only address and
                // MapController lives in Core.
                map.fightIcon = SceneBuilder.LoadSpriteByKey(MapScreen.FightIconKey);
                map.eliteIcon = SceneBuilder.LoadSpriteByKey(MapScreen.EliteIconKey);
                map.bossIcon = SceneBuilder.LoadSpriteByKey(MapScreen.BossIconKey);
                map.restIcon = SceneBuilder.LoadSpriteByKey(MapScreen.RestIconKey);
                map.eventIcon = SceneBuilder.LoadSpriteByKey(MapScreen.EventIconKey);
                map.treasureIcon = SceneBuilder.LoadSpriteByKey(MapScreen.TreasureIconKey);
            },
        };
    }

    // The hub's atmosphere. Same bounded hatch as DressAmbience: it ATTACHES
    // components and never positions anything -- every coordinate came from
    // HubAmbience, which the audit already checked.
    private static void DressHub(UiEmitResult result, HubScreen screen)
    {
        var ambience = screen.Ambience;
        if (ambience == null) return;

        // The whole place breathes. One KenBurnsDrift on the world wrapper
        // rather than one per element: the buildings are staged in the world's
        // own coordinates, so scaling the world moves them together and the
        // parallax stays correct for free.
        var breath = result.Attach<KenBurnsDrift>(screen.World);
        breath.MaxScale = 1.03f;
        breath.ScalePeriodSeconds = 71f;
        breath.PanAmplitude = new Vector2(10f, 6f);
        breath.PanPeriodSeconds = 97f;

        foreach (var star in ambience.Stars)
        {
            result.Attach<StarTwinkle>(star);
        }

        // Fire, on the two things that are actually burning.
        foreach (var brazier in ambience.Braziers)
        {
            result.Attach<LanternFlicker>(brazier).BaseColor = result.Image(brazier).color;
        }

        // Everything else that glows is enchantment, so it pulses rather than
        // flickers -- and each gets its own phase, or the whole hub throbs on
        // one beat like a single machine driving all of it.
        for (int i = 0; i < ambience.Pulses.Count; i++)
        {
            var pulse = result.Attach<BeaconPulse>(ambience.Pulses[i]);
            pulse.BaseColor = result.Image(ambience.Pulses[i]).color;
            pulse.PeriodSeconds = 3.4f + i % 3 * 0.7f;
            pulse.PhaseSeconds = HubAmbience.Repeat01(i * HubAmbience.GoldenStride) * pulse.PeriodSeconds;
            pulse.MinAlpha = 0.55f;
            pulse.MaxAlpha = 1f;
        }

        for (int i = 0; i < ambience.Embers.Count; i++)
        {
            var origin = HubAmbience.EmberOrigins[i];
            var ember = result.Attach<MoteDrift>(ambience.Embers[i]);
            ember.BaseColor = result.Image(ambience.Embers[i]).color;
            ember.TravelHeight = HubAmbience.EmberTravel(origin.Y);
            ember.LifeSeconds = 13f + i % 5 * 1.6f;
            ember.SwayAmplitude = 18f + i % 3 * 7f;
            ember.StartProgress = HubAmbience.Repeat01(i * HubAmbience.GoldenStride);
        }

        // Every building gets a looper. Four of the five have one frame and it
        // silently does nothing for them -- which is the point: a per-building
        // list would need updating every time a sheet gains a frame, and the
        // talents tree's f1/f2 have been dead weight precisely because nobody
        // updated one.
        for (int i = 0; i < screen.BuildingArt.Count; i++)
        {
            var (node, folder) = screen.BuildingArt[i];
            var looper = result.Attach<HubBuildingLooper>(node);
            looper.FramesFolder = folder;
            looper.SecondsPerFrame = 0.55f;
            looper.PhaseSeconds = HubAmbience.Repeat01(i * HubAmbience.GoldenStride) * 1.65f;
        }

        for (int i = 0; i < ambience.Drifts.Count; i++)
        {
            var band = HubAmbience.VoidMist[i];
            var drift = result.Attach<SlowDrift>(ambience.Drifts[i]);
            drift.Amplitude = new Vector2(34f, 11f);
            drift.PeriodSeconds = band.Period;
            drift.PhaseSeconds = HubAmbience.Repeat01(i * HubAmbience.GoldenStride) * band.Period;
        }
    }

    private static ScreenDef Talents()
    {
        TalentScreen screen = null;

        return new ScreenDef
        {
            PanelName = "TalentPanel",
            ScenePath = TalentScene,
            BuildTree = () =>
            {
                screen = TalentScreen.Build();
                return screen.Root;
            },
            Wire = result =>
            {
                var talents = result.Attach<TalentController>(screen.Root);
                talents.sky = result.Rect(screen.Sky);
                talents.orbs = screen.Orbs.Select(result.Button).ToArray();

                // SIX BAKED VARIANTS, one per state. Wired here rather than
                // loaded at runtime because they live under Art/ and not
                // Resources/ -- the same arrangement the map's node icons use.
                //
                // The controller paints STATE BY SPRITE, so these are not
                // decoration: a missing one leaves a stone drawn as whatever it
                // was last, which is the one failure mode worth being loud
                // about. LoadSpriteByKey already logs a miss.
                talents.orbUnlitSprite = SceneBuilder.LoadSpriteByKey(TalentScreen.OrbUnlitKey);
                talents.orbLitSprite = SceneBuilder.LoadSpriteByKey(TalentScreen.OrbLitKey);
                talents.orbAshSprite = SceneBuilder.LoadSpriteByKey(TalentScreen.OrbAshKey);
                talents.orbAshUnauthoredSprite =
                    SceneBuilder.LoadSpriteByKey(TalentScreen.OrbAshUnauthoredKey);
                talents.orbReachableSprite = SceneBuilder.LoadSpriteByKey(TalentScreen.OrbReachableKey);
                talents.orbCostlySprite = SceneBuilder.LoadSpriteByKey(TalentScreen.OrbCostlyKey);

                // A stone's own furniture, in the orbs' own order.
                talents.orbAuras = screen.OrbAuras.Select(result.Image).ToArray();
                talents.orbCores = screen.OrbCores.Select(result.Image).ToArray();
                talents.orbRings = screen.OrbRings.Select(result.Image).ToArray();
                talents.orbLabels = screen.OrbLabels.Select(result.Tmp).ToArray();
                talents.orbPrices = screen.OrbPrices.Select(result.Tmp).ToArray();

                // The gated pair only. These are SHORTER than the arrays above
                // and indexed by their own position -- collarSlots is what maps
                // that position back to a stone.
                talents.collarSlots = screen.CollarSlots.ToArray();
                talents.collarTracks = screen.CollarTracks.Select(result.Image).ToArray();
                talents.collarFills = screen.CollarFills.Select(result.Image).ToArray();
                talents.collarCounts = screen.CollarCounts.Select(result.Tmp).ToArray();

                talents.edgeGlows = screen.EdgeGlows.Select(result.Go).ToArray();
                talents.edgeChildSlots = screen.EdgeChildSlots.ToArray();

                // The motion, attached here because Domain cannot name a
                // MonoBehaviour -- the same bounded escape hatch DressAmbience
                // uses. Baked rather than added at runtime because neither
                // carries a delegate or any state the controller owns: they
                // animate whatever they are on, forever, and a scene that
                // simply HAS them cannot forget to switch them on.
                for (int i = 0; i < screen.EdgeCores.Count; i++)
                {
                    result.Attach<TalentEdgeCrackle>(screen.EdgeCores[i]);
                }

                for (int i = 0; i < screen.EdgeSparks.Count; i++)
                {
                    result.Attach<TalentEdgeSpark>(screen.EdgeSparks[i])
                        .SetLength(screen.EdgeLengths[i]);
                }

                talents.orbGlows = screen.OrbGlows.Select(result.Image).ToArray();
                talents.characterName = result.Tmp(screen.CharacterName);
                talents.pathName = result.Tmp(screen.PathName);
                talents.emberCount = result.Tmp(screen.EmberCount);
                talents.detailName = result.Tmp(screen.DetailName);
                talents.detailBody = result.Tmp(screen.DetailBody);
                talents.panelKicker = result.Tmp(screen.PanelKicker);
                talents.panelPrice = result.Tmp(screen.PanelPrice);
                talents.panelRefusal = result.Tmp(screen.PanelRefusal);
                talents.panelMeterFill = result.Rect(screen.PanelMeterFill);
                talents.investLabel = result.Tmp(screen.InvestLabel);
                talents.investButton = result.Button(screen.InvestButton);

                // THE BACKDROP, driven from the controller rather than by a
                // component each. Both halves of every layer are bound -- the
                // rect that moves and the graphic that fades -- because the
                // fades and the drifts run on different periods and a single
                // handle could only carry one of them.
                talents.starFields = screen.StarFields.Select(result.Rect).ToArray();
                talents.cloudWashes = screen.CloudWashes.Select(result.Rect).ToArray();
                talents.cloudImages = screen.CloudWashes.Select(result.Image).ToArray();
                talents.dustMotes = screen.DustMotes.Select(result.Rect).ToArray();
                talents.dustImages = screen.DustMotes.Select(result.Image).ToArray();
                talents.shootingStars = screen.ShootingStars.Select(result.Rect).ToArray();
                talents.shootingStarImages = screen.ShootingStars.Select(result.Image).ToArray();

                talents.respecButton = result.Button(screen.RespecButton);
                talents.respecDialog = result.Go(screen.RespecDialog);
                talents.respecDialogBody = result.Tmp(screen.RespecDialogBody);
                talents.respecConfirmButton = result.Button(screen.RespecConfirmButton);
                talents.respecCancelButton = result.Button(screen.RespecCancelButton);
                talents.prevPathButton = result.Button(screen.PrevPathButton);
                talents.nextPathButton = result.Button(screen.NextPathButton);
                talents.prevCharacterButton = result.Button(screen.PrevCharacterButton);
                talents.nextCharacterButton = result.Button(screen.NextCharacterButton);
                talents.backButton = result.Button(screen.BackButton);
            },
        };
    }

    // The character overlay: a Modal living inside the hub's own tree.
    //
    // The controller is attached to the MODAL NODE, not to the hub root, so
    // OnEnable fires every time the overlay is opened -- the same arrangement
    // SaveSlotController uses, and the reason a refresh-on-open needs no
    // explicit call from whoever opened it.
    private static void WireCharacterOverlay(UiEmitResult result, HubScreen screen, HubController hub)
    {
        hub.characterOverlayPanel = result.Go(screen.SystemMenu.Root);

        // The hub's overlay, its glossary and its relic draft all sit on
        // Escape-ish paths already, so each is declared as owning Escape ahead
        // of the overarching menu.
        WireSystemMenu(result, screen.Root, screen.SystemMenu, lockedForFight: false,
            inDescent: false, screen.Glossary.Root, screen.Draft.Root);
    }

    // The overarching menu's wiring, shared by every scene that carries one.
    //
    // `escapeConsumers` is the only per-scene difference: the fight and the map
    // already put the character sheet on Escape, and this menu must not open on
    // top of a sheet the player is trying to close. Passed in rather than found
    // by name, so a scene that grows another Escape-owning panel declares it
    // here instead of the menu guessing.
    private static SystemMenuController WireSystemMenu(
        UiEmitResult result, NodeRef host, SystemMenuScreen menu, bool lockedForFight,
        bool inDescent, params NodeRef[] escapeConsumers)
    {
        // ATTACHED TO THE SCENE ROOT, not to the menu it drives.
        //
        // It was on the menu's own modal, which is Inactive until the menu
        // opens -- so its Update() did not run while the menu was closed, and
        // the Escape that is supposed to OPEN it could never fire. Escape only
        // ever closed a menu that something else had already opened.
        //
        // That is why it survived a design pass that lists "Escape opens the
        // menu" as built: every screenshot and every test opens it by calling
        // Open() directly, and legacy Input cannot be pressed headlessly, so
        // nothing that runs in CI was ever in a position to notice.
        //
        // SystemMenuTests.TheMenuIsListeningWhileItIsClosed pins it now.
        var controller = result.Attach<SystemMenuController>(host);

        controller.panel = result.Go(menu.Root);
        controller.tabButtons = menu.TabButtons.Select(result.Button).ToArray();
        controller.tabHovers = menu.TabHovers.Select(result.Go).ToArray();
        controller.tabUnderlines = menu.TabUnderlines.Select(result.Go).ToArray();
        controller.tabDividers = menu.TabDividers.Select(result.Go).ToArray();
        controller.panes = menu.Panes.Select(result.Go).ToArray();
        controller.escapeConsumers = escapeConsumers.Select(result.Go).ToArray();

        // Decided here rather than sniffed at runtime: the hub is not a
        // descent, and no state can make it one.
        controller.inDescent = inDescent;

        controller.runTitle = result.Tmp(menu.RunTitle);
        controller.contextLine = result.Tmp(menu.ContextLine);
        controller.goldValue = result.Tmp(menu.GoldValue);
        controller.embersValue = result.Tmp(menu.EmbersValue);
        controller.closeButton = result.Button(menu.CloseButton);

        if (menu.Dossier != null) WireDossier(result, menu.Dossier, lockedForFight);
        if (menu.RewardTrack != null)
        {
            WireRewardTrack(result, menu.RewardTrack);

            // The dossier's door into it. Bound here rather than in WireDossier
            // because it is the one thing the dossier needs that lives on
            // another screen.
            if (menu.Dossier != null)
            {
                var dossierController = result.Go(menu.Dossier.Root).GetComponent<CharacterDossierController>();
                if (dossierController != null)
                {
                    dossierController.trackRow = result.Button(menu.Dossier.TrackRow);
                    dossierController.trackPanel = result.Go(menu.RewardTrack.Root);
                }
            }
        }
        if (menu.Options != null) WireOptions(result, menu.Options);
        if (menu.RunStats != null) WireRunStats(result, menu.RunStats);
        if (menu.Exits != null) WireExits(result, menu.Exits, controller);

        return controller;
    }

    // The Run statistics pane, inside the system menu.
    //
    // Keyed like the Options pane's controls rather than index-aligned with
    // RunStatRows: both arrays are built off the SAME declared list here, so
    // they agree by construction, and the key travels with the label so a
    // reordering of the table cannot silently swap two four-digit figures.
    private static RunStatsController WireRunStats(UiEmitResult result, RunStatsScreen stats)
    {
        var controller = result.Attach<RunStatsController>(stats.Root);

        controller.valueKeys = stats.ValueKeys.ToArray();
        controller.values = stats.Values.Select(result.Tmp).ToArray();

        return controller;
    }

    // The Main menu pane, inside the system menu.
    //
    // It is handed the MENU'S OWN CONTROLLER rather than a copy of its context
    // flag: the pane has to close the menu before it navigates -- closing is
    // what puts Time.timeScale back -- and it asks the same object whether this
    // scene is a descent, so the abandon card and the tab bar cannot disagree.
    private static ExitsController WireExits(
        UiEmitResult result, ExitsScreen exits, SystemMenuController menu)
    {
        var controller = result.Attach<ExitsController>(exits.Root);

        controller.exitButtons = exits.ExitButtons.Select(result.Button).ToArray();
        controller.exitHovers = exits.ExitHovers.Select(result.Go).ToArray();

        // The label is a CHILD of the button, which result.Tmp already looks
        // down one level for -- so the same NodeRef serves both the click and
        // the text swap, and there is no second handle to keep in step.
        controller.exitLabels = exits.ExitButtons.Select(result.Tmp).ToArray();

        controller.exitBlocks = exits.ExitBlocks.Select(result.Rect).ToArray();
        controller.separator = result.Go(exits.Separator);
        controller.abandonCard = result.Go(exits.AbandonCard);
        controller.abandonHold = result.Button(exits.AbandonHold);
        controller.abandonFill = result.Rect(exits.AbandonFill);
        controller.menu = menu;

        return controller;
    }

    // The Options pane, inside the system menu.
    //
    // Sliders and steppers are wired as two DENSE sets, each carrying the
    // setting key its controls belong to. The first attempt padded one set of
    // arrays with nulls to keep every index matching OptionRows.AllRows, and
    // UiWiringSweep refused it -- correctly, because a null element is
    // indistinguishable from a reference somebody forgot.
    private static OptionsController WireOptions(UiEmitResult result, OptionsScreen options)
    {
        var controller = result.Attach<OptionsController>(options.Root);

        controller.rowHovers = options.RowHovers.Select(result.Go).ToArray();

        controller.sliderKeys = options.SliderKeys.ToArray();
        controller.sliderTracks = options.SliderTracks.Select(result.Image).ToArray();
        controller.sliderFills = options.SliderFills.Select(result.Rect).ToArray();
        controller.sliderValues = options.SliderValues.Select(result.Tmp).ToArray();

        controller.stepperKeys = options.StepperKeys.ToArray();
        controller.stepPrev = options.StepPrev.Select(result.Button).ToArray();
        controller.stepNext = options.StepNext.Select(result.Button).ToArray();
        controller.stepperValues = options.StepperValues.Select(result.Tmp).ToArray();

        controller.restoreDefaults = result.Button(options.RestoreDefaults);

        return controller;
    }

    // The character dossier, inside the system menu's Character pane.
    //
    // Bound with the SAME icon arrays every other item surface uses, from every
    // ItemDefinition with an authored iconPath -- an item whose art is missing
    // disables its Image rather than painting a white quad.
    // The reward track, inside the system menu's own pane.
    //
    // Three parallel arrays of a hundred, which is the largest declared count
    // in the project -- and the reason they are arrays rather than a lookup is
    // that index i IS level i + FirstLevel by construction, so neither the
    // screen nor the controller ever searches for a node.
    private static RewardTrackController WireRewardTrack(
        UiEmitResult result, RewardTrackScreen track)
    {
        var controller = result.Attach<RewardTrackController>(track.Root);

        controller.viewport = result.Rect(track.Viewport);
        controller.content = result.Rect(track.Content);
        controller.railFill = result.Rect(track.RailFill);
        controller.railGlowFill = result.Rect(track.RailGlowFill);

        // THE FOUR NODES THAT MOVE, each bound twice -- once as the Image whose
        // colour is animated and once as the RectTransform that is moved or
        // scaled. Two components of one GameObject, so no lookup is being done
        // twice; this is the alternative to a GetComponent in an Update that
        // runs sixty times a second for the life of the panel.
        controller.shimmer = result.Image(track.Shimmering);
        controller.shimmerRect = result.Rect(track.Shimmering);
        controller.hereHalo = result.Image(track.HereHalo);
        controller.hereHaloRect = result.Rect(track.HereHalo);
        controller.nextMark = result.Rect(track.NextMark);
        controller.burstRoots = track.BurstRoots.Select(result.Rect).ToArray();
        controller.burstCores = track.BurstCores.Select(result.Image).ToArray();
        controller.burstRings = track.BurstRings.Select(result.Image).ToArray();
        controller.burstRays = track.BurstRays.Select(result.Image).ToArray();
        controller.burstSparks = track.BurstSparks.Select(result.Image).ToArray();

        controller.summaryLevel = result.Tmp(track.SummaryLevel);
        controller.summaryNextAt = result.Tmp(track.SummaryNextAt);
        controller.summaryReward = result.Tmp(track.SummaryReward);
        controller.collectButton = result.Button(track.CollectButton);
        controller.collectLabel = result.Tmp(track.CollectCaption);
        controller.collectPip = result.Image(track.CollectPip);
        controller.closeButton = result.Button(track.CloseButton);

        controller.cardRect = result.Rect(track.Card);
        controller.cardMat = result.Image(track.CardMat);
        controller.cardArt = result.Image(track.CardArt);
        controller.cardKicker = result.Tmp(track.CardKicker);
        controller.cardLevel = result.Tmp(track.CardLevel);
        controller.cardCaption = result.Tmp(track.CardCaption);
        controller.cardState = result.Tmp(track.CardState);
        controller.cardStateDot = result.Image(track.CardStateDot);

        // ONE ENTRY PER LEVEL, resolved here rather than at runtime: the
        // controller lives in the runtime assembly and LoadSpriteByKey is
        // editor-only, so these bake into the scene like every other art
        // reference. LoadSpriteByKey caches, so ninety-nine calls are twelve
        // loads and eighty-seven dictionary hits.
        controller.cardArtByLevel = Enumerable
            .Range(RewardTrackLayout.FirstLevel, RewardTrackLayout.NodeCount)
            .Select(level => SceneBuilder.LoadSpriteByKey(RewardTrackLayout.CardArtFor(level)))
            .ToArray();

        controller.ribbon = result.Rect(track.Ribbon);
        controller.ribbonFill = result.Rect(track.RibbonFill);
        controller.ribbonPlayhead = result.Rect(track.RibbonPlayhead);
        controller.ribbonWindow = result.Rect(track.RibbonWindow);
        controller.ribbonGrab = result.Button(track.RibbonGrab);

        // The disc is bound as BOTH the Button that takes the click and the
        // Image that carries the state's colour, for the same reason as above:
        // it is one GameObject wearing both, and the controller needs each on a
        // different path -- one at wiring time to attach a listener, one per
        // repaint.
        controller.dots = track.Dots.Select(result.Button).ToArray();
        controller.discs = track.Dots.Select(result.Image).ToArray();

        controller.rings = track.Rings.Select(result.Image).ToArray();
        controller.pulses = track.Pulses.Select(result.Image).ToArray();
        controller.mats = track.Mats.Select(result.Image).ToArray();
        controller.icons = track.Icons.Select(result.Image).ToArray();
        controller.seals = track.Seals.Select(result.Go).ToArray();
        controller.captions = track.Captions.Select(result.Tmp).ToArray();
        controller.levelNumbers = track.LevelNumbers.Select(result.Tmp).ToArray();

        controller.milestoneAuras = track.MilestoneAuras.Select(result.Image).ToArray();
        controller.milestoneRings = track.MilestoneRings.Select(result.Image).ToArray();

        controller.ribbonTicks = track.RibbonTicks.Select(result.Image).ToArray();
        controller.ribbonDots = track.RibbonDots.Select(result.Image).ToArray();
        controller.ribbonNumbers = track.RibbonNumbers.Select(result.Tmp).ToArray();

        // NO CountBindings for any of these, and it is worth saying why rather
        // than leaving the absence to be read as an oversight. E4 exists to
        // catch a strip sized from one collection and filled from another;
        // every array above is built by Select over the very list the nodes
        // were appended to, so the binding it would check is the same count
        // compared with itself. The reward track also has no ScreenDef of its
        // own -- it lives inside the system menu, which three scenes each build
        // through WireSystemMenu -- so declaring one would mean threading a
        // bindings list through all three to assert n == n.
        return controller;
    }

    private static CharacterDossierController WireDossier(
        UiEmitResult result, CharacterDossierScreen dossier, bool lockedForFight)
    {
        var controller = result.Attach<CharacterDossierController>(dossier.Root);
        controller.lockedForFight = lockedForFight;

        controller.characterName = result.Tmp(dossier.CharacterName);
        controller.subLine = result.Tmp(dossier.SubLine);
        controller.xpFill = result.Rect(dossier.XpFill);
        controller.xpRemaining = result.Tmp(dossier.XpRemaining);
        controller.trackNext = result.Tmp(dossier.TrackNext);
        controller.unspentPoints = result.Tmp(dossier.UnspentPoints);
        controller.attributePluses = dossier.AttributePluses.Select(result.Button).ToArray();
        controller.prevCharacterButton = result.Button(dossier.PrevCharacterButton);
        controller.nextCharacterButton = result.Button(dossier.NextCharacterButton);

        controller.skillsCount = result.Tmp(dossier.SkillsCount);
        controller.packRow = result.Button(dossier.PackRow);
        controller.packChevron = result.Tmp(dossier.PackChevron);
        controller.packPanel = result.Go(dossier.PackPanel);
        controller.packCloseButton = result.Button(dossier.PackCloseButton);

        controller.slotCells = dossier.SlotCells.Select(result.Button).ToArray();
        controller.slotIcons = dossier.SlotIcons.Select(result.Image).ToArray();
        controller.slotLabels = dossier.SlotLabels.Select(result.Tmp).ToArray();
        controller.slotBlockedCaptions = dossier.SlotBlockedCaptions.Select(result.Go).ToArray();

        controller.attributeCells = dossier.AttributeCells.Select(result.Button).ToArray();
        controller.attributeValues = dossier.AttributeValues.Select(result.Tmp).ToArray();
        controller.attributeKeys = dossier.AttributeKeys.Select(result.Tmp).ToArray();

        controller.slotRarityTicks = dossier.SlotRarityTicks.Select(result.Image).ToArray();
        controller.slotRiftGlows = dossier.SlotRiftGlows.Select(result.Image).ToArray();

        controller.packCells = dossier.PackCells.Select(result.Button).ToArray();
        controller.packIcons = dossier.PackIcons.Select(result.Image).ToArray();
        controller.packRarityTicks = dossier.PackRarityTicks.Select(result.Image).ToArray();
        controller.packRiftGlows = dossier.PackRiftGlows.Select(result.Image).ToArray();
        controller.packCounts = dossier.PackCounts.Select(result.Tmp).ToArray();
        controller.packNames = dossier.PackNames.Select(result.Tmp).ToArray();
        controller.packSortTabs = dossier.PackFilterTabs.Select(result.Button).ToArray();
        controller.packSortUnderlines = dossier.PackSortUnderlines.Select(result.Go).ToArray();
        controller.packScrollTrack = result.Image(dossier.PackScrollTrack);
        controller.packScrollThumb = result.Rect(dossier.PackScrollThumb);
        controller.carriedValue = result.Tmp(dossier.CarriedValue);

        controller.tooltip = result.Go(dossier.Tooltip);
        controller.tooltipTitle = result.Tmp(dossier.TooltipTitle);
        controller.tooltipBody = result.Tmp(dossier.TooltipBody);

        controller.statValues = dossier.StatValues.Select(result.Tmp).ToArray();
        controller.statPreviews = dossier.StatPreviews.Select(result.Tmp).ToArray();
        controller.statHighlights = dossier.StatHighlights.Select(result.Go).ToArray();

        controller.portrait = result.Image(dossier.Portrait);

        // Every character with an authored portrait. portraitPath is
        // Assets-relative by convention (ArtPathConvention), which is exactly
        // what LoadSpriteByKey takes, so these bake into the scene like every
        // other piece of menu art rather than loading at runtime.
        var faces = ContentDatabase.Characters
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.portraitPath))
            .ToList();
        controller.portraits = faces
            .Select(c => new IconEntry(c.id, SceneBuilder.LoadSpriteByKey(c.portraitPath)))
            .ToArray();

        var withArt = ContentDatabase.Items
            .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
            .ToList();
        controller.icons = withArt
            .Select(i => new IconEntry(i.id, SceneBuilder.LoadSpriteByKey(i.iconPath)))
            .ToArray();

        return controller;
    }

    // The controller sits on the MODAL NODE, not on the thing it opens -- so
    // its OnEnable fires when the overlay opens, and the animation is done to a
    // child it holds a reference to rather than to itself.
    private static ReckoningController WireReckoning(UiEmitResult result, ReckoningScreen screen)
    {
        var controller = result.Attach<ReckoningController>(screen.Root);

        // The WIPE MASK, deliberately, and the painted frame is not bound at
        // all. The controller has no business reaching the frame: everything it
        // used to do to it -- scale it, lift it -- is now done to the mask
        // around it, and a reference it does not hold cannot be squashed by
        // accident a second time.
        controller.frameWipe = result.Rect(screen.FrameWipe);

        // The Continue arrow's heat, animated. Attached rather than wired into
        // the controller: it needs no state from the Reckoning, and a screen
        // controller that also owns an ambient loop is the shape every other
        // ambience component here was pulled OUT of.
        result.Attach<EmberFlare>(screen.ContinueGlow);
        controller.goldLabel = result.Tmp(screen.GoldLabel);
        controller.continueButton = result.Button(screen.ContinueButton);

        controller.offerTooltip = result.Go(screen.OfferTooltip);
        controller.offerTooltipText = result.Tmp(screen.OfferTooltipText);
        controller.offerHalos = screen.OfferHalos.Select(result.Image).ToArray();
        controller.offerBursts = screen.OfferBursts.Select(result.Image).ToArray();
        controller.offerBurstRects = screen.OfferBursts.Select(result.Rect).ToArray();
        controller.offerIcons = screen.OfferIcons.Select(result.Image).ToArray();
        controller.offerRiftGlows = screen.OfferRiftGlows.Select(result.Image).ToArray();

        // Every item that authored an icon, baked as two parallel arrays.
        // Resolved here rather than at runtime because Resources loading and
        // AssetDatabase are different worlds and only the builder has the
        // second one.
        var offerArt = ContentDatabase.Items
            .Where(i => i != null && !string.IsNullOrEmpty(i.iconPath))
            .ToList();

        controller.icons = offerArt
            .Select(i => new IconEntry(i.id, SceneBuilder.LoadSpriteByKey(i.iconPath)))
            .ToArray();

        controller.tabButtons = screen.TabButtons.Select(result.Button).ToArray();
        controller.tabMarkers = screen.TabMarkers.Select(result.Image).ToArray();
        controller.pages = screen.Pages.Select(result.Go).ToArray();

        // The modal's own dimmer, so the gloom can be faded up rather than
        // snapped on. Ui.Modal builds it; the screen holds the ref.
        controller.dimmer = result.Image(screen.Dimmer);
        controller.frameGlow = result.Image(screen.FrameGlow);
        controller.offerPhase = result.Rect(screen.OfferPhase);
        controller.summaryPhase = result.Rect(screen.SummaryPhase);

        controller.relicEmptyHint = result.Go(screen.RelicEmptyHint);
        controller.relicRows = screen.RelicRows.Select(result.Go).ToArray();
        controller.relicNames = screen.RelicNames.Select(result.Tmp).ToArray();
        controller.relicMetas = screen.RelicMetas.Select(result.Tmp).ToArray();
        controller.relicBodies = screen.RelicBodies.Select(result.Tmp).ToArray();

        controller.tallyRows = screen.TallyRows.Select(result.Go).ToArray();
        controller.tallyNames = screen.TallyNames.Select(result.Tmp).ToArray();
        controller.tallyStats = screen.TallyStats.Select(result.Tmp).ToArray();
        controller.tallyKills = screen.TallyKills.Select(result.Tmp).ToArray();
        controller.lootHeading = result.Go(screen.LootHeading);

        controller.rowGroups = screen.RowGroups.Select(result.Go).ToArray();
        controller.rowNames = screen.RowNames.Select(result.Tmp).ToArray();
        controller.rowLevels = screen.RowLevels.Select(result.Tmp).ToArray();
        controller.rowBarFills = screen.RowBarFills.Select(result.Image).ToArray();
        controller.rowBarBefores = screen.RowBarBefores.Select(result.Image).ToArray();
        controller.rowGains = screen.RowGains.Select(result.Tmp).ToArray();

        controller.offerButtons = screen.OfferButtons.Select(result.Button).ToArray();
        controller.offerRects = screen.OfferButtons.Select(result.Rect).ToArray();
        controller.rerollButton = result.Button(screen.RerollButton);
        // Require<T> looks one level down, which is where a button keeps its
        // caption -- so the label needs no NodeRef of its own.
        controller.rerollLabel = result.Tmp(screen.RerollButton);
        controller.offerNames = screen.OfferNames.Select(result.Tmp).ToArray();
        controller.offerMetas = screen.OfferMetas.Select(result.Tmp).ToArray();

        return controller;
    }

    private static DefeatController WireDefeat(UiEmitResult result, DefeatScreen screen)
    {
        var controller = result.Attach<DefeatController>(screen.Root);

        controller.frame = result.Rect(screen.Frame);
        controller.goldLostLabel = result.Tmp(screen.GoldLostLabel);
        controller.embersLabel = result.Tmp(screen.EmbersLabel);
        controller.depthLabel = result.Tmp(screen.DepthLabel);
        controller.expLabel = result.Tmp(screen.ExpLabel);

        controller.rowGroups = screen.RowGroups.Select(result.Go).ToArray();
        controller.rowNames = screen.RowNames.Select(result.Tmp).ToArray();
        controller.rowStats = screen.RowStats.Select(result.Tmp).ToArray();

        controller.returnButton = result.Button(screen.ReturnButton);
        controller.inspectButton = result.Button(screen.InspectButton);

        return controller;
    }

    private static void WireGlossary(UiEmitResult result, HubScreen screen, HubController hub)
    {
        var glossary = screen.Glossary;
        var controller = result.Attach<GlossaryController>(glossary.Root);

        controller.categoryButtons = glossary.CategoryButtons.Select(result.Button).ToArray();
        controller.categoryMarkers = glossary.CategoryMarkers.Select(result.Image).ToArray();
        controller.categoryLabels = glossary.CategoryLabels.Select(result.Tmp).ToArray();
        controller.categoryCounts = glossary.CategoryCounts.Select(result.Tmp).ToArray();

        controller.rows = glossary.Rows.Select(result.Button).ToArray();
        controller.rowMarkers = glossary.RowMarkers.Select(result.Image).ToArray();
        controller.rowNames = glossary.RowNames.Select(result.Tmp).ToArray();
        controller.rowMetas = glossary.RowMetas.Select(result.Tmp).ToArray();

        controller.emptyHint = result.Go(glossary.EmptyHint);
        controller.pageLabel = result.Tmp(glossary.PageLabel);
        controller.prevPageButton = result.Button(glossary.PrevPageButton);
        controller.nextPageButton = result.Button(glossary.NextPageButton);

        controller.detailIcon = result.Image(glossary.DetailIcon);
        controller.detailName = result.Tmp(glossary.DetailName);
        controller.detailMeta = result.Tmp(glossary.DetailMeta);
        controller.detailBody = result.Tmp(glossary.DetailBody);
        controller.detailLockedBy = result.Go(glossary.DetailLockedBy);
        controller.detailLockedByLabel = result.Tmp(glossary.DetailLockedBy);
        controller.closeButton = result.Button(glossary.CloseButton);

        // Icons for EVERY category at once, in two parallel arrays. Items and
        // relics are the only two with art today; the rest resolve to nothing
        // and ItemIcons disables the Image, which is the same graceful posture
        // the character overlay already takes.
        var withArt = ContentDatabase.Items
            .Where(i => i != null && !string.IsNullOrEmpty(i.iconPath))
            .Select(i => (i.id, i.iconPath))
            .Concat(ContentDatabase.Relics
                .Where(r => r != null && !string.IsNullOrEmpty(r.iconPath))
                .Select(r => (r.id, r.iconPath)))
            .ToList();

        controller.icons = withArt
            .Select(a => new IconEntry(a.Item1, SceneBuilder.LoadSpriteByKey(a.Item2)))
            .ToArray();

        hub.glossaryPanel = result.Go(glossary.Root);
    }

    private static void WireRelicDraft(UiEmitResult result, HubScreen screen, HubController hub)
    {
        var draft = screen.Draft;
        var controller = result.Attach<RelicDraftController>(draft.Root);

        controller.cards = draft.Cards.Select(result.Button).ToArray();
        controller.cardSelections = draft.CardSelections.Select(result.Image).ToArray();
        controller.cardIcons = draft.CardIcons.Select(result.Image).ToArray();
        controller.cardNames = draft.CardNames.Select(result.Tmp).ToArray();
        controller.cardRarities = draft.CardRarities.Select(result.Tmp).ToArray();
        controller.cardBodies = draft.CardBodies.Select(result.Tmp).ToArray();
        controller.cardHalos = draft.CardHalos.Select(result.Image).ToArray();
        controller.cardBursts = draft.CardBursts.Select(result.Image).ToArray();

        controller.emptyHint = result.Go(draft.EmptyHint);
        controller.descendButton = result.Button(draft.DescendButton);
        controller.prevPageButton = result.Button(draft.PrevPageButton);
        controller.nextPageButton = result.Button(draft.NextPageButton);
        controller.pageLabel = result.Tmp(draft.PageLabel);

        // Relic icons, baked as two parallel arrays. Resolved here rather than
        // at runtime because Resources loading and AssetDatabase are different
        // worlds and only the builder has the second one.
        var withArt = ContentDatabase.Relics
            .Where(r => r != null && !string.IsNullOrEmpty(r.iconPath))
            .ToList();

        controller.icons = withArt
            .Select(r => new IconEntry(r.id, SceneBuilder.LoadSpriteByKey(r.iconPath)))
            .ToArray();

        hub.relicDraft = controller;
    }

    private static void WireDebugMenu(UiEmitResult result, HubScreen screen, HubController hub)
    {
        var debug = screen.Debug;
        var controller = result.Attach<DebugMenuController>(debug.Root);

        controller.closeButton = result.Button(debug.CloseButton);
        controller.giveGoldButton = result.Button(debug.GiveGoldButton);
        controller.giveEmbersButton = result.Button(debug.GiveEmbersButton);
        controller.giveOneEmberButton = result.Button(debug.GiveOneEmberButton);

        controller.filterButtons = debug.FilterButtons.Select(result.Button).ToArray();
        controller.rowButtons = debug.RowButtons.Select(result.Button).ToArray();
        controller.rowLabels = debug.RowLabels.Select(result.Tmp).ToArray();

        controller.pageLabel = result.Tmp(debug.PageLabel);
        controller.prevPageButton = result.Button(debug.PrevPageButton);
        controller.nextPageButton = result.Button(debug.NextPageButton);

        hub.debugMenuPanel = result.Go(debug.Root);
    }
}
