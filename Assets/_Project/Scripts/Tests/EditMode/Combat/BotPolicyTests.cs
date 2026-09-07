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
    // RandomLegalPolicy and GreedyAggressivePolicy over FightSession.
    public class BotPolicyTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100, int attack = 20, int speed = 5) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, attack, speed);

        private static (FightSession session, CombatantState hero, List<CombatantState> foes) HeroVsMany(
            params int[] foeHealths)
        {
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: 10);
            var foes = foeHealths
                .Select((hp, i) => Fighter($"Foe{i}", false, maxHealth: hp, attack: 1, speed: 1))
                .ToList();

            var encounter = new CombatEncounter(new[] { hero }, foes);
            // TestSkills.RangedPacket stands in for the old free basic spell
            // so a target behind the front rank is reachable at all -- Attack is
            // front-rank-only (see FightSession.CanReach), and with
            // more than one foe that is the only way this fixture can show
            // GreedyAggressive choosing BETWEEN targets rather than being
            // handed just one.
            var skills = new List<ResolvedSkill> { TestSkills.RangedPacket() };
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foes);
        }

        [Test]
        public void RandomLegalPolicy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new RandomLegalPolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void GreedyAggressivePolicy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new GreedyAggressivePolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void GreedyAggressivePolicy_WithTwoReachableFoes_TargetsTheLowerHpOne()
        {
            // foes[0] (200 HP) is the front rank, reachable by Attack or the
            // ranged skill; foes[1] (40 HP) sits behind it and is reachable
            // only by the ranged skill (Attack is front-rank-only -- see
            // FightSession.CanReach). GreedyAggressive must still
            // pick the lower-HP one across both kinds of reach.
            var (session, hero, foes) = HeroVsMany(200, 40);
            var policy = new GreedyAggressivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(foes[1], chosen.Target, "the 40-HP foe is lower than the 200-HP one and should be targeted first");
        }

        [Test]
        public void GreedyAggressivePolicy_BelowHealthThreshold_UsesAHealingItemOverAttacking()
        {
            var (session, hero, foes) = HeroVsMany(60);
            hero.CurrentHealth = (int)(hero.MaxHealth * 0.2f);
            var policy = new GreedyAggressivePolicy();
            var satchel = new List<SatchelStack> { new SatchelStack("potion", "Potion", 3, false) };
            var legal = FightAction.LegalActions(session, hero, satchel);

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Item, chosen.Kind);
            Assert.AreEqual("potion", chosen.ItemId);
        }

        // ---- GreedyDefensivePolicy ---------------------------------------

        private static (FightSession session, CombatantState hero, List<CombatantState> foes) HeroVsManyWithSkills(
            IReadOnlyList<ResolvedSkill> skills, params int[] foeHealths)
        {
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: 10);
            var foes = foeHealths
                .Select((hp, i) => Fighter($"Foe{i}", false, maxHealth: hp, attack: 1, speed: 1))
                .ToList();

            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return (session, hero, foes);
        }

        private static ResolvedSkill WardSkill() =>
            new ResolvedSkill("ward", "Fleece Ward", "", "hero", 1, SkillEffect.Ward,
                SkillTargeting.Self, 0, 0, false, 100, 0, false, null, SpellPresentation.None, 0);

        private static ResolvedSkill HealSelfSkill() =>
            new ResolvedSkill("mend", "Mend", "", "hero", 1, SkillEffect.HealSelf,
                SkillTargeting.Self, 0, 0, false, 50, 0, false, null, SpellPresentation.None, 0);

        // The provoke shape (Domain/Combat/SkillEntryResolver.DefaultTargetingFor
        // falls through to SingleEnemy for Provoke, and it authors no
        // meleeReach flag) that reproduces the seed 42/35/64 TooManyCommands
        // livelock: a RANGED, NON-DAMAGING SingleEnemy skill, legal against
        // any living enemy regardless of front rank.
        private static ResolvedSkill ProvokeSkill() =>
            new ResolvedSkill("provoke", "Provoke", "", "hero", 1, SkillEffect.Provoke,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0);

        [Test]
        public void GreedyDefensivePolicy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new GreedyDefensivePolicy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void GreedyDefensivePolicy_BelowHealthThreshold_HealsOverAttacking()
        {
            var (session, hero, foes) = HeroVsMany(60);
            hero.CurrentHealth = (int)(hero.MaxHealth * 0.4f); // under GreedyDefensive's 50% line
            var policy = new GreedyDefensivePolicy();
            var satchel = new List<SatchelStack> { new SatchelStack("potion", "Potion", 3, false) };
            var legal = FightAction.LegalActions(session, hero, satchel);

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Item, chosen.Kind);
            Assert.AreEqual("potion", chosen.ItemId);
        }

        [Test]
        public void GreedyDefensivePolicy_HealSkillReady_HealsOverAttackingEvenWithNoItem()
        {
            var skills = new List<ResolvedSkill> { HealSelfSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 60);
            hero.CurrentHealth = (int)(hero.MaxHealth * 0.4f);
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Skill, chosen.Kind);
            var option = session.SkillOptionsFor(hero).First(o => o.Index == chosen.SkillIndex);
            Assert.AreEqual(SkillEffect.HealSelf, option.Skill.Effect);
        }

        [Test]
        public void GreedyDefensivePolicy_AtFullHealthWithAWardReady_UsesTheWardBeforeAttacking()
        {
            var skills = new List<ResolvedSkill> { WardSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 60);
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Skill, chosen.Kind);
            var option = session.SkillOptionsFor(hero).First(o => o.Index == chosen.SkillIndex);
            Assert.AreEqual(SkillEffect.Ward, option.Skill.Effect);
        }

        [Test]
        public void GreedyDefensivePolicy_WardKeepsGettingConsumedEachTurn_EventuallySwingsInstead()
        {
            // Pins the seed 629 / GreedyDefensive / Late livelock the balance
            // bot found: FightInvariants.TooManyCommands at 201 commands, the
            // last ten all fleece_ward, the enemy's HP never moving. Root
            // cause was NOT the game -- AdvanceAfterAction/
            // AutoResolveEnemyTurns ran fine every round -- it was this
            // policy re-warding forever because a single-hit ward
            // (StatusEffects.ConsumeWard) is gone again the instant a hit
            // lands, so "!alreadyWarded" was true on every single ask.
            //
            // Simulated here without a real enemy attack (this fixture's foe
            // is harmless -- see HeroVsManyWithSkills) by clearing the
            // ward status by hand between calls, which is exactly what an
            // enemy landing a hit every round would do to it. The same
            // policy instance is reused across every call, the same way
            // BotRunDriver reuses one policy object for a whole run.
            var skills = new List<ResolvedSkill> { WardSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 60);
            var policy = new GreedyDefensivePolicy();
            var rng = new SeededRandom(1);

            bool sawADamagingChoice = false;
            for (int i = 0; i < 10; i++)
            {
                var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());
                var chosen = policy.Choose(session, hero, legal, rng);

                bool isWard = chosen.Kind == FightActionKind.Skill &&
                    session.SkillOptionsFor(hero).First(o => o.Index == chosen.SkillIndex).Skill.Effect == SkillEffect.Ward;

                if (!isWard)
                {
                    sawADamagingChoice = true;
                    break;
                }

                // The ward "gets hit" -- gone before the next ask, same as a
                // real ConsumeWard call would leave it.
                hero.Statuses.Clear();
            }

            Assert.IsTrue(sawADamagingChoice,
                "the policy re-warded on every one of 10 straight asks with the ward consumed each time -- " +
                "this is the seed 629 livelock, not a fixed archetype");
        }

        [Test]
        public void GreedyDefensivePolicy_WithNoHealOrWardToTake_TargetsTheMoreThreateningEnemy()
        {
            // foe0: low HP, harmless (Attack 1). foe1: high HP, dangerous
            // (Attack 100) and reachable only via the ranged skill (Attack is
            // front-rank-only). GreedyAggressive would target foe0 for being
            // lowest-HP; GreedyDefensive must target foe1 for being the
            // bigger threat instead.
            var hero = Fighter("Hero", true, maxHealth: 300, attack: 200, speed: 10);
            var foe0 = Fighter("Foe0", false, maxHealth: 10, attack: 1, speed: 1);
            var foe1 = Fighter("Foe1", false, maxHealth: 200, attack: 100, speed: 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe0, foe1 });
            var skills = new List<ResolvedSkill> { TestSkills.RangedPacket() };
            var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical);
            var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(foe1, chosen.Target,
                "foe1's higher Attack makes it the bigger threat despite foe0 having lower HP");
        }

        // ---- target-first livelock (TooManyCommands, seeds 42/35/64) -------
        //
        // foes[0] (front rank, 200 HP): reachable by Attack or Provoke.
        // foes[1] (back rank, 40 HP -- LOWER than foes[0]): reachable only
        // by Provoke, which deals no damage. Before the fix, both policies
        // locked onto whichever target their own priority read first (lowest
        // HP for aggressive, most-threatening for defensive) and only THEN
        // filtered candidates by that target -- which here always resolves
        // to foes[1], discards Attack for targeting the wrong enemy, and
        // leaves Provoke as the only legal damaging-kind action, forever.
        // Both archetypes must now recognise foes[1] is not a real option
        // (nothing legal can put a positive number on it) and hit foes[0]
        // with Attack instead.

        [Test]
        public void GreedyAggressivePolicy_LowerHpFoeReachableOnlyByNonDamagingSkill_AttacksTheReachableFoeInstead()
        {
            var skills = new List<ResolvedSkill> { ProvokeSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 200, 40);
            var policy = new GreedyAggressivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Attack, chosen.Kind,
                "Attack on the reachable front foe must win over a 0-damage Provoke aimed at the unreachable lower-HP one");
            Assert.AreEqual(foes[0], chosen.Target);
        }

        [Test]
        public void GreedyDefensivePolicy_LowerHpFoeReachableOnlyByNonDamagingSkill_AttacksTheReachableFoeInstead()
        {
            var skills = new List<ResolvedSkill> { ProvokeSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 200, 40);
            var policy = new GreedyDefensivePolicy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(FightActionKind.Attack, chosen.Kind,
                "Attack on the reachable front foe must win over a 0-damage Provoke aimed at the unreachable lower-HP one");
            Assert.AreEqual(foes[0], chosen.Target);
        }

        // ---- NonDamagingSkillGuard wiring -----------------------------------
        //
        // `legal` is built by hand rather than through FightAction.LegalActions
        // for these two: whenever ANY enemy is alive, FightSession.CanReach
        // always allows Attack against the front rank (Max(1, ...)
        // in CombatMath.ComputeAttackDamage means it is never a 0-damage
        // candidate), so the guarded fallback below can never actually be
        // reached through LegalActions' own output in this game's current
        // rules -- there is always a real Attack for TryChooseDamagingAction
        // to prefer first. The wiring is still real production code (a future
        // status effect or kit that legitimately strips Attack would hit it),
        // and this is the only way to exercise it directly: a `legal` with a
        // repeatable 0-damage Skill and NO Attack action at all.

        [Test]
        public void GreedyAggressivePolicy_WithOnlyANonDamagingSkillRepeated_StopsPickingItAfterTheGuardCap()
        {
            var skills = new List<ResolvedSkill> { ProvokeSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 200);
            var provokeOption = session.SkillOptionsFor(hero).First(o => o.Skill.Effect == SkillEffect.Provoke);
            var legal = new List<FightAction>
            {
                // MOVE FIRST, deliberately: with no Attack on this hand-built
                // menu, FightAction.LastResort falls through to legal[0], and
                // a Skill sitting there would be the very thing the guard is
                // supposed to have refused.
                new FightAction(FightActionKind.Move, moveDirection: MoveDirection.Back),
                new FightAction(FightActionKind.Skill, foes[0], provokeOption.Index),
            };
            var policy = new GreedyAggressivePolicy();
            var rng = new SeededRandom(1);

            var first = policy.Choose(session, hero, legal, rng);
            var second = policy.Choose(session, hero, legal, rng);
            var third = policy.Choose(session, hero, legal, rng);

            Assert.AreEqual(FightActionKind.Skill, first.Kind);
            Assert.AreEqual(FightActionKind.Skill, second.Kind);
            Assert.AreNotEqual(FightActionKind.Skill, third.Kind,
                "the guard's default cap is 2 -- a third straight ask with no Attack legal and no progress made must not repeat the same non-damaging skill");
        }

        [Test]
        public void GreedyDefensivePolicy_WithOnlyANonDamagingSkillRepeated_StopsPickingItAfterTheGuardCap()
        {
            var skills = new List<ResolvedSkill> { ProvokeSkill() };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 200);
            var provokeOption = session.SkillOptionsFor(hero).First(o => o.Skill.Effect == SkillEffect.Provoke);
            var legal = new List<FightAction>
            {
                // MOVE FIRST, deliberately: with no Attack on this hand-built
                // menu, FightAction.LastResort falls through to legal[0], and
                // a Skill sitting there would be the very thing the guard is
                // supposed to have refused.
                new FightAction(FightActionKind.Move, moveDirection: MoveDirection.Back),
                new FightAction(FightActionKind.Skill, foes[0], provokeOption.Index),
            };
            var policy = new GreedyDefensivePolicy();
            var rng = new SeededRandom(1);

            var first = policy.Choose(session, hero, legal, rng);
            var second = policy.Choose(session, hero, legal, rng);
            var third = policy.Choose(session, hero, legal, rng);

            Assert.AreEqual(FightActionKind.Skill, first.Kind);
            Assert.AreEqual(FightActionKind.Skill, second.Kind);
            Assert.AreNotEqual(FightActionKind.Skill, third.Kind,
                "the guard's default cap is 2 -- a third straight ask with no Attack legal and no progress made must not repeat the same non-damaging skill");
        }

        // ---- Lookahead2Policy ----------------------------------------------

        [Test]
        public void Lookahead2Policy_FinishesAFight_WithNoInvariantHits()
        {
            var (session, hero, foes) = HeroVsMany(60);
            var policy = new Lookahead2Policy();
            var rng = new SeededRandom(42);
            var satchel = System.Array.Empty<SatchelStack>();

            var hits = FightRunner.Play(session, policy, satchel, rng, new FightTrace());

            Assert.IsTrue(session.IsOver, "the fight must reach an end state");
            CollectionAssert.IsEmpty(hits, "a normal fight must trip no invariant");
        }

        [Test]
        public void Lookahead2Policy_WhenOneOptionKillsAndAnotherOnlyChips_PicksTheKill()
        {
            // foe0 (front rank, 500 HP): reachable by Attack or the ranged
            // skill, but neither kills it. foe1 (back rank, 1 HP): reachable
            // only by the ranged skill, and any positive hit kills it. The
            // kill must win even though it is not the biggest single number
            // on the menu.
            var (session, hero, foes) = HeroVsMany(500, 1);
            var policy = new Lookahead2Policy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreEqual(foes[1], chosen.Target, "the lethal hit on the 1-HP foe must beat any chip damage on the 500-HP one");
        }

        [Test]
        public void Lookahead2Policy_SameSeedTwice_PicksTheSameAction()
        {
            var (sessionA, heroA, foesA) = HeroVsMany(200, 40);
            var (sessionB, heroB, foesB) = HeroVsMany(200, 40);
            var policy = new Lookahead2Policy();
            var legalA = FightAction.LegalActions(sessionA, heroA, System.Array.Empty<SatchelStack>());
            var legalB = FightAction.LegalActions(sessionB, heroB, System.Array.Empty<SatchelStack>());

            var chosenA = policy.Choose(sessionA, heroA, legalA, new SeededRandom(7));
            var chosenB = policy.Choose(sessionB, heroB, legalB, new SeededRandom(7));

            Assert.AreEqual(chosenA.Kind, chosenB.Kind);
            Assert.AreEqual(chosenA.Target?.Name, chosenB.Target?.Name);
        }

        [Test]
        public void Lookahead2Policy_AtNearFullHealth_AttacksInsteadOfARepeatedZeroValueHeal()
        {
            // Pins the seed 18 (and 418 further rows across the same batch)
            // livelock: Woolgathering (HealSelf, flatAmount 40 + power 30 per
            // point of Wool spent) previewed a flat, HP-independent number
            // that beat a real attack every single turn because ScoreOf used
            // to score a heal's own effect UNCAPPED -- even at/near full
            // health, where the cast restores almost nothing. This heal is
            // built the same way: a large flatAmount and no resource cost
            // (so PreviewSkillPower returns exactly flatAmount regardless of
            // the actor's own Wool), which an uncapped ScoreOf would always
            // rank above the attack below. Capped to the 1 HP actually
            // missing, it must not.
            var bigHeal = new ResolvedSkill("mega_heal", "Mega Heal", "", "hero", 1, SkillEffect.HealSelf,
                SkillTargeting.Self, 0, 0, false, 0, 999, false, null, SpellPresentation.None, 0);
            var skills = new List<ResolvedSkill> { bigHeal };
            var (session, hero, foes) = HeroVsManyWithSkills(skills, 5000);
            hero.CurrentHealth = hero.MaxHealth - 1;
            var policy = new Lookahead2Policy();
            var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

            var chosen = policy.Choose(session, hero, legal, new SeededRandom(1));

            Assert.AreNotEqual(FightActionKind.Skill, chosen.Kind,
                "an uncapped heal preview of 999 would always outscore Attack/the ranged skill; capped to the 1 HP actually missing it must not win over either");
        }

        [Test]
        public void Lookahead2Policy_IncomingDamageMagnitude_DoesNotChangeOrderingBetweenTwoDamagingOptions()
        {
            // The score already subtracts the SAME incoming-damage term from
            // every non-lethal option (ScoreOf sums ThreatOf across every
            // living enemy regardless of which one is targeted), so raising
            // the enemy's own attack must not flip which of two damaging
            // options this policy prefers -- it changes both options' scores
            // by the identical amount. Pinned directly rather than assumed:
            // a future change that made `incoming` depend on the action
            // (say, only counting threats the action does not also block)
            // would break this silently otherwise.
            FightActionKind ChosenKindWithFoeAttack(int foeAttack)
            {
                var hero = Fighter("Hero", true, maxHealth: 300, attack: 50, speed: 10);
                var foe = Fighter("Foe", false, maxHealth: 5000, attack: foeAttack, speed: 1);
                var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
                // Fixed at 1 damage so the ranged skill never ties or beats
                // the melee Attack on raw damage alone.
                var skills = new List<ResolvedSkill> { TestSkills.RangedPacket(fixedDamage: 1) };
                var kit = new PlayerKit("hero", CharacterRole.Tank, skills, null, DamageType.Physical);
                var session = new FightSession(encounter, new List<PlayerKit> { kit }, null, new SeededRandom(1))
                {
                    DamageVarianceRange = 0f,
                };
                session.Begin();
                var policy = new Lookahead2Policy();
                var legal = FightAction.LegalActions(session, hero, System.Array.Empty<SatchelStack>());

                return policy.Choose(session, hero, legal, new SeededRandom(1)).Kind;
            }

            var lowThreat = ChosenKindWithFoeAttack(1);
            var highThreat = ChosenKindWithFoeAttack(500);

            Assert.AreEqual(FightActionKind.Attack, lowThreat, "melee should out-damage the weak ranged skill regardless of incoming threat");
            Assert.AreEqual(lowThreat, highThreat,
                "raising the enemy's own attack -- and so the incoming term subtracted from every option alike -- must not flip which damaging option ranks best");
        }

        [Test]
        public void NonDamagingSkillGuard_BlocksAfterMaxConsecutive_AndResetsOnProgress()
        {
            var guard = new NonDamagingSkillGuard();
            var actor = Fighter("Hero", true);

            Assert.IsTrue(guard.MayChoose(actor, "ward", 2));
            guard.RecordChosen(actor, "ward");
            Assert.IsTrue(guard.MayChoose(actor, "ward", 2));
            guard.RecordChosen(actor, "ward");
            Assert.IsFalse(guard.MayChoose(actor, "ward", 2), "a third repeat past a cap of 2 must be blocked");

            guard.RecordProgress(actor);
            Assert.IsTrue(guard.MayChoose(actor, "ward", 2), "progress must clear the streak");
        }

        // ---- Archetypes.Create ----------------------------------------------

        [Test]
        public void Archetypes_Create_KnowsAllFourNames()
        {
            foreach (string name in Archetypes.Names)
            {
                var made = Archetypes.Create(name);
                Assert.IsNotNull(made, $"'{name}' is listed in Archetypes.Names but Create returned null");
                Assert.IsInstanceOf<IFightPolicy>(made);
                Assert.IsInstanceOf<IRunPolicy>(made);
            }
        }

        [Test]
        public void Archetypes_Create_AnUnknownName_ReturnsNull()
        {
            Assert.IsNull(Archetypes.Create("NotAnArchetype"));
        }
    }
}
