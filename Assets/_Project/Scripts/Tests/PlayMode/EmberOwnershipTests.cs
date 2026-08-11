using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Economy;

namespace PrincesPalace.PlayModeTests
{
    // Embers belong to a CHARACTER, not to the profile.
    //
    // A shared pool meant the character you actually play funds the one you
    // never touch -- kindle Shawn's tree all season and the newest recruit is
    // one purchase away from the top of theirs. These pin the ownership rule
    // and the migration that moves an old save onto it.
    public class EmberOwnershipTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-embers-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private static RunSnapshot RunThatKilled(string boss)
        {
            var run = new RunSnapshot { hasRun = true };
            run.bossesKilled.Add(boss);
            return run;
        }

        [Test]
        public void ABossPaysTheSquadThatRanRatherThanAPool()
        {
            var squad = Save.ActiveSquad();
            Assert.IsNotEmpty(squad, "fixture: a new profile fields somebody");

            var before = squad.ToDictionary(c => c.definitionId, c => c.embers);

            var result = RunSettlement.Settle(Save, RunThatKilled("warden"));

            Assert.AreEqual(1, result.EmbersEarned);
            foreach (var character in Save.ActiveSquad())
            {
                Assert.AreEqual(before[character.definitionId] + 1, character.embers,
                    $"{character.definitionId} was not paid");
            }
        }

        [Test]
        public void EachSquadMemberGetsTheFullAmountRatherThanAShare()
        {
            // Splitting would make bringing a second character a tax on the
            // first, which is the opposite of what a squad is for.
            var run = new RunSnapshot { hasRun = true };
            run.bossesKilled.AddRange(new[] { "a", "b", "c" });

            var result = RunSettlement.Settle(Save, run);

            Assert.AreEqual(3, result.EmbersEarned);
            foreach (var character in Save.ActiveSquad())
            {
                Assert.AreEqual(3, character.embers);
            }
        }

        [Test]
        public void ACharacterLeftInTheHubEarnsNothing()
        {
            // The whole point. Progression should reflect who you played.
            var save = Save;
            var benched = new Character { definitionId = "never_fielded" };
            save.roster.Add(benched);

            // Not in selectedCharacterIds, so ActiveSquad excludes them --
            // unless the roster fallback kicks in, which is why the fixture
            // states a selection explicitly.
            save.selectedCharacterIds = new System.Collections.Generic.List<string>
            {
                save.roster[0].definitionId,
            };

            RunSettlement.Settle(save, RunThatKilled("warden"));

            Assert.AreEqual(0, benched.embers, "a character who never ran was paid anyway");
            Assert.AreEqual(1, save.roster[0].embers);
        }

        [Test]
        public void EmbersNeverLandOnTheSharedWalletAgain()
        {
            // The field survives only so a pre-v3 save can be migrated. If
            // anything starts writing to it, embers are silently shared again
            // and nothing else in the game would notice.
            int before = Save.wallet.Get(CurrencyType.Embers);

            RunSettlement.Settle(Save, RunThatKilled("warden"));

            Assert.AreEqual(before, Save.wallet.Get(CurrencyType.Embers));
        }

        [Test]
        public void EmbersSurviveBeingWrittenToDisk()
        {
            RunSettlement.Settle(Save, RunThatKilled("warden"));

            SaveSlotManager.Forget();

            Assert.AreEqual(1, Save.ActiveSquad().First().embers,
                "the payout never reached the file");
        }

        // ---- the migration ------------------------------------------------------

        [Test]
        public void AnOldSavesPooledEmbersMoveOntoTheRoster()
        {
            // Handed whole to the first roster member rather than split:
            // splitting silently reduces what any one character can afford,
            // and nothing recorded who earned them.
            var save = SaveData.CreateNew();
            save.version = 2;
            save.wallet.Set(CurrencyType.Embers, 17);
            foreach (var character in save.roster) character.embers = 0;

            Assert.IsTrue(save.Migrate());

            Assert.AreEqual(17, save.roster[0].embers);
            Assert.AreEqual(0, save.wallet.Get(CurrencyType.Embers), "the pool was not cleared");
            Assert.AreEqual(SaveData.CurrentVersion, save.version);
        }

        [Test]
        public void MigratingASaveWithNoPooledEmbersChangesNothing()
        {
            var save = SaveData.CreateNew();
            save.version = 2;
            save.wallet.Set(CurrencyType.Embers, 0);
            save.roster[0].embers = 4;

            save.Migrate();

            Assert.AreEqual(4, save.roster[0].embers, "an existing per-character balance was disturbed");
        }

        [Test]
        public void MigrationRunsOnceRatherThanOnEveryLoad()
        {
            // Gated on the version the save CAME FROM. A second pass would
            // hand out the pool again, which is the meta-progression version
            // of the money printer.
            var save = SaveData.CreateNew();
            save.version = 2;
            save.wallet.Set(CurrencyType.Embers, 9);
            foreach (var character in save.roster) character.embers = 0;

            save.Migrate();
            save.Migrate();

            Assert.AreEqual(9, save.roster[0].embers);
        }
    }
}
