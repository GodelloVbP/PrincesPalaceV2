using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Plan 4b, Cursed Blood: while the holder's HealConversion window is open,
    // every heal through FightSession.HealAndCount restores nothing and its
    // EFFECTIVE amount (capped by missing health at that moment) is dealt as
    // Void to every living enemy. Also pins that the Regen tick, lifesteal
    // and potions all reach that funnel.
    public class HealConversionTests
    {
        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) Fight(
            int heroHealth = 200)
        {
            var hero = new CombatantState("Bjorn", true, heroHealth, 0, 20, 20) { CritChancePercent = 0 };
            var foe1 = new CombatantState("Foe1", false, 1000, 0, 1, 1);
            var foe2 = new CombatantState("Foe2", false, 1000, 0, 1, 1);
            var kit = new PlayerKit("bjorn", CharacterRole.Tank, null, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe1, foe2 }),
                new List<PlayerKit> { kit }, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return (session, hero, foe1, foe2);
        }

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        [Test]
        public void EffectiveAmount_IsTheHealCappedByMissingHealth()
        {
            var c = new CombatantState("C", true, 100, 0, 1, 1) { CurrentHealth = 90 };
            Assert.AreEqual(10, HealConversion.EffectiveAmount(c, 50));
            Assert.AreEqual(5, HealConversion.EffectiveAmount(c, 5));
            c.CurrentHealth = 100;
            Assert.AreEqual(0, HealConversion.EffectiveAmount(c, 50), "full health converts nothing");
            c.CurrentHealth = 0;
            Assert.AreEqual(0, HealConversion.EffectiveAmount(c, 50), "a corpse converts nothing");
        }

        [Test]
        public void Off_AHealRestoresHealthAndTouchesNoEnemy()
        {
            var (session, hero, foe1, foe2) = Fight();
            hero.CurrentHealth = 150;

            session.HealForTest(hero, 30);

            Assert.AreEqual(180, hero.CurrentHealth);
            Assert.AreEqual(1000, foe1.CurrentHealth);
            Assert.AreEqual(1000, foe2.CurrentHealth);
        }

        [Test]
        public void On_AHealRestoresNothingAndHitsEveryEnemyForIt()
        {
            var (session, hero, foe1, foe2) = Fight();
            hero.CurrentHealth = 150;
            session.OpenCursedBlood(hero, 2);

            session.HealForTest(hero, 30);

            Assert.AreEqual(150, hero.CurrentHealth, "no healing takes");
            Assert.AreEqual(970, foe1.CurrentHealth);
            Assert.AreEqual(970, foe2.CurrentHealth);
            Assert.AreEqual(0, session.Ledger.For("bjorn").Healed, "nothing restored, nothing booked");
            Assert.IsTrue(Messages(session).Contains("Bjorn's cursed blood turns 30 healing into void!"));
        }

        [Test]
        public void OnlyTheEffectiveHealConverts_CappedByMissingHealth()
        {
            var (session, hero, foe1, _) = Fight();
            hero.CurrentHealth = 190; // missing 10
            session.OpenCursedBlood(hero, 2);

            session.HealForTest(hero, 50);

            Assert.AreEqual(990, foe1.CurrentHealth, "50 raw on 10 missing converts 10");
            Assert.AreEqual(190, hero.CurrentHealth);

            // And since he cannot heal, the missing health stays put: the next
            // heal converts in full up to the same cap.
            session.HealForTest(hero, 50);
            Assert.AreEqual(980, foe1.CurrentHealth);
        }

        [Test]
        public void AtFullHealth_NothingConvertsAndNothingHeals()
        {
            var (session, hero, foe1, _) = Fight();
            session.OpenCursedBlood(hero, 2);

            session.HealForTest(hero, 50);

            Assert.AreEqual(200, hero.CurrentHealth);
            Assert.AreEqual(1000, foe1.CurrentHealth);
        }

        [Test]
        public void ARegenTickConverts()
        {
            var (session, hero, foe1, foe2) = Fight();
            hero.CurrentHealth = 150;
            session.ApplyStatusToForTest(hero, StatusEffectType.Regen, 25, 3);
            session.OpenCursedBlood(hero, 2);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(150, hero.CurrentHealth);
            Assert.AreEqual(975, foe1.CurrentHealth);
            Assert.AreEqual(975, foe2.CurrentHealth);
            Assert.IsFalse(Messages(session).Any(m => m.Contains("regenerates")),
                "a converted tick does not claim to have regenerated anything");
        }

        // The Regen tick joined the funnel without changing what it does
        // outside Cursed Blood: same heal, same one ledger row, same line.
        [Test]
        public void ARegenTickOutsideCursedBlood_HealsAndIsBookedOnce()
        {
            var (session, hero, foe1, _) = Fight();
            hero.CurrentHealth = 150;
            session.ApplyStatusToForTest(hero, StatusEffectType.Regen, 25, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(175, hero.CurrentHealth);
            Assert.AreEqual(1000, foe1.CurrentHealth);
            Assert.AreEqual(25, session.Ledger.For("bjorn").Healed);
            var beats = session.DrainBeats();
            var regen = beats.Single(b => b.StatusTick == StatusEffectType.Regen);
            Assert.AreEqual(25, regen.Amount);
            Assert.IsTrue(regen.IsHealing);
            Assert.IsTrue(regen.Messages.Contains("Bjorn regenerates 25 health."));
        }

        [Test]
        public void APotionConverts()
        {
            var (session, hero, foe1, foe2) = Fight();
            hero.CurrentHealth = 150; // missing 50
            session.OpenCursedBlood(hero, 2);

            session.UseConsumable("Health Potion", 60, restoresMana: false);

            Assert.AreEqual(950, foe1.CurrentHealth, "60 raw on 50 missing converts 50");
            Assert.AreEqual(950, foe2.CurrentHealth);
            Assert.AreEqual(0, session.Ledger.For("bjorn").Healed);
        }

        [Test]
        public void LifestealConverts()
        {
            var (session, hero, foe1, _) = Fight(heroHealth: 100);
            hero.CurrentHealth = 50;
            hero.RelicLifestealPercent = 50;
            session.OpenCursedBlood(hero, 2);

            session.ExecuteAttack(foe1);

            // 20 Attack x the 1.2 basic-attack coefficient = 24 (see
            // RelicMechanicsTests.VampireDenturesHealsTenPercentOfLandedDamage);
            // 50% of 24 = 12 converts, on top of the swing itself.
            Assert.AreEqual(1000 - 24 - 12, foe1.CurrentHealth);
        }

        [Test]
        public void TheWindowClosesAfterItsTurns()
        {
            var (session, hero, foe1, _) = Fight();
            Assert.AreSame(hero, session.Current, "fixture: opened on the holder's own turn");
            hero.CurrentHealth = 150;
            session.OpenCursedBlood(hero, 2);

            // Opened on his own turn, so that turn's end is spared (TurnWindow):
            // it covers two FULL turns after this one.
            session.TickStatusesAtTurnEndForTest(hero);
            session.TickStatusesAtTurnEndForTest(hero);
            Assert.IsTrue(hero.HealConversion.IsActive);
            session.TickStatusesAtTurnEndForTest(hero);
            Assert.IsFalse(hero.HealConversion.IsActive);

            session.HealForTest(hero, 30);
            Assert.AreEqual(180, hero.CurrentHealth, "heals land again once it closes");
            Assert.AreEqual(1000, foe1.CurrentHealth);
        }

        [Test]
        public void AConvertedHealIsVoidDamage_ResistedLikeAnyTypedHit()
        {
            var (session, hero, foe1, foe2) = Fight();
            foe2.TypedResistance = foe2.TypedResistance.With(DamageType.Void, 100);
            hero.CurrentHealth = 100;
            session.OpenCursedBlood(hero, 2);

            session.HealForTest(hero, 100);

            Assert.AreEqual(900, foe1.CurrentHealth);
            Assert.Less(1000 - foe2.CurrentHealth, 100, "a Void-resistant enemy takes less");
        }
    }
}
