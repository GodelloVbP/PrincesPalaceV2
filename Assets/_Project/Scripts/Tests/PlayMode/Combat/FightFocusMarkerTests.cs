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
    // Two separate complaints about the same screen:
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

        // The same encounter with a twelve-potion satchel, the cheapest list
        // that overflows the submenu's five-row window.
        private IEnumerator BindAnEncounterWithALongSatchel()
        {
            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));

            var satchel = new List<SatchelStack>();
            for (int i = 0; i < 12; i++) satchel.Add(new SatchelStack($"potion_{i}", $"Potion {i + 1}", 3, restoresMana: false));

            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal, satchel);
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
            // nothing's is. The ring (Primary's gold ring) reads as hover
            // too, so the resting column is flat: no verb is lit for any
            // reason.
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

            // THE FIGURE, not the plate: an arrow on "the thing you're
            // targeting" points at the monster on the battlefield, not its
            // readout at the top of the screen. EnemyHitArea<n> is the rect
            // over that figure and is
            // shown only while a target is being picked.
            //
            // Nothing hovered yet: FocusedElement answers whatever
            // ConfirmFocus would actually press, which is the first living
            // enemy. The two read the same rule on purpose, so the arrow can
            // never point at one enemy while Submit hits another.
            Assert.AreSame(Node("EnemyHitArea0").transform, Marker.Target,
                "with nothing hovered, the marker sits on the figure Submit would land on");

            // STICK DOWN, not up: the pad's vertical axis is negated at
            // Target depth (FightController.Input's
            // IFightNavigationTarget.MoveFocus -- "up is one slot deeper"),
            // so the press that walks the marker off the near end of a
            // two-enemy rack is Down.
            yield return Move(-1f);

            Assert.AreSame(Node("EnemyHitArea1").transform, Marker.Target,
                "and a stick press walks it along the rack");
        }

        // The ITEM list wheeled two notches down with Potion 1
        // still focused puts the arrow in empty stage space above the list --
        // pointing at a row the viewport had clipped away. The marker now
        // hides while its row is scrolled out, and comes back ON THAT ROW'S
        // successor the moment the pad arrows (which scrolls it into view).
        [UnityTest]
        public IEnumerator AFocusedRowWheeledOutOfItsWindow_HidesTheMarker_UntilThePadBringsARowBack()
        {
            yield return LoadFight();
            yield return BindAnEncounterWithALongSatchel();

            // Up twice onto ITEM (verb 2 in this bottom-up column), then open it.
            yield return Move(1f);
            yield return Move(1f);
            Assert.AreEqual(2, _fight.FocusedVerbForTest, "precondition: the stick reached ITEM");
            yield return PressSubmit();
            yield return DriveFrame();

            Assert.AreSame(Node("CharacterSkill0").transform, Marker.Target,
                "precondition: opening the list focuses its first row");
            Assert.IsTrue(Marker.IsShown, "precondition: the first row is in view and the marker is on it");

            // The wheel, through the component the viewport really carries.
            var wheel = Node("SubmenuViewport").GetComponent<ListScroll>();
            Assert.IsNotNull(wheel, "nothing is listening for a wheel over the list");
            wheel.Scrolled(FightSubmenuLayout.RowPitch * 3f);
            yield return DriveFrame();

            Assert.AreSame(Node("CharacterSkill0").transform, Marker.Target,
                "the wheel scrolls the list, it does not move the focus");
            Assert.IsFalse(Marker.IsShown,
                "row 0 is scrolled out of the window; an arrow beside it would point at empty stage");

            // Stick down one row: the pad scrolls the row it lands on into view.
            yield return Move(-1f);

            Assert.AreSame(Node("CharacterSkill1").transform, Marker.Target,
                "precondition: stick down stepped the focus to the next row");
            Assert.IsTrue(Marker.IsShown, "the arrowed-onto row is back in the window, and so is the marker");
        }

        // The submenu stands 8px from the verb column, so the
        // arrow drawn LEFT of a focused list row would land on the ITEM / MOVE
        // verb and read as selecting it. Through the real dispatcher: open
        // the ITEM list, step to a row level with a verb, and the marker must
        // cover no verb button (FocusMarkerPlacement.EdgeFor's neighbour
        // rule moves it to the row's other side).
        [UnityTest]
        public IEnumerator TheMarkerBesideAFocusedSubmenuRow_CoversNoVerbButton()
        {
            yield return LoadFight();
            yield return BindAnEncounterWithALongSatchel();

            yield return Move(1f);
            yield return Move(1f);
            Assert.AreEqual(2, _fight.FocusedVerbForTest, "precondition: the stick reached ITEM");
            yield return PressSubmit();

            // Down to the fourth row -- the one level with the ITEM verb in
            // the QA capture (Rally beside ITEM).
            yield return Move(-1f);
            yield return Move(-1f);
            yield return Move(-1f);

            // Let any open animation settle; the marker re-decides whenever
            // its target's box moves.
            for (int i = 0; i < 20; i++) yield return DriveFrame();

            Assert.AreSame(Node("CharacterSkill3").transform, Marker.Target,
                "precondition: three stick-downs focus the fourth row");
            Assert.IsTrue(Marker.IsShown, "precondition: the marker is drawn");

            var frame = (RectTransform)Marker.transform.parent;
            var row = BoxIn(frame, (RectTransform)Node("CharacterSkill3").transform);

            float half = FocusMarkerPlacement.Size * 0.5f;
            var marker = new Rect(Marker.LocalPosition.x - half, Marker.LocalPosition.y - half,
                FocusMarkerPlacement.Size, FocusMarkerPlacement.Size);

            // Where the shape rule alone would have drawn it: left of the row.
            var leftOfRow = new Rect(row.xMin - FocusMarkerPlacement.Gap - FocusMarkerPlacement.Size,
                row.center.y - half, FocusMarkerPlacement.Size, FocusMarkerPlacement.Size);
            bool leftWasTaken = false;

            for (int v = 0; v < 4; v++)
            {
                var verb = Node($"Verb{v}");
                if (verb == null || !verb.activeInHierarchy) continue;

                var box = BoxIn(frame, (RectTransform)verb.transform);
                if (leftOfRow.Overlaps(box)) leftWasTaken = true;
                Assert.IsFalse(marker.Overlaps(box),
                    $"the marker at {Marker.LocalPosition} overlaps Verb{v} ({box}) -- it reads as selecting that verb");
            }

            Assert.IsTrue(leftWasTaken,
                "precondition: this row is level with a verb, so a marker on its left edge would cover it");

            Assert.AreEqual(row.center.y, Marker.LocalPosition.y, 0.5f,
                "still level with the focused row it points at");
        }

        // Moved to the row's right by the rule above, the arrow would stand
        // a few pixels off the list's scrollbar (the bar is 8px off the
        // rows). The bar is a keep-clear rect: the marker steps
        // past it and never comes nearer than 7 units (its 10-unit gap less
        // the 3-unit bob) at any point of the bob.
        [UnityTest]
        public IEnumerator TheMarkerBesideAScrollingList_StandsClearOfItsScrollbar()
        {
            yield return LoadFight();
            yield return BindAnEncounterWithALongSatchel();

            yield return Move(1f);
            yield return Move(1f);
            Assert.AreEqual(2, _fight.FocusedVerbForTest, "precondition: the stick reached ITEM");
            yield return PressSubmit();

            yield return Move(-1f);
            yield return Move(-1f);
            yield return Move(-1f);

            var track = Node("SubmenuScrollTrack");
            Assert.IsTrue(track != null && track.activeInHierarchy,
                "precondition: the long satchel scrolls, so its bar is drawn");

            var frame = (RectTransform)Marker.transform.parent;
            var bar = BoxIn(frame, (RectTransform)track.transform);
            float half = FocusMarkerPlacement.Size * 0.5f;

            // Sample across more than one bob period (1.6s) so the nearest
            // swing is in the sample, not just wherever frame 20 happened to
            // land.
            float nearest = float.MaxValue;
            float start = Time.unscaledTime;
            for (int f = 0; f < 600 && Time.unscaledTime - start < 1.8f; f++)
            {
                yield return DriveFrame();
                Assert.IsTrue(Marker.IsShown, "precondition: the marker is drawn");

                var marker = new Rect(Marker.LocalPosition.x - half, Marker.LocalPosition.y - half,
                    FocusMarkerPlacement.Size, FocusMarkerPlacement.Size);
                Assert.IsFalse(marker.Overlaps(bar),
                    $"the marker at {Marker.LocalPosition} overlaps the scrollbar ({bar})");

                float gap = marker.xMin >= bar.xMax ? marker.xMin - bar.xMax
                    : marker.xMax <= bar.xMin ? bar.xMin - marker.xMax
                    : 0f;
                if (marker.yMax > bar.yMin && marker.yMin < bar.yMax && gap < nearest) nearest = gap;
            }

            Assert.GreaterOrEqual(nearest, 7f - 0.5f,
                $"the marker came within {nearest} units of the scrollbar ({bar}) -- it crowds the bar");
        }

        private static Rect BoxIn(RectTransform frame, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var min = frame.InverseTransformPoint(corners[0]);
            var max = frame.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        // A HOVER CAN OUTLIVE THE MONSTER IT LANDED ON. AddEnemyHover wires
        // PointerEnter straight to OnEnemyHovered with no alive check, so the
        // pointer can still be sitting on a plate/figure whose enemy died
        // under it. FocusedElement/FocusedActor (FightController.Input.cs)
        // must not trust _hoveredEnemyIndex unconditionally: a dead hover
        // reads as no hover and falls back the same way ConfirmFocus's own
        // no-hover branch does.
        [UnityTest]
        public IEnumerator ADeadHoverReadsAsNoHover_TheMarkerFallsBackToTheFirstLivingEnemy()
        {
            yield return LoadFight();
            yield return BindARealEncounter();

            yield return PressSubmit();
            Assert.AreSame(Node("EnemyHitArea0").transform, Marker.Target,
                "precondition: OnVerbPressed(0) hovers the front living enemy explicitly");

            var session = _fight.GetType()
                .GetField("_session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_fight) as FightSession;
            Assert.IsNotNull(session, "could not reach the bound session to kill an enemy under the hover");

            session.Encounter.Enemies[0].CurrentHealth = 0;
            yield return null;

            Assert.AreSame(Node("EnemyHitArea1").transform, Marker.Target,
                "a dead hover falls back to the first living enemy, not a corpse");
        }

        // gap-fight-b-1: the PLATE's own hover, not the figure's hit area
        // ADeadHoverReadsAsNoHover exercises above. A plate is deactivated
        // (SetShown(false)) the instant its monster dies, and Unity delivers
        // no PointerExit to a disabled object, so a raw EventTrigger wiring
        // would leave _hoveredEnemyIndex and _inspectedActor on the corpse
        // forever -- not merely until the next refresh, which is what the
        // marker-only test above could not tell apart from this.
        // HoverIndex.OnDisable is what actually fires the missing exit; this
        // pins THAT wiring, not just the symptom-level fallback.
        [UnityTest]
        public IEnumerator HoveringAPlate_ThenKillingItsEnemy_ClearsTheHoverAndTheStatusBox()
        {
            yield return LoadFight();
            yield return BindARealEncounter();

            var plate = Node("EnemyPlate0");
            Assert.IsNotNull(plate, "no EnemyPlate0 in the Fight scene");

            var hover = plate.GetComponent<HoverIndex>();
            Assert.IsNotNull(hover, "EnemyPlate0 should carry a HoverIndex, not a raw EventTrigger");
            hover.OnPointerEnter(null);
            yield return null;

            Assert.AreEqual(0, _fight.HoveredEnemyIndexForTest,
                "precondition: hovering the plate set the hovered index");

            var session = _fight.GetType()
                .GetField("_session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_fight) as FightSession;
            Assert.IsNotNull(session, "could not reach the bound session to kill an enemy under the hover");

            session.Encounter.Enemies[0].CurrentHealth = 0;

            // A direct health poke, unlike a real cast, fires none of the
            // beat-resolution repaints on its own -- RefreshEnemyPlates only
            // runs from RefreshUi, so this drives it explicitly rather than
            // waiting frames for a repaint nothing will schedule.
            _fight.RefreshUi();
            yield return null; // OnDisable's own exit event lands the frame after SetShown(false)

            Assert.AreEqual(-1, _fight.HoveredEnemyIndexForTest,
                "the dead plate's OnDisable must clear the hover, not leave it pointed at the corpse");

            var inspectedActor = _fight.GetType()
                .GetField("_inspectedActor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_fight);
            Assert.IsNull(inspectedActor, "the status box must not still be describing the dead enemy");
        }
    }
}
