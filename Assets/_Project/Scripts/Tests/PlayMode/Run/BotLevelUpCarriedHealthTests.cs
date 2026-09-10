using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.PlayModeTests
{
    // THE BOT'S DOOR ONTO THE REWARD TRACK, held to the same rule as the
    // screen's.
    //
    // Collecting a track node can move a character's maximum health, and the
    // run stores current health as an ABSOLUTE number -- so the claim has to be
    // photographed before and rescaled after, or the bar grows a tail of empty
    // that nothing in the descent can fill. RewardTrackController.Input.Claim
    // has done that since the track shipped; BotRunDriver.CollectLevelUps is
    // the second door onto the same claim and did not, so every batch the bot
    // reported was measured against parties quietly weaker than the game's.
    //
    // PlayMode because the claim reads ContentDatabase and the rescale reads
    // the live save, both Core.
    public class BotLevelUpCarriedHealthTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-bot-levelup-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE WHOLE TRACK RATHER THAN ONE NODE, deliberately: which single
        // level grants max health is a content decision, and a test that
        // claimed a node granting none would pass by proving nothing. Claiming
        // through the top guarantees the maximum moves, which is the condition
        // the rescale exists for.
        //
        // Every number below is a literal read off content and written down,
        // never re-derived from the production formula -- CLAUDE.md gotcha 5.
        // If the track or the base stats change, the three literals move
        // together and this fails loudly rather than agreeing with whatever the
        // code now does.
        [Test]
        public void ClaimingATrackNodeMidRunPreservesTheCarriedFraction()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = save.ActiveSquad().First();
            character.level = RewardTrack.MaxLevel;
            character.claimedTrackLevel = 0;

            RunManager.StartRun(4242UL);
            var run = RunManager.Run;
            run.currentHealth.Clear();
            run.currentHealth.Add(new RunHealthEntry { characterId = character.definitionId, hp = 50 });

            Assert.AreEqual(280, ContentDatabase.EffectiveStats(character).maxHealth,
                "the maximum this character carries BEFORE the claim");

            Assert.IsTrue(BotRunDriver.ClaimTrackForTest(character), "nothing was claimed at all");

            Assert.AreEqual(430, ContentDatabase.EffectiveStats(character).maxHealth,
                "the maximum the whole track adds up to: 280 base plus the track's 150");

            // 50 of 280 is the same fraction as 77 of 430:
            // (50 * 430 + 140) / 280 = 77 (CarriedHealth.Rescaled, rounded).
            // Left at 50 the character would walk into the next room on 12% of
            // a bar they had half of.
            Assert.AreEqual(77, run.currentHealth[0].hp,
                "the run kept the old absolute against a maximum that moved under it");
        }
    }
}
