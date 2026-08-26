using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // Fortunate: a fight WIN writes RunSnapshot.runFavor, gated on whether
    // the winning, FIELDED squad actually has it equipped. RewardApplier.Apply
    // is only ever reached on the won path (FightBootstrap returns via
    // RunManager.EndRun on a loss, before a payout exists) -- see
    // RewardApplier.Apply's own comment -- so exercising Apply directly IS
    // exercising the win path, the same posture RewardApplierTests already
    // takes for the experience half of a payout.
    public class FortunateFavorTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-fortunate-tests-" + System.Guid.NewGuid().ToString("N"));
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

        private static List<string> FieldedIds() =>
            SaveSlotManager.CurrentSave.ActiveSquad().Select(c => c.definitionId).ToList();

        private static Character First() => SaveSlotManager.CurrentSave.ActiveSquad()[0];

        [Test]
        public void AWin_WithNoFortunateItemEquipped_GrantsNoRunFavor()
        {
            var save = SaveSlotManager.CurrentSave;
            Assert.AreEqual(0, save.activeRun.runFavor, "fixture check: a fresh save starts at zero run favour");

            RewardApplier.Apply(new VictoryRewards.Payout(50, 10), FieldedIds());

            Assert.AreEqual(0, save.activeRun.runFavor,
                "no Fortunate item equipped anywhere in the fielded squad -- runFavor must stay exactly 0");
        }

        [Test]
        public void AWin_WithAFortunateItemEquipped_GrantsItsScaledFavor()
        {
            var item = ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.tier == 0);
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = First();
            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.RiftForged);

            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            RewardApplier.Apply(new VictoryRewards.Payout(50, 10), FieldedIds());

            // fortunate's authored base is 5; TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            // 5 x 1.6 = 8, away-from-zero rounded.
            Assert.AreEqual(8, SaveSlotManager.CurrentSave.activeRun.runFavor,
                "a won fight with Fortunate equipped must write its scaled favour to the run");
        }

        [Test]
        public void OnlyTheFieldedSquadIsConsidered_NotTheWholeRoster()
        {
            var item = ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.tier == 0);
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var squad = SaveSlotManager.CurrentSave.ActiveSquad();
            Assert.Greater(squad.Count, 0, "fixture: the default save must field at least one character");

            var fielded = First();
            var wearer = squad.Count > 1 ? squad[1] : null;

            if (wearer == null)
            {
                Assert.Inconclusive("fixture: this save's squad has only one member -- cannot test squad-vs-roster exclusion");
                return;
            }

            wearer.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "fortunate" }, riftTier: (int)RiftTier.Ordinary);

            // Field ONLY the first character -- the wearer is a roster member
            // but is NOT in this fight.
            RewardApplier.Apply(new VictoryRewards.Payout(50, 10), new List<string> { fielded.definitionId });

            Assert.AreEqual(0, SaveSlotManager.CurrentSave.activeRun.runFavor,
                "a Fortunate item on a squad member who was not FIELDED for this fight must grant nothing");
        }
    }
}
