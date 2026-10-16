using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The thin half of item description: pulling numbers off a ScriptableObject
    // and simulating a swap. Every rule about how those numbers READ lives in
    // Domain.ItemStatLines (flat stats) and Domain.ModifierAffixLines
    // (rolled affixes), where a test can reach it.
    public static class ItemDescription
    {
        public static string CompactSummary(ItemInstance copy, int honing = 0)
        {
            var gear = ContentDatabase.Effective(copy, honing);
            if (!gear.HasValue) return "";
            return ItemStatLines.Compact(gear.Value.StatBonus, gear.Value.AbilityScoreBonus, ScalingDescription(gear.Value));
        }

        // `viewer`, when given, is who this card is being shown TO -- not
        // necessarily who has the item equipped. For a weapon it is what
        // turns WeaponPower (a property of the item alone) into DMG (what it
        // would actually deal in this character's hands), per D7.2. Optional
        // and defaulted to null so every existing caller with no viewer in
        // scope keeps compiling and simply loses the DMG line, same as
        // before this phase.
        // `honing` is the honing the numbers are shown at: the slot's own for
        // a worn copy, and for one that is not worn the honing of the slot it
        // would take (ContentDatabase.HoningFor). The rolled affixes are the
        // copy's own.
        // `wornIn` names the slot the copy is worn in by `viewer`, which lets
        // the card mark its trait "inactive" when the wearer already gets that
        // mechanic from a relic or an earlier slot (TraitStacking).
        public static string CardSummary(ItemInstance copy, int honing = 0, Character viewer = null,
            EquipmentSlot? wornIn = null)
        {
            var gear = ContentDatabase.Effective(copy, honing);
            if (!gear.HasValue) return "";
            bool inactive = wornIn.HasValue && viewer != null &&
                ContentDatabase.InactiveTraitSlots(viewer, SaveSlotManager.CurrentSave?.activeRun?.relicIds)
                    .Contains(wornIn.Value);
            return ItemStatLines.Card(gear.Value.StatBonus, gear.Value.AbilityScoreBonus, ScalingDescription(gear.Value),
                WeaponDamageLine(copy, honing, viewer), ModifierLines(gear.Value, copy.ModifierIds, viewer, inactive));
        }

        // "Fiery -- <Keyword>+20%</Keyword> Fire dmg on hit", one line per
        // rolled modifier. Thin wrapper over Domain.ModifierAffixLines.LinePairs
        // -- the plain, no-comparison listing CardSummary still wants, and
        // the case ModifierComparisonLines itself degrades to when there is
        // nothing currently equipped to diff against.
        private static IReadOnlyList<string> ModifierLines(GearStats gear,
            IReadOnlyList<string> modifierIds, Character viewer, bool traitInactive = false)
        {
            return ModifierAffixLines.LinePairs(ResolvedModifierEffects(gear, modifierIds),
                    PrimaryPoolOf(viewer))
                ?.Select(pair => traitInactive && ContentDatabase.GetModifier(pair.Id)?.Data.Trait == true
                    ? pair.Line + UiStrings.TraitInactive.Format()
                    : pair.Line)
                .ToList();
        }

        // THE POOL THE VIEWER ACTUALLY HOLDS, for the one affix family whose
        // answer depends on it: every Max Mana and mana-regen source applies
        // to a WisdomDerived pool and to no other (PoolPrecedence), so a card
        // shown to a Fixed pool's owner promises nothing it cannot deliver.
        // Null viewer -- a listing with nobody chosen -- reads as mana, which
        // is what every character shipped today carries.
        private static ResolvedPool PrimaryPoolOf(Character viewer) =>
            viewer == null ? null : ContentDatabase.PrimaryPoolOf(viewer.definitionId);

        // ONE item copy's rolled modifiers, scaled -- pulled off
        // ContentDatabase.ModifierEffectsForItem (the ScriptableObject seam)
        // and flattened into the plain (id, displayName, effect) shape
        // Domain.ModifierAffixLines takes. This is the ONLY place in this
        // file that reaches ContentDatabase for modifier data; everything
        // downstream (grouping by id, formatting fragments, gain/loss/
        // unchanged classification) is Domain.ModifierAffixLines' job, so a
        // Domain-only EditMode suite can exercise it directly.
        private static IReadOnlyList<ModifierAffixLines.AffixEffect> ResolvedModifierEffects(GearStats? gear,
            IReadOnlyList<string> modifierIds)
        {
            if (!gear.HasValue || modifierIds == null || modifierIds.Count == 0)
            {
                return null;
            }

            var scaled = ContentDatabase.ModifierEffectsForItem(gear.Value.Tier, modifierIds);
            if (scaled.Count == 0)
            {
                return null;
            }

            return scaled
                .Select(pair => new ModifierAffixLines.AffixEffect(pair.Modifier.id, pair.Modifier.Data.DisplayName, pair.Effect))
                .ToList();
        }

        // The display name for every id currently worn in the slot a
        // candidate would occupy -- resolved off ContentDatabase.GetModifier
        // (the ScriptableObject seam), same graceful-degradation posture the
        // pre-move code took: an id with no matching ModifierDefinition (or
        // no displayName) prints the raw id rather than dropping the line.
        private static IReadOnlyList<ModifierAffixLines.EquippedModifier> ResolvedEquippedModifiers(
            IReadOnlyList<string> equippedModifierIds)
        {
            if (equippedModifierIds == null || equippedModifierIds.Count == 0)
            {
                return null;
            }

            return equippedModifierIds.Select(id =>
            {
                var modifier = ContentDatabase.GetModifier(id);
                string name = modifier == null || string.IsNullOrEmpty(modifier.Data.DisplayName) ? id : modifier.Data.DisplayName;
                return new ModifierAffixLines.EquippedModifier(id, name);
            }).ToList();
        }

        // The AFFIXES section, compared against whatever is CURRENTLY
        // EQUIPPED in the slot the candidate would occupy -- the same
        // "VS. EQUIPPED" posture the numeric stat delta already takes
        // (ItemStatLines.DeltaLines), extended to affixes rather than left as
        // a plain listing. Thin wrapper: resolves both sides off
        // ContentDatabase and hands the plain result to
        // Domain.ModifierAffixLines.ComparisonLines, which owns the actual
        // gain/loss/unchanged classification -- see that method's own header
        // for the full rule.
        private static IReadOnlyList<string> ModifierComparisonLines(ItemInstance copy,
            IReadOnlyList<string> equippedModifierIds, Character viewer)
        {
            return ModifierAffixLines.ComparisonLines(
                ResolvedModifierEffects(ContentDatabase.Effective(copy), copy.ModifierIds),
                ResolvedEquippedModifiers(equippedModifierIds),
                PrimaryPoolOf(viewer));
        }

        // The rolled modifier ids on whatever is CURRENTLY worn in the slot
        // `candidate` would occupy, or null when that slot is empty -- the
        // affix half of "VS. EQUIPPED", reading the SAME
        // EquipmentLoadout.ResolveTargetSlot(candidate.equipSlot) Compare's
        // own clone-and-resolve runs, rather than a second rule for "which
        // slot". Called against the character's REAL equipment, which
        // Compare never mutates (only its clone is), so this can run before
        // or after Compare with an identical answer either way.
        private static IReadOnlyList<string> EquippedModifierIds(Character character, ItemInstance candidate)
        {
            if (character?.equipment == null) return null;

            var targetSlot = character.equipment.ResolveTargetSlot(ContentDatabase.DefinitionOf(candidate).equipSlot);
            return character.equipment.IsEmpty(targetSlot) ? null : character.equipment.GetModifierIds(targetSlot);
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
        private static string WeaponDamageLine(ItemInstance copy, int honing, Character viewer)
        {
            var item = ContentDatabase.DefinitionOf(copy);
            if (item == null || item.kind != ItemKind.Weapon || viewer == null)
            {
                return null;
            }

            var gear = ContentDatabase.Effective(copy, honing).Value;

            var viewerScores = ContentDatabase.EffectiveAbilityScores(viewer);
            int damage = WeaponPower.DisplayDamage(gear.WeaponPower, gear.Scaling, viewerScores);

            int? equippedDamage = null;
            var equipped = ContentDatabase.EquippedWeapon(viewer);
            var equippedCopy = viewer.equipment.GetInstance(EquipmentSlot.Weapon1);
            if (equipped != null && (equippedCopy.ItemId != copy.ItemId || equippedCopy.Tier != copy.Tier))
            {
                int? equippedPower = ContentDatabase.EquippedWeaponPower(viewer);
                if (equippedPower.HasValue)
                {
                    equippedDamage = WeaponPower.DisplayDamage(equippedPower.Value,
                        ContentDatabase.Effective(equippedCopy).Value.Scaling, viewerScores);
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
        private static string ScalingDescription(GearStats gear)
        {
            string attack = gear.Scaling.Describe();
            string spell = gear.SpellScaling.Describe();

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
        //
        // preferredSlot names the hand or slot the swap targets when the caller
        // already knows it (a reward card names its slot); null is the pack
        // click's ResolveTargetSlot.
        public static ItemComparison Compare(Character character, ItemInstance candidate,
            EquipmentSlot? preferredSlot = null)
        {
            if (character?.equipment == null || candidate == null)
            {
                return ItemComparison.None;
            }

            var beforeStats = ContentDatabase.EffectiveStats(character);
            var beforeScores = ContentDatabase.EffectiveAbilityScores(character);
            var beforeLive = LiveSlots(character);

            var targetSlot = preferredSlot ?? character.equipment.ResolveTargetSlot(ContentDatabase.DefinitionOf(candidate).equipSlot);
            var clone = SimulateEquip(character, candidate, preferredSlot);
            if (clone == null) return ItemComparison.None;

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

        // THE SWAP ITSELF, as a character you can ask questions of.
        //
        // Lifted out of Compare unchanged so a SECOND caller can read numbers
        // Compare does not return. ItemComparison carries a StatBlock delta,
        // and a StatBlock has no field for the one number a weapon actually
        // changes: gear no longer grants flat Attack (ContentDatabase.
        // EffectiveStats zeroes `bonus.attack`), so a sword's whole
        // contribution arrives as WeaponPower at the FightEncounterAdapter
        // seam and reads as +0 attack in the delta. The bot's GearEvaluator
        // has to ask ContentDatabase.EquippedWeaponPower of the simulated
        // character to see it -- which needs the character, not the delta.
        //
        // Returns a THROWAWAY. The real character is never mutated, which is
        // what lets this be called in a loop over a whole bag.
        // `preferredSlot` names WHICH hand, for the one item class that has two
        // -- the same parameter EquipMove.TryEquip takes and for the same
        // reason. Null keeps Compare's original behaviour exactly:
        // ResolveTargetSlot, which is what a player clicking a pack cell with
        // no slot in mind gets. A preferred slot the item does not fit is
        // refused here the way EquipMove refuses it, rather than silently
        // simulating a helm on somebody's feet.
        public static Character SimulateEquip(Character character, ItemInstance candidate,
            EquipmentSlot? preferredSlot = null)
        {
            if (character?.equipment == null || candidate == null) return character;
            var definition = ContentDatabase.DefinitionOf(candidate);
            if (preferredSlot.HasValue && !EquipmentSlots.Accepts(preferredSlot.Value, definition.equipSlot))
            {
                return null;
            }

            var clone = CloneForSimulation(character);
            var targetSlot = preferredSlot ?? clone.equipment.ResolveTargetSlot(definition.equipSlot);
            clone.equipment.Put(targetSlot, candidate);
            return clone;
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
        public static string ComparisonBody(Character character, ItemInstance candidate)
        {
            if (character == null || candidate == null) return "";

            var comparison = Compare(character, candidate);
            var equippedModifierIds = EquippedModifierIds(character, candidate);

            int honing = ContentDatabase.HoningFor(character, candidate);
            var gear = ContentDatabase.Effective(candidate, honing).Value;
            return ItemStatLines.Body(
                gear.StatBonus,
                gear.AbilityScoreBonus,
                ScalingDescription(gear),
                // Through RequirementCurve, so the line names what the CURRENT
                // difficulty knob demands rather than the raw authored number
                // -- the same value the resolver gates on. Its own block
                // overload, not six calls to the int one: this had grown a
                // hand-written copy of a method that already existed.
                RequirementCurve.ApplyGear(gear.Requirements),
                ContentDatabase.EffectiveAbilityScores(character),
                comparison,
                WeaponDamageLine(candidate, honing, character),
                ModifierComparisonLines(candidate, equippedModifierIds, character));
        }
    }
}
