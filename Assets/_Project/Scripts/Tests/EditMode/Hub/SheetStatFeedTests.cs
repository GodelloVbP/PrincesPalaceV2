using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // SheetStats.FedBy has to be TRUE, not plausible.
    //
    // It drives the character sheet's one real mechanic -- hover an attribute,
    // see what it is doing for you -- and a wrong entry is a lie the player
    // cannot check: the row lights up and the number never moves. So this does
    // not restate the table. It RAISES each score and watches which derived
    // figures actually move, then asserts the table says exactly that.
    public class SheetStatFeedTests
    {
        private static AbilityScoreBlock Neutral() => new AbilityScoreBlock(10, 10, 10, 10, 10, 10);

        private static AbilityScoreBlock Raise(AbilityScore score, int by)
        {
            var s = Neutral();
            switch (score)
            {
                case AbilityScore.Strength: return new AbilityScoreBlock(s.strength + by, s.dexterity, s.constitution, s.wisdom, s.intelligence, s.charisma);
                case AbilityScore.Dexterity: return new AbilityScoreBlock(s.strength, s.dexterity + by, s.constitution, s.wisdom, s.intelligence, s.charisma);
                case AbilityScore.Constitution: return new AbilityScoreBlock(s.strength, s.dexterity, s.constitution + by, s.wisdom, s.intelligence, s.charisma);
                case AbilityScore.Wisdom: return new AbilityScoreBlock(s.strength, s.dexterity, s.constitution, s.wisdom + by, s.intelligence, s.charisma);
                case AbilityScore.Intelligence: return new AbilityScoreBlock(s.strength, s.dexterity, s.constitution, s.wisdom, s.intelligence + by, s.charisma);
                default: return new AbilityScoreBlock(s.strength, s.dexterity, s.constitution, s.wisdom, s.intelligence, s.charisma + by);
            }
        }

        // Every derived figure a score could move, by the ONLY thing that
        // actually converts scores into stats. Attack is deliberately absent
        // -- AbilityDerivation.AttackBonus was deleted in Phase 2 (D2);
        // Strength derives no stat at all until Phase 3 wires weapon power
        // in, so there is nothing here for raising it to move.
        private static Dictionary<SheetStat, int> Derive(AbilityScoreBlock scores) =>
            new Dictionary<SheetStat, int>
            {
                { SheetStat.Speed, AbilityDerivation.SpeedBonus(scores) },
                { SheetStat.MaxHealth, AbilityDerivation.MaxHealthBonus(scores) },
                { SheetStat.PhysicalDefense, AbilityDerivation.PhysicalDefenseBonus(scores) },
                { SheetStat.MaxMana, AbilityDerivation.MaxManaBonus(scores) },
                { SheetStat.MagicalDefense, AbilityDerivation.MagicalDefenseBonus(scores) },
                { SheetStat.SignatureGain, AbilityDerivation.SignatureGainBonus(scores) },
            };

        [Test]
        public void TheTableMatchesWhatRaisingAScoreActuallyMoves()
        {
            var baseline = Derive(Neutral());

            foreach (AbilityScore score in AbilityScores.All)
            {
                var raised = Derive(Raise(score, 8));

                var actuallyMoved = baseline.Keys
                    .Where(stat => raised[stat] != baseline[stat])
                    .OrderBy(s => s.ToString())
                    .ToList();

                var tableClaims = SheetStats.Feeds(score)
                    .Where(baseline.ContainsKey)
                    .OrderBy(s => s.ToString())
                    .ToList();

                CollectionAssert.AreEqual(actuallyMoved, tableClaims,
                    $"SheetStats says {score} feeds [{string.Join(", ", tableClaims)}], but raising it " +
                    $"actually moves [{string.Join(", ", actuallyMoved)}]. The sheet would highlight a row " +
                    "whose number never changes.");
            }
        }

        // The one truly gear-only row left. Physical/Magical Defense moved
        // OFF this list in Phase 2 -- Constitution and Wisdom derive them now
        // (AbilityDerivation.PhysicalDefenseBonus/MagicalDefenseBonus) -- so
        // only ManaRegen remains fed by nothing but gear and talents.
        [Test]
        public void ManaRegenIsFedByNoAttribute()
        {
            CollectionAssert.IsEmpty(SheetStats.FedBy(SheetStat.ManaRegen),
                "ManaRegen comes from gear and talents only; no attribute derives it.");
        }

        // Recorded rather than asserted away: Intelligence and (as of Phase 2)
        // Strength are the two scores with no row right now, because both
        // ride weapon/skill scaling instead of deriving a stat directly, and
        // that wiring is Phase 3 (D3), not yet landed. If either gains a row
        // this test is what should change.
        [Test]
        public void IntelligenceFeedsNoRowYet()
        {
            CollectionAssert.IsEmpty(SheetStats.Feeds(AbilityScore.Intelligence),
                "Intelligence has gained a derived row - update the sheet's hover copy with it.");
        }

        [Test]
        public void StrengthFeedsNoRowYet()
        {
            CollectionAssert.IsEmpty(SheetStats.Feeds(AbilityScore.Strength),
                "Strength has gained a derived row - update the sheet's hover copy with it " +
                "(expected once Phase 3 wires weapon power in).");
        }

        [Test]
        public void EveryAttributeButIntelligenceAndStrengthLightsSomething()
        {
            foreach (AbilityScore score in AbilityScores.All)
            {
                if (score == AbilityScore.Intelligence || score == AbilityScore.Strength) continue;

                CollectionAssert.IsNotEmpty(SheetStats.Feeds(score),
                    $"{score} lights no row, so hovering it on the sheet does nothing");
            }
        }

        // THE SAME TRUTHFULNESS RULE, FOR A CHARACTER WHOSE POOL IS Fixed.
        //
        // A Fixed pool is exactly its authored capacity from every source
        // (PoolCapacityRule.Fixed), so Wisdom moves that number by nothing.
        // Lighting the row anyway would be the precise lie this whole file
        // exists to prevent: the row highlights and the figure never changes.
        // Nothing authors a Fixed pool yet, which is why this cannot be a
        // content-driven check and is pinned against the rule instead.
        [Test]
        public void WisdomStopsFeedingThePoolRowForAFixedPool()
        {
            CollectionAssert.Contains(SheetStats.FedBy(SheetStat.MaxMana, PoolCapacityRule.WisdomDerived),
                AbilityScore.Wisdom,
                "mana is WisdomDerived and every character shipped today carries it");

            CollectionAssert.IsEmpty(SheetStats.FedBy(SheetStat.MaxMana, PoolCapacityRule.Fixed));

            CollectionAssert.AreEqual(
                new[] { SheetStat.MagicalDefense },
                SheetStats.Feeds(AbilityScore.Wisdom, PoolCapacityRule.Fixed),
                "Wisdom still mitigates magic for such a character - it just buys them no capacity");

            // And the hover copy agrees with the highlight rather than
            // promising a pool that will not move.
            StringAssert.DoesNotContain("Max Mana",
                SheetStats.PerPointSummary(Neutral(), AbilityScore.Wisdom, PoolCapacityRule.Fixed));
            StringAssert.Contains("Max Mana",
                SheetStats.PerPointSummary(Neutral(), AbilityScore.Wisdom, PoolCapacityRule.WisdomDerived));
        }
    }
}
