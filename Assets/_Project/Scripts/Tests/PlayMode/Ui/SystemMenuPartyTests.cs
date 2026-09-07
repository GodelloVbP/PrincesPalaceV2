using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
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
    // The Party pane inside the system menu: PartyController translating a
    // save into PartyFormation and back. The MODEL's own rules (a swap is a
    // swap, a locked seat refuses, a benched card cannot bench itself twice)
    // are PartyFormationTests' job (Domain, EditMode) -- what this pins is
    // that the controller reads the right save fields, writes the right ones
    // back, and reaches the model's decisions through an actual scene.
    //
    // CONTENT-SHAPE LIMIT, stated rather than hidden behind a skip
    // (CODE_STANDARDS SS8): characters.json authors exactly three characters
    // and the formation has exactly three seats, so every character is
    // always seated and no roster card is ever benched -- with today's
    // content there is no way to drive a genuinely benched card through the
    // UI, in Camp or in Run. That state (CardState.IsBenched, "not
    // selectable") is exercised at the model level by PartyFormationTests
    // instead, against a formation built directly rather than through a
    // save. This file covers what a save with 3-of-3 filled CAN reach: the
    // seat-swap paths, the Run-mode bench refusal, and ViewOnly.
    public class SystemMenuPartyTests
    {
        private string _root;
        private SystemMenuController _menu;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-party-tests-" + System.Guid.NewGuid().ToString("N"));
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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- opening paints the save's own order ------------------------------

        [UnityTest]
        public IEnumerator OpeningOnTheHubPaintsThreeSeatsMatchingSelectedCharacterIds()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            OpenParty();
            yield return null;

            var party = Party();
            Assert.IsNotNull(party, "the hub carries no PartyController");

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front], "seat 0 is not the front seat");
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("bear", party.Formation.SeatIds[PartySeat.Rear]);

            Assert.AreEqual("Odette", TextOf("PartySeat0Name"));
            Assert.AreEqual("Shawn", TextOf("PartySeat1Name"));
            Assert.AreEqual("Bjorn", TextOf("PartySeat2Name"));
        }

        // ---- camp: a roster card, then a seat -----------------------------------

        [UnityTest]
        public IEnumerator CampClickingASeatedCardThenASeatPersistsTheSwapToDisk()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            SaveSlotManager.SaveCurrent();

            OpenParty();
            var party = Party();
            yield return null;

            // owl (rear) is the "card" half of this path -- with all three
            // seats always filled (see this file's own header), every card
            // is also a seat, so clicking it is ClickCard selecting a SEAT
            // source rather than a Roster one. Clicking the front seat next
            // commits a SWAP through that same selection.
            party.ClickCard("owl");
            party.ClickSeat(PartySeat.Front);
            yield return null;

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual("bear", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Rear]);

            SaveSlotManager.Forget();
            var reloaded = SaveSlotManager.CurrentSave;
            CollectionAssert.AreEqual(
                new[] { "owl", "bear", "sheep" }, reloaded.selectedCharacterIds,
                "the swap did not reach disk");
        }

        // ---- camp: seat to seat --------------------------------------------------

        [UnityTest]
        public IEnumerator CampSwappingTwoSeatsDirectly()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            OpenParty();
            var party = Party();
            yield return null;

            party.ClickSeat(PartySeat.Front);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual("bear", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Rear]);
            Assert.AreEqual("Swapped Shawn and Odette.", TextOf("PartyToastText"));
        }

        // ---- run mode: reposition only, benching refused -------------------------

        [UnityTest]
        public IEnumerator InARunSendingASeatedMemberToTheBenchIsRefused()
        {
            yield return LoadScene("Map");

            OpenParty();
            var party = Party();
            yield return null;

            Assert.AreEqual(PartyMode.Run, party.Formation.Mode, "the map's copy of the pane did not read as a run");

            party.ClickSeat(PartySeat.Front);
            yield return null;
            Assert.IsFalse(party.Formation.CanSendToBench,
                "Camp's own bench link condition is true mid-run - SendToBench would silently no-op instead of refusing");

            var before = SaveSlotManager.CurrentSave.selectedCharacterIds.ToList();

            party.SendToBench();
            yield return null;

            Assert.AreEqual("Mid-run you can only reposition.", TextOf("PartyToastText"),
                "the refusal toast did not reach the pane");
            CollectionAssert.AreEqual(before, SaveSlotManager.CurrentSave.selectedCharacterIds,
                "a refused bench still changed the seating");
        }

        // ---- view-only: the fight's copy is inert --------------------------------

        [UnityTest]
        public IEnumerator InAFightEveryClickLeavesTheSaveUnchanged()
        {
            yield return LoadScene("Fight");

            OpenParty();
            var party = Party();
            yield return null;

            Assert.AreEqual(PartyMode.ViewOnly, party.Formation.Mode, "the fight's copy of the pane is not locked");

            var before = SaveSlotManager.CurrentSave.selectedCharacterIds.ToList();

            party.ClickSeat(PartySeat.Front);
            party.ClickCard("owl");
            party.SendToBench();
            party.Cancel();
            yield return null;

            CollectionAssert.AreEqual(before, SaveSlotManager.CurrentSave.selectedCharacterIds,
                "a click in a fight changed the party");
            Assert.AreEqual("The formation is fixed during a fight.", TextOf("PartyToastText"));
        }

        // ---- every art slot shows exactly one of a sprite or a monogram --------

        [UnityTest]
        public IEnumerator EverySeatAndCardArtSlotShowsExactlyOneOfArtOrMonogram()
        {
            yield return LoadScene("Hub");

            OpenParty();
            var party = Party();
            yield return null;

            // Every seat is occupied with today's 3-character, 3-seat
            // content, so this covers every seat by construction; an empty
            // seat (never reachable today - see this file's own header)
            // would show neither, which is correct for "nobody is there" and
            // is not what this test is about.
            for (int seat = 0; seat < PartySeat.Count; seat++)
            {
                bool art = IsActive($"PartySeat{seat}Art");
                bool monogram = IsActive($"PartySeat{seat}MonogramPlate");
                Assert.AreNotEqual(art, monogram,
                    $"seat {seat} shows both an art sprite and its monogram fallback, or neither");
            }

            for (int card = 0; card < 3; card++)
            {
                bool art = IsActive($"PartyCard{card}Art");
                bool monogram = IsActive($"PartyCard{card}MonogramPlate");
                Assert.AreNotEqual(art, monogram,
                    $"card {card} shows both an art sprite and its monogram fallback, or neither");
            }
        }

        // ---- P4: drag-and-drop, the second input path ---------------------------
        //
        // Driven through PartyDragSource's own PUBLIC IBeginDragHandler/
        // IEndDragHandler methods, reached off the scene the same way TextOf/
        // IsActive already reach a node -- CODE_STANDARDS SS4a's own rule
        // ("PlayMode tests drive the UI through scenes and public API like a
        // player does") is why this cannot reach PartyController's internal
        // seatDragSources/cardDragSources arrays directly.

        [UnityTest]
        public IEnumerator DraggingASeatOntoAnotherSeatSwapsAndPersists()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            SaveSlotManager.SaveCurrent();

            OpenParty();
            var party = Party();
            yield return null;

            var fromGo = NodeOf("PartySeat0Button");
            var toGo = NodeOf("PartySeat2Button");
            var drag = DragSourceOf(fromGo);

            drag.OnBeginDrag(FakePointer(fromGo));
            drag.OnEndDrag(FakePointer(toGo));
            yield return null;

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual("bear", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Rear]);

            SaveSlotManager.Forget();
            var reloaded = SaveSlotManager.CurrentSave;
            CollectionAssert.AreEqual(
                new[] { "owl", "bear", "sheep" }, reloaded.selectedCharacterIds,
                "the dragged swap did not reach disk");
        }

        [UnityTest]
        public IEnumerator DraggingARosterCardOntoASeatCommitsThroughTheSameContract()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            SaveSlotManager.SaveCurrent();

            OpenParty();
            var party = Party();
            yield return null;

            // owl is PartyCard2 (characters.json's own order: sheep,
            // bear, owl) and, per this file's own header,
            // always also a seat with today's 3-of-3 content -- this drives
            // the CARD drag source's own entry point (BeginCardDragAt)
            // rather than the seat's, landing on the front seat.
            var cardGo = NodeOf("PartyCard2Button");
            var seatGo = NodeOf("PartySeat0Button");
            var drag = DragSourceOf(cardGo);

            drag.OnBeginDrag(FakePointer(cardGo));
            drag.OnEndDrag(FakePointer(seatGo));
            yield return null;

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual("bear", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Rear]);

            SaveSlotManager.Forget();
            var reloaded = SaveSlotManager.CurrentSave;
            CollectionAssert.AreEqual(
                new[] { "owl", "bear", "sheep" }, reloaded.selectedCharacterIds,
                "the dragged placement did not reach disk");
        }

        [UnityTest]
        public IEnumerator DraggingASeatAndReleasingOverNothingCancels()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            SaveSlotManager.SaveCurrent();

            OpenParty();
            var party = Party();
            yield return null;

            var before = SaveSlotManager.CurrentSave.selectedCharacterIds.ToList();

            var seatGo = NodeOf("PartySeat0Button");
            var drag = DragSourceOf(seatGo);

            drag.OnBeginDrag(FakePointer(seatGo));
            drag.OnEndDrag(FakePointer(null));
            yield return null;

            Assert.IsNull(party.Formation.SelectedId, "a cancelled drag left a selection behind");
            CollectionAssert.AreEqual(before, SaveSlotManager.CurrentSave.selectedCharacterIds,
                "a drag that landed nowhere still changed the seating");
        }

        [UnityTest]
        public IEnumerator DraggingASeatOntoTheRosterZoneInARunIsRefusedWithTheToast()
        {
            yield return LoadScene("Map");

            OpenParty();
            var party = Party();
            yield return null;

            Assert.AreEqual(PartyMode.Run, party.Formation.Mode, "the map's copy of the pane did not read as a run");

            var before = SaveSlotManager.CurrentSave.selectedCharacterIds.ToList();

            var seatGo = NodeOf("PartySeat0Button");
            var zoneGo = NodeOf("PartyRosterDropZone");
            Assert.IsNotNull(zoneGo, "the party pane has no roster drop zone");
            var drag = DragSourceOf(seatGo);

            drag.OnBeginDrag(FakePointer(seatGo));
            drag.OnEndDrag(FakePointer(zoneGo));
            yield return null;

            Assert.AreEqual("Mid-run you can only reposition.", TextOf("PartyToastText"),
                "the refusal toast did not reach the pane");
            CollectionAssert.AreEqual(before, SaveSlotManager.CurrentSave.selectedCharacterIds,
                "a refused roster drop still changed the seating");
        }

        [UnityTest]
        public IEnumerator ACompletedDragDoesNotAlsoFireTheButtonsClick()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            SaveSlotManager.SaveCurrent();

            OpenParty();
            var party = Party();
            yield return null;

            var fromGo = NodeOf("PartySeat0Button");
            var toGo = NodeOf("PartySeat2Button");
            var drag = DragSourceOf(fromGo);
            var toButton = toGo.GetComponent<Button>();

            drag.OnBeginDrag(FakePointer(fromGo));
            drag.OnEndDrag(FakePointer(toGo));

            // uGUI is documented to still raise a Button's own onClick on the
            // pointer-up that ends a drag over it when the drag handler sits
            // on that same object -- PartyController.Wire's own per-frame
            // suppression (_dragResolvedFrame) is what has to eat this, not
            // luck. Invoking the destination's click explicitly, in the SAME
            // frame EndDrag just resolved in, is the worst case that
            // suppression exists to cover.
            toButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(party.Formation.SelectedId,
                "a click on the drag's own destination, in the same frame, re-selected its new occupant");
            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front], "the swap itself should still stand");
        }

        // ---- P4: the toast fades rather than hard-cutting ------------------------

        [UnityTest]
        public IEnumerator TheToastFadesOutOverTime()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            OpenParty();
            yield return null;

            var party = Party();
            party.ClickSeat(PartySeat.Front);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            var toastGo = NodeOf("PartyToast");
            var group = toastGo.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "the toast carries no CanvasGroup");
            Assert.AreEqual(1f, group.alpha, 0.01f, "the toast should show at full alpha right after a commit");

            yield return new WaitForSecondsRealtime(2.5f);

            Assert.IsTrue(group.alpha <= 0.01f || !toastGo.activeSelf,
                "the toast should have faded out (or deactivated) after 2.5s");
        }

        // ---- P4: the ground line, pinned against Shawn's own manifest entry -----

        [UnityTest]
        public IEnumerator ShawnsSeatArtSitsAboveTheSlotFloorByHisManifestGroundLine()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            OpenParty();
            yield return null;

            var artGo = NodeOf("PartySeat0Art");
            var image = artGo.GetComponent<Image>();
            Assert.IsNotNull(image, "PartySeat0Art carries no Image");
            Assert.IsNotNull(image.sprite, "Shawn's own idle art did not resolve -- nothing to ground");

            var rect = image.rectTransform;

            // PartyLayout.FeetLine is the shared bottom line every seat's art
            // slot pins to, in PANEL-local space; ColumnButtonCentreY converts
            // it into the seat BUTTON's own local space, which is what the
            // art node's anchoredPosition is actually measured in (it is a
            // child of the button) -- see PartyScreen.BuildSeat's own LocalY.
            float floorY = PartyLayout.FeetLine - PartyLayout.ColumnButtonCentreY;

            // Resources/StanceManifest.json's own "Characters/sheep" entry --
            // pinned as a literal per CODE_STANDARDS SS5 rather than re-read
            // from the asset, so this cannot become a tautology against the
            // very file it is checking.
            const float shawnGroundLine = 43f;

            float scale = Mathf.Min(
                rect.rect.width / image.sprite.rect.width, rect.rect.height / image.sprite.rect.height);
            float expected = floorY - shawnGroundLine * scale;

            Assert.AreEqual(expected, rect.anchoredPosition.y, 1.5f,
                "Shawn's seat art is not shifted off the slot floor by his own manifest ground line");
            Assert.Less(rect.anchoredPosition.y, floorY,
                "the ground-line shift should sit BELOW plain canvas-bottom, not above it");
        }

        // ---- fixture --------------------------------------------------------------

        private static IEnumerator LoadScene(string name)
        {
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private void OpenParty()
        {
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "this scene has no SystemMenuController");

            _menu.Open();
            _menu.Select(SystemMenuTab.Party);
        }

        private static PartyController Party() =>
            Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);

        private GameObject NodeOf(string nodeName) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == nodeName)?.gameObject;

        private string TextOf(string nodeName)
        {
            var go = NodeOf(nodeName);
            Assert.IsNotNull(go, $"the Party pane has no '{nodeName}'");

            var label = go.GetComponent<TMP_Text>();
            Assert.IsNotNull(label, $"'{nodeName}' carries no text");
            return label.text;
        }

        private bool IsActive(string nodeName)
        {
            var go = NodeOf(nodeName);
            Assert.IsNotNull(go, $"the Party pane has no '{nodeName}'");
            return go.activeSelf;
        }

        private static PartyDragSource DragSourceOf(GameObject go)
        {
            var drag = go.GetComponent<PartyDragSource>();
            Assert.IsNotNull(drag, $"'{go.name}' carries no PartyDragSource");
            return drag;
        }

        // `target == null` fabricates a drag that ends over nothing -- the
        // default RaycastResult's own gameObject is null, so EndDrag's hit
        // test reads it as DragTargetNone the same as a real pointer release
        // over open space would.
        private static PointerEventData FakePointer(GameObject target)
        {
            var data = new PointerEventData(EventSystem.current);
            if (target != null) data.pointerCurrentRaycast = new RaycastResult { gameObject = target };
            return data;
        }
    }
}
