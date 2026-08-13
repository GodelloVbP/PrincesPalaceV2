using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Equipment
{
    // What an item's numbers say, as text, in ONE place.
    //
    // Ported from v1, where the same job was done by two screens separately --
    // EquipmentController.BonusSummary and ItemChoiceController.Summarise --
    // which had quietly drifted: one of them never grew the manaRegen and
    // resistance lines the other already had, so a leather torso's largest
    // contribution simply never appeared on the paperdoll. One field walk,
    // shared, is what makes that class of drift impossible rather than fixed
    // once.
    //
    // IN DOMAIN, where v1 had it in Core. v1's version reached into
    // ItemDefinition -- a ScriptableObject -- so none of it could be tested by
    // a suite that is Domain-only, and the formatting is the half most worth
    // pinning. Everything here takes plain blocks; Core.ItemDescription is the
    // thin adapter that pulls those blocks off an asset.
    public static class ItemStatLines
    {
        // Signed COLOUR rather than an arrow glyph. Legacy uGUI Text silently
        // drops a glyph the font does not carry, and these panels are read
        // rather than glanced at, so a colour that always renders beats a
        // symbol that might not.
        public const string GainHex = "#8FE07F";
        public const string LossHex = "#E05A5A";
        public const string HeadingHex = "#A695BC";

        // Every non-zero "+N ABBR" this grants, in a fixed order: flat combat
        // stats first, then ability scores. Both joiners below use this rather
        // than walking the fields again, so they cannot list different things.
        public static List<string> BonusParts(StatBlock stats, AbilityScoreBlock scores)
        {
            var parts = new List<string>();

            Append(parts, "HP", stats.maxHealth);
            Append(parts, "SPD", stats.speed);
            Append(parts, "ATK", stats.attack);
            Append(parts, "DEF", stats.defense);
            Append(parts, "MP/turn", stats.manaRegen);
            Append(parts, "phys res", stats.physicalResistance);
            Append(parts, "magic res", stats.magicalResistance);

            foreach (AbilityScore score in AbilityScores.All)
            {
                Append(parts, AbilityScores.ShortName(score), scores[score]);
            }

            return parts;
        }

        // "+1 STR, +3 DEF - scales STR B". One line, for a slot label with room
        // for exactly one. Scaling trails after a bullet rather than a comma so
        // it does not read as another flat bonus.
        public static string Compact(StatBlock stats, AbilityScoreBlock scores, string scaling)
        {
            string bonuses = string.Join(", ", BonusParts(stats, scores));
            if (string.IsNullOrEmpty(scaling)) return bonuses;
            return bonuses.Length == 0 ? $"- scales {scaling}" : $"{bonuses} - scales {scaling}";
        }

        // Bonuses on one line, SCALES on its own beneath -- printed even when
        // there are no bonuses, because for a weapon the grades ARE the item.
        // For a card with room for two lines.
        public static string Card(StatBlock stats, AbilityScoreBlock scores, string scaling)
        {
            string bonuses = string.Join("  ", BonusParts(stats, scores));
            if (string.IsNullOrEmpty(scaling)) return bonuses;
            return bonuses.Length == 0 ? $"SCALES  {scaling}" : $"{bonuses}\nSCALES  {scaling}";
        }

        // One coloured "+/-N Label" per non-zero field of the delta.
        public static List<string> DeltaLines(in ItemComparison comparison)
        {
            var lines = new List<string>();

            AppendDelta(lines, "Max HP", comparison.StatDelta.maxHealth);
            AppendDelta(lines, "Speed", comparison.StatDelta.speed);
            AppendDelta(lines, "Attack", comparison.StatDelta.attack);
            AppendDelta(lines, "Defense", comparison.StatDelta.defense);
            AppendDelta(lines, "Mana/turn", comparison.StatDelta.manaRegen);
            AppendDelta(lines, "Phys res", comparison.StatDelta.physicalResistance);
            AppendDelta(lines, "Magic res", comparison.StatDelta.magicalResistance);

            foreach (AbilityScore score in AbilityScores.All)
            {
                AppendDelta(lines, AbilityScores.ShortName(score), comparison.ScoreDelta[score]);
            }

            return lines;
        }

        // "Requires STR 15", green once met and red with the shortfall named
        // once not.
        //
        // `required` must already have been through RequirementCurve, so the
        // line names what the CURRENT difficulty knob demands rather than the
        // raw authored number. `isLive` comes from the resolver rather than
        // being re-derived from these two blocks, so the colour can never
        // disagree with whether the item is actually contributing -- the
        // resolver settles a whole loadout at once and a per-item comparison
        // here would get cyclic cases wrong.
        public static string RequirementLine(AbilityScoreBlock required, AbilityScoreBlock have, bool isLive)
        {
            var need = new List<string>();
            var has = new List<string>();

            foreach (AbilityScore score in AbilityScores.All)
            {
                int amount = required[score];
                if (amount <= 0) continue;

                need.Add($"{AbilityScores.ShortName(score)} {amount}");
                has.Add($"{AbilityScores.ShortName(score)} {have[score]}");
            }

            if (need.Count == 0) return "";

            string text = $"Requires {string.Join(", ", need)}";
            if (!isLive) text += $" - have {string.Join(", ", has)}";

            return Coloured(isLive ? GainHex : LossHex, text);
        }

        // The whole hover panel: what it grants, what it demands, what changes,
        // and what else moves as a side effect.
        public static string Body(StatBlock bonus, AbilityScoreBlock scoreBonus, string scaling,
                                  AbilityScoreBlock required, AbilityScoreBlock have,
                                  in ItemComparison comparison)
        {
            var lines = new List<string>();

            string card = Card(bonus, scoreBonus, scaling);
            if (card.Length > 0) lines.Add(card);

            string requirement = RequirementLine(required, have, comparison.CandidateIsLive);
            if (requirement.Length > 0) lines.Add(requirement);

            var deltas = DeltaLines(comparison);
            if (deltas.Count > 0)
            {
                lines.Add("");
                lines.Add(Coloured(HeadingHex, "VS. EQUIPPED"));
                lines.AddRange(deltas);
            }

            // AFTER the deltas, because these are consequences of the swap
            // rather than part of its arithmetic -- and they are the lines a
            // player most needs to see before confirming.
            if (comparison.NewlyLiveSlots.Count > 0)
            {
                lines.Add(Coloured(GainHex, "Also unlocks: " + SlotNames(comparison.NewlyLiveSlots)));
            }

            if (comparison.NewlyInertSlots.Count > 0)
            {
                lines.Add(Coloured(LossHex, "Also breaks: " + SlotNames(comparison.NewlyInertSlots)));
            }

            return string.Join("\n", lines);
        }

        private static string SlotNames(IReadOnlyList<EquipmentSlot> slots)
        {
            return string.Join(", ", slots.Select(EquipmentSlots.DisplayName));
        }

        private static string Coloured(string hex, string text) => $"<color={hex}>{text}</color>";

        private static void Append(List<string> parts, string abbreviation, int value)
        {
            if (value != 0) parts.Add($"{Signed(value)} {abbreviation}");
        }

        private static void AppendDelta(List<string> lines, string label, int value)
        {
            if (value == 0) return;
            lines.Add(Coloured(value > 0 ? GainHex : LossHex, $"{Signed(value)} {label}"));
        }

        private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
