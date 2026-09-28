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

        // 18 + spell attack, the shape prism_ward already uses, so the two
        // move together as a caster grows instead of crossing over somewhere
        // in the middle of a run.
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

        // Against an undefended target with variance off.
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

        // Hero speed 10, one enemy at speed 20, read off
        // CombatEncounter.UpcomingTurns over a 30-turn forecast window.
        //
        //   chill on the enemy   enemy turns in 30   actions denied
        //   none                 17                  --
        //   one at 25%           16                  1
        //   two at 25%           15                  2
        //   three at 25%         12                  5
        //
        // THE FINDING: a 25% Chilled does NOT deny 25% of an enemy's
        // actions. It denies about 6%
        // of them. The badge says -25% Speed and that is true, but SpeedScale's
        // charge curve is deliberately sub-linear with a hard ceiling (its own
        // header records why), so a quarter off the Speed number is much less
        // than a quarter off how often the actor acts. The third stack is where
        // it starts to bite, which is an argument FOR the stacking model rather
        // than against the number.
        //
        // Not tuned. 25% is the prototype figure and the measurement says it
        // is weak rather than wrong; whether the percent should move or the
        // expectation should is a design call, recorded in section 5.
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

        // Against an undefended target, variance off. Each
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
        // comparison exercises. Left at the prototype 150; the open call
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

        // Against the two named enemies' own authored broad defense
        // (enemies.json), variance off.
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
        // neither of the two named enemies actually is. gloom_moth's
        // bigger MagicalDefense (10 against rust_knight's 5) reads as the
        // SAME landed 9 for Lightning Bolt -- CombatMath.AfterResistance's
        // integer division floors 10x100/105 (9.52) and 10x100/110 (9.09) to
        // the same 9, a rounding coincidence at this particular packet size,
        // not a claim that the two bodies resist equally. What "fragile"
        // actually changes is time-to-kill against gloom_moth's low 45 max
        // health, which this harness does not model (that is
        // `tools/bot.ps1`'s half of the pair). Reported rather than tuned:
        // "a defended enemy" may want a body with real MagicalDefense (there
        // is none in the shipped roster above single digits except
        // ember_hound/beetle's low tens) for this comparison to say what it
        // intends.
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

        // ---- milestone C: initiative displacement, measured ------------------
        //
        // THE SAME INSTRUMENT MILESTONE A USED FOR THE CHILL -- enemy actions
        // allowed inside a 30-turn forecast, read off CombatEncounter.
        // UpcomingTurns -- so Gale Scythe's delay and Winter's Rebuke's Chill
        // can be compared as two answers to one question rather than as two
        // numbers that happen to be nearby.

        private static int EnemyTurnsAfterSweeps(int window, int casts)
        {
            // THE REBUKE'S OWN BOARD, deliberately: hero speed 10, one enemy
            // at speed 20. Reusing it is what makes the two rows comparable.
            var hero = Caster(LowAnchorAttack);
            var foe = Dummy(speed: 20);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var session = new FightSession(encounter, null, null, new SeededRandom(5));
            session.Begin();

            for (int i = 0; i < casts; i++) encounter.PushBack(foe, 1);

            return encounter.UpcomingTurns(window).Count(c => !c.IsPlayerSide);
        }

        private static int EnemyTurnsOnAMatchedFieldAfterOneSweep(int window, bool sweep)
        {
            // AND AN EVENLY MATCHED FIELD, which is the board Gale Scythe is
            // actually for: a full party, two enemies, nobody faster than
            // anybody, and one cast delaying both enemies at once. Section
            // 5's middle band in miniature.
            var hero = new CombatantState("Hero", true, 500, 200, 40, 10);
            var second = new CombatantState("Second", true, 500, 50, 20, 10);
            var third = new CombatantState("Third", true, 500, 50, 20, 10);
            var foeA = new CombatantState("FoeA", false, 100000, 10, 5, 10);
            var foeB = new CombatantState("FoeB", false, 100000, 10, 5, 10);
            var encounter = new CombatEncounter(new[] { hero, second, third }, new[] { foeA, foeB });
            var session = new FightSession(encounter, null, null, new SeededRandom(5));
            session.Begin();

            if (sweep) encounter.PushBackAll(new[] { foeA, foeB }, 1);

            return encounter.UpcomingTurns(window).Count(c => !c.IsPlayerSide);
        }

        //   board                              no cast  1 cast  2  3
        //   hero 10 vs one enemy 20            17       17      17 16
        //   hero 10 vs two enemies at 10       12       10      -- --
        //
        // Set beside milestone A's Chill on the identical first board
        // (17 / 16 / 15 / 12), the two control spells turn out to be
        // complements rather than substitutes, and the reason is what each one
        // moves. A Chill lowers the RATE, so it keeps paying for its whole
        // duration and pays most against a fast actor. A delay is a ONE-OFF
        // charge nudge, so a faster enemy earns it straight back before the
        // window is out -- three Gale Scythes on a speed-20 enemy deny a single
        // action, where one Rebuke denies one.
        //
        // What Gale Scythe has instead is BREADTH: on an evenly matched field
        // it denies about one action per enemy it hits, and it hits everybody,
        // so the two-enemy row is 2 denied for one cast at 11 mana. Against
        // three it would be three.
        //
        // Not tuned: as written, the spell is a crowd tool and reads on the
        // card like a tempo tool. The
        // honest one-line description of what it buys is "one action off each
        // enemy you are keeping pace with", not "each one loses a place".
        [Test]
        public void GaleScythesDelayIsBreadthRatherThanDepth()
        {
            Assert.AreEqual(17, EnemyTurnsAfterSweeps(30, 0), "the undelayed baseline, the Rebuke's own board");
            Assert.AreEqual(17, EnemyTurnsAfterSweeps(30, 1), "one sweep denies a fast enemy nothing at all");
            Assert.AreEqual(17, EnemyTurnsAfterSweeps(30, 2), "nor does a second");
            Assert.AreEqual(16, EnemyTurnsAfterSweeps(30, 3), "three of them deny one action");

            Assert.AreEqual(12, EnemyTurnsOnAMatchedFieldAfterOneSweep(30, sweep: false),
                "two evenly matched enemies, undelayed");
            Assert.AreEqual(10, EnemyTurnsOnAMatchedFieldAfterOneSweep(30, sweep: true),
                "one sweep across a matched field denies one action per enemy it hit");
        }

        // ---- Borrowed Moment against Gift: Haste ------------------------------

        // A MID-FIGHT CHARGE LANDSCAPE, built on the queue directly, and the
        // reason is the headline finding below: at the moment a fight OPENS,
        // every charge is seeded from Speed and so sits in the low tens
        // against a threshold of 100. A displacement moves an actor by a few
        // points there, and every actor still needs seventy to ninety ticks --
        // so nothing moves at all, for either spell. Measured on the opening
        // board, Borrowed Moment, Gift: Haste and a Headbutt all buy exactly
        // zero positions, which is a property of the SCHEDULER and not of any
        // of the three.
        //
        // Charges 80 / 80 / 70 / 55 against a caster mid-action, every rate
        // 1.0, is what the same field looks like two rounds in.
        private static (int position, int turns) AllyAdvancedBy(string treatment, int window)
        {
            var order = new TurnOrder<string>();
            order.AddCombatant("Caster", 99);
            order.SetSpeed("Caster", 10);
            order.Start();

            foreach (var entry in new[] { ("Fast1", 80), ("Fast2", 80), ("Mid", 70), ("Ally", 55) })
            {
                order.AddCombatant(entry.Item1, entry.Item2);
                order.SetSpeed(entry.Item1, 10);
            }

            if (treatment == "one") order.PullForward("Ally", 1);
            if (treatment == "two") order.PullForward("Ally", 2);
            if (treatment == "haste") order.PullToFront("Ally");

            return (order.ForecastPositionOf("Ally", window),
                order.Project(window).Count(a => a == "Ally"));
        }

        // Ally's forecast position and its turn COUNT inside a 30-turn
        // window:
        //
        //   treatment                 position  turns in 30
        //   nothing                   4         6
        //   Borrowed Moment, 1 slot   3         6
        //   Borrowed Moment, 2 slots  1         6
        //   Gift: Haste               1         6
        //
        // ACTIONS GAINED PER MANA IS ZERO, and that is the contract working
        // rather than the spell failing (plan 1.9 rule 3): an advance buys a
        // POSITION, never a turn. Six turns before, six turns after, at 8 mana
        // a cast. The number section 5 asked for is therefore 0.00 actions per
        // mana, and 0.375 POSITIONS per mana -- which is the honest unit and
        // the one the tooltip already uses.
        //
        // AGAINST GIFT: HASTE the two land in the same place here, and the
        // second slot is what gets them there: slot 1 clears Mid's 70, slot 2
        // clears the 80 level, which holds two combatants and so is worth two
        // positions at once. Gift: Haste reaches the same spot in one step and
        // costs no mana at all.
        //
        // THE CASE FOR THE SPELL IS THEREFORE NOT POWER, and should not be
        // priced as if it were. Gift: Haste is a Fragile Lamb talent paid for
        // in wool, on one character, one strand deep; Borrowed Moment is a
        // book any caster can hold. What the 8 mana buys is ACCESS, and on a
        // board with three or more charge levels above the target it buys
        // strictly less movement than the talent does. Left at the prototype
        // 8/2; the open call is whether a book that matches a talent's
        // effect at a mana price is the trade intended.
        [Test]
        public void BorrowedMomentBuysPositionsAndNeverActions()
        {
            Assert.AreEqual((4, 6), AllyAdvancedBy("none", 30), "the un-advanced baseline");
            Assert.AreEqual((3, 6), AllyAdvancedBy("one", 30), "one slot clears one charge level");
            Assert.AreEqual((1, 6), AllyAdvancedBy("two", 30),
                "two slots clear a level holding two combatants, so the second slot is worth two positions");
            Assert.AreEqual((1, 6), AllyAdvancedBy("haste", 30),
                "Gift: Haste reaches the same place in one step and costs no mana");
        }

        [Test]
        public void AtTheOpeningOfAFightNoDisplacementMovesAnybody()
        {
            // THE FINDING THAT MATTERS MORE THAN EITHER TABLE ABOVE, and it
            // is about the scheduler rather than about milestone C: on a board
            // whose charges are all freshly seeded from Speed, an advance, a
            // pull-to-front and a delay are all worth zero positions, because
            // a few points of charge is nothing against the seventy-odd ticks
            // every actor still owes.
            //
            // Gift: Haste has had this property since it shipped -- it is
            // included here precisely so the row cannot be read as something
            // this milestone introduced. What makes Gift: Haste feel immediate
            // in play is its talent (GiftAppliesImmediateTurn), not the pull.
            // A FRESH BOARD PER TREATMENT, because a displacement is a write:
            // the second treatment measured against the first one's board
            // would be measuring both.
            int PositionAfter(System.Action<CombatEncounter> treatment)
            {
                var party = new[]
                {
                    new CombatantState("Caster", true, 500, 200, 40, 14),
                    new CombatantState("Ally", true, 500, 50, 20, 10),
                    new CombatantState("Mid", true, 500, 50, 20, 12),
                };
                var foes = new[]
                {
                    new CombatantState("FoeA", false, 100000, 10, 5, 13),
                    new CombatantState("FoeB", false, 100000, 10, 5, 11),
                };
                var encounter = new CombatEncounter(party, foes);
                new FightSession(encounter, null, null, new SeededRandom(5)).Begin();

                treatment?.Invoke(encounter);
                return encounter.ForecastPositionOf(party[1]);
            }

            Assert.AreEqual(4, PositionAfter(null), "the ally opens fourth");
            Assert.AreEqual(4, PositionAfter(e => e.PullForward(e.PlayerParty[1], 2)),
                "a two-slot advance on an opening board moves nobody");
            Assert.AreEqual(4, PositionAfter(e => e.PullToFront(e.PlayerParty[1])),
                "and neither does a pull to the front -- this is the scheduler, not the spell");
        }

        // ---- MILESTONE D: enemy actions DENIED by Velvet Shackles -------------
        //
        // A DIFFERENT METRIC FROM MILESTONE C'S, and the difference is the
        // point. A delay or a Chill changes WHEN an enemy acts, so it is
        // measured against a forecast window. A root changes WHETHER it acts,
        // so it is measured by running its turns and counting the ones that
        // resolved into nothing. Section 5 calls this "enemy actions allowed
        // during a control rotation -- the metric the control loop lives or
        // dies on"; denied is that number read from the other end.
        //
        // THE KITS BELOW MIRROR enemies.json ROW FOR ROW in the two things
        // this measures -- which abilities are in the draw, at which weights,
        // and whether each is a physical move -- and nothing else. Numbers and
        // reaches are left out on purpose: the hero deals 1 into 100000 health,
        // because a fight that ends mid-window measures the window's length
        // rather than the spell. `bog_mud_burst`'s authored reachSlots [2,3] is
        // one of the omissions: against a one-hero party it would zero-weight
        // the cast for having nothing in reach, which is the front-rank rule
        // being measured instead of the root.
        private const int ShackleWindowTurns = 6;

        private static (int acted, int denied) EnemyTurns(
            int enemySpeed, float plainSwingWeight, IReadOnlyList<ResolvedSkill> abilities, bool shackled)
        {
            var hero = new CombatantState("Hero", true, 100000, 50, 1, 10);
            var foe = new CombatantState("Foe", false, 100000, 10, 5, enemySpeed);

            var pool = new List<EnemyAbility>();
            if (plainSwingWeight > 0f)
            {
                pool.Add(EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, plainSwingWeight));
            }
            foreach (var ability in abilities) pool.Add(EnemyAbility.Of(ability, 2f));

            var source = new ResolvedEnemy("foe", "Foe", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { null }, new List<EnemyKit> { new EnemyKit(source, false, pool) },
                new SeededRandom(101)) { DamageVarianceRange = 0f };

            // BEFORE Begin(), so the very first telegraph is already drawn
            // against a shackled pool -- applying it later would measure one
            // stale intent as well as the root.
            if (shackled) StatusEffects.Apply(foe.Statuses, StatusEffectType.Rooted, 0, 99);
            session.Begin();

            int acted = 0;
            int denied = 0;
            for (int guard = 0; guard < 200 && acted + denied < ShackleWindowTurns; guard++)
            {
                session.ExecuteAttack(foe);
                foreach (var beat in session.DrainBeats())
                {
                    if (!beat.IsAction || beat.Actor.IsPlayerSide) continue;
                    if (beat.Messages.Any(m => m.Contains("rooted"))) denied++;
                    else acted++;
                }
            }

            return (acted, denied);
        }

        // Enemy turns that resolved into an action, out of six, with and
        // without Rooted:
        //
        //   enemy          kit                                no root   shackled
        //   crystal_bat    plain swing only                    6 / 6     0 / 6
        //   golem          boulder_slam only, attackWeight 0   6 / 6     0 / 6
        //   bog_witch      swing (w3) + bog_mud_burst (w2)     6 / 6     6 / 6
        //
        // FINDING, REPORTED RATHER THAN TUNED: speed is not the axis, kit
        // composition is. Velvet Shackles denies a whole turn only from an
        // enemy with no non-physical option -- eleven of the sixteen rows in
        // enemies.json (every one with no abilities list at all) plus golem,
        // whose single ability is physical and whose attackWeight is 0.
        // Against bog_witch it denies nothing: the witch simply casts every
        // turn instead of mixing in swings, so what the 9 mana buys there is a
        // DOWNGRADE of the turn rather than the loss of it. The same holds for
        // forest_warden, which keeps `roar`, and for treant, which keeps
        // `spore_cloud`; beetle keeps `shell_up`, which is a heal and so the
        // one case where the substitute may serve the monster better than what
        // it replaced.
        [Test]
        public void VelvetShacklesDeniesAWholeTurnOnlyFromAnEnemyWithNoCast()
        {
            var boulderSlam = new ResolvedSkill("boulder_slam", "Boulder Slam", "", "golem", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 10, false,
                null, SpellPresentation.None, 0, physicalMove: true);
            var bogMudBurst = new ResolvedSkill("bog_mud_burst", "Bog Mud Burst", "", "bog_witch", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 10, false,
                null, SpellPresentation.None, 0, physicalMove: false);

            var none = new List<ResolvedSkill>();

            Assert.AreEqual((6, 0), EnemyTurns(15, 1f, none, shackled: false),
                "crystal_bat baseline: every turn is a swing");
            Assert.AreEqual((0, 6), EnemyTurns(15, 1f, none, shackled: true),
                "and a swing is all it has, so a root takes every one of them");

            Assert.AreEqual((6, 0), EnemyTurns(3, 0f, new List<ResolvedSkill> { boulderSlam }, shackled: false),
                "golem baseline: attackWeight 0, so the slam is its whole turn");
            Assert.AreEqual((0, 6), EnemyTurns(3, 0f, new List<ResolvedSkill> { boulderSlam }, shackled: true),
                "a thrown boulder is a physical move, so the golem has nothing left at all");

            Assert.AreEqual((6, 0), EnemyTurns(8, 3f, new List<ResolvedSkill> { bogMudBurst }, shackled: false),
                "bog_witch baseline");
            Assert.AreEqual((6, 0), EnemyTurns(8, 3f, new List<ResolvedSkill> { bogMudBurst }, shackled: true),
                "a caster loses its swing and casts instead -- nine mana bought a downgrade, not a turn");
        }

        // THE SPELL AS AUTHORED buys exactly two of those turns, and the
        // measurement above is per-turn, so the figure for the card is two
        // actions denied against a physical-only enemy and none against a
        // caster, for 9 mana.
        [Test]
        public void TwoAuthoredTurnsAreTwoDeniedActionsAgainstAPhysicalOnlyEnemy()
        {
            // SPEED 9 AGAINST THE HERO'S 10, the one-reply-per-action cadence
            // RootedStatusTests established. The measurement above varies the
            // enemy's authored speed because it counts a RATE; this one counts
            // the two turns the spell actually buys, and a faster enemy would
            // take both of them inside the player's first action and make the
            // "and then it is free again" half unreadable.
            var hero = new CombatantState("Hero", true, 100000, 50, 1, 10);
            var foe = new CombatantState("Foe", false, 100000, 10, 5, 9);
            var source = new ResolvedEnemy("foe", "Foe", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { null }, new List<EnemyKit> { new EnemyKit(source, false) },
                new SeededRandom(103)) { DamageVarianceRange = 0f };
            session.Begin();
            StatusEffects.Apply(foe.Statuses, StatusEffectType.Rooted, 0, 2);

            int acted = 0;
            int denied = 0;
            for (int turn = 0; turn < 4; turn++)
            {
                session.ExecuteAttack(foe);
                foreach (var beat in session.DrainBeats())
                {
                    if (!beat.IsAction || beat.Actor.IsPlayerSide) continue;
                    if (beat.Messages.Any(m => m.Contains("rooted"))) denied++;
                    else acted++;
                }
            }

            Assert.AreEqual(2, denied, "two authored turns, two actions denied");
            Assert.AreEqual(2, acted, "and the two turns after them are the enemy's own again");
        }

        // ---- milestone E: Censer of Embers' stack vs direct damage -------------

        private static ResolvedSkill Censer() =>
            new ResolvedSkill("censer_of_embers", "Censer of Embers", "", "sheep", 1,
                SkillEffect.Afflict, SkillTargeting.SingleEnemy, 8, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Burn, statusMagnitude: 4, statusDuration: 3,
                cooldownTurns: 2, physicalMove: false);

        // Against an undefended target with variance off.
        // Cooldown 2 means "turn one then turn three" (SkillCooldownTests'
        // own words), so three casts take FIVE of the caster's own turns
        // (1, 3, 5), with the caster's non-Physical Filler spending the two
        // between them -- ExecuteAttack would deal its own damage to the same
        // target this is measuring, so it is not the honest filler here.
        //
        //   spell                mana   total damage   per mana
        //   Censer x3 (stacked)  24     36             1.5
        //   lightning_bolt x3    33     30             0.91
        //
        // THE FINDING, reported rather than tuned: three stacked Burns beat
        // three Lightning Bolts by a wide margin per mana (1.5 against 0.91)
        // AND in absolute total (36 against 30) for less mana spent (24
        // against 33), even though the cooldown SPACES the casts out to one
        // every other turn rather than letting them overlap freely. Each
        // instance still ticks three times at its own 4-magnitude snapshot
        // regardless of how the other two are timed (D3's independence), so
        // the total is exactly 3 instances x 4 magnitude x 3 ticks = 36 no
        // matter the cadence the cooldown imposes. A DoT that stacks without
        // a ladder or a cap earns exactly the property D3 itself names: "an
        // applier adding one instance per turn plateaus... it does not run
        // away" -- but three instances across five turns is well short of
        // the plateau, and the comparison this pair asks for is a snapshot at
        // three, not the asymptote. Left at the prototype figure.
        [Test]
        public void CensersThreeCastStack_OutpacesThreeLightningBoltsPerMana()
        {
            var caster = new CombatantState("Caster", true, 500, 200, LowAnchorAttack, 10);
            var dummy = new CombatantState("Dummy", false, 100000, 10, 5, 9);
            dummy.PhysicalDefense = 0;
            dummy.MagicalDefense = 0;

            var kit = new PlayerKit("sheep", CharacterRole.Support, new[] { Censer(), Filler() }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { caster }, new[] { dummy }),
                new List<PlayerKit> { kit }, null, new SeededRandom(17)) { DamageVarianceRange = 0f };
            session.Begin();

            int before = dummy.CurrentHealth;

            // THREE CASTS ON TURNS 1, 3, 5 -- the Filler spends turns 2 and 4
            // while the cooldown is still up.
            Assert.IsTrue(session.CastSkill(0, dummy), "cast 0 must be accepted");
            session.DrainBeats();
            Assert.IsTrue(session.CastSkill(1, caster));
            session.DrainBeats();
            Assert.IsTrue(session.CastSkill(0, dummy), "cast 1 must be accepted once the cooldown clears");
            session.DrainBeats();
            Assert.IsTrue(session.CastSkill(1, caster));
            session.DrainBeats();
            Assert.IsTrue(session.CastSkill(0, dummy), "cast 2 must be accepted");
            session.DrainBeats();

            // AND ENOUGH FURTHER FILLER TURNS for every live instance to
            // finish its three ticks.
            for (int i = 0; i < 10 && dummy.Statuses.Any(s => s.Type == StatusEffectType.Burn); i++)
            {
                session.CastSkill(1, caster);
                session.DrainBeats();
            }

            int total = before - dummy.CurrentHealth;
            Assert.AreEqual(36, total, "three independent 4-magnitude, 3-tick Burns: 3 x 4 x 3");

            int lightningTotal = 3 * LandedDamage(Packet("lightning_bolt", DamageType.Lightning, 10, 11), LowAnchorAttack);
            Assert.AreEqual(30, lightningTotal, "fixture check: three Lightning Bolts at 10 each");

            Assert.Greater(total / 24f, lightningTotal / 33f,
                "the stack must beat direct damage per mana for this finding to be worth recording");
        }

        // ---- milestone E: Thorn Tithe vs direct damage, fast vs slow -----------

        private static ResolvedSkill ThornTithe() =>
            new ResolvedSkill("thorn_tithe", "Thorn Tithe", "", "sheep", 1,
                SkillEffect.Afflict, SkillTargeting.SingleEnemy, 10, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Thorned, statusMagnitude: 5, statusDuration: 3,
                cooldownTurns: 3, physicalMove: false);

        // A NON-PHYSICAL, SELF-TARGETED FILLER for the caster's remaining
        // turns -- ExecuteAttack would deal its OWN damage to the same
        // target the tithe is measuring, and re-casting Thorn Tithe itself
        // is refused by its own cooldown (a refusal spends no turn at all,
        // per plan 1.1), which would leave the loop unable to advance the
        // clock. This is the identical device ThornTitheTests' FillerHeal
        // uses, at kit index 1 so index 0 stays Thorn Tithe.
        private static ResolvedSkill Filler() =>
            new ResolvedSkill("filler_heal", "Filler", "", "sheep", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0, physicalMove: false);

        // Runs the tithe to its natural end (three affected turns) against a
        // target that spends every one of those turns on a PLAIN SWING --
        // the physical move the retaliation is watching for -- and reports
        // the total damage taken.
        private static int TotalDamageOverThreeAffectedTurns(int targetSpeed)
        {
            var caster = new CombatantState("Caster", true, 500, 200, LowAnchorAttack, 10);
            var target = new CombatantState("Target", false, 100000, 10, 5, targetSpeed);
            target.PhysicalDefense = 0;
            target.MagicalDefense = 0;

            var kit = new PlayerKit("sheep", CharacterRole.Support, new[] { ThornTithe(), Filler() }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { caster }, new[] { target }),
                new List<PlayerKit> { kit }, null, new SeededRandom(19)) { DamageVarianceRange = 0f };
            session.Begin();

            int before = target.CurrentHealth;
            Assert.IsTrue(session.CastSkill(0, target));
            session.DrainBeats();

            // FURTHER TURNS, THE FILLER ONLY (the caster has nothing else to
            // spend that does not touch the target), until Thorned falls off
            // -- enough for both remaining opening ticks and every
            // physical-move retaliation the three affected turns owe. The
            // TARGET's own turns are a plain swing every time (no abilities
            // authored), which is the physical move the hook is watching for.
            for (int i = 0; i < 20 && target.Statuses.Any(s => s.Type == StatusEffectType.Thorned); i++)
            {
                session.CastSkill(1, caster);
                session.DrainBeats();
            }

            return before - target.CurrentHealth;
        }

        // Against an undefended target, variance off, 10 mana, one cast.
        // `crystal_bat` (speed 15) and `golem`/`treant`
        // (speed 3) are section 5's own named fast/slow ends; this harness
        // varies only the axis that matters to a RETALIATION -- how often the
        // target's own physical move recurs relative to the tithe's fixed
        // three-affected-turn window, not how often it recurs relative to the
        // CASTER (Thorn Tithe's opening tick and the direct-damage comparison
        // both ride the caster's own cast cadence identically; only the
        // retaliation count depends on the target's own turn rate).
        //
        //   target speed   total damage   direct-damage comparison (Blackglass Spear, 10 mana, one packet)
        //   15 (fast)      30             9-14 depending on the target's defence (BlackglassSpearVsLightningBolt)
        //   3  (slow)      30             same
        //
        // THE FINDING, reported rather than tuned: the total is IDENTICAL at
        // both ends (30 = three opening ticks plus three retaliations, five
        // each -- six events of five, not a function of speed at all in this
        // all-physical fixture, since every one of the target's own turns
        // both opens with a tick AND resolves a physical swing). One 10-mana
        // cast of Thorn Tithe therefore already clears a single Blackglass
        // Spear packet at either end, for a spell that also denies nothing
        // and costs no action beyond the first. Speed changes WHEN the
        // target's turns land relative to the caster's, not HOW MANY of the
        // three affected turns it gets to spend on a physical move here --
        // the same axis milestone D already found for Velvet Shackles (kit
        // composition, not speed, decides a root's value). Thorn Tithe's own
        // axis is narrower still: against a target with nothing physical to
        // do (a pure caster), the retaliation never fires at all and the
        // total falls to 15 (three opening ticks only) -- recorded as the
        // real fast/slow-equivalent finding below.
        [Test]
        public void ThornTitheVsDirectDamage_OnFastAndSlowPhysicalTargets()
        {
            int fast = TotalDamageOverThreeAffectedTurns(targetSpeed: 15);
            int slow = TotalDamageOverThreeAffectedTurns(targetSpeed: 3);

            Assert.AreEqual(30, fast, "3 opening ticks + 3 retaliations, 5 each, against a target that always swings");
            Assert.AreEqual(30, slow, "identical -- both targets take a physical move every one of their own turns");
        }

        // THE REAL AXIS: a target with nothing physical in its kit never pays
        // the retaliation half at all, so the same 10-mana cast is worth a
        // third less against it than against a swinging target -- kit
        // composition, not speed, decides Thorn Tithe's value, exactly as
        // milestone D found for Velvet Shackles.
        [Test]
        public void ThornTitheAgainstAPureCaster_EarnsOnlyTheOpeningTicks()
        {
            var caster = new CombatantState("Caster", true, 500, 200, LowAnchorAttack, 10);
            var casterFoe = new CombatantState("CasterFoe", false, 100000, 50, 5, 9);
            casterFoe.PhysicalDefense = 0;
            casterFoe.MagicalDefense = 0;

            var cast = new ResolvedSkill("foe_cast", "Cackle", "", "foe", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0, physicalMove: false);
            var kit = new PlayerKit("sheep", CharacterRole.Support, new[] { ThornTithe(), Filler() }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { caster }, new[] { casterFoe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(
                    new ResolvedEnemy("foe", "foe", new StatBlock(), 0, 0, false,
                        DamageType.Physical, DamageType.Physical, 0), false,
                    new List<EnemyAbility> { EnemyAbility.Of(cast, 1_000_000f) }) },
                new SeededRandom(23)) { DamageVarianceRange = 0f };
            session.Begin();

            int before = casterFoe.CurrentHealth;
            Assert.IsTrue(session.CastSkill(0, casterFoe));
            session.DrainBeats();

            for (int i = 0; i < 20 && casterFoe.Statuses.Any(s => s.Type == StatusEffectType.Thorned); i++)
            {
                session.CastSkill(1, caster);
                session.DrainBeats();
            }

            int total = before - casterFoe.CurrentHealth;
            Assert.AreEqual(15, total, "three opening ticks only -- a caster with no physical move never retaliates");
        }
    }
}
