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
    // WHERE AN ACTION CAN LAND: the Reach value type, FightSession.CanReach /
    // EligibleTargets, and the two commands that enforce them.
    //
    // Sessions are built inline rather than from a shared fixture (the
    // TurnRiderTests idiom): every case here turns on a specific formation,
    // and a fixture that fielded one arrangement for all of them would make
    // half the tests read as though the arrangement did not matter.
    public class ReachTests
    {
        private static CombatantState Hero(string name = "Hero", int speed = 10, int maxMana = 50) =>
            new CombatantState(name, true, 200, maxMana, 20, speed);

        private static CombatantState Foe(string name, int health = 1000, int speed = 1) =>
            new CombatantState(name, false, health, 0, 5, speed);

        private static FightSession Fight(CombatantState[] party, CombatantState[] foes,
            IReadOnlyList<ResolvedSkill> skills = null, ulong seed = 1)
        {
            var kits = party.Select(p =>
                new PlayerKit(p.Name, CharacterRole.Tank, skills, null, DamageType.Physical)).ToList();

            var session = new FightSession(new CombatEncounter(party, foes), kits, null, new SeededRandom(seed))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return session;
        }

        // A single-target spell that can only be aimed at the given 1-based
        // authored positions -- the shape skills.json's `reachSlots` resolves
        // to. Built locally rather than in Tests/EditMode/Shared: the reach
        // argument is the whole point of every skill in this file.
        private static ResolvedSkill RangedSkill(Reach reach, int manaCost = 10, int cooldown = 0) =>
            new ResolvedSkill("lob", "Lob", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, manaCost, 0, false, 0, 30, ignoresDefense: true,
                null, SpellPresentation.None, 0, cooldownTurns: cooldown, reach: reach);

        // ---- the value type, and the one place both conventions meet --------

        [Test]
        public void FromContentIsOneBasedAndRanksIsZeroBased()
        {
            // THE CONVERSION, pinned. Content says "positions 2 and 3"; the
            // engine means living ranks 1 and 2. Nothing else in the codebase
            // is allowed to do this subtraction.
            var authored = Reach.FromContent(new[] { 2, 3 });

            Assert.IsFalse(authored.Allows(0), "position 2 is not the front rank");
            Assert.IsTrue(authored.Allows(1));
            Assert.IsTrue(authored.Allows(2));
            Assert.AreEqual(Reach.Ranks(1, 2), authored,
                "the 1-based door and the 0-based door must land on the same value");
        }

        [Test]
        public void ADeadOrAbsentRankIsReachedByNothing()
        {
            Assert.IsFalse(Reach.Any.Allows(-1), "-1 is LivingRankOf's 'not on the field' answer");
            Assert.IsFalse(Reach.Melee.Allows(-1));
            Assert.IsFalse(Reach.Ranks(0, 1, 2).Allows(-1));
            Assert.IsTrue(Reach.Any.Allows(2), "Any is otherwise unrestricted");
        }

        [Test]
        public void MeleeAndAnAuthoredFrontOnlyReachAreNotEqualDespiteTheSameMask()
        {
            // The KIND is the load-bearing half: Monkey King's Scepter lifts
            // one of these and not the other (RelicMechanicsTests pins that
            // half), so a value type that compared only masks would make the
            // relic lift both.
            Assert.AreNotEqual(Reach.Melee, Reach.FromContent(new[] { 1 }));
            Assert.AreEqual(ReachKind.Melee, Reach.Melee.Kind);
            Assert.AreEqual(ReachKind.ExplicitRanks, Reach.FromContent(new[] { 1 }).Kind);
        }

        // ---- rank, and what death does to it --------------------------------

        [Test]
        public void DeathCompressesRanksAndSecondLifeRestoresThem()
        {
            var front = Hero("Front");
            var back = Hero("Back", speed: 1);
            var foe = Foe("Foe");
            var session = Fight(new[] { front, back }, new[] { foe });
            session.SecondLifeCharges = 1;

            Assert.AreEqual(0, session.Encounter.LivingRankOf(front));
            Assert.AreEqual(1, session.Encounter.LivingRankOf(back));

            front.CurrentHealth = 0;

            Assert.AreEqual(-1, session.Encounter.LivingRankOf(front), "a corpse holds no rank");
            Assert.AreEqual(0, session.Encounter.LivingRankOf(back), "the survivor compresses forward");
            CollectionAssert.AreEqual(new[] { back },
                session.EligibleTargets(foe, Reach.Melee).ToList(),
                "and the enemy's melee follows the compression, not the list");

            // What TrySecondLife does to a downed party: everyone back up, so
            // the ranks simply return to list order -- nobody moved.
            front.CurrentHealth = front.MaxHealth / 2;

            Assert.AreEqual(0, session.Encounter.LivingRankOf(front));
            Assert.AreEqual(1, session.Encounter.LivingRankOf(back));
            CollectionAssert.AreEqual(new[] { front },
                session.EligibleTargets(foe, Reach.Melee).ToList());
        }

        // ---- CanReach's own evaluation order --------------------------------

        [Test]
        public void AnAllyIsNeverReachableBySingleOpponentReach()
        {
            var hero = Hero();
            var ally = Hero("Ally", speed: 1);
            var session = Fight(new[] { hero, ally }, new[] { Foe("Foe") });

            Assert.IsFalse(session.CanReach(hero, Reach.Any, ally),
                "a single-opponent question asked about an ally is a caller bug, and false surfaces it");
            Assert.IsFalse(session.CanReach(hero, Reach.Any, hero));
        }

        [Test]
        public void ProvokeNarrowsEveryReachKindOntoTheProvoker()
        {
            // The back-rank taunt case: a Ranks(1,2) ability stays legal
            // because the provoker is the only legal target whatever the mask
            // said, and the front rank stops being one.
            var provoker = Hero("Provoker", speed: 1);
            var bystander = Hero("Bystander");
            var foe = Foe("Foe");
            var session = Fight(new[] { bystander, provoker }, new[] { foe });

            StatusEffects.Apply(foe.Statuses, StatusEffectType.Provoked, 0, 3, provoker);

            Assert.IsTrue(session.CanReach(foe, Reach.Melee, provoker),
                "the provoker is reachable from anywhere");
            Assert.IsTrue(session.CanReach(foe, Reach.Ranks(1, 2), provoker));
            Assert.IsFalse(session.CanReach(foe, Reach.Melee, bystander),
                "and everyone else stops being reachable, front rank included");
            CollectionAssert.AreEqual(new[] { provoker },
                session.EligibleTargets(foe, Reach.Melee).ToList());
        }

        // ---- enforcement in the commands ------------------------------------

        [Test]
        public void ExecuteAttackOnABackRankEnemyRefusesAndSpendsNothing()
        {
            var hero = Hero();
            var front = Foe("Front");
            var back = Foe("Back");
            var session = Fight(new[] { hero }, new[] { front, back });
            session.DrainBeats();
            session.DrainImmediateMessages();

            int backHealth = back.CurrentHealth;
            int frontHealth = front.CurrentHealth;

            bool accepted = session.ExecuteAttack(back);

            Assert.IsFalse(accepted);
            Assert.AreEqual(backHealth, back.CurrentHealth, "the blow never landed");
            Assert.AreEqual(frontHealth, front.CurrentHealth, "and it did not land on the front rank instead");
            CollectionAssert.IsEmpty(session.DrainBeats(), "a refused command opens no beat");
            Assert.AreSame(hero, session.Current, "and spends no turn");
            Assert.IsTrue(session.DrainImmediateMessages().Any(m => m.Contains("out of reach")));
        }

        [Test]
        public void CastSkillOutOfReachRefusesBeforeManaAndCooldown()
        {
            var hero = Hero();
            var front = Foe("Front");
            var back = Foe("Back");
            var skill = RangedSkill(Reach.Ranks(1, 2), manaCost: 10, cooldown: 2);
            var session = Fight(new[] { hero }, new[] { front, back }, new List<ResolvedSkill> { skill });
            session.DrainBeats();

            int mana = hero.CurrentMana;

            bool accepted = session.CastSkill(0, front);

            Assert.IsFalse(accepted);
            Assert.AreEqual(mana, hero.CurrentMana, "mana is checked AFTER reach, so nothing was paid");
            Assert.AreEqual(0, session.CooldownRemaining(hero, "lob"), "and no cooldown was started");
            Assert.AreEqual(front.MaxHealth, front.CurrentHealth);
            Assert.AreSame(hero, session.Current);
        }

        [Test]
        public void ARankRestrictedSkillWithOneLivingEnemyHasNoEligibleTargetAndItsRowIsDim()
        {
            var hero = Hero();
            var only = Foe("Only");
            var skill = RangedSkill(Reach.Ranks(1, 2));
            var session = Fight(new[] { hero }, new[] { only }, new List<ResolvedSkill> { skill });

            CollectionAssert.IsEmpty(session.EligibleTargets(hero, skill.Reach).ToList());
            Assert.IsFalse(session.CanReach(hero, skill.Reach, only));
            Assert.IsFalse(session.CastSkill(0, only), "and the command refuses the click that would have cast it");
        }

        [Test]
        public void AReachAnySkillStillLandsBehindALivingFrontRank()
        {
            // The control: every skill authored before reach existed resolves
            // to Reach.Any and must be completely unaffected by all of this.
            var hero = Hero();
            var front = Foe("Front");
            var back = Foe("Back");
            var skill = RangedSkill(Reach.Any, manaCost: 0);
            var session = Fight(new[] { hero }, new[] { front, back }, new List<ResolvedSkill> { skill });

            Assert.IsTrue(session.CastSkill(0, back));
            Assert.Less(back.CurrentHealth, back.MaxHealth);
            Assert.AreEqual(front.MaxHealth, front.CurrentHealth);
        }
    }
}
