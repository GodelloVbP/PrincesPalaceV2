using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Step D's behavioural gate (docs/GAMEPAD_NAVIGATION_PLAN.md section 8,
    // AUDIT.md #156): the Party pane carried and dropped on stick + Submit +
    // Cancel alone, through the REAL dispatcher on the real Hub scene.
    //
    // WHAT THIS DOES NOT DO, deliberately: it never calls ClickSeat,
    // Formation.Drop or ClaimCancel directly. SystemMenuPartyTests already
    // pins the model's own rules that way; what is new here is whether a
    // press reaches them, which only the module can answer.
    public class PartyGamepadNavigationTests
    {
        private string _root;
        private ScriptedBaseInput _input;
        private SystemMenuController _menu;
        private PartyController _party;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-party-pad-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). Closing the
        // menu (NavSceneReuse.CloseHubModals) is the reset: the Party pane's
        // own OnDisable cancels any carry the last test left, and its
        // OnEnable re-reads the formation from this test's fresh save when
        // Select(Party) shows it again below. The one thing a close does not
        // clear is a mouse drag in flight -- see SubmitDuringAMouseDrag.
        private IEnumerator OpenTheParty()
        {
            yield return SharedScene.Ensure("Hub");

            NavSceneReuse.CloseHubModals();
            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub carries no SystemMenuController");
            _menu.Open();
            _menu.Select(SystemMenuTab.Party);
            yield return null;

            _party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_party, "the hub carries no PartyController");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator SubmitFrame()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
        }

        private IEnumerator CancelFrame()
        {
            _input.CancelDown = true;
            yield return DriveFrame();
        }

        // A Move press, then the axis back to centre -- StandaloneInputModule
        // treats a held axis as a repeat, and a repeat is timing this suite
        // has no control over (plan section 10).
        private IEnumerator MoveRightFrame()
        {
            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            yield return DriveFrame();
        }

        // SEAT INDEX, NOT SCREEN COLUMN -- seat 0 is the front rank and is
        // drawn on the RIGHT (PartyLayout.VisualColumnForSeat), so "Right
        // from Rear" below lands on Middle, not on Front. The rail is built
        // in visual order for exactly this reason; these tests pin that.
        private GameObject Seat(int index) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .First(t => t.name == $"PartySeat{index}Button").gameObject;

        private void Select(GameObject go)
        {
            EventSystem.current.SetSelectedGameObject(go);
        }

        // ---- carry, move, drop ------------------------------------------------------

        [UnityTest]
        public IEnumerator SubmitPicksUp_MoveRelocatesTheCandidate_SubmitDropsOnWhatIsSelected()
        {
            yield return OpenTheParty();

            Assert.AreEqual("sheep", _party.Formation.SeatIds[PartySeat.Middle], "fixture: seats not as authored");
            Assert.AreEqual("bear", _party.Formation.SeatIds[PartySeat.Rear]);

            // REAR is the LEFTMOST column, so one Right press steps to
            // MIDDLE -- a plain step along the rail, no wrap involved.
            Select(Seat(PartySeat.Rear));
            yield return null;

            yield return SubmitFrame();

            Assert.AreEqual("bear", _party.Formation.SelectedId,
                "Submit on a selected seat should pick its occupant up -- the Button's own OnSubmit runs the " +
                "same onClick a mouse click does, which is ClickSeat");

            // ORDINARY Selectable.OnMove relocates the candidate: the seats
            // are a Rail, so Right steps to the next seat. Nothing in Party
            // handles Move at all -- that is the point of the seats being
            // real Buttons.
            yield return MoveRightFrame();

            Assert.AreEqual(Seat(PartySeat.Middle), EventSystem.current.currentSelectedGameObject,
                "a Move while carrying should walk the seat rail, not commit anything");
            Assert.AreEqual("bear", _party.Formation.SelectedId, "the Move committed the carry by itself");

            yield return SubmitFrame();

            // THE DESTINATION IS THE SELECTION, resolved through the seat the
            // module was standing on -- a swap, exactly as the same two
            // clicks would have produced.
            Assert.AreEqual("bear", _party.Formation.SeatIds[PartySeat.Middle],
                "the drop did not swap through the seat the selection was standing on");
            Assert.AreEqual("sheep", _party.Formation.SeatIds[PartySeat.Rear]);
            Assert.IsNull(_party.Formation.SelectedId, "the carry should be over once it is dropped");
        }

        // ---- Cancel, claimed and unclaimed -----------------------------------------

        [UnityTest]
        public IEnumerator CancelWhileCarrying_PutsThemBack_ReselectsTheSource_AndLeavesTheMenuOpen()
        {
            yield return OpenTheParty();

            Select(Seat(PartySeat.Middle));
            yield return null;
            yield return SubmitFrame();
            Assert.AreEqual("sheep", _party.Formation.SelectedId, "fixture: nothing was picked up");

            // Walk away from the source first, so "reselects the source" is a
            // real claim rather than "the selection never moved". Right from
            // MIDDLE is FRONT, the rightmost column.
            yield return MoveRightFrame();
            Assert.AreEqual(Seat(PartySeat.Front), EventSystem.current.currentSelectedGameObject);

            yield return CancelFrame();

            Assert.IsNull(_party.Formation.SelectedId, "Cancel while carrying should put the carried one back");
            Assert.IsTrue(_menu.IsOpen,
                "Cancel spent on the carry must NOT also close the menu -- the pane claimed that press " +
                "(INavCancelClaim), so SystemMenu's own Close never runs");
            Assert.AreEqual(Seat(PartySeat.Middle), EventSystem.current.currentSelectedGameObject,
                "selection should return to the seat the carry started from, not stay where it wandered to");
            Assert.AreEqual("sheep", _party.Formation.SeatIds[PartySeat.Middle],
                "nobody should have moved seats");
        }

        [UnityTest]
        public IEnumerator CancelWhileNotCarrying_ClosesTheMenu()
        {
            yield return OpenTheParty();

            Select(Seat(PartySeat.Front));
            yield return null;
            Assert.IsNull(_party.Formation.SelectedId, "fixture: something was already carried");

            yield return CancelFrame();

            Assert.IsFalse(_menu.IsOpen,
                "with nothing carried the pane declines the press and the context's own Cancel (Close) runs " +
                "-- the behaviour every other pane has");
        }

        [UnityTest]
        public IEnumerator ClosingTheMenuMidCarry_CancelsTheCarryRatherThanKeepingIt()
        {
            yield return OpenTheParty();

            Select(Seat(PartySeat.Front));
            yield return null;
            yield return SubmitFrame();
            Assert.AreEqual("owl", _party.Formation.SelectedId, "fixture: nothing was picked up");

            _menu.Close();
            yield return null;

            Assert.IsNull(_party.Formation.SelectedId,
                "leaving the pane mid-carry must end the transaction -- reopening would otherwise show a " +
                "banner and a bench link for a carry the player cannot see");
        }

        // ---- the drag guard ---------------------------------------------------------

        // A MOUSE DRAG IN FLIGHT OWNS THE GESTURE. Without the _dragging half
        // of PartyController.IgnoreClick, a Submit press landing mid-drag
        // would resolve the same carry a second time, against the selection
        // instead of the pointer.
        [UnityTest]
        public IEnumerator SubmitDuringAMouseDrag_IsIgnored()
        {
            yield return OpenTheParty();
            SharedScene.MarkDirty("leaves a mouse drag in flight (PartyController._dragging), which only the drag's own end clears");

            var front = Seat(PartySeat.Front);
            var drag = front.GetComponent<PartyDragSource>();
            Assert.IsNotNull(drag, "the seat button carries no PartyDragSource");

            drag.OnBeginDrag(new PointerEventData(EventSystem.current)
            {
                pointerCurrentRaycast = new RaycastResult { gameObject = front },
            });
            Assert.AreEqual("owl", _party.Formation.SelectedId, "fixture: the drag did not pick anybody up");

            // Submit on a DIFFERENT seat, which would otherwise drop there.
            Select(Seat(PartySeat.Rear));
            yield return null;
            yield return SubmitFrame();

            Assert.AreEqual("owl", _party.Formation.SelectedId,
                "the Submit resolved a carry the mouse still had in flight");
            Assert.AreEqual("owl", _party.Formation.SeatIds[PartySeat.Front],
                "the Submit moved somebody mid-drag");
            Assert.AreEqual("bear", _party.Formation.SeatIds[PartySeat.Rear]);
        }

        // ---- where the stick is standing --------------------------------------------
        //
        // ADAPTED, NOT RELAXED (hardware round 1's visual pass). This test
        // used to be TheSelectedSeatLightsItsOwnHalo_AndOnlyThatOne and
        // asserted PartySeat<n>SelectHalo's activeSelf. Those halos are gone
        // -- the owner played the screen on a pad and reported "the gold halo
        // (e.g. in party screen) is way too strong" -- and the claim they
        // were making is now made about the one arrow every screen shares.
        // Same two steps, same two seats, same "and only that one": it is the
        // indicator that changed, not what is being asked of it.

        [UnityTest]
        public IEnumerator TheFocusMarkerStandsOnTheSelectedSeat_AndFollowsAMoveOffIt()
        {
            yield return OpenTheParty();

            Select(Seat(PartySeat.Rear));
            yield return null;

            Assert.IsTrue(NavigationInputModule.Marker.IsShown, "the marker is not drawn at all");
            Assert.AreSame(Seat(PartySeat.Rear).transform, NavigationInputModule.Marker.Target,
                "the marker is not on the selected seat");

            yield return MoveRightFrame();

            Assert.AreSame(Seat(PartySeat.Middle).transform, NavigationInputModule.Marker.Target,
                "the marker did not follow the selection");
        }

        // ---- the links row (plan phase 3, item 3 -- the phase 2 gap named in ----
        // ---- the plan's own status header) --------------------------------------

        private Button CancelLink() =>
            _menu.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "PartyCancelLink");

        private Button BenchLink() =>
            _menu.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "PartyBenchLink");

        [UnityTest]
        public IEnumerator NotCarrying_TheLinksAreHiddenAndUnreachable()
        {
            yield return OpenTheParty();

            Assert.IsFalse(CancelLink().gameObject.activeSelf, "fixture: nothing is carried yet");
            Assert.IsFalse(BenchLink().gameObject.activeSelf);

            // Up from a seat with nothing carried should find no target at
            // all (the seat rail's own row has nothing above it) rather than
            // the hidden links -- RefreshNavigation only wires them in when
            // they are actually shown.
            Select(Seat(PartySeat.Rear));
            yield return null;

            _input.Vertical = 1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Seat(PartySeat.Rear), EventSystem.current.currentSelectedGameObject,
                "Up should not have moved onto a hidden, unwired link");
        }

        // A helper for the three tests below, all starting from the same
        // "bear picked up from Rear" state -- ONE Submit, so each test then
        // drives exactly the one press its own claim is about.
        private IEnumerator PickUpBearFromRear()
        {
            Select(Seat(PartySeat.Rear));
            yield return null;
            yield return SubmitFrame();
            Assert.AreEqual("bear", _party.Formation.SelectedId, "fixture: nothing was picked up");
            Assert.IsTrue(_party.Formation.CanSendToBench, "fixture: this carry should be bench-eligible");
        }

        [UnityTest]
        public IEnumerator CarryingFromAnEligibleSeat_RevealsBothLinks()
        {
            yield return OpenTheParty();
            yield return PickUpBearFromRear();

            Assert.IsTrue(CancelLink().gameObject.activeSelf, "carrying should reveal the cancel link");
            Assert.IsTrue(BenchLink().gameObject.activeSelf, "carrying from an eligible seat should reveal " +
                "the bench link");
        }

        [UnityTest]
        public IEnumerator Up_FromTheSeatRailWhileCarrying_ReachesTheLinksRow()
        {
            yield return OpenTheParty();
            yield return PickUpBearFromRear();

            _input.Vertical = 1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(CancelLink().gameObject, EventSystem.current.currentSelectedGameObject,
                "Up from the seat rail while carrying should reach the links row -- cancelLink is the " +
                "wider-shown of the two and the group's own first member");
        }

        [UnityTest]
        public IEnumerator Right_FromCancelLink_ReachesBenchLink()
        {
            yield return OpenTheParty();
            yield return PickUpBearFromRear();

            Select(CancelLink().gameObject);
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(BenchLink().gameObject, EventSystem.current.currentSelectedGameObject,
                "the links row is an ordinary Rail -- Right from cancel should reach bench");
        }

        [UnityTest]
        public IEnumerator Submit_OnBenchLinkWhileCarrying_BenchesExactlyOnce()
        {
            yield return OpenTheParty();
            yield return PickUpBearFromRear();

            Select(BenchLink().gameObject);
            yield return null;
            yield return SubmitFrame();

            Assert.IsNull(_party.Formation.SelectedId, "benching should end the carry");
            Assert.IsFalse(_party.Formation.SeatIds.Contains("bear"),
                "Submit on the bench link should bench the carried character exactly once, through the " +
                "same SendToBench the mouse click calls");
        }

        [UnityTest]
        public IEnumerator CarryingFromTheRoster_OnlyCancelIsReachable_NotBench()
        {
            yield return OpenTheParty();

            // OWL LEFT BENCHED rather than a fabricated fourth character --
            // this project's content has exactly three characters (sheep,
            // bear, owl; ContentDatabase.Characters.Count), which is also
            // RosterCardCount (HubScreen.Build(ContentDatabase.Characters.
            // Count)), so there is no fourth card to reach. The roster row
            // shows every roster member regardless of seating, so leaving
            // owl out of selectedCharacterIds gives an unseated, reachable
            // "PartyCard2Button" (roster order is file order: sheep, bear,
            // owl) without inventing an id the content database has never
            // heard of.
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear" };
            SaveSlotManager.SaveCurrent();
            _party.Refresh();
            yield return null;

            var rosterCard = _menu.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "PartyCard2Button");
            Assert.IsTrue(rosterCard.gameObject.activeSelf, "fixture: owl's card should still be populated, " +
                "just unseated");

            Select(rosterCard.gameObject);
            yield return null;
            yield return SubmitFrame();
            Assert.AreEqual("owl", _party.Formation.SelectedId, "fixture: the roster card was not picked up");

            Assert.IsTrue(CancelLink().gameObject.activeSelf, "carrying should reveal the cancel link");
            Assert.IsFalse(BenchLink().gameObject.activeSelf,
                "a roster carry is not a seated one -- CanSendToBench requires SelectedFrom.Kind == Seat");
        }
    }
}
