using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PrincesPalace;
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

    public static readonly List<ScreenDef> All = new List<ScreenDef>
    {
        MainMenu(),
        Hub(),
        Map(),
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
                fight.enemySprites = screen.EnemySprites.Select(result.Image).ToArray();
                fight.enemyHitFlashes = screen.EnemyHitFlashes.Select(result.Image).ToArray();
                fight.enemyNameplates = screen.EnemyNameplates.Select(result.Tmp).ToArray();
                fight.enemyFootShadows = screen.EnemyFootShadows.Select(result.Image).ToArray();

                fight.partySlots = screen.PartySlots.Select(result.Rect).ToArray();
                fight.partySprites = screen.PartySprites.Select(result.Image).ToArray();
                fight.partyHitFlashes = screen.PartyHitFlashes.Select(result.Image).ToArray();
                fight.partyNameplates = screen.PartyNameplates.Select(result.Tmp).ToArray();
                fight.partyFootShadows = screen.PartyFootShadows.Select(result.Image).ToArray();

                fight.initiativeIcons = screen.InitiativeIcons.Select(result.Image).ToArray();
                fight.initiativeRings = screen.InitiativeRings.Select(result.Image).ToArray();
                fight.initiativeLabels = screen.InitiativeLabels.Select(result.Tmp).ToArray();

                fight.barkPortrait = result.Image(screen.BarkPortrait);
                fight.barkLabel = result.Tmp(screen.BarkLabel);

                fight.enemiesHint = result.Tmp(screen.EnemiesHint);
                fight.enemyPlates = screen.EnemyPlates.Select(result.Button).ToArray();
                fight.enemyPlateNames = screen.EnemyPlateNames.Select(result.Tmp).ToArray();
                fight.enemyPlateHps = screen.EnemyPlateHps.Select(result.Tmp).ToArray();
                fight.enemyPlateHpFills = screen.EnemyPlateHpFills.Select(result.Image).ToArray();
                fight.enemyPlateTags = screen.EnemyPlateTags.Select(result.Tmp).ToArray();
                fight.enemyPlateReticles = screen.EnemyPlateReticles.Select(result.Go).ToArray();

                fight.partyPortrait = result.Image(screen.PartyPortrait);
                fight.partyName = result.Tmp(screen.PartyName);
                fight.partyClass = result.Tmp(screen.PartyClass);
                fight.partyHpFill = result.Image(screen.PartyHpFill);
                fight.partyHpValue = result.Tmp(screen.PartyHpValue);
                fight.partyMpFill = result.Image(screen.PartyMpFill);
                fight.partyMpValue = result.Tmp(screen.PartyMpValue);
                fight.partyMpPreview = result.Rect(screen.PartyMpPreview);
                fight.woolPips = screen.WoolPips.Select(result.Image).ToArray();
                fight.woolValue = result.Tmp(screen.WoolValue);

                fight.verbButtons = screen.VerbButtons.Select(result.Button).ToArray();
                fight.verbLabels = screen.VerbLabels.Select(result.Tmp).ToArray();
                fight.verbCarets = screen.VerbCarets.Select(result.Go).ToArray();
                fight.breadcrumb = result.Tmp(screen.Breadcrumb);
                fight.continueButton = result.Button(screen.ContinueButton);

                fight.submenuColumn = result.Go(screen.SubmenuColumn);
                fight.submenuTitle = result.Tmp(screen.SubmenuTitle);
                fight.submenuHint = result.Tmp(screen.SubmenuHint);
                fight.submenuBackButton = result.Button(screen.SubmenuBackButton);
                fight.submenuRows = screen.SubmenuRows.Select(result.Button).ToArray();
                fight.submenuRowRects = screen.SubmenuRows.Select(result.Rect).ToArray();
                fight.submenuNames = screen.SubmenuNames.Select(result.Tmp).ToArray();
                fight.submenuMetas = screen.SubmenuMetas.Select(result.Tmp).ToArray();
                fight.submenuCosts = screen.SubmenuCosts.Select(result.Tmp).ToArray();

                fight.detailColumn = result.Go(screen.DetailColumn);
                fight.detailName = result.Tmp(screen.DetailName);
                fight.detailKind = result.Tmp(screen.DetailKind);
                fight.detailBody = result.Tmp(screen.DetailBody);
                fight.detailStatValues = screen.DetailStatValues.Select(result.Tmp).ToArray();

                fight.targetPrompt = result.Go(screen.TargetPrompt);
                fight.targetPromptLabel = result.Tmp(screen.TargetPromptLabel);

                fight.spellVfx = result.Image(screen.SpellVfx);

                // The player lives on the VFX node itself, so disabling that
                // node is a real stop rather than something the controller has
                // to remember -- the same arrangement FightBeatPlayer has with
                // the popup pool.
                var vfx = result.Attach<SpellVfxPlayer>(screen.SpellVfx);
                vfx.image = fight.spellVfx;

                // Never eats a click meant for an enemy behind it. The effect is
                // drawn ABOVE the stage, so without this a spell in flight would
                // swallow the next target selection.
                vfx.image.raycastTarget = false;
                vfx.image.preserveAspect = true;
                vfx.image.enabled = false;
                fight.spellVfxPlayer = vfx;
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
                foreach (var slotRef in screen.EnemySlots.Concat(screen.PartySlots))
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

                // The four whose screens do not exist yet. This array is the
                // one place that list lives, and it shrinks as they land.
                hub.unbuiltButtons = new[]
                {
                    result.Button(screen.TalentsButton),
                    result.Button(screen.PrincipalityButton),
                    result.Button(screen.CharacterSheetButton),
                    result.Button(screen.RelicsButton),
                };
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
                menu.optionsButton = result.Button(screen.OptionsButton);
                menu.exitButton = result.Button(screen.ExitButton);
                menu.closeSaveSlotButton = result.Button(screen.CloseSaveSlotButton);
                menu.closeOptionsButton = result.Button(screen.CloseOptionsButton);
                menu.saveSlotPanel = result.Go(screen.SaveSlotPanel);
                menu.optionsPanel = result.Go(screen.OptionsPanel);

                slots = result.Attach<SaveSlotController>(screen.SaveSlotPanel);
                slots.slotButtons = screen.SlotButtons.Select(result.Button).ToArray();

                reset = result.Attach<ResetProgressController>(screen.OptionsPanel);
                reset.deleteButtons = screen.DeleteButtons.Select(result.Button).ToArray();
                reset.slotLabels = screen.SlotLabels.Select(result.Tmp).ToArray();
                reset.confirmPanel = result.Go(screen.ResetConfirmPanel);
                reset.confirmLabel = result.Tmp(screen.ResetConfirmLabel);
                reset.confirmYesButton = result.Button(screen.ResetConfirmYesButton);
                reset.confirmNoButton = result.Button(screen.ResetConfirmNoButton);

                // NOT subscribing SaveSlotController.Refresh to a C# event here.
                // This Wire step runs at BUILD time and delegates do not
                // serialise, so the subscription would silently not exist in the
                // shipped scene -- and the wiring sweep could not catch it,
                // because it only sees serialized object references. The Play
                // panel refreshes in OnEnable instead, which covers the only
                // path that matters: delete in Options, reopen Play.
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
                new UiCountAudit.Binding
                {
                    Label = "ResetProgressController.slotLabels",
                    Declared = screen.SlotLabels,
                    BoundLength = () => reset.slotLabels.Length,
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
                map.nodeButtons = screen.NodeButtons.Select(result.Button).ToArray();
                map.nodeLabels = screen.NodeLabels.Select(result.Tmp).ToArray();
                map.nodeMarkers = screen.NodeMarkers.Select(result.Image).ToArray();
                map.depthLabel = result.Tmp(screen.DepthLabel);
                map.goldLabel = result.Tmp(screen.GoldLabel);
                map.abandonButton = result.Button(screen.AbandonButton);
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
}
