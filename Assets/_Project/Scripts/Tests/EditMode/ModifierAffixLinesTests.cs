using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // How a rolled modifier's effects read, and how a candidate's affixes
    // diff against whatever is currently equipped.
    //
    // NONE OF THIS WAS TESTABLE FROM EDITMODE before this moved out of
    // Core.ItemDescription -- it reached ContentDatabase.ModifierEffectsForItem
    // / GetModifier (ScriptableObject accessors), so only the PlayMode/
    // content-pipeline suite could exercise the gain/loss/neutral
    // classification. This suite feeds ModifierAffixLines plain, already-
    // resolved data -- exactly what Core.ItemDescription resolves off
    // ContentDatabase before calling in -- and pins the exact formatted
    // output (CLAUDE.md gotcha 5: literal expected strings, never the
    // production formula recomputed).
    public class ModifierAffixLinesTests
    {
        private const string GainHex = ItemStatLines.GainHex;
        private const string LossHex = ItemStatLines.LossHex;
        private const string KeywordHex = ModifierEffectText.KeywordHex;

        private static ModifierAffixLines.Effect Fiery(int magnitude = 20)
        {
            return new ModifierAffixLines.Effect("fiery", "Fiery",
                new ModifierEffect(ModifierEffectType.ElementalDamageOnHitPercent, magnitude, against: DamageType.Fire));
        }

        private static ModifierAffixLines.Effect Swift(int magnitude = 12)
        {
            return new ModifierAffixLines.Effect("swift", "Swift",
                new ModifierEffect(ModifierEffectType.DodgeRating, magnitude));
        }

        // ---- LinePairs -------------------------------------------------

        [Test]
        public void NullOrEmptyInputReturnsNull()
        {
            Assert.IsNull(ModifierAffixLines.LinePairs(null));
            Assert.IsNull(ModifierAffixLines.LinePairs(new List<ModifierAffixLines.Effect>()));
        }

        [Test]
        public void OneModifierFormatsAsNameThenItsFragment()
        {
            var pairs = ModifierAffixLines.LinePairs(new[] { Fiery() });

            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual("fiery", pairs[0].Id);
            Assert.AreEqual($"Fiery -- <color={KeywordHex}>+20%</color> Fire dmg on hit", pairs[0].Line);
        }

        [Test]
        public void SeveralEffectsOnOneModifierIdJoinOntoOneLine()
        {
            // Two effects sharing the same id -- a modifier authoring more
            // than one rule -- merge onto a single "Name -- frag, frag" line
            // rather than printing the display name twice.
            var effects = new[]
            {
                new ModifierAffixLines.Effect("runic", "Runic",
                    new ModifierEffect(ModifierEffectType.FlatMaxManaBonus, 10)),
                new ModifierAffixLines.Effect("runic", "Runic",
                    new ModifierEffect(ModifierEffectType.FlatManaRegenBonus, 3)),
            };

            var pairs = ModifierAffixLines.LinePairs(effects);

            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual($"Runic -- <color={KeywordHex}>+10</color> Max Mana, " +
                $"<color={KeywordHex}>+3</color> MP/turn", pairs[0].Line);
        }

        [Test]
        public void MultipleModifiersPrintInFirstSeenOrder()
        {
            var pairs = ModifierAffixLines.LinePairs(new[] { Fiery(), Swift() });

            Assert.AreEqual(2, pairs.Count);
            Assert.AreEqual("fiery", pairs[0].Id);
            Assert.AreEqual("swift", pairs[1].Id);
        }

        // ---- ComparisonLines --------------------------------------------

        [Test]
        public void NoEquippedComparisonDegradesToThePlainListing()
        {
            var lines = ModifierAffixLines.ComparisonLines(new[] { Fiery() }, null);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual($"Fiery -- <color={KeywordHex}>+20%</color> Fire dmg on hit", lines[0]);
        }

        [Test]
        public void NoEquippedAndNoCandidateReturnsNull()
        {
            Assert.IsNull(ModifierAffixLines.ComparisonLines(null, null));
        }

        [Test]
        public void AnAffixNotOnTheEquippedItemIsAGain()
        {
            var equipped = new[] { new ModifierAffixLines.EquippedModifier("swift", "Swift") };
            var lines = ModifierAffixLines.ComparisonLines(new[] { Fiery() }, equipped);

            Assert.AreEqual(2, lines.Count, string.Join(" | ", lines));
            StringAssert.Contains($"<color={GainHex}>Fiery -- <color={KeywordHex}>+20%</color> Fire dmg on hit</color>", lines[0]);
        }

        [Test]
        public void AnAffixOnBothStaysUncoloured()
        {
            var equipped = new[] { new ModifierAffixLines.EquippedModifier("fiery", "Fiery") };
            var lines = ModifierAffixLines.ComparisonLines(new[] { Fiery() }, equipped);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual($"Fiery -- <color={KeywordHex}>+20%</color> Fire dmg on hit", lines[0]);
        }

        [Test]
        public void AnEquippedAffixNotOnTheCandidateIsALoss()
        {
            var equipped = new[] { new ModifierAffixLines.EquippedModifier("swift", "Swift") };
            var lines = ModifierAffixLines.ComparisonLines(new[] { Fiery() }, equipped);

            Assert.IsTrue(lines.Contains($"<color={LossHex}>Losing: Swift</color>"),
                string.Join(" | ", lines));
        }

        [Test]
        public void ACandidateWithNoAffixesStillReportsWhatWouldBeLost()
        {
            // A candidate that rolled nothing replacing something that had
            // rolled affixes is still a real loss -- must not bail out early
            // on a null candidate list.
            var equipped = new[] { new ModifierAffixLines.EquippedModifier("fiery", "Fiery") };
            var lines = ModifierAffixLines.ComparisonLines(null, equipped);

            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual($"<color={LossHex}>Losing: Fiery</color>", lines[0]);
        }

        [Test]
        public void GainAndLossCanAppearTogether()
        {
            var equipped = new[] { new ModifierAffixLines.EquippedModifier("swift", "Swift") };
            var lines = ModifierAffixLines.ComparisonLines(new[] { Fiery() }, equipped);

            Assert.AreEqual(2, lines.Count);
            StringAssert.Contains("Fiery", lines[0]);
            StringAssert.Contains(GainHex, lines[0]);
            Assert.AreEqual($"<color={LossHex}>Losing: Swift</color>", lines[1]);
        }

        [Test]
        public void ADuplicateEquippedIdReportsTheLossOnlyOnce()
        {
            var equipped = new[]
            {
                new ModifierAffixLines.EquippedModifier("swift", "Swift"),
                new ModifierAffixLines.EquippedModifier("swift", "Swift"),
            };
            var lines = ModifierAffixLines.ComparisonLines(null, equipped);

            Assert.AreEqual(1, lines.Count);
        }
    }
}
