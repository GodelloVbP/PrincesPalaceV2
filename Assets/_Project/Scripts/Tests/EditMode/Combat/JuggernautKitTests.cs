using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The Juggernaut constellation's mechanics as content will drive them
    // (docs/PLAN_BJORN_CONSTELLATIONS.md section 4): the fight-start arm for
    // the Phase 4 seams, Thick Blood's curve regen, Wrath, Gorge, Blood Price's
    // damage rider and Fury fill, Unbroken, Cursed Blood's once-per-fight cast
    // and the bot's valuation of the utility skills.
    //
    // FIXTURE ARITHMETIC. Bjorn: 260 max health, Attack 20, no flat Fury
    // gains. The foe has no defences and 1000 health, so a hit lands as its raw
    // figure: Attack 20 (x1 plus any attack bonus, rounded) + the skill's
    // flatAmount. Every expected figure below is written out as a literal.
    public class JuggernautKitTests
    {
        private const string GorgeId = "gorge";

        private static TalentEffectSet Talents(params TalentEffect[] effects) => new TalentEffectSet(effects);

        private static TalentEffect T(TalentEffectType type, int magnitude = 0, int threshold = 0,
            string skillId = null) =>
            new TalentEffect(type, magnitude, threshold,
                skillId ?? (TalentEffect.IsSkillScoped(type) ? GorgeId : ""));

        private static CombatantState Bjorn(TalentEffectSet talents, int fury = 0, int health = 260)
        {
            var pool = new ResourcePool("fury", "Fury", 100, 0, 0, 0);
            pool.Gain(fury);
            return new CombatantState("Bjorn", true, 260, pool, 20, 20)
            {
                CritChancePercent = 0,
                PhysicalDefense = 40,
                MagicalDefense = 12,
                Talents = talents,
                CurrentHealth = health,
            };
        }

        private static CombatantState Foe(int health = 1000) =>
            new CombatantState("Foe", false, 1000, 0, 1, 1) { CritChancePercent = 0, CurrentHealth = health };

        private static ResolvedSkill Strike(string id, int flat, int cost = 0, int lifesteal = 0) =>
            new ResolvedSkill(id, id, "test fixture", "bjorn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, cost, 0, false, 0, flat, false,
                null, SpellPresentation.None, 0,
                physicalMove: true, lifestealPercent: lifesteal);

        private static ResolvedSkill Window(string id, SkillEffect effect, int cost, int turns, int regenPercent = 0,
            bool oncePerFight = false) =>
            new ResolvedSkill(id, id, "test fixture", "bjorn", 1, effect, SkillTargeting.Self, cost, 0, false,
                0, 0, false, null, SpellPresentation.None, 0,
                windowTurns: turns, regenPercentOfMaxHealth: regenPercent, oncePerFight: oncePerFight);

        private static FightSession Fight(CombatantState bjorn, ResolvedSkill[] skills, params CombatantState[] foes)
        {
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, foes),
                new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, skills, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            Assert.AreSame(bjorn, session.Encounter.Current, "fixture: Bjorn's turn");
            return session;
        }

        // ---- the fight-start pass ------------------------------------------------------

        [Test]
        public void TheRoot_SetsTheJuggernautEngine_AndSecondWindsCooldown()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.FuryEngineJuggernaut),
                T(TalentEffectType.SkillCooldownTurns, 4, 0, "second_wind")));

            Fight(bjorn, null, Foe());

            Assert.AreEqual(FuryEngineKind.Juggernaut, bjorn.FuryEngine.Kind);
            Assert.AreEqual(4, bjorn.CooldownOverrides["second_wind"]);
        }

        [Test]
        public void NoTalents_LeavesEveryJuggernautSeamOff()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);

            Fight(bjorn, null, Foe());

            Assert.AreEqual(FuryEngineKind.None, bjorn.FuryEngine.Kind);
            Assert.IsNull(bjorn.CrowdControl.Unyielding);
            Assert.IsNull(bjorn.DelayedDamage);
            Assert.AreEqual(0, bjorn.ShortfallHealthPermille);
            Assert.AreEqual(0, bjorn.CooldownOverrides.Count);
        }

        [Test]
        public void UnyieldingTiers_SetTheCooldownAndTheFury()
        {
            var t1 = Bjorn(Talents(T(TalentEffectType.UnyieldingTier, 1)));
            var t2 = Bjorn(Talents(T(TalentEffectType.UnyieldingTier, 1), T(TalentEffectType.UnyieldingTier, 2)));
            var t3 = Bjorn(Talents(T(TalentEffectType.UnyieldingTier, 3)));
            Fight(t1, null, Foe());
            Fight(t2, null, Foe());
            Fight(t3, null, Foe());

            Assert.AreEqual(4, t1.CrowdControl.Unyielding.CooldownTurns);
            Assert.AreEqual(0, t1.CrowdControl.Unyielding.FuryGain);
            Assert.AreEqual(3, t2.CrowdControl.Unyielding.CooldownTurns);
            Assert.AreEqual(0, t2.CrowdControl.Unyielding.FuryGain);
            Assert.AreEqual(3, t3.CrowdControl.Unyielding.CooldownTurns, "owning T3 alone still reads as T1 and T2");
            Assert.AreEqual(20, t3.CrowdControl.Unyielding.FuryGain);
        }

        [Test]
        public void IgnorePain_ArmsThePoolWithItsPercentTurnsAndT3Flag()
        {
            var t2 = Bjorn(Talents(T(TalentEffectType.DelayedDamagePercent, 20, 3),
                T(TalentEffectType.DelayedDamagePercent, 30, 3)));
            var t3 = Bjorn(Talents(T(TalentEffectType.DelayedDamagePercent, 30, 3),
                T(TalentEffectType.HealReducesDelayedDamage)));
            Fight(t2, null, Foe());
            Fight(t3, null, Foe());

            Assert.AreEqual(30, t2.DelayedDamage.Percent, "the stronger tier wins");
            Assert.AreEqual(3, t2.DelayedDamage.Turns);
            Assert.IsFalse(t2.DelayedDamage.HealsReducePool);
            Assert.IsTrue(t3.DelayedDamage.HealsReducePool);
        }

        [Test]
        public void BloodPrice_SetsTheShortfallPermille()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.ShortfallPaidInHealthPermille, 5)));

            Fight(bjorn, null, Foe());

            Assert.AreEqual(5, bjorn.ShortfallHealthPermille);
        }

        // ---- Thick Blood's curve --------------------------------------------------------

        // Literal pins, worked by hand: floor + (ceiling - floor) x t^2 with
        // t^2 = 16 missing^2 / (9 max^2) under three quarters missing, then
        // floor(max x hundredths / 10000).
        [TestCase(260, 260, 2, 6, 5)]     // full health: 2% of 260 = 5.2
        [TestCase(195, 260, 2, 6, 6)]     // 75% health: 200 + 400 x 16 x 65^2 / (9 x 260^2) = 244 hundredths -> 6.3
        [TestCase(130, 260, 2, 6, 9)]     // half health: 377 hundredths -> 9.8
        [TestCase(65, 260, 2, 6, 15)]     // a quarter or less: the ceiling, 6% of 260 = 15.6
        [TestCase(1, 260, 2, 6, 15)]
        [TestCase(65, 260, 2, 8, 20)]     // the T3 ceiling, 8% of 260 = 20.8
        [TestCase(130, 260, 2, 8, 12)]    // half health, T3: 200 + 266 = 466 hundredths -> 12.1
        public void TheCurve_IsPinnedAtItsLandmarks(int current, int max, int floor, int ceiling, int expected)
        {
            Assert.AreEqual(expected, HealthCurveRegen.AmountFor(current, max, floor, ceiling));
        }

        [Test]
        public void TheCurve_IsAtLeastOneForAPositiveRule_AndZeroForNone()
        {
            Assert.AreEqual(1, HealthCurveRegen.AmountFor(10, 10, 2, 6), "2% of 10 floors to 0 and is lifted to 1");
            Assert.AreEqual(0, HealthCurveRegen.AmountFor(10, 100, 0, 0));
            Assert.AreEqual(0, HealthCurveRegen.AmountFor(10, 0, 2, 6));
        }

        [Test]
        public void TheRegen_HealsAtTurnStart_ThroughTheCurve()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.HealthCurveRegen, 6, 2)));
            var session = Fight(bjorn, null, Foe());
            bjorn.CurrentHealth = 65;

            session.TickStatusesForTest(bjorn);

            Assert.AreEqual(80, bjorn.CurrentHealth, "65 + 6% of 260 (15)");
        }

        [Test]
        public void TheRegen_DoesNothingAtFullHealth()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.HealthCurveRegen, 6, 2)));
            var session = Fight(bjorn, null, Foe());
            session.DrainBeats();

            session.TickStatusesForTest(bjorn);

            Assert.AreEqual(260, bjorn.CurrentHealth);
            Assert.IsEmpty(session.DrainBeats(), "no beat for a heal of nothing");
        }

        [Test]
        public void TheRegen_UnderCursedBlood_IsDealtToTheEnemyInstead()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.HealthCurveRegen, 6, 2)));
            var foe = Foe();
            var session = Fight(bjorn, null, foe);
            bjorn.CurrentHealth = 65;
            session.OpenCursedBlood(bjorn, 2);

            session.TickStatusesForTest(bjorn);

            Assert.AreEqual(65, bjorn.CurrentHealth, "nothing heals him");
            Assert.AreEqual(985, foe.CurrentHealth, "the 15 he would have healed, as Void damage");
        }

        // ---- Wrath ---------------------------------------------------------------------

        [TestCase(50, 65, 37)]     // +0.5% per point, 75% missing: 37.5 floored
        [TestCase(75, 65, 56)]     // 56.25
        [TestCase(100, 65, 75)]
        [TestCase(100, 260, 0)]    // full health: nothing
        [TestCase(100, 130, 50)]   // half health
        public void Wrath_ScalesWithMissingHealth(int rate, int health, int expectedPercent)
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.DamagePerMissingHealth, rate)), health: health);
            var session = Fight(bjorn, null, Foe());

            Assert.AreEqual(expectedPercent, session.AttackBonusForTest(bjorn));
        }

        [Test]
        public void Wrath_RaisesTheDamageOfACast()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.DamagePerMissingHealth, 50)), health: 65);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("cleave", 10) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(963, foe.CurrentHealth, "Attack 20 x 1.37 = 27.4 -> 27, plus 10");
        }

        // ---- Blood Price ---------------------------------------------------------------

        [Test]
        public void BloodPrice_PaysTheShortfallInHealth_AndT2AddsItsDamage()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.ShortfallPaidInHealthPermille, 5),
                T(TalentEffectType.BloodPaidDamagePercent, 15)));
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("cleave", 10, cost: 30) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(221, bjorn.CurrentHealth, "30 Fury short at 0.5% of 260 = 39 health");
            Assert.AreEqual(967, foe.CurrentHealth, "Attack 20 x 1.15 = 23, plus 10");
        }

        [Test]
        public void ACastPaidEntirelyInFury_GetsNoBloodPriceBonus()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.ShortfallPaidInHealthPermille, 5),
                T(TalentEffectType.BloodPaidDamagePercent, 15)), fury: 30);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("cleave", 10, cost: 30) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(260, bjorn.CurrentHealth);
            Assert.AreEqual(970, foe.CurrentHealth);
        }

        [Test]
        public void TheCheatDeathSave_FillsTheFury_OnlyWithItsFlag()
        {
            var filling = Bjorn(Talents(T(TalentEffectType.CheatDeathOncePerFight),
                T(TalentEffectType.CheatDeathFillsPrimary)), fury: 20, health: 10);
            var plain = Bjorn(Talents(T(TalentEffectType.CheatDeathOncePerFight)), fury: 20, health: 10);

            var saved = CombatMath.ApplyDamageDetailed(filling, 50);
            CombatMath.ApplyDamageDetailed(plain, 50);

            Assert.IsTrue(saved.CheatedDeath);
            Assert.AreEqual(1, filling.CurrentHealth);
            Assert.AreEqual(100, filling.PrimaryPool.Current);
            Assert.AreEqual(1, plain.CurrentHealth);
            Assert.AreEqual(20, plain.PrimaryPool.Current, "without the flag the save leaves Fury alone");
        }

        [Test]
        public void TheFuryFill_DoesNotFireForAnAlreadySpentSave()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.CheatDeathOncePerFight),
                T(TalentEffectType.CheatDeathFillsPrimary)), fury: 0, health: 10);

            CombatMath.ApplyDamageDetailed(bjorn, 50);
            bjorn.PrimaryPool.TrySpend(100);
            bjorn.CurrentHealth = 10;
            CombatMath.ApplyDamageDetailed(bjorn, 50);

            Assert.AreEqual(0, bjorn.CurrentHealth, "the second lethal blow kills");
            Assert.AreEqual(0, bjorn.PrimaryPool.Current);
        }

        // ---- Gorge ---------------------------------------------------------------------

        [Test]
        public void Gorge_HealsItsAuthoredShareOfTheDamage()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, fury: 30, health: 200);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike(GorgeId, 30, cost: 30, lifesteal: 30) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(950, foe.CurrentHealth, "Attack 20 + 30");
            Assert.AreEqual(215, bjorn.CurrentHealth, "30% of 50");
        }

        [Test]
        public void GorgeT2_RaisesTheShare_OnlyForItsOwnSkill()
        {
            var t2 = Talents(T(TalentEffectType.SkillLifestealPercent, 40));

            var named = Bjorn(t2, fury: 30, health: 200);
            var namedFoe = Foe();
            var namedSession = Fight(named, new[] { Strike(GorgeId, 30, cost: 30, lifesteal: 30) }, namedFoe);
            Assert.IsTrue(namedSession.CastSkill(0, namedFoe));
            Assert.AreEqual(220, named.CurrentHealth, "40% of 50 on the named skill");

            var other = Bjorn(t2, fury: 30, health: 200);
            var otherFoe = Foe();
            var otherSession = Fight(other, new[] { Strike("other", 30, cost: 30, lifesteal: 30) }, otherFoe);
            Assert.IsTrue(otherSession.CastSkill(0, otherFoe));
            Assert.AreEqual(215, other.CurrentHealth, "another skill keeps its own 30%");
        }

        [Test]
        public void GorgeT3_HitsHarderUnderHalfHealth_AndNotAbove()
        {
            var t3 = Talents(T(TalentEffectType.SkillDamageBonusBelowOwnHealth, 25, 50));

            var hurt = Bjorn(t3, fury: 30, health: 130);
            var hurtFoe = Foe();
            var hurtSession = Fight(hurt, new[] { Strike(GorgeId, 30, cost: 30) }, hurtFoe);
            Assert.IsTrue(hurtSession.CastSkill(0, hurtFoe));
            Assert.AreEqual(938, hurtFoe.CurrentHealth, "at exactly half health: 50 x 1.25 = 62");

            var healthy = Bjorn(t3, fury: 30, health: 131);
            var healthyFoe = Foe();
            var healthySession = Fight(healthy, new[] { Strike(GorgeId, 30, cost: 30) }, healthyFoe);
            Assert.IsTrue(healthySession.CastSkill(0, healthyFoe));
            Assert.AreEqual(950, healthyFoe.CurrentHealth, "one point above half: no bonus");
        }

        // ---- Unbroken and Cursed Blood ---------------------------------------------------

        [Test]
        public void Unbroken_OpensARegenAndUnstoppable()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, fury: 50, health: 100);
            var session = Fight(bjorn, new[] { Window("unbroken", SkillEffect.Unbroken, 50, 2, regenPercent: 15) }, Foe());

            Assert.IsTrue(session.CastSkill(0, null));

            var regen = bjorn.Statuses.Single(s => s.Type == StatusEffectType.Regen);
            Assert.AreEqual(39, regen.Magnitude, "15% of 260");
            Assert.AreEqual(1, regen.TurnsRemaining, "cast for 2; the first tick already landed at his next turn start");
            Assert.Greater(bjorn.CurrentHealth, 100, "and healed him");
            Assert.IsTrue(bjorn.CrowdControl.Unstoppable.IsOpen);
            Assert.AreEqual(0, bjorn.PrimaryPool.Current, "50 Fury paid");
        }

        [Test]
        public void CursedBlood_OpensTheWindow_AndCanBeCastOncePerFight()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, fury: 100, health: 100);
            var session = Fight(bjorn, new[]
            {
                Window("cursed", SkillEffect.CursedBlood, 50, 2, oncePerFight: true),
            }, Foe());

            Assert.AreEqual(0, session.CooldownRemaining(bjorn, "cursed"));
            Assert.IsTrue(session.CastSkill(0, null));

            Assert.IsTrue(bjorn.HealConversion.IsActive);
            Assert.AreEqual(FightSession.SpentForFight, session.CooldownRemaining(bjorn, "cursed"));

            for (int i = 0; i < 5; i++) session.TickCooldownsForTest(bjorn);
            Assert.AreEqual(FightSession.SpentForFight, session.CooldownRemaining(bjorn, "cursed"),
                "turns do not bring it back");
        }

        [Test]
        public void CursedBlood_ConvertsUnbrokensRegen()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, fury: 100, health: 100);
            var foe = Foe();
            var session = Fight(bjorn, null, foe);
            session.OpenCursedBlood(bjorn, 2);
            StatusEffects.Apply(bjorn.Statuses, StatusEffectType.Regen, 39, 2);

            session.TickStatusesForTest(bjorn);

            Assert.AreEqual(100, bjorn.CurrentHealth);
            Assert.AreEqual(961, foe.CurrentHealth, "the 39 he would have healed");
        }

        // ---- the bot's valuation ---------------------------------------------------------

        [TestCase(90, true)]
        [TestCase(91, true)]
        [TestCase(92, false)]
        public void UnbrokenIsWorthCasting_AtOrBelowThirtyFivePercent(int health, bool worth)
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, health: health);

            Assert.AreEqual(worth, UtilitySkills.UnbrokenWorthIt(bjorn));
        }

        [Test]
        public void UnbrokenIsNotRecast_WhileUnstoppableRuns()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, health: 50);
            bjorn.CrowdControl.Unstoppable.Open(2, true);

            Assert.IsFalse(UtilitySkills.UnbrokenWorthIt(bjorn));
        }

        [Test]
        public void CursedBloodIsWorthCasting_WhenTheRegenAboutToLandIsAShareOfHisHealth()
        {
            var bare = Bjorn(TalentEffectSet.Empty, health: 65);
            Assert.IsFalse(UtilitySkills.CursedBloodWorthIt(bare), "no heal source, nothing to convert");

            var regenerating = Bjorn(TalentEffectSet.Empty, health: 65);
            StatusEffects.Apply(regenerating.Statuses, StatusEffectType.Regen, 39, 2);
            Assert.IsTrue(UtilitySkills.CursedBloodWorthIt(regenerating), "2 x 39 = 78 of his 260");

            var trickle = Bjorn(TalentEffectSet.Empty, health: 65);
            StatusEffects.Apply(trickle.Statuses, StatusEffectType.Regen, 10, 2);
            Assert.IsFalse(UtilitySkills.CursedBloodWorthIt(trickle), "2 x 10 = 20 is under a fifth of 260 (52)");
        }

        [Test]
        public void SecondWindIsWorthCasting_WhenHurt_OrAsANukeUnderCursedBlood()
        {
            var healthy = Bjorn(TalentEffectSet.Empty, health: 200);
            Assert.IsFalse(UtilitySkills.SecondWindWorthIt(healthy));

            var hurt = Bjorn(TalentEffectSet.Empty, health: 90);
            Assert.IsTrue(UtilitySkills.SecondWindWorthIt(hurt));

            var cursed = Bjorn(TalentEffectSet.Empty, health: 195);
            cursed.HealConversion.Window.Open(2, true);
            Assert.IsTrue(UtilitySkills.SecondWindWorthIt(cursed), "65 missing is a quarter of 260: a real nuke");
        }

        [Test]
        public void TheRunnerPlaysAReadyUnbroken_WhenHeIsLow()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty, fury: 50, health: 80);
            var foe = Foe();
            var session = Fight(bjorn, new[]
            {
                Window("unbroken", SkillEffect.Unbroken, 50, 2, regenPercent: 15),
            }, foe);

            var legal = FightAction.LegalActions(session, bjorn, null);
            var pick = UtilitySkills.Choose(session, bjorn, legal);

            Assert.IsTrue(pick.HasValue);
            Assert.AreEqual(FightActionKind.Skill, pick.Value.Kind);
        }
    }
}
