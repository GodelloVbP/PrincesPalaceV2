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

        // ---- milestone B: Ashen Reckoning vs recasting Bite, vs an ordinary
        // ---- Nature detonation ------------------------------------------------

        private static ResolvedSkill Bite() =>
            new ResolvedSkill("vipers_bite", "Viper's Bite", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 7, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Poison, 3) }, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 3, statusDuration: 3);

        private static ResolvedSkill Reckoning() =>
            new ResolvedSkill("ashen_reckoning", "Ashen Reckoning", "", "hero", 1, SkillEffect.Reclaim,
                SkillTargeting.SingleEnemy, 10, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Vulnerable, statusMagnitude: 20, statusDuration: 1,
                requiresStatus: StatusEffectType.Poison, detonationPercent: 150,
                detonationSplit: new[] { DamageType.Poison, DamageType.Fire });

        // AN ORDINARY NATURE HIT, shaped like the existing Root Strike/mud_burst
        // family -- a fixed Nature packet with no status of its own, so its
        // only interaction with an existing Poison is the ordinary 100%
        // detonation every Nature/Poison hit already carries (D2).
        private static ResolvedSkill NatureHit() =>
            new ResolvedSkill("root_strike_test", "Root Strike", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 6, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Nature, 10) }, SpellPresentation.None, 0);

        // Casts ONE skill against a fresh, undefended dummy that already
        // carries the given Poison (none, if magnitude is 0), and reports the
        // damage landed and the Poison left standing afterward.
        private static (int damage, int poisonMagnitude, int poisonTurns) CastAgainstPoisoned(
            ResolvedSkill skill, int poisonMagnitude, int poisonTurns)
        {
            var caster = Caster(LowAnchorAttack);
            var dummy = Dummy(speed: 1);
            dummy.PhysicalDefense = 0;
            dummy.MagicalDefense = 0;
            if (poisonMagnitude > 0)
            {
                dummy.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, poisonMagnitude, poisonTurns));
            }

            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { skill }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { caster }, new[] { dummy }),
                new List<PlayerKit> { kit }, null, new SeededRandom(11))
            {
                DamageVarianceRange = 0f,
            };

            int before = dummy.CurrentHealth;
            session.CastSkill(0, dummy);
            int dealt = before - dummy.CurrentHealth;

            var poison = dummy.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Poison);
            return (dealt, poison?.Magnitude ?? 0, poison?.TurnsRemaining ?? 0);
        }

        // MEASURED 2026-09-20, against an undefended target, variance off. Each
        // line starts from a fresh Bite cast (packet 3, leaves Poison 3/3) and
        // asks what the SECOND cast is worth against exactly that Poison.
        //
        //   line                    2nd cast          damage    total (2 casts)   mana (2 casts)
        //   Bite / Bite             Bite               12         15                14
        //   Bite / Reckoning        Ashen Reckoning    14         17                17
        //   Bite / Root Strike      ordinary Nature    19         22                13
        //
        // (2nd-cast damage: Bite = packet 3 + detonation 9 = 12; Reckoning =
        // (3x3=9) marked up 150% = 13.5 -> 14, no packet of its own; Root
        // Strike = packet 10 + ordinary 100% detonation 9 = 19.)
        //
        // THE FINDING, reported rather than tuned: on a bare stack (one 3/3
        // Poison), Ashen Reckoning is worth MORE than a Bite recast (14 vs 12)
        // for three more mana, and an ordinary Nature-typed hit that happens
        // to detonate the same pile deals more still (19) for one LESS mana
        // than Reckoning -- because Root Strike's own packet (10) is bigger
        // than either Poison-shaped alternative's. That is not a flaw in
        // Reckoning's arithmetic: the premium (150%) is the only lever it
        // has over an ordinary detonation, and 50% of a single 3/3 stack's
        // worth (9) is 4.5, which does not close a 10-point packet gap on its
        // own. Reckoning's case is the STACKED pile Bite alone cannot reach in
        // one cast (three Bites deposit three independent 3/3 instances a
        // single Reckoning detonates AT ONCE for 150% of their SUM), and the
        // Vulnerable it leaves behind, neither of which this single-stack
        // comparison exercises. Left at the prototype 150; the owner's call
        // is whether the premium should rise to make a one-stack Reckoning
        // competitive with a bigger fixed packet on its own, or whether its
        // case is deliberately the multi-stack one.
        [Test]
        public void AshenReckoningOnASingleBiteStack_BeatsARecastButNotAFreshNaturePacket()
        {
            var freshBite = CastAgainstPoisoned(Bite(), poisonMagnitude: 0, poisonTurns: 0);
            Assert.AreEqual(3, freshBite.damage, "the bare packet, nothing to detonate yet");
            Assert.AreEqual(3, freshBite.poisonMagnitude);
            Assert.AreEqual(3, freshBite.poisonTurns);

            var recast = CastAgainstPoisoned(Bite(), freshBite.poisonMagnitude, freshBite.poisonTurns);
            Assert.AreEqual(12, recast.damage, "packet 3 + the old stack's worth (3x3=9)");

            var reckoning = CastAgainstPoisoned(Reckoning(), freshBite.poisonMagnitude, freshBite.poisonTurns);
            Assert.AreEqual(14, reckoning.damage, "150% of the same 9 -- 13.5 rounds up to 14 -- no packet of its own");

            var natureHit = CastAgainstPoisoned(NatureHit(), freshBite.poisonMagnitude, freshBite.poisonTurns);
            Assert.AreEqual(19, natureHit.damage, "packet 10 + the ordinary 100% detonation of the same 9");

            Assert.Greater(reckoning.damage, recast.damage, "the premium beats a plain recast on the same stack");
            Assert.Less(reckoning.damage, natureHit.damage,
                "but not a fixed packet with no Poison shape of its own -- Reckoning's case is the multi-stack pile, not this one");
        }

        // ---- milestone B: Blackglass Spear vs Lightning Bolt --------------------

        private static ResolvedSkill BlackglassSpear() =>
            new ResolvedSkill("blackglass_spear", "Blackglass Spear", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 12, 0, false, 0, 0, ignoresDefense: true,
                new[] { new DamageInstance(DamageType.Void, 13) }, SpellPresentation.None, 0,
                healthCostPercent: 5);

        private static ResolvedSkill LightningBolt() =>
            new ResolvedSkill("lightning_bolt", "Lightning Bolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 11, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Lightning, 10) }, SpellPresentation.None, 0);

        // Casts one skill against a dummy carrying the given broad defense
        // (physical/magical), affinity left Neutral -- both compared spells
        // are non-physical and neither Void nor Lightning is either enemy's
        // authored weakness/resistance (enemies.json: rust_knight is
        // Arcane-weak/Physical-resistant, gloom_moth is Fire-weak/Ice-
        // resistant), so Neutral is the honest reading of what these two
        // packets actually meet on either body.
        private static int LandedAgainstDefended(ResolvedSkill skill, int physicalDefense, int magicalDefense)
        {
            var caster = Caster(LowAnchorAttack);
            var dummy = Dummy(speed: 1);
            dummy.PhysicalDefense = physicalDefense;
            dummy.MagicalDefense = magicalDefense;

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

        // MEASURED 2026-09-20, against the two named enemies' own authored
        // broad defense (enemies.json), variance off.
        //
        //   target                     phys def   mag def   Blackglass (Void 13)   Lightning Bolt (Ltng 10)
        //   rust_knight (defended)     35         5         13                     9
        //   gloom_moth (fragile)       5          10        13                     9
        //
        // THE FINDING: rust_knight's own "high defence" is PHYSICAL (35);
        // its MagicalDefense is a modest 5, and both compared spells are
        // already non-physical, so ignoresDefense buys Blackglass Spear only
        // a small edge here (13 vs 9) rather than a dramatic one -- the
        // bypass matters most against a MAGICALLY armoured body, which
        // neither of the owner's two named enemies actually is. gloom_moth's
        // bigger MagicalDefense (10 against rust_knight's 5) reads as the
        // SAME landed 9 for Lightning Bolt -- CombatMath.AfterResistance's
        // integer division floors 10x100/105 (9.52) and 10x100/110 (9.09) to
        // the same 9, a rounding coincidence at this particular packet size,
        // not a claim that the two bodies resist equally. What "fragile"
        // actually changes is time-to-kill against gloom_moth's low 45 max
        // health, which this harness does not model (that is
        // `tools/bot.ps1`'s half of the pair). Reported rather than tuned:
        // the owner's own account of "a defended enemy" may want a body with
        // real MagicalDefense (there is none in the shipped roster above
        // single digits except ember_hound/beetle's low tens) for this
        // comparison to say what the brief intends.
        [Test]
        public void BlackglassSpearVsLightningBolt_OnTheTwoNamedEnemies()
        {
            Assert.AreEqual(13, LandedAgainstDefended(BlackglassSpear(), physicalDefense: 35, magicalDefense: 5),
                "rust_knight: ignoresDefense means its 5 MagicalDefense never applies");
            Assert.AreEqual(9, LandedAgainstDefended(LightningBolt(), physicalDefense: 35, magicalDefense: 5),
                "rust_knight: Lightning Bolt still pays the 5 MagicalDefense, softened by the resistance curve");

            Assert.AreEqual(13, LandedAgainstDefended(BlackglassSpear(), physicalDefense: 5, magicalDefense: 10),
                "gloom_moth: the same 13, defense bypassed either way");
            Assert.AreEqual(9, LandedAgainstDefended(LightningBolt(), physicalDefense: 5, magicalDefense: 10),
                "gloom_moth: reads the same 9 as rust_knight here -- integer division floors both, see this test's own header");
        }
    }
}
