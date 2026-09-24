using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
            SharedScene.AfterTest();
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

        // ---- P5: one common scale per slot kind, not a per-actor fit ------------
        //
        // Bjorn (bear, idle canvas 486x467) is delivered on a bigger canvas
        // than Shawn (sheep, 540x370) -- see PartyArtScale's own header for
        // the defect this pins shut. If PartyController still fit each
        // occupant into the seat box independently, both would be drawn at
        // the SAME height (the box's) regardless of canvas size; the whole
        // point of PartyArtScale.ScaleFor is that they are not.
        [UnityTest]
        public IEnumerator BjornsSeatArtIsTallerThanShawnsByTheirOwnCanvasRatio()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            OpenParty();
            yield return null;

            var shawnImage = NodeOf("PartySeat0Art").GetComponent<Image>();
            var bjornImage = NodeOf("PartySeat1Art").GetComponent<Image>();
            Assert.IsNotNull(shawnImage?.sprite, "Shawn's own idle art did not resolve -- nothing to compare");
            Assert.IsNotNull(bjornImage?.sprite, "Bjorn's own idle art did not resolve -- nothing to compare");

            float shawnDrawnHeight = shawnImage.rectTransform.rect.height;
            float bjornDrawnHeight = bjornImage.rectTransform.rect.height;

            // ONE SHARED SCALE means Bjorn's drawn height divided by Shawn's
            // must equal their raw SPRITE heights' own ratio -- exactly the
            // relative size the fight stage already draws them at, since
            // that stage applies no per-actor fit at all.
            float expectedBjornHeight = shawnDrawnHeight * (bjornImage.sprite.rect.height / shawnImage.sprite.rect.height);

            Assert.AreEqual(expectedBjornHeight, bjornDrawnHeight, 1f,
                "Bjorn's drawn seat-art height is not Shawn's own drawn height scaled by their sprite-height " +
                "ratio -- each is still being fit into the slot independently rather than at one common scale");
            Assert.Greater(bjornDrawnHeight, shawnDrawnHeight,
                "Bjorn's canvas (467px tall) is taller than Shawn's (370px) -- he should read as the taller " +
                "figure in the seat row, matching the fight stage rather than being shrunk to match the slot");
        }

        // ---- benching writes a seat, not a shorter list (AUDIT #93, #118) ----
        //
        // PartySeatGapRoundTripTests transcribes PartyController.SeatList and
        // the Refresh loop rather than calling them -- it is engine-free
        // Domain and cannot stand up a MonoBehaviour -- so THIS is what stops
        // the transcription drifting away from production in silence. It
        // presses the affordance and reads the file.
        [UnityTest]
        public IEnumerator BenchingTheFrontRankerWritesAnEmptyFrontSeatRatherThanPromotingTheMiddle()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            SaveSlotManager.SaveCurrent();

            OpenParty();
            var party = Party();
            yield return null;

            NodeOf("PartySeat0Button").GetComponent<Button>().onClick.Invoke();
            var bench = NodeOf("PartyBenchLink");
            Assert.IsNotNull(bench, "the Party pane has no PartyBenchLink");
            Assert.IsTrue(bench.activeInHierarchy,
                "the bench affordance is not offered for a selected front-ranker in Camp");
            bench.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsNull(party.Formation.SeatIds[PartySeat.Front],
                "fixture: the model should be holding the hole");

            SaveSlotManager.Forget();
            var reloaded = SaveSlotManager.CurrentSave;
            CollectionAssert.AreEqual(
                new[] { "", "bear", "owl" }, reloaded.selectedCharacterIds,
                "the save compacted the hole away, so bear is the seat enemy melee concentrates on " +
                "and nobody chose that");
            CollectionAssert.AreEqual(
                new[] { "bear", "owl" }, reloaded.ActiveSquadIds(),
                "an empty seat is a seat, not a party member");
        }

        // AND THE BENCH SURVIVES THE RELOAD (AUDIT #118). Reconcile runs on
        // every load; its top-up used to read "two of a possible three" as a
        // vacancy and hand the benched member straight back.
        [UnityTest]
        public IEnumerator ABenchedMemberIsStillBenchedAfterTheSaveIsReloaded()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
            save.Reconcile();
            SaveSlotManager.SaveCurrent();

            OpenParty();
            yield return null;

            NodeOf("PartySeat0Button").GetComponent<Button>().onClick.Invoke();
            NodeOf("PartyBenchLink").GetComponent<Button>().onClick.Invoke();
            yield return null;

            // A fresh load: SaveSystem.Load runs Migrate and Reconcile, which
            // is the step the finding is about.
            SaveSlotManager.Forget();
            var reloaded = SaveSlotManager.CurrentSave;

            CollectionAssert.AreEqual(
                new[] { "bear", "owl" }, reloaded.ActiveSquadIds(),
                "the benched member was handed back by the load, so Bench is undone by quitting");
        }

        // ---- fixture --------------------------------------------------------------

        // THE SCENE IS SHARED ACROSS THIS FIXTURE (SharedScene) while
        // consecutive tests ask for the same one. The pane itself needs no
        // reset: closing the menu deactivates it, whose OnDisable drops any
        // half-made selection, and OpenParty's reopen runs its OnEnable ->
        // Refresh against the save [SetUp] just pointed at a fresh root.
        //
        // What does need it is the toast. Close stops its fade coroutine with
        // the toast still showing, so without this a refusal test would read
        // the previous test's refusal off it.
        private IEnumerator LoadScene(string name)
        {
            yield return SharedScene.Ensure(name);

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (menu == null) yield break;
            if (menu.IsOpen) menu.Close();

            var toast = menu.GetComponentsInChildren<PartyToast>(includeInactive: true).FirstOrDefault();
            if (toast != null) toast.gameObject.SetActive(false);
            var toastText = menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == "PartyToastText")?.GetComponent<TMP_Text>();
            if (toastText != null) toastText.text = "";
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
