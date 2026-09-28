using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
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
        //
        // BALANCE REDESIGN PHASE 6 (D7.1): each ability-score line now names
        // what it actually derives -- "+2 WIS (+4 Mana, +4 Mag Def)" -- rather
        // than leaving "what does this even do" for the player to remember
        // from the glossary. See AppendAbilityGrant for which scores get an
        // exact figure and which get a named destination instead.
        public static List<string> BonusParts(StatBlock stats, AbilityScoreBlock scores)
        {
            var parts = new List<string>();

            Append(parts, "HP", stats.maxHealth);
            Append(parts, "SPD", stats.speed);
            Append(parts, "ATK", stats.attack);
            Append(parts, "MP/turn", stats.manaRegen);
            Append(parts, "phys def", stats.physicalDefense);
            Append(parts, "magic def", stats.magicalDefense);

            foreach (AbilityScore score in AbilityScores.All)
            {
                AppendAbilityGrant(parts, score, scores[score]);
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

        // DMG first (if this is a weapon a viewer is looking at), bonuses on
        // their own line, SCALES on its own beneath -- printed even when
        // there are no bonuses, because for a weapon the grades ARE the item.
        // For a card with room for a few lines.
        //
        // BALANCE REDESIGN PHASE 6 (D7.2): `weaponDamage` is optional and
        // null for anything that is not a weapon (or has no viewer to score
        // it against) -- see ItemDescription.CardSummary/ComparisonBody in
        // Core, which are the only callers with a WeaponPower and a viewer's
        // ability scores in hand to compute it from. The SCALES line stays
        // exactly as it was: this ADDS the number a weapon's grade only
        // implies, it does not replace it.
        //
        // ITEM-MODIFIER PLAN PHASE E: `modifierLines`, LAST, after a blank
        // separator -- rolled affixes are a distinct fact from the item's own
        // flat stats/scaling, the same reason VS. EQUIPPED gets its own
        // blank-then-heading break in Body below rather than running straight
        // into the deltas. Optional and null for the vast majority of items,
        // which have rolled none -- see ModifierSection for why an empty/null
        // list prints nothing at all rather than an empty heading.
        public static string Card(StatBlock stats, AbilityScoreBlock scores, string scaling,
            string weaponDamage = null, IReadOnlyList<string> modifierLines = null)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(weaponDamage)) lines.Add(weaponDamage);

            string bonuses = string.Join("  ", BonusParts(stats, scores));
            if (bonuses.Length > 0) lines.Add(bonuses);

            if (!string.IsNullOrEmpty(scaling)) lines.Add($"SCALES  {scaling}");

            string modifiers = ModifierSection(modifierLines);
            if (modifiers.Length > 0)
            {
                lines.Add("");
                lines.Add(modifiers);
            }

            return string.Join("\n", lines);
        }

        // "AFFIXES" heading over one line per rolled modifier -- shared by
        // Card (a single character's hover) and SquadBody (called once per
        // member there, PHASE F), so no screen can describe the same roll a
        // different way. Each entry in `modifierLines` is already a COMPLETE
        // line -- either "DisplayName -- fragment[, fragment]" from
        // ItemDescription.ModifierLines, or that same text coloured
        // GainHex/left plain, plus trailing "Losing: DisplayName" lines in
        // LossHex, from ItemDescription.ModifierComparisonLines once there is
        // something currently equipped to diff against.
        //
        // Empty or null prints NOTHING, not an empty heading -- the vast
        // majority of items roll no modifiers at all, and "AFFIXES" over a
        // blank line would read as a content gap rather than as "this item
        // has none".
        public static string ModifierSection(IReadOnlyList<string> modifierLines)
        {
            if (modifierLines == null || modifierLines.Count == 0)
            {
                return "";
            }

            var lines = new List<string> { Coloured(HeadingHex, "AFFIXES") };
            lines.AddRange(modifierLines);
            return string.Join("\n", lines);
        }

        // One coloured "+/-N Label" per non-zero field of the delta.
        public static List<string> DeltaLines(in ItemComparison comparison)
        {
            var lines = new List<string>();

            AppendDelta(lines, "Max HP", comparison.StatDelta.maxHealth);
            AppendDelta(lines, "Speed", comparison.StatDelta.speed);
            AppendDelta(lines, "Attack", comparison.StatDelta.attack);
            AppendDelta(lines, "Mana/turn", comparison.StatDelta.manaRegen);
            AppendDelta(lines, "Phys def", comparison.StatDelta.physicalDefense);
            AppendDelta(lines, "Magic def", comparison.StatDelta.magicalDefense);

            foreach (AbilityScore score in AbilityScores.All)
            {
                AppendDelta(lines, AbilityScores.ShortName(score), comparison.ScoreDelta[score]);
            }

            return lines;
        }

        // ONE BLOCK PER SQUAD MEMBER, for a reward the whole squad shares.
        //
        // The Reckoning hands its prize to the STOCKPILE, not to a character,
        // so there is no single "currently equipped" to diff against -- which
        // is why the comparison the character sheet has could not simply be
        // pointed at this screen. Every member gets their own line, because
        // "+4 Attack for Shawn, nothing for Wool" is the decision the player is
        // actually being asked to make.
        //
        // A member with no movement still gets a line saying so. Dropping them
        // would make the box change height per hover and, worse, read as though
        // that character had not been considered.
        // `ModifierLines` IS PER-MEMBER, not one list shared by the whole
        // squad: each line is coloured against what THAT member currently
        // has equipped in the slot (a
        // gain, a loss, or unchanged -- see Core.ItemDescription's
        // ModifierComparisonLines), so two members can legitimately see
        // different colours, or a different set of lines outright, for the
        // exact same candidate item. Folding that back into one shared list
        // would silently pick one member's answer and print it for all of
        // them.
        public static string SquadBody(IReadOnlyList<(string Name, ItemComparison Comparison, IReadOnlyList<string> ModifierLines)> squad)
        {
            if (squad == null || squad.Count == 0) return "";

            var blocks = new List<string>();

            foreach (var member in squad)
            {
                string block = $"{member.Name}\n   {LineFor(member.Comparison)}";

                // Indented under the member's own block, same "   " prefix
                // LineFor's own lines get -- an AFFIXES heading at the
                // squad's own indentation would read as though it belonged
                // to nobody.
                string modifiers = ModifierSection(member.ModifierLines);
                if (modifiers.Length > 0)
                {
                    block += "\n   " + modifiers.Replace("\n", "\n   ");
                }

                blocks.Add(block);
            }

            return string.Join("\n", blocks);
        }

        // ONE line describing what the swap does to this member.
        //
        // "no change" and "cannot equip" are DIFFERENT ANSWERS and collapsing
        // them is the trap: an item whose requirements a character does not
        // meet is inert, so every delta is legitimately zero -- and reporting
        // that as "no change" tells the player the item is useless to them when
        // the truth is that it is useless to them YET. CandidateIsLive is the
        // only thing that can tell those two apart.
        private static string LineFor(in ItemComparison comparison)
        {
            if (!comparison.CandidateIsLive) return "cannot equip yet";

            var deltas = DeltaLines(comparison);
            string body = deltas.Count == 0 ? "no change" : string.Join("\n   ", deltas);

            // A swap that knocks another slot dormant is the one consequence a
            // list of deltas hides: the numbers above already include the loss,
            // so without this the player sees a drop with no cause.
            int inert = comparison.NewlyInertSlots?.Count ?? 0;
            if (inert > 0) body += $"\n   ({inert} other slot{(inert == 1 ? "" : "s")} goes inert)";

            return body;
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
        //
        // `weaponDamage` threads straight through to Card -- see its own
        // header for why this is optional and who computes it.
        public static string Body(StatBlock bonus, AbilityScoreBlock scoreBonus, string scaling,
                                  AbilityScoreBlock required, AbilityScoreBlock have,
                                  in ItemComparison comparison, string weaponDamage = null,
                                  IReadOnlyList<string> modifierLines = null)
        {
            var lines = new List<string>();

            string card = Card(bonus, scoreBonus, scaling, weaponDamage, modifierLines);
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

        // PUBLIC: Core.ItemDescription's affix comparison (ITEM-MODIFIER PLAN
        // PHASE F) wraps individual AFFIXES lines in this same markup rather
        // than hand-writing a second "<color=#HEX>text</color>" literal --
        // there is no second convention to invent, only this one to reuse.
        public static string Coloured(string hex, string text) => $"<color={hex}>{text}</color>";

        private static void Append(List<string> parts, string abbreviation, int value)
        {
            if (value != 0) parts.Add($"{Signed(value)} {abbreviation}");
        }

        // "+2 WIS (+4 Mana, +4 Mag Def)" -- what a flat ability-score grant
        // ACTUALLY DOES, not just its raw number. Omitted entirely at zero,
        // same as every other line here.
        private static void AppendAbilityGrant(List<string> parts, AbilityScore score, int value)
        {
            if (value == 0) return;
            parts.Add($"{Signed(value)} {AbilityScores.ShortName(score)} ({AbilityGrantAnnotation(score, value)})");
        }

        // CON and WIS are pure multiplication in AbilityDerivation (score
        // minus 10, times a flat rate) -- which means a flat grant's effect
        // is EXACT and ORDER-INDEPENDENT: N granted points are always worth
        // exactly N x rate, whatever the wearer's own score already is. Safe
        // to print a real number for those two, so this does.
        //
        // STR and INT derive no flat stat at all -- weapon/spell GRADES
        // answer "how hard do I hit" instead (see AbilityDerivation's own
        // header) -- so there is no number to print for either, only where
        // the point actually goes.
        //
        // DEX and CHA are NOT safe the same way CON/WIS are: both divide
        // ((score - 10) / 2 and / 4, see AbilityDerivation.SpeedBonus /
        // SignatureGainBonus), and integer division means a flat grant's
        // marginal effect depends on the wearer's own remainder -- a +2 DEX
        // item can be worth +1 Speed on one character and +0 on another,
        // and this function has no wearer to check. Naming what the point
        // feeds, without a number it cannot promise, is the honest version;
        // SheetStats.PerPointSummary is where the exact figure belongs,
        // because it DOES have a wearer to measure against.
        private static string AbilityGrantAnnotation(AbilityScore score, int delta)
        {
            switch (score)
            {
                case AbilityScore.Constitution:
                    return $"{Signed(delta * AbilityDerivation.HealthPerPoint)} HP, " +
                           $"{Signed(delta * AbilityDerivation.PhysicalDefensePerPoint)} Phys Def";
                case AbilityScore.Wisdom:
                    return $"{Signed(delta * AbilityDerivation.ManaPerPoint)} Mana, " +
                           $"{Signed(delta * AbilityDerivation.MagicalDefensePerPoint)} Mag Def";
                case AbilityScore.Strength:
                    return "weapon scaling";
                case AbilityScore.Intelligence:
                    return "spell scaling";
                case AbilityScore.Dexterity:
                    return "Speed, weapon scaling";
                default: // Charisma
                    return "Signature Gain";
            }
        }

        // THE mitigation curve, read as a percentage -- D/(D+Softener), the
        // exact fraction DamagePipeline.AfterDefences runs on every hit, via
        // CombatMath.AfterResistance. ONE function so the dossier's own
        // Defense rows (SheetStats.DisplayText) and the glossary's worked
        // examples (GlossaryEntries.Mechanics()) cannot round the same number
        // two different ways -- or drift apart outright if the curve is ever
        // retuned.
        //
        // TRUNCATING integer division, matching CombatMath.AfterResistance
        // exactly rather than rounding away-from-zero on a float -- this is a
        // DISPLAY of what that function actually does, not an independent
        // formula, so it has to share its convention rather than its own. It
        // still lands on the design doc's own pinned examples exactly: 25 ->
        // 20%, 50 -> 33%, 100 -> 50%, 300 -> 75% (all four happen to divide
        // evenly, so truncation does not move them).
        public static int DamageReductionPercent(int defense)
        {
            if (defense <= 0) return 0;
            return 100 * defense / (defense + CombatMath.ResistanceSoftener);
        }

        // "DMG 87", or "DMG 87 -> 104" against whatever is currently
        // equipped -- see WeaponPower.DisplayDamage for how each side is
        // computed. No arrow glyph (this file's own header explains why:
        // legacy uGUI Text drops a glyph the font does not carry), and no
        // delta printed at all when the two numbers already agree -- hovering
        // the weapon already in the slot should not claim it upgrades itself.
        public static string WeaponDamageText(int damage, int? equippedDamage = null)
        {
            if (equippedDamage.HasValue && equippedDamage.Value != damage)
            {
                return $"DMG {equippedDamage.Value} -> {damage}";
            }

            return $"DMG {damage}";
        }

        private static void AppendDelta(List<string> lines, string label, int value)
        {
            if (value == 0) return;
            lines.Add(Coloured(value > 0 ? GainHex : LossHex, $"{Signed(value)} {label}"));
        }

        private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
