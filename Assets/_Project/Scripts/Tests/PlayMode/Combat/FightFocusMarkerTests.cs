using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // FIGHT'S HALF of hardware round 1's visual findings, which is two
    // separate complaints from the owner about the same screen:
    //
    //   "there is no proper selector. Maybe we can make a small hovering
    //    arrow for the thing you're targeting with gamepad?"
    //   "attack is always seeming to be hovered over (not a gamepad bug) and
    //    makes it difficult to notice if you hover/select it."
    //
    // They are the same defect from both ends. Fight's model keeps a focused
    // verb index at ALL times (Submit has to have something to press), and
    // RefreshVerbs painted that index with ThemedMenuState.Open -- the same
    // plate that means "this branch is open" -- so index 0 wore the open
    // look from the moment the scene woke and nothing else on the screen said
    // where the pad was. There is a marker now (Core/FocusMarker.cs) and the
    // Open plate means only Open.
    //
    // WHY FIGHT NEEDED AN ACCESSOR AND NO OTHER SCREEN DID: the dispatcher
    // asserts EventSystem selection null every frame while Fight is top
    // (docs/GAMEPAD_NAVIGATION_PLAN.md section 3), so there is no selection
    // for the marker to read. IFightNavigationTarget.FocusedElement is the
    // model's own answer, read-only, built entirely from indices MoveFocus
    // and ConfirmFocus already maintained -- which is why
    // FightGamepadNavigationTests and AllyTargetPickerTests are untouched by
    // any of this.
    public class FightFocusMarkerTests
    {
        private FightController _fight;
        private ScriptedBaseInput _input;

        [SetUp]
        public void PlayFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void Restore() => TestGlobals.ResetAll();

        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the Fight scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // ONE STICK FRAME, through the real dispatcher. Fight's branch reads
        // the axis itself and has its own armed edge, so it never touches
        // StandaloneInputModule's real-time repeat gate -- which is why this
        // needs no settle and the ordinary-context files all do.
        private IEnumerator Move(float vertical)
        {
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        private IEnumerator PressSubmit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
        }

        private static GameObject Node(string name) => GameObject.Find(name);

        private static FocusMarker Marker => NavigationInputModule.Marker;

        // A bound, begun session with two enemies -- FightEncounterAdapter
        // plus Begin(), the same pair FightGamepadNavigationTests' own header
        // explains is needed before CanAct is ever true (Bind alone leaves no
        // current actor and every confirm silently no-ops).
        private IEnumerator BindARealEncounter()
        {
            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need two enemies for a target rack worth walking");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to drive input against");
        }

        // ---- the rest state (the ATTACK complaint) -----------------------------

        [UnityTest]
        public IEnumerator AtRest_AttackIsTheRecommendedDefault_NotAnOpenBranch()
        {
            yield return LoadFight();

            var attack = Node("Verb0").GetComponent<ThemedButtonState>();
            Assert.IsNotNull(attack, "Verb0 should be a themed button");

            // Open is "this verb's branch is showing", which at rest
            // nothing's is. Before hardware round 1 the pad's own focus index
            // painted Open here, unconditionally, forever; after it, index 0
            // still wore Primary's gold ring, and the owner's answer to
            // AUDIT.md #171 (2026-09-19) was that the ring reads as hover too.
            // So the resting column is flat: no verb is lit for any reason.
            Assert.AreEqual(ThemedMenuState.Idle, attack.CurrentMenuState,
                "nothing is open at rest, so no verb may wear a plate or a ring");
        }

        [UnityTest]
        public IEnumerator MovingThePadFocusOntoAVerb_DoesNotOpenIt()
        {
            yield return LoadFight();

            // A BOUND, BEGUN SESSION, because the dispatcher's own Fight
            // target refuses every press while _session is null
            // (FightController.Input's explicit interface members reinstate
            // PollGamepadNavigation's busy guard) -- a test that skipped this
            // would prove only that nothing happened.
            yield return BindARealEncounter();

            // Up off ATTACK, which in this bottom-up column is toward the
            // higher index (BuildVerbColumn's own order).
            yield return Move(1f);
            Assert.AreEqual(1, _fight.FocusedVerbForTest, "precondition: the stick moved the focus");

            Assert.AreEqual(ThemedMenuState.Idle, Node("Verb1").GetComponent<ThemedButtonState>().CurrentMenuState,
                "a focused verb is not an opened verb -- the marker is what says the pad is here");
            Assert.AreEqual(ThemedMenuState.Idle, Node("Verb0").GetComponent<ThemedButtonState>().CurrentMenuState,
                "and the verb the focus LEFT is drawn no differently from the two it never visited");
        }

        // ---- the marker (the "no proper selector" complaint) --------------------

        [UnityTest]
        public IEnumerator TheMarkerStandsOnTheFocusedVerb_AndFollowsAStickPress()
        {
            yield return LoadFight();
            yield return BindARealEncounter();

            // Fight asserts selection null every frame, so if the marker were
            // reading the EventSystem there would be nothing here at all.
            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "precondition: Fight's own branch holds selection at null");

            yield return Move(0f);
            Assert.IsNotNull(Marker, "Fight never created a marker");
            Assert.AreSame(Node("Verb0").transform, Marker.Target, "the marker starts on the focused verb");
            Assert.IsTrue(Marker.IsShown);

            yield return Move(1f);

            Assert.AreSame(Node("Verb1").transform, Marker.Target,
                "the marker must follow Fight's own focus, which is the only focus this screen has");
        }

        [UnityTest]
        public IEnumerator AtTargetDepth_TheMarkerStandsOnTheEnemyFigureSubmitWouldHit()
        {
            yield return LoadFight();
            yield return BindARealEncounter();

            // ATTACK (focus 0) skips the submenu straight to targeting -- the
            // exact path OnVerbPressed(0) takes for a click.
            yield return PressSubmit();
            Assert.IsNotNull(Node("TargetPrompt"),
                "precondition: the Submit reached Target depth (the prompt is only shown while targeting)");

            // THE FIGURE, not the plate. The owner asked for an arrow on
            // "the thing you're targeting", and the thing being targeted is
            // the monster on the battlefield, not its readout at the top of
            // the screen. EnemyHitArea<n> is the rect over that figure and is
            // shown only while a target is being picked.
            //
            // Nothing hovered yet: FocusedElement answers whatever
            // ConfirmFocus would actually press, which is the first living
            // enemy. The two read the same rule on purpose, so the arrow can
            // never point at one enemy while Submit hits another.
            Assert.AreSame(Node("EnemyHitArea0").transform, Marker.Target,
                "with nothing hovered, the marker sits on the figure Submit would land on");

            // STICK DOWN, not up, since 2026-09-19: the pad's vertical axis is
            // negated at Target depth (FightController.Input's
            // IFightNavigationTarget.MoveFocus -- the owner's "up is one slot
            // deeper"), so the press that walks the marker off the near end of
            // a two-enemy rack is the one that used to sit still.
            yield return Move(-1f);

            Assert.AreSame(Node("EnemyHitArea1").transform, Marker.Target,
                "and a stick press walks it along the rack");
        }
    }
}
