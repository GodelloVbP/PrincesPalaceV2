using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "placeholder_brawler" };

            OpenParty();
            yield return null;

            var party = Party();
            Assert.IsNotNull(party, "the hub carries no PartyController");

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front], "seat 0 is not the front seat");
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("placeholder_brawler", party.Formation.SeatIds[PartySeat.Rear]);

            Assert.AreEqual("Odette", TextOf("PartySeat0Name"));
            Assert.AreEqual("Shawn", TextOf("PartySeat1Name"));
            Assert.AreEqual("Placeholder Brawler", TextOf("PartySeat2Name"));
        }

        // ---- camp: a roster card, then a seat -----------------------------------

        [UnityTest]
        public IEnumerator CampClickingASeatedCardThenASeatPersistsTheSwapToDisk()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "placeholder_brawler", "owl" };
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
            Assert.AreEqual("placeholder_brawler", party.Formation.SeatIds[PartySeat.Middle]);
            Assert.AreEqual("sheep", party.Formation.SeatIds[PartySeat.Rear]);

            SaveSlotManager.Forget();
            var reloaded = SaveSlotManager.CurrentSave;
            CollectionAssert.AreEqual(
                new[] { "owl", "placeholder_brawler", "sheep" }, reloaded.selectedCharacterIds,
                "the swap did not reach disk");
        }

        // ---- camp: seat to seat --------------------------------------------------

        [UnityTest]
        public IEnumerator CampSwappingTwoSeatsDirectly()
        {
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "placeholder_brawler", "owl" };

            OpenParty();
            var party = Party();
            yield return null;

            party.ClickSeat(PartySeat.Front);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front]);
            Assert.AreEqual("placeholder_brawler", party.Formation.SeatIds[PartySeat.Middle]);
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
    }
}
