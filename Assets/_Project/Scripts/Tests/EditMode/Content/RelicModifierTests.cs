using PrincesPalace.Domain.Stats;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The numeric half of a relic: the stat kinds EffectFold applies, which
    // are most relics and none of which should cost a line of C#. The rules worth pinning are the two that
    // decide whether a stack of individually reasonable relics stays playable:
    // percents SUM rather than compound, and flats land after them.
    public class RelicModifierTests
    {
        private static Effect Mod(EffectKind type, int amount) =>
            new Effect(type, amount);

        // Held as relics, so every copy sums (EffectStacking).
        private static EffectSet Held(params Effect[] effects) => EffectSet.Of(EffectHolder.Relic, effects);

        [Test]
        public void APercentAppliesToTheBase()
        {
            int result = EffectFold.Apply(100, EffectStat.Attack,
                Held(Mod(EffectKind.AttackPercent, 20)));

            Assert.AreEqual(120, result);
        }

        [Test]
        public void PercentsSumRatherThanCompound()
        {
            // Two +50% relics give +100%, not +125%. Summing is arithmetic the
            // player can do; compounding is how a stack of reasonable relics
            // becomes an unplayable number nobody authored.
            int result = EffectFold.Apply(100, EffectStat.Attack, Held(Mod(EffectKind.AttackPercent, 50), Mod(EffectKind.AttackPercent, 50)));

            Assert.AreEqual(200, result);
        }

        [Test]
        public void FlatsLandAfterPercentsSoTheyDoNotGetScaled()
        {
            // 100 base, +100%, +10 flat = 210. The other order would give 220
            // and make every flat bonus quietly scale with every percent relic
            // in the run -- which reads as a bug the first time anyone checks.
            int result = EffectFold.Apply(100, EffectStat.Attack, Held(Mod(EffectKind.AttackPercent, 100), Mod(EffectKind.AttackFlat, 10)));

            Assert.AreEqual(210, result);
        }

        [Test]
        public void AModifierForAnotherStatIsIgnored()
        {
            int result = EffectFold.Apply(100, EffectStat.Attack,
                Held(Mod(EffectKind.DefensePercent, 500)));

            Assert.AreEqual(100, result);
        }

        [Test]
        public void NegativeModifiersReduceButNeverBelowZero()
        {
            // A relic with a real downside is the interesting kind. A stack of
            // them producing a negative max health would take the fight down
            // rather than make the character weak.
            Assert.AreEqual(80, EffectFold.Apply(100, EffectStat.Attack,
                Held(Mod(EffectKind.AttackPercent, -20))));

            Assert.AreEqual(0, EffectFold.Apply(100, EffectStat.Attack, Held(Mod(EffectKind.AttackPercent, -90), Mod(EffectKind.AttackPercent, -90))));
        }

        [Test]
        public void NoModifiersLeavesTheValueAlone()
        {
            Assert.AreEqual(100, EffectFold.Apply(100, EffectStat.Attack, null));
            Assert.AreEqual(100, EffectFold.Apply(100, EffectStat.Attack, EffectSet.Empty));
        }

        [Test]
        public void NoneIsNeverAStatSoItCanNeverChangeAnything()
        {
            Assert.IsFalse(EffectCatalog.Has(EffectKind.None), "None has no catalog row, so no stat");
            Assert.AreEqual(100, EffectFold.Apply(100, EffectStat.None, Held(Mod(EffectKind.AttackFlat, 999))));
        }

        // Modifier types that deliberately do NOT map to a single stat, and
        // are applied by their own path instead. Named here rather than
        // skipped by a condition, so adding one is a decision somebody makes
        // in this file rather than a hole that opens quietly.
        private static readonly EffectKind[] NotSingleStat =
        {
            // Five numbers, not one -- see EffectFold.ApplyResistance.
            EffectKind.ResistanceFlat,
        };

        [Test]
        public void EveryModifierTypeExceptNoneMapsToARealStat()
        {
            // Walked from the catalog: a relic stat kind added without a Stat
            // would fold into nothing and silently do nothing forever. Relic
            // mechanics are flags, read at their own hooks, not here.
            foreach (var info in EffectCatalog.All)
            {
                if (!info.Allows(EffectHolder.Relic) || info.IsMechanic) continue;
                if (NotSingleStat.Contains(info.Kind)) continue;

                Assert.AreNotEqual(EffectStat.None, info.Stat,
                    $"{info.Kind} has no EffectStat in its catalog row, so it can never apply");
            }
        }

        // AND THE EXEMPTION IS NOT A FREE PASS. A type excused from the sweep
        // above still has to be applied by something, or the exemption is
        // exactly the silent hole the sweep exists to refuse.
        [Test]
        public void AModifierExcusedFromTheStatSweepIsStillAppliedBySomething()
        {
            var before = new ResistanceByType();

            var after = EffectFold.ApplyResistance(before, Held(
                new Effect(EffectKind.ResistanceFlat, 15, against: DamageType.Fire),
                new Effect(EffectKind.ResistanceFlat, 10, againstMagical: true)));

            Assert.AreEqual(25, after.Fire, "fire takes both the specific 15 and the magical 10");
            Assert.AreEqual(10, after.Ice, "ice takes the magical 10 alone");
            Assert.AreEqual(0, after.Physical, "physical is not magical and takes neither");
        }

        [Test]
        public void EveryStatExceptNoneHasBothAPercentAndAFlatType()
        {
            // The pairing is the promise the enum makes. A stat with only one
            // half is a gap an author will hit and not understand.
            foreach (EffectStat stat in Enum.GetValues(typeof(EffectStat)))
            {
                if (stat == EffectStat.None) continue;

                // ArmorPenetration is FLAT ONLY, on purpose -- see
                // EffectKind.ArmorPenetrationFlat's own comment: it
                // already lands against a stat (broad Defense) that is
                // itself already a percentage-shaped mitigation curve, the
                // same reasoning ResistanceFlat gives for staying flat-only
                // (ResistanceFlat is exempt from this canary entirely by
                // never mapping to an EffectStat at all).
                if (stat == EffectStat.ArmorPenetration) continue;

                // Balance pass 2: WeaknessDamageBonusPercent and
                // LifestealPercent are flat-only for the identical reason --
                // each already lands against something percent-shaped (a
                // multiplier, a percent-of-damage heal), so a percent-of-a-
                // percent twin would be exactly as unreadable as it would be
                // for ArmorPenetration or Resistance.
                if (stat == EffectStat.WeaknessBonus || stat == EffectStat.Lifesteal) continue;

                // The crit stats are flat points added to the party baseline
                // (CritRules), so a percent-of-a-percent twin would read the
                // same way as it would for Lifesteal.
                if (stat == EffectStat.CritChance || stat == EffectStat.CritDamage) continue;

                var kinds = EffectCatalog.All.Where(i => i.Stat == stat).ToList();

                Assert.IsTrue(kinds.Any(i => i.Percent), $"{stat} has no percent kind");
                Assert.IsTrue(kinds.Any(i => !i.Percent), $"{stat} has no flat kind");
            }
        }

        [Test]
        public void AHugeStackSaturatesRatherThanOverflowing()
        {
            var huge = Held(Enumerable.Range(0, 50)
                .Select(_ => Mod(EffectKind.AttackFlat, int.MaxValue / 10))
                .ToArray());

            int result = EffectFold.Apply(1000, EffectStat.Attack, huge);

            Assert.AreEqual(int.MaxValue, result, "an int wrap would turn a stack of buffs into a negative stat");
        }

        // ---- what the resolver refuses to ship ---------------------------------------

        private static readonly string[] Known = { "first_forest_boss" };
        private static readonly string[] KnownCharacters = { "sheep" };

        private static RawRelicEntry Relic(RawEffect[] effects) =>
            new RawRelicEntry { id = "x", displayName = "X", effects = effects };

        [Test]
        public void AnUnknownModifierTypeIsAContentBuildFailure()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(new[] { new RawEffect { type = "Wingspan", magnitude = 5 } }) },
                Known, KnownCharacters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("isn't an effect a relic can grant", errors[0]);
        }

        [Test]
        public void AZeroAmountIsAContentBuildFailure()
        {
            // A modifier of zero is a line of content that changes nothing,
            // which is indistinguishable from a mistyped amount.
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(new[] { new RawEffect { type = "AttackPercent", magnitude = 0 } }) },
                Known, KnownCharacters, out _, out var errors);

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
                id = "x", displayName = "X",
                effects = new[]
                {
                    new RawEffect { type = "DualWield" },
                    new RawEffect { type = "AttackPercent", magnitude = 15 },
                },
            };

            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { raw }, Known, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(EffectKind.DualWield, resolved[0].Mechanic);
            Assert.AreEqual(2, resolved[0].Effects.Length);
            Assert.IsTrue(resolved[0].HasBehaviour);
        }

        [Test]
        public void APlaceholderHasNoBehaviourAndSaysSo()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(new RawEffect[0]) }, Known, KnownCharacters, out var resolved, out _);

            Assert.IsTrue(ok, "a mechanic-less relic is still valid content");
            Assert.IsFalse(resolved[0].HasBehaviour);
        }

        // ---- mechanic (e): armour penetration -------------------------------------

        [Test]
        public void ArmorPenetrationFlatAppliesToTheArmorPenetrationStat()
        {
            int result = EffectFold.Apply(0, EffectStat.ArmorPenetration,
                Held(Mod(EffectKind.ArmorPenetrationFlat, 35)));

            Assert.AreEqual(35, result, "Pointy Nail's own number, applied as a flat addition to a base of 0");
        }

        [Test]
        public void ArmorPenetrationDoesNotLeakOntoOtherStats()
        {
            int attack = EffectFold.Apply(100, EffectStat.Attack,
                Held(Mod(EffectKind.ArmorPenetrationFlat, 35)));

            Assert.AreEqual(100, attack, "an armour-penetration modifier must not touch Attack");
        }
    }
}
