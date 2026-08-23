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
    // Cooldowns: the rhythm a kit has instead of a wallet.
    //
    // THE NUMBER IS "TURNS UNTIL USABLE AGAIN", counted from the turn it was
    // cast on -- cast on turn 1 with a cooldown of 2 and it is back on turn 3.
    // That is the reading an author says out loud, and it is the one thing here
    // worth pinning hardest, because every other spelling of it is off by one
    // and off-by-one in a cooldown is invisible until somebody counts.
    public class SkillCooldownTests
    {
        private static ResolvedSkill Bolt(int cooldown) =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 20, ignoresDefense: true,
                null, SpellPresentation.None, 0, cooldownTurns: cooldown);

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            ResolvedSkill skill, RelicEffect? relic = null)
        {
            var hero = new CombatantState("Shawn", true, 999999, 999, 20, 0, 10);
            var foe = new CombatantState("Dummy", false, 999999, 0, 1, 0, 1);

            var relics = relic.HasValue
                ? new List<ResolvedRelic> { new ResolvedRelic("r", "r", "", relic.Value, 0) }
                : new List<ResolvedRelic>();

            var kit = new PlayerKit("hero", CharacterRole.Tank,
                new List<ResolvedSkill> { skill }, relics, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { new EnemyKit(new ResolvedEnemy("dummy", "Dummy", new StatBlock(),
                    0, 0, false, DamageType.Physical, DamageType.Physical, 0), false) },
                new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe);
        }

        // A TURN PASSES BY DOING SOMETHING ELSE. A refused cast is not a turn:
        // CastSkill returns false without spending anything, so a test that
        // called it in a loop would sit on turn one forever and conclude the
        // cooldown never expires. Swinging is what a player does while they
        // wait, and it is what makes the turn advance.
        private static void PassATurn(FightSession session, CombatantState foe) =>
            session.ExecuteAttack(foe);

        // ---- the count -----------------------------------------------------------

        [Test]
        public void ASkillWithNoCooldownIsCastableEveryTurn()
        {
            var bolt = Bolt(0);
            var (session, _, foe) = Fight(bolt);

            for (int turn = 1; turn <= 5; turn++)
            {
                Assert.IsTrue(session.CastSkill(bolt, foe), $"turn {turn} refused a cooldown-free skill");
            }
        }

        // TURN 1, THEN TURN 3. The headline reading, walked as the sequence a
        // player actually experiences: cast, swing, cast.
        [Test]
        public void ACooldownOfTwoMeansTurnOneThenTurnThree()
        {
            var bolt = Bolt(2);
            var (session, _, foe) = Fight(bolt);

            Assert.IsTrue(session.CastSkill(bolt, foe), "turn 1 should be castable");

            Assert.IsFalse(session.CastSkill(bolt, foe), "turn 2 is the wait");
            PassATurn(session, foe);

            Assert.IsTrue(session.CastSkill(bolt, foe), "turn 3 should be back");
        }

        // A COOLDOWN OF 1 IS NOT A COOLDOWN, and this is why the resolver
        // refuses it rather than letting it through.
        //
        // The number counts from the turn it was cast on, so 1 means "turn one,
        // then turn two" -- which is every turn. A skill authored that way
        // behaves exactly as one with no cooldown, and an author who typed it
        // believes they made it wait. The domain still HANDLES the value
        // sanely, which is what this pins; the refusal lives at the content
        // boundary where a typo is still a typo.
        [Test]
        public void ACooldownOfOneIsIndistinguishableFromNone()
        {
            var one = Bolt(1);
            var (sessionA, _, foeA) = Fight(one);

            Assert.IsTrue(sessionA.CastSkill(one, foeA), "turn 1");
            Assert.IsTrue(sessionA.CastSkill(one, foeA),
                "back on turn 2, which is every turn -- see SkillEntryResolver, which refuses it");

            var none = Bolt(0);
            var (sessionB, _, foeB) = Fight(none);

            Assert.IsTrue(sessionB.CastSkill(none, foeB));
            Assert.IsTrue(sessionB.CastSkill(none, foeB),
                "a cooldown of 0 behaves identically, which is the whole objection to 1");
        }

        [Test]
        public void ALongCooldownWaitsTheWholeWay()
        {
            var bolt = Bolt(5);
            var (session, _, foe) = Fight(bolt);

            Assert.IsTrue(session.CastSkill(bolt, foe), "turn 1");

            for (int turn = 2; turn <= 5; turn++)
            {
                Assert.IsFalse(session.CastSkill(bolt, foe), $"turn {turn} should still be waiting");
                PassATurn(session, foe);
            }

            Assert.IsTrue(session.CastSkill(bolt, foe), "turn 6 should be back");
        }

        // ---- what the player is told ---------------------------------------------

        // A REFUSAL THE PLAYER CAN READ. A greyed row that says nothing sends
        // them to check their mana, which is not the problem.
        //
        // Read at the START OF THE NEXT TURN, which is when a menu is ever
        // looked at -- a cooldown of 3 cast on turn 1 is ready on turn 4, so
        // standing on turn 2 there are 2 turns left to wait.
        [Test]
        public void TheMenuSaysHowLongRatherThanJustGoingQuiet()
        {
            var bolt = Bolt(3);
            var (session, hero, foe) = Fight(bolt);

            session.CastSkill(bolt, foe);

            var option = session.SkillOptionsFor(hero).Single();

            Assert.AreEqual(2, option.CooldownRemaining,
                "cast on turn 1 with a cooldown of 3 is ready on turn 4, so turn 2 has 2 to wait");
            Assert.IsFalse(option.Ready, "a cooling skill is not ready even when it is affordable");
            Assert.IsTrue(option.Affordable,
                "affordability and readiness are different questions -- folding them together " +
                "would make the row say 'no mana' about a skill they can pay for");
        }


        // ---- the salt ledger -----------------------------------------------------

        // The relic that makes a long cooldown a decision instead of a wait.
        [Test]
        public void TheSaltLedgerTakesATurnOffEverySwing()
        {
            var bolt = Bolt(3);
            var (session, _, foe) = Fight(bolt, RelicEffect.SaltLedger);

            Assert.IsTrue(session.CastSkill(bolt, foe), "turn 1");

            // Turn 2: swing. The turn itself takes one off, and the Ledger
            // takes a second -- so a 3-turn wait is served by turn 3 rather
            // than turn 4.
            PassATurn(session, foe);

            Assert.IsTrue(session.CastSkill(bolt, foe), "the swing bought a turn back");
        }

        [Test]
        public void WithoutTheLedgerASwingBuysNothing()
        {
            var bolt = Bolt(3);
            var (session, _, foe) = Fight(bolt);

            Assert.IsTrue(session.CastSkill(bolt, foe), "turn 1");
            PassATurn(session, foe);

            Assert.IsFalse(session.CastSkill(bolt, foe),
                "no relic, so the swing bought nothing and turn 3 is still a wait");
            PassATurn(session, foe);

            Assert.IsTrue(session.CastSkill(bolt, foe), "and back on turn 4, as the cooldown says");
        }
    }
}
