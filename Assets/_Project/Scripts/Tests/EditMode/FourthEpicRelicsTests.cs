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
    // Sword in a Box, Lucky Deck, the Drowned Lantern's mark, and The First
    // Rune -- the last four relics.
    public class FourthEpicRelicsTests
    {
        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        // ignoresDefense throughout so a base amount reads as exactly its
        // flatAmount, which is what lets these tests name literal numbers.
        private static ResolvedSkill Bolt(int flat, SkillEffect effect = SkillEffect.DamageSingle) =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1, effect,
                effect == SkillEffect.DamageAll ? SkillTargeting.AllEnemies : SkillTargeting.SingleEnemy,
                0, 0, false, 0, flat, ignoresDefense: true,
                null, SpellPresentation.None, 0);

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            RelicEffect relic, ResolvedSkill? skill = null, int foeCount = 1, int heroSpeed = 10)
        {
            var hero = new CombatantState("Shawn", true, 999999, 999, 20, 0, heroSpeed);
            var foes = Enumerable.Range(0, foeCount)
                .Select(i => new CombatantState($"Foe{i}", false, 999999, 0, 1, 0, 1))
                .ToArray();

            var relics = new List<ResolvedRelic> { Relic(relic) };
            var kit = new PlayerKit("hero", CharacterRole.Tank,
                skill.HasValue ? new List<ResolvedSkill> { skill.Value } : null, relics, null);

            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name, new StatBlock(),
                    0, 0, false, DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(new CombatEncounter(new[] { hero }, foes),
                new List<PlayerKit> { kit }, enemyKits,
                new SeededRandom(9)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foes[0]);
        }

        // ---- sword in a box --------------------------------------------------------

        // THE FOLLOW-UP ATTACK LANDS ON TOP OF THE SPELL. Measured as a
        // difference from an identical fight with no relic, so the assertion
        // survives a retune of either formula.
        [Test]
        public void ACastFollowsWithAFreeAttackOnTheSameTarget()
        {
            var bolt = Bolt(50);

            var (withRelic, heroA, foeA) = Fight(RelicEffect.SwordInABox, bolt);
            int beforeA = foeA.CurrentHealth;
            withRelic.CastSkill(bolt, foeA);
            int dealtWithRelic = beforeA - foeA.CurrentHealth;

            var (withoutRelic, heroB, foeB) = Fight(RelicEffect.None, bolt);
            int beforeB = foeB.CurrentHealth;
            withoutRelic.CastSkill(bolt, foeB);
            int dealtWithout = beforeB - foeB.CurrentHealth;

            Assert.Greater(dealtWithRelic, dealtWithout,
                "the follow-up attack should have added its own damage on top of the spell");
        }

        // A SELF-HEAL MUST NOT MAKE THE ACTOR ATTACK THEMSELF. Guarded on
        // opposing sides, not merely on the target being alive.
        [Test]
        public void ASelfTargetedHealDoesNotTriggerAFollowUpAttack()
        {
            var heal = new ResolvedSkill("heal", "Heal", "", "hero", 1, SkillEffect.HealSelf,
                SkillTargeting.Self, 0, 0, false, 0, 40, false,
                null, SpellPresentation.None, 0);

            var (session, hero, foe) = Fight(RelicEffect.SwordInABox, heal);
            int foeHealthBefore = foe.CurrentHealth;

            session.CastSkill(heal, hero);

            Assert.AreEqual(foeHealthBefore, foe.CurrentHealth,
                "healing should never make the caster swing at an untouched enemy");
        }

        // ---- the drowned lantern's mark ---------------------------------------------

        [Test]
        public void ASpellMarksItsTargetAndAnAttackConsumesItForBonusDamage()
        {
            var bolt = Bolt(50);
            var (session, hero, foe) = Fight(RelicEffect.DrownedLantern, bolt);

            session.CastSkill(bolt, foe);
            Assert.IsTrue(session.IsMarked(foe), "the spell should have marked its target");

            int before = foe.CurrentHealth;
            session.ExecuteAttack(foe);
            int fromMarkedSwing = before - foe.CurrentHealth;

            Assert.IsFalse(session.IsMarked(foe), "the attack should have consumed the mark");

            // A second swing, with nothing left to consume, for the baseline.
            before = foe.CurrentHealth;
            session.ExecuteAttack(foe);
            int fromPlainSwing = before - foe.CurrentHealth;

            Assert.Greater(fromMarkedSwing, fromPlainSwing,
                "the marked swing should have hit harder than the same swing with no mark left");
        }

        [Test]
        public void AMarkIsOnlySpentOnce()
        {
            var bolt = Bolt(50);
            var (session, hero, foe) = Fight(RelicEffect.DrownedLantern, bolt);

            session.CastSkill(bolt, foe);
            session.ExecuteAttack(foe);

            int before = foe.CurrentHealth;
            session.ExecuteAttack(foe);
            int secondSwing = before - foe.CurrentHealth;

            before = foe.CurrentHealth;
            session.ExecuteAttack(foe);
            int thirdSwing = before - foe.CurrentHealth;

            Assert.AreEqual(secondSwing, thirdSwing,
                "a mark that was already spent must not pay out again on the next swing");
        }

        // ---- the first rune ---------------------------------------------------------

        [Test]
        public void ASingleTargetSpellLandsAgainForFree()
        {
            var bolt = Bolt(50);

            var (withRelic, heroA, foeA) = Fight(RelicEffect.FirstRune, bolt);
            int beforeA = foeA.CurrentHealth;
            withRelic.CastSkill(bolt, foeA);
            int dealtWithRelic = beforeA - foeA.CurrentHealth;

            var (withoutRelic, heroB, foeB) = Fight(RelicEffect.None, bolt);
            int beforeB = foeB.CurrentHealth;
            withoutRelic.CastSkill(bolt, foeB);
            int dealtWithout = beforeB - foeB.CurrentHealth;

            // Close to double, not asserted as EXACTLY double: the base
            // formula is deterministic but re-deriving its exact figure here
            // would make this a recomputation of production code rather than
            // a test of it (CLAUDE.md gotcha 5).
            Assert.Greater(dealtWithRelic, dealtWithout,
                "the copy should have landed real, additional damage");
            Assert.Greater(dealtWithRelic, dealtWithout * 3 / 2,
                "a genuine second copy of the spell should roughly double the total, not just add a sliver");
        }

        // FREE MEANS FREE. The copy must not have spent a second mana cost.
        [Test]
        public void TheCopyCostsNoAdditionalMana()
        {
            var bolt = new ResolvedSkill("bolt", "Bolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, manaCost: 30, resourceCost: 0, false, 0, 50, true,
                null, SpellPresentation.None, 0);

            var (session, hero, foe) = Fight(RelicEffect.FirstRune, bolt);
            int manaBefore = hero.CurrentMana;

            session.CastSkill(bolt, foe);

            Assert.AreEqual(manaBefore - 30, hero.CurrentMana,
                "the rune's copy must not have charged a second 30 mana");
        }

        // SCOPED TO A SINGLE DAMAGING TARGET. An all-enemies sweep already hit
        // everyone, and "a copy on the same target" does not name one thing to
        // repeat -- excluded rather than guessed at.
        [Test]
        public void ADamageAllSpellDoesNotTriggerTheRune()
        {
            var sweep = Bolt(50, SkillEffect.DamageAll);

            var (withRelic, heroA, foeA) = Fight(RelicEffect.FirstRune, sweep, foeCount: 2);
            int beforeA = foeA.CurrentHealth;
            withRelic.CastSkill(sweep, foeA);
            int dealtWithRelic = beforeA - foeA.CurrentHealth;

            var (withoutRelic, heroB, foeB) = Fight(RelicEffect.None, sweep, foeCount: 2);
            int beforeB = foeB.CurrentHealth;
            withoutRelic.CastSkill(sweep, foeB);
            int dealtWithout = beforeB - foeB.CurrentHealth;

            Assert.AreEqual(dealtWithout, dealtWithRelic,
                "an all-enemies cast should not be doubled by a relic scoped to a single target");
        }

        // A KILLING COPY MUST NOT LOOP. Structural, not a runtime flag: the
        // rune calls *Inner directly and never re-enters RelicsAfterCast, so a
        // third cast is not merely prevented, it has no path to happen at all.
        // This is the test that would fail by hanging if that were untrue.
        [Test]
        public void CastingTwiceInARowNeverCascades()
        {
            var bolt = Bolt(999999);
            var (session, hero, foe) = Fight(RelicEffect.FirstRune, bolt);

            Assert.IsTrue(session.CastSkill(bolt, foe), "the cast should have been accepted");
            Assert.IsFalse(foe.IsAlive, "a single overwhelming cast plus its free copy should have finished it");
        }

        // ---- lucky deck ---------------------------------------------------------------

        [Test]
        public void TheHealBranchRestoresHealthAndMana()
        {
            var (session, hero, _) = Fight(RelicEffect.LuckyDeck);
            hero.CurrentHealth = 1;
            hero.CurrentMana = 0;

            session.LuckyDeckHealForTest(hero);

            Assert.AreEqual(hero.MaxHealth * FightTuning.LuckyDeckHealHealthPercent / 100 + 1, hero.CurrentHealth);
            Assert.AreEqual(hero.MaxMana * FightTuning.LuckyDeckHealManaPercent / 100, hero.CurrentMana);
        }

        [Test]
        public void TheSplashBranchHitsEveryOtherEnemyForAFractionOfTheBlow()
        {
            var (session, hero, foeA) = Fight(RelicEffect.LuckyDeck, foeCount: 3);
            var others = session.Encounter.Enemies.Where(e => !ReferenceEquals(e, foeA)).ToList();
            Assert.AreEqual(2, others.Count);

            var before = others.ToDictionary(e => e, e => e.CurrentHealth);

            session.LuckyDeckSplashForTest(hero, foeA, damage: 100);

            int expected = 100 * FightTuning.LuckyDeckSplashPercent / 100;
            foreach (var other in others)
            {
                Assert.AreEqual(before[other] - expected, other.CurrentHealth,
                    $"{other.Name} should have taken exactly the splash fraction");
            }
        }

        [Test]
        public void TheSplashBranchNeverTouchesThePrimaryTarget()
        {
            var (session, hero, foeA) = Fight(RelicEffect.LuckyDeck, foeCount: 2);
            int before = foeA.CurrentHealth;

            session.LuckyDeckSplashForTest(hero, foeA, damage: 100);

            Assert.AreEqual(before, foeA.CurrentHealth, "the splash should land on OTHER enemies only");
        }

        [Test]
        public void TheSlowBranchTakesRealSpeedAwayAndShowsIt()
        {
            // Speed 10, not the Fight() helper's usual 1: a combatant already
            // at the floor cannot be slowed further -- RevokeSpeedBuff clamps
            // at a minimum of 1 -- and testing the malus on one would be
            // testing the floor instead of the malus.
            var (session, hero, foe) = Fight(RelicEffect.LuckyDeck);
            foe.Speed = 10;
            int before = foe.Speed;

            session.LuckyDeckSlowForTest(hero, foe);

            Assert.Less(foe.Speed, before, "the slow should reduce the real Speed stat, not a cosmetic copy");
            Assert.IsTrue(session.SpeedBonusFrom(foe, RelicEffect.LuckyDeck) < 0,
                "the malus should be tracked under the relic that granted it");
        }

        // THE WIRING: a real swing with the relic equipped should reach one of
        // the three branches. Checked as "something changed" rather than
        // "which branch fired", since the roll is genuinely random -- this is
        // the test that would fail if RelicsAfterSwing stopped calling
        // RollLuckyDeck at all.
        [Test]
        public void ARealSwingWithTheRelicReachesOneOfTheThreeBranches()
        {
            bool anyEffect = false;

            for (int seed = 0; seed < 30 && !anyEffect; seed++)
            {
                var hero = new CombatantState("Shawn", true, 999999, 999, 5, 0, 10);
                var foeA = new CombatantState("A", false, 999999, 0, 1, 0, 1);
                var foeB = new CombatantState("B", false, 999999, 0, 1, 0, 1);

                var kit = new PlayerKit("hero", CharacterRole.Tank, null,
                    new List<ResolvedRelic> { Relic(RelicEffect.LuckyDeck) }, null);

                var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foeA, foeB }),
                    new List<PlayerKit> { kit },
                    new List<EnemyKit>
                    {
                        new EnemyKit(new ResolvedEnemy("a", "A", new StatBlock(), 0, 0, false,
                            DamageType.Physical, DamageType.Physical, 0), false),
                        new EnemyKit(new ResolvedEnemy("b", "B", new StatBlock(), 0, 0, false,
                            DamageType.Physical, DamageType.Physical, 0), false),
                    },
                    new SeededRandom((ulong)seed)) { DamageVarianceRange = 0f };
                session.Begin();

                hero.CurrentHealth = hero.MaxHealth / 2;
                hero.CurrentMana = 0;
                int heroSpeedBefore = hero.Speed;
                int foeBHealthBefore = foeB.CurrentHealth;

                session.ExecuteAttack(foeA);

                anyEffect = hero.CurrentHealth > hero.MaxHealth / 2
                            || hero.CurrentMana > 0
                            || foeB.CurrentHealth < foeBHealthBefore
                            || hero.Speed != heroSpeedBefore
                            || session.SpeedBonusFrom(foeA, RelicEffect.LuckyDeck) < 0;
            }

            Assert.IsTrue(anyEffect, "across 30 seeds, not one swing showed any sign of Lucky Deck firing");
        }
    }
}
