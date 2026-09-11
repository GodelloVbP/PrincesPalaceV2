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
            new CombatantState("Hero", true, 500, 10, 20, 10);

        private static CombatantState Monster(string name, int health = 1000) =>
            new CombatantState(name, false, health, 10, 5, 9);

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
            // NOTE ON THIS TEST'S RELATIONSHIP TO THE STUN TELEGRAPH FIX
            // (see FightSession.Enemies.cs's PHASE D3b comment, and
            // AStunnedEnemyWithARealSkill_TelegraphsAForfeit_NotAFakeSkill
            // below): Stun lands here AFTER session.Begin() has already
            // committed "Boulder Slam" via BuildIntent, at a moment the
            // monster genuinely was not yet stunned -- so that commitment was
            // truthful when it was made, and this test was never actually
            // pinning the fraudulent-telegraph bug (verified by reading, not
            // assumed). What it DOES validly test, and still does after the
            // fix: a mid-round Stun still forces the skip, still messages the
            // player, and still discards the stale commitment rather than
            // leaving it to be reused -- confirmed below by re-checking the
            // NEXT intent is a freshly-rolled real one (Stun is fully consumed
            // by one skip, per StatusEffects.ConsumeStun's own comment, so the
            // monster is telegraphed truthfully again for its next turn).
            var (session, encounter) = Fight(skillChance: 1f);
            var monster = encounter.Enemies[0];

            session.Begin();
            Assert.AreEqual("Boulder Slam", session.IntentFor(monster),
                "true when committed -- the monster was not yet stunned");

            StatusEffects.Apply(monster.Statuses, StatusEffectType.Stun, 0, 2);
            session.ExecuteAttack(monster);

            Assert.IsTrue(session.DrainBeats()
                .SelectMany(b => b.Messages)
                .Any(m => m.Contains("stunned")));

            Assert.AreEqual("Boulder Slam", session.IntentFor(monster),
                "the stale commitment was spent, not reused -- this is a fresh roll for a " +
                "monster Stun has now fully released (ConsumeStun removes it outright)");
        }

        // ---- PHASE D3b FIX: BuildIntent and Stun -------------------------------
        //
        // Stun forfeits a turn UNCONDITIONALLY (ResolveSkippedTurn's isStunned
        // check never looks at the enemy's pool), unlike Rooted, which only
        // forfeits when the pool is genuinely empty. BuildIntent had no Stun
        // check at all: an enemy stunned BEFORE its intent was ever drawn
        // still had a real skill/attack rolled and telegraphed here, promising
        // an attack that ResolveSkippedTurn was always going to cancel. See
        // FightSession.Enemies.cs's own comment on the fix.

        [Test]
        public void AStunnedEnemyWithARealSkill_TelegraphsAForfeit_NotAFakeSkill()
        {
            // skillChance 1f, matching ASkippedTurnAlsoSpendsTheCommitment's own
            // setup: a real, affordable, certain-to-be-picked skill sits in the
            // pool. The only difference is WHEN Stun lands -- here, before the
            // draw ever happens, which is the case BuildIntent must catch.
            var (session, encounter) = Fight(skillChance: 1f);
            var monster = encounter.Enemies[0];
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Stun, 0, 2);

            session.PrepareEnemyIntents();

            Assert.AreEqual(FightSession.IntentForfeit, session.IntentFor(monster),
                "a stunned enemy must telegraph a forfeit, not the real skill it would " +
                "otherwise have drawn -- Stun forfeits the turn regardless of what is in the pool");

            var detail = session.IntentDetailFor(monster);
            Assert.IsTrue(detail.HasValue, "a forfeit is still a committed intent, not a missing one");
            Assert.AreEqual(0, detail.Value.ExpectedDamage,
                "no damage may be previewed for a turn that will not actually happen");
            Assert.AreNotEqual(EnemyIntentKind.Attack, detail.Value.Kind,
                "the badge must not read as an ordinary threat when nothing is coming");
        }

        // ---- PHASE D3 FIX: BuildIntent's -1 fallback ---------------------------
        //
        // EnemyAbilityDraw.Pick returns -1 for two DIFFERENT reasons: a genuinely
        // mis-authored pool (every weight zeroed by content mistake), and a
        // Rooted enemy whose only entry EffectivePoolFor itself zeroed. BuildIntent
        // used to treat both the same way -- swing anyway -- which telegraphed a
        // full-power Attack for a turn Rooted was always going to forfeit at
        // resolution. See FightSession.Enemies.cs's own comment on the fix.

        [Test]
        public void ARootedEnemyWithNoLegalSkill_TelegraphsAForfeit_NotAFakeAttack()
        {
            var (session, encounter) = Fight(skillChance: 0f);
            var monster = encounter.Enemies[0];
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Rooted, 0, 5);

            session.PrepareEnemyIntents();

            Assert.AreEqual(FightSession.IntentForfeit, session.IntentFor(monster),
                "a Rooted enemy with nothing else to cast must telegraph a forfeit, not \"Attack\"");

            var detail = session.IntentDetailFor(monster);
            Assert.IsTrue(detail.HasValue, "a forfeit is still a committed intent, not a missing one");
            Assert.AreEqual(0, detail.Value.ExpectedDamage,
                "no damage may be previewed for a turn that will not actually happen");
            Assert.AreNotEqual(EnemyIntentKind.Attack, detail.Value.Kind,
                "the badge must not read as an ordinary threat when nothing is coming");
        }

        // AND NOTHING IS PUT ON THE NAMEPLATE FOR IT. IntentForfeit is "a
        // sentinel before it is a label", and TelegraphSuffix printed it raw:
        // "Forfeits Turn!", in the slot its own header reserves for "something
        // special is coming", styled exactly like "Roar!" and "Grapple!".
        [Test]
        public void AForfeitIsNotTelegraphedAsThoughItWereAThreat()
        {
            var (session, encounter) = Fight(skillChance: 0f);
            var monster = encounter.Enemies[0];
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Rooted, 0, 5);

            session.PrepareEnemyIntents();
            Assert.AreEqual(FightSession.IntentForfeit, session.IntentFor(monster), "fixture: it is forfeiting");

            Assert.AreEqual("", session.TelegraphSuffix(monster, isPlayerTurn: true),
                "a monster that can do nothing threatens nothing, so the nameplate stays blank");
        }

        [Test]
        public void AGenuinelyMisauthoredZeroWeightPool_StillSwingsAnyway()
        {
            // The ORIGINAL case the -1 fallback exists for -- no Rooted
            // involved, just an author who zeroed every weight in the pool.
            // The fix must not remove this safety net, only gate a new branch
            // ahead of it: a monster standing frozen for a content bug reads
            // as the fight being broken, which is worse than the swing being
            // wrong.
            var monster = Monster("Golem");
            var encounter = new CombatEncounter(new[] { Hero() }, new[] { monster });
            var skill = new ResolvedSkill("thorn", "Thorn", "", "monster", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0);
            var kits = new List<EnemyKit>
            {
                new EnemyKit(Source("golem", "unused", 0f), false, new List<EnemyAbility>
                {
                    EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 0f),
                    EnemyAbility.Of(skill, 0f),
                }),
            };
            var session = Session(encounter, kits);

            session.PrepareEnemyIntents();

            Assert.AreEqual(FightSession.IntentAttack, session.IntentFor(monster),
                "not Rooted -- the original 'swing anyway' safety net must still fire");

            var detail = session.IntentDetailFor(monster);
            Assert.IsTrue(detail.HasValue);
            Assert.AreEqual(EnemyIntentKind.Attack, detail.Value.Kind);
            Assert.Greater(detail.Value.ExpectedDamage, 0,
                "the safety-net swing must still preview real damage, exactly as before this fix");
        }

        // ---- a summon the stage can no longer accept ------------------------------

        [Test]
        public void AFullStageZeroWeightsASummonAbility()
        {
            // FightSession.Skills.cs:383-387 states the whole point of the
            // draw-time check: "THE CAP IS ALSO CHECKED AT DRAW TIME (see
            // PrepareEnemyIntents' EffectivePoolFor), which is what stops the
            // boss from visibly winding up for a call that then does nothing
            // turn after turn." It checks the wrong cap.
            //
            // EffectivePoolFor only knows SummonCap, which counts the LIVING.
            // The refusal that actually fires is CombatEncounter.TryAddEnemy's
            // _enemies.Count >= maxSlots -- and _enemies NEVER SHRINKS
            // (CombatEncounter.cs:21-23), so the stage cap counts every body
            // ever fielded, corpses included. Start at two, summon one, and the
            // third slot is gone for the rest of the fight -- while the living
            // count drops back under the per-id cap every time the player
            // clears one, re-arming the ability at full weight.
            var hero = new CombatantState("Shawn", true, 100000, 30, 5, 1);
            var warden = new CombatantState("Warden", false, 100000, 10, 1, 20);
            var mate = new CombatantState("Mate", false, 100000, 10, 1, 20);
            var encounter = new CombatEncounter(new[] { hero }, new[] { warden, mate });

            int summonsMade = 0;
            FightSession.SummonFactory factory =
                (string id, out CombatantState state, out EnemyKit kit) =>
                {
                    summonsMade++;
                    state = new CombatantState("Rat" + summonsMade, false, 1, 0, 1, 1);
                    kit = new EnemyKit(SummonSource("rat"), false);
                    return true;
                };

            // summonCap 2 is the shipped forest_warden Roar row; the stage cap
            // (FightHudSpec.StageSlotsPerSide, 3) is the one that bites.
            var roar = new ResolvedSkill("roar", "Roar", "", "forest_warden", 1,
                SkillEffect.Summon, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, summonEnemyId: "rat", summonCap: 2);

            var kits = new List<EnemyKit>
            {
                new EnemyKit(SummonSource("forest_warden"), false,
                    new List<EnemyAbility> { EnemyAbility.Of(roar, 1f) }),
                new EnemyKit(SummonSource("mate"), false),
            };

            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) },
                kits, new SeededRandom(3), summonFactory: factory) { DamageVarianceRange = 0f };
            session.Begin();

            // Nothing here can die: the driver just hands the turn round and
            // round so the warden gets many draws off a stage that is full
            // after the first one.
            for (int i = 0; i < 12 && !session.IsOver; i++)
            {
                if (session.IsPlayerTurn) session.ExecuteAttack(encounter.Enemies[0]);
                else session.AutoResolveEnemyTurns();
            }

            var lines = session.DrainBeats().SelectMany(b => b.Messages).ToList();

            Assert.AreEqual(1, summonsMade,
                "the warden kept being drawn for a call the stage could never accept");
            Assert.IsFalse(lines.Any(l => l.Contains("no room left on the field")),
                "the warden wound up for a Roar that fizzled");
        }

        private static ResolvedEnemy SummonSource(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 1, 1, false,
                DamageType.Physical, DamageType.Physical, 0);
    }
}
