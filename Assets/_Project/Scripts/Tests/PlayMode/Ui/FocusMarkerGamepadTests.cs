using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE ONE PAD-FOCUS MARKER, through the REAL dispatcher (hardware round
    // 1's visual findings; Core/FocusMarker.cs has the argument, and
    // FocusMarkerPlacementTests pins the arithmetic with literal rects).
    //
    // What this file can prove that the EditMode placement tests cannot:
    // that the thing is actually created, parented, reparented, drawn and
    // hidden on real scenes by real input -- every press below goes through
    // ScriptedBaseInput, never through a SetSelectedGameObject standing in
    // for a Move (JourneyFixture's own rule, which this file inherits along
    // with its helpers rather than copying them).
    //
    // WHAT IS ASSERTED IS IDENTITY, NOT COORDINATES. `Marker.Target` is the
    // literal node the module settled on, so a test that checks it cannot
    // pass by re-running FocusMarkerPlacement's own arithmetic against
    // itself (CLAUDE.md gotcha 5). Where geometry matters at all -- "drawn
    // above everything" -- it is asserted as a hierarchy fact, which is what
    // draw order IS in uGUI.
    //
    // ONE MOVE PER TEST, or JourneyFixture.Move's own real-time settle
    // between two: StandaloneInputModule's move gate is a real-time
    // timestamp outside the scripted-input seam, so a second chained Move
    // inside 0.5s is silently swallowed (that method's own header).
    public class FocusMarkerGamepadTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-focus-marker-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            // No scene crossing in this file: every claim is about one
            // screen's own marker, and a stray Cancel falling through to
            // Navigation.Go would swap the scene out from under the
            // assertion.
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator LoadHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            // Start() runs one frame after activation; a second frame lets
            // the dispatcher settle the entry selection and place the marker.
            yield return null;
            TakeOverInput();
            yield return null;
            yield return null;
        }

        private static FocusMarker Marker => NavigationInputModule.Marker;

        private static void AssertMarkerIsOnTheSelection(string because)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, because + " (nothing was selected at all)");
            Assert.IsNotNull(Marker, because + " (no marker was ever created)");
            Assert.IsTrue(Marker.IsShown, because + " (the marker exists but is not drawn)");
            Assert.AreSame(selected.transform, Marker.Target, because);
        }

        // ---- it exists, it follows, and it is on top ---------------------------

        [UnityTest]
        public IEnumerator OnArrival_TheMarkerStandsOnWhateverTheScreenSelected()
        {
            yield return LoadHub();

            AssertMarkerIsOnTheSelection("the hub's entry is selected on arrival, so the marker belongs on it");
        }

        [UnityTest]
        public IEnumerator AMove_TakesTheMarkerToTheLiteralNodeTheMoveSelected()
        {
            yield return LoadHub();

            var before = Marker.Target;
            Assert.IsNotNull(before, "precondition: the marker is somewhere to begin with");

            // Up off the gate reaches the hub ring's left arm (the hub's own
            // authored order, hardware round 1 item 2) -- which building
            // exactly is that screen's business, not this file's, so the
            // claim is "wherever the Move went, the marker went too".
            yield return MoveUp();

            Assert.AreNotSame(before, Marker.Target, "the Move did not move the marker at all");
            AssertMarkerIsOnTheSelection("the marker must land on exactly the node the Move selected");
        }

        [UnityTest]
        public IEnumerator TheMarkerIsTheLastChildOfTheRootCanvas_WhichIsWhatDrawnOnTopMeansInUGui()
        {
            yield return LoadHub();

            var parent = Marker.transform.parent;
            Assert.IsNotNull(parent, "the marker was never parented to anything");

            var canvas = parent.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "the marker's parent is not a Canvas");
            Assert.IsTrue(canvas.isRootCanvas, "the marker hangs off a nested canvas, so a later sibling of "
                                               + "that canvas would draw over it");

            Assert.AreEqual(parent.childCount - 1, Marker.transform.GetSiblingIndex(),
                "uGUI draws later siblings on top, so the marker has to be the last one");
        }

        [UnityTest]
        public IEnumerator TheMarkerNeverEatsThePress_ItIsNotARaycastTarget()
        {
            yield return LoadHub();

            var image = Marker.GetComponent<UnityEngine.UI.Image>();
            Assert.IsNotNull(image);
            Assert.IsFalse(image.raycastTarget,
                "the marker sits over the control it points at, so a raycastable one would swallow the click");
        }

        // ---- whose hands are on the controls -----------------------------------

        [UnityTest]
        public IEnumerator AMouseClick_HidesTheMarker_AndTheNextPadMoveBringsItBack()
        {
            yield return LoadHub();

            Assert.IsTrue(Marker.IsShown, "precondition: the pad owns the screen on arrival");

            // A real click through the module's own raycast, not a
            // SetSelectedGameObject standing in for one -- the point of the
            // rule is that the POINTER moving is what silences the marker,
            // and only a real pointer move can prove it.
            // TalentsBuilding, not the gate: its onClick is
            // Navigation.Go(Talents), which this fixture's LoadOverride
            // swallows, so the click selects a control and changes nothing
            // else. The gate would start a descent.
            yield return Click(Node("TalentsBuilding"));

            Assert.IsFalse(NavigationInputModule.LastInputWasPad,
                "a click is a mouse, and the module has to have noticed");
            Assert.IsFalse(Marker.IsShown,
                "the marker is a pad affordance -- it must not jump to whatever the pointer just clicked");

            yield return MoveUp();

            Assert.IsTrue(NavigationInputModule.LastInputWasPad, "a stick past the threshold is the pad");
            AssertMarkerIsOnTheSelection("the next Move hands the screen back to the pad, marker and all");
        }

        [UnityTest]
        public IEnumerator APadFrameWinsATieWithTheMouse_SoADriftingPointerCannotFlickerIt()
        {
            yield return LoadHub();

            // Both in the same frame: the pointer moves AND the stick is
            // past the threshold. A worn pad and a mouse nudged by the desk
            // is an ordinary living-room frame, and the marker blinking off
            // mid-Move would be the visible symptom.
            Input.MousePosition = new Vector2(640f, 360f);
            Input.Horizontal = 1f;
            yield return DriveFrame();
            Input.Horizontal = 0f;

            Assert.IsTrue(NavigationInputModule.LastInputWasPad,
                "a frame carrying both devices is a player with a hand on the pad");
            Assert.IsTrue(Marker.IsShown);
        }

        // ---- the screens the owner named ---------------------------------------

        [UnityTest]
        public IEnumerator Talents_TheMarkerFollowsAMoveThroughTheTree()
        {
            // "The talent tree also has no visible focus" -- hardware round 1
            // fixed its LINKS (item 1) and said in its own status header that
            // a Move nobody can see is still half a fix. This is the half.
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            TakeOverInput();
            yield return null;
            yield return null;

            Assert.AreSame(Node("Orb0_0").transform, Marker.Target,
                "the talents entry is path 0's root orb, and the marker starts on it");

            // Up from the root reaches the centre tier-1 stone -- the literal
            // node TalentGamepadNavigationTests.Up_FromTheRoot_ReachesTheCentreChild
            // already pins, asserted here about the marker rather than about
            // the selection, so the two claims cannot drift apart.
            yield return MoveUp();

            Assert.AreSame(Node("Orb0_2").transform, Marker.Target,
                "the marker must follow the stick to the literal orb the Move selected");
            Assert.IsTrue(Marker.IsShown);
        }

        [UnityTest]
        public IEnumerator Party_MidCarry_TheMarkerStandsOnTheSlotSubmitWouldHit()
        {
            // The one screen with a legitimate SECOND need (the brief's own
            // item 3): a carry lights every VALID DESTINATION gold at once,
            // and the player still has to know which of them Submit will
            // actually resolve on. That distinction used to be the halo's;
            // it is the marker's now, and this is the test that says the two
            // are still distinguishable -- the ring says "legal", the arrow
            // says "this one".
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            TakeOverInput();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(SystemMenuTab.Party);
            yield return null;

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(party, "the Party pane has no controller");

            var source = Node($"PartySeat{PartySeat.Middle}Button");
            var destination = Node($"PartySeat{PartySeat.Rear}Button");
            Assert.IsNotNull(source, "the party pane did not build its seats");
            Assert.IsNotNull(destination);

            EventSystem.current.SetSelectedGameObject(source);
            yield return null;
            yield return PressSubmit();
            Assert.IsNotNull(party.Formation.SelectedId, "fixture: the Submit picked nobody up");

            EventSystem.current.SetSelectedGameObject(destination);
            yield return null;

            Assert.AreSame(destination.transform, NavigationInputModule.Marker.Target,
                "mid-carry, the marker has to be on the slot Submit will resolve on -- the gold ring says "
                + "which slots are legal, and it says so about several of them at once");
            Assert.IsTrue(NavigationInputModule.Marker.IsShown);
        }
    }
}
