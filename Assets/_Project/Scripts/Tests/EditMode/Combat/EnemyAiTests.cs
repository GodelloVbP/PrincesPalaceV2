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
    // What the monsters actually do on their turn.
    //
    // In v1 every one of these needed a loaded Gameplay scene and a real fight
    // played out through the UI, so the branches that make an enemy turn
    // interesting -- a skipped turn, a redirected one, the status rider -- were
    // covered only by whatever a PlayMode fight happened to walk through.
    public class EnemyAiTests
    {
        // Slow enough that the hero always opens, fast enough that a single
        // hero action is always followed by exactly one monster reply.
        private static CombatantState Hero(string name = "Hero", int health = 500, int speed = 10) =>
            new CombatantState(name, true, health, 20, 20, speed);

        private static CombatantState Monster(string name = "Golem", int health = 1000, int attack = 30) =>
            new CombatantState(name, false, health, 10, attack, 9);

        private static ResolvedEnemy Source(
            string id,
            string skillName = "",
            float skillChance = 0f,
            float skillPower = 2f,
            StatusEffectType? appliesStatus = null,
            int statusMagnitude = 0,
            int statusDuration = 0,
            bool attackHoldsPosition = false,
            string vfxPath = "",
            string sfxPath = "",
            DamageType attackType = DamageType.Physical) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0,
                skillName: skillName, skillPower: skillPower, skillChance: skillChance,
                presentation: SpellPresentation.Of(vfxPath, 0.6f, 3, sfxPath),
                appliesStatus: appliesStatus, statusMagnitude: statusMagnitude,
                statusDuration: statusDuration, attackHoldsPosition: attackHoldsPosition,
                attackType: attackType);

        // Variance off so a damage assertion is a fact. The roll has its own
        // tests, and a monster's swing is the one hit the player ever takes.
        private static FightSession Session(
            CombatEncounter encounter,
            IReadOnlyList<EnemyKit> enemyKits,
            IReadOnlyList<PlayerKit> playerKits = null) =>
            new FightSession(encounter, playerKits, enemyKits, new SeededRandom(7)) { DamageVarianceRange = 0f };

        // One hero, one monster, one exchange. Returns the beats the monster's
        // reply produced -- everything after the hero's own opening beat.
        private static (FightSession session, CombatantState hero, CombatantState monster) OneOnOne(
            ResolvedEnemy source, CombatantState hero = null, CombatantState monster = null)
        {
            hero = hero ?? Hero();
            monster = monster ?? Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(source, false) });
            session.Begin();
            return (session, hero, monster);
        }

        private static IReadOnlyList<CombatBeat> EnemyBeats(FightSession session) =>
            session.DrainBeats().Where(b => b.IsAction && !b.Actor.IsPlayerSide).ToList();

        private static IEnumerable<string> MessagesOf(IReadOnlyList<CombatBeat> beats) =>
            beats.SelectMany(b => b.Messages);

        // ---- the plain swing --------------------------------------------------

        [Test]
        public void AMonsterAttacksTheParty()
        {
            var (session, hero, _) = OneOnOne(Source("golem"));
            int before = hero.CurrentHealth;

            session.ExecuteAttack(session.Encounter.Enemies[0]);
            var beats = EnemyBeats(session);

            Assert.AreEqual(1, beats.Count, "exactly one reply");
            Assert.Less(hero.CurrentHealth, before);
            Assert.AreSame(hero, beats[0].Target);
            Assert.Greater(beats[0].Amount, 0, "the beat carries what it dealt");
            Assert.AreEqual(FightSession.Stances.Attack, beats[0].Stances[beats[0].Actor]);
            Assert.AreEqual(FightSession.Stances.Hurt, beats[0].Stances[hero],
                "a hit taken is visible on the stage, not only in the log");
        }

        // THE ATTACK-TYPE INTEGRATION TEST the balance-redesign plan's D1
        // calls for: an enemy authored with a non-Physical attackType must,
        // in a resolved fight, have its swing reduced by the target's
        // MagicalDefense and NOT PhysicalDefense. Before Phase 1, an enemy
        // had no way to reach MagicalDefense at all -- every monster's
        // attack was untyped Physical regardless of what it was authored as
        // -- which made MagicalDefense a dead stat against every enemy in
        // the game. See ActorAttackType's own comment, FightSession.Skills.cs.
        [Test]
        public void AnEnemyWithAnAuthoredAttackType_IsMetByMagicalDefenseNotPhysical()
        {
            var heavyPhysical = Hero();
            heavyPhysical.PhysicalDefense = 500;
            heavyPhysical.MagicalDefense = 0;
            var (physicalRun, _, _) = OneOnOne(Source("imp", attackType: DamageType.Fire), heavyPhysical);
            physicalRun.ExecuteAttack(physicalRun.Encounter.Enemies[0]);
            int damageAgainstAPhysicalWall = EnemyBeats(physicalRun)[0].Amount;

            var heavyMagical = Hero();
            heavyMagical.PhysicalDefense = 0;
            heavyMagical.MagicalDefense = 500;
            var (magicalRun, _, _) = OneOnOne(Source("imp", attackType: DamageType.Fire), heavyMagical);
            magicalRun.ExecuteAttack(magicalRun.Encounter.Enemies[0]);
            int damageAgainstAMagicalWall = EnemyBeats(magicalRun)[0].Amount;

            Assert.Less(damageAgainstAMagicalWall, damageAgainstAPhysicalWall,
                "a Fire-typed enemy's swing must be blunted by a wall of MagicalDefense, not shrug " +
                "it off the way it shrugs off a wall of PhysicalDefense -- if both walls blunted it " +
                "equally, MagicalDefense would still be the dead stat this integration test exists " +
                "to catch");
        }

        // The companion fact: an enemy that authors NOTHING still reads as
        // Physical, exactly as every enemy always did before this field
        // existed — the default this whole mechanism has to fall back to.
        [Test]
        public void AnEnemyWithNoAuthoredAttackType_IsStillMetByPhysicalDefense()
        {
            var heavyPhysical = Hero();
            heavyPhysical.PhysicalDefense = 500;
            var (session, _, _) = OneOnOne(Source("golem"), heavyPhysical);

            session.ExecuteAttack(session.Encounter.Enemies[0]);

            // Monster()'s default Attack (30) raw, softened by
            // PhysicalDefense 500: 30 x 100/(500+100) = 5.
            Assert.AreEqual(5, EnemyBeats(session)[0].Amount,
                "an untyped/Physical swing should be stopped by a wall of PhysicalDefense");
        }

        [Test]
        public void ASkillHitsHarderThanASwing_AndSaysWhichItWas()
        {
            var plainHero = Hero();
            var (plain, _, _) = OneOnOne(Source("golem"), plainHero);
            plain.ExecuteAttack(plain.Encounter.Enemies[0]);
            int plainDamage = EnemyBeats(plain)[0].Amount;

            var skillHero = Hero();
            var (skilled, _, _) = OneOnOne(
                Source("golem", skillName: "Boulder Slam", skillChance: 1f, skillPower: 2f), skillHero);
            skilled.ExecuteAttack(skilled.Encounter.Enemies[0]);
            var beats = EnemyBeats(skilled);

            Assert.Greater(beats[0].Amount, plainDamage);
            Assert.IsTrue(MessagesOf(beats).Any(m => m.Contains("uses Boulder Slam")));
            Assert.AreEqual(FightSession.Stances.Cast, beats[0].Stances[beats[0].Actor]);
        }

        [Test]
        public void ASkillRecordsItsOwnPresentation()
        {
            var (session, _, _) = OneOnOne(Source("golem",
                skillName: "Boulder Slam", skillChance: 1f, vfxPath: "Vfx/rock", sfxPath: "Sfx/slam"));

            session.ExecuteAttack(session.Encounter.Enemies[0]);
            var beat = EnemyBeats(session)[0];

            Assert.AreEqual("Vfx/rock", beat.Vfx.path);
            Assert.AreEqual("Sfx/slam", beat.Vfx.sfxPath);
            Assert.IsTrue(beat.HasSpellAnimation);
        }

        [Test]
        public void AMonsterWhoseAttackArtIsStationaryDoesNotLunge()
        {
            // The golem's "attack" pose is a ground slam with earth spikes, not
            // a forward strike; without this the lunge made it read as flying.
            var (session, _, _) = OneOnOne(Source("golem", attackHoldsPosition: true));

            session.ExecuteAttack(session.Encounter.Enemies[0]);

            Assert.IsTrue(EnemyBeats(session)[0].ActorHoldsPosition);
        }

        [Test]
        public void AStationaryAttackPoseSaysNothingAboutSkills()
        {
            // A skill holds position through its cast stance already; the flag
            // is about the PLAIN attack and must not force the wrong pose.
            var (session, _, _) = OneOnOne(Source("golem",
                skillName: "Boulder Slam", skillChance: 1f, attackHoldsPosition: false));

            session.ExecuteAttack(session.Encounter.Enemies[0]);
            var beat = EnemyBeats(session)[0];

            Assert.AreEqual(FightSession.Stances.Cast, beat.Stances[beat.Actor]);
        }

        // ---- turns that do not happen ----------------------------------------

        [Test]
        public void ABrokenGuardCostsExactlyOneTurn()
        {
            var monster = Monster();
            monster.BreakShield = new BreakShield(1);
            var (session, hero, _) = OneOnOne(Source("golem"), monster: monster);
            monster.BreakShield.Deplete(monster.BreakShield.Max);
            Assert.IsTrue(monster.BreakShield.IsBroken, "fixture: the guard really is broken");

            int before = hero.CurrentHealth;
            session.ExecuteAttack(monster);
            var beats = EnemyBeats(session);

            Assert.AreEqual(hero.CurrentHealth, before, "the turn was skipped, not weakened");
            Assert.IsTrue(MessagesOf(beats).Any(m => m.Contains("still reeling")));
            Assert.IsFalse(monster.BreakShield.IsBroken, "and the skip is spent here, not on a timer");
        }

        [Test]
        public void AStunCostsExactlyOneTurn()
        {
            var (session, hero, monster) = OneOnOne(Source("golem"));
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Stun, 0, 5);

            int before = hero.CurrentHealth;
            session.ExecuteAttack(monster);

            Assert.AreEqual(hero.CurrentHealth, before);
            Assert.IsTrue(MessagesOf(EnemyBeats(session)).Any(m => m.Contains("stunned")));
            Assert.IsFalse(StatusEffects.HasStun(monster.Statuses), "consumed the instant it is spent");
        }

        [Test]
        public void BothReasonsAtOnceAreReportedTogether()
        {
            // Either or both can be true, and the player should see why -- two
            // separate lines would read as two lost turns.
            var monster = Monster();
            monster.BreakShield = new BreakShield(1);
            var (session, _, _) = OneOnOne(Source("golem"), monster: monster);
            monster.BreakShield.Deplete(monster.BreakShield.Max);
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Stun, 0, 5);

            session.ExecuteAttack(monster);
            var lines = MessagesOf(EnemyBeats(session)).ToList();

            Assert.AreEqual(1, lines.Count(m => m.Contains("cannot act")));
            Assert.IsTrue(lines.Any(m => m.Contains("stunned AND still reeling")));
        }

        [Test]
        public void AMonsterKilledByItsOwnPoisonTickNeverSwings()
        {
            // The tick happens at the top of the monster's turn, before the AI
            // resolves anything. CombatEncounter only re-checks who is alive
            // inside AdvanceTurn, so without the liveness guard a dead monster
            // still gets one more swing on the strength of having been alive
            // when the schedule picked it.
            //
            // THE TANK STANDS IN FRONT, which it did not have to before the
            // front-rank rule bound the player's own swing: the hero's attack
            // is only here to hand the turn on, and aiming it past a living
            // front rank is now refused outright.
            var hero = Hero();
            var tank = Monster("Tank", health: 1000);
            var doomed = Monster("Doomed", health: 1000);
            var encounter = new CombatEncounter(new[] { hero }, new[] { tank, doomed });
            var session = Session(encounter, new List<EnemyKit>
            {
                new EnemyKit(Source("tank"), false),
                new EnemyKit(Source("doomed"), false),
            });
            session.Begin();

            doomed.CurrentHealth = 1;
            StatusEffects.Apply(doomed.Statuses, StatusEffectType.Poison, 50, 5);

            session.ExecuteAttack(tank);
            var beats = EnemyBeats(session);

            Assert.IsFalse(doomed.IsAlive);
            Assert.IsFalse(beats.Any(b => ReferenceEquals(b.Actor, doomed)),
                "a corpse does not get a swing");
        }

        // ---- who gets hit -----------------------------------------------------

        [Test]
        public void ATauntOverridesTheAIsOwnPick()
        {
            var taunter = Hero("Taunter");
            // Slow, so the monster acts before the party comes round again and the
            // exchange is one hero action against one monster reply.
            var bystander = Hero("Bystander", speed: 1);
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { taunter, bystander }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(Source("golem"), false) });
            session.Begin();
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Provoked, 0, 5, taunter);

            int untouched = bystander.CurrentHealth;
            session.ExecuteAttack(monster);
            var beats = EnemyBeats(session);

            Assert.AreSame(taunter, beats[0].Target);
            Assert.AreEqual(untouched, bystander.CurrentHealth);
            Assert.IsTrue(MessagesOf(beats).Any(m => m.Contains("can see nothing but")));
        }

        [Test]
        public void ATauntIsSpentByTheTurnItRedirected()
        {
            // One redirected turn, not a countdown.
            var taunter = Hero("Taunter");
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { taunter }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(Source("golem"), false) });
            session.Begin();
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Provoked, 0, 99, taunter);

            session.ExecuteAttack(monster);

            Assert.IsNull(StatusEffects.ProvokedBy(monster), "spent, despite 99 turns of duration left");
        }

        [Test]
        public void ADeadTaunterDoesNotKeepPullingAggro()
        {
            var taunter = Hero("Taunter");
            var survivor = Hero("Survivor", speed: 1);
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { taunter, survivor }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(Source("golem"), false) });
            session.Begin();
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Provoked, 0, 5, taunter);
            taunter.CurrentHealth = 0;

            session.ExecuteAttack(monster);

            Assert.AreSame(survivor, EnemyBeats(session)[0].Target);
        }

        [Test]
        public void AGoadedMonsterSwingsWide()
        {
            // Provoke T2. Applied to the PAIR, so it blunts the blow aimed at
            // the taunter and nothing else.
            var plainHero = Hero();
            var (plain, _, _) = OneOnOne(Source("golem"), plainHero);
            plain.ExecuteAttack(plain.Encounter.Enemies[0]);
            int fullDamage = EnemyBeats(plain)[0].Amount;

            var taunter = Hero("Taunter");
            var (goaded, _, monster) = OneOnOne(Source("golem"), taunter);
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Provoked, 40, 5, taunter);

            goaded.ExecuteAttack(monster);

            Assert.Less(EnemyBeats(goaded)[0].Amount, fullDamage);
        }

        // ---- riders on a landed hit -------------------------------------------

        [Test]
        public void AMonsterWhoseClawsBleedAppliesItOnAPlainAttack()
        {
            // Deliberately NOT skill-gated, unlike the VFX: a monster whose
            // whole gimmick is a status applies it on every landed hit.
            var (session, hero, monster) = OneOnOne(
                Source("rat", appliesStatus: StatusEffectType.Poison, statusMagnitude: 5, statusDuration: 3));

            session.ExecuteAttack(monster);

            Assert.IsTrue(hero.Statuses.Any(s => s.Type == StatusEffectType.Poison));
            Assert.IsTrue(MessagesOf(EnemyBeats(session)).Any(m => m.Contains("afflicted")));
        }

        [Test]
        public void ADeadTargetIsNotAfflicted()
        {
            var frail = Hero("Frail", health: 500);
            var (session, _, monster) = OneOnOne(
                Source("rat", appliesStatus: StatusEffectType.Poison, statusMagnitude: 5, statusDuration: 3),
                frail);
            frail.CurrentHealth = 1;

            session.ExecuteAttack(monster);

            Assert.IsFalse(frail.IsAlive);
            Assert.IsFalse(frail.Statuses.Any(s => s.Type == StatusEffectType.Poison),
                "poisoning a corpse is a status the player can never see resolve");
            Assert.IsTrue(MessagesOf(EnemyBeats(session)).Any(m => m.Contains("is defeated")));
        }

        [Test]
        public void BeingGroundDownIsItselfAWayToBuild()
        {
            // Granted per HIT rather than per point of damage, so a swarm of
            // weak attackers is not a better generator than one real threat.
            var hero = Hero();
            hero.SignaturePool = new ResourcePool("wool", "Wool", 16,
                gainPerTurn: 0, gainOnAttack: 0, gainOnDamageTaken: 2);
            var (session, _, monster) = OneOnOne(Source("golem"), hero);

            session.ExecuteAttack(monster);

            Assert.AreEqual(2, hero.SignaturePool.Current);
        }

        [Test]
        public void TheTargetSaysSomethingAboutTheBlow()
        {
            // Domain picks the LINE; Core owns the catalog. The choice depends
            // on health AT THE MOMENT THE BLOW LANDS, which is why it cannot
            // wait for playback.
            var hero = Hero();
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter,
                new List<EnemyKit> { new EnemyKit(Source("golem"), false) },
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) });
            session.Begin();

            session.ExecuteAttack(monster);
            var beat = EnemyBeats(session)[0];

            Assert.IsTrue(beat.HasTargetVoice);
            Assert.AreEqual("shawn", beat.TargetVoiceId);
            Assert.AreEqual(VoiceLine.Hurt, beat.TargetVoiceLine);
            Assert.IsFalse(beat.TargetVoiceOncePerFight, "a grunt is an event and repeats");
        }

        [Test]
        public void GoingDownOutranksEverythingElse()
        {
            // A character does not grunt and then die, they just die.
            var hero = Hero();
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter,
                new List<EnemyKit> { new EnemyKit(Source("golem"), false) },
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) });
            session.Begin();
            hero.CurrentHealth = 1;

            session.ExecuteAttack(monster);
            var beat = EnemyBeats(session)[0];

            Assert.AreEqual(VoiceLine.Down, beat.TargetVoiceLine);
        }

        [Test]
        public void NearlyDeadIsSaidOncePerFight()
        {
            // A threshold, not an event.
            var hero = Hero();
            var monster = Monster(attack: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter,
                new List<EnemyKit> { new EnemyKit(Source("golem"), false) },
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) });
            session.Begin();
            // Just at the threshold before the blow, and a blow small enough that
            // they are still standing under it afterwards -- the whole point is
            // the line between Hurt and Down.
            hero.CurrentHealth = (int)(hero.MaxHealth * VoiceThresholds.LowHealthFraction);

            session.ExecuteAttack(monster);
            var beat = EnemyBeats(session)[0];

            Assert.AreEqual(VoiceLine.LowHealth, beat.TargetVoiceLine);
            Assert.IsTrue(beat.TargetVoiceOncePerFight);
        }

        [Test]
        public void AMonsterWithNoKitStillTakesAnOrdinaryTurn()
        {
            // Graceful degradation: an enemy the session was never handed a
            // source for swings rather than throwing or standing idle.
            var hero = Hero();
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter, null);
            session.Begin();

            int before = hero.CurrentHealth;
            session.ExecuteAttack(monster);

            Assert.Less(hero.CurrentHealth, before);
            Assert.AreEqual(1, EnemyBeats(session).Count);
        }
    }
}
