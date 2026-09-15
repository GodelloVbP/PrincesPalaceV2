using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // SECOND LIFE, CLAUSE BY CLAUSE AGAINST THE PLAN THAT PROPOSES MOVING IT.
    //
    // docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §5 moves Second Life
    // to level 25 and describes it as: "once per run, the first time this
    // character would drop to 0 health they stay at 1 and gain no other
    // effect; consumed on use, reset at run start, not shared. This is the
    // existing rule as read from the code; phase 2 pins it and any difference
    // is reported before content moves."
    //
    // It is NOT the existing rule. Four of the six clauses describe something
    // else, and this file asserts what the CODE does, with the divergence
    // named on each one. Nothing here changes behaviour -- phase 2's brief is
    // to report, and the owner decides in phase 4 whether the content moves to
    // a rule the code already has or the code moves to the rule the plan
    // describes.
    //
    //   1. "the first time THIS CHARACTER would drop to 0"
    //      -> the code fires only when the WHOLE PARTY would be wiped.
    //         FightSession.ResolveOutcome calls TrySecondLife inside
    //         `if (!_encounter.PlayerWon && ...)`, which is only reached once
    //         the encounter is already over. A character who falls with an
    //         ally still standing spends nothing and stays down.
    //   2. "they stay at 1"
    //      -> the code returns HALF of max health, floored, minimum 1
    //         (SecondLifeNumerator/Denominator).
    //   3. "not shared"
    //      -> the code raises EVERY downed member for one charge, and
    //         SquadTrack.SecondLivesLeft sums the charges across the fielded
    //         squad into one run-scoped pot. It is shared twice over.
    //   4. "once per run"
    //      -> true only of a squad with exactly one collected node. Three
    //         characters past level 25 bring three charges to the same run.
    //
    // The two clauses that DO hold are "consumed on use" (pinned below) and
    // "reset at run start" (run.secondLivesUsed is a RunSnapshot field, so a
    // new descent starts at zero -- Core, out of this assembly's reach; see
    // SquadTrack.cs and RunOrchestrator.cs:432).
    public class SecondLifeSpecTests
    {
        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 20, 10, false,
                DamageType.Physical, DamageType.Physical, 0);

        // A party of `heroHealth` values against one monster too tough to
        // kill, so the fight runs until the party is wiped. Speed 9 against
        // the heroes' 10, the same choice FightOutcomeTests documents: at
        // speed 1 the monster barely acts and the defeat cases never fire.
        private static (FightSession session, List<CombatantState> party, CombatantState foe) Party(
            int charges, params int[] heroHealth)
        {
            var party = new List<CombatantState>();
            var kits = new List<PlayerKit>();
            for (int i = 0; i < heroHealth.Length; i++)
            {
                party.Add(new CombatantState("Hero" + i, true, heroHealth[i], 30, 40, 10));
                kits.Add(new PlayerKit("hero" + i, CharacterRole.Tank, null, null, null));
            }

            var foe = new CombatantState("Rat", false, 5000, 10, 8, 9);
            var encounter = new CombatEncounter(party, new[] { foe });

            var session = new FightSession(encounter, kits,
                new List<EnemyKit> { new EnemyKit(Source("rat"), false) },
                new SeededRandom(2), isEliteFight: false)
            {
                DamageVarianceRange = 0f,
                SecondLifeCharges = charges,
            };

            session.Begin();
            return (session, party, foe);
        }

        // ---- clause 1: "the first time this character would drop to 0" ------
        //
        // DIFFERS. The trigger is the PARTY being wiped, not a character
        // falling. With an ally still up the fight is not over, ResolveOutcome
        // returns at its own `!_encounter.IsOver` guard, and the charge is
        // untouched -- the fallen character simply stays down for the rest of
        // the fight.
        [Test]
        public void ACharacterFallingWithAnAllyStandingSpendsNothing()
        {
            var (session, party, foe) = Party(charges: 1, 200, 200);
            party[0].CurrentHealth = 1;

            session.ExecuteAttack(foe);

            Assert.IsFalse(session.IsOver, "fixture: the monster is unkillable, the fight runs on");
            Assert.AreEqual(0, session.SecondLivesSpent,
                "a charge was spent on one character falling, not on the party being wiped");
        }

        // ---- clause 2: "they stay at 1" -------------------------------------
        //
        // DIFFERS. Half of max health, floored -- 100 of 200 here, and the
        // literal is the fixture's own number rather than a recomputation of
        // MaxHealth * SecondLifeNumerator / SecondLifeDenominator.
        [Test]
        public void TheRaisedPartyComesBackOnHalfMaxHealthNotOne()
        {
            var (session, party, foe) = Party(charges: 1, 200);
            party[0].CurrentHealth = 1;

            session.ExecuteAttack(foe);

            Assert.IsFalse(session.IsOver, "the fight ended despite a charge being available");
            Assert.AreEqual(100, party[0].CurrentHealth,
                "the plan says 1; the code says half of max");
        }

        // The one place the "minimum 1" half of the arithmetic shows: a
        // character whose max health is 1 halves to 0, which would revive them
        // into death and spend the charge for nothing.
        [Test]
        public void AFrailCharacterComesBackOnOneRatherThanZero()
        {
            var (session, party, foe) = Party(charges: 1, 1);
            party[0].CurrentHealth = 1;

            session.ExecuteAttack(foe);

            Assert.AreEqual(1, party[0].CurrentHealth);
            Assert.AreEqual(1, session.SecondLivesSpent);
        }

        // ---- clause 3: "not shared between characters" ----------------------
        //
        // DIFFERS, and deliberately so on the code's side: TrySecondLife's own
        // comment argues that raising one member and losing anyway would spend
        // the charge to change nothing. ONE charge raises EVERY downed member.
        [Test]
        public void OneChargeRaisesTheWholeDownedParty()
        {
            var (session, party, foe) = Party(charges: 1, 200, 160);

            // Both on their last point, then driven until the monster has put
            // them BOTH down -- which is the only state that reaches
            // TrySecondLife at all (clause 1 above). Held at 1 each pass so a
            // hero the monster has not reached yet cannot heal past it.
            int guard = 0;
            while (session.SecondLivesSpent == 0 && !session.IsOver && guard++ < 20)
            {
                foreach (var member in party)
                {
                    if (member.IsAlive) member.CurrentHealth = 1;
                }

                session.ExecuteAttack(foe);
            }

            Assert.AreEqual(1, session.SecondLivesSpent, "the party was never wiped, so nothing was spent");
            Assert.IsFalse(session.IsOver);
            Assert.AreEqual(100, party[0].CurrentHealth, "half of 200");
            Assert.AreEqual(80, party[1].CurrentHealth, "half of 160, so both were raised for one charge");
        }

        // ---- clause 4: "once per run" ---------------------------------------
        //
        // DIFFERS in the general case. The session's ceiling is whatever the
        // caller set, and the caller (SquadTrack.SecondLivesLeft) SUMS the
        // collected nodes across the fielded squad -- so three characters past
        // level 25 bring three refusals to one run, not one.
        [Test]
        public void TheCeilingIsWhateverTheSquadBroughtNotOne()
        {
            var (session, party, foe) = Party(charges: 2, 200);

            party[0].CurrentHealth = 1;
            session.ExecuteAttack(foe);
            Assert.AreEqual(1, session.SecondLivesSpent);
            Assert.IsFalse(session.IsOver);

            party[0].CurrentHealth = 1;
            session.ExecuteAttack(foe);
            Assert.AreEqual(2, session.SecondLivesSpent, "the second charge was not available");
            Assert.IsFalse(session.IsOver, "a squad carrying two charges was wiped on the second death");
        }

        // ---- clause 5: "consumed on use" ------------------------------------
        //
        // HOLDS. One charge buys exactly one refusal; the next wipe ends the
        // fight. Driven in a loop rather than with one more swing because
        // whether the monster replies on any given exchange is a turn-order
        // detail, the same reasoning FightOutcomeTests.ASecondLifeIsSpentOnlyOnce
        // records.
        [Test]
        public void AChargeIsConsumedOnUseAndTheNextWipeEndsTheFight()
        {
            var (session, party, foe) = Party(charges: 1, 200);
            party[0].CurrentHealth = 1;

            session.ExecuteAttack(foe);
            Assert.AreEqual(1, session.SecondLivesSpent);

            int guard = 0;
            while (!session.IsOver && guard++ < 20)
            {
                party[0].CurrentHealth = 1;
                session.ExecuteAttack(foe);
            }

            Assert.Less(guard, 20, "the fight never ended, so the charge is being spent repeatedly");
            Assert.IsTrue(session.IsOver);
            Assert.IsFalse(session.PlayerWon);
            Assert.AreEqual(1, session.SecondLivesSpent, "one charge bought more than one refusal");
        }

        // A charge is not spendable by a fight that ended for any other
        // reason. Nobody is down, so there is nothing to raise.
        [Test]
        public void AChargeIsNotSpentOnAFightThatWasWon()
        {
            var party = new List<CombatantState> { new CombatantState("Hero", true, 300, 30, 40, 10) };
            var foe = new CombatantState("Rat", false, 1, 10, 8, 9);

            var session = new FightSession(new CombatEncounter(party, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("hero", CharacterRole.Tank, null, null, null) },
                new List<EnemyKit> { new EnemyKit(Source("rat"), false) },
                new SeededRandom(2), isEliteFight: false)
            {
                DamageVarianceRange = 0f,
                SecondLifeCharges = 1,
            };
            session.Begin();

            session.ExecuteAttack(foe);

            Assert.IsTrue(session.PlayerWon);
            Assert.AreEqual(0, session.SecondLivesSpent);
        }
    }
}
