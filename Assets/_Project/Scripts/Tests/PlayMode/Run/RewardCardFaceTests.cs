using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // WHAT A REWARD CARD SAYS (docs/PLAN_REWARD_SCREENS.md C5): the plinth
    // step, the slot band, the ghost, the delta and its sign, the lost affix,
    // the inert card, the relic mapping. Values only; ReckoningCardRowTests
    // covers where the screen puts them.
    public class RewardCardFaceTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-card-face-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static Character ArmedSheep(out SaveData save)
        {
            RunManager.StartRun(4242UL);
            save = SaveSlotManager.CurrentSave;
            var sheep = save.roster.First(c => c.definitionId == "sheep");
            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", tier: 0);
            return sheep;
        }

        private static PendingCard Card(ItemInstance copy, EquipmentSlot slot = EquipmentSlot.Weapon1,
            bool inert = false, CardOutcome outcome = CardOutcome.Open) =>
            new PendingCard { Copy = copy, RecipientId = "sheep", Slot = slot, Inert = inert, Outcome = outcome };

        // ---- the plinth ------------------------------------------------------------

        [Test]
        public void ThePlinthStepFollowsTheItemsQuality()
        {
            ArmedSheep(out var save);

            Assert.AreEqual(1, RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 2))).PlinthStep, "Common");
            Assert.AreEqual(2, RewardCardFace.OfGear(save,
                Card(ItemInstance.Of("sword", 2, new[] { "stalwart" }))).PlinthStep, "Uncommon");
            Assert.AreEqual(3, RewardCardFace.OfGear(save,
                Card(ItemInstance.Of("sword", 2, new[] { "stalwart", "bulwark" }))).PlinthStep, "Rare");
            Assert.AreEqual(4, RewardCardFace.OfGear(save,
                Card(ItemInstance.Of("sword", 2, new[] { "stalwart", "bulwark", "trait_tinfoil_pipe" }))).PlinthStep, "Legendary");
        }

        [Test]
        public void ARelicRaritiesMapToFourPlinths()
        {
            Assert.AreEqual(1, RewardCardFace.PlinthStepOf(RelicRarity.Common));
            Assert.AreEqual(2, RewardCardFace.PlinthStepOf(RelicRarity.Uncommon));
            Assert.AreEqual(3, RewardCardFace.PlinthStepOf(RelicRarity.Rare));
            Assert.AreEqual(3, RewardCardFace.PlinthStepOf(RelicRarity.UltraRare));
            Assert.AreEqual(4, RewardCardFace.PlinthStepOf(RelicRarity.Mythic));
            Assert.AreEqual(4, RewardCardFace.PlinthStepOf(RelicRarity.Godlike));
        }

        [Test]
        public void ARelicCardHasNoBandNoWearerAndNoDelta()
        {
            RunManager.StartRun(4242UL);

            var face = RewardCardFace.OfRelic(new PendingCard { RelicId = "kinship" });

            Assert.IsFalse(face.HasBand);
            Assert.IsFalse(face.HasDelta);
            Assert.AreEqual("", face.WearerId);
            Assert.IsNull(face.GhostKey);
            Assert.IsNotEmpty(face.Name);
        }

        // ---- the slot band ---------------------------------------------------------

        [Test]
        public void TheBandCarriesTheTierAndTheSlotsHoningAndTheNameCarriesNoPlus()
        {
            var sheep = ArmedSheep(out var save);
            var copy = ItemInstance.Of("sword", 4);

            using (HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 3))
            {
                var face = RewardCardFace.OfGear(save, Card(copy));

                Assert.IsTrue(face.HasBand);
                Assert.AreEqual(4, face.BandTier);
                Assert.AreEqual(3, face.BandPlus);
                Assert.AreEqual(ContentDatabase.NameOf(copy), face.Name, "honing is the slot's, not the item's");
                StringAssert.DoesNotContain("+3", face.Name);
            }
        }

        [Test]
        public void TheBandStaysLitWhenTheCardIsTaken()
        {
            var sheep = ArmedSheep(out var save);

            using (HoningPreview.Add("sheep", EquipmentSlot.Weapon1, 3))
            {
                var taken = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 4), outcome: CardOutcome.Taken));

                Assert.AreEqual(3, taken.BandPlus, "the honing came with the slot");
                Assert.IsFalse(taken.HasDelta, "a taken card is compared with nothing");
                Assert.IsNull(taken.GhostKey);
            }
        }

        // ---- the ghost -------------------------------------------------------------

        [Test]
        public void TheGhostIsWhatTheWearerHasNow_AndAnEmptySlotHasNone()
        {
            var sheep = ArmedSheep(out var save);

            var worn = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 4)));
            Assert.AreEqual(ItemIcons.KeyOf(sheep.equipment.GetInstance(EquipmentSlot.Weapon1)), worn.GhostKey);
            Assert.IsNotNull(worn.GhostKey);

            var helm = ContentDatabase.Offerable.First(i => i.equipSlot == EquipmentSlot.Head);
            var empty = RewardCardFace.OfGear(save, Card(ItemInstance.Of(helm.id, 2), EquipmentSlot.Head));
            Assert.IsNull(empty.GhostKey, "nothing worn on the head: no ghost");
            Assert.IsFalse(empty.HasLost);
        }

        [Test]
        public void ALostAffixIsNamedWhenTheWornCopyHasOneTheCardLacks()
        {
            var sheep = ArmedSheep(out var save);
            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", new List<string> { "stalwart" }, tier: 0);

            var lacking = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 4)));
            Assert.IsTrue(lacking.HasLost);
            Assert.IsNotEmpty(lacking.LostName);

            var keeping = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 4, new[] { "stalwart" })));
            Assert.IsFalse(keeping.HasLost);
        }

        // ---- the delta -------------------------------------------------------------

        [Test]
        public void TheChevronsSideFollowsTheHeadlineStatsSign()
        {
            var sheep = ArmedSheep(out var save);

            var up = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 5)));
            Assert.IsTrue(up.HasDelta);
            Assert.AreEqual(1, up.DeltaSign);
            StringAssert.Contains("+", up.DeltaText);

            sheep.equipment.Set(EquipmentSlot.Weapon1, "sword", tier: 5);
            var down = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 0)));
            Assert.AreEqual(-1, down.DeltaSign);
            StringAssert.Contains("-", down.DeltaText);
        }

        [Test]
        public void ANoStatChangeCardIsFlatWithNoNumber()
        {
            ArmedSheep(out var save);

            var flat = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 0, new[] { "trait_tinfoil_pipe" })));

            Assert.IsTrue(flat.HasDelta);
            Assert.AreEqual(0, flat.DeltaSign);
            Assert.AreEqual("", flat.DeltaText);
        }

        [Test]
        public void AnInertCardHasNoDeltaAndSaysWhy()
        {
            ArmedSheep(out var save);

            var face = RewardCardFace.OfGear(save, Card(ItemInstance.Of("sword", 5), inert: true));

            Assert.IsTrue(face.Inert);
            Assert.IsFalse(face.HasDelta);
            Assert.AreEqual(UiStrings.ReckoningInert.Format(), face.Feature);
        }

        // ---- the focus detail ------------------------------------------------------

        [Test]
        public void TheGearDetailListsTheStatsThatMoveAsNowToNext()
        {
            ArmedSheep(out var save);
            var helm = ContentDatabase.Offerable.First(i => i.equipSlot == EquipmentSlot.Head);

            var detail = RewardCardFace.DetailOfGear(save, Card(ItemInstance.Of(helm.id, 5), EquipmentSlot.Head));

            StringAssert.Contains("→", detail.Body, "now -> next");
            StringAssert.Contains("Head", detail.Body, "the slot is named");
            Assert.IsNotEmpty(detail.Title);
        }

        [Test]
        public void AWeaponSwapsDetailFallsBackToTheHeadlineNumber()
        {
            ArmedSheep(out var save);

            var detail = RewardCardFace.DetailOfGear(save, Card(ItemInstance.Of("sword", 5)));

            StringAssert.DoesNotContain(UiStrings.ReckoningNoChange.Format(), detail.Body);
            StringAssert.Contains("+", detail.Body);
        }

        [Test]
        public void TheRelicDetailSortsTheSquadIntoGainsAndUnreached()
        {
            RunManager.StartRun(4242UL);
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "owl", "bear" };

            // kinship is borne by the sheep alone.
            var detail = RewardCardFace.DetailOfRelic(save, new PendingCard { RelicId = "kinship" });

            StringAssert.Contains("gains:", detail.Body);
            StringAssert.Contains("not reached:", detail.Body);
            StringAssert.DoesNotContain("adds nothing", detail.Body);
        }
    }
}
