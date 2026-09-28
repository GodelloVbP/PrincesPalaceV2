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
    // An enemy's schedule, counted on its own acting turns
    // (docs/PLAN_BELLWETHER_KIT.md 1.6, 2.2, 3.7; M4). Fixture skills and a
    // fixture monster, never the Bellwether's content (M5). Every figure is
    // a literal.
    public class EnemyScheduleTests
    {
        private const string Scratch = "Scratch";
        private const string Chains = "Dark Chains";
        private const string Knell = "Death Knell";

        // Every fixture skill is the same 1-point hit unless a test says
        // otherwise, so the three differ only in name: whatever the schedule
        // does to the RNG stream, the skills themselves cannot.
        private static ResolvedSkill Hit(string id, string name) =>
            new ResolvedSkill(id, name, "", "bell", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0, playerSelectable: false);

        private static EnemyKit Kit(bool scheduled, params ResolvedSkill[] skills)
        {
            var source = new ResolvedEnemy("bell", "Bell", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);
            if (scheduled)
            {
                source.Schedule = new[]
                {
                    new EnemyScheduleEntry(new[] { 1, 5 }, new[] { "chains", "knell" }),
                };
            }

            // Scratch carries the whole weight; the pair is authored at 0,
            // exactly the way M5 lists it in `abilities`.
            var pool = skills.Select((s, i) => EnemyAbility.Of(s, i == 0 ? 1f : 0f)).ToList();
            return new EnemyKit(source, false, pool);
        }

        private static EnemyKit PairKit(bool scheduled = true) =>
            Kit(scheduled, Hit("scratch", Scratch), Hit("chains", Chains), Hit("knell", Knell));

        // The hero is twice as fast as the bell, so at most one bell turn
        // falls between two of his; the bell has health to spare.
        private static (FightSession session, CombatantState hero, CombatantState bell) Fight(
            EnemyKit kit, SeededRandom rng = null, bool stunnedFromTheStart = false)
        {
            var hero = new CombatantState("Shawn", true, 100000, 10, 20, 20);
            var bell = new CombatantState("Bell", false, 100000, 0, 0, 10);
            if (stunnedFromTheStart) bell.Statuses.Add(new ActiveStatus(StatusEffectType.Stun, 0, 1));

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { bell }),
                null, new List<EnemyKit> { kit }, rng ?? new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            Assert.AreSame(hero, session.Current, "fixture: the hero opens");
            return (session, hero, bell);
        }

        // Swings until the bell has taken `upTo` acting turns, returning what
        // it resolved on each (the committed label, read just before the turn
        // it resolves on).
        private static List<string> Play(FightSession session, CombatantState bell, int upTo)
        {
            var played = new List<string>();
            for (int guard = 0; session.ActingTurnsOf(bell) < upTo && guard < 400; guard++)
            {
                string label = session.IntentFor(bell);
                int before = session.ActingTurnsOf(bell);
                Assert.IsTrue(session.ExecuteAttack(bell), "fixture: the swing was refused");
                int after = session.ActingTurnsOf(bell);
                Assert.LessOrEqual(after - before, 1, "fixture: two bell turns between two of the hero's");
                if (after > before) played.Add(label);
            }

            Assert.AreEqual(upTo, session.ActingTurnsOf(bell), "the bell never got that many turns");
            return played;
        }

        // ---- the pair ----------------------------------------------------------

        [Test]
        public void ThePairFallsOnActingTurnsOneTwoAndFiveSix_TheDrawOtherwise()
        {
            var (session, _, bell) = Fight(PairKit());

            CollectionAssert.AreEqual(
                new[] { Chains, Knell, Scratch, Scratch, Chains, Knell, Scratch },
                Play(session, bell, 7));
        }

        [Test]
        public void WithoutAScheduleEveryTurnIsTheDraw()
        {
            var (session, _, bell) = Fight(PairKit(scheduled: false));

            CollectionAssert.AreEqual(Enumerable.Repeat(Scratch, 4).ToArray(), Play(session, bell, 4));
        }

        [Test]
        public void AStunAfterTheChainsDelaysTheKnell_AndNothingTakesItsPlace()
        {
            var (session, _, bell) = Fight(PairKit());
            CollectionAssert.AreEqual(new[] { Chains }, Play(session, bell, 1));
            Assert.AreEqual(Knell, session.IntentFor(bell), "the knell is committed once the chains resolved");

            bell.Statuses.Add(new ActiveStatus(StatusEffectType.Stun, 0, 1));
            var log = new List<string>();
            for (int guard = 0; StatusEffects.HasStun(bell.Statuses) && guard < 20; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
                log.AddRange(session.DrainBeats().SelectMany(b => b.Messages));
            }

            CollectionAssert.Contains(log, "Bell is stunned and cannot act!");
            Assert.AreEqual(1, session.ActingTurnsOf(bell), "a stunned turn is not an acting turn");
            Assert.AreEqual(Knell, session.IntentFor(bell), "the step is still owed");

            CollectionAssert.AreEqual(new[] { Knell, Scratch, Scratch, Chains }, Play(session, bell, 5));
        }

        [Test]
        public void AStunOnTheFirstTurnDelaysTheChains()
        {
            var (session, _, bell) = Fight(PairKit(), stunnedFromTheStart: true);
            Assert.AreEqual(FightSession.IntentForfeit, session.IntentFor(bell));

            for (int guard = 0; StatusEffects.HasStun(bell.Statuses) && guard < 20; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            Assert.AreEqual(0, session.ActingTurnsOf(bell));
            CollectionAssert.AreEqual(new[] { Chains, Knell, Scratch }, Play(session, bell, 3));
        }

        [Test]
        public void TheScheduleSpendsTheSameDrawsAsNoSchedule()
        {
            var withRng = new SeededRandom(21);
            var withoutRng = new SeededRandom(21);
            var (with, _, withBell) = Fight(PairKit(), withRng);
            var (without, _, withoutBell) = Fight(PairKit(scheduled: false), withoutRng);

            Play(with, withBell, 7);
            Play(without, withoutBell, 7);

            // Same seed, same later rolls: the streams stand at one position.
            Assert.AreNotEqual(new SeededRandom(21).State, withRng.State, "fixture: nothing was drawn at all");
            Assert.AreEqual(withoutRng.State, withRng.State);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(withoutRng.NextFloat(), withRng.NextFloat(), $"roll {i} after seven bell turns");
            }
        }

        [Test]
        public void TheChainsIntentNamesTheKnellThatFollows()
        {
            var (session, _, bell) = Fight(PairKit());

            var intent = session.IntentDetailFor(bell).Value;
            Assert.AreEqual(Chains, intent.Label);
            Assert.AreEqual(Knell, intent.Then);
            Assert.AreEqual("Dark Chains! Death Knell next", session.TelegraphLine(bell));
            Assert.AreEqual("\nDark Chains! Death Knell next", session.TelegraphSuffix(bell, isPlayerTurn: true));
            StringAssert.EndsWith("\nDeath Knell next", FightHudModel.IntentTooltip("Bell", intent));

            Play(session, bell, 1);
            Assert.IsNull(session.IntentDetailFor(bell).Value.Then, "the knell is the pair's last step");
        }
    }
}
