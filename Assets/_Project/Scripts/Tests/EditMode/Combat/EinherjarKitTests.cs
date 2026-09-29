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
    // The Einherjar constellation's mechanics as content will drive them
    // (docs/PLAN_BJORN_CONSTELLATIONS.md section 3): the talent effects that
    // arm the engine seams, Hack's two blows, the Slam's Fury-tier riders,
    // Headsplitter's wound scaling and refund, and Berserk as a form kept up
    // by Fury.
    //
    // FIXTURE ARITHMETIC. Bjorn: Attack 20, no flat Fury gains (the pool's
    // are 0), so every Fury figure below is the engine or a rider. The foe
    // has no defences unless a test gives it some, so a hit lands as its raw
    // figure: Attack 20 + the skill's flatAmount. Fury per hit under the
    // Einherjar engine is floor(hit x 10 / 20), clamped 5-30.
    public class EinherjarKitTests
    {
        private static TalentEffectSet Talents(params TalentEffect[] effects) => new TalentEffectSet(effects);

        private static TalentEffect T(TalentEffectType type, int magnitude = 0, int threshold = 0) =>
            new TalentEffect(type, magnitude, threshold);

        private static CombatantState Bjorn(TalentEffectSet talents, int critChance = 0, int fury = 0)
        {
            var pool = new ResourcePool("fury", "Fury", 100, 0, 0, 0);
            pool.Gain(fury);
            return new CombatantState("Bjorn", true, 260, pool, 20, 20)
            {
                CritChancePercent = critChance,
                PhysicalDefense = 40,
                MagicalDefense = 12,
                Talents = talents,
            };
        }

        private static CombatantState Foe(int physicalDefense = 0, int health = 1000) =>
            new CombatantState("Foe", false, 1000, 0, 1, 1)
            {
                CritChancePercent = 0,
                PhysicalDefense = physicalDefense,
                CurrentHealth = health,
            };

        private static ResolvedSkill Strike(string id, int flat, int hitCount = 1, ResolvedPoolTier[] tiers = null,
            int power = 0, int cost = 0, bool spendsAllPrimary = false, int missingPercent = 0, int refundPercent = 0) =>
            new ResolvedSkill(id, id, "test fixture", "bjorn", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, cost, 0, false, power, flat, false,
                null, SpellPresentation.None, 0,
                physicalMove: true, poolTiers: tiers, spendsAllPrimary: spendsAllPrimary,
                hitCount: hitCount, damagePerMissingHealthPercent: missingPercent,
                refundsSpentOnKillPercent: refundPercent);

        private static readonly ResolvedPoolTier[] SlamTiers =
        {
            new ResolvedPoolTier(0.5f, 2f),
            new ResolvedPoolTier(1f, 4f),
        };

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

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        private static readonly TalentEffect Engine = new TalentEffect(TalentEffectType.FuryEngineEinherjar, 0);

        // ---- the fight-start pass ------------------------------------------------------

        [Test]
        public void TheRootTalent_SetsTheEinherjarEngine_AtFightStart()
        {
            var bjorn = Bjorn(Talents(Engine));

            Fight(bjorn, null, Foe());

            Assert.AreEqual(FuryEngineKind.Einherjar, bjorn.FuryEngine.Kind);
        }

        [Test]
        public void NoTalents_LeavesEverySeamOff()
        {
            var bjorn = Bjorn(TalentEffectSet.Empty);

            Fight(bjorn, null, Foe());

            Assert.AreEqual(FuryEngineKind.None, bjorn.FuryEngine.Kind);
            Assert.IsFalse(bjorn.Momentum.Enabled);
            Assert.IsNull(bjorn.BattleTrance);
            Assert.IsNull(bjorn.TwinRampage);
        }

        [Test]
        public void MomentumTiers_SwitchOnTheirThreeHalves()
        {
            var t1 = Bjorn(Talents(T(TalentEffectType.MomentumTier, 1)));
            var t2 = Bjorn(Talents(T(TalentEffectType.MomentumTier, 1), T(TalentEffectType.MomentumTier, 2)));
            var t3 = Bjorn(Talents(T(TalentEffectType.MomentumTier, 3)));
            Fight(t1, null, Foe());
            Fight(t2, null, Foe());
            Fight(t3, null, Foe());

            Assert.IsTrue(t1.Momentum.Enabled);
            Assert.IsFalse(t1.Momentum.ExtendedStackCap);
            Assert.IsFalse(t1.Momentum.IgnoresSmallHits);

            Assert.IsTrue(t2.Momentum.ExtendedStackCap);
            Assert.IsTrue(t2.Momentum.CritDamagePerStackBonus);
            Assert.IsFalse(t2.Momentum.IgnoresSmallHits);

            Assert.IsTrue(t3.Momentum.Enabled && t3.Momentum.ExtendedStackCap && t3.Momentum.IgnoresSmallHits,
                "owning T3 alone still reads as T1 and T2");
        }

        [Test]
        public void BattleTranceTiers_SetPercentDoublingAndProtect()
        {
            var t1 = Bjorn(Talents(T(TalentEffectType.BattleTranceTier, 1)));
            var t2 = Bjorn(Talents(T(TalentEffectType.BattleTranceTier, 2)));
            var t3 = Bjorn(Talents(T(TalentEffectType.BattleTranceTier, 3)));
            Fight(t1, null, Foe());
            Fight(t2, null, Foe());
            Fight(t3, null, Foe());

            Assert.AreEqual(20, t1.BattleTrance.Percent);
            Assert.IsFalse(t1.BattleTrance.DoublesWhileTransformed);

            Assert.AreEqual(30, t2.BattleTrance.Percent);
            Assert.IsTrue(t2.BattleTrance.DoublesWhileTransformed);
            Assert.IsFalse(t2.BattleTrance.ProtectWhenTranceBreaks);

            Assert.AreEqual(30, t3.BattleTrance.Percent);
            Assert.IsTrue(t3.BattleTrance.ProtectWhenTranceBreaks);
        }

        [Test]
        public void TheCapstone_ArmsTwinRampage()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.TwinRampage)));

            Fight(bjorn, null, Foe());

            Assert.IsNotNull(bjorn.TwinRampage);
            Assert.AreEqual("rampage", bjorn.TwinRampage.SkillId);
        }

        // ---- Hack ------------------------------------------------------------------------

        [Test]
        public void Hack_StrikesTwice_AndEachBlowPaysTheEngineSeparately()
        {
            var bjorn = Bjorn(Talents(Engine));
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("hack", 10, hitCount: 2) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(940, foe.CurrentHealth, "two blows of 20 + 10");
            Assert.AreEqual(30, bjorn.PrimaryPool.Current, "15 per blow: floor(30 x 10 / 20), twice");
        }

        [Test]
        public void AnOrdinarySingleBlow_PaysTheEngineOnce()
        {
            var bjorn = Bjorn(Talents(Engine));
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("cleave", 10) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(970, foe.CurrentHealth);
            Assert.AreEqual(15, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void Hack_TheSecondBlowIsSkippedWhenTheFirstKills()
        {
            var bjorn = Bjorn(Talents(Engine));
            var foe = Foe(health: 20);
            var other = Foe();
            var session = Fight(bjorn, new[] { Strike("hack", 10, hitCount: 2) }, foe, other);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.IsFalse(foe.IsAlive);
            Assert.AreEqual(1000, other.CurrentHealth, "the second blow does not wander to another enemy");
            Assert.AreEqual(15, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void Hack_PreviewIsBothBlows()
        {
            var bjorn = Bjorn(Talents(Engine));
            var hack = Strike("hack", 10, hitCount: 2);
            var session = Fight(bjorn, new[] { hack }, Foe());

            Assert.AreEqual(60, session.PreviewSkillPower(bjorn, hack));
        }

        // ---- the Slam's riders --------------------------------------------------------------

        [Test]
        public void Slam_AtFiftyFury_GetsFifteenCritChance_AndACritRestoresTenFury()
        {
            // 85 base + 15 from the tier = 100: a certain crit.
            var bjorn = Bjorn(Talents(Engine,
                T(TalentEffectType.SlamCritChanceAtFury, 15),
                T(TalentEffectType.SlamCritRestoresFury, 10)), critChance: 85, fury: 50);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("slam", 10, tiers: SlamTiers) }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(910, foe.CurrentHealth, "(20 + 10) x 2 tier x 1.5 crit = 90");
            Assert.AreEqual(40, bjorn.PrimaryPool.Current, "50 spent, +30 engine (90 x 10 / 20 clamps to 30), +10 crit restore");
            Assert.AreEqual(0, bjorn.CastCritChanceBonus, "the cast's bonus does not outlive it");
        }

        [Test]
        public void Slam_BelowTheFirstTier_GetsNoTierCritChance()
        {
            var bjorn = Bjorn(Talents(Engine, T(TalentEffectType.SlamCritChanceAtFury, 15)), critChance: 85, fury: 40);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Strike("slam", 10, tiers: SlamTiers) }, foe);

            session.CastSkill(0, foe);

            Assert.AreEqual(0, bjorn.CastCritChanceBonus);
            Assert.AreEqual(85, CritRules.ChanceFor(bjorn));
        }

        [Test]
        public void Slam_AtFullFury_IgnoresPartOfTheTargetsDefense_ButNotAtHalf()
        {
            // Foe defense 100: a hit lands x 100 / 200. Ignoring 50% reads
            // defense 50: x 100 / 150.
            var talents = Talents(Engine, T(TalentEffectType.SlamIgnoresDefenseAtFullFury, 50));

            var full = Bjorn(talents, fury: 100);
            var fullFoe = Foe(physicalDefense: 100);
            Fight(full, new[] { Strike("slam", 10, tiers: SlamTiers) }, fullFoe).CastSkill(0, fullFoe);
            Assert.AreEqual(920, fullFoe.CurrentHealth, "(20 + 10) x 4 = 120, x 100 / 150 = 80");

            var half = Bjorn(talents, fury: 50);
            var halfFoe = Foe(physicalDefense: 100);
            Fight(half, new[] { Strike("slam", 10, tiers: SlamTiers) }, halfFoe).CastSkill(0, halfFoe);
            Assert.AreEqual(970, halfFoe.CurrentHealth, "(20 + 10) x 2 = 60, x 100 / 200 = 30: no ignore below the full tier");
        }

        // ---- Bloodfire and the crit bonus ------------------------------------------------------

        [Test]
        public void AKill_RefundsFury_AndMoreWhileTransformed()
        {
            var talents = Talents(Engine, T(TalentEffectType.FuryOnKill, 15),
                T(TalentEffectType.FuryOnKillWhileTransformed, 20));

            var plain = Bjorn(talents);
            var plainFoe = Foe(health: 5);
            var plainSession = Fight(plain, new[] { Strike("cleave", 10) }, plainFoe, Foe());
            plainSession.CastSkill(0, plainFoe);
            Assert.AreEqual(30, plain.PrimaryPool.Current, "15 engine + 15 for the kill");

            var berserk = Bjorn(talents);
            var berserkFoe = Foe(health: 5);
            var berserkSession = Fight(berserk, new[] { Strike("cleave", 10) }, berserkFoe, Foe());
            Transformation.Enter(berserk, "Berserk", 3, 0, 0, 0);
            berserkSession.CastSkill(0, berserkFoe);
            Assert.AreEqual(50, berserk.PrimaryPool.Current, "15 engine + 15 + 20 while transformed");
        }

        [Test]
        public void ACrit_PaysHalfAgainOfTheEngine_WithTheNode()
        {
            var with = Bjorn(Talents(Engine, T(TalentEffectType.CritFuryBonusPercent, 50)), critChance: 100);
            var withFoe = Foe();
            Fight(with, new[] { Strike("cleave", 10) }, withFoe).CastSkill(0, withFoe);
            Assert.AreEqual(33, with.PrimaryPool.Current, "a 45 crit pays floor(45 x 10 / 20) = 22, +11 (50%)");

            var without = Bjorn(Talents(Engine), critChance: 100);
            var withoutFoe = Foe();
            Fight(without, new[] { Strike("cleave", 10) }, withoutFoe).CastSkill(0, withoutFoe);
            Assert.AreEqual(22, without.PrimaryPool.Current);
        }

        [Test]
        public void BloodfireT2_ForgivesTheFirstIdleDrain_Once()
        {
            var bjorn = Bjorn(Talents(Engine, T(TalentEffectType.FirstIdleTurnFree)));
            bjorn.PrimaryPool.DecayPerIdleTurn = 10;
            var session = Fight(bjorn, null, Foe());
            bjorn.PrimaryPool.Gain(50);

            session.TickPrimaryPoolForTest(bjorn);
            Assert.AreEqual(50, bjorn.PrimaryPool.Current, "the first idle turn drains nothing");

            session.TickPrimaryPoolForTest(bjorn);
            Assert.AreEqual(40, bjorn.PrimaryPool.Current, "the second does");
        }

        [Test]
        public void WithoutTheNode_TheFirstIdleTurnDrains()
        {
            var bjorn = Bjorn(Talents(Engine));
            bjorn.PrimaryPool.DecayPerIdleTurn = 10;
            var session = Fight(bjorn, null, Foe());
            bjorn.PrimaryPool.Gain(50);

            session.TickPrimaryPoolForTest(bjorn);

            Assert.AreEqual(40, bjorn.PrimaryPool.Current);
        }

        // ---- situational crit chance ---------------------------------------------------------------

        [Test]
        public void CritChance_AgainstAWoundedTarget_ReadsTheTargetsHealthInclusively()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.CritChanceBelowTargetHealth, 25, 30)));
            var foe = Foe();

            foe.CurrentHealth = 300;
            Assert.AreEqual(25, CritRules.ChanceFor(bjorn, foe), "exactly 30%");
            foe.CurrentHealth = 301;
            Assert.AreEqual(0, CritRules.ChanceFor(bjorn, foe), "just above");
            Assert.AreEqual(0, CritRules.ChanceFor(bjorn), "no target, no bonus");
        }

        [Test]
        public void CritChance_WhileTransformed_AddsTheNodesTen()
        {
            var bjorn = Bjorn(Talents(T(TalentEffectType.CritChanceWhileTransformed, 10)));
            Assert.AreEqual(0, CritRules.ChanceFor(bjorn));

            Transformation.Enter(bjorn, "Berserk", 3, 0, 0, 0);

            Assert.AreEqual(10, CritRules.ChanceFor(bjorn));
        }

        // ---- Headsplitter ------------------------------------------------------------------------------

        private static ResolvedSkill Headsplitter() =>
            Strike("headsplitter", 10, power: 1, cost: 30, spendsAllPrimary: true, missingPercent: 100, refundPercent: 50);

        [Test]
        public void Headsplitter_SpendsAllFury_AndScalesWithFuryAndWounds()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 60);
            var foe = Foe();
            foe.CurrentHealth = 500;
            var session = Fight(bjorn, new[] { Headsplitter() }, foe);

            Assert.IsTrue(session.CastSkill(0, foe));

            // raw 20 + 10 + 60 Fury = 90; the target is half gone: +50% = 135.
            Assert.AreEqual(365, foe.CurrentHealth);
            Assert.AreEqual(30, bjorn.PrimaryPool.Current, "all 60 spent, +30 engine (135 x 10 / 20 clamps to 30)");
        }

        [Test]
        public void Headsplitter_IsRefusedBelowItsMinimum()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 29);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Headsplitter() }, foe);

            Assert.IsFalse(session.CastSkill(0, foe));
            Assert.AreEqual(1000, foe.CurrentHealth);
        }

        [Test]
        public void Headsplitter_AKillRefundsHalfTheFurySpent()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 60);
            var foe = Foe(health: 100);
            var session = Fight(bjorn, new[] { Headsplitter() }, foe, Foe());

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.IsFalse(foe.IsAlive);
            Assert.AreEqual(60, bjorn.PrimaryPool.Current, "30 refunded (half of 60) + 30 engine");
        }

        [Test]
        public void Headsplitter_T3_AKillFillsMomentum()
        {
            var bjorn = Bjorn(Talents(Engine, T(TalentEffectType.MomentumTier, 1), T(TalentEffectType.KillFillsMomentum)),
                fury: 60);
            var foe = Foe(health: 100);
            var session = Fight(bjorn, new[] { Headsplitter() }, foe, Foe());

            session.CastSkill(0, foe);

            Assert.AreEqual(5, bjorn.Momentum.Stacks);
        }

        [Test]
        public void Headsplitter_ANonKillFillsNothing()
        {
            var bjorn = Bjorn(Talents(Engine, T(TalentEffectType.MomentumTier, 1), T(TalentEffectType.KillFillsMomentum)),
                fury: 60);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Headsplitter() }, foe);

            session.CastSkill(0, foe);

            Assert.AreEqual(1, bjorn.Momentum.Stacks, "just the turn's own stack");
        }

        // ---- Berserk --------------------------------------------------------------------------------------

        private static ResolvedSkill Berserk() =>
            new ResolvedSkill("berserk", "Berserk", "test fixture", "bjorn", 1, SkillEffect.Transform,
                SkillTargeting.Self, 50, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                transform: new TransformGrant
                {
                    displayName = "Berserk",
                    defenseToAttackPercent = 50,
                    primaryDrainPerTurn = 10,
                    forbidsEffects = new[] { "Provoke", "Ward" },
                });

        private static ResolvedSkill Bellow() =>
            new ResolvedSkill("bellow", "Bellow", "test fixture", "bjorn", 1, SkillEffect.Provoke,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0);

        [Test]
        public void Berserk_MovesHalfOfBothDefensesIntoAttack_AndGivesThemBack()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 100);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Berserk() }, foe);

            Assert.IsTrue(session.CastSkill(0, null));

            Assert.IsNotNull(bjorn.Transformation);
            Assert.AreEqual(46, bjorn.Attack, "20 + 20 of 40 physical + 6 of 12 magical");
            Assert.AreEqual(20, bjorn.PhysicalDefense);
            Assert.AreEqual(6, bjorn.MagicalDefense);

            Transformation.Exit(bjorn);

            Assert.AreEqual(20, bjorn.Attack);
            Assert.AreEqual(40, bjorn.PhysicalDefense);
            Assert.AreEqual(12, bjorn.MagicalDefense);
        }

        [Test]
        public void Berserk_DrainsTenFuryEachOfHisTurns_AndHasNoIdleDecay()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 100);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Berserk() }, foe);

            // After Begin, so the opening turn start does not decay the pool.
            bjorn.PrimaryPool.DecayPerIdleTurn = 25;

            session.CastSkill(0, null);

            // 100 - 50 to enter = 50; his next turn start drains 10, and the
            // idle decay of 25 does not apply on top.
            Assert.AreSame(bjorn, session.Encounter.Current);
            Assert.IsNotNull(bjorn.Transformation);
            Assert.AreEqual(40, bjorn.PrimaryPool.Current);
        }

        [Test]
        public void Berserk_EndsWhenTheFuryRunsOut()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 50);
            var session = Fight(bjorn, new[] { Berserk() }, Foe());

            session.CastSkill(0, null);

            // 50 spent to enter leaves 0; the next turn start cannot pay.
            Assert.IsNull(bjorn.Transformation);
            Assert.AreEqual(20, bjorn.Attack);
            Assert.AreEqual(40, bjorn.PhysicalDefense);
            Assert.AreEqual(12, bjorn.MagicalDefense);
        }

        [Test]
        public void Berserk_ForbidsBellowAndShieldsWhileItRuns()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 100);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Berserk(), Bellow() }, foe);

            Assert.IsTrue(session.CastSkill(0, null));
            var options = session.SkillOptionsFor(bjorn).ToList();

            Assert.IsTrue(options.Single(o => o.Skill.Id == "bellow").Restricted);
            Assert.IsFalse(session.CastSkill(1, foe));
        }

        [Test]
        public void ForbiddenSkills_AreCastableOutsideTheForm()
        {
            var bjorn = Bjorn(Talents(Engine), fury: 100);
            var foe = Foe();
            var session = Fight(bjorn, new[] { Berserk(), Bellow() }, foe);

            Assert.IsTrue(session.CastSkill(1, foe));
        }

        [Test]
        public void Berserk_BattleTranceDoublesWhileTransformed()
        {
            var bjorn = Bjorn(Talents(Engine, T(TalentEffectType.BattleTranceTier, 2)), fury: 100);
            var session = Fight(bjorn, new[] { Berserk() }, Foe());

            Assert.AreEqual(30, bjorn.BattleTrance.PercentFor(bjorn));
            session.CastSkill(0, null);

            Assert.AreEqual(60, bjorn.BattleTrance.PercentFor(bjorn));
        }

        // ---- the bot's valuation of Berserk ------------------------------------------------------------

        private sealed class AlwaysAttack : IFightPolicy
        {
            public FightAction Choose(FightSession session, CombatantState actor,
                IReadOnlyList<FightAction> legal, SeededRandom rng) =>
                legal.First(a => a.Kind == FightActionKind.Attack);
        }

        private static bool BotWoreBerserk(int fury)
        {
            var bjorn = Bjorn(Talents(Engine), fury: fury);
            var foe = Foe();
            // One attack kills it, so a fight where the bot does not cast
            // Berserk ends on its first command and never lets Fury climb.
            foe.CurrentHealth = 15;
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, new[] { Berserk() }, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            var trace = new FightTrace();

            FightRunner.Play(session, new AlwaysAttack(), null, new SeededRandom(5), trace,
                transformUse: TransformUse.WhenReady);

            return trace.TransformedActors.Contains("Bjorn");
        }

        [Test]
        public void TheBot_EntersBerserk_OnlyWithThreeTurnsOfFuryLeftAfterPaying()
        {
            Assert.IsTrue(BotWoreBerserk(80), "80 - 50 = 30 = three turns of 10");
            Assert.IsFalse(BotWoreBerserk(79), "29 left is not three turns");
        }
    }
}
