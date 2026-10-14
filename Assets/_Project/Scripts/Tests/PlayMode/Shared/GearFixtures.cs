using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Real gear for tests that wear it. Requirements climb with the tier and an
    // inert piece contributes nothing (no stats, no modifier effects), so a
    // test that needs a LIVE copy at a given tier asks for one the first
    // character can actually wear there.
    public static class GearFixtures
    {
        public static ItemDefinition WearableAt(int tier, EquipmentSlot? slot = null)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            var scores = ContentDatabase.EffectiveAbilityScores(new Character(definition.id));

            return ContentDatabase.Equippables.FirstOrDefault(i => (!slot.HasValue || i.equipSlot == slot.Value) && scores.Meets(
                RequirementCurve.ApplyGear(ContentDatabase.Effective(ItemInstance.Of(i.id, tier)).Value.Requirements)));
        }
    }
}
