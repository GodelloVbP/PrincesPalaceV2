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
    // Phase 2, the root Fury engines (docs/PLAN_BJORN_CONSTELLATIONS.md,
    // sections 1-4 and "Phase 4 engine seams"): Sentinel per hit taken on the
    // RAW incoming figure, Einherjar per damaging action on its largest hit,
    // Juggernaut per turn start with idle decay off. Engine None is today's
    // flat gainOnAttack / gainOnDamageTaken, unchanged.
    //
    // FIXTURE ARITHMETIC. Bjorn: 260 HP, Attack 20, PhysicalDefense 40,
    // MagicalDefense 12, so a raw physical hit lands x 100 / 140 (raw 28 ->
    // 20, raw 140 -> 100, raw 280 -> 200). His planted shield is 130. The
    // pool is authored with flat gains 15 (attack) / 10 (taken) so an engine
    // that fails to replace them shows up as an extra 15 or 10. The foe has
    // no defences, so what is dealt to it lands.
    public class FuryEngineTests
    {
        private sealed class Rig
        {
            public FightSession Session;
            public CombatantState Bjorn;
            public CombatantState Foe;
        }

        private static Rig Fight(FuryEngineKind engine)
        {
            var bjorn = new CombatantState("Bjorn", true, 260,
                new ResourcePool("fury", "Fury", 100, 0, 15, 10), 20, 20)
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

            // Set after Begin, so the opening turn start (a Juggernaut's first
            // +15) is not in the figures below.
            bjorn.FuryEngine.Kind = engine;
            return new Rig { Session = session, Bjorn = bjorn, Foe = foe };
        }

        private static int Fury(Rig r) => r.Bjorn.PrimaryPool.Current;

        // ---- the formulas --------------------------------------------------------------

        [Test]
        public void Sentinel_IsRawOverMaxHpTimes150_ClampedThreeToTwentyFive()
        {
            Assert.AreEqual(16, FuryEngine.SentinelFury(28, 260), "4200 / 260 floors to 16");
            Assert.AreEqual(15, FuryEngine.SentinelFury(26, 260));
            Assert.AreEqual(25, FuryEngine.SentinelFury(52, 260), "30 clamps to 25");
            Assert.AreEqual(3, FuryEngine.SentinelFury(2, 260), "1 clamps up to 3");
            Assert.AreEqual(0, FuryEngine.SentinelFury(0, 260), "no hit pays nothing");
        }

        [Test]
        public void Einherjar_IsDamageOverAttackTimesTen_ClampedFiveToThirty()
        {
            Assert.AreEqual(10, FuryEngine.EinherjarFury(20, 20), "a basic hit of Attack pays 10");
            Assert.AreEqual(20, FuryEngine.EinherjarFury(40, 20), "a 2x hit pays 20");
            Assert.AreEqual(16, FuryEngine.EinherjarFury(33, 20), "16.5 floors");
            Assert.AreEqual(5, FuryEngine.EinherjarFury(4, 20), "2 clamps up to 5");
            Assert.AreEqual(30, FuryEngine.EinherjarFury(100, 20), "50 clamps to 30");
            Assert.AreEqual(0, FuryEngine.EinherjarFury(0, 20));
            Assert.AreEqual(30, FuryEngine.EinherjarFury(15, 0), "Attack 0 reads as 1");
        }

        [Test]
        public void Juggernaut_PaysTheCurvedTable()
        {
            Assert.AreEqual(15, FuryEngine.JuggernautFury(260, 260), "100%");
            Assert.AreEqual(20, FuryEngine.JuggernautFury(195, 260), "75%");
            Assert.AreEqual(35, FuryEngine.JuggernautFury(130, 260), "50%");
            Assert.AreEqual(60, FuryEngine.JuggernautFury(65, 260), "25%");
            Assert.AreEqual(60, FuryEngine.JuggernautFury(10, 260), "under 25% stays 60");
            Assert.AreEqual(27, FuryEngine.JuggernautFury(60, 100), "60%: 15 + 12.8 floors");
            Assert.AreEqual(15, FuryEngine.JuggernautFury(90, 100), "90%: 15 + 0.8 floors");
            Assert.AreEqual(15, FuryEngine.JuggernautFury(300, 260), "overhealed reads as full");
        }

        // ---- engine None: unchanged ------------------------------------------------------

        [Test]
        public void NoEngine_TheFlatGainsStillPay()
        {
            var r = Fight(FuryEngineKind.None);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);
            Assert.AreEqual(10, Fury(r), "gainOnDamageTaken");

            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            Assert.AreEqual(25, Fury(r), "gainOnAttack, once per action");
        }

        [Test]
        public void NoEngine_IdleDecayIsThePools()
        {
            var r = Fight(FuryEngineKind.None);
            r.Bjorn.PrimaryPool.DecayPerIdleTurn = 10;
            r.Bjorn.PrimaryPool.Gain(50);

            r.Session.TickPrimaryPoolForTest(r.Bjorn);

            Assert.AreEqual(40, Fury(r));
        }

        // ---- Sentinel ----------------------------------------------------------------------

        [Test]
        public void Sentinel_PaysOnTheRawFigure_NotWhatLanded()
        {
            var r = Fight(FuryEngineKind.Sentinel);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);

            Assert.AreEqual(240, r.Bjorn.CurrentHealth, "20 landed");
            Assert.AreEqual(16, Fury(r), "raw 28 pays 16 (the 20 that landed would pay 11); no flat 10");
        }

        [Test]
        public void Sentinel_EarnsNothingForSwinging()
        {
            var r = Fight(FuryEngineKind.Sentinel);

            r.Session.DealForTest(r.Bjorn, r.Foe, 40);

            Assert.AreEqual(960, r.Foe.CurrentHealth);
            Assert.AreEqual(0, Fury(r));
        }

        [Test]
        public void Sentinel_AnOrdinaryWardThatEatsTheWholeHit_PaysNothing()
        {
            var r = Fight(FuryEngineKind.Sentinel);
            StatusEffects.ApplyWard(r.Bjorn.Statuses, 100, 2, r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);

            Assert.AreEqual(260, r.Bjorn.CurrentHealth);
            Assert.AreEqual(0, Fury(r), "the pools never hear a fully warded hit (plan 4a)");

            // And the figure it held is not read by the next, unrelated blow.
            r.Session.DealForTest(r.Foe, r.Bjorn, 2);
            Assert.AreEqual(3, Fury(r), "the next blow pays on its own 2 (clamped to 3), not on the held 28");
        }

        [Test]
        public void Sentinel_ThePlantedShieldEatingTheWholeHit_PaysOnceOnTheRawFigure()
        {
            var r = Fight(FuryEngineKind.Sentinel);
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);

            Assert.AreEqual(260, r.Bjorn.CurrentHealth);
            Assert.AreEqual(16, Fury(r));
        }

        [Test]
        public void Sentinel_AHitThatBreaksThroughThePlantedShield_PaysOnce()
        {
            var r = Fight(FuryEngineKind.Sentinel);
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 280, DamageType.Physical); // 200: 130 shield, 70 health

            Assert.AreEqual(190, r.Bjorn.CurrentHealth);
            Assert.AreEqual(25, Fury(r), "one clamped payment, not one for the shield and one for health");
        }

        // ---- Einherjar ------------------------------------------------------------------

        [Test]
        public void Einherjar_PaysOncePerAction_ForItsLargestHit()
        {
            var r = Fight(FuryEngineKind.Einherjar);

            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            Assert.AreEqual(20, Fury(r), "paid on the beat of the hit");

            r.Session.DealForTest(r.Bjorn, r.Foe, 60);
            Assert.AreEqual(30, Fury(r), "a larger hit in the same action tops up to its own worth");

            r.Session.DealForTest(r.Bjorn, r.Foe, 20);
            Assert.AreEqual(30, Fury(r), "a smaller one adds nothing");
        }

        [Test]
        public void Einherjar_AreaHits_CountOnlyTheLargest_WhateverTheOrder()
        {
            var r = Fight(FuryEngineKind.Einherjar);

            r.Session.DealForTest(r.Bjorn, r.Foe, 30);
            r.Session.DealForTest(r.Bjorn, r.Foe, 50);
            r.Session.DealForTest(r.Bjorn, r.Foe, 10);

            Assert.AreEqual(25, Fury(r), "50 x 10 / 20; the 30 and the 10 pay nothing extra");
        }

        [Test]
        public void Einherjar_ANewActionPaysAgain()
        {
            var r = Fight(FuryEngineKind.Einherjar);

            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            r.Session.StartEngineActionForTest(r.Bjorn);
            r.Session.DealForTest(r.Bjorn, r.Foe, 40);

            Assert.AreEqual(40, Fury(r));
        }

        [Test]
        public void Einherjar_EarnsNothingFromBeingHit()
        {
            var r = Fight(FuryEngineKind.Einherjar);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);

            Assert.AreEqual(160, r.Bjorn.CurrentHealth);
            Assert.AreEqual(0, Fury(r));
        }

        [Test]
        public void Hack_EachHitPaysSeparately_AndTheFlagEndsWithTheAction()
        {
            var r = Fight(FuryEngineKind.Einherjar);

            r.Session.BeginPerHitEngineAction(r.Bjorn);
            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            r.Session.DealForTest(r.Bjorn, r.Foe, 30);
            Assert.AreEqual(35, Fury(r), "20 + 15: two hits, two payments");

            r.Session.SettleEngineActionForTest(r.Bjorn);
            Assert.IsFalse(r.Bjorn.FuryEngine.PaysPerHit);

            r.Session.StartEngineActionForTest(r.Bjorn);
            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            Assert.AreEqual(55, Fury(r), "the next action is once-per-action again");
        }

        // ---- Juggernaut ------------------------------------------------------------------

        [Test]
        public void Juggernaut_PaysAtTurnStartByHealth()
        {
            var r = Fight(FuryEngineKind.Juggernaut);

            r.Session.TickPrimaryPoolForTest(r.Bjorn);
            Assert.AreEqual(15, Fury(r), "full health");

            r.Bjorn.CurrentHealth = 130;
            r.Session.TickPrimaryPoolForTest(r.Bjorn);
            Assert.AreEqual(50, Fury(r), "+35 at half");

            r.Bjorn.CurrentHealth = 65;
            r.Session.TickPrimaryPoolForTest(r.Bjorn);
            Assert.AreEqual(100, Fury(r), "+60 at a quarter, capped at the pool's 100");
        }

        [Test]
        public void Juggernaut_PaysOncePerOwnTurn_NotAgainOnAnExtraActionsReopen()
        {
            var r = Fight(FuryEngineKind.Juggernaut);
            r.Bjorn.CurrentHealth = 130;

            r.Session.TickPrimaryPoolForTest(r.Bjorn);
            Assert.AreEqual(35, Fury(r), "his turn opens at half health");

            r.Session.ReopenTurnForTest(r.Bjorn); // Trample's extra action
            Assert.AreEqual(35, Fury(r), "the same turn: nothing more");

            r.Session.TickPrimaryPoolForTest(r.Bjorn);
            Assert.AreEqual(70, Fury(r), "his next turn pays again");
        }

        [Test]
        public void Juggernaut_HasNoIdleDecay()
        {
            var r = Fight(FuryEngineKind.Juggernaut);
            r.Bjorn.PrimaryPool.DecayPerIdleTurn = 10;
            r.Bjorn.PrimaryPool.Gain(50);

            r.Session.TickPrimaryPoolForTest(r.Bjorn);

            Assert.AreEqual(65, Fury(r), "an idle turn: +15, and the pool's 10 decay is off");
        }

        [Test]
        public void Juggernaut_HitsPayNothing_ButDirectGrantsStillDo()
        {
            var r = Fight(FuryEngineKind.Juggernaut);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 28, DamageType.Physical);
            r.Session.DealForTest(r.Bjorn, r.Foe, 40);
            Assert.AreEqual(0, Fury(r), "no hit-based income, dealt or taken");

            r.Bjorn.PrimaryPool.Gain(20); // a talent rider from another tree (Unyielding T3, Bellow T3)
            Assert.AreEqual(20, Fury(r));
        }

        // ---- per-holder cooldowns (Second Wind under the Juggernaut root) ------------------

        [Test]
        public void ACooldownOverride_ReplacesTheRowsCooldown_ForThatHolderOnly()
        {
            var skill = new ResolvedSkill { Id = "second_wind", CooldownTurns = 0 };
            var holder = new CombatantState("Bjorn", true, 100, 0, 1, 1);
            var other = new CombatantState("Other", true, 100, 0, 1, 1);

            holder.CooldownOverrides["second_wind"] = 4;

            Assert.AreEqual(4, FightSession.CooldownTurnsFor(holder, skill));
            Assert.AreEqual(0, FightSession.CooldownTurnsFor(other, skill));
        }
    }
}
