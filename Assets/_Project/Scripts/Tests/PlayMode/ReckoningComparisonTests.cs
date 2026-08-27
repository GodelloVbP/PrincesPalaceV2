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
            ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable)
                .OrderBy(i => i.tier)
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

            var comparison = ItemDescription.Compare(hero, item);

            Assert.IsTrue(comparison.CandidateIsLive,
                $"a level {hero.level} character cannot equip '{item.displayName}' (tier {item.tier}), the " +
                "lowest-requirement equippable in the game - so every offer on the Reckoning would read " +
                "'cannot equip yet' and the comparison would look broken");
        }

        [Test]
        public void AnEquippableOfferReportsRealMovement()
        {
            var hero = SaveSlotManager.CurrentSave.ActiveSquad().First();
            var item = EasiestItem();

            string body = ItemDescription.SquadComparisonBody(
                SaveSlotManager.CurrentSave.ActiveSquad(), item, 0);

            Assert.IsNotEmpty(body, "the squad comparison produced no text at all");
            StringAssert.DoesNotContain("cannot equip yet", body,
                "the easiest item in the game reports as unequippable");
        }

        // ITEM-MODIFIER PLAN PHASE E: an offer's rolled modifiers must show
        // on the hover BEFORE the player takes it -- ItemOffer.Modifiers/
        // RiftTier are set the moment ItemOfferRoll rolls the offer (see
        // ItemOffer's own header), well before Take() ever writes an
        // InventoryEntry, and ReckoningController.OnOfferHover reads
        // straight off the offer rather than off anything claimed. This
        // pins that at the model layer, the same "screen half is a
        // screenshot question" posture this whole file takes.
        [Test]
        public void AnOfferWithRolledModifiers_ShowsThemBeforeItIsEverTaken()
        {
            var item = EasiestItem();
            Assert.IsNotNull(item, "content has no equippable items at all");

            string body = ItemDescription.SquadComparisonBody(
                SaveSlotManager.CurrentSave.ActiveSquad(), item, candidatePlus: 0,
                riftTier: RiftTier.RiftForged,
                modifierIds: new System.Collections.Generic.List<string> { "fiery" });

            StringAssert.Contains("AFFIXES", body, "an unclaimed offer's rolled modifiers must already show on the hover");
            StringAssert.Contains("Fiery", body);
        }

        [Test]
        public void AnOfferWithNoRolledModifiers_ShowsNoAffixesSection()
        {
            var item = EasiestItem();
            Assert.IsNotNull(item, "content has no equippable items at all");

            string body = ItemDescription.SquadComparisonBody(SaveSlotManager.CurrentSave.ActiveSquad(), item);

            StringAssert.DoesNotContain("AFFIXES", body);
        }

        // ITEM-MODIFIER PLAN PHASE F: SquadComparisonBody's own version of
        // the AFFIXES comparison -- PER MEMBER, since two squad members can
        // have different rolls (or nothing at all) currently worn in the
        // same slot.
        //
        // Built as two standalone Character instances rather than
        // ActiveSquad() -- per this project's own convention, only Shawn is
        // a real character and a fresh profile's active squad is not
        // guaranteed to hold two, so a fixture asserting "at least two
        // members" would be pinning something this codebase does not
        // promise. SquadComparisonBody takes any IReadOnlyList<Character>;
        // it does not care whether they came from a save.
        //
        // Uses the same item id for "currently worn" and "candidate" that
        // ItemModifierTooltipTests' ComparisonBody tests do, for the same
        // reason: Compare's own header notes hovering a candidate already
        // worn is safe, and it means this test needs only one
        // ItemDefinition rather than hunting for two that share a slot.
        [Test]
        public void SquadComparisonBody_DiffersPerMember_WhenMembersHaveDifferentAffixesEquipped()
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var item = EasiestItem();
            Assert.IsNotNull(item, "content has no equippable items at all");

            var wearsFrosty = new Character(definition.id);
            wearsFrosty.equipment.Set(item.equipSlot, item.id,
                modifierIds: new System.Collections.Generic.List<string> { "frosty" });

            var wearsNothing = new Character(definition.id);

            var squad = new System.Collections.Generic.List<Character> { wearsFrosty, wearsNothing };

            string body = ItemDescription.SquadComparisonBody(squad, item, candidatePlus: 0,
                riftTier: RiftTier.Ordinary,
                modifierIds: new System.Collections.Generic.List<string> { "fiery" });

            StringAssert.Contains($"<color={ItemStatLines.GainHex}>Fiery", body,
                "the candidate's own affix is a gain for the member with nothing worn there yet, and for the " +
                "member losing a different affix, so it must show as a gain somewhere in the body");
            StringAssert.Contains($"<color={ItemStatLines.LossHex}>Losing: Frosty</color>", body,
                "the member who currently wears Frosty must see it named as a loss");
        }

        // The distinction the box exists to make. Collapsing these two was the
        // first cut of this feature: an inert item's deltas are all legitimately
        // zero, so it reported "no change" and told the player the item was
        // worthless to them rather than not-yet-usable.
        [Test]
        public void InertAndUnchangedAreDifferentSentences()
        {
            var none = ItemComparison.None;
            Assert.IsFalse(none.CandidateIsLive);

            var lines = ItemStatLines.SquadBody(
                new (string, ItemComparison, System.Collections.Generic.IReadOnlyList<string>)[]
                    { ("Shawn", none, null) });

            StringAssert.Contains("cannot equip", lines);
            StringAssert.DoesNotContain("no change", lines);
        }
    }
}
