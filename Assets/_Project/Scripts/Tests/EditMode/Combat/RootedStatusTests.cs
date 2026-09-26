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
    // PHASE D3, the item-modifier plan: Rooted. Builds on the existing
    // melee(plain-attack)/ranged(skill) distinction confirmed at
    // FightSession.CanReachEnemy/FightController.Input -- a rooted ENEMY
    // loses its plain-attack option from its own action draw
    // (FightSession.Enemies.EffectivePoolFor) and must act through a skill,
    // or forfeit the turn via the exact mechanism ResolveSkippedTurn already
    // uses for Stun. The real-content half (Sylvan's RootChancePercent rider
    // actually landing Rooted through a real fight) lives beside Frosty's
    // own PlayMode counterpart; this file is the hand-built-fixture half,
    // mirroring ChilledStatusTests' own split.
    //
    // Every expected number here is either a literal this test authors
    // itself (Rooted carries no formula of its own to recompute -- it is a
    // gate, not a magnitude) or FightTuning's own published constant, never
    // recomputed from the production code under test (CLAUDE.md gotcha 5).
    public class RootedStatusTests
    {
        // Same gap EnemyAiTests uses: slow enough that the hero always opens,
        // fast enough that a single hero action is always followed by
        // exactly one monster reply -- a wider gap (tried first) let the
        // hero keep recharging past the monster's own turn entirely within
        // one ExecuteAttack call, which read as "the monster never acted"
        // rather than "Rooted gated it".
        private static CombatantState Hero(string name = "Hero", int health = 500, int speed = 10) =>
            new CombatantState(name, true, health, 20, 20, speed);

        private static CombatantState Monster(string name = "Monster", int health = 1000, int speed = 9) =>
            new CombatantState(name, false, health, 10, 15, speed);

        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

        private static ResolvedSkill Skill(string id = "thorn_lash", string displayName = "Thorn Lash") =>
            new ResolvedSkill(id, displayName, "", "monster", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0);

        private static FightSession Session(CombatEncounter encounter, IReadOnlyList<EnemyKit> kits) =>
            new FightSession(encounter, new List<PlayerKit> { null }, kits, new SeededRandom(1))
                { DamageVarianceRange = 0f };

        private static IReadOnlyList<CombatBeat> EnemyBeats(IReadOnlyList<CombatBeat> beats) =>
            beats.Where(b => b.IsAction && !b.Actor.IsPlayerSide).ToList();

        private static IEnumerable<string> MessagesOf(IReadOnlyList<CombatBeat> beats) =>
            beats.SelectMany(b => b.Messages);

        // The plain-swing entry's weight so overwhelmingly outweighs the
        // skill's that EVERY roll in [0,1) picks it at baseline -- see
        // EnemyAbilityDraw.Pick's own math: `roll` is clamped to 0.9999999f
        // before being multiplied by the pool's total weight, and
        // 1_000_000 / 1_000_000.01 exceeds that clamp. This makes the
        // baseline assertion below a mathematical certainty, not a seed that
        // happened to work.
        private static IReadOnlyList<EnemyAbility> OverwhelminglyPlainSwingPool(ResolvedSkill skill) =>
            new List<EnemyAbility>
            {
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 1_000_000f),
                EnemyAbility.Of(skill, 0.01f),
            };

        // ---- the enemy action-selection gate -----------------------------------

        // AUDIT #148: this committed swing turns illegal by resolution time
        // (Rooted lands on the enemy between its intent being drawn and that
        // intent resolving) used to redraw a fresh ability from the pool --
        // even one the player was never shown, undoing the very thing Root
        // was spent to stop. The owner's call: Root reliably cancels the
        // swing it caught. Full stop, whether or not a legal skill exists.
        [Test]
        public void RootedEnemyWithACommittedPlainSwing_VoidsTheAttack_EvenWithALegalSkillAvailable()
        {
            var skill = Skill();

            var baselineHero = Hero();
            var baselineMonster = Monster();
            var baselineSession = Session(
                new CombatEncounter(new[] { baselineHero }, new[] { baselineMonster }),
                new List<EnemyKit> { new EnemyKit(Source("monster"), false, OverwhelminglyPlainSwingPool(skill)) });
            baselineSession.Begin();
            baselineSession.ExecuteAttack(baselineMonster);

            var baselineLines = MessagesOf(EnemyBeats(baselineSession.DrainBeats())).ToList();
            Assert.IsTrue(baselineLines.Any(m => m.Contains("attacks") && m.Contains("damage")),
                "fixture check: without Rooted, the overwhelming plain-swing weight must win the draw");
            Assert.IsFalse(baselineLines.Any(m => m.Contains("Thorn Lash")),
                "fixture check: the skill must not fire at baseline");

            var rootedHero = Hero();
            var rootedMonster = Monster();
            var rootedSession = Session(
                new CombatEncounter(new[] { rootedHero }, new[] { rootedMonster }),
                new List<EnemyKit> { new EnemyKit(Source("monster"), false, OverwhelminglyPlainSwingPool(skill)) });
            rootedSession.Begin();
            StatusEffects.Apply(rootedMonster.Statuses, StatusEffectType.Rooted, 0, 5);

            rootedSession.ExecuteAttack(rootedMonster);
            var rootedLines = MessagesOf(EnemyBeats(rootedSession.DrainBeats())).ToList();

            Assert.IsTrue(rootedLines.Any(m => m.Contains("rooted")),
                "Rooted must void the stale plain-swing commitment outright");
            Assert.IsFalse(rootedLines.Any(m => m.Contains("Thorn Lash")),
                "the legal skill must NOT fire as a substitute -- Root cancels the swing, it does not " +
                "hand the enemy a different, un-telegraphed attack");
            Assert.IsFalse(rootedLines.Any(m => m.Contains("damage")),
                "a voided swing must land no damage");
        }

        [Test]
        public void RootedEnemyWithNoLegalSkill_ForfeitsItsTurn_TheSameWayStunDoes()
        {
            // No abilities authored: EnemyKit's own legacy-pool fallback is a
            // SINGLE entry, the plain swing (see EnemyKit.LegacyPoolFor).
            // Rooted excludes it, leaving nothing at all to draw from.
            var hero = Hero();
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(Source("monster"), false) });
            session.Begin();
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Rooted, 0, 5);

            int before = hero.CurrentHealth;
            session.ExecuteAttack(monster);
            var beats = session.DrainBeats();
            var lines = MessagesOf(EnemyBeats(beats)).ToList();

            Assert.AreEqual(before, hero.CurrentHealth, "the turn was forfeited, not weakened");

            // THE SAME WORDING ResolveSkippedTurn already prints for Stun/
            // BreakShield -- one skip mechanism reporting itself, not a
            // second one that happens to look similar.
            Assert.IsTrue(lines.Any(m => m.Contains("cannot act")),
                "Rooted's no-legal-skill forfeit must go through the exact same 'cannot act' path Stun uses");

            // UNLIKE Stun (ConsumeStun removes it outright, spent on the
            // skip), Rooted decays by TURN COUNT like Chilled/Protect -- and
            // since plan D1 that count moves at the END of the bearer's turn.
            // A 5-turn Rooted reads 4 here: the forfeited turn WAS the
            // monster's turn, and it has ended by the time this line runs.
            //
            // IT READ 5 UNTIL MILESTONE D, and that was the bug rather than
            // the rule. The turn-end clock only ran inside AdvanceAfterAction,
            // which no monster turn and no skipped turn reaches, so a rooted
            // monster forfeited, aged nothing, and forfeited again for ever --
            // a root that fed itself. See FightSession.EndTurnStatusesForCurrent.
            var rooted = monster.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Rooted);
            Assert.IsNotNull(rooted, "a forfeited turn must not erase the turns of Rooted still owed -- " +
                                      "that would make forfeiting the turn Rooted's OWN escape hatch");
            Assert.AreEqual(4, rooted.TurnsRemaining,
                "not consumed outright like Stun, and aged by exactly the one turn that was spent");
        }

        [Test]
        public void RootedEnemyWithNoLegalSkill_TelegraphsAForfeit_NotAFakeAttack()
        {
            // PHASE D3 FIX. BuildIntent's own -1 fallback (FightSession.Enemies.cs)
            // used to swing regardless of WHY the draw came back empty, so this
            // exact fixture -- Rooted, no abilities authored at all -- was
            // telegraphed a full-power "Attack" with a real damage preview on
            // the player's turn, and then had that very turn silently forfeited
            // once AutoResolveEnemyTurns actually reached it (see the test
            // above, which proves the RESOLUTION half already worked). The
            // player planned around damage that was a lie.
            //
            // Rooted is applied BEFORE Begin(), unlike the resolution test
            // above -- Begin() commits the opening telegraph at its own tail
            // end (see FightSession.Begin), so applying it any later would
            // telegraph against the pre-Rooted state and prove nothing about
            // BuildIntent's fallback at all.
            var hero = Hero();
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(Source("monster"), false) });
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Rooted, 0, 5);

            session.Begin();

            Assert.AreEqual(FightSession.IntentForfeit, session.IntentFor(monster),
                "a Rooted enemy with nothing else to cast must telegraph a forfeit, not \"Attack\"");

            var detail = session.IntentDetailFor(monster);
            Assert.IsTrue(detail.HasValue, "a forfeit is still a committed intent, not a missing one");
            Assert.AreEqual(0, detail.Value.ExpectedDamage,
                "no damage preview may be shown for a turn the enemy cannot actually take");
            Assert.AreNotEqual(EnemyIntentKind.Attack, detail.Value.Kind,
                "the badge must not read as an ordinary threat when nothing is actually coming");
        }

        // ---- the SAME mechanism, side by side with Stun ------------------------

        [Test]
        public void RootedForfeit_AndStunForfeit_ProduceTheIdenticalShapeOfSkip()
        {
            var stunHero = Hero();
            var stunMonster = Monster();
            var stunSession = Session(
                new CombatEncounter(new[] { stunHero }, new[] { stunMonster }),
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) });
            stunSession.Begin();
            StatusEffects.Apply(stunMonster.Statuses, StatusEffectType.Stun, 0, 5);
            int stunHealthBefore = stunHero.CurrentHealth;
            stunSession.ExecuteAttack(stunMonster);
            var stunLines = MessagesOf(EnemyBeats(stunSession.DrainBeats())).ToList();

            var rootedHero = Hero();
            var rootedMonster = Monster();
            var rootedSession = Session(
                new CombatEncounter(new[] { rootedHero }, new[] { rootedMonster }),
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) });
            rootedSession.Begin();
            StatusEffects.Apply(rootedMonster.Statuses, StatusEffectType.Rooted, 0, 5);
            int rootedHealthBefore = rootedHero.CurrentHealth;
            rootedSession.ExecuteAttack(rootedMonster);
            var rootedLines = MessagesOf(EnemyBeats(rootedSession.DrainBeats())).ToList();

            // Same OBSERVABLE shape from the outside: no damage dealt, and a
            // "cannot act" line, from BOTH sources -- what a second,
            // independently-invented skip mechanism could easily fail to
            // match exactly (a different message, a beat that plays
            // differently, damage that leaks through).
            Assert.AreEqual(stunHero.CurrentHealth, stunHealthBefore);
            Assert.AreEqual(rootedHero.CurrentHealth, rootedHealthBefore);
            Assert.AreEqual(stunLines.Count(m => m.Contains("cannot act")),
                rootedLines.Count(m => m.Contains("cannot act")));
        }

        // ---- Rooted is read only for the ENEMY side ----------------------------

        [Test]
        public void ARootedPlayer_StillTakesAnOrdinaryTurn_NeverForfeits()
        {
            // Nothing in the game authors an enemy ability that applies
            // Rooted to a PLAYER today (see StatusEffectType.Rooted's own
            // comment) -- this proves the gate is safe BY CONSTRUCTION if
            // one ever did, not merely untested.
            //
            // Applied BEFORE Begin(), and the hero is the faster combatant
            // (Hero()'s default speed 10 against Monster()'s 9) so the
            // hero opens the fight -- which is what makes this actually
            // exercise ResolveSkippedTurn's check, since that method runs
            // for BOTH sides at the top of AutoResolveEnemyTurns' loop,
            // BEFORE the `current.IsPlayerSide` break that would otherwise
            // hand the opening turn back untouched. Without the
            // `!enemy.IsPlayerSide` guard in that method, a rooted player
            // would read as "helpless" unconditionally -- SourceFor(player)
            // is always null, so RootedEnemyHasNoLegalAction would find an
            // empty pool regardless -- and their ENTIRE opening turn, not
            // merely their plain attack, would be silently forfeited here.
            var hero = Hero();
            var monster = Monster();
            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter, new List<EnemyKit> { new EnemyKit(Source("monster"), false) });
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Rooted, 0, 5);

            session.Begin();

            Assert.IsTrue(encounter.IsPlayerTurn,
                "the rooted hero's own opening turn must never be swallowed by the enemy-only forfeit gate");

            int monsterHealthBefore = monster.CurrentHealth;
            session.ExecuteAttack(monster);

            Assert.Less(monster.CurrentHealth, monsterHealthBefore,
                "a rooted PLAYER must still be able to land their own plain attack -- Rooted only ever " +
                "gates an ENEMY's own action draw, never the player's");
        }

        // ---- coexistence with the other two Phase D mechanics ------------------

        [Test]
        public void RootedChilledAndDodgeCapable_AllCoexistOnOneCombatantWithoutInterference()
        {
            var skill = Skill();
            var hero = Hero();
            var monster = Monster();
            // DodgeRating 100_000 curves to 99% (100*100000/100100, floored)
            // -- the highest CombatMath.DodgePercentFrom's curve can ever
            // produce (no finite rating reaches a TRUE guarantee, see its
            // own header), so this fixture leans on the fixed
            // SeededRandom(1) stream this class always uses instead: Begin()
            // spends this session's first TWO draws -- one picking the
            // monster's telegraphed intent, one inside
            // PickIntentTarget's NextInt (which advances the
            // stream even with a single living player, since range-1 still
            // calls NextUlong) -- before the swing below spends the THIRD on
            // its own dodge roll. Confirmed by running this suite (not by
            // trusting the hand-derivation alone) that this third draw lands
            // just under 99%, so the dodge fires reliably for this exact
            // seed/rating pair -- this test is about Rooted/Chilled
            // coexistence, not about pinning the dodge curve itself (that
            // lives in CombatMathTests).
            monster.ModifierEffects = new ModifierEffectSet(
                new[] { new ModifierEffect(ModifierEffectType.DodgeRating, 100_000) });

            var encounter = new CombatEncounter(new[] { hero }, new[] { monster });
            var session = Session(encounter,
                new List<EnemyKit> { new EnemyKit(Source("monster"), false, OverwhelminglyPlainSwingPool(skill)) });
            session.Begin();

            int speedBeforeChill = monster.Speed;
            StatusEffects.Apply(monster.Statuses, StatusEffectType.Rooted, 0, 5);

            // NOT a bare StatusEffects.Apply -- Chilled's own malus is booked
            // in FightSession.SpeedBuffs' own dictionary, not derived from
            // the status entry itself (see StatusEffects.cs's own header),
            // so ApplyChilledForTest is what ChilledStatusTests itself calls
            // to exercise the real read hook rather than only the status list.
            session.ApplyChilledForTest(monster, 30, 5);

            Assert.AreEqual(2, monster.Statuses.Count(s =>
                    s.Type == StatusEffectType.Rooted || s.Type == StatusEffectType.Chilled),
                "Rooted and Chilled are different Types, so StatusEffects.Apply's refresh-by-type rule " +
                "must keep both as distinct entries, neither one clobbering the other");
            Assert.Less(monster.Speed, speedBeforeChill, "Chilled's own malus must apply exactly as it " +
                                                           "always does, undisturbed by Rooted sitting " +
                                                           "alongside it on the same status list");

            int monsterHealthBefore = monster.CurrentHealth;
            session.ExecuteAttack(monster);
            var beats = session.DrainBeats();

            var heroBeat = beats.First(b => ReferenceEquals(b.Actor, hero));
            Assert.IsTrue(heroBeat.Missed,
                "a high DodgeRating must still fire on the incoming swing, unaffected by " +
                "the target also carrying Rooted and Chilled");
            Assert.AreEqual(monsterHealthBefore, monster.CurrentHealth, "the dodged swing must deal no damage");

            var monsterLines = MessagesOf(EnemyBeats(beats)).ToList();
            Assert.IsTrue(monsterLines.Any(m => m.Contains("rooted")),
                "Rooted's own gate must still void the plain attack on the monster's OWN turn, " +
                "unaffected by the dodge that just fired against it or by Chilled slowing it down");
        }

        // ---- Sylvan's on-hit proc landing the RIGHT duration -----------------
        //
        // PHASE F: every test above hand-applies Rooted via StatusEffects.Apply
        // directly, so none of them exercise FightSession.cs's own
        // `turns: FightTuning.RootOnHitTurns` call site at all. This is the
        // missing proof that the on-hit proc itself lands the authored
        // duration, not merely that FightTuning.RootOnHitTurns equals
        // whatever it equals (see FightTuningItemModifierConstantsTests for
        // that separate, narrower pin).
        [Test]
        public void RootChancePercentOnHit_LandsRooted_ForExactlyOneTurn()
        {
            // A LARGE hero/monster speed gap -- the same fixture shape
            // ChilledStatusTests' own ChilledOnHit test relies on -- so the
            // hero remains Current after this one swing and nothing ticks
            // the freshly-applied Rooted down before this test reads it.
            var hero = Hero(speed: 200);
            hero.ModifierEffects = new ModifierEffectSet(
                new[] { new ModifierEffect(ModifierEffectType.RootChancePercent, 100) });
            var monster = Monster(speed: 1);
            var session = Session(new CombatEncounter(new[] { hero }, new[] { monster }),
                new List<EnemyKit> { new EnemyKit(Source("monster"), false) });
            session.Begin();

            session.ExecuteAttack(monster);
            Assert.IsTrue(session.IsPlayerTurn,
                "fixture check: the hero must still hold the turn after one swing, or the monster's own " +
                "turn-start tick would disturb the TurnsRemaining this test is about to check");

            var rooted = monster.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Rooted);
            Assert.IsNotNull(rooted, "a guaranteed 100% RootChancePercent must land Rooted on the first hit");

            // The literal, not FightTuning.RootOnHitTurns read back at
            // itself -- if the on-hit call site ever stops passing that
            // constant through, this is what turns red. ONE since plan D1
            // moved Rooted to the turn-end clock, where 1 affects the same one
            // turn the old turn-start 2 affected.
            Assert.AreEqual(1, rooted.TurnsRemaining,
                "the on-hit proc must apply Rooted for exactly FightTuning.RootOnHitTurns (1) turn");
        }
    }
}
