using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE PIPS (plan C20): a strip chip lights when its character wants looking
    // at, a rail row when that section is what they are owed. Each is driven
    // through the control a player presses, and each must clear (or light) with no help from the
    // test: the pips follow the change, they are not repainted by hand.
    //
    // THE RULE ITSELF is CharacterAttentionTests' (EditMode); this proves the
    // wiring -- that the feed reads the right places and every change ends in a
    // repaint.
    public class DossierAttentionTests
    {
        private const string Shawn = "sheep";
        private const string Odette = "owl";
        private const string Bjorn = "bear";
        private const string Book = "mud_burst";

        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-attention-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };

            // Every event cue on the track runs off unscaled time; collapsed so
            // a claim settles inside a frame.
            RewardTrackController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.MarkDirty("DossierAttentionTests changes the roster's levels and loads its own scenes");
            SharedScene.AfterTest();
            RewardTrackController.SpeedMultiplier = 1f;
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- the arrangements ------------------------------------------------------------

        // Camp: the hub, three seated, everyone level 30 with everything
        // collected and no points to spend, so each test lights exactly what it
        // sets.
        private IEnumerator OpenAtCamp()
        {
            yield return SharedScene.Ensure("Hub");
            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            DossierPane.Rest();

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { Shawn, Odette, Bjorn };
            foreach (var member in save.roster) Settle(member);

            _menu.Open(SystemMenuTab.Character);
            yield return null;
            Assert.IsTrue(_dossier.SelectCharacter(Shawn), "fixture: Shawn is not on the strip");
            _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            _dossier.Refresh();
            yield return null;
        }

        // A descent: the map, exactly Shawn, Odette and Bjorn on the roster
        // (so what the hint names is what three characters hold), one book in
        // the pack and none placed. The pad is the device in hand, so the hint
        // names Start.
        private IEnumerator OpenInADescent()
        {
            NavigationInputModule.ResetInputDeviceForTests();
            RunManager.StartRun(4242UL);

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { Shawn, Odette, Bjorn };
            save.roster.RemoveAll(c => c.definitionId != Shawn && c.definitionId != Odette && c.definitionId != Bjorn);
            foreach (var member in save.roster) Settle(member);

            RunManager.Run.unassignedSpellBooks = new List<string> { Book };
            RunManager.Run.learnedSpells = new List<LearnedSpellEntry>();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
        }

        private static void Settle(Character member)
        {
            member.level = 30;
            member.claimedTrackLevel = 30;
            member.unspentStatPoints = 0;
        }

        private IEnumerator OpenLoadoutOf(string who)
        {
            _menu.Open(SystemMenuTab.Character);
            yield return null;
            Assert.IsTrue(_dossier.SelectCharacter(who), $"fixture: {who} is not on the strip");
            _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            yield return null;
        }

        // ---- reading the pips -------------------------------------------------------------

        private static bool Lit(string pip)
        {
            var node = Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.name == pip && t.gameObject.scene.IsValid());
            Assert.IsNotNull(node, $"the pane drew no pip named {pip}");
            return node.gameObject.activeSelf;
        }

        private static string RailPip(CharacterSection section) => $"DossierSection{CharacterSections.IndexOf(section)}Pip";

        // The chip showing `id`: by the name it draws, so the strip's order
        // (Rear, Middle, Front, then the bench) is not assumed here.
        private static string ChipPipOf(string id)
        {
            string name = ContentDatabase.GetCharacter(id).Data.DisplayName;
            for (int i = 0; i < DossierLayout.ChipCapacity; i++)
            {
                if (DossierPane.TextOf($"DossierChip{i}Name") == name) return $"DossierChip{i}Pip";
            }

            Assert.Fail($"no chip on the strip draws {name}");
            return null;
        }

        private static void AssertRail(bool loadout, bool attributes, bool rewards, string because)
        {
            Assert.AreEqual(loadout, Lit(RailPip(CharacterSection.Loadout)), $"{because}: LOADOUT's pip");
            Assert.AreEqual(attributes, Lit(RailPip(CharacterSection.Attributes)), $"{because}: ATTRIBUTES' pip");
            Assert.IsFalse(Lit(RailPip(CharacterSection.Skills)), $"{because}: SKILLS never carries a pip");
            Assert.AreEqual(rewards, Lit(RailPip(CharacterSection.RewardTrack)), $"{because}: REWARD TRACK's pip");
        }

        // ---- attribute points ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnUnspentPointLightsTheChipAndAttributesAndSpendingItClearsBoth()
        {
            yield return OpenAtCamp();
            AssertRail(false, false, false, "with nothing owed");
            Assert.IsFalse(Lit(ChipPipOf(Shawn)), "with nothing owed: Shawn's chip");

            DossierPane.Shown().unspentStatPoints = 1;
            _dossier.Refresh();

            AssertRail(false, true, false, "with a point to spend");
            Assert.IsTrue(Lit(ChipPipOf(Shawn)), "a point to spend: Shawn's chip");
            Assert.IsFalse(Lit(ChipPipOf(Odette)), "Odette owes nothing, and her chip must not borrow Shawn's pip");

            _dossier.ShowSection(CharacterSection.Attributes, focusRow: true);
            yield return null;
            DossierPane.Press("DossierAttrPlus0");

            Assert.AreEqual(0, DossierPane.Shown().unspentStatPoints, "fixture: the plus did not spend the point");
            AssertRail(false, false, false, "after spending it");
            Assert.IsFalse(Lit(ChipPipOf(Shawn)), "after spending it: Shawn's chip");
        }

        // ---- the reward track --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RewardsWaitingLightTheRewardTrackAndCollectingClearsItWithoutARefresh()
        {
            yield return OpenAtCamp();

            DossierPane.Shown().claimedTrackLevel = 0;
            _dossier.Refresh();
            AssertRail(false, false, true, "with thirty levels owed");
            Assert.IsTrue(Lit(ChipPipOf(Shawn)), "thirty levels owed: Shawn's chip");

            DossierPane.Press(DossierPane.SectionRow(CharacterSection.RewardTrack));
            yield return null;
            yield return null;
            DossierPane.Press("TrackCollectButton");
            yield return null;

            Assert.AreEqual(30, DossierPane.Shown().claimedTrackLevel, "fixture: the collect did not move the watermark");
            Assert.IsFalse(Lit(RailPip(CharacterSection.RewardTrack)),
                "the reward track's pip outlived the collecting that paid it");

            // THE CLAIM PAYS POINTS, and ATTRIBUTES now owes the player a
            // choice: a pip clearing on one row while another lights is the
            // whole point of drawing them per section.
            Assert.Greater(DossierPane.Shown().unspentStatPoints, 0, "fixture: the track paid no points");
            Assert.IsTrue(Lit(RailPip(CharacterSection.Attributes)), "the points the claim paid lit no pip");
            Assert.IsTrue(Lit(ChipPipOf(Shawn)), "the chip must stay lit while ATTRIBUTES is");
        }

        // ---- spell books (a descent) ------------------------------------------------------------

        [UnityTest]
        public IEnumerator ABookWaitingLightsLoadoutForWhoeverCanHoldItAndPlacingItClearsThem()
        {
            yield return OpenInADescent();
            yield return OpenLoadoutOf(Shawn);

            AssertRail(true, false, false, "a book waiting, Shawn");
            Assert.IsTrue(Lit(ChipPipOf(Shawn)), "a book waiting: Shawn's chip");
            Assert.IsTrue(Lit(ChipPipOf(Odette)), "a book waiting: Odette's chip (she holds books too)");
            Assert.IsFalse(Lit(ChipPipOf(Bjorn)), "Bjorn's Fury refuses books, so nothing waits for him");

            DossierPane.Press(DossierPane.PackBookCell(_dossier, Book));
            yield return null;

            Assert.AreEqual(1, RunManager.Run.learnedSpells.Count, "fixture: the press did not place the book");
            AssertRail(false, false, false, "after placing it");
            Assert.IsFalse(Lit(ChipPipOf(Shawn)), "after placing it: Shawn's chip");
            Assert.IsFalse(Lit(ChipPipOf(Odette)), "after placing it: Odette's chip -- no book is left for her either");
        }

        [UnityTest]
        public IEnumerator TakingABookBackOffItsSlotLightsThePipsAgain()
        {
            yield return OpenInADescent();
            yield return OpenLoadoutOf(Shawn);
            DossierPane.Press(DossierPane.PackBookCell(_dossier, Book));
            yield return null;
            AssertRail(false, false, false, "book placed");

            DossierPane.Press("DossierBook0");
            yield return null;

            Assert.AreEqual(0, RunManager.Run.learnedSpells.Count, "fixture: the slot press did not take the book back");
            AssertRail(true, false, false, "book back in the pack");
            Assert.IsTrue(Lit(ChipPipOf(Shawn)), "book back in the pack: Shawn's chip");
        }

        [UnityTest]
        public IEnumerator ABookWithNoFreeSlotLightsNothing()
        {
            yield return OpenInADescent();

            // Every slot of both holders full, and a fourth book still waiting.
            string[] placed = { "frost_flare", "lightning_bolt", "cinderfault" };
            for (int slot = 0; slot < placed.Length; slot++)
            {
                RunManager.Run.learnedSpells.Add(new LearnedSpellEntry { characterId = Shawn, skillId = placed[slot], slot = slot });
                RunManager.Run.learnedSpells.Add(new LearnedSpellEntry { characterId = Odette, skillId = placed[slot], slot = slot });
            }

            yield return OpenLoadoutOf(Shawn);

            AssertRail(false, false, false, "a book waiting and every slot full");
            Assert.IsFalse(Lit(ChipPipOf(Shawn)), "every slot full: Shawn's chip");
            Assert.IsFalse(Lit(ChipPipOf(Odette)), "every slot full: Odette's chip");
        }

        // The carried figure is not the fight's: outside a fight the rail's
        // level line is the level and nothing else, and no fight source is
        // registered to ask.
        [UnityTest]
        public IEnumerator OutsideAFightTheRailReadsTheLevelAlone()
        {
            yield return OpenInADescent();
            yield return OpenLoadoutOf(Shawn);

            Assert.AreEqual("Level 30", DossierPane.TextOf("DossierLevel"));
            Assert.IsNull(_menu.FightSource, "a descent's menu holds a fight source");
        }
    }
}
