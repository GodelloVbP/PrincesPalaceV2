using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // FightRunner.Play and FightInvariants.Check, together: a determinism
    // check (the same seed twice must give an identical RunTrace hash) and
    // the invariant sweep that catches a state no legitimate fight reaches.
    public class BotFightRunnerTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static (FightSession session, CombatantState hero, CombatantState foe) OneOnOne(
            int foeHealth = 60, int heroSpeed = 10)
        {
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: heroSpeed);
            var foe = Fighter("Foe", false, maxHealth: foeHealth, attack: 1, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foe);
        }

        private static RunTrace Play(IFightPolicy policy, ulong seed)
        {
            var (session, hero, foe) = OneOnOne();
            var fightTrace = new FightTrace();
            FightRunner.Play(session, policy, System.Array.Empty<SatchelStack>(), new SeededRandom(seed), fightTrace);

            var runTrace = new RunTrace { Seed = seed, Archetype = policy.GetType().Name, Profile = "Fresh" };
            runTrace.Fights.Add(fightTrace);
            return runTrace;
        }

        [Test]
        public void Play_SameSeedTwice_ProducesAnIdenticalTraceHash()
        {
            var first = Play(new GreedyAggressivePolicy(), 7);
            var second = Play(new GreedyAggressivePolicy(), 7);

            Assert.AreEqual(first.Hash(), second.Hash(),
                "the same seed, archetype and profile must replay to the same trace");
        }

        [Test]
        public void Hash_ADifferentSeedOnAnOtherwiseIdenticalTrace_ChangesTheHash()
        {
            // Direct on RunTrace rather than through FightRunner: the seed
            // going into a policy's own tie-breaks can legitimately produce
            // the same action sequence by chance on a two-option menu, which
            // would make this flaky for the wrong reason. Hash() itself
            // reading the seed field is what this test is actually about.
            var first = new RunTrace { Seed = 1, Archetype = "A", Profile = "Fresh" };
            var second = new RunTrace { Seed = 2, Archetype = "A", Profile = "Fresh" };

            Assert.AreNotEqual(first.Hash(), second.Hash(),
                "two different seeds landing on the same hash would mean the hash ignores the seed");
        }

        // A THREE-MEMBER PARTY, which is what a formation needs to be a
        // formation. OneOnOne above cannot exercise Move at all.
        private static FightSession Squad(int foeHealth = 400)
        {
            var party = new[]
            {
                Fighter("Front", true, maxHealth: 300, attack: 60, speed: 12),
                Fighter("Middle", true, maxHealth: 300, attack: 60, speed: 11),
                Fighter("Back", true, maxHealth: 300, attack: 60, speed: 10),
            };
            var foe = Fighter("Foe", false, maxHealth: foeHealth, attack: 8, speed: 6);

            var kits = party
                .Select(p => new PlayerKit(p.Name, CharacterRole.Tank, null, null, DamageType.Physical))
                .ToList();

            var session = new FightSession(new CombatEncounter(party, new[] { foe }), kits, null,
                new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();
            return session;
        }

        [Test]
        public void Play_MoveThenGreedy_FinishesTwentySeededFightsWithNoInvariantHits()
        {
            // A BOUNDED SCRIPT, not an always-Move one. Pacing back and forth
            // forever is legitimate play, so a policy that never stops moving
            // would stall honestly and there would be no way to tell that from
            // a bug. MoveThenGreedyPolicy takes two moves and then plays to
            // win, which makes "did it finish" a real question again.
            //
            // PartyOrderIntact is the invariant this is really here for: Move
            // is the only thing in the game that reorders the party list, and
            // a swap that duplicated a member would leave the fight looking
            // entirely normal.
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var session = Squad();
                var trace = new FightTrace();

                var hits = FightRunner.Play(session, new MoveThenGreedyPolicy(),
                    System.Array.Empty<SatchelStack>(), new SeededRandom(seed), trace);

                CollectionAssert.IsEmpty(hits, $"seed {seed}");
                Assert.IsTrue(session.IsOver, $"seed {seed} did not finish");
                Assert.IsTrue(trace.TurnTraces.Any(t => t.Action == "Move"),
                    $"seed {seed} never actually moved, so it proves nothing");
                Assert.AreEqual(3, session.Encounter.PlayerParty.Distinct().Count(),
                    $"seed {seed} lost or duplicated a party member");
            }
        }

        [Test]
        public void Check_APartyMemberMissingFromTheCurrentOrder_TripsPartyOrderIntact()
        {
            // FORCED, because SwapPartySlots cannot produce it -- which is the
            // whole reason the invariant is worth having: the failure it names
            // would be silent if it ever did happen, so the check has to be
            // shown firing at least once. A stranger in the fight-start
            // snapshot stands in for a member the list lost.
            var session = Squad();
            var startingOrder = session.Encounter.PlayerParty.ToList();
            startingOrder.Add(Fighter("Ghost", true));

            var hits = FightInvariants.Check(session, 1, startingOrder);

            Assert.IsTrue(hits.Any(h => h.Name == "PartyOrderIntact"),
                "a member missing from the current list must be reported");
            CollectionAssert.IsEmpty(
                FightInvariants.Check(session, 1, session.Encounter.PlayerParty.ToList())
                    .Where(h => h.Name == "PartyOrderIntact").ToList(),
                "and an intact party must not");
        }

        [Test]
        public void Check_OnANormalFight_ReportsNoInvariantHits()
        {
            var (session, hero, foe) = OneOnOne();

            var hits = FightInvariants.Check(session, 1);

            CollectionAssert.IsEmpty(hits);
        }

        [Test]
        public void Check_WhenAFixtureForcesHpAboveMax_ReportsHpAboveMax()
        {
            var (session, hero, foe) = OneOnOne();
            hero.CurrentHealth = hero.MaxHealth + 50;

            var hits = FightInvariants.Check(session, 1);

            Assert.IsTrue(hits.Any(h => h.Name == "HpAboveMax"),
                "a living combatant above their own max HP must be reported");
        }

        [Test]
        public void Check_WhenMoreThanTheCommandCapWasIssued_ReportsTooManyCommands()
        {
            var (session, hero, foe) = OneOnOne();

            var hits = FightInvariants.Check(session, FightInvariants.MaxPlayerCommands + 1);

            Assert.IsTrue(hits.Any(h => h.Name == "TooManyCommands"));
        }

        // ---- the stall, which is not a command count ----------------------------
        //
        // Only the false-positive half is pinned here, and that is deliberate
        // rather than lazy. A genuine two-sided deadlock cannot be built out
        // of this engine on purpose: an attack of 0 still lands minimum
        // damage, so both sides make progress every command, and a policy that
        // only passes trips the pre-existing StalledEnemyTurn fault and leaves
        // the loop by that door instead. The one shape that IS reachable, and
        // the one that cost 3,220 false bug rows, is a fight that grinds and
        // wins -- which is what the test below refuses to let regress.

        // THE REGRESSION THIS WHOLE CHANGE IS FOR. A slow win is not a stall.
        // A Mid GreedyDefensive at the floor-3 boss took 201 commands to bring
        // a Throne Colossus from 794 HP to 35 -- winning the whole way -- and
        // the old flat 200-command cap called it stuck and truncated the run
        // into a death, 3,220 times in one batch.
        [Test]
        public void Play_AGrindingButWinningFight_IsNotReportedAsStuck()
        {
            // Chip damage against a large pool: hundreds of commands, every
            // one of them progress.
            var hero = Fighter("Hero", true, maxHealth: 5000, attack: 4, speed: 10);
            var foe = Fighter("Foe", false, maxHealth: 900, attack: 1, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };

            var trace = new FightTrace();
            var hits = FightRunner.Play(session, new GreedyAggressivePolicy(),
                System.Array.Empty<SatchelStack>(), new SeededRandom(1), trace);

            Assert.Greater(trace.Turns, 200,
                "the fixture did not actually run past the old cap, so it proves nothing");
            Assert.IsTrue(session.IsOver && session.PlayerWon, "the fixture fight was not won");
            Assert.IsFalse(hits.Any(h => h.Name == "TooManyCommands"),
                "a fight that was progressing every single command was called stuck");
        }
    }
}
