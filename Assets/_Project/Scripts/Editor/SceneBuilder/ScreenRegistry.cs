using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Progression;
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

    // Gamepad navigation (docs/GAMEPAD_NAVIGATION_PLAN.md section 9a). Read
    // AFTER BuildTree so a closure over the same screen-local variable BuildTree
    // populated can hand back a declaration built from THIS tree's own node
    // instances, never a stale one. Null is the ordinary case in phase 2 --
    // most screens have not been given a declaration yet, and UiAudit.
    // CheckNavigable / UiNavControlsAudit / UiNavWiring all treat null as
    // "not checked", not "checked and empty".
    public Func<UiNavDeclaration> Nav;
}

// The ONE list of screens in the game.
//
// It drives scene assembly, the build-time audits, and the screenshot tool. v1
// had a separate hand-maintained KnownPanels table in ScreenshotTool plus a
// third copy in screenshot.ps1's usage text, with a documented "nothing keeps
// these in sync" hazard. There is one list now, so a screen cannot exist in the
// game and be invisible to its own tooling.
//
// WHAT A Wire STEP STILL WRITES BY HAND. UiAutoBind, called once per attached
// controller, binds every [SerializeField] whose own identifier mirrors a
// NodeRef on the screen -- which was 372 lines of `ctrl.x = result.T(screen.X)`
// here. Four shapes it cannot see, and every explicit assignment left in this
// file is one of them:
//   - the NAME DIFFERS, deliberately (exitLabels from ExitButtons, discs from
//     Dots), or one node is wanted through two components (shimmer and
//     shimmerRect, submenuRows and submenuRowRects);
//   - the value comes from a SUB-OBJECT of a screen-side card or row struct
//     (Shop's cards, MainMenu's slot cards), which is not a NodeRef field;
//   - the value is not a NodeRef at all -- a Sprite off LoadSpriteByKey, an
//     IconEntry[] built from ContentDatabase, another controller;
//   - the node lives on a DIFFERENT screen object than the one being bound
//     (a nested screen's Root, the dossier's door into the reward track).
// A field the binder misses and nobody writes here fails the build: see
// UiWiringSweep, which reads the serialized view of every attached controller.
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

    // The battle screen. The largest tree in the game, and the one the binder
    // does the most for: seventy-eight of FightController's fields mirror a
    // NodeRef name, so what survives below is only what a name cannot say.
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
                // ContentDatabase.Characters.Count, not the 3-character
                // compatibility default HubScreen/MapScreen/FightScreen.Build
                // fall back to when called with no argument -- see
                // SystemMenuScreen.Build's own partyRosterCardCount comment.
                screen = FightScreen.Build(ContentDatabase.Characters.Count);
                return screen.Root;
            },
            Wire = result =>
            {
                fight = result.Attach<FightController>(screen.Root);
                UiAutoBind.Bind(result, fight, screen);

                fight.backgroundImage = result.Image(screen.Background);
                fight.normalBackground = fight.backgroundImage.sprite;
                fight.eliteBackground = SceneBuilder.LoadSpriteByKey(FightScreen.EliteBackgroundKey);
                fight.bossBackground = SceneBuilder.LoadSpriteByKey(FightScreen.BossBackgroundKey);

                // ONE SHAKER PER RACK, not one for the whole screen. The two
                // stages are separate containers and the HUD is neither of
                // them, which is the point -- see StageShake on why the
                // painted frame has to stay nailed down while the fight moves.
                fight.stageShakes = new[]
                {
                    result.Attach<StageShake>(screen.EnemyStage),
                    result.Attach<StageShake>(screen.PartyStage),
                };

                WireSystemMenu(result, screen.Root, screen.SystemMenu, lockedForFight: true,
                    inDescent: true);

                fight.submenuRowRects = screen.SubmenuRows.Select(result.Rect).ToArray();

                fight.reckoning = WireReckoning(result, screen.Reckoning);
                fight.defeat = WireDefeat(result, screen.Defeat);

                // Readable mid-fight, inert mid-fight. Same tree as the hub's,
                // built with the lock on.
                fight.characterSheetPanel = result.Go(screen.SystemMenu.Root);

                // ONE PLAYER PER POOL MEMBER. The player lives on the VFX
                // node itself, so disabling that node is a real stop rather
                // than something the controller has to remember -- the same
                // arrangement FightBeatPlayer has with the popup pool.
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

                // THE SHARED GROUND BAND. Same component, same three settings
                // -- the difference is where it sits in the tree (behind the
                // racks; see FightScreen.BuildSpellGroundVfx) and that each
                // member's size is recomputed per cast rather than authored.
                fight.spellGroundVfxPlayers = screen.SpellGroundVfx
                    .Select((node, i) =>
                    {
                        var player = result.Attach<SpellVfxPlayer>(node);
                        player.image = fight.spellGroundVfx[i];
                        player.image.raycastTarget = false;
                        player.image.preserveAspect = true;
                        player.image.enabled = false;

                        player.fade = result.Image(screen.SpellGroundVfxNext[i]);
                        player.fade.raycastTarget = false;
                        player.fade.preserveAspect = true;
                        player.fade.enabled = false;

                        return player;
                    })
                    .ToArray();

                // THE DROPS. A bare Image each, on their own node between the
                // sprite pool and the damage numbers -- see
                // FightScreen.BuildSpellParticles for why they are not more
                // members of the pool above.
                foreach (var drop in fight.spellParticles)
                {
                    drop.raycastTarget = false;
                    drop.preserveAspect = true;
                    drop.enabled = false;
                }

                var particles = result.Attach<SpellParticleRenderer>(screen.SpellParticlePool);
                particles.particles = fight.spellParticles;

                // THE MODULE, ON THE EFFECTS POOL NODE. It ticks from its own
                // Update, so it lives on a node that exists for the whole
                // fight and is disabled with it -- which is what makes
                // "nothing survives teardown" a property of the tree rather
                // than something the controller has to remember. Handed both
                // bands because ownership is per cast and a cast can hold
                // members of either.
                fight.performancePlayer = result.Attach<SpellPerformancePlayer>(screen.SpellVfxPool);
                fight.performancePlayer.effectRenderers = fight.spellVfxPlayers;
                fight.performancePlayer.groundRenderers = fight.spellGroundVfxPlayers;
                fight.performancePlayer.particleRenderer = particles;

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
                //
                // STORED ON THE CONTROLLER, not just attached and left for a
                // GetComponentInChildren to rediscover per beat -- the two
                // fields these fill (enemyHitFlashes/partyHitFlashes) used to
                // be a dead Image[] pair UiAutoBind wired to the raw overlay
                // graphic and nothing ever read; they now carry the
                // StageHitFlash itself, the same seam stageShakes uses.
                var flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(PipelineBuilder.HitFlashMaterialPath);
                fight.enemyHitFlashes = screen.EnemyHitFlashes
                    .Select(flashRef => AttachHitFlash(result, flashMaterial, flashRef)).ToArray();
                fight.partyHitFlashes = screen.PartyHitFlashes
                    .Select(flashRef => AttachHitFlash(result, flashMaterial, flashRef)).ToArray();

                // The plate a combatant with no battle art falls back to. Loaded
                // through the same LoadSprite the tree uses, so the importer
                // flip to Sprite happens here too rather than being a thing
                // someone has to remember (CLAUDE.md gotcha 3).
                fight.enemyFallbackSprite = SceneBuilder.LoadSpriteByKey(FightScreen.FallbackPlateKey);

                // NO PLATE ART IS BAKED HERE, and that is the point of the
                // 2026-09-10 column pass. Which character's leather sits in
                // which of the three slots is decided per encounter, so the
                // sprites are loaded at runtime off characters.json's
                // plateArt (PcPlateSprites) rather than serialised into the
                // scene -- a character authored after the last scene build
                // gets their own plate with no rebuild. The tree still bakes
                // ONE default per slot so an unrefreshed scene is not three
                // white quads; see PcPlateArt.BakedDefaults.
                //
                // The per-ButtonTheme 2x1 container bake this replaced was
                // already gone: it loaded one kit sprite per theme for a
                // frame the card no longer has.

                // Something has to actually START a fight, or the scene opens on
                // an empty stage and the screen cannot be looked at. Placeholder
                // until a descent exists to hand a room's roster over -- see
                // FightBootstrap's own header.
                var bootstrap = result.Attach<FightBootstrap>(screen.Root);
                bootstrap.fight = fight;

                // The lunge/recoil animator, one per stage slot -- stored on
                // the controller now, the same shape stageShakes and
                // spellVfxPlayers above already use.
                //
                // WITHOUT A REFERENCE HELD SOMEWHERE NOTHING MOVES. This used
                // to attach a StageActorAnimator to every slot and discard the
                // return outright: FightBeatPlayer.Lunge found its way back to
                // one with a GetComponent on the slot and returned silently
                // when it found nothing, so the whole feature was a no-op that
                // no test noticed -- the class existed, the call site existed,
                // and the two were never introduced. Handing the array to the
                // controller instead means a PlayMode test can inject its own
                // stand-in animators with no scene at all, and E3
                // (UiWiringSweep) now refuses the build outright if either
                // array comes back with a null element or shorter than the
                // slots it was built from -- these two fields carry no
                // [UiOptional], so that failure mode fails loudly instead of
                // shipping quietly a second time.
                fight.enemyActorAnimators = screen.EnemySlots.Select(result.Attach<StageActorAnimator>).ToArray();
                fight.partyActorAnimators = screen.PartySlots.Select(result.Attach<StageActorAnimator>).ToArray();

                // The death fade reaches TWO sibling images -- the figure and
                // its ground shadow -- because a corpse whose shadow stayed put
                // reads as the sprite failing to draw rather than as a death.
                // Stored on the controller for the same reason the animator
                // above now is: FadeTheFallen used to re-derive this with
                // SlotFor(combatant).GetComponent<StageDeathFade>() every time
                // a beat's snapshot mentioned a corpse.
                fight.enemyDeathFades = new StageDeathFade[screen.EnemySlots.Count];
                for (int i = 0; i < screen.EnemySlots.Count; i++)
                {
                    var fade = result.Attach<StageDeathFade>(screen.EnemySlots[i]);
                    fade.sprite = result.Image(screen.EnemySprites[i]);
                    fade.shadow = result.Image(screen.EnemyFootShadows[i]);
                    fade.glow = result.Image(screen.EnemyFootGlows[i]);
                    fight.enemyDeathFades[i] = fade;
                }

                fight.partyDeathFades = new StageDeathFade[screen.PartySlots.Count];
                for (int i = 0; i < screen.PartySlots.Count; i++)
                {
                    var fade = result.Attach<StageDeathFade>(screen.PartySlots[i]);
                    fade.sprite = result.Image(screen.PartySprites[i]);
                    fade.shadow = result.Image(screen.PartyFootShadows[i]);
                    fade.glow = result.Image(screen.PartyFootGlows[i]);
                    fight.partyDeathFades[i] = fade;
                }

                var player = result.Attach<FightBeatPlayer>(screen.DamagePopupPool);
                player.popups = popupComponents.ToArray();
                fight.beatPlayer = player;
            },

            // E4, and only where two DIFFERENT things are being counted.
            //
            // Eight declarations stood here, one per indexed array on
            // FightController. UiAutoBind builds each of those from the very
            // list the nodes were appended to, so there is no longer a second
            // count for E4 to disagree with: the check had become `n == n`,
            // which is the same thing WireRewardTrack's own comment says about
            // never having written its ninety-nine.
            //
            // This one is not that. The popup pool is sized by FightScreen and
            // filled into a SECOND controller, and nothing but this line says
            // the two agree -- which is exactly the shipped Store bug's shape.
            CountBindings = () => new[]
            {
                Count("FightBeatPlayer.popups", screen.DamagePopups, () => fight.beatPlayer.popups.Length),
            },
        };
    }

    private static UiCountAudit.Binding Count(string label, IReadOnlyList<NodeRef> declared, Func<int> bound) =>
        new UiCountAudit.Binding { Label = label, Declared = declared, BoundLength = bound };

    // Attaches one hit-flash overlay and hands it the shared material -- the
    // same three lines for an enemy slot or a party slot, so the two Select
    // calls in Fight()'s Wire step cannot drift apart on what they set.
    private static StageHitFlash AttachHitFlash(UiEmitResult result, Material material, NodeRef flashRef)
    {
        var flash = result.Attach<StageHitFlash>(flashRef);
        flash.image = result.Image(flashRef);
        flash.image.material = material;
        flash.image.raycastTarget = false;
        return flash;
    }

    private static ScreenDef Hub()
    {
        HubScreen screen = null;

        return new ScreenDef
        {
            PanelName = "HubPanel",
            ScenePath = HubScene,
            BuildTree = () =>
            {
                screen = HubScreen.Build(ContentDatabase.Characters.Count);
                return screen.Root;
            },
            Wire = result =>
            {
                var hub = result.Attach<HubController>(screen.Root);
                UiAutoBind.Bind(result, hub, screen);
                DressHub(result, screen);

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
                var menu = result.Attach<MainMenuController>(screen.Root);
                UiAutoBind.Bind(result, menu, screen);

                // THREE CONTROLLERS OFF ONE SCREEN OBJECT, so each gets its own
                // Bind against the same MainMenuScreen. The slot cards are the
                // sub-object case: a SlotCardRefs is not a NodeRef field, so
                // every column of it is still written out below.
                var slots = result.Attach<SaveSlotController>(screen.SaveSlotPanel);
                UiAutoBind.Bind(result, slots, screen);
                slots.slotNumbers = screen.ChooseCards.Select(c => result.Tmp(c.Number)).ToArray();
                slots.slotTops = screen.ChooseCards.Select(c => result.Tmp(c.Top)).ToArray();
                slots.slotDetails = screen.ChooseCards.Select(c => result.Tmp(c.Detail)).ToArray();
                slots.slotGolds = screen.ChooseCards.Select(c => result.Tmp(c.Gold)).ToArray();
                slots.managePanel = result.Go(screen.ManageSavesPanel);

                var reset = result.Attach<ResetProgressController>(screen.ManageSavesPanel);
                UiAutoBind.Bind(result, reset, screen);
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

            // NO CountBindings. Both that stood here -- slotButtons and
            // deleteButtons -- are now built by UiAutoBind off the very list
            // the nodes were appended to, so E4 would be comparing a count
            // with itself. The cards those buttons sit on are a different
            // matter and are still written by hand above; nothing indexes
            // them against a separate number.
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
        ShopController shopController = null;

        return new ScreenDef
        {
            PanelName = "MapPanel",
            ScenePath = MapScene,
            BuildTree = () =>
            {
                screen = MapScreen.Build(ContentDatabase.Characters.Count);
                return screen.Root;
            },
            Wire = result =>
            {
                var map = result.Attach<MapController>(screen.Root);
                UiAutoBind.Bind(result, map, screen);

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

                shopController = WireShop(result, screen.Shop);
                map.shop = shopController;
            },

            // Gate 2's own count bindings (docs/PLAN_SHOP.md §7.3): the six
            // offer arrays and the PACK modal's row array. The four/three/
            // three counts are ShopStock's own constants (§7.1 point 3,
            // revised in docs/handoffs/shop_v2/GAP_AUDIT.md's Gate 1 exit
            // record); this pairing is what stops a future count change on
            // one side from orphaning cards on the other.
            //
            // THE ONLY SCREEN-LEVEL ONES LEFT, and they are kept for a reason
            // the deleted ones did not have: the shop's cards come off a
            // sub-object, so UiAutoBind cannot see them and the lines in
            // WireShop stay hand-written -- which is precisely the edit E4
            // exists to catch. The arrays a binder now owns cannot be
            // rewritten to read a different list, because there is no line
            // left to rewrite.
            CountBindings = () => new[]
            {
                Count("ShopController.gearCards", screen.Shop.GearCards.Select(c => c.Button).ToList(),
                    () => shopController.gearCards.Length),
                Count("ShopController.bookCards", screen.Shop.BookCards.Select(c => c.Button).ToList(),
                    () => shopController.bookCards.Length),
                Count("ShopController.relicCards", screen.Shop.RelicCards.Select(c => c.Button).ToList(),
                    () => shopController.relicCards.Length),
                Count("ShopController.packRows", screen.Shop.PackRows.Select(r => r.Row).ToList(),
                    () => shopController.packRows.Length),
            },
        };
    }

    private static ShopController WireShop(UiEmitResult result, ShopScreen screen)
    {
        var shop = result.Attach<ShopController>(screen.Root);
        UiAutoBind.Bind(result, shop, screen);

        // Item and relic art, baked as parallel arrays. Resolved here rather
        // than at runtime because Resources loading and AssetDatabase are
        // different worlds and only the builder has the second one -- the same
        // arrangement the relic draft and the Reckoning's offers already use.
        // Skills author no iconPath, so a book card gets no table and
        // ItemIcons.Apply hides its slot.
        shop.itemArt = ContentDatabase.Items
            .Where(i => i != null && !string.IsNullOrEmpty(i.iconPath))
            .Select(i => new IconEntry(i.id, SceneBuilder.LoadSpriteByKey(i.iconPath)))
            .ToArray();

        shop.relicArt = ContentDatabase.Relics
            .Where(r => r != null && !string.IsNullOrEmpty(r.iconPath))
            .Select(r => new IconEntry(r.id, SceneBuilder.LoadSpriteByKey(r.iconPath)))
            .ToArray();

        shop.gearCards = screen.GearCards.Select(c => result.Button(c.Button)).ToArray();
        shop.gearIcons = screen.GearCards.Select(c => result.Image(c.Icon)).ToArray();
        shop.gearNames = screen.GearCards.Select(c => result.Tmp(c.Name)).ToArray();
        shop.gearMetas = screen.GearCards.Select(c => result.Tmp(c.Meta)).ToArray();
        shop.gearPrices = screen.GearCards.Select(c => result.Tmp(c.Price)).ToArray();

        shop.bookCards = screen.BookCards.Select(c => result.Button(c.Button)).ToArray();
        shop.bookIcons = screen.BookCards.Select(c => result.Image(c.Icon)).ToArray();
        shop.bookNames = screen.BookCards.Select(c => result.Tmp(c.Name)).ToArray();
        shop.bookMetas = screen.BookCards.Select(c => result.Tmp(c.Meta)).ToArray();
        shop.bookPrices = screen.BookCards.Select(c => result.Tmp(c.Price)).ToArray();

        shop.relicCards = screen.RelicCards.Select(c => result.Button(c.Button)).ToArray();
        shop.relicIcons = screen.RelicCards.Select(c => result.Image(c.Icon)).ToArray();
        shop.relicNames = screen.RelicCards.Select(c => result.Tmp(c.Name)).ToArray();
        shop.relicMetas = screen.RelicCards.Select(c => result.Tmp(c.Meta)).ToArray();
        shop.relicPrices = screen.RelicCards.Select(c => result.Tmp(c.Price)).ToArray();

        shop.packPrevButton = result.Button(screen.PackPrevPage);
        shop.packNextButton = result.Button(screen.PackNextPage);
        shop.packRows = screen.PackRows.Select(r => result.Go(r.Row)).ToArray();
        shop.packNames = screen.PackRows.Select(r => result.Tmp(r.Name)).ToArray();
        shop.packMetas = screen.PackRows.Select(r => result.Tmp(r.Meta)).ToArray();
        shop.packPrices = screen.PackRows.Select(r => result.Tmp(r.Price)).ToArray();
        shop.packSellOneButtons = screen.PackRows.Select(r => result.Button(r.SellOne)).ToArray();
        shop.packSellAllButtons = screen.PackRows.Select(r => result.Button(r.SellAll)).ToArray();

        return shop;
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
                UiAutoBind.Bind(result, talents, screen);

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

                // Which stone each gated collar belongs to, and which stone each
                // edge feeds. Plain int arrays, so no NodeRef and no binder:
                // the collar arrays it does fill are SHORTER than the orb
                // arrays, and this is what maps a collar's position back.
                talents.collarSlots = screen.CollarSlots.ToArray();
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

                // THE BACKDROP, driven from the controller rather than by a
                // component each. Both halves of every layer are bound -- the
                // rect that moves and the graphic that fades -- because the
                // fades and the drifts run on different periods and a single
                // handle could only carry one of them. The rect half mirrors
                // the node's name and the binder took it; the SECOND view of
                // the same node is what a name-matching binder cannot express.
                talents.cloudImages = screen.CloudWashes.Select(result.Image).ToArray();
                talents.dustImages = screen.DustMotes.Select(result.Image).ToArray();
                talents.shootingStarImages = screen.ShootingStars.Select(result.Image).ToArray();
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

        // The hub's own NavContext.Cancel now opens this directly (plan
        // section 3/4) instead of racing it against a separate Escape poll,
        // so HubController needs the controller in hand.
        hub.systemMenu = WireSystemMenu(result, screen.Root, screen.SystemMenu, lockedForFight: false,
            inDescent: false);
    }

    // The overarching menu's wiring, shared by every scene that carries one.
    //
    // `escapeConsumers` is gone (docs/GAMEPAD_NAVIGATION_PLAN.md phase 2, step
    // B): the menu no longer polls Escape itself at all, so there is nothing
    // left for a per-scene consumer list to guard. Only the hub currently
    // reopens it on Cancel (HubController.HandleEscape, now the hub's own
    // NavContext.Cancel handler) -- the map and the fight lose "Cancel opens
    // the system menu" via keyboard/gamepad in this commit, a deliberate,
    // stated scope narrowing rather than an oversight: giving Map and Fight
    // their own base NavContext with the same wiring is left to whichever
    // later step gives each of them the rest of their own navigation (Map's
    // Graph group, Fight's is registered already for its own model).
    private static SystemMenuController WireSystemMenu(
        UiEmitResult result, NodeRef host, SystemMenuScreen menu, bool lockedForFight,
        bool inDescent)
    {
        var controller = result.Attach<SystemMenuController>(host);
        UiAutoBind.Bind(result, controller, menu);

        controller.panel = result.Go(menu.Root);

        // Decided here rather than sniffed at runtime: the hub is not a
        // descent, and no state can make it one.
        controller.inDescent = inDescent;

        CharacterDossierController dossierController = null;
        if (menu.Dossier != null)
        {
            dossierController = WireDossier(result, menu.Dossier, lockedForFight);

            // Not on WireDossier's own signature: WireDossier does not carry
            // the SystemMenuController it would need, and WireSystemMenu has
            // `controller` in hand already. Same shape as ExitsController.menu
            // below -- only the hub copy of the dossier (InDescent false)
            // shows the refund minus.
            dossierController.menu = controller;
        }

        if (menu.RewardTrack != null)
        {
            var trackController = WireRewardTrack(result, menu.RewardTrack);

            // The dossier's door into it, and the typed handle ShowTrack()
            // calls ShowFor on. Bound here rather than in WireDossier because
            // both are the one thing the dossier needs that lives on another
            // screen -- WireDossier's own signature does not carry the
            // RewardTrack screen to reach for. Both wiring calls above
            // already return their controller, so this is a straight field
            // assignment rather than a GetComponent re-find.
            if (dossierController != null)
            {
                dossierController.trackPanel = result.Go(menu.RewardTrack.Root);
                dossierController.trackScreen = trackController;
            }
        }
        if (menu.Options != null) WireOptions(result, menu.Options);
        if (menu.RunStats != null) WireRunStats(result, menu.RunStats);
        if (menu.Party != null) WireParty(result, menu.Party, controller, lockedForFight);
        if (menu.Exits != null) WireExits(result, menu.Exits, controller);

        return controller;
    }

    // The Party pane, inside the system menu.
    //
    // `menu` and `lockedForFight` are the two fields UiAutoBind cannot
    // reach -- the same residual ExitsController.menu and CharacterDossier
    // Controller.lockedForFight already carry, for the same reason: neither
    // is a NodeRef, so nothing on PartyScreen has a same-named member for the
    // binder to match against.
    //
    // `seatBadgeTexts` is the one PARTY-SPECIFIC residual: PartyScreen.
    // BuildSeat parents a text label under each badge's OutlineBox but never
    // captures a NodeRef for it (only the badge box itself, as SeatBadges),
    // so it is reached here the same way ExitsController.exitLabels reaches
    // a button's own child label -- `searchChildren: true`.
    private static PartyController WireParty(
        UiEmitResult result, PartyScreen party, SystemMenuController menu, bool lockedForFight)
    {
        var controller = result.Attach<PartyController>(party.Root);
        UiAutoBind.Bind(result, controller, party);

        controller.menu = menu;
        controller.lockedForFight = lockedForFight;
        controller.seatBadgeTexts = party.SeatBadges.Select(badge => result.Tmp(badge, searchChildren: true)).ToArray();

        // The roster card's title row is a BUTTON, so UiAutoBind fills
        // `cardTitles` with the Buttons and cannot also reach the label
        // EmitButton bakes underneath each one. Same `searchChildren: true`
        // route as seatBadgeTexts above, and for the same reason: the text
        // lives on a GameObject the tree never declared.
        controller.cardTitleTexts = party.CardTitles
            .Select(title => result.Tmp(title, searchChildren: true))
            .ToArray();

        // NO CountBindings for this pane, and the reason is the same one the
        // reward track's own block gives: E4 exists to catch a strip SIZED
        // from one collection and FILLED from another, and every array here is
        // built by `.Select` over the very NodeRef list the nodes were
        // appended to (party.SeatBadges, party.CardTitles). The binding it
        // would check is that list's count compared with itself.
        //
        // THIS ALSO RETIRES seatBadgeTexts FROM UiCountAuditCoverageLintTests'
        // KnownOffenders, where it sat as "a real gap the bug-hunt found". It
        // was never a gap of that shape -- the allowlist was catching every
        // unmarked site and this one had simply never been explained.
        //
        // P4: one drag surface per seat/card button. Attached here so
        // UiWiringSweep sees the arrays (the same shape as FightController.
        // partyActorAnimators); WHAT each one does is a per-index closure
        // PartyController.Wire assigns at runtime -- see PartyDragSource's
        // own header for why that split, not UiAutoBind, owns the delegates.
        controller.seatDragSources = party.SeatButtons.Select(result.Attach<PartyDragSource>).ToArray();
        controller.cardDragSources = party.CardButtons.Select(result.Attach<PartyDragSource>).ToArray();

        // The toast's own fader (P4). [RequireComponent(CanvasGroup)] on
        // PartyToast adds the CanvasGroup as a side effect of this one
        // Attach -- see PartyToast's own header.
        controller.toastFader = result.Attach<PartyToast>(party.Toast);

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
        UiAutoBind.Bind(result, controller, stats);

        controller.valueKeys = stats.ValueKeys.ToArray();

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
        UiAutoBind.Bind(result, controller, exits);

        // The label is a CHILD of the button, so the same NodeRef serves both
        // the click and the text swap and there is no second handle to keep in
        // step. `searchChildren: true` is what says so out loud: descending was
        // the unconditional behaviour of every typed lookup until F9, which
        // meant any of them could quietly return a component from a different
        // object than the node named. Two sites want it; this is one.
        controller.exitLabels = exits.ExitButtons
            .Select(button => result.Tmp(button, searchChildren: true)).ToArray();

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
        UiAutoBind.Bind(result, controller, options);

        // The keys themselves are string arrays, not nodes, so they stay here.
        controller.sliderKeys = options.SliderKeys.ToArray();
        controller.stepperKeys = options.StepperKeys.ToArray();

        // OptionRow (plan section 7) attached HERE, at build time, not
        // runtime-added the way the old HoverIndex was -- UiAutoBind has no
        // name match for `rows` against `RowHovers` (deliberately: it binds
        // GameObject/Tmp/Button/Image/Rect by name, never an arbitrary
        // component type), so this is explicit, the same shape every other
        // Attach<T> call in this file already is.
        controller.rows = options.RowHovers.Select(result.Attach<OptionRow>).ToArray();

        // NO CountBindings: rows is built directly off options.RowHovers,
        // the very list it is meant to agree with, so registering a count
        // check here would only be comparing that list with itself.

        return controller;
    }

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
        UiAutoBind.Bind(result, controller, track);

        // THE FOUR NODES THAT MOVE, each bound twice -- once as the Image whose
        // colour is animated and once as the RectTransform that is moved or
        // scaled. Two components of one GameObject, so no lookup is being done
        // twice; this is the alternative to a GetComponent in an Update that
        // runs sixty times a second for the life of the panel.
        controller.shimmer = result.Image(track.Shimmering);
        controller.shimmerRect = result.Rect(track.Shimmering);
        controller.hereHaloRect = result.Rect(track.HereHalo);

        controller.collectLabel = result.Tmp(track.CollectCaption);

        controller.cardRect = result.Rect(track.Card);

        // ONE ENTRY PER REWARD KIND, not per level -- docs/PLAN_REWARD_
        // TRACKS.md §1's "the three things that DO become runtime": which
        // reward sits at which level is a per-character, runtime question,
        // so the art cannot be baked by level. It is still resolved HERE
        // rather than at runtime, because the controller lives in the
        // runtime assembly and LoadSpriteByKey is editor-only -- only the
        // KEY it is indexed by is kind rather than level. Sized off the enum
        // itself so a thirteenth reward kind is a longer array rather than a
        // silent miss, and index (int)TrackReward.X is the controller's own
        // lookup (RewardTrackController.MarkFor/CardArtFor).
        var rewardKinds = (TrackReward[])Enum.GetValues(typeof(TrackReward));

        // INDEX 0 IS TrackReward.None, and both IconFor and CardArtKeyFor
        // return null for it by design (their own None case/default) -- the
        // reward a level has NOT been authored with yet has no art. A null
        // Sprite in a SerializeField array fails UiWiringSweep's E3 check
        // regardless of [UiOptional], which only excuses an array being
        // EMPTY, never half-wired. Filled with the same neutral ring the
        // tree bakes into every mark slot before a controller ever runs
        // (RewardTrackScreen.cs's TrackIcon{level}) -- it is never actually
        // read: a well-formed track never resolves a level to None
        // (RewardTrackTests.NoLevelOfTheTrackPaysNothing), so this is
        // satisfying the sweep rather than standing in for missing art.
        var neutralMark = SceneBuilder.LoadSpriteByKey("proc:ring_outline");

        controller.markByReward = rewardKinds
            .Select(reward => reward == TrackReward.None
                ? neutralMark
                : SceneBuilder.LoadSpriteByKey(RewardTrackLayout.IconFor(reward)))
            .ToArray();

        // NULL IS A REAL ANSWER HERE NOW, not a miss. Since phase 5 only nine
        // reward kinds have a medallion that honestly means them; the other
        // twelve draw a word (RewardTrackLayout.CardGlyphFor) and CardArtKeyFor
        // returns null for every one of them. UiWiringSweep still refuses a
        // half-wired array whatever the reason, so those slots carry the same
        // neutral ring TrackReward.None does -- and the controller never reads
        // them, because it asks CardGlyphFor first and hides the Image
        // outright when there is a word to draw.
        controller.cardArtByReward = rewardKinds
            .Select(reward =>
            {
                string key = RewardTrackLayout.CardArtKeyFor(reward);
                return string.IsNullOrEmpty(key) ? neutralMark : SceneBuilder.LoadSpriteByKey(key);
            })
            .ToArray();

        // The disc is bound as BOTH the Button that takes the click and the
        // Image that carries the state's colour, for the same reason as above:
        // it is one GameObject wearing both, and the controller needs each on a
        // different path -- one at wiring time to attach a listener, one per
        // repaint.
        controller.discs = track.Dots.Select(result.Image).ToArray();

        // NO CountBindings for any of these, and it is worth saying why rather
        // than leaving the absence to be read as an oversight. E4 exists to
        // catch a strip sized from one collection and filled from another;
        // every array above is filled by UiAutoBind off the very list the nodes
        // were appended to, so the binding it would check is the same count
        // compared with itself. The reward track also has no ScreenDef of its
        // own -- it lives inside the system menu, which three scenes each build
        // through WireSystemMenu -- so declaring one would mean threading a
        // bindings list through all three to assert n == n.
        return controller;
    }

    // The character dossier, inside the system menu's Character pane.
    //
    // Bound with the SAME icon arrays every other item surface uses, from every
    // ItemDefinition with an authored iconPath -- an item whose art is missing
    // disables its Image rather than painting a white quad.
    private static CharacterDossierController WireDossier(
        UiEmitResult result, CharacterDossierScreen dossier, bool lockedForFight)
    {
        var controller = result.Attach<CharacterDossierController>(dossier.Root);
        UiAutoBind.Bind(result, controller, dossier);
        controller.lockedForFight = lockedForFight;

        controller.packSortTabs = dossier.PackFilterTabs.Select(result.Button).ToArray();

        // NO PORTRAIT ARRAY, and its absence is the point. Every character with
        // a portraitPath used to be baked here as an IconEntry, which made the
        // scene a photograph of the roster: a character authored after the last
        // scene build had an empty plate and nothing short of -BuildScenes
        // filled it. portraitPath is Resources-relative now and
        // CharacterPortraits loads it by id at the moment the dossier draws.
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
        // The controller takes the WIPE MASK and no handle on the painted frame
        // at all -- there is no frame field for the binder to fill, deliberately.
        // Everything the controller used to do to the frame -- scale it, lift
        // it -- is now done to the mask around it, and a reference it does not
        // hold cannot be squashed by accident a second time.
        var controller = result.Attach<ReckoningController>(screen.Root);
        UiAutoBind.Bind(result, controller, screen);

        // The Continue arrow's heat, animated. Attached rather than wired into
        // the controller: it needs no state from the Reckoning, and a screen
        // controller that also owns an ambient loop is the shape every other
        // ambience component here was pulled OUT of.
        result.Attach<EmberFlare>(screen.ContinueGlow);

        controller.offerBurstRects = screen.OfferBursts.Select(result.Rect).ToArray();

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

        controller.offerRects = screen.OfferButtons.Select(result.Rect).ToArray();

        return controller;
    }

    private static DefeatController WireDefeat(UiEmitResult result, DefeatScreen screen)
    {
        var controller = result.Attach<DefeatController>(screen.Root);
        UiAutoBind.Bind(result, controller, screen);

        return controller;
    }

    private static void WireGlossary(UiEmitResult result, HubScreen screen, HubController hub)
    {
        var glossary = screen.Glossary;
        var controller = result.Attach<GlossaryController>(glossary.Root);
        UiAutoBind.Bind(result, controller, glossary);

        controller.detailLockedByLabel = result.Tmp(glossary.DetailLockedBy);

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
        UiAutoBind.Bind(result, controller, draft);

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
        UiAutoBind.Bind(result, controller, debug);

        hub.debugMenuPanel = result.Go(debug.Root);
    }
}
