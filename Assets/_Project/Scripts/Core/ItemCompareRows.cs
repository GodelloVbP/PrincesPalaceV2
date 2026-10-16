using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE STAT ROWS OF "WHAT WOULD THIS ITEM CHANGE", shared by the dossier's
    // compare column and the reward card's focus detail (docs/PLAN_REWARD_SCREENS.md
    // C5). One pass over ItemDescription.Compare, so the two screens cannot
    // disagree about a number. Pure reads: nothing is equipped or saved.
    public static class ItemCompareRows
    {
        public readonly struct Row
        {
            public readonly SheetStat Stat;
            public readonly int Now;
            public readonly int Next;

            public Row(SheetStat stat, int now, int next)
            {
                Stat = stat;
                Now = now;
                Next = next;
            }

            public int Delta => Next - Now;
        }

        public readonly struct Result
        {
            public readonly ItemComparison Comparison;

            // Only the stats that move, in SheetStats.Derived order.
            public readonly IReadOnlyList<Row> Rows;

            public Result(ItemComparison comparison, IReadOnlyList<Row> rows)
            {
                Comparison = comparison;
                Rows = rows;
            }
        }

        // `scores` are the character's effective ability scores now; the pool
        // rule and signature flag say which of the two pool rows exist.
        public static Result Build(Character character, ItemInstance copy, AbilityScoreBlock scores,
            PoolCapacityRule poolRule, bool hasSignature)
        {
            var comparison = ItemDescription.Compare(character, copy);
            var previewScores = scores + comparison.ScoreDelta;
            var stats = ContentDatabase.EffectiveStats(character);

            var rows = new List<Row>();
            foreach (var stat in SheetStats.Derived)
            {
                if (!StatApplies(stat, poolRule, hasSignature)) continue;

                int delta = DeltaFor(stat, comparison, scores, previewScores, poolRule);
                if (delta == 0 || !TryCurrentValue(character, stat, stats, scores, out int now)) continue;

                rows.Add(new Row(stat, now, now + delta));
            }

            return new Result(comparison, rows);
        }

        // Whether this character has the row at all. A signature row for
        // someone with no signature would read "-" beside a name that is not
        // theirs, and regen for a Fixed pool would name an income that pool
        // never takes from any source (its gain is authored, pools.json), the
        // same rule SheetStats.FedBy states for Wisdom.
        public static bool StatApplies(SheetStat stat, PoolCapacityRule poolRule, bool hasSignature)
        {
            switch (stat)
            {
                case SheetStat.SignatureGain: return hasSignature;
                case SheetStat.ManaRegen: return poolRule != PoolCapacityRule.Fixed;
                default: return true;
            }
        }

        // What a stat reads as RIGHT NOW, from the model. False means there
        // is no number to show, which is not the same thing as zero.
        public static bool TryCurrentValue(Character character, SheetStat stat,
            StatBlock stats, AbilityScoreBlock scores, out int value)
        {
            if (stat == SheetStat.MaxMana)
            {
                value = ContentDatabase.EffectiveMaxMana(character);
                return true;
            }

            if (stat == SheetStat.SignatureGain)
            {
                var signature = ContentDatabase.BuildSignatureResource(character);
                value = signature?.GainPerTurn ?? 0;
                return signature != null;
            }

            value = SheetStats.ValueOf(stat, stats, scores);
            return true;
        }

        // How far an equip would move a stat. The two pool rows are derived
        // from Wisdom and Charisma, so an item that shifts a score shifts them
        // too -- unless the pool is Fixed, when Wisdom moves nothing.
        private static int DeltaFor(SheetStat stat, ItemComparison comparison, AbilityScoreBlock scores,
            AbilityScoreBlock previewScores, PoolCapacityRule poolRule)
        {
            switch (stat)
            {
                case SheetStat.MaxHealth: return comparison.StatDelta.maxHealth;
                case SheetStat.Attack: return comparison.StatDelta.attack;
                case SheetStat.Speed: return comparison.StatDelta.speed;
                case SheetStat.ManaRegen: return comparison.StatDelta.manaRegen;
                case SheetStat.PhysicalDefense: return comparison.StatDelta.physicalDefense;
                case SheetStat.MagicalDefense: return comparison.StatDelta.magicalDefense;
                case SheetStat.MaxMana:
                case SheetStat.SignatureGain:
                    return SheetStats.ScoreDelta(stat, scores, previewScores, poolRule);
                default: return 0;
            }
        }
    }
}
