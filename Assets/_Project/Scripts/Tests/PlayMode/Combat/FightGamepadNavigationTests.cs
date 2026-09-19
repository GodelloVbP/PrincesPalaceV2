using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // Legacy Input cannot be simulated headlessly (see FightController.Input's
    // own PollGamepadNavigation comment for why -- the same reason the C/I
    // character-sheet hotkeys are tested through ToggleCharacterSheet rather
    // than through a simulated keypress). MoveFocus/ConfirmFocus/Wrap are
    // internal for exactly this reason: this suite drives the LOGIC directly,
    // the way a stick press would, and leaves the axis-polling glue itself
    // untested -- it is a few lines reading Input.GetAxisRaw, not a rule.
    public class FightGamepadNavigationTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        [Test]
        public void WrapCyclesBothDirectionsAtTheEnds()
        {
            Assert.AreEqual(0, FightController.Wrap(4, 4));
            Assert.AreEqual(3, FightController.Wrap(-1, 4));
            Assert.AreEqual(2, FightController.Wrap(2, 4));
            Assert.AreEqual(0, FightController.Wrap(0, 4));

            // A count of zero (no rows, no living enemies) never divides by
            // zero -- it lands on 0 rather than throwing, which is what lets
            // MoveFocus call this unconditionally instead of guarding first.
            Assert.AreEqual(0, FightController.Wrap(7, 0));
        }

        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
        }

        [UnityTest]
        public IEnumerator MovingFocusAtRootStepsBottomUpTheWayTheColumnIsBuilt()
        {
            yield return LoadFight();

            // Root depth needs no bound session at all -- MoveFocus only
            // touches _session on the Sub/Target branches. Verified against
            // the scene straight after load, before any encounter exists.
            Assert.AreEqual(0, _fight.FocusedVerbForTest, "starts on ATTACK, index 0");

            // UP (delta -1): BuildVerbColumn puts ATTACK at the BOTTOM of the
            // column and MOVE at the top, so moving the stick toward the top
            // of the screen has to step toward the HIGHER index -- the
            // opposite of a plain top-to-bottom list.
            _fight.MoveFocus(-1);
            Assert.AreEqual(1, _fight.FocusedVerbForTest, "up moves toward the top of a bottom-up column");

            // DOWN (delta +1) from ATTACK wraps to the far end (MOVE) rather
            // than refusing to move -- a cyclic list, not a clamped one.
            _fight.MoveFocus(1);
            _fight.MoveFocus(1);
            Assert.AreEqual(3, _fight.FocusedVerbForTest, "wraps to MOVE, the top of the column");
        }

        [UnityTest]
        public IEnumerator MovingFocusAtTargetDepthCyclesTheHoveredLivingEnemy()
        {
            yield return LoadFight();

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to prove target cycling");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            // Bind alone was never enough -- see EnemyFightableTests' own
            // header comment. FightBootstrap is what calls Begin() in real
            // play, and without it there is no current actor, so CanAct is
            // false for everything and ConfirmFocus silently no-ops -- which
            // is exactly what the first version of this test did, quietly.
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to test input against");

            // ATTACK (Root, focus 0) skips the submenu straight to targeting,
            // the exact click path OnVerbPressed(0) already takes.
            _fight.ConfirmFocus();
            yield return null;

            // Only the front enemy is reachable by a melee ATTACK
            // (FightSession.CanReachEnemy) -- cycling past it and
            // confirming there is a SEPARATE, already-covered claim
            // (Phase 1's OnEnemyPressed reach gate). This test's own claim
            // is narrower and does not need reach at all: that MoveFocus
            // actually moves which enemy is hovered, one step per call,
            // through the exact hover state RefreshInitiative's ghost
            // preview and the reticle's hover-brighten already read.
            //
            // LITERAL INDICES, not just "changed" -- WrapFromNoHover's fix
            // (FightController.Input.cs) made a first "next" press land on
            // living[0] rather than living[1], so this pins the corrected
            // value instead of only proving movement happened.
            _fight.MoveFocus(1);
            int first = _fight.HoveredEnemyIndexForTest;

            _fight.MoveFocus(1);
            int second = _fight.HoveredEnemyIndexForTest;

            Assert.AreEqual(0, first, "a first \"next\" press from no hover lands on the first living enemy");
            Assert.AreEqual(1, second, "a second \"next\" press steps to the next living enemy");
        }

        // THE OTHER HALF OF THE SAME FIX: a first "previous" press from no
        // hover lands on the LAST living enemy rather than the first --
        // WrapFromNoHover(-1, -1, count) == count - 1, which this pins as a
        // literal alongside MovingFocusAtTargetDepthCyclesTheHoveredLivingEnemy's
        // "next" case so the two directions cannot silently drift apart.
        [UnityTest]
        public IEnumerator AFirstPreviousPressAtTargetDepthLandsOnTheLastLivingEnemy()
        {
            yield return LoadFight();

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to prove target cycling");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to test input against");

            _fight.ConfirmFocus();
            yield return null;

            _fight.MoveFocus(-1);
            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest,
                "a first \"previous\" press from no hover lands on the last living enemy, index 1 of 2");
        }

        // THE GATE'S OWN BUG (2026-09-19 follow-up): the two tests above
        // drive MoveFocus/CycleTarget DIRECTLY, bypassing
        // IFightNavigationTarget.MoveFocus -- the wrapper that flips the
        // pad's vertical sign at Target depth so "up is one slot deeper" (the
        // owner's call). That flip exposed a second, separate bug:
        // FocusedElement/ConfirmFocus already treat no hover as enemy 0 (the
        // marker sits there before any press, and Submit would hit it), so a
        // FIRST pad press has to read as one step FROM that implicit 0 --
        // not as WrapFromNoHover's older "first press lands ON index 0" rule,
        // which is what the two direct-call tests above still correctly pin.
        // Before today's fix, the first Up press landed back on 0 (no
        // visible change) instead of on 1.
        [UnityTest]
        public IEnumerator FirstPressOnAFreshTargetPickTreatsEnemyZeroAsHovered()
        {
            yield return StandUpAFight();

            var pad = (IFightNavigationTarget)_fight;
            pad.ConfirmFocus();
            yield return null;
            Assert.AreEqual(-1, _fight.HoveredEnemyIndexForTest,
                "precondition: nothing hovered, the marker reads FocusedElement's implicit enemy 0");

            // UP: ProcessFight hands the wrapper raw -1, which Target depth
            // negates to +1 (CycleTarget's "deeper" direction) -- one slot
            // deeper than the enemy 0 a fresh pick already implies.
            pad.MoveFocus(-1);
            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest,
                "first Up from a fresh pick steps one slot deeper than the implicit enemy 0, not onto it again");

            // A WHOLE NEW SCENE, not Back-then-re-enter: _hoveredEnemyIndex is
            // a mouse-hover latch (OnEnemyUnhovered's own comment) that
            // OnBackPressed never clears, so reopening the same rack would
            // still read the 1 just proven above. Reloading is the only way
            // to get a genuinely fresh, unhovered pick to test Down against.
            yield return StandUpAFight();
            pad = (IFightNavigationTarget)_fight;
            pad.ConfirmFocus();
            yield return null;
            Assert.AreEqual(-1, _fight.HoveredEnemyIndexForTest, "precondition: a new pick, nothing hovered yet");

            // DOWN: raw +1, negated to -1 (CycleTarget's "nearer" direction)
            // -- wraps to the deepest living enemy, the same place a SECOND
            // press from enemy 0 would land (Wrap(0 - 1, count) == count - 1).
            // THIS HALF WAS NEVER ACTUALLY BROKEN: WrapFromNoHover's older
            // no-hover rule already agreed with "as if enemy 0 were hovered"
            // whenever delta is negative, since count - 1 is what both rules
            // compute -- pinned here beside Up's fix so the two directions
            // stay proven together rather than drifting apart unnoticed.
            pad.MoveFocus(1);
            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest,
                "first Down from a fresh pick wraps to the last living enemy");
        }

        // THE LEFT/RIGHT TWIN of the fix above, through InspectMove instead
        // of the vertical wrapper -- and covering the case the enemy-rack
        // fix in HorizontalAtTargetDepthCyclesTargets does NOT: the MIRRORED
        // ally rack, where Right means "nearer" rather than "deeper"
        // (InspectMove's own header). A fresh Right on the ally rack already
        // read correctly before today (HorizontalAtTargetDepthCyclesAllyTargets
        // pins it, unaffected by this fix, same reason Down above is
        // unaffected) -- Left is the ally rack's "deeper" direction, and
        // "deeper" is exactly the case that was broken.
        [UnityTest]
        public IEnumerator FirstPressOnAFreshTargetPickTreatsPlateZeroAsHoveredOnTheMirroredAllyRack()
        {
            yield return LoadFight();

            var caster = new CombatantState("Shawn", true, 300, 30, 40, 30);
            var ally = new CombatantState("Bjorn", true, 300, 30, 40, 8);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 1);
            var encounter = new CombatEncounter(new[] { caster, ally }, new[] { foe });

            var ward = new Domain.Content.ResolvedSkill("fleece_ward", "Ward", "", "sheep", 1,
                SkillEffect.Ward, SkillTargeting.SingleAlly,
                0, 0, false, 0, 50, false, null, Domain.Content.SpellPresentation.None, 0);

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", Domain.Content.CharacterRole.Support, new[] { ward }, null,
                    Domain.Stats.DamageType.Physical),
                new PlayerKit("bear", Domain.Content.CharacterRole.Tank, null, null,
                    Domain.Stats.DamageType.Physical),
            };
            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new Domain.Content.ResolvedEnemy("front", "Front", new Domain.Stats.StatBlock(),
                    5, 3, false, Domain.Stats.DamageType.Physical, Domain.Stats.DamageType.Physical, 0), false),
            };

            var session = new FightSession(encounter, kits, enemyKits, new Domain.Rng.SeededRandom(9));
            session.Begin();
            _fight.Bind(session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            var pad = (IFightNavigationTarget)_fight;

            // Up once reaches SKILL; Submit opens the list, Submit again
            // presses row 0 -- the ward -- opening the party rack. Both
            // plates are pickable (a ward can target its own caster).
            pad.MoveFocus(-1);
            pad.ConfirmFocus();
            yield return null;
            pad.ConfirmFocus();
            yield return null;

            Assert.IsTrue(IsShown("TargetPrompt"), "precondition: the ally pick opened");
            Assert.AreEqual(-1, _fight.HoveredAllyIndexForTest, "precondition: nothing hovered yet");

            // LEFT on the mirrored ally rack is the "deeper" direction
            // (InspectMove's own header: Right means nearer there, so Left is
            // the one that steps away from the implicit plate 0). Before
            // today's fix this landed back on plate 0 -- the caster, no
            // visible change -- instead of stepping to plate 1.
            pad.InspectMove(-1);
            yield return null;
            Assert.AreEqual(1, _fight.HoveredAllyIndexForTest,
                "Left from a fresh ally pick steps one slot deeper than the implicit plate 0, landing on Bjorn");
        }

        // ---- the status box follows the pad (2026-09-19) -------------------------
        //
        // The owner's question, in their words: "in a fight, how does a player
        // hover over a mob or a PC to check (de)buffs? (gamepad)". The answer
        // this pins is that the box follows the FOCUS: whenever the pad is on
        // an actor, that actor's statuses are on screen, and when it is on
        // anything else there is no box at all.
        //
        // WHAT IT ALSO RECORDS, by the route it has to take to get there: the
        // only depth at which Fight's focus is on an actor is Target, so the
        // sequence below -- Submit on ATTACK, then the stick -- is the whole
        // of a pad player's access to this. There is no inspect path that
        // does not first commit to a verb; adding one needs a horizontal axis
        // read in NavigationInputModule's Fight branch, which reads Vertical
        // only (docs/GAMEPAD_NAVIGATION_PLAN.md section 3).
        [UnityTest]
        public IEnumerator TheStatusBoxFollowsPadFocusOntoAnActorAndLeavesWithIt()
        {
            yield return LoadFight();

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to prove the box follows focus");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            var enemy = built.Session.Encounter.Enemies[0];

            // ASSERTED, NOT ASSUMED (CODE_STANDARDS section 8): the row count
            // below is a literal, so a fixture that started carrying statuses
            // of its own has to fail here rather than quietly change what the
            // literal means.
            Assert.AreEqual(0, enemy.Statuses.Count, "precondition: the first enemy starts with no statuses");

            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Poison, 5, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Vulnerable, 15, 2);

            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to drive input against");

            // ROOT DEPTH: the pad is on a verb, and a verb is not an actor.
            Assert.IsFalse(StatusBoxIsShown(), "nothing is being inspected while the focus is on the verb column");

            // ATTACK, which goes straight to the enemy rack. Nothing hovered
            // yet, so the focus is on what Submit would press -- the first
            // living enemy -- and the box is on that enemy from the moment
            // the rack opens rather than after the first stick press.
            _fight.ConfirmFocus();
            yield return null;

            Assert.IsTrue(StatusBoxIsShown(), "the focused enemy's statuses come up with the rack");
            Assert.AreEqual(2, VisibleStatusBoxRows(), "one row per active status on the focused enemy");

            // And it stays with the focus as the stick walks the rack: enemy
            // 1 carries nothing, so the box has nothing to show and goes.
            _fight.MoveFocus(1);
            _fight.MoveFocus(1);
            yield return null;

            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest, "precondition: the stick reached the second enemy");
            Assert.IsFalse(StatusBoxIsShown(), "an actor with no statuses shows no box rather than an empty one");

            // Cancel steps back out of targeting to the verb column, and the
            // box goes with the focus.
            _fight.MoveFocus(-1);
            yield return null;
            Assert.IsTrue(StatusBoxIsShown(), "precondition: back on the enemy that carries the statuses");

            ((IFightNavigationTarget)_fight).OnBackPressed();
            yield return null;

            Assert.IsFalse(StatusBoxIsShown(), "leaving the rack for the verb column closes the box");
        }

        // ---- inspecting without committing to a verb (2026-09-19) ---------------
        //
        // The other half of the owner's question, and the half the block above
        // recorded as impossible: a pad could reach an actor only through a
        // verb. The horizontal axis is the answer
        // (NavigationInputModule.ProcessFight -> IFightNavigationTarget.
        // InspectMove), and what it reaches is a ring of living combatants
        // laid over Root depth -- every monster, then every squadmate.
        //
        // DRIVEN THROUGH THE INTERFACE, not through the public MoveFocus/
        // ConfirmFocus, and deliberately: inspect is layered onto the frozen
        // focus model at the explicit-interface seam that already carries the
        // busy guard (FightController.Input.cs), which is the only path a pad
        // press actually takes. A direct MoveFocus/ConfirmFocus call is the
        // Back BUTTON's path and the older tests' path, and both still mean
        // exactly what they always did.

        [Test]
        public void SteppingTheInspectRingWrapsForwardAndLeavesByItsFront()
        {
            // Forward is an ordinary ring: off the end and round to the front.
            Assert.AreEqual(1, FightController.StepInspectRing(0, 1, 3));
            Assert.AreEqual(0, FightController.StepInspectRing(2, 1, 3),
                "forward off the last squadmate wraps round to the first monster");

            // Backward is NOT, and that asymmetry is the whole design: the
            // verb column is the stop before the first monster, so stepping
            // back off the front is how a player gets home without hunting
            // for the B button.
            Assert.AreEqual(0, FightController.StepInspectRing(1, -1, 3));
            Assert.AreEqual(-1, FightController.StepInspectRing(0, -1, 3),
                "back off the front of the ring is the verb column, not a wrap");

            // Nobody left standing: there is no stop to be on, either way.
            Assert.AreEqual(-1, FightController.StepInspectRing(0, 1, 0));
            Assert.AreEqual(-1, FightController.StepInspectRing(0, -1, 0));
        }

        [UnityTest]
        public IEnumerator RightAtRootWalksTheActorRowAndTheBoxComesWithIt()
        {
            yield return StandUpAFight();

            var pad = (IFightNavigationTarget)_fight;

            Assert.AreEqual("Verb0", FocusName(pad), "precondition: the pad starts on the verb column");
            Assert.IsFalse(StatusBoxIsShown(), "precondition: a verb is not an actor");

            // RIGHT once: onto the first living monster, with no verb pressed
            // and nothing committed to.
            //
            // THE FIGURE, NOT THE PLATE. The marker and the status box have to
            // name the same end of the screen -- PlaceStatusBox puts the box
            // under the figure on the battlefield, so the arrow belongs on the
            // figure's own hit area rather than on the HUD readout in the
            // corner. That is what ShowFigureTarget's `perched` arm buys.
            pad.InspectMove(1);
            yield return null;

            Assert.AreEqual("EnemyHitArea0", FocusName(pad),
                "Right off the verb column lands on the first living enemy's FIGURE");
            Assert.IsTrue(StatusBoxIsShown(), "the box opens on the actor being read");
            Assert.AreEqual(2, VisibleStatusBoxRows(), "one row per status on that enemy");

            // AND IT EATS NOTHING WHILE IT PERCHES. A hit area is a NoChrome
            // Button that raycasts against its whole rect, so a perched one
            // left raycastable would swallow the clicks the stage behind it
            // needs -- starting with the intent icon, which is the MOUSE's way
            // to inspect this same monster.
            AssertRaycasts("EnemyHitArea0", false,
                "a perched figure target must not take clicks meant for the stage behind it");

            // RIGHT again: the next monster, which carries nothing, so there
            // is no box rather than an empty one.
            pad.InspectMove(1);
            yield return null;

            Assert.AreEqual("EnemyHitArea1", FocusName(pad), "Right steps to the next enemy");
            Assert.IsFalse(StatusBoxIsShown(), "an actor with no statuses shows no box");

            // RIGHT again: off the end of the monsters and onto the party,
            // which is the half of the owner's question about PCs.
            //
            // BY NAME PREFIX, not by a literal slot: the party's hit areas are
            // indexed by STAGE SLOT and the ring walks PLATES (FightScreen.
            // PartyHitAreas' own header -- "the two halves are indexed
            // differently and that is the point"), so which slot a one-member
            // party occupies is not this test's to pin. Which ACTOR the focus
            // reached is pinned by the row count instead.
            pad.InspectMove(1);
            yield return null;

            StringAssert.StartsWith("PartyHitArea", FocusName(pad),
                "the ring runs enemies first, then the party's own figures");
            Assert.IsTrue(StatusBoxIsShown(), "the squadmate's own statuses read the same way");
            Assert.AreEqual(1, VisibleStatusBoxRows(), "one row for the one status on the hero");

            // RIGHT once more wraps back to the front of the ring.
            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual("EnemyHitArea0", FocusName(pad),
                "forward off the last squadmate wraps round to the first monster");

            // LEFT from the front of the ring is the way home.
            pad.InspectMove(-1);
            yield return null;

            Assert.AreEqual("Verb0", FocusName(pad), "Left off the first enemy returns to the verb column");
            Assert.IsFalse(StatusBoxIsShown(), "and the box goes with the focus");
            Assert.IsFalse(IsShown("EnemyHitArea0"), "and the figure targets go back down with it");
        }

        [UnityTest]
        public IEnumerator SubmitWhileInspectingCommitsToNothing()
        {
            yield return StandUpAFight();

            var pad = (IFightNavigationTarget)_fight;

            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual("EnemyHitArea0", FocusName(pad), "precondition: reading the first enemy");

            pad.ConfirmFocus();
            yield return null;

            // A pad player reading a monster has no verb pending, so there is
            // nothing for Submit to confirm -- it must not fall through to
            // Root's own ConfirmFocus and open ATTACK on whatever they were
            // only looking at.
            Assert.IsFalse(IsShown("TargetPrompt"),
                "Submit while inspecting opened a target pick");
            Assert.AreEqual(-1, _fight.HoveredEnemyIndexForTest,
                "inspecting never touches the target cursor a real pick uses");
            Assert.AreEqual("EnemyHitArea0", FocusName(pad), "and the focus has not moved either");

            // The click half of the same claim: the figure the pad is perched
            // on is not a button a MOUSE could start that pick with either.
            AssertRaycasts("EnemyHitArea0", false, "a perched figure target is still not clickable");
        }

        [UnityTest]
        public IEnumerator BackFromInspectingReturnsToTheVerbTheStickLeftFrom()
        {
            yield return StandUpAFight();

            var pad = (IFightNavigationTarget)_fight;

            // Two presses UP the bottom-up column, so the verb returned to is
            // demonstrably not index 0's default.
            pad.MoveFocus(-1);
            pad.MoveFocus(-1);
            yield return null;
            Assert.AreEqual(2, _fight.FocusedVerbForTest, "precondition: the stick walked to ITEM");

            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual("EnemyHitArea0", FocusName(pad), "precondition: reading the first enemy");
            Assert.IsTrue(StatusBoxIsShown(), "precondition: the box is up");

            pad.OnBackPressed();
            yield return null;

            Assert.AreEqual(2, _fight.FocusedVerbForTest, "B returns to the verb the stick left from");
            Assert.AreEqual("Verb2", FocusName(pad), "and the marker goes back with it");
            Assert.IsFalse(StatusBoxIsShown(), "and the box closes");
        }

        // TARGET-PICK SEMANTICS CHANGED, DELIBERATELY, AT ONE DEPTH ONLY
        // (the owner's 2026-09-19 follow-up: "selecting different mobs with
        // gamepad goes with up down, but it should work with left right").
        // Left/Right at Target depth now CYCLES the rack, the same rack
        // Up/Down already walk -- see HorizontalAtTargetDepthCyclesTargets
        // and its ally twin below. What survives from the old claim is
        // narrower than it was: a submenu LIST (a skill or item row) still
        // answers to Up/Down alone, and cycling a target -- on either axis
        // -- still only ever HOVERS one; a player aiming a spell still
        // cannot have a press quietly CONFIRM the pick out from under them.
        [UnityTest]
        public IEnumerator HorizontalAtTargetDepthCyclesTargets()
        {
            yield return StandUpAFight();

            var pad = (IFightNavigationTarget)_fight;

            // ATTACK, straight to the enemy rack, nothing hovered yet.
            pad.ConfirmFocus();
            yield return null;
            Assert.IsTrue(IsShown("TargetPrompt"), "precondition: a target pick is open");

            // RIGHT from no hover treats enemy 0 as ALREADY hovered (the gate's
            // own 2026-09-19 fix, CycleTargetFromPad) and steps one slot
            // DEEPER from it -- enemy 1, not enemy 0. A bare "first press
            // lands on 0" would be no visible change at all, since the marker
            // already sat on EnemyHitArea0 before this press (FocusedElement's
            // own no-hover rule, pinned in FightFocusMarkerTests).
            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest,
                "Right from a fresh pick steps one slot deeper than the implicit enemy 0");
            Assert.AreEqual("EnemyHitArea1", FocusName(pad), "and the marker follows onto its figure");

            // THE OTHER HALF OF ShowFigureTarget's SPLIT: a figure that is a
            // real mark takes clicks, exactly as it always did. Pinned beside
            // the perched case so the two cannot drift into one.
            AssertRaycasts("EnemyHitArea1", true, "a figure being picked is still a click target");

            // RIGHT again: one slot DEEPER still, which on a two-enemy rack
            // wraps off the far end back to the near one.
            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual(0, _fight.HoveredEnemyIndexForTest, "Right again wraps to the near enemy");
            Assert.AreEqual("EnemyHitArea0", FocusName(pad), "and the marker follows it there");

            // RIGHT again returns to the deeper enemy -- an ordinary hovered
            // press, not a fresh pick, so it is unaffected by today's fix and
            // just proves the cycle keeps going.
            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual(1, _fight.HoveredEnemyIndexForTest, "Right cycles back to the deeper enemy");

            // LEFT is the reverse: one slot nearer, wrapping the other way.
            pad.InspectMove(-1);
            yield return null;
            Assert.AreEqual(0, _fight.HoveredEnemyIndexForTest, "Left off the deeper enemy wraps to the near one");
            Assert.AreEqual("EnemyHitArea0", FocusName(pad));

            // AND THE PICK IS STILL OPEN THROUGHOUT -- cycling a target with
            // either axis only ever hovers one; it never confirms it.
            Assert.IsTrue(IsShown("TargetPrompt"), "cycling with Left/Right must not close or confirm the pick");
        }

        // THE ALLY RACK'S DIAGONAL IS MIRRORED (FightStageAnchors.SlotOffset
        // negates X for the party), so its on-screen "right" is the enemy
        // rack's "left": Near.X 320 -> Far.X 810 becomes on-screen
        // -320 -> -810, meaning the FARTHER ally sits FURTHER LEFT. Right
        // therefore has to mean "one slot NEARER" here to keep pointing
        // rightward on screen -- InspectMove's own header states the same
        // rule this test pins in literal plate indices.
        [UnityTest]
        public IEnumerator HorizontalAtTargetDepthCyclesAllyTargets()
        {
            yield return LoadFight();

            // A hand-built two-PC party and a single-ally skill (a ward: no
            // mana, no resource, no cooldown -- the cheapest way to stand up
            // an ally pick, same fixture shape AllyTargetPickerTests uses).
            // ContentDatabase's own roster is not guaranteed to hand the
            // first character a SingleAlly skill, and this test's claim has
            // nothing to do with which one is on the row.
            var caster = new CombatantState("Shawn", true, 300, 30, 40, 30);
            var ally = new CombatantState("Bjorn", true, 300, 30, 40, 8);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 1);
            var encounter = new CombatEncounter(new[] { caster, ally }, new[] { foe });

            var ward = new Domain.Content.ResolvedSkill("fleece_ward", "Ward", "", "sheep", 1,
                SkillEffect.Ward, SkillTargeting.SingleAlly,
                0, 0, false, 0, 50, false, null, Domain.Content.SpellPresentation.None, 0);

            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", Domain.Content.CharacterRole.Support, new[] { ward }, null,
                    Domain.Stats.DamageType.Physical),
                new PlayerKit("bear", Domain.Content.CharacterRole.Tank, null, null,
                    Domain.Stats.DamageType.Physical),
            };
            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new Domain.Content.ResolvedEnemy("front", "Front", new Domain.Stats.StatBlock(),
                    5, 3, false, Domain.Stats.DamageType.Physical, Domain.Stats.DamageType.Physical, 0), false),
            };

            var session = new FightSession(encounter, kits, enemyKits, new Domain.Rng.SeededRandom(9));
            session.Begin();
            _fight.Bind(session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            Assert.AreSame(caster, session.Current, "fixture: the ward's owner has to be the one acting");

            var pad = (IFightNavigationTarget)_fight;

            // Up once (bottom-up column) reaches SKILL; Submit opens the
            // list, Submit again presses row 0 -- the ward -- which opens
            // the party rack (AllyTargetPickerTests' own
            // AWardOpensThePartyRackAndClosesTheEnemyOne covers that half;
            // this test's claim starts once the rack is already open).
            pad.MoveFocus(-1);
            pad.ConfirmFocus();
            yield return null;
            pad.ConfirmFocus();
            yield return null;

            Assert.IsTrue(IsShown("TargetPrompt"), "precondition: the ally pick opened");

            // RIGHT from no hover: both plates are pickable (a ward can
            // target its own caster), and Right means "toward the near end"
            // on this mirrored rack -- plate 1 (Bjorn), not plate 0.
            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual(1, _fight.HoveredAllyIndexForTest,
                "Right on the mirrored ally rack lands nearer (plate 1), not deeper");

            // RIGHT again steps to the other pickable plate -- the caster,
            // one slot DEEPER on this rack's own diagonal even though it
            // reads as the "other end" rather than "further along".
            pad.InspectMove(1);
            yield return null;
            Assert.AreEqual(0, _fight.HoveredAllyIndexForTest, "Right again reaches the caster's own plate");

            // LEFT is the reverse, wrapping the other way -- back to Bjorn.
            pad.InspectMove(-1);
            yield return null;
            Assert.AreEqual(1, _fight.HoveredAllyIndexForTest, "Left off the caster wraps to the other plate");

            Assert.IsTrue(IsShown("TargetPrompt"), "cycling with Left/Right must not close or confirm the pick");
        }

        // THE OTHER SURVIVING HALF OF THE OLD CLAIM: a submenu LIST (a
        // skill or an item row) is walked by Up/Down alone, same as before
        // this pass -- only Target depth gained a horizontal meaning.
        [UnityTest]
        public IEnumerator HorizontalAtSubmenuDepthStillDoesNothing()
        {
            yield return LoadFight();

            // A hand-built one-skill kit, the same shape
            // FightSubmenuAffordabilityTests uses -- ContentDatabase's real
            // roster is not guaranteed to hand the first character an
            // affordable skill, and this test's claim has nothing to do
            // with which skill is on the row.
            var hero = new CombatantState("Shawn", true, 300, 5, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });

            var skill = new Domain.Content.ResolvedSkill("s", "Spellblade", "", "shawn", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, manaCost: 5, resourceCost: 0,
                spendsAllResource: false, power: 1, flatAmount: 0, ignoresDefense: false, damageInstances: null,
                presentation: Domain.Content.SpellPresentation.None, sortOrder: 0);

            var kit = new PlayerKit("shawn", Domain.Content.CharacterRole.Tank,
                new List<Domain.Content.ResolvedSkill> { skill }, null, null);
            var enemyKit = new EnemyKit(
                new Domain.Content.ResolvedEnemy("front", "Front", new Domain.Stats.StatBlock(), 5, 3, false,
                    Domain.Stats.DamageType.Physical, Domain.Stats.DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new Domain.Rng.SeededRandom(9));
            session.Begin();
            _fight.Bind(session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            var pad = (IFightNavigationTarget)_fight;

            // Up once from ATTACK (index 0, bottom of the column) reaches
            // SKILL (index 1) -- MovingFocusAtRootStepsBottomUpTheWayThe
            // ColumnIsBuilt's own ordering. Submit opens the one-row list.
            pad.MoveFocus(-1);
            pad.ConfirmFocus();
            yield return null;

            Assert.IsTrue(IsShown("CharacterSkill0"), "precondition: the one-skill kit opened its own row");

            pad.InspectMove(1);
            pad.InspectMove(-1);
            yield return null;

            Assert.IsTrue(IsShown("CharacterSkill0"), "Left/Right at a submenu still does not touch it");
        }

        // One hero, two monsters, and a status on each of the two actors the
        // ring tests read -- so "the box shows THIS actor's statuses" is a
        // literal row count rather than merely "something came up".
        private IEnumerator StandUpAFight()
        {
            yield return LoadFight();

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need at least two enemies to walk the ring");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            var enemy = built.Session.Encounter.Enemies[0];
            var member = built.Session.Encounter.PlayerParty[0];

            // ASSERTED, NOT ASSUMED (CODE_STANDARDS section 8): every row
            // count below is a literal, so a fixture that started carrying
            // statuses of its own fails here rather than quietly changing
            // what those literals mean.
            Assert.AreEqual(0, enemy.Statuses.Count, "precondition: the first enemy starts with no statuses");
            Assert.AreEqual(0, member.Statuses.Count, "precondition: the hero starts with no statuses");

            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Poison, 5, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Vulnerable, 15, 2);
            StatusEffects.Apply(member.Statuses, StatusEffectType.Vulnerable, 15, 2);

            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to drive input against");
        }

        // BY NAME, not by GameObject identity: which node the marker is on is
        // the claim, and a name says what failed when one of these goes red
        // ("EnemyPlate0" instead of "EnemyHitArea0" is a whole diagnosis;
        // "expected <GameObject> but was <GameObject>" is not).
        private static string FocusName(IFightNavigationTarget pad)
        {
            var focused = pad.FocusedElement as GameObject;
            return focused == null ? "<nothing>" : focused.name;
        }

        private static void AssertRaycasts(string name, bool expected, string because)
        {
            var node = Node(name);
            Assert.IsNotNull(node, $"the Fight scene has no {name} - rebuild the scenes (-BuildScenes)");

            var graphic = node.GetComponent<UnityEngine.UI.Graphic>();
            Assert.IsNotNull(graphic, $"{name} has no Graphic, so it can neither take nor refuse a click");
            Assert.AreEqual(expected, graphic.raycastTarget, because);
        }

        private static bool IsShown(string name)
        {
            var node = Node(name);
            Assert.IsNotNull(node, $"the Fight scene has no {name} - rebuild the scenes (-BuildScenes)");
            return node.activeInHierarchy;
        }

        // GameObject.Find cannot see it: the box is inactive whenever nothing
        // is being inspected, which is most of a fight and is exactly the
        // state half of these assertions are about.
        private static GameObject Node(string name)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            if (canvas == null) return null;

            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(includeInactive: true))
            {
                if (rect.name == name) return rect.gameObject;
            }

            return null;
        }

        private static bool StatusBoxIsShown()
        {
            var box = Node("StatusBox");
            Assert.IsNotNull(box, "the Fight scene has no StatusBox - rebuild the scenes (-BuildScenes)");
            return box.activeInHierarchy;
        }

        private static int VisibleStatusBoxRows()
        {
            int shown = 0;
            for (int i = 0; i < FightScreen.StatusBoxRows; i++)
            {
                var row = Node($"StatusBoxText{i}");
                Assert.IsNotNull(row, $"the Fight scene has no StatusBoxText{i}");
                if (row.activeInHierarchy) shown++;
            }

            return shown;
        }
    }
}
