using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // THE GENERATED ASSETS STORE THESE AS RAW INTS.
    //
    // SkillDefinition and everything under Resources/Content/ serialise
    // SkillEffect, SkillTargeting and StatusEffectType by ORDINAL, not by name
    // -- SkillEffect's and DamageType's own headers say so. So inserting a
    // member in the middle of one of these enums, or reordering two, silently
    // repoints every already-built asset: a Ward becomes a Shatter, a Poison
    // becomes a Regen, and nothing fails to compile. A content rebuild would
    // paper over it locally and a stale build would not.
    //
    // Thirteen new members are queued across the spell-expansion milestones
    // (plan D10), which is thirteen chances to insert rather than append. This
    // is the table that turns an insertion into a red test instead of a
    // mis-typed save.
    //
    // HOW TO EXTEND IT: add the new member at the END of its list, with the
    // next number. Never renumber, never reorder. If a member is retired, leave
    // its ordinal occupied rather than closing the gap.
    public class ContentEnumOrdinalTests
    {
        private static readonly Dictionary<string, int> SkillEffectOrdinals = new Dictionary<string, int>
        {
            { "DamageSingle", 0 },
            { "DamageAll", 1 },
            { "HealSelf", 2 },
            { "HealParty", 3 },
            { "RestorePartyMana", 4 },
            { "Provoke", 5 },
            { "Transform", 6 },
            { "Ward", 7 },
            { "Shatter", 8 },
            { "BuffParty", 9 },
            { "GiftMana", 10 },
            { "GiftFury", 11 },
            { "GiftHaste", 12 },
            { "Summon", 13 },
            { "HealSingle", 14 },
            { "Reclaim", 15 },
            // Milestone C, appended together after Reclaim.
            { "Hasten", 16 },
            { "SwapAllies", 17 },
            // Milestone D. Plan D10 listed Afflict FIRST of the five spell-
            // expansion members; the three above landed in milestones B and C
            // while it waited for D, so it appends behind them rather than
            // renumbering them. "Never insert" is the rule the generated
            // assets depend on; the plan's ordering was not.
            { "Afflict", 18 },
            { "Enthrall", 19 },
        };

        private static readonly Dictionary<string, int> SkillTargetingOrdinals = new Dictionary<string, int>
        {
            { "SingleEnemy", 0 },
            { "AllEnemies", 1 },
            { "Self", 2 },
            { "Party", 3 },
            { "SingleAlly", 4 },
        };

        private static readonly Dictionary<string, int> StatusEffectTypeOrdinals = new Dictionary<string, int>
        {
            { "Poison", 0 },
            { "Regen", 1 },
            { "Protect", 2 },
            { "Vulnerable", 3 },
            { "Stun", 4 },
            { "Shielded", 5 },
            { "Provoked", 6 },
            { "Empowered", 7 },
            { "Chilled", 8 },
            { "Rooted", 9 },
            { "Marked", 10 },
            { "Feared", 11 },
            // Milestone E.
            { "Burn", 12 },
            { "Thorned", 13 },
        };

        private static readonly Dictionary<string, int> DamageTypeOrdinals = new Dictionary<string, int>
        {
            { "Physical", 0 },
            { "Fire", 1 },
            { "Ice", 2 },
            { "Nature", 3 },
            { "Poison", 4 },
            { "Arcane", 5 },
            { "Earth", 6 },
            { "Water", 7 },
            { "Wind", 8 },
            { "Lightning", 9 },
            { "Void", 10 },
        };

        [Test]
        public void SkillEffectOrdinalsMatchTheirPinnedList() =>
            AssertOrdinals<SkillEffect>(SkillEffectOrdinals);

        [Test]
        public void SkillTargetingOrdinalsMatchTheirPinnedList() =>
            AssertOrdinals<SkillTargeting>(SkillTargetingOrdinals);

        [Test]
        public void StatusEffectTypeOrdinalsMatchTheirPinnedList() =>
            AssertOrdinals<StatusEffectType>(StatusEffectTypeOrdinals);

        [Test]
        public void DamageTypeOrdinalsMatchTheirPinnedList() =>
            AssertOrdinals<DamageType>(DamageTypeOrdinals);

        // Both directions. The name->ordinal half catches a reorder; the
        // count half catches an INSERTION, which would otherwise leave every
        // pinned name still correct up to the insertion point and only shift
        // the members nobody remembered to pin.
        private static void AssertOrdinals<T>(Dictionary<string, int> pinned) where T : struct, Enum
        {
            var live = Enum.GetValues(typeof(T)).Cast<T>().ToList();

            Assert.GreaterOrEqual(pinned.Count, 5,
                "vacuity guard: this table pins almost nothing");

            foreach (var pair in pinned)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(T), pair.Key),
                    $"{typeof(T).Name}.{pair.Key} was renamed or removed -- every generated asset still "
                    + "holds its ordinal, so this is a content migration, not a rename");

                var member = (T)Enum.Parse(typeof(T), pair.Key);
                Assert.AreEqual(pair.Value, Convert.ToInt32(member),
                    $"{typeof(T).Name}.{pair.Key} moved from {pair.Value} to {Convert.ToInt32(member)} -- "
                    + "every ScriptableObject under Resources/Content/ stores this enum as a raw int, so "
                    + "a member inserted or reordered repoints already-built assets silently");
            }

            Assert.AreEqual(pinned.Count, live.Count,
                $"{typeof(T).Name} has {live.Count} members and this table pins {pinned.Count}. A NEW member "
                + "is welcome -- append it here with the next number. If it was inserted rather than "
                + "appended, move it to the end of the enum instead of renumbering this table.");
        }
    }
}
