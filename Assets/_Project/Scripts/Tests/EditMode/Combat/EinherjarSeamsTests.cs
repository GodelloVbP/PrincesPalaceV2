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
    // The Einherjar tree's engine seams (docs/PLAN_BJORN_CONSTELLATIONS.md,
    // section 3 and "Phase 4 engine seams"): Momentum, Battle Trance, Twin
    // Rampage, plus the per-holder cooldown Second Wind needs under the
    // Juggernaut root. Everything off by default.
    //
    // FIXTURE ARITHMETIC (the single-foe rig). Bjorn: 260 HP, Attack 20,
    // PhysicalDefense 40, MagicalDefense 12, so a raw physical hit lands
    // x 100 / 140 (raw 140 -> 100, raw 280 -> 200, raw 350 -> 250). The pool
    // has no flat gains, so every Fury change below is the seam under test.
    public class EinherjarSeamsTests
    {
        private sealed class Rig
        {
            public FightSession Session;
            public CombatantState Bjorn;
            public CombatantState Foe;
        }

        private static Rig Fight()
        {
            var bjorn = new CombatantState("Bjorn", true, 260,
                new ResourcePool("fury", "Fury", 100, 0, 0, 0), 20, 20)
            {
                CritChancePercent = 0,
                PhysicalDefense = 40,
                MagicalDefense = 12,
            };
            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1) { CritChancePercent = 0 };
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            Assert.AreSame(bjorn, session.Encounter.Current, "fixture: Bjorn's turn");
            return new Rig { Session = session, Bjorn = bjorn, Foe = foe };
        }

        private static List<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages).Concat(session.DrainImmediateMessages()).ToList();

        // One of his turns: deal `damage` (0 = none) and end it.
        private static void Turn(Rig r, int damage)
        {
            if (damage > 0) r.Session.DealForTest(r.Bjorn, r.Foe, damage);
            r.Session.TickStatusesAtTurnEndForTest(r.Bjorn);
        }

        // ---- Momentum ----------------------------------------------------------------------

        [Test]
        public void Momentum_IsOffByDefault()
        {
            var r = Fight();

            Turn(r, 20);
            Turn(r, 20);

            Assert.AreEqual(0, r.Bjorn.Momentum.Stacks);
            Assert.AreEqual(0, CritRules.ChanceFor(r.Bjorn));
            Assert.AreEqual(150, CritRules.DamagePercentFor(r.Bjorn));
        }

        [Test]
        public void Momentum_BuildsAStackPerConsecutiveDamagingTurn_FivePercentEach_CappedAtFive()
        {
            var r = Fight();
            r.Bjorn.Momentum.Enabled = true;

            Turn(r, 20);
            Assert.AreEqual(1, r.Bjorn.Momentum.Stacks);
            Assert.AreEqual(5, CritRules.ChanceFor(r.Bjorn));

            for (int i = 0; i < 6; i++) Turn(r, 20);

            Assert.AreEqual(5, r.Bjorn.Momentum.Stacks);
            Assert.AreEqual(25, CritRules.ChanceFor(r.Bjorn));
            Assert.AreEqual(150, CritRules.DamagePercentFor(r.Bjorn), "no crit damage without the T2 flag");
        }

        [Test]
        public void Momentum_T2_CapsAtEight_AndAddsFivePercentCritDamagePerStack()
        {
            var r = Fight();
            var momentum = r.Bjorn.Momentum;
            momentum.Enabled = true;
            momentum.ExtendedStackCap = true;
            momentum.CritDamagePerStackBonus = true;

            for (int i = 0; i < 10; i++) Turn(r, 20);

            Assert.AreEqual(8, momentum.Stacks);
            Assert.AreEqual(40, CritRules.ChanceFor(r.Bjorn));
            Assert.AreEqual(190, CritRules.DamagePercentFor(r.Bjorn));
        }

        [Test]
        public void Momentum_ATurnWithoutDamage_ResetsToZero()
        {
            var r = Fight();
            r.Bjorn.Momentum.Enabled = true;

            Turn(r, 20);
            Turn(r, 20);
            Turn(r, 20);
            Assert.AreEqual(3, r.Bjorn.Momentum.Stacks);

            Turn(r, 0);
            Assert.AreEqual(0, r.Bjorn.Momentum.Stacks);
            Assert.AreEqual(0, CritRules.ChanceFor(r.Bjorn));
        }

        [Test]
        public void Momentum_AHitTakenRemovesOneStack()
        {
            var r = Fight();
            r.Bjorn.Momentum.Enabled = true;
            r.Bjorn.Momentum.SetStacks(3);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);  // 20
            Assert.AreEqual(2, r.Bjorn.Momentum.Stacks);
            Assert.Contains("Bjorn loses momentum (2).", Messages(r.Session));

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);
            Assert.AreEqual(1, r.Bjorn.Momentum.Stacks, "one per hit, never all of them");
        }

        [Test]
        public void Momentum_T3_HitsUnderTenPercentOfMaxHp_KeepTheStack()
        {
            var r = Fight();
            var momentum = r.Bjorn.Momentum;
            momentum.Enabled = true;
            momentum.IgnoresSmallHits = true;
            momentum.SetStacks(3);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 35, DamageType.Physical);  // 25 < 26
            Assert.AreEqual(3, momentum.Stacks);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 42, DamageType.Physical);  // 30
            Assert.AreEqual(2, momentum.Stacks);
        }

        // ---- Battle Trance -----------------------------------------------------------------

        private static Rig TranceFight(int fury, BattleTrance trance)
        {
            var r = Fight();
            r.Bjorn.PrimaryPool.Gain(fury);
            r.Bjorn.BattleTrance = trance;
            return r;
        }

        [Test]
        public void BattleTrance_IsOffByDefault()
        {
            var r = Fight();
            r.Bjorn.PrimaryPool.Gain(80);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(160, r.Bjorn.CurrentHealth);
            Assert.AreEqual(80, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void BattleTrance_PaysTwentyPercentWithFury_OnePointPerPercentOfMaxHp()
        {
            var r = TranceFight(60, new BattleTrance());

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);  // 100

            Assert.AreEqual(180, r.Bjorn.CurrentHealth, "80 of the 100 to health");
            Assert.AreEqual(52, r.Bjorn.PrimaryPool.Current, "20 soaked = 7.7% of 260, rounded up to 8 Fury");
        }

        [Test]
        public void BattleTrance_T2_IsThirtyPercent_AndDoublesWhileTransformed()
        {
            var r = TranceFight(80, new BattleTrance(30) { DoublesWhileTransformed = true });

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            Assert.AreEqual(190, r.Bjorn.CurrentHealth, "30 of the 100 soaked");
            Assert.AreEqual(68, r.Bjorn.PrimaryPool.Current, "11.5% of max HP -> 12 Fury");

            Transformation.Enter(r.Bjorn, "Berserk", 3, 0, 0, 0);
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            Assert.AreEqual(150, r.Bjorn.CurrentHealth, "60 soaked in Berserk");
            Assert.AreEqual(44, r.Bjorn.PrimaryPool.Current, "23.1% -> 24 Fury");
        }

        [Test]
        public void BattleTrance_BelowTheThreshold_SoaksNothing()
        {
            var r = TranceFight(49, new BattleTrance());

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(160, r.Bjorn.CurrentHealth);
            Assert.AreEqual(49, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void BattleTrance_AtTheThreshold_Soaks()
        {
            var r = TranceFight(50, new BattleTrance());

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(180, r.Bjorn.CurrentHealth);
            Assert.AreEqual(42, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void BattleTrance_T3_ASoakThatTakesFuryBelowTheThreshold_GrantsProtect_AndTheHookFires()
        {
            var r = TranceFight(55, new BattleTrance { ProtectWhenTranceBreaks = true });

            CombatantState broke = null;
            r.Session.BattleTranceBroke += holder => broke = holder;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);  // 100: 20 soaked for 8

            Assert.AreEqual(47, r.Bjorn.PrimaryPool.Current, "55 -> 47 crosses the 50 line");
            Assert.AreEqual(180, r.Bjorn.CurrentHealth);
            Assert.AreSame(r.Bjorn, broke);

            var protect = r.Bjorn.Statuses.Single(s => s.Type == StatusEffectType.Protect);
            Assert.AreEqual(50, protect.Magnitude);
            Assert.AreEqual(1, protect.TurnsRemaining);
        }

        [Test]
        public void BattleTrance_T3_ASoakThatStaysAtTheThreshold_GrantsNothing()
        {
            var r = TranceFight(58, new BattleTrance { ProtectWhenTranceBreaks = true });

            CombatantState broke = null;
            r.Session.BattleTranceBroke += holder => broke = holder;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(50, r.Bjorn.PrimaryPool.Current, "58 -> 50: still at the threshold, still soaking");
            Assert.IsNull(broke);
            Assert.IsFalse(r.Bjorn.Statuses.Any(s => s.Type == StatusEffectType.Protect));
        }

        [Test]
        public void BattleTrance_AFullPoolSoak_EmptiesTheBar_AndBreaksTheTrance()
        {
            var trance = new BattleTrance(30) { DoublesWhileTransformed = true, ProtectWhenTranceBreaks = true };
            var r = TranceFight(50, trance);
            Transformation.Enter(r.Bjorn, "Berserk", 3, 0, 0, 0);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 350, DamageType.Physical);  // 250; 60% wants 150

            Assert.AreEqual(0, r.Bjorn.PrimaryPool.Current, "50 Fury covers only 130 of the 150");
            Assert.AreEqual(140, r.Bjorn.CurrentHealth, "250 - 130 = 120 to health");
            Assert.IsTrue(r.Bjorn.Statuses.Any(s => s.Type == StatusEffectType.Protect));
        }

        [Test]
        public void BattleTrance_BreakingWithoutT3_GrantsNoProtect()
        {
            var r = TranceFight(55, new BattleTrance());

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(47, r.Bjorn.PrimaryPool.Current);
            Assert.IsFalse(r.Bjorn.Statuses.Any(s => s.Type == StatusEffectType.Protect));
        }

        // ---- Twin Rampage ----------------------------------------------------------------

        // Rampage as authored (skills.json): 19 per target for Attack 10
        // (PhaseFourSkillTests pins that), tiers 50% -> x2 and 100% -> x4.
        private static ResolvedSkill Authored(string id)
        {
            var raw = ContentDataFiles.ParseFile<RawSkillFile>(ContentDataFiles.DataPath("skills.json")).skills;
            Assert.IsTrue(SkillEntryResolver.TryResolveAll(raw, null, new[] { "bear" }, out var resolved, out _));
            return resolved.Single(s => s.Id == id);
        }

        private static ResolvedSkill AuthoredRampage() => Authored("rampage");

        private sealed class SweepRig
        {
            public FightSession Session;
            public CombatantState Bjorn;
            public CombatantState Foe1;
            public CombatantState Foe2;
        }

        private static SweepRig SweepFight(bool twin)
        {
            var bjorn = new CombatantState("Bjorn", true, 500, new ResourcePool("fury", "Fury", 100, 0, 0, 0), 10, 20)
            {
                CritChancePercent = 0,
            };
            bjorn.FuryEngine.Kind = FuryEngineKind.Einherjar;
            if (twin) bjorn.TwinRampage = new TwinRampageRule();

            var foe1 = new CombatantState("Foe1", false, 1000, 10, 30, 1) { CritChancePercent = 0 };
            var foe2 = new CombatantState("Foe2", false, 1000, 10, 30, 1) { CritChancePercent = 0 };
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe1, foe2 }),
                new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, new[] { AuthoredRampage() }, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return new SweepRig { Session = session, Bjorn = bjorn, Foe1 = foe1, Foe2 = foe2 };
        }

        [Test]
        public void WithoutTheRule_AFullRampageIsOneSweep()
        {
            var r = SweepFight(twin: false);
            r.Bjorn.PrimaryPool.Gain(100);

            Assert.IsTrue(r.Session.CastSkill(0, r.Foe1));

            var said = Messages(r.Session);
            Assert.AreEqual(924, r.Foe1.CurrentHealth, "19 x 4");
            Assert.AreEqual(924, r.Foe2.CurrentHealth);
            Assert.IsFalse(said.Any(m => m.Contains("stunned")), "nobody is stunned");
            Assert.AreEqual(30, r.Bjorn.PrimaryPool.Current, "76 / 10 x 10 clamps to 30");
        }

        [Test]
        public void TwinRampage_ASecondSweepAtOneTimes_StunsEveryoneItHits()
        {
            var r = SweepFight(twin: true);
            r.Bjorn.PrimaryPool.Gain(100);

            Assert.IsTrue(r.Session.CastSkill(0, r.Foe1));

            Assert.AreEqual(905, r.Foe1.CurrentHealth, "76 + 19");
            Assert.AreEqual(905, r.Foe2.CurrentHealth);
            var said = Messages(r.Session);
            Assert.Contains("Foe1 is stunned!", said);
            Assert.Contains("Foe2 is stunned!", said);
            Assert.AreEqual(5, r.Bjorn.TwinRampage.Cooldown.TurnsRemaining);
        }

        [Test]
        public void TwinRampage_TheFirstSweepsFuryDoesNotRefillTheBarBeforeTheSecond()
        {
            var r = SweepFight(twin: true);
            r.Bjorn.PrimaryPool.Gain(100);

            r.Session.CastSkill(0, r.Foe1);

            var sweeps = r.Session.DrainBeats()
                .Where(b => b.Cause == BeatCause.Action && ReferenceEquals(b.Actor, r.Bjorn)).ToList();
            Assert.AreEqual(2, sweeps.Count, "one beat per sweep");
            Assert.AreEqual(0, sweeps[0].Snapshot[r.Bjorn].Primary, "empty after the first sweep");
            Assert.AreEqual(30, sweeps[1].Snapshot[r.Bjorn].Primary,
                "paid once, after the second, for the action's largest hit (76)");
            Assert.AreEqual(30, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void TwinRampage_DuringTheCooldown_AFullRampageIsAnOrdinaryOne()
        {
            var r = SweepFight(twin: true);
            r.Bjorn.PrimaryPool.Gain(100);
            r.Session.CastSkill(0, r.Foe1);
            Assert.AreSame(r.Bjorn, r.Session.Encounter.Current);
            Messages(r.Session);

            r.Bjorn.PrimaryPool.Gain(100);
            Assert.IsTrue(r.Session.CastSkill(0, r.Foe1));

            Assert.AreEqual(829, r.Foe1.CurrentHealth, "905 - 76: one sweep");
            Assert.IsFalse(Messages(r.Session).Any(m => m.Contains("stunned")), "no stun this time");
            Assert.AreEqual(4, r.Bjorn.TwinRampage.Cooldown.TurnsRemaining, "one of his turns has passed");
        }

        // ---- Second Wind's per-holder cooldown ----------------------------------------------

        private static ResolvedSkill SecondWind() => Authored("second_wind");

        private static (FightSession session, CombatantState bjorn) SecondWindFight(int overrideTurns)
        {
            var bjorn = new CombatantState("Bjorn", true, 500, new ResourcePool("fury", "Fury", 100, 0, 0, 0), 10, 20)
            {
                CritChancePercent = 0,
            };
            if (overrideTurns > 0) bjorn.CooldownOverrides["second_wind"] = overrideTurns;

            var foe = new CombatantState("Foe", false, 1000, 10, 1, 1) { CritChancePercent = 0 };
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, new[] { SecondWind() }, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            return (session, bjorn);
        }

        [Test]
        public void SecondWind_HasNoCooldownOfItsOwn()
        {
            var (session, bjorn) = SecondWindFight(0);
            bjorn.PrimaryPool.Gain(50);

            Assert.IsTrue(session.CastSkill(0, bjorn));
            Assert.AreEqual(0, session.CooldownRemaining(bjorn, "second_wind"));
        }

        [Test]
        public void SecondWind_UnderTheJuggernautOverride_WaitsFourTurns()
        {
            var (session, bjorn) = SecondWindFight(4);
            bjorn.PrimaryPool.Gain(50);

            Assert.IsTrue(session.CastSkill(0, bjorn));
            Assert.AreSame(bjorn, session.Encounter.Current);
            Assert.AreEqual(3, session.CooldownRemaining(bjorn, "second_wind"),
                "4 charged, one of his turn starts already ticked");

            bjorn.PrimaryPool.Gain(50);
            Assert.IsFalse(session.CastSkill(0, bjorn), "still cooling down");
        }
    }
}
