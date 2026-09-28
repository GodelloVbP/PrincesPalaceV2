using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // Plan 4b moved the Regen tick, lifesteal and Mending Fleece's ward-break
    // heal into FightSession.HealAndCount so Cursed Blood can convert them.
    // What fires ON a heal must not change for them (owner correction,
    // 2026-09-28): World Ender's Crown never heard those three and still does
    // not, and still hears every heal it heard before (skills, potions).
    //
    // Fixture: a crown bearer at 10/100 who has not fired it. Any heal that
    // RUNS the crown's check leaves them under 30% and fires it -- so the
    // crown firing is exactly the observable "this heal ran heal triggers".
    public class HealTriggerTests
    {
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight()
        {
            var hero = new CombatantState("Bjorn", true, 100, 0, 20, 20) { CritChancePercent = 0 };
            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1);
            var crown = new ResolvedRelic("crown", "crown", "", RelicEffect.WorldEndersCrown, 0);
            var kit = new PlayerKit("bjorn", CharacterRole.Tank, null, new List<ResolvedRelic> { crown }, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            hero.CurrentHealth = 10;
            return (session, hero, foe);
        }

        private static bool CrownFired(CombatantState foe) =>
            foe.Statuses.Any(s => s.Type == StatusEffectType.Feared);

        [Test]
        public void ARegenTickDoesNotFireTheCrown()
        {
            var (session, hero, foe) = Fight();
            session.ApplyStatusToForTest(hero, StatusEffectType.Regen, 5, 3);

            session.TickStatusesForTest(hero);

            Assert.AreEqual(15, hero.CurrentHealth, "fixture: the tick healed");
            Assert.IsFalse(CrownFired(foe));
            Assert.AreEqual(5, session.Ledger.For("bjorn").Healed, "but it is booked as healing");
        }

        [Test]
        public void LifestealDoesNotFireTheCrown()
        {
            var (session, hero, foe) = Fight();
            hero.RelicLifestealPercent = 50;

            session.ExecuteAttack(foe);

            Assert.AreEqual(1000 - 24, foe.CurrentHealth, "fixture: a 24-point swing");
            Assert.IsFalse(CrownFired(foe));
            Assert.AreEqual(12, session.Ledger.For("bjorn").Healed, "50% of 24, booked as healing");
        }

        [Test]
        public void TheMendingFleeceBreakHealDoesNotFireTheCrown()
        {
            var (session, hero, foe) = Fight();
            hero.Talents = new TalentEffectSet(new[] { new TalentEffect(TalentEffectType.WardHealsWhenSpent, 10) });
            StatusEffects.ApplyWard(hero.Statuses, points: 5, turns: 2, source: hero);

            session.ResolveWardForTest(hero, 20);

            Assert.AreEqual(20, hero.CurrentHealth, "fixture: the break healed 10% of 100");
            Assert.IsFalse(CrownFired(foe));
            Assert.AreEqual(10, session.Ledger.For("bjorn").Healed);
        }

        [Test]
        public void AFunnelHealStillFiresTheCrown()
        {
            var (session, hero, foe) = Fight();

            session.HealForTest(hero, 5);

            Assert.IsTrue(CrownFired(foe));
        }

        [Test]
        public void APotionStillFiresTheCrown()
        {
            var (session, hero, foe) = Fight();

            session.UseConsumable("Health Potion", 5, restoresMana: false);

            Assert.IsTrue(CrownFired(foe));
        }
    }
}
