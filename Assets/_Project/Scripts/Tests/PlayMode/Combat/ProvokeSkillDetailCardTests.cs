using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // THE SCREEN'S OWN COPY OF THE BUG THE BALANCE BOT FOUND.
    //
    // GreedyAggressivePolicy.EstimateDamage already works around
    // SkillResolution.Amount having no case for eight authored player-skill
    // effects (Provoke, Transform, Ward, Shatter, BuffParty and the three
    // Gifts) -- that workaround is the bot's business and stays exactly as
    // it is. The fight screen has no such workaround: FightController.
    // Hud.cs's CurrentDetail() calls FightHudModel.DetailForSkill for
    // whichever row is hovered, DetailForSkill's POWER stat calls
    // PowerLabel, and PowerLabel calls FightSession.PreviewSkillPower, which
    // calls SkillResolution.Amount unconditionally -- so opening the detail
    // card for Provoke (or any of the other seven) threw
    // ArgumentOutOfRangeException out of the fight screen the instant the
    // player's mouse crossed the row, with no cast attempted and no button
    // pressed.
    //
    // Driven through the real door, the way FightSettlementTests opens a
    // fight: RunManager into a real room, the real Fight scene, the real
    // Skill verb button, and HoverRowForTest standing in for the mouse entry
    // OnRowHovered is normally wired to (see its own header).
    public class ProvokeSkillDetailCardTests
    {
        private const string SheepId = "sheep";
        private const string ProvokeSkillId = "provoke";
        private const string ProvokeTalentId = "sheep_ram_provoke_1";
        private const ulong Seed = 639228196442867409UL;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-provokecard-" + System.Guid.NewGuid().ToString("N"));
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
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator HoveringProvokesRowDoesNotThrowAndTheCardShowsNoMisleadingZero()
        {
            // THE SHEEP, ALONE, WITH PROVOKE BOUGHT. Fielding just the one
            // character makes it unambiguous which kit's rows the submenu is
            // showing, and adding the granting talent directly (rather than
            // its whole prerequisite chain) is the same shortcut
            // WardIsTalentGatedTests already takes for the same reason:
            // AvailableSkillsFor only checks the grantsSkillId talent is
            // unlocked, not its prerequisites.
            var save = SaveSlotManager.CurrentSave;
            var sheep = save.roster.First(c => c.definitionId == SheepId);
            sheep.unlockedTalentIds.Add(ProvokeTalentId);
            save.selectedCharacterIds = new List<string> { SheepId };

            RunManager.StartRun(Seed);

            var choices = RunManager.Choices();
            CollectionAssert.IsNotEmpty(choices, "the leg offered nowhere to go");
            var room = choices.FirstOrDefault(n => n.Type == RoomType.Fight) ?? choices[0];
            Assert.IsTrue(RunManager.MoveTo(room.Id), "could not move into the fight room");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession, "the room built no session");

            var actor = fight.Session.Encounter.PlayerParty.First();
            var kit = fight.Session.KitFor(actor);
            Assert.IsNotNull(kit, "fixture: the sheep has a kit");

            int index = -1;
            for (int i = 0; i < kit.Skills.Count; i++)
            {
                if (kit.Skills[i].Id == ProvokeSkillId) { index = i; break; }
            }

            Assert.GreaterOrEqual(index, 0,
                "fixture: Provoke is not on the sheep's kit -- the talent grant is broken, " +
                "which is a different bug than the one this test is pinning");

            // WAIT FOR THE PLAYER'S OWN TURN, the same gate PlayToTheEnd uses in
            // FightSettlementTests -- the Skill verb is not interactable while
            // the opening beat is still playing.
            Button verb1 = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (verb1 == null && Time.realtimeSinceStartup < deadline)
            {
                if (fight.IsBusy || !fight.Session.IsPlayerTurn) { yield return null; continue; }

                verb1 = fight.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(b => b.name == "Verb1" && b.interactable);
                if (verb1 == null) yield return null;
            }

            Assert.IsNotNull(verb1, "the Skill verb never became interactable");
            verb1.onClick.Invoke();
            yield return null;

            // THIS is the throw the balance bot's report describes: hovering
            // the row that opens the detail card, nothing more.
            Assert.DoesNotThrow(() => fight.HoverRowForTest(index),
                "opening Provoke's detail card threw out of the fight screen -- " +
                "SkillResolution.Amount has no case for SkillEffect.Provoke");

            var panel = fight.CurrentDetailForTest();
            Assert.AreEqual("Provoke", panel.Name, "fixture: the hovered row is not Provoke's own panel");

            var power = panel.Stats.FirstOrDefault(s => s.Key == "POWER");
            Assert.AreEqual("POWER", power.Key, "fixture: the detail card has no POWER stat at all");
            Assert.AreEqual("-", power.Value,
                "Provoke has no number to preview -- the whole action is redirecting who gets " +
                "attacked, not a health bar moving -- so the card should read '-' rather than a " +
                "'0' that reads as \"this does nothing\"");
        }
    }
}
