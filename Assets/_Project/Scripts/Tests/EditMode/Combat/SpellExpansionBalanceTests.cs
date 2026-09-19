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
    // THE BALANCE HARNESS for milestone A, and the instrument behind the
    // measured values recorded in docs/PLAN_SPELL_EXPANSION.md section 5.
    //
    // WHY A HARNESS AND NOT tools/bot.ps1. The bot plays whole runs and its
    // numbers are a floor, not a verdict -- but it reports damage and survival,
    // not points-of-shield-per-mana, and it cannot script "the same spell at
    // spell-Attack 4 and at 90", which is the comparison that decides whether
    // a ward earns one of three book slots. Section 5 names the focused
    // harness as the honest instrument for exactly these two pairs; this is it.
    //
    // THE NUMBERS ARE PINNED AS LITERALS, deliberately. They are measurements,
    // so a change to one is a balance decision somebody made, and it should
    // cost a visible edit here and a line in section 5 rather than sliding.
    // Nothing below recomputes a production formula to get its expectation.
    //
    // INVESTIGATE, DO NOT AUTO-TUNE. Where a figure looked wrong it is written
    // down and left alone; section 5 records what was found.
    public class SpellExpansionBalanceTests
    {
        // The two anchor casters PhaseFourSkillTests already uses for wards:
        // a level-1 spell attack of 4, and a late-run 90.
        private const int LowAnchorAttack = 4;
        private const int HighAnchorAttack = 90;

        private static CombatantState Caster(int attack, int mana = 200) =>
            new CombatantState("Caster", true, 500, mana, attack, 10);

        private static CombatantState Dummy(int health = 100000, int speed = 20) =>
            new CombatantState("Dummy", false, health, 10, 5, speed);

        // ---- Gilded Aegis against the existing protection family --------------

        private static ResolvedSkill Aegis() =>
            new ResolvedSkill("gilded_aegis", "Gilded Aegis", "", "hero", 1, SkillEffect.Ward,
                SkillTargeting.SingleAlly, 7, 0, false, 0, 18, false,
                null, SpellPresentation.None, 0, wardTurns: 2, scalingAxis: ScalingAxis.Spell);

        private static int WardPoints(ResolvedSkill skill, CombatantState caster) =>
            SkillResolution.Amount(SkillEffect.Ward, caster, caster, skill.Power, skill.FlatAmount,
                resourceSpent: 0, ignoresDefense: false, type: DamageType.Physical, axis: skill.ScalingAxis,
                percentOfMaxHealthPerPoint: 0, percentOfCasterMaxHealth: skill.PercentOfCasterMaxHealth);

        // MEASURED 2026-09-20. 18 + spell attack, the shape prism_ward already
        // uses, so the two move together as a caster grows instead of crossing
        // over somewhere in the middle of a run.
        //
        //   caster        Gilded Aegis   prism_ward   fleece_ward
        //   spell atk 4   22 pts / 7 mp  24 / 8 mp    50 pts / 2 wool
        //   spell atk 90  108 / 7 mp     110 / 8 mp   50 / 2 wool
        //
        // Deliberately just under prism_ward at both anchors, for one mana
        // less and with a two-turn cooldown prism_ward does not carry. Read as
        // points per mana that is 3.1 against 3.0 low and 15.4 against 13.8
        // high -- Aegis is marginally better per point of mana and strictly
        // worse per cast, which is the trade a cooldown is supposed to buy.
        [Test]
        public void GildedAegisIsEighteenPlusSpellAttack_JustUnderPrismWardAtBothAnchors()
        {
            var aegis = Aegis();

            Assert.AreEqual(22, WardPoints(aegis, Caster(LowAnchorAttack)));
            Assert.AreEqual(108, WardPoints(aegis, Caster(HighAnchorAttack)));

            Assert.AreEqual(7, aegis.ManaCost, "one under prism_ward's eight");
        }

        // NOT DOMINATED AND NOT DOMINATING, which is the only question section
        // 5 asks of a number before final art. Against the flat 50 of
        // fleece_ward it is worse early and twice as good late; against
        // prism_ward it is a shade worse for a shade less mana. Neither
        // comparison is one-sided, so the prototype figure stands.
        [Test]
        public void GildedAegisCrossesFleeceWardsFlatFifty_SomewhereInTheMiddleOfARun()
        {
            Assert.Less(WardPoints(Aegis(), Caster(LowAnchorAttack)), 50,
                "a fresh caster's Aegis must not already beat the 50-point flat ward");
            Assert.Greater(WardPoints(Aegis(), Caster(HighAnchorAttack)), 50,
                "and a late caster's must, or the scaling term is decoration");
        }

        // ---- Winter's Rebuke against the direct-damage books -------------------

        private static ResolvedSkill Packet(string id, DamageType type, int amount, int mana) =>
            new ResolvedSkill(id, id, "", "hero", 1, SkillEffect.DamageSingle, SkillTargeting.SingleEnemy,
                mana, 0, false, 0, 0, false,
                new[] { new DamageInstance(type, amount) }, SpellPresentation.None, 0);

        // What one cast actually takes off a defenceless dummy, variance off --
        // the resolution path itself rather than an arithmetic restatement of
        // it.
        private static int LandedDamage(ResolvedSkill skill, int attack)
        {
            var caster = Caster(attack);

            // Speed 1 so the caster holds the opening turn -- otherwise the
            // cast is refused for the honest reason that it is not their turn,
            // and the measurement reads zero.
            var dummy = Dummy(speed: 1);
            dummy.PhysicalDefense = 0;
            dummy.MagicalDefense = 0;

            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { skill }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { caster }, new[] { dummy }),
                new List<PlayerKit> { kit }, null, new SeededRandom(11))
            {
                DamageVarianceRange = 0f,
            };

            int before = dummy.CurrentHealth;
            session.CastSkill(0, dummy);
            return before - dummy.CurrentHealth;
        }

        // MEASURED 2026-09-20, against an undefended target with variance off.
        //
        //   spell            packet   mana   landed   per mana
        //   Winter's Rebuke  Frost 6  8      6        0.75
        //   mud_burst        Earth 6  8      6        0.75
        //   lightning_bolt   Ltng 10  11     10       0.91
        //
        // A fixed packet does not scale with the caster's attack at all -- the
        // tier and spell-scaling multipliers are 1 on a bare fixture -- so both
        // anchors read the same, which is the existing behaviour of every
        // damageInstances book and not something this spell introduces.
        //
        // THE FINDING, recorded rather than tuned: Winter's Rebuke is exactly
        // mud_burst's packet for exactly mud_burst's mana, and it is a tier-2
        // book where mud_burst is tier 1. It is not dominated -- it carries a
        // two-turn Chilled where mud_burst carries a one-turn Vulnerable, and
        // the Chill is worth roughly a fifth of an enemy action per round (the
        // queue-control case below) -- but the damage half of it earns nothing
        // over a cheaper book. Left at the prototype figure and reported in
        // section 5 rather than moved, because which side should move is a
        // design call.
        [Test]
        public void WintersRebukeMatchesMudBurstsPacketForTheSameMana()
        {
            Assert.AreEqual(6, LandedDamage(Packet("winters_rebuke", DamageType.Ice, 6, 8), LowAnchorAttack));
            Assert.AreEqual(6, LandedDamage(Packet("winters_rebuke", DamageType.Ice, 6, 8), HighAnchorAttack));

            Assert.AreEqual(6, LandedDamage(Packet("mud_burst", DamageType.Earth, 6, 8), LowAnchorAttack));
            Assert.AreEqual(10, LandedDamage(Packet("lightning_bolt", DamageType.Lightning, 10, 11), LowAnchorAttack));
        }

        // ---- the queue-control half --------------------------------------------

        // ENEMY ACTIONS ALLOWED, which is the metric section 5 says control
        // lives or dies on, read straight off the real forecast rather than
        // derived from the speed arithmetic.
        private static int EnemyTurnsInTheNext(int window, params int[] chillPercents)
        {
            var hero = Caster(LowAnchorAttack);
            var foe = Dummy(speed: 20);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var session = new FightSession(encounter, null, null, new SeededRandom(5));
            session.Begin();

            foreach (int percent in chillPercents)
            {
                session.ApplyChilledForTest(foe, percent, turns: 5, source: hero);
            }

            return encounter.UpcomingTurns(window).Count(c => !c.IsPlayerSide);
        }

        // MEASURED 2026-09-20. Hero speed 10, one enemy at speed 20, read off
        // CombatEncounter.UpcomingTurns over a 30-turn forecast window.
        //
        //   chill on the enemy   enemy turns in 30   actions denied
        //   none                 17                  --
        //   one at 25%           16                  1
        //   two at 25%           15                  2
        //   three at 25%         12                  5
        //
        // THE FINDING, and it is the one worth carrying to the owner: a 25%
        // Chilled does NOT deny 25% of an enemy's actions. It denies about 6%
        // of them. The badge says -25% Speed and that is true, but SpeedScale's
        // charge curve is deliberately sub-linear with a hard ceiling (its own
        // header records why), so a quarter off the Speed number is much less
        // than a quarter off how often the actor acts. The third stack is where
        // it starts to bite, which is an argument FOR the stacking model rather
        // than against the number.
        //
        // Not tuned. 25% is the owner's prototype figure and the measurement
        // says it is weak rather than wrong; whether the percent should move or
        // the expectation should is a design call, recorded in section 5.
        [Test]
        public void WintersRebukesChillDeniesFarFewerActionsThanItsPercentSuggests()
        {
            Assert.AreEqual(17, EnemyTurnsInTheNext(30), "the unchilled baseline");
            Assert.AreEqual(16, EnemyTurnsInTheNext(30, 25), "one Rebuke buys one action in thirty");
            Assert.AreEqual(15, EnemyTurnsInTheNext(30, 25, 25), "two buy two");
            Assert.AreEqual(12, EnemyTurnsInTheNext(30, 25, 25, 25),
                "and three buy five -- the stacking model is what makes a second and third cast worth it");
        }
    }
}
