using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The numeric half of a relic.
    //
    // RelicEffect keeps the mechanics that are real special cases; this covers
    // the ones that are a number on a stat, which is most of them and none of
    // which should cost a line of C#. The rules worth pinning are the two that
    // decide whether a stack of individually reasonable relics stays playable:
    // percents SUM rather than compound, and flats land after them.
    public class RelicModifierTests
    {
        private static RelicModifier Mod(RelicModifierType type, int amount) =>
            new RelicModifier(type, amount);

        [Test]
        public void APercentAppliesToTheBase()
        {
            int result = RelicModifiers.Apply(100, RelicStat.Attack,
                new[] { Mod(RelicModifierType.AttackPercent, 20) });

            Assert.AreEqual(120, result);
        }

        [Test]
        public void PercentsSumRatherThanCompound()
        {
            // Two +50% relics give +100%, not +125%. Summing is arithmetic the
            // player can do; compounding is how a stack of reasonable relics
            // becomes an unplayable number nobody authored.
            int result = RelicModifiers.Apply(100, RelicStat.Attack, new[]
            {
                Mod(RelicModifierType.AttackPercent, 50),
                Mod(RelicModifierType.AttackPercent, 50),
            });

            Assert.AreEqual(200, result);
        }

        [Test]
        public void FlatsLandAfterPercentsSoTheyDoNotGetScaled()
        {
            // 100 base, +100%, +10 flat = 210. The other order would give 220
            // and make every flat bonus quietly scale with every percent relic
            // in the run -- which reads as a bug the first time anyone checks.
            int result = RelicModifiers.Apply(100, RelicStat.Attack, new[]
            {
                Mod(RelicModifierType.AttackPercent, 100),
                Mod(RelicModifierType.AttackFlat, 10),
            });

            Assert.AreEqual(210, result);
        }

        [Test]
        public void AModifierForAnotherStatIsIgnored()
        {
            int result = RelicModifiers.Apply(100, RelicStat.Attack,
                new[] { Mod(RelicModifierType.DefencePercent, 500) });

            Assert.AreEqual(100, result);
        }

        [Test]
        public void NegativeModifiersReduceButNeverBelowZero()
        {
            // A relic with a real downside is the interesting kind. A stack of
            // them producing a negative max health would take the fight down
            // rather than make the character weak.
            Assert.AreEqual(80, RelicModifiers.Apply(100, RelicStat.Attack,
                new[] { Mod(RelicModifierType.AttackPercent, -20) }));

            Assert.AreEqual(0, RelicModifiers.Apply(100, RelicStat.Attack, new[]
            {
                Mod(RelicModifierType.AttackPercent, -90),
                Mod(RelicModifierType.AttackPercent, -90),
            }));
        }

        [Test]
        public void NoModifiersLeavesTheValueAlone()
        {
            Assert.AreEqual(100, RelicModifiers.Apply(100, RelicStat.Attack, null));
            Assert.AreEqual(100, RelicModifiers.Apply(100, RelicStat.Attack, new RelicModifier[0]));
        }

        [Test]
        public void NoneIsNeverAStatSoItCanNeverChangeAnything()
        {
            Assert.AreEqual(RelicStat.None, Mod(RelicModifierType.None, 999).Stat);
            Assert.AreEqual(100, RelicModifiers.Apply(100, RelicStat.None,
                new[] { Mod(RelicModifierType.None, 999) }));
        }

        [Test]
        public void EveryModifierTypeExceptNoneMapsToARealStat()
        {
            // Walked from the enum: a type added without a case in Stat would
            // resolve to None and silently do nothing forever.
            foreach (RelicModifierType type in Enum.GetValues(typeof(RelicModifierType)))
            {
                if (type == RelicModifierType.None) continue;

                Assert.AreNotEqual(RelicStat.None, Mod(type, 1).Stat,
                    $"{type} has no case in RelicModifier.Stat, so it can never apply");
            }
        }

        [Test]
        public void EveryStatExceptNoneHasBothAPercentAndAFlatType()
        {
            // The pairing is the promise the enum makes. A stat with only one
            // half is a gap an author will hit and not understand.
            foreach (RelicStat stat in Enum.GetValues(typeof(RelicStat)))
            {
                if (stat == RelicStat.None) continue;

                var types = Enum.GetValues(typeof(RelicModifierType))
                    .Cast<RelicModifierType>()
                    .Where(t => t != RelicModifierType.None)
                    .Select(t => Mod(t, 1))
                    .Where(m => m.Stat == stat)
                    .ToList();

                Assert.IsTrue(types.Any(m => m.IsPercent), $"{stat} has no percent modifier");
                Assert.IsTrue(types.Any(m => !m.IsPercent), $"{stat} has no flat modifier");
            }
        }

        [Test]
        public void AHugeStackSaturatesRatherThanOverflowing()
        {
            var huge = Enumerable.Range(0, 50)
                .Select(_ => Mod(RelicModifierType.AttackFlat, int.MaxValue / 10))
                .ToList();

            int result = RelicModifiers.Apply(1000, RelicStat.Attack, huge);

            Assert.AreEqual(int.MaxValue, result, "an int wrap would turn a stack of buffs into a negative stat");
        }

        // ---- what the resolver refuses to ship ---------------------------------------

        private static readonly string[] Known = { "first_forest_boss" };

        private static RawRelicEntry Relic(RawRelicModifier[] modifiers) =>
            new RawRelicEntry { id = "x", displayName = "X", effect = "None", modifiers = modifiers };

        [Test]
        public void AnUnknownModifierTypeIsAContentBuildFailure()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(new[] { new RawRelicModifier { type = "Wingspan", amount = 5 } }) },
                Known, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a known RelicModifierType", errors[0]);
        }

        [Test]
        public void AZeroAmountIsAContentBuildFailure()
        {
            // A modifier of zero is a line of content that changes nothing,
            // which is indistinguishable from a mistyped amount.
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(new[] { new RawRelicModifier { type = "AttackPercent", amount = 0 } }) },
                Known, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("would do nothing", errors[0]);
        }

        [Test]
        public void ARelicMayCarryBothAnEffectAndModifiers()
        {
            // The whole point of the split: "+15% attack AND strike twice" is
            // one relic, one modifier, one effect.
            var raw = new RawRelicEntry
            {
                id = "x", displayName = "X", effect = "DualWield",
                modifiers = new[] { new RawRelicModifier { type = "AttackPercent", amount = 15 } },
            };

            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { raw }, Known, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(RelicEffect.DualWield, resolved[0].Effect);
            Assert.AreEqual(1, resolved[0].Modifiers.Count);
            Assert.IsTrue(resolved[0].HasBehaviour);
        }

        [Test]
        public void APlaceholderHasNoBehaviourAndSaysSo()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(new RawRelicModifier[0]) }, Known, out var resolved, out _);

            Assert.IsTrue(ok, "a mechanic-less relic is still valid content");
            Assert.IsFalse(resolved[0].HasBehaviour);
        }
    }
}
