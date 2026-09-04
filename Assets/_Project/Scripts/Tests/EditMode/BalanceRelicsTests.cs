using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Magic Marker, Jar of Bear Urine, World Ender's Crown, Cursed Idol,
    // Amassing Star and Rampaging Bull's Horn -- combat-level, same shape
    // as SpeedAndBountyRelicTests: a hand-built PlayerKit handed straight
    // to a real FightSession. (Pointy Nail on the End of a Stick has no
    // combat-level test here -- it is a pure numeric modifier, covered by
    // RelicModifierTests.ArmorPenetrationFlatAppliesToTheArmorPenetrationStat
    // and CombatMath.BroadDefense's own ArmorPenetrationTests.)
    public class BalanceRelicsTests
    {
        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static EnemyKit Foe(string name = "dummy") =>
            new EnemyKit(new ResolvedEnemy(name, name, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        private static (FightSession session, CombatantState hero, CombatantState foe1, CombatantState foe2) Fight(
            RelicEffect relic, int heroMaxHealth = 100, int heroMaxMana = 100,
            IReadOnlyList<ResolvedSkill> skills = null)
        {
            var hero = new CombatantState("Shawn", true, heroMaxHealth, heroMaxMana, 20, 20);
            var foe1 = new CombatantState("Foe1", false, 999999, 0, 1, 1);
            var foe2 = new CombatantState("Foe2", false, 999999, 0, 1, 1);

            var kit = new PlayerKit("hero", CharacterRole.Tank, skills,
                new List<ResolvedRelic> { Relic(relic) }, null);

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe1, foe2 }),
                new List<PlayerKit> { kit },
                new List<EnemyKit> { Foe("Foe1"), Foe("Foe2") },
                new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, hero, foe1, foe2);
        }

        // ---- magic marker -------------------------------------------------------

        [Test]
        public void MagicMarkerMarksASpellTarget()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker,
                skills: new List<ResolvedSkill> { TestSkills.CastableSkill() });

            session.CastSkill(0, foe1);

            Assert.IsTrue(Marks.IsMarked(foe1), "a cast should mark whatever it hits");
        }

        [Test]
        public void MagicMarkerConsumesTheMarkAndRestoresTwentyPercentOfMissingMana()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker, heroMaxMana: 100,
                skills: new List<ResolvedSkill> { TestSkills.CastableSkill() });
            hero.CurrentMana = 50; // 50 missing

            session.CastSkill(0, foe1); // marks foe1, costs 0 mana (test skill is authored free)
            session.ExecuteAttack(foe1); // consumes the mark

            Assert.IsFalse(Marks.IsMarked(foe1), "the attack should have consumed the mark");
            Assert.AreEqual(60, hero.CurrentMana, "50 missing x 20% = 10 restored, 50 + 10 = 60");
        }

        [Test]
        public void AnAttackWithNoMarkRestoresNothing()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.MagicMarker, heroMaxMana: 100);
            hero.CurrentMana = 50;

            session.ExecuteAttack(foe1); // no spell was cast first -- nothing marked

            Assert.AreEqual(50, hero.CurrentMana, "no mark, no restore");
        }

        // ---- jar of bear urine ---------------------------------------------------

        [Test]
        public void JarOfBearUrineMarksEveryEnemyAtCombatStart()
        {
            var (_, _, foe1, foe2) = Fight(RelicEffect.JarOfBearUrine);

            Assert.IsTrue(Marks.IsMarked(foe1));
            Assert.IsTrue(Marks.IsMarked(foe2));
        }

        // ---- world ender's crown -------------------------------------------------

        [Test]
        public void CrossingBelowThirtyPercentFearsEveryEnemy()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.WorldEndersCrown);

            // 100 -> 25, which is 25% -- below the 30% line.
            session.DealDamageForTest(foe1, hero, 75, DamageType.Physical);

            Assert.IsTrue(Fear.IsFeared(foe1));
            Assert.IsTrue(Fear.IsFeared(foe2));
        }

        [Test]
        public void ItDoesNotFireAgainWhileStillBelowTheLine()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.WorldEndersCrown);

            session.DealDamageForTest(foe1, hero, 75, DamageType.Physical); // 100 -> 25, fires
            StatusEffects.Tick(foe1); // Fear's 1-turn duration expires
            StatusEffects.Tick(foe2);
            Assert.IsFalse(Fear.IsFeared(foe1), "the first Fear must have expired for this to be a real check");

            session.DealDamageForTest(foe1, hero, 5, DamageType.Physical); // 25 -> 20, still below 30%

            Assert.IsFalse(Fear.IsFeared(foe1), "still below the line -- must not re-fire");
            Assert.IsFalse(Fear.IsFeared(foe2));
        }

        [Test]
        public void GoingBackAboveThirtyPercentReArmsIt()
        {
            var (session, hero, foe1, foe2) = Fight(RelicEffect.WorldEndersCrown);

            session.DealDamageForTest(foe1, hero, 75, DamageType.Physical); // 100 -> 25, fires
            StatusEffects.Tick(foe1);
            StatusEffects.Tick(foe2);

            session.HealForTest(hero, 50); // 25 -> 75, back above 30%
            session.DealDamageForTest(foe1, hero, 55, DamageType.Physical); // 75 -> 20, below again

            Assert.IsTrue(Fear.IsFeared(foe1), "re-armed by going back above 30%, so this crossing must fire too");
            Assert.IsTrue(Fear.IsFeared(foe2));
        }

        // ---- cursed idol ----------------------------------------------------------

        [Test]
        public void EachHitStacksAThreePercentResistanceShred()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.CursedIdol);

            int before = foe1.CurrentHealth;
            session.DealDamageForTest(hero, foe1, 100, DamageType.Physical); // 0 existing stacks -> +0%
            int firstLoss = before - foe1.CurrentHealth;
            Assert.AreEqual(100, firstLoss, "no stack yet, so no bonus on the hit that creates the first one");

            before = foe1.CurrentHealth;
            session.DealDamageForTest(hero, foe1, 100, DamageType.Physical); // 1 existing stack -> +3%
            int secondLoss = before - foe1.CurrentHealth;
            Assert.AreEqual(103, secondLoss, "1 stack x 3% = 3% bonus on top of the 100");

            before = foe1.CurrentHealth;
            session.DealDamageForTest(hero, foe1, 100, DamageType.Physical); // 2 existing stacks -> +6%
            int thirdLoss = before - foe1.CurrentHealth;
            Assert.AreEqual(106, thirdLoss, "2 stacks x 3% = 6% bonus");

            Assert.AreEqual(3, FallingOffStacks.Count(foe1, FightTuning.CursedIdolStackKey));
        }

        [Test]
        public void StacksCapAtFifteenPercent()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.CursedIdol);

            for (int i = 0; i < 7; i++)
            {
                session.DealDamageForTest(hero, foe1, 100, DamageType.Physical);
            }

            Assert.AreEqual(5, FallingOffStacks.Count(foe1, FightTuning.CursedIdolStackKey), "capped at 5 stacks");
            Assert.AreEqual(15, FallingOffStacks.Magnitude(foe1, FightTuning.CursedIdolStackKey, 3, 15));
        }

        // ---- amassing star ---------------------------------------------------------

        [Test]
        public void ARealKillGrantsTwoPercentRunWideDamage()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.AmassingStar);
            foe1.CurrentHealth = 1;

            // A REAL kill, through ExecuteAttack -- RelicsOnEachKill (and so
            // AmassingStarOnKill) only fires from the actual kill-recording
            // path (ApplyFinalDamage -> RecordKill), which the
            // DealDamageForTest seam deliberately bypasses (it exists to
            // test the DAMAGE funnel alone, not the kill funnel).
            session.ExecuteAttack(foe1);

            Assert.AreEqual(2, session.BonusDamagePercentEarned, "one real kill = +2%");
        }

        [Test]
        public void KillingASummonGrantsNothing()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.AmassingStar);
            foe1.CurrentHealth = 1;
            foe1.IsSummon = true;

            session.ExecuteAttack(foe1);

            Assert.AreEqual(0, session.BonusDamagePercentEarned, "summons do not count");
        }

        [Test]
        public void RunWideBonusDamagePercentIncreasesActualDamageDealt()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.DualWield); // a relic irrelevant to this mechanic

            session.ExecuteAttack(foe1);
            int withoutBonus = 999999 - foe1.CurrentHealth;

            foe1.CurrentHealth = 999999;
            session.RunWideBonusDamagePercent = 50;
            session.ExecuteAttack(foe1);
            int withBonus = 999999 - foe1.CurrentHealth;

            Assert.AreEqual(withoutBonus + withoutBonus * 50 / 100, withBonus,
                "the run-wide bonus is an exact percent of the same swing's own damage");
        }

        // ---- rampaging bull's horn --------------------------------------------------

        private static ResolvedSkill TransformSkill() =>
            new ResolvedSkill("black_ram", "Black Ram Mode", "", "hero", 1,
                SkillEffect.Transform, SkillTargeting.Self, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0);

        [Test]
        public void CastingAConvergenceAbilityGrantsFiftyPercentReduction()
        {
            var (session, hero, foe1, _) = Fight(RelicEffect.RampagingBullsHorn,
                skills: new List<ResolvedSkill> { TransformSkill() });

            session.CastSkill(0, hero);

            float multiplier = StatusEffects.DamageTakenMultiplier(hero.Statuses);
            Assert.AreEqual(0.5f, multiplier, 0.0001f, "Protect at 50% halves incoming damage");
        }

        [Test]
        public void AnOrdinaryCastDoesNotGrantTheReduction()
        {
            var noop = new ResolvedSkill("heal", "Heal", "", "hero", 1,
                SkillEffect.HealSelf, SkillTargeting.Self, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0);

            var (session, hero, _, _) = Fight(RelicEffect.RampagingBullsHorn,
                skills: new List<ResolvedSkill> { noop });

            session.CastSkill(0, hero);

            Assert.AreEqual(1f, StatusEffects.DamageTakenMultiplier(hero.Statuses),
                "only a convergence (Transform) cast should grant the reduction");
        }
    }
}
