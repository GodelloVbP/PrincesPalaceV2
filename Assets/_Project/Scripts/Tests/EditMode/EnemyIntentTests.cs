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
    // What a monster tells the player it is about to do, and when that is shown.
    //
    // The telegraph is the only part of enemy behaviour the player can plan
    // around, so the two restrictions on it -- commit once, show only on the
    // player's turn -- are the whole feature. Both were comments in v1 and
    // nothing else.
    public class EnemyIntentTests
    {
        private static CombatantState Hero() =>
            new CombatantState("Hero", true, 500, 10, 20, 0, 10);

        private static CombatantState Monster(string name, int health = 1000) =>
            new CombatantState(name, false, health, 10, 5, 0, 9);

        private static ResolvedEnemy Source(string id, string skillName, float skillChance) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0,
                skillName: skillName, skillPower: 2f, skillChance: skillChance);

        private static FightSession Session(CombatEncounter encounter, IReadOnlyList<EnemyKit> kits, ulong seed = 1) =>
            new FightSession(encounter, null, kits, new SeededRandom(seed)) { DamageVarianceRange = 0f };

        // skillChance 1 and 0 make the roll's outcome a fact rather than a
        // sample. Which VALUES the roll produces is SeededRandom's own test.
        private static (FightSession session, CombatEncounter encounter) Fight(float skillChance, string skillName = "Boulder Slam")
        {
            var monster = Monster("Golem");
            var encounter = new CombatEncounter(new[] { Hero() }, new[] { monster });
            var kits = new List<EnemyKit> { new EnemyKit(Source("golem", skillName, skillChance), false) };
            return (Session(encounter, kits), encounter);
        }

        [Test]
        public void AnEnemyThatWillUseItsSkillSaysSo()
        {
            var (session, encounter) = Fight(skillChance: 1f);

            session.PrepareEnemyIntents();

            Assert.AreEqual("Boulder Slam", session.IntentFor(encounter.Enemies[0]));
        }

        [Test]
        public void AnEnemyThatWillJustSwingDeclaresAPlainAttack()
        {
            var (session, encounter) = Fight(skillChance: 0f);

            session.PrepareEnemyIntents();

            Assert.AreEqual(FightSession.IntentAttack, session.IntentFor(encounter.Enemies[0]));
        }

        [Test]
        public void AnEnemyWithNoSkillAuthoredNeverDeclaresOne()
        {
            // skillChance 1 with a blank name: "has a skill" is the NAME, not
            // the chance, or a content mistake becomes a nameless telegraph.
            var (session, encounter) = Fight(skillChance: 1f, skillName: "");

            session.PrepareEnemyIntents();

            Assert.AreEqual(FightSession.IntentAttack, session.IntentFor(encounter.Enemies[0]));
        }

        [Test]
        public void AnIntentIsNotReRolledBehindThePlayersBack()
        {
            // Preparing twice must not re-decide. The player reads the
            // nameplate and commits to a plan; changing the answer underneath
            // them makes the telegraph worse than none at all.
            var (session, encounter) = Fight(skillChance: 0.5f);
            var monster = encounter.Enemies[0];

            session.PrepareEnemyIntents();
            string first = session.IntentFor(monster);

            for (int i = 0; i < 20; i++)
            {
                session.PrepareEnemyIntents();
            }

            Assert.AreEqual(first, session.IntentFor(monster));
        }

        [Test]
        public void OnlySkillsAreTelegraphed()
        {
            // "Intent: Attack" on every enemy every turn is noise the player
            // learns to stop reading, which buries the one line that changes a
            // decision. A blank nameplate means nothing special is coming.
            var (session, encounter) = Fight(skillChance: 0f);
            session.PrepareEnemyIntents();

            Assert.AreEqual("", session.TelegraphSuffix(encounter.Enemies[0], isPlayerTurn: true));
        }

        [Test]
        public void ASkillIsTelegraphedOnItsOwnLine()
        {
            var (session, encounter) = Fight(skillChance: 1f);
            session.PrepareEnemyIntents();

            Assert.AreEqual("\nBoulder Slam!", session.TelegraphSuffix(encounter.Enemies[0], isPlayerTurn: true));
        }

        [Test]
        public void NothingIsTelegraphedWhileAnEnemyTurnIsBeingShown()
        {
            // A round resolves in full and is then played back, so by the time
            // an enemy's blow is animating the session already holds its
            // commitment for the NEXT turn. Rendering it mid-playback would
            // telegraph the wrong turn entirely.
            var (session, encounter) = Fight(skillChance: 1f);
            session.PrepareEnemyIntents();

            Assert.AreEqual("", session.TelegraphSuffix(encounter.Enemies[0], isPlayerTurn: false));
        }

        [Test]
        public void AnEnemyWithNothingDeclaredTelegraphsNothing()
        {
            var (session, encounter) = Fight(skillChance: 1f);

            Assert.IsNull(session.IntentFor(encounter.Enemies[0]));
            Assert.AreEqual("", session.TelegraphSuffix(encounter.Enemies[0], isPlayerTurn: true));
        }

        [Test]
        public void ADeadEnemyIsNeverAskedForAnIntent()
        {
            var hero = Hero();
            var dead = Monster("Corpse", health: 1000);
            var alive = Monster("Standing");
            var encounter = new CombatEncounter(new[] { hero }, new[] { dead, alive });
            var kits = new List<EnemyKit>
            {
                new EnemyKit(Source("corpse", "Grave Slam", 1f), false),
                new EnemyKit(Source("standing", "Boulder Slam", 1f), false),
            };
            var session = Session(encounter, kits);
            dead.CurrentHealth = 0;

            session.PrepareEnemyIntents();

            Assert.IsNull(session.IntentFor(dead));
            Assert.AreEqual("Boulder Slam", session.IntentFor(alive));
        }

        [Test]
        public void TakingTheTurnSpendsTheCommitment()
        {
            // Consumed by the turn it described. Left in place, the nameplate
            // would keep telegraphing a blow that has already landed.
            var (session, encounter) = Fight(skillChance: 1f);
            var monster = encounter.Enemies[0];

            session.Begin();
            session.ExecuteAttack(monster);

            // Whatever is declared now is the NEXT turn's commitment, freshly
            // rolled -- not the one that was just spent.
            Assert.AreEqual("Boulder Slam", session.IntentFor(monster),
                "re-rolled at 100% chance, so equal by value and not by identity");
            Assert.IsTrue(session.DrainBeats()
                .SelectMany(b => b.Messages)
                .Any(m => m.Contains("Boulder Slam")), "and the spent one was actually used");
        }

        [Test]
        public void ASkippedTurnAlsoSpendsTheCommitment()
        {
            // The monster declared it and then could not deliver, so re-rolling
            // next turn is honest -- the alternative is a telegraph the player
            // has already watched fail to happen.
            var (session, encounter) = Fight(skillChance: 1f);
            var monster = encounter.Enemies[0];

            session.Begin();
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Stun, 0, 2);
            session.ExecuteAttack(monster);

            Assert.IsTrue(session.DrainBeats()
                .SelectMany(b => b.Messages)
                .Any(m => m.Contains("stunned")));
        }
    }
}
