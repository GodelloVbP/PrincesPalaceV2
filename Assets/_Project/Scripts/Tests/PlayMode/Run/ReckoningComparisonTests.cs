using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's hover comparison, at the model layer.
    //
    // The screen half is a screenshot question. This is the half that can go
    // quietly wrong: ItemDescription.Compare had NO production caller at all
    // before this feature -- it was written, correct, and unreachable, exactly
    // the shape AUDIT #42 records for relics. A test that only asserted "a box
    // appears" would have been satisfied by a box full of nothing.
    public class ReckoningComparisonTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-cmp-" + System.Guid.NewGuid().ToString("N"));
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

        // The lowest-requirement equippable there is, which a level 1 character
        // should be able to wear.
        private static ItemDefinition EasiestItem() =>
            ContentDatabase.Equippables
                .Where(i => i != null)
                .OrderBy(i => i.sortOrder)
                .FirstOrDefault();

        [Test]
        public void AFreshCharacterCanActuallyEquipSomething()
        {
            // The guard on the whole feature. If NOTHING is live for a starting
            // character, every hover reads "cannot equip yet" and the box looks
            // broken while being technically truthful.
            var hero = SaveSlotManager.CurrentSave.ActiveSquad().First();
            var item = EasiestItem();
            Assert.IsNotNull(item, "content has no equippable items at all");

            var comparison = ItemDescription.Compare(hero, ItemInstance.Of(item.id, 0));

            Assert.IsTrue(comparison.CandidateIsLive,
                $"a level {hero.level} character cannot equip '{item.displayName}' (tier 0), the " +
                "lowest-requirement equippable in the game - so every offer on the Reckoning would read " +
                "'cannot equip yet' and the comparison would look broken");
        }
    }
}
