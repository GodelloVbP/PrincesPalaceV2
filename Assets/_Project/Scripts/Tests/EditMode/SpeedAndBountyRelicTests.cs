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
    // Ballerina's Slippers, the Tin-Foil Pipe, the Toothed Necklace, and the
    // Bounty Hunter Contract.
    //
    // TWO SHAPES SHARE THE UNDERLYING MACHINERY and are worth telling apart in
    // the reading of these tests: the Slippers accumulate across a whole fight
    // and never expire, the Pipe grants once and expires on its own next turn.
    // Same GrantSpeedPercent call, opposite durations, and the two relics would
    // be indistinguishable if only one of them were tested.
    public class SpeedAndBountyRelicTests
    {
        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            RelicEffect relic, int heroSpeed = 20, int heroHealth = 200)
        {
            var hero = new CombatantState("Shawn", true, heroHealth, 999, 20, 0, heroSpeed);
            var foe = new CombatantState("Dummy", false, 999999, 0, 1, 0, 1);

            var kit = new PlayerKit("hero", CharacterRole.Tank, null,
                new List<ResolvedRelic> { Relic(relic) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(),
                    0, 0, false, DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe);
        }

        // ---- ballerina's slippers -------------------------------------------------

        [Test]
        public void EachSwingMakesTheWearerFaster()
        {
            var (session, hero, foe) = Fight(RelicEffect.BallerinasSlippers, heroSpeed: 20);

            session.ExecuteAttack(foe);

            Assert.Greater(hero.Speed, 20, "the first swing granted nothing");
        }

        // 10% PER SWING, TO A CEILING OF 40%. Four swings should reach the cap
        // and a fifth should not push past it -- pinned as a ceiling rather than
        // as a running total, because a running total is exactly what a cap is
        // supposed to prevent.
        [Test]
        public void TheSlippersCapAtFortyPercent()
        {
            var (session, hero, foe) = Fight(RelicEffect.BallerinasSlippers, heroSpeed: 20);

            for (int i = 0; i < 8; i++) session.ExecuteAttack(foe);

            Assert.AreEqual(28, hero.Speed, "20 base + 40% = 28, and it must not climb past that");
        }

        // NEVER EXPIRES ON ITS OWN. The whole point is warming up across a long
        // fight -- a Slippers bonus that faded between turns would never be
        // distinguishable from the Pipe.
        [Test]
        public void TheSlippersSurviveATurnWithNoSwing()
        {
            var (session, hero, foe) = Fight(RelicEffect.BallerinasSlippers, heroSpeed: 20);

            session.ExecuteAttack(foe);
            int afterSwing = hero.Speed;

            // A turn passes with no swing at all.
            session.CastSkill(new ResolvedSkill("noop", "Noop", "", "hero", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0), hero);

            Assert.AreEqual(afterSwing, hero.Speed, "the bonus decayed without a swing having happened");
        }

        // ---- the tin-foil pipe -----------------------------------------------------

        // WHY THESE GO THROUGH THE FOR-TEST SEAM rather than a full cast: a
        // one-turn buff's entire observable life sits inside one synchronous
        // CastSkill call. By the time that call returns, AdvanceAfterAction has
        // already run GrantTurnStart for the player's own next turn, which is
        // the exact tick that revokes a turns: 1 buff -- there is no
        // externally observable moment between granting and that tick,
        // confirmed by instrumenting the resolve loop directly. See
        // FightSession.SpeedBuffs' own seam comment.
        [Test]
        public void ACastMakesTheWearerFasterByTwentyPercent()
        {
            var (session, hero, _) = Fight(RelicEffect.TinFoilPipe, heroSpeed: 20);

            bool granted = session.GrantSpeedPercentForTest(hero, RelicEffect.TinFoilPipe,
                FightTuning.PipePercent, FightTuning.PipeTurns);

            Assert.IsTrue(granted, "the pipe granted nothing");
            Assert.AreEqual(24, hero.Speed, "20 base + 20% = 24");
        }

        // EXPIRES ON THE NEXT TURN, unlike the Slippers. Granted, ticked once,
        // and the speed should be back to baseline -- the same arithmetic a
        // real turn start runs, driven directly.
        [Test]
        public void ThePipeFadesAfterOneTurn()
        {
            var (session, hero, _) = Fight(RelicEffect.TinFoilPipe, heroSpeed: 20);

            session.GrantSpeedPercentForTest(hero, RelicEffect.TinFoilPipe,
                FightTuning.PipePercent, FightTuning.PipeTurns);
            Assert.AreEqual(24, hero.Speed, "the grant should have applied");

            session.TickSpeedBuffsForTest(hero);

            Assert.AreEqual(20, hero.Speed, "the pipe's speed survived past its own next turn");
        }

        // THE WIRING, not just the arithmetic: casting with the Pipe equipped
        // has to actually reach GrantSpeedPercent. Checked through the full
        // API for the one thing that IS observable there -- SpeedBonusFrom
        // reads back at 0 after the round trip regardless of whether the
        // relic fired, so this checks the one relic-carrying case against an
        // identical relic-free case and requires them to differ in TOTAL
        // enemy turns survived over several casts, which the buff's effect on
        // charge accumulation should shift even though no single grant is
        // directly visible.
        [Test]
        public void TheWiringActuallyReachesTheGrantWhenCasting()
        {
            int EnemyActionsOver(RelicEffect? relic, int rounds)
            {
                var hero = new CombatantState("Shawn", true, 999999, 999, 5, 0, 10);
                var foe = new CombatantState("Dummy", false, 999999, 0, 10, 0, 11);

                var bolt = new ResolvedSkill("bolt", "Bolt", "", "hero", 1,
                    SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, true,
                    null, SpellPresentation.None, 0);

                var relics = relic.HasValue
                    ? new List<ResolvedRelic> { Relic(relic.Value) }
                    : new List<ResolvedRelic>();

                var kit = new PlayerKit("hero", CharacterRole.Tank,
                    new List<ResolvedSkill> { bolt }, relics, null);

                var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                    new List<PlayerKit> { kit },
                    new List<EnemyKit> { new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(),
                        0, 0, false, DamageType.Physical, DamageType.Physical, 0), false) },
                    new SeededRandom(5)) { DamageVarianceRange = 0f };
                session.Begin();

                int hits = 0;
                int before = hero.CurrentHealth;

                for (int i = 0; i < rounds; i++)
                {
                    session.CastSkill(bolt, foe);
                    int now = hero.CurrentHealth;
                    if (now < before) hits++;
                    before = now;
                }

                return hits;
            }

            int without = EnemyActionsOver(null, 12);
            int with = EnemyActionsOver(RelicEffect.TinFoilPipe, 12);

            Assert.Less(with, without,
                "casting with the Tin-Foil Pipe should win hero more of the immediate follow-up " +
                "races against a similarly-paced foe, landing fewer enemy hits over the same " +
                "number of casts. Equal counts means RelicsAfterCast never reached the grant.");
        }

        // ---- the toothed necklace ---------------------------------------------------

        // NOTHING AT FULL HEALTH. The relic is a response to being hurt, and at
        // full health there is nothing to respond to.
        [Test]
        public void AtFullHealthTheNecklaceGrantsNothing()
        {
            var (session, hero, foe) = Fight(RelicEffect.ToothedNecklace, heroSpeed: 20, heroHealth: 200);

            session.ExecuteAttack(foe);

            Assert.AreEqual(20, hero.Speed, "full health should carry no speed bonus");
        }

        // FULL VALUE AT A QUARTER HEALTH, and no further -- the floor is a
        // quarter rather than zero on purpose, so the bonus does not peak on
        // the turn the wearer is about to die.
        [Test]
        public void AtAQuarterHealthTheNecklaceReachesItsCeiling()
        {
            var (session, hero, foe) = Fight(RelicEffect.ToothedNecklace, heroSpeed: 20, heroHealth: 200);
            hero.CurrentHealth = 50; // exactly 25%

            session.ExecuteAttack(foe);

            Assert.AreEqual(24, hero.Speed, "20 base + 20% max speed bonus, fully reached at 25% health");
        }

        // BELOW THE FLOOR IS THE SAME AS AT IT. A wearer at 5% health is not
        // MORE dangerous than one at 25% -- the relic has a ceiling, not a
        // second cliff hiding under the first.
        [Test]
        public void BelowAQuarterHealthTheNecklaceDoesNotExceedItsCeiling()
        {
            var (session, hero, foe) = Fight(RelicEffect.ToothedNecklace, heroSpeed: 20, heroHealth: 200);
            hero.CurrentHealth = 5;

            session.ExecuteAttack(foe);

            Assert.AreEqual(24, hero.Speed, "below the floor should cap at the same value as the floor");
        }

        // THE RAMP MOVES BOTH WAYS. Healed back up, the speed the necklace gave
        // has to come back off -- a bonus that only ever grows would make
        // healing a liability nobody would choose.
        [Test]
        public void HealingBackUpGivesTheSpeedBonusBack()
        {
            var (session, hero, foe) = Fight(RelicEffect.ToothedNecklace, heroSpeed: 20, heroHealth: 200);
            hero.CurrentHealth = 50;
            session.ExecuteAttack(foe);
            Assert.AreEqual(24, hero.Speed, "the bonus should have applied while hurt");

            hero.CurrentHealth = 200;
            session.ExecuteAttack(foe);

            Assert.AreEqual(20, hero.Speed, "healed to full, the necklace's speed should be gone");
        }

        // The damage half: measured as a difference from an identical fight with
        // no relic, at the same wound, so the assertion survives a retune of the
        // underlying attack formula.
        [Test]
        public void TheNecklaceHitsHarderTheMoreHurtTheWearerIs()
        {
            var (withRelic, heroA, foeA) = Fight(RelicEffect.ToothedNecklace, heroSpeed: 20, heroHealth: 200);
            heroA.CurrentHealth = 50;
            int beforeA = foeA.CurrentHealth;
            withRelic.ExecuteAttack(foeA);
            int dealtWithRelic = beforeA - foeA.CurrentHealth;

            var (withoutRelic, heroB, foeB) = Fight(RelicEffect.None, heroSpeed: 20, heroHealth: 200);
            heroB.CurrentHealth = 50;
            int beforeB = foeB.CurrentHealth;
            withoutRelic.ExecuteAttack(foeB);
            int dealtWithout = beforeB - foeB.CurrentHealth;

            Assert.Greater(dealtWithRelic, dealtWithout,
                "at 25% health the necklace should add its full 30% damage bonus");
        }

        // ---- two speed relics on one character ---------------------------------------
        //
        // A REAL BUG, FOUND AFTER SHIPPING, not designed against from the
        // start. GrantSpeedPercent's own base subtracted only the CURRENT
        // relic's own Granted, which stops a relic compounding against
        // ITSELF on a second grant and does nothing to stop it compounding
        // against a DIFFERENT relic's grant that landed first: Slippers
        // +10% off 100 gave +10 (110), and the Pipe's +20% then read 110 as
        // its base and granted +22 instead of the +20 a true 20%-of-100
        // owes -- 132 total instead of the 114 the two relics together
        // should produce.
        //
        // NOT A CORNER CASE. SquadTrack.StartingRelics escalates past level
        // 25 with no cap to lift (RewardTrack.cs), so a character carrying
        // two relics at once is an ordinary mid-run state, not a
        // hypothetical.
        [Test]
        public void TwoSpeedRelicsBothMeasureAgainstTheSameTrueBase()
        {
            var hero = new CombatantState("Shawn", true, 999999, 999, 20, 0, 100);
            var foe = new CombatantState("Dummy", false, 999999, 0, 1, 0, 1);

            var relics = new List<ResolvedRelic>
            {
                Relic(RelicEffect.BallerinasSlippers),
                Relic(RelicEffect.TinFoilPipe),
            };
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, relics, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(),
                    0, 0, false, DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();

            session.GrantSpeedPercentForTest(hero, RelicEffect.BallerinasSlippers, 10, turns: 0);
            Assert.AreEqual(110, hero.Speed, "10% of the true base 100 is +10");

            session.GrantSpeedPercentForTest(hero, RelicEffect.TinFoilPipe, 20, turns: 1);

            Assert.AreEqual(130, hero.Speed,
                "the Pipe's 20% must read the TRUE base (100 -> +20), not the Slippers-inflated " +
                "110 (which would wrongly grant +22 and land on 132)");
        }

        // ---- the bounty hunter contract ---------------------------------------------

        [Test]
        public void KillingSomethingPaysWithTheContract()
        {
            var hero = new CombatantState("Shawn", true, 9999, 999, 999, 0, 20);
            var foe = new CombatantState("Weakling", false, 1, 0, 0, 0, 1);

            var enemy = new ResolvedEnemy("weakling", "Weakling", new StatBlock(), 30, 5, false,
                DamageType.Physical, DamageType.Physical, 0);

            var kit = new PlayerKit("hero", CharacterRole.Tank, null,
                new List<ResolvedRelic> { Relic(RelicEffect.BountyHunterContract) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(enemy, false) },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();

            Assert.AreEqual(0, session.BountyEarned, "nothing should be owed before anything has died");

            session.ExecuteAttack(foe);

            Assert.Greater(session.BountyEarned, 0, "the kill should have paid out");
        }

        // THE HALF THAT WAS MISSING, and the reason the two tests above passed
        // while the relic was worth nothing.
        //
        // Both of them assert on session.BountyEarned, which no production code
        // read: the contract banked gold onto the session, printed a line in the
        // combat log saying so, and the fight's Payout was computed from the
        // enemies' own CurrencyReward alone. A player wearing it collected
        // nothing. Exactly the shape CLAUDE.md's gotcha 5 warns about -- a test
        // that measures the thing it can reach rather than the thing that
        // matters -- and exactly the shape of AUDIT #42.
        //
        // Asserted against a bounty-less session rather than a literal, because
        // the base payout rides DifficultyCurve and a pinned number here would
        // be pinning the curve by accident.
        [Test]
        public void TheBountyReachesTheFightsPayout_NotJustTheSession()
        {
            int GoldFrom(bool wearingTheContract)
            {
                var hero = new CombatantState("Shawn", true, 9999, 999, 999, 0, 20);
                var foe = new CombatantState("Weakling", false, 1, 0, 0, 0, 1);

                var enemy = new ResolvedEnemy("weakling", "Weakling", new StatBlock(), 200, 5, false,
                    DamageType.Physical, DamageType.Fire, 0);

                var relics = wearingTheContract
                    ? new List<ResolvedRelic> { Relic(RelicEffect.BountyHunterContract) }
                    : new List<ResolvedRelic>();

                var kit = new PlayerKit("hero", CharacterRole.Tank, null, relics, null);

                var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                    new List<PlayerKit> { kit },
                    new List<EnemyKit> { new EnemyKit(enemy, false) },
                    new SeededRandom(5)) { DamageVarianceRange = 0f };
                session.Begin();
                session.ExecuteAttack(foe);

                Assert.IsTrue(session.Payout.HasValue, "the fight should have ended in a victory");
                return session.Payout.Value.Gold;
            }

            int without = GoldFrom(wearingTheContract: false);
            int with = GoldFrom(wearingTheContract: true);

            Assert.Greater(with, without,
                "the contract's gold never left the session - the payout the run actually banks " +
                "is identical with and without the relic");
        }

        [Test]
        public void ABiggerBountyPaysMoreThanASmallerOne()
        {
            int Payout(int expReward)
            {
                var hero = new CombatantState("Shawn", true, 9999, 999, 999, 0, 20);
                var foe = new CombatantState("Target", false, 1, 0, 0, 0, 1);

                var enemy = new ResolvedEnemy("target", "Target", new StatBlock(), expReward, 5, false,
                    DamageType.Physical, DamageType.Physical, 0);

                var kit = new PlayerKit("hero", CharacterRole.Tank, null,
                    new List<ResolvedRelic> { Relic(RelicEffect.BountyHunterContract) }, null);

                var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                    new List<PlayerKit> { kit },
                    new List<EnemyKit> { new EnemyKit(enemy, false) },
                    new SeededRandom(5)) { DamageVarianceRange = 0f };
                session.Begin();
                session.ExecuteAttack(foe);

                return session.BountyEarned;
            }

            Assert.Greater(Payout(200), Payout(10), "a tougher kill should pay more than a trivial one");
        }

        // ---- a sweep gets the same bonuses a single cast does -----------------------
        //
        // Found while wiring the necklace: PotencyBonus was only ever added on
        // the single-target and plain-swing paths, so a charged DamageAll spell
        // charged nothing. The gap would have repeated silently for every future
        // per-cast bonus that did not think to check the sweep.
        [Test]
        public void ADamageAllSpellStillGetsThePotencyBonus()
        {
            var hero = new CombatantState("Shawn", true, 999999, 999, 20, 0, 10);
            var foeA = new CombatantState("A", false, 999999, 0, 1, 0, 1);
            var foeB = new CombatantState("B", false, 999999, 0, 1, 0, 1);

            var sweep = new ResolvedSkill("sweep", "Sweep", "", "hero", 1,
                SkillEffect.DamageAll, SkillTargeting.AllEnemies, 0, 0, false, 0, 50, true,
                null, SpellPresentation.None, 0);

            var kit = new PlayerKit("hero", CharacterRole.Tank,
                new List<ResolvedSkill> { sweep },
                new List<ResolvedRelic> { Relic(RelicEffect.ChargingCrystal) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foeA, foeB }),
                new List<PlayerKit> { kit },
                new List<EnemyKit>
                {
                    new EnemyKit(new ResolvedEnemy("a", "A", new StatBlock(), 0, 0, false,
                        DamageType.Physical, DamageType.Physical, 0), false),
                    new EnemyKit(new ResolvedEnemy("b", "B", new StatBlock(), 0, 0, false,
                        DamageType.Physical, DamageType.Physical, 0), false),
                },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();

            int Sweep()
            {
                int before = foeA.CurrentHealth;
                session.CastSkill(sweep, foeA);
                return before - foeA.CurrentHealth;
            }

            int first = Sweep();
            Sweep();
            Sweep();
            int fourth = Sweep();

            Assert.Greater(fourth, first, "the fourth cast of an all-enemies spell should be the charged one");
        }


    }
}