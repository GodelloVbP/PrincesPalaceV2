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
    // THE MONSTERS' SIDE OF THE FRONT-RANK RULE, and the draw policy that
    // keeps a seeded run reproducible while it applies.
    //
    // The player's half of this rule has been live since v1; the enemy half is
    // new, and it is the largest player-facing change in the positioning work:
    // enemy melee now concentrates on rank 0 instead of picking a party member
    // at random, which is what makes a Move worth a turn at all.
    public class EnemyReachTests
    {
        private static CombatantState Member(string name, int speed, int health = 5000) =>
            new CombatantState(name, true, health, 10, 20, speed);

        // SPEED MATTERS TO THE FIXTURE, and the two settings mean two
        // different paths through ResolveEnemyAction:
        //
        //   Opening (faster than the whole party) -- swings during Begin(),
        //     BEFORE any intent exists, because AutoResolveEnemyTurns runs
        //     ahead of PrepareEnemyIntents. That is the no-committed-intent
        //     path, and it is reach-gated too.
        //   Replying (slower than the front-ranker, faster than the rest) --
        //     the ordinary path: an intent is telegraphed on the player's
        //     turn and honoured or re-picked when it resolves.
        private const int OpeningSpeed = 1000;
        private const int ReplyingSpeed = 50;

        private static CombatantState Monster(string name = "Foe", int speed = ReplyingSpeed, int attack = 30) =>
            new CombatantState(name, false, 100000, 10, attack, speed);

        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

        // A single-target monster ability with an authored reach -- the shape
        // an enemies.json row pointing at a skills.json entry with reachSlots
        // resolves to.
        private static ResolvedSkill Ability(Reach reach, int flat = 40) =>
            new ResolvedSkill("lob", "Lob", "", "", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, flat, ignoresDefense: true,
                null, SpellPresentation.None, 0, reach: reach);

        private static FightSession Fight(CombatantState[] party, CombatantState[] foes,
            IReadOnlyList<EnemyAbility> abilities = null, SeededRandom rng = null)
        {
            var kits = foes.Select(f => new EnemyKit(Source(f.Name), false, abilities)).ToList();
            return new FightSession(new CombatEncounter(party, foes), null, kits,
                rng ?? new SeededRandom(11)) { DamageVarianceRange = 0f };
        }

        // ---- the mirror of the player's own rule ----------------------------

        [Test]
        public void EnemyMeleeOnlyEverLandsOnTheFrontRank()
        {
            // THE NO-INTENT PATH. This monster is faster than the whole
            // party, so it swings during Begin() with nothing telegraphed --
            // the path that used to pick a party member uniformly at random
            // and reach straight past the front rank.
            var front = Member("Front", 10);
            var back = Member("Back", 9);
            var session = Fight(new[] { front, back }, new[] { Monster(speed: OpeningSpeed) });
            session.Begin();

            Assert.Less(front.CurrentHealth, front.MaxHealth, "the front rank takes the swing");
            Assert.AreEqual(back.MaxHealth, back.CurrentHealth,
                "and the back rank is untouched -- this used to be a coin flip");
        }

        [Test]
        public void ARankRestrictedAbilityCannotBeAimedAtTheFrontRank()
        {
            // Speeds chosen so the front-ranker opens (its intent is
            // telegraphed before the monster acts) and the monster then
            // replies before the back-ranker's own turn comes round.
            var front = Member("Front", 100);
            var back = Member("Back", 1);
            var abilities = new List<EnemyAbility> { EnemyAbility.Of(Ability(Reach.Ranks(1, 2)), 1f) };
            var session = Fight(new[] { front, back }, new[] { Monster() }, abilities);
            session.Begin();

            session.ExecuteAttack(session.Encounter.Enemies[0]);

            Assert.AreEqual(front.MaxHealth, front.CurrentHealth, "the front rank is out of its reach");
            Assert.Less(back.CurrentHealth, back.MaxHealth);
        }

        [Test]
        public void AnAbilityWithNothingInReachIsZeroWeightedAndSomethingElseIsDrawn()
        {
            // WEIGHTLESS FOR THIS DRAW, not gone -- the same "capped this turn
            // does not mean gone" treatment the summon cap already gets. The
            // lob outweighs the swing a thousand to one, so it wins every draw
            // it is legal for; with its only legal rank empty it is skipped
            // over and the monster swings instead. The authored weight is
            // untouched: it is legal again the moment somebody stands there.
            var front = Member("Front", 100);
            var back = Member("Back", 1);
            var abilities = new List<EnemyAbility>
            {
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1f),
                EnemyAbility.Of(Ability(Reach.Ranks(1, 2)), 1000f),
            };
            var session = Fight(new[] { front, back }, new[] { Monster() }, abilities);
            back.CurrentHealth = 0;
            session.Begin();

            var enemy = session.Encounter.Enemies[0];
            CollectionAssert.IsEmpty(session.EligibleTargets(enemy, Reach.Ranks(1, 2)).ToList(),
                "rank 1 is a corpse, so the lob has nowhere to land");

            session.ExecuteAttack(enemy);

            Assert.Less(front.CurrentHealth, front.MaxHealth,
                "the swing, drawn only because the lob was weighted out for this draw");
        }

        [Test]
        public void ATauntFromTheBackRankKeepsBothKindsOfAbilityLegal()
        {
            // Provoke overrides the mask for EVERY ReachKind, in both
            // directions: the provoker becomes reachable from anywhere, and
            // nobody else is reachable at all. So a melee monster and a
            // back-rank-only one are both still legal, and both go for the
            // taunter.
            var bystander = Member("Bystander", 10);
            var provoker = Member("Provoker", 9);
            var melee = Monster("Melee");
            var lobber = Monster("Lobber");
            var session = Fight(new[] { bystander, provoker }, new[] { melee, lobber });

            StatusEffects.Apply(melee.Statuses, StatusEffectType.Provoked, 0, 5, provoker);
            StatusEffects.Apply(lobber.Statuses, StatusEffectType.Provoked, 0, 5, provoker);

            CollectionAssert.AreEqual(new[] { provoker },
                session.EligibleTargets(melee, Reach.Melee).ToList());
            CollectionAssert.AreEqual(new[] { provoker },
                session.EligibleTargets(lobber, Reach.Ranks(1, 2)).ToList(),
                "a back-rank-only ability is kept legal by the taunt, not refused by the mask");
        }

        // ---- the draw policy ------------------------------------------------

        [Test]
        public void OnePreparationConsumesExactlyOneAbilityDrawAndOneTargetDrawPerEnemy()
        {
            // AGAINST A CONTROL GENERATOR. PrepareEnemyIntents is called
            // directly rather than through Begin(), so the only draws in the
            // stream are the ones this test is counting.
            var rng = new SeededRandom(23);
            var control = new SeededRandom(23);

            var party = new[] { Member("A", 10), Member("B", 9) };
            var session = Fight(party, new[] { Monster("One"), Monster("Two") }, rng: rng);

            session.PrepareEnemyIntents();

            for (int enemy = 0; enemy < 2; enemy++)
            {
                control.NextFloat();          // which ability
                control.NextInt(0, 2);        // which target, over the LIVING PARTY count
            }

            Assert.AreEqual(control.NextFloat(), rng.NextFloat(),
                "two enemies must have consumed exactly four values between them");
        }

        [Test]
        public void TheTargetDrawHappensWhetherOrNotATauntOverridesIt()
        {
            // THE POINT OF DRAWING UNCONDITIONALLY. A draw made only when it
            // was going to be used would make the rest of a seeded run depend
            // on whether a taunt happened to be up, which is a thing the
            // player controls.
            var withTaunt = new SeededRandom(31);
            var without = new SeededRandom(31);

            var taunted = Monster("One");
            var tauntedParty = new[] { Member("A", 10), Member("B", 9) };
            var a = Fight(tauntedParty, new[] { taunted }, rng: withTaunt);
            StatusEffects.Apply(taunted.Statuses, StatusEffectType.Provoked, 0, 5, tauntedParty[1]);

            var b = Fight(new[] { Member("A", 10), Member("B", 9) }, new[] { Monster("One") }, rng: without);

            a.PrepareEnemyIntents();
            b.PrepareEnemyIntents();

            Assert.AreEqual(without.NextFloat(), withTaunt.NextFloat(),
                "the taunt changed which target was picked, not how many values it cost to pick one");
        }

        [Test]
        public void AResolutionTimeRePickCostsNothingFromTheStream()
        {
            // TWO RUNS THAT DIFFER ONLY IN WHETHER A RE-PICK FIRES.
            //
            // Both field the same party, the same speeds, the same monster and
            // the same authored ability -- everything except its Reach. With
            // Melee the promised front-ranker stops being reachable the moment
            // the party swaps, so the promise is re-picked; with Any it stays
            // reachable and is honoured. Both party members carry identical
            // stats, so the swing that follows costs the same either way.
            //
            // A re-pick that rolled for its replacement would leave the two
            // generators one value apart.
            var rePicked = new SeededRandom(47);
            var honoured = new SeededRandom(47);

            Assert.AreEqual(
                StreamAfterAMoveAndOneReply(Reach.Any, honoured),
                StreamAfterAMoveAndOneReply(Reach.Melee, rePicked),
                "the re-pick is the first eligible target in list order, and rolls for nothing");
        }

        private static float StreamAfterAMoveAndOneReply(Reach reach, SeededRandom rng)
        {
            // IDENTICAL STATS on both members, so the swing that follows the
            // Move costs the same however the target was chosen. Only the
            // speeds differ, and the schedule consumes nothing from the
            // stream.
            var front = Member("Front", 100);
            var behind = Member("Behind", 1);
            var abilities = new List<EnemyAbility> { EnemyAbility.Of(Ability(reach, flat: 10), 1f) };
            var session = Fight(new[] { front, behind }, new[] { Monster() }, abilities, rng);
            session.Begin();

            session.Move(MoveDirection.Back);

            return rng.NextFloat();
        }

        [Test]
        public void APromiseAgainstAFrontRankerThatMovedIsRePickedOntoTheNewFront()
        {
            // The player's whole reason to spend a turn on Move: the blow that
            // was coming for the wounded front-ranker lands on whoever is
            // standing there when it arrives.
            var front = Member("Front", 100);
            var behind = Member("Behind", 1);
            var abilities = new List<EnemyAbility> { EnemyAbility.Of(Ability(Reach.Melee), 1f) };
            var session = Fight(new[] { front, behind }, new[] { Monster() }, abilities);
            session.Begin();
            session.DrainBeats();

            session.Move(MoveDirection.Back);

            var messages = session.DrainBeats().SelectMany(b => b.Messages).ToList();

            Assert.AreEqual(front.MaxHealth, front.CurrentHealth,
                "the promised target stepped out of reach and must not be hit anyway");
            Assert.Less(behind.CurrentHealth, behind.MaxHealth);
            Assert.IsTrue(messages.Any(m => m.Contains("Behind")),
                "and the log names whoever actually took it: " + string.Join(" | ", messages));
        }
    }
}
