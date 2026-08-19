using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
        // actually converts scores into stats.
        private static Dictionary<SheetStat, int> Derive(AbilityScoreBlock scores) =>
            new Dictionary<SheetStat, int>
            {
                { SheetStat.Attack, AbilityDerivation.AttackBonus(scores) },
                { SheetStat.Speed, AbilityDerivation.SpeedBonus(scores) },
                { SheetStat.MaxHealth, AbilityDerivation.MaxHealthBonus(scores) },
                { SheetStat.MaxMana, AbilityDerivation.MaxManaBonus(scores) },
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

        // The gear-only rows. Claiming a score feeds armour would light a row
        // that no attribute can ever move -- the handover for this screen did
        // exactly that, giving Physical DEF to Constitution.
        [Test]
        public void ArmourAndRegenAreFedByNoAttribute()
        {
            foreach (var stat in new[] { SheetStat.Defence, SheetStat.PhysicalResistance,
                                         SheetStat.MagicalResistance, SheetStat.ManaRegen })
            {
                CollectionAssert.IsEmpty(SheetStats.FedBy(stat),
                    $"{stat} comes from gear and talents only; no attribute derives it.");
            }
        }

        // Recorded rather than asserted away: Intelligence is the one score with
        // no row, because it rides weapon/skill scaling instead of deriving a
        // stat. If a scaling row is ever added this test is what should change.
        [Test]
        public void IntelligenceFeedsNoRowYet()
        {
            CollectionAssert.IsEmpty(SheetStats.Feeds(AbilityScore.Intelligence),
                "Intelligence has gained a derived row - update the sheet's hover copy with it.");
        }

        [Test]
        public void EveryAttributeButIntelligenceLightsSomething()
        {
            foreach (AbilityScore score in AbilityScores.All)
            {
                if (score == AbilityScore.Intelligence) continue;

                CollectionAssert.IsNotEmpty(SheetStats.Feeds(score),
                    $"{score} lights no row, so hovering it on the sheet does nothing");
            }
        }
    }
}
