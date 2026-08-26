using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
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

        // `viewer`, when given, is who this card is being shown TO -- not
        // necessarily who has the item equipped. For a weapon it is what
        // turns WeaponPower (a property of the item alone) into DMG (what it
        // would actually deal in this character's hands), per D7.2. Optional
        // and defaulted to null so every existing caller with no viewer in
        // scope keeps compiling and simply loses the DMG line, same as
        // before this phase.
        // ITEM-MODIFIER PLAN PHASE E: `riftTier`/`modifierIds` name WHICH
        // ROLL this particular copy carries -- optional and defaulted to
        // Ordinary/null so every existing caller with no roll in scope keeps
        // compiling and simply shows no AFFIXES section, same as before this
        // phase (the overwhelming majority of items).
        public static string CardSummary(ItemDefinition item, int plus = 0, Character viewer = null,
            RiftTier riftTier = RiftTier.Ordinary, IReadOnlyList<string> modifierIds = null)
        {
            if (item == null) return "";
            return ItemStatLines.Card(item.StatBonusAt(plus), item.abilityScoreBonus, ScalingDescription(item),
                WeaponDamageLine(item, plus, viewer), ModifierLines(item, riftTier, modifierIds));
        }

        // "Fiery -- deals 24% bonus Fire damage on hit", one line per rolled
        // modifier -- the adapter half of ModifierEffectText.Describe: pulls
        // this ONE item copy's scaled effects off ContentDatabase (the exact
        // seam ContentDatabase.ModifierEffects itself reads, never a second
        // formula), groups them by the ModifierDefinition they came from
        // (preserving each modifier's own authored effect order), and joins
        // each modifier's fragments onto one line behind its display name.
        //
        // GroupBy over the already-ordered ModifierEffectsForItem list
        // preserves first-seen order (LINQ-to-objects' own documented
        // behaviour), so modifiers print in the same order the item's
        // modifierIds were rolled/authored in, not resorted.
        private static IReadOnlyList<string> ModifierLines(ItemDefinition item, RiftTier riftTier,
            IReadOnlyList<string> modifierIds)
        {
            if (item == null || modifierIds == null || modifierIds.Count == 0)
            {
                return null;
            }

            var scaled = ContentDatabase.ModifierEffectsForItem(item.tier, riftTier, modifierIds);
            if (scaled.Count == 0)
            {
                return null;
            }

            var lines = new List<string>();
            foreach (var group in scaled.GroupBy(pair => pair.Modifier))
            {
                string fragments = string.Join(", ", group
                    .Select(pair => ModifierEffectText.Describe(pair.Effect))
                    .Where(fragment => fragment.Length > 0));

                if (fragments.Length == 0) continue;
                lines.Add($"{group.Key.displayName} -- {fragments}");
            }

            return lines;
        }

        // "DMG 87", or "DMG 87 -> 104" against whatever `viewer` currently
        // has equipped in the main hand -- null for anything that is not a
        // weapon, or when there is no viewer to score it against (WeaponPower
        // means nothing without a wielder's ability scores).
        //
        // Reads the SAME EquippedWeapon/EquippedWeaponPower every other
        // Effective* consumer does, rather than re-deriving the equipped
        // weapon's plus by hand -- see ContentDatabase.EquippedWeaponPower's
        // own header for why null there means "empty hand", not "hits for
        // nothing".
        private static string WeaponDamageLine(ItemDefinition item, int plus, Character viewer)
        {
            if (item == null || item.kind != ItemKind.Weapon || viewer == null)
            {
                return null;
            }

            var viewerScores = ContentDatabase.EffectiveAbilityScores(viewer);
            int damage = WeaponPower.DisplayDamage(item.WeaponPowerAt(plus), item.scaling, viewerScores);

            int? equippedDamage = null;
            var equipped = ContentDatabase.EquippedWeapon(viewer);
            if (equipped != null && equipped.id != item.id)
            {
                int? equippedPower = ContentDatabase.EquippedWeaponPower(viewer);
                if (equippedPower.HasValue)
                {
                    equippedDamage = WeaponPower.DisplayDamage(equippedPower.Value, equipped.scaling, viewerScores);
                }
            }

            return ItemStatLines.WeaponDamageText(damage, equippedDamage);
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
                                                 ItemDefinition candidate, int candidatePlus = 0,
                                                 RiftTier riftTier = RiftTier.Ordinary,
                                                 IReadOnlyList<string> modifierIds = null)
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

            return ItemStatLines.SquadBody(rows, ModifierLines(candidate, riftTier, modifierIds));
        }

        public static string ComparisonBody(Character character, ItemDefinition candidate, int candidatePlus = 0,
                                            RiftTier riftTier = RiftTier.Ordinary,
                                            IReadOnlyList<string> modifierIds = null)
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
                RequirementCurve.ApplyGear(candidate.requirements),
                ContentDatabase.EffectiveAbilityScores(character),
                comparison,
                WeaponDamageLine(candidate, candidatePlus, character),
                ModifierLines(candidate, riftTier, modifierIds));
        }
    }
}
