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
    // THORN TITHE (plan 2.12) AND THE POST-ACTION HOOK (plan 1.11), milestone
    // E. Almost every test here is about the HOOK rather than about the
    // spell's own two lines of resolution (ResolveAfflict, unchanged) --
    // Censer of Embers proved the "content and no code" half of Afflict
    // already; this file proves the retaliation seam that Thorn Tithe alone
    // needs.
    //
    // THE HOOK'S PLACEMENT IS THE MILESTONE'S OWN CORRECTION: the plan's
    // first draft put it at the top of AdvanceAfterAction, which only the
    // four PLAYER commands ever reach. Thorn Tithe is cast on ENEMIES, so
    // every test below that drives an enemy's own physical action goes
    // through FightSession.Enemies.AutoResolveEnemyTurns -- see
    // TriggerPhysicalMoveRetaliation's own header in FightSession.Riders.cs.
    public class ThornTitheTests
    {
        // The same speed gap VelvetShacklesTests uses, for the same reason:
        // the hero always opens, and one hero action is followed by exactly
        // one monster reply.
        private static CombatantState Hero(string name = "Hero", int health = 500, int speed = 10) =>
            new CombatantState(name, true, health, 50, 20, speed);

        private static CombatantState Monster(string name = "Monster", int health = 1000, int speed = 9) =>
            new CombatantState(name, false, health, 0, 15, speed);

        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

        // A NON-PHYSICAL FILLER for the hero's own turn -- Self-targeted so it
        // never touches the monster's health, which would confound every
        // assertion below about what the RETALIATION alone did.
        private static ResolvedSkill FillerHeal(int mana = 0) =>
            new ResolvedSkill("filler_heal", "Filler", "", "sheep", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, mana, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0, physicalMove: false);

        // A cast ability for the monster's own kit -- NON-physical, so a
        // monster that only ever draws this one never completes a physical
        // move at all.
        private static ResolvedSkill MonsterCast(int mana = 0) =>
            new ResolvedSkill("bog_mud_burst", "Mud Burst", "", "bog_witch", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, mana, 0, false, 0, 5, false,
                null, SpellPresentation.None, 0, physicalMove: false);

        private static IReadOnlyList<CombatBeat> EnemyBeats(IReadOnlyList<CombatBeat> beats) =>
            beats.Where(b => b.Actor != null && !b.Actor.IsPlayerSide).ToList();

        private static IEnumerable<string> MessagesOf(IReadOnlyList<CombatBeat> beats) =>
            beats.SelectMany(b => b.Messages);

        private static void GuaranteeDodge(CombatantState target) =>
            target.ModifierEffects = new ModifierEffectSet(
                new[] { new ModifierEffect(ModifierEffectType.DodgeRating, 100_000) });

        // ---- 1.11: a miss still triggers retaliation (dispatcher) --------------

        [Test]
        public void APhysicalSwingThatMisses_StillPaysTheTithe()
        {
            var hero = Hero();
            GuaranteeDodge(hero);
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { FillerHeal() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(31)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Thorned, 5, 3, hero);
            int foeHealthBefore = foe.CurrentHealth;

            Assert.IsTrue(session.CastSkill(0, hero), "the filler cast must be accepted");

            var lines = MessagesOf(session.DrainBeats()).ToList();
            Assert.IsTrue(lines.Any(m => m.Contains("misses")), "fixture check: the swing must miss");
            Assert.AreEqual(foeHealthBefore - 10, foe.CurrentHealth,
                "5 from the turn-start tick and 5 from the retaliation -- the owner's rule that a miss still pays it");
            Assert.IsTrue(lines.Any(m => m.Contains("thorns lash back")),
                "and the player is told why");
        }

        // ---- 1.11: an interrupted/forfeited action does not, but the opening
        //      tick still lands -------------------------------------------------

        [Test]
        public void AForfeitedTurn_PaysNoTithe_ButStillTakesTheOpeningTick()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { FillerHeal() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(37)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Thorned, 5, 3, hero);
            StatusEffects.Apply(foe.Statuses, StatusEffectType.Stun, 0, 1);
            int foeHealthBefore = foe.CurrentHealth;

            Assert.IsTrue(session.CastSkill(0, hero));

            var lines = MessagesOf(session.DrainBeats()).ToList();
            Assert.AreEqual(foeHealthBefore - 5, foe.CurrentHealth,
                "the opening tick still fires -- the turn-start clock does not care whether the turn resolves");
            Assert.IsFalse(lines.Any(m => m.Contains("thorns lash back")),
                "no action completed, so no retaliation -- a forfeit is not a physical move");
        }

        // ---- 1.11: the retaliation is not itself a physical move ---------------

        [Test]
        public void ATitheRetaliation_DoesNotRetriggerItself()
        {
            var hero = Hero();
            GuaranteeDodge(hero);
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { FillerHeal() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(41)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Thorned, 5, 3, hero);

            Assert.IsTrue(session.CastSkill(0, hero));

            var lines = MessagesOf(session.DrainBeats()).ToList();
            int retaliations = lines.Count(m => m.Contains("thorns lash back"));
            Assert.AreEqual(1, retaliations,
                "one physical move, one retaliation -- the retaliation itself must not count as a second one");
        }

        // ---- 1.11: three affected turns, three opportunities, third included --

        [Test]
        public void ThreeAffectedTurns_OfferThreeRetaliations_TheThirdIncluded()
        {
            var hero = Hero();
            GuaranteeDodge(hero);
            var foe = Monster(health: 10000);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { FillerHeal() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(43)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Thorned, 5, 3, hero);

            var lines = new List<string>();
            for (int round = 0; round < 3; round++)
            {
                Assert.IsTrue(session.CastSkill(0, hero), $"round {round}: the filler cast must be accepted");
                lines.AddRange(MessagesOf(session.DrainBeats()));
            }

            int retaliations = lines.Count(m => m.Contains("thorns lash back"));
            Assert.AreEqual(3, retaliations,
                "three affected turns, three physical replies, three retaliations -- the third included");
        }

        // ---- the corrected placement's own point: an enemy's action, both ways --

        [Test]
        public void AnEnemysPhysicalAction_TriggersRetaliation()
        {
            // The plain swing every monster without an abilities list falls
            // back to (EnemyKit's LegacyPoolFor) IS the physical case --
            // covered already by APhysicalSwingThatMisses_StillPaysTheTithe
            // when it lands rather than misses, pinned here without the
            // guaranteed dodge so the swing actually connects.
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { FillerHeal() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(47)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Thorned, 5, 3, hero);
            int foeHealthBeforeAction = foe.CurrentHealth;

            Assert.IsTrue(session.CastSkill(0, hero));

            var lines = MessagesOf(session.DrainBeats()).ToList();
            Assert.IsFalse(lines.Any(m => m.Contains("misses")), "fixture check: the swing must land this time");
            Assert.AreEqual(foeHealthBeforeAction - 10, foe.CurrentHealth,
                "5 from the turn-start tick and 5 from the retaliation -- a landed swing pays the tithe " +
                "exactly like a missed one");
        }

        [Test]
        public void AnEnemysNonPhysicalAction_DoesNotTriggerRetaliation()
        {
            var hero = Hero();
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, new[] { FillerHeal() }, null, DamageType.Physical) },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false,
                    new List<EnemyAbility> { EnemyAbility.Of(MonsterCast(), 1_000_000f) }) },
                new SeededRandom(53)) { DamageVarianceRange = 0f };
            session.Begin();

            session.ApplyStatusToForTest(foe, StatusEffectType.Thorned, 5, 3, hero);
            int foeHealthBeforeAction = foe.CurrentHealth;

            Assert.IsTrue(session.CastSkill(0, hero));

            var lines = MessagesOf(session.DrainBeats()).ToList();
            Assert.IsTrue(lines.Any(m => m.Contains("Mud Burst")), "fixture check: the cast must be the one drawn");
            Assert.AreEqual(foeHealthBeforeAction - 5, foe.CurrentHealth,
                "only the turn-start tick -- a cast is not a physical move, so no retaliation for it even " +
                "though the monster clearly acted");
            Assert.IsFalse(lines.Any(m => m.Contains("thorns lash back")));
        }

        // ---- the player side of the same hook -----------------------------------

        [Test]
        public void APlayersOwnPhysicalAttack_PaysTheTitheWhenThePlayerCarriesIt()
        {
            // Thorn Tithe curses enemies today, but 1.11's hook is not
            // hardcoded to one side -- AdvanceAfterAction is the identical
            // seam every player command reaches, and this pins that half of
            // it directly rather than trusting it by inference from the
            // enemy-side tests above.
            var hero = Hero();
            GuaranteeDodge(hero); // isolates the tithe from the monster's own counter-swing
            var foe = Monster();
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) },
                new SeededRandom(59)) { DamageVarianceRange = 0f };

            // APPLIED BEFORE Begin() SPECIFICALLY: the retaliation cache is
            // filled at a TURN-START tick (FightSession.Riders
            // .TriggerPhysicalMoveRetaliation's own header), and Begin()'s
            // own turn-start bookkeeping for whoever opens the fight is the
            // only one this single-action fixture reaches -- applying it any
            // later would land mid-turn, after that tick already ran, and the
            // cache would stay empty for an action taken later in that same
            // turn. That sequencing only ever arises in a fixture: in a real
            // fight nobody can curse the combatant whose turn is already
            // running.
            session.ApplyStatusToForTest(hero, StatusEffectType.Thorned, 7, 3, null);
            session.Begin();

            Assert.IsTrue(session.ExecuteAttack(foe), "the plain attack must be accepted");

            // MESSAGES, NOT A RAW HEALTH DELTA: AutoResolveEnemyTurns already
            // opens the HERO's own next turn before handing control back
            // (StepToNextTurn's GrantTurnStart runs for whoever is Current
            // next, player or not), so a bare one-hero/one-monster fixture
            // sees a SECOND Thorned tick land in the same call, from the
            // following turn opening early -- a fixture artifact of this
            // exact 1v1 shape, not part of what this test is pinning. The
            // message count sidesteps it cleanly.
            var lines = MessagesOf(session.DrainBeats()).ToList();
            Assert.AreEqual(1, lines.Count(m => m.Contains("thorns lash back")),
                "the player's own physical swing pays the tithe exactly once, the same as an enemy's would");
        }
    }
}
