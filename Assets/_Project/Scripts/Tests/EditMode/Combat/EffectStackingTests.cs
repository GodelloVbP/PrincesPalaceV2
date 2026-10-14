using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // docs/PLAN_ITEMS.md 2.1's stacking table, pinned (section 6): two copies
    // of an affix give the best one, an affix and any other holder add, and a
    // mechanic held twice is still held once, whoever the holders are.
    public class EffectStackingTests
    {
        private static EffectSet TwoLifestealAffixesAndVampireDentures() =>
            EffectSet.Of(EffectHolder.Affix, new[]
                {
                    new Effect(EffectKind.LifestealPercent, 8),
                    new Effect(EffectKind.LifestealPercent, 12),
                })
                .WithRelics(new[] { TestRelics.Stats("vampire_dentures", new Effect(EffectKind.LifestealPercent, 10)) },
                    "sheep");

        [Test]
        public void TwoLifestealAffixesAndVampireDentures_GiveTheBestAffixPlusTheRelic()
        {
            Assert.AreEqual(22, TwoLifestealAffixesAndVampireDentures().Magnitude(EffectKind.LifestealPercent),
                "best affix 12, not 8 + 12; plus the relic's 10");
        }

        [Test]
        public void TwoLifestealAffixesAndVampireDentures_HealThatShareOfALandedHit()
        {
            var hero = new CombatantState("Shawn", true, 100, 0, 20, 20)
            {
                CurrentHealth = 50,
                Effects = TwoLifestealAffixesAndVampireDentures(),
            };
            var foe = new CombatantState("Foe", false, 999999, 0, 1, 1);
            var kit = new PlayerKit("sheep", CharacterRole.Tank, new List<ResolvedSkill>(), new List<ResolvedRelic>(), null);
            var foeKit = new EnemyKit(new ResolvedEnemy("Foe", "Foe", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, new List<EnemyKit> { foeKit },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.ExecuteAttack(foe);

            Assert.AreEqual(24, 999999 - foe.CurrentHealth, "20 Attack at the 1.2x basic-attack coefficient");
            Assert.AreEqual(55, hero.CurrentHealth, "22% of 24 = 5.28, rounded to 5, on top of 50");
        }

        [Test]
        public void AMechanicHeldTwice_IsHeldOnce()
        {
            var relics = new[] { TestRelics.Of(EffectKind.ChargingCrystal), TestRelics.Of(EffectKind.ChargingCrystal) };
            var effects = TestRelics.EffectsFor("sheep", relics);

            Assert.IsTrue(effects.Has(EffectKind.ChargingCrystal));
            Assert.AreEqual(50, effects.Magnitude(EffectKind.ChargingCrystal), "a second holder adds nothing");
        }

        // Section 6: one member with the same trait kind on two worn copies
        // plus the relic. Combat reads the mechanic once; the dossier marks
        // the surplus copies inactive (relic first, then slot order).
        [Test]
        public void ATraitOnTwoWornCopiesPlusTheRelic_IsHeldOnce_AndBothCopiesAreInactive()
        {
            var effects = EffectSet.Of(EffectHolder.Trait, new[]
                {
                    new Effect(EffectKind.ChargingCrystal, 50),
                    new Effect(EffectKind.ChargingCrystal, 50),
                })
                .WithRelics(new[] { TestRelics.Of(EffectKind.ChargingCrystal) }, "sheep");

            Assert.IsTrue(effects.Has(EffectKind.ChargingCrystal));
            Assert.AreEqual(50, effects.Magnitude(EffectKind.ChargingCrystal), "three holders, one effect");

            var worn = new[] { EffectKind.ChargingCrystal, EffectKind.None, EffectKind.ChargingCrystal };
            CollectionAssert.AreEqual(new[] { true, false, true },
                TraitStacking.Inactive(worn, new[] { EffectKind.ChargingCrystal }));
        }

        [Test]
        public void TheSameTraitOnTwoWornCopies_WithoutARelic_MakesOnlyTheLaterOneInactive()
        {
            var worn = new[] { EffectKind.LongCount, EffectKind.DualWield, EffectKind.LongCount };
            CollectionAssert.AreEqual(new[] { false, false, true }, TraitStacking.Inactive(worn, null));
        }

        [Test]
        public void ATraitAndARelicOfDifferentKinds_AreBothActive()
        {
            var worn = new[] { EffectKind.EssenceSiphon };
            CollectionAssert.AreEqual(new[] { false }, TraitStacking.Inactive(worn, new[] { EffectKind.DualWield }));
        }

        [Test]
        public void ResistanceCopies_AllCount_PerElement()
        {
            var effects = EffectSet.Of(EffectHolder.Affix, new[]
                {
                    new Effect(EffectKind.ResistanceFlat, 8, against: DamageType.Fire),
                    new Effect(EffectKind.ResistanceFlat, 8, against: DamageType.Fire),
                })
                .WithRelics(new[] { TestRelics.Stats("darrows_buckler",
                    new Effect(EffectKind.ResistanceFlat, 25, againstMagical: true)) }, "sheep");

            var folded = EffectFold.ApplyResistance(new ResistanceByType(), effects);

            Assert.AreEqual(41, folded.For(DamageType.Fire), "8 + 8 + 25: resistance copies are summed, not best-of");
            Assert.AreEqual(25, folded.For(DamageType.Ice), "the magical relic reaches every non-Physical type");
            Assert.AreEqual(0, folded.For(DamageType.Physical));
        }

        [Test]
        public void RelicStats_SumPercentsThenAddFlats()
        {
            var effects = TestRelics.EffectsFor("sheep", new[]
            {
                TestRelics.Stats("a", new Effect(EffectKind.AttackPercent, 20)),
                TestRelics.Stats("b", new Effect(EffectKind.AttackPercent, 30), new Effect(EffectKind.AttackFlat, 5)),
            });

            Assert.AreEqual(155, EffectFold.Apply(100, EffectStat.Attack, effects), "100 + 50% (summed, not 56%) + 5");
        }

        [Test]
        public void ARelicAndARunicAffix_BothFeedMaxMana()
        {
            var effects = EffectSet.Of(EffectHolder.Affix, new[]
                {
                    new Effect(EffectKind.MaxManaFlat, 6),
                    new Effect(EffectKind.MaxManaFlat, 9),
                })
                .WithRelics(new[] { TestRelics.Stats("r", new Effect(EffectKind.MaxManaFlat, 10)) }, "owl");

            Assert.AreEqual(119, EffectFold.Apply(100, EffectStat.MaxMana, effects), "100 + best affix 9 + relic 10");
        }

        [Test]
        public void ABearersRelic_ReachesNobodyElse()
        {
            var relics = new[] { TestRelics.Of(EffectKind.Kinship, bearer: "sheep") };

            Assert.IsTrue(TestRelics.EffectsFor("sheep", relics).Has(EffectKind.Kinship));
            Assert.IsFalse(TestRelics.EffectsFor("owl", relics).Has(EffectKind.Kinship));
        }
    }
}
