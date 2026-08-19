using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // The thin half of item description: pulling numbers off a ScriptableObject
    // and simulating a swap. Every rule about how those numbers READ lives in
    // Domain.ItemStatLines, where a test can reach it.
    public static class ItemDescription
    {
        public static string CompactSummary(ItemDefinition item, int plus = 0)
        {
            if (item == null) return "";
            return ItemStatLines.Compact(item.StatBonusAt(plus), item.abilityScoreBonus, ScalingDescription(item));
        }

        public static string CardSummary(ItemDefinition item, int plus = 0)
        {
            if (item == null) return "";
            return ItemStatLines.Card(item.StatBonusAt(plus), item.abilityScoreBonus, ScalingDescription(item));
        }

        // Both scaling axes on one line: the plain Attack grade first, the
        // spell grade after a bullet. A staff with `scaling None` and
        // `spellScaling INT A` would otherwise print no scaling line at all,
        // despite that grade being the entire reason to pick it up -- the same
        // silent omission ItemStatLines.BonusParts exists to prevent for flat
        // stats.
        private static string ScalingDescription(ItemDefinition item)
        {
            string attack = item.scaling.Describe();
            string spell = item.spellScaling.Describe();

            if (attack.Length == 0) return spell;
            return spell.Length == 0 ? attack : $"{attack} - SPELL {spell}";
        }

        // What equipping `candidate` would actually change, computed by
        // SIMULATING the swap rather than subtracting two stat blocks.
        //
        // Clone the character, place the candidate through the same
        // ResolveTargetSlot every real equip uses, and re-read
        // EffectiveStats/EffectiveAbilityScores/ActiveLoadout. THE CASCADE
        // FALLS OUT FOR FREE: requirements are met from everything ELSE worn,
        // so a swap that displaces a piece something else was leaning on --
        // or that wakes a piece already worn but dormant -- comes back through
        // the same resolver every other read goes through, with no hand-written
        // "what depends on what" anywhere.
        public static ItemComparison Compare(Character character, ItemDefinition candidate, int candidatePlus = 0)
        {
            if (character?.equipment == null || candidate == null)
            {
                return ItemComparison.None;
            }

            var beforeStats = ContentDatabase.EffectiveStats(character);
            var beforeScores = ContentDatabase.EffectiveAbilityScores(character);
            var beforeLive = LiveSlots(character);

            var clone = CloneForSimulation(character);
            var targetSlot = clone.equipment.ResolveTargetSlot(candidate.equipSlot);
            clone.equipment.Set(targetSlot, candidate.id, candidatePlus);

            var afterStats = ContentDatabase.EffectiveStats(clone);
            var afterScores = ContentDatabase.EffectiveAbilityScores(clone);
            var afterLive = LiveSlots(clone);

            // The candidate's own slot is reported separately, never folded
            // into the cascade lists: "cascade" means something ELSE moved, and
            // folding it in would make every unwearable item claim to have
            // broken something.
            var newlyInert = beforeLive.Where(s => s != targetSlot && !afterLive.Contains(s)).ToList();
            var newlyLive = afterLive.Where(s => s != targetSlot && !beforeLive.Contains(s)).ToList();

            return new ItemComparison(
                afterStats - beforeStats,
                afterScores - beforeScores,
                newlyInert,
                newlyLive,
                afterLive.Contains(targetSlot));
        }

        // A COMPLETE copy, by round-trip rather than by hand.
        //
        // v1 listed the four fields it thought mattered. That is a standing
        // invitation for the simulation to drift from reality: add a field that
        // feeds EffectiveStats -- v2 has already added investedAbilityScores
        // and unspentStatPoints since -- and the comparison silently starts
        // answering for a character that does not exist. A serialised
        // round-trip copies whatever the type has, so it cannot fall behind.
        //
        // The loadout is then replaced with its own Clone() regardless.
        // JsonUtility does copy it, but the equipment is the one field this
        // whole method exists to mutate, and having it deep-copied by something
        // that states it deep-copies beats relying on a serializer's treatment
        // of a nested type.
        private static Character CloneForSimulation(Character character)
        {
            var clone = JsonUtility.FromJson<Character>(JsonUtility.ToJson(character));
            clone.equipment = character.equipment.Clone();
            return clone;
        }

        private static HashSet<EquipmentSlot> LiveSlots(Character character)
        {
            return new HashSet<EquipmentSlot>(
                ContentDatabase.ActiveLoadout(character).LiveEntries.Select(e => e.Entry.slot));
        }

        // The hover panel, ready to display.
        //
        // Safe to call with the candidate ALREADY worn: Compare re-places it
        // where it already is, netting a zero delta but still running the real
        // resolver -- so an equipped-but-inert item's hover explains why it is
        // inert through the exact same path a bag item's hover uses to explain
        // what wearing it would do.
        // The same comparison, for EVERY member of a squad at once.
        //
        // The Reckoning's prize goes to the stockpile rather than to a
        // character, so "what would this replace" has as many answers as there
        // are members. Each one runs the real Compare -- the clone-and-resolve
        // path, not a shortcut -- so a swap that would knock another slot inert
        // is accounted for per character, which is exactly the case a
        // "best candidate only" summary would hide.
        public static string SquadComparisonBody(IReadOnlyList<Character> squad,
                                                 ItemDefinition candidate, int candidatePlus = 0)
        {
            if (squad == null || candidate == null) return "";

            var rows = new List<(string, ItemComparison)>();
            foreach (var member in squad)
            {
                if (member == null) continue;
                var definition = ContentDatabase.GetCharacter(member.definitionId);
                string name = definition == null || string.IsNullOrWhiteSpace(definition.displayName)
                    ? member.definitionId
                    : definition.displayName;
                rows.Add((name, Compare(member, candidate, candidatePlus)));
            }

            return ItemStatLines.SquadBody(rows);
        }

        public static string ComparisonBody(Character character, ItemDefinition candidate, int candidatePlus = 0)
        {
            if (character == null || candidate == null) return "";

            var comparison = Compare(character, candidate, candidatePlus);

            return ItemStatLines.Body(
                candidate.StatBonusAt(candidatePlus),
                candidate.abilityScoreBonus,
                ScalingDescription(candidate),
                // Through RequirementCurve, so the line names what the CURRENT
                // difficulty knob demands rather than the raw authored number
                // -- the same value the resolver gates on. Its own block
                // overload, not six calls to the int one: this had grown a
                // hand-written copy of a method that already existed.
                RequirementCurve.Apply(candidate.requirements),
                ContentDatabase.EffectiveAbilityScores(character),
                comparison);
        }
    }
}
