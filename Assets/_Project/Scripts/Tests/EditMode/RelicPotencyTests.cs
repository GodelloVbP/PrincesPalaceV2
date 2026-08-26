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
    // The counting relics, and the one rule that makes them a choice rather
    // than a lottery ticket.
    //
    //     The bonus is a percentage of the action's OWN BASE, added AFTER
    //     everything that multiplies.
    //
    // A spell whose base is 50, doubled by something else, and then charged at
    // +50% deals 125 -- not 150. The doubling is worth 50, the crystal is worth
    // 25, and the crystal is worth 25 whatever else is going on. Applied at the
    // front of the pipeline instead it would compound with every multiplier it
    // met, and be worth six times as much against a weak target wearing
    // Vulnerable as against a resistant one.
    //
    // ASSERTED AS A DIFFERENCE, not against a table of absolute figures. What
    // matters is that charging a cast adds exactly half its base and not half
    // its total, and stating it that way stays true when the underlying damage
    // numbers are retuned -- which they will be.
    public class RelicPotencyTests
    {
        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static ResolvedSkill Bolt(int flat) =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, flat, ignoresDefense: true,
                null, SpellPresentation.None, 0);

        // ignoresDefense so armour cannot move the figures between the two
        // worlds these tests compare. The base is still the caster's scaled
        // attack PLUS the flat amount, not the flat amount alone -- which is
        // why nothing here asserts an absolute number.
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            RelicEffect? relic, ResolvedSkill? skill = null)
        {
            var hero = new CombatantState("Shawn", true, 999999, 300, 20, 10);
            // BIG ENOUGH TO SURVIVE EVERY BLOW THE TEST LANDS. A dummy that
            // dies partway reports the last hit as its remaining health, which
            // reads as the relic misfiring.
            var foe = new CombatantState("Dummy", false, 999999, 0, 5, 1);

            var relics = relic.HasValue
                ? new List<ResolvedRelic> { Relic(relic.Value) }
                : new List<ResolvedRelic>();

            var kit = new PlayerKit("hero", CharacterRole.Tank,
                skill.HasValue ? new List<ResolvedSkill> { skill.Value } : null, relics, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(),
                    0, 0, false, DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(11)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe);
        }

        private static int CastOnce(FightSession session, CombatantState hero, CombatantState foe,
                                    ResolvedSkill skill)
        {
            int before = foe.CurrentHealth;
            Assert.IsTrue(session.CastSkill(skill, foe), "the cast was refused");
            return before - foe.CurrentHealth;
        }

        // ---- the crystal ---------------------------------------------------------

        [Test]
        public void TheFourthCastIsTheOneThatIsCharged()
        {
            var bolt = Bolt(50);
            var (session, hero, foe) = Fight(RelicEffect.ChargingCrystal, bolt);

            var dealt = Enumerable.Range(0, 8)
                .Select(_ => CastOnce(session, hero, foe, bolt))
                .ToList();

            // 1, 2, 3 plain; 4 charged; 5, 6, 7 plain; 8 charged.
            Assert.AreEqual(dealt[0], dealt[1], "the second cast should be ordinary");
            Assert.AreEqual(dealt[0], dealt[2], "and the third");
            Assert.Greater(dealt[3], dealt[0], "the FOURTH is the charged one");

            Assert.AreEqual(dealt[0], dealt[4], "the count restarts rather than staying charged");
            Assert.AreEqual(dealt[0], dealt[6], "still ordinary at seven");
            Assert.AreEqual(dealt[3], dealt[7], "and the eighth is charged again, by the same amount");
        }

        // THE HEADLINE RULE, and the only spelling of it that actually proves
        // anything: the charge is worth THE SAME whether or not the cast is
        // being multiplied by something else.
        //
        // Measured twice -- once on a plain target, once on a target wearing
        // Vulnerable so the whole cast is scaled up -- and the two bonuses have
        // to match to the point. A bonus taken off the TOTAL would grow with
        // the multiplier and the two would differ; that is precisely the
        // failure this relic is designed not to have.
        [Test]
        public void TheChargeIsWorthTheSameWhateverElseIsMultiplyingTheCast()
        {
            int plainWorld = ChargeBonus(vulnerable: false);
            int multipliedWorld = ChargeBonus(vulnerable: true);

            Assert.Greater(plainWorld, 0, "the charge did not fire at all");
            Assert.AreEqual(plainWorld, multipliedWorld,
                "the charge changed size because something else was multiplying the cast. It is " +
                "supposed to be a percentage of the spell's OWN BASE, added after everything that " +
                "multiplies -- 50 base doubled and then charged is 125, not 150.");
        }

        // And it IS a percentage rather than a flat bonus in disguise: the same
        // relic on a spell with four times the base is worth four times as much.
        [Test]
        public void TheChargeTracksTheSpellItCharges()
        {
            int small = ChargeBonus(vulnerable: false, flat: 50);
            int large = ChargeBonus(vulnerable: false, flat: 50 + 4 * 40);

            Assert.Greater(large, small * 2,
                "a bigger spell must charge for proportionally more, not for the same flat figure");
        }

        // A charged cast minus an uncharged one, on an otherwise identical
        // board. Four casts, because the fourth is the one that counts.
        private static int ChargeBonus(bool vulnerable, int flat = 50)
        {
            var bolt = Bolt(flat);
            var (session, hero, foe) = Fight(RelicEffect.ChargingCrystal, bolt);

            if (vulnerable)
            {
                StatusEffects.Apply(foe.Statuses, StatusEffectType.Vulnerable, 50, 99);
            }

            int plain = CastOnce(session, hero, foe, bolt);
            CastOnce(session, hero, foe, bolt);
            CastOnce(session, hero, foe, bolt);
            int charged = CastOnce(session, hero, foe, bolt);

            if (vulnerable)
            {
                Assert.Greater(plain, 0);
            }

            return charged - plain;
        }


        [Test]
        public void WithoutTheRelicNoCastIsEverCharged()
        {
            var bolt = Bolt(50);
            var (session, hero, foe) = Fight(null, bolt);

            var dealt = Enumerable.Range(0, 8)
                .Select(_ => CastOnce(session, hero, foe, bolt))
                .ToList();

            Assert.AreEqual(1, dealt.Distinct().Count(),
                "no relic is equipped, so every cast should land for the same figure");
        }

        // A CAST IS A CAST, however the player reached it.
        //
        // The Charging Crystal counted character skills and not the basic Skill
        // action, because the counter was hooked into one of the two paths that
        // resolve a cast and nobody had a list of what those paths were. The
        // relic said "every 4th spell" and meant "every 4th of one kind", so a
        // player pressing the plain Skill button charged nothing, ever.
        //
        // Found by naming the moments rather than by playing the game -- see
        // FightSession.Relics.
        [Test]
        public void TheBasicSpellCountsAsASpell()
        {
            var (session, hero, foe) = Fight(RelicEffect.ChargingCrystal);

            var dealt = Enumerable.Range(0, 8)
                .Select(_ =>
                {
                    int before = foe.CurrentHealth;
                    session.ExecuteSkill(foe);
                    return before - foe.CurrentHealth;
                })
                .ToList();

            Assert.AreEqual(dealt[0], dealt[1], "the second basic cast is ordinary");
            Assert.Greater(dealt[3], dealt[0],
                "the fourth basic cast was not charged, so the plain Skill button does not count " +
                "as a spell");
            Assert.AreEqual(dealt[3], dealt[7], "and the eighth charges by the same amount");
        }

        // ---- the long count ------------------------------------------------------

        [Test]
        public void EveryThirdSwingCarriesTheTally()
        {
            var (session, hero, foe) = Fight(RelicEffect.LongCount);

            var dealt = Enumerable.Range(0, 6)
                .Select(_ =>
                {
                    int before = foe.CurrentHealth;
                    session.ExecuteAttack(foe);
                    return before - foe.CurrentHealth;
                })
                .ToList();

            Assert.AreEqual(dealt[0], dealt[1], "the second swing is ordinary");
            Assert.Greater(dealt[2], dealt[0], "the THIRD is the counted one");
            Assert.AreEqual(dealt[0], dealt[3], "and the count restarts");
            Assert.AreEqual(dealt[2], dealt[5], "the sixth matches the third");

            // 40% of the swing's own base, by the same rule the crystal follows.
            Assert.AreEqual(dealt[0] * 40 / 100, dealt[2] - dealt[0],
                "the tally is worth 40% of the blow's own base");
        }

        // A CAST IS NOT A SWING. The two relics count different things, and a
        // shared counter would make either of them fire on the other's actions
        // -- which is the bug a single "actions taken" tally would have.
        [Test]
        public void TheTwoCountersDoNotFeedEachOther()
        {
            var bolt = Bolt(50);
            var (session, hero, foe) = Fight(RelicEffect.ChargingCrystal, bolt);

            // Three swings first. If they advanced the spell tally, the very
            // first cast would come out charged.
            for (int i = 0; i < 3; i++) session.ExecuteAttack(foe);

            int first = CastOnce(session, hero, foe, bolt);
            CastOnce(session, hero, foe, bolt);
            CastOnce(session, hero, foe, bolt);
            int fourth = CastOnce(session, hero, foe, bolt);

            Assert.Greater(fourth, first,
                "swings advanced the spell counter, so 'every fourth cast' counted the wrong actions");
        }
    }
}
