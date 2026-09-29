using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // Plan 4e: ONE definition of crowd control, ONE application-time block
    // (FightSession.RecordStatus). Unstoppable ignores CC; Unyielding negates
    // one attempt, surges (+speed, +damage) and starts a cooldown -- but not
    // while Unstoppable already blocked the attempt.
    public class CrowdControlTests
    {
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight()
        {
            var hero = new CombatantState("Bjorn", true, 200, 0, 20, 20);
            var foe = new CombatantState("Foe", false, 1000, 0, 1, 1);
            var kit = new PlayerKit("bjorn", CharacterRole.Tank, null, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return (session, hero, foe);
        }

        private static readonly StatusEffectType[] Control =
        {
            StatusEffectType.Stun, StatusEffectType.Feared, StatusEffectType.Rooted, StatusEffectType.Chilled,
        };

        private static bool Has(CombatantState c, StatusEffectType type) => c.Statuses.Any(s => s.Type == type);

        private static void Attempt(FightSession session, CombatantState target, StatusEffectType type) =>
            session.ApplyStatusToForTest(target, type, type == StatusEffectType.Chilled ? 20 : 0, 2);

        // ---- the predicate -------------------------------------------------------

        [Test]
        public void CrowdControlIsExactlyStunFearedRootedChilled()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                Assert.AreEqual(Control.Contains(type), CrowdControl.IsCrowdControl(type), type.ToString());
            }
        }

        // ---- the window's clock ----------------------------------------------------

        [Test]
        public void TurnWindow_OpenedOffTurn_CoversExactlyNTurnEnds()
        {
            var w = new TurnWindow();
            w.Open(2, openedOnHoldersTurn: false);
            Assert.IsFalse(w.AgeAtHoldersTurnEnd());
            Assert.IsTrue(w.IsOpen);
            Assert.IsTrue(w.AgeAtHoldersTurnEnd(), "true exactly when it closes");
            Assert.IsFalse(w.IsOpen);
            Assert.IsFalse(w.AgeAtHoldersTurnEnd(), "a closed window says nothing");
        }

        [Test]
        public void TurnWindow_OpenedOnTheHoldersTurn_SparesThatTurnsEnd()
        {
            var w = new TurnWindow();
            w.Open(2, openedOnHoldersTurn: true);
            w.AgeAtHoldersTurnEnd();
            Assert.AreEqual(2, w.TurnsRemaining);
            w.AgeAtHoldersTurnEnd();
            Assert.AreEqual(1, w.TurnsRemaining);
            Assert.IsTrue(w.AgeAtHoldersTurnEnd());
        }

        [Test]
        public void TurnWindow_ReopeningNeverShortens()
        {
            var w = new TurnWindow();
            w.Open(3, false);
            w.Open(1, false);
            Assert.AreEqual(3, w.TurnsRemaining);
            w.Open(5, false);
            Assert.AreEqual(5, w.TurnsRemaining);
        }

        // ---- everyone else: unchanged ----------------------------------------------

        [Test]
        public void WithoutAGuard_EveryControlStatusLandsAsBefore()
        {
            foreach (var type in Control)
            {
                var (session, hero, _) = Fight();
                Attempt(session, hero, type);
                Assert.IsTrue(Has(hero, type), type.ToString());
            }
        }

        [Test]
        public void WithoutAGuard_ChilledStillSlows()
        {
            var (session, hero, _) = Fight();
            Attempt(session, hero, StatusEffectType.Chilled);
            Assert.AreEqual(16, hero.Speed, "20% off 20");
        }

        // ---- Unstoppable -------------------------------------------------------------

        [Test]
        public void Unstoppable_BlocksEveryControlStatus_AndNothingElse()
        {
            var (session, hero, _) = Fight();
            session.OpenUnstoppable(hero, 2);

            foreach (var type in Control) Attempt(session, hero, type);
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 5, 2);

            foreach (var type in Control) Assert.IsFalse(Has(hero, type), type.ToString());
            Assert.IsTrue(Has(hero, StatusEffectType.Poison), "a DoT is not control");
            Assert.AreEqual(20, hero.Speed, "a blocked chill slows nobody");
        }

        [Test]
        public void Unstoppable_EndsWithItsWindow()
        {
            var (session, hero, _) = Fight();
            session.OpenUnstoppable(hero, 1);
            session.TickStatusesAtTurnEndForTest(hero); // spared: opened on his turn
            session.TickStatusesAtTurnEndForTest(hero);

            Attempt(session, hero, StatusEffectType.Stun);

            Assert.IsTrue(Has(hero, StatusEffectType.Stun));
        }

        // ---- Unyielding --------------------------------------------------------------

        [Test]
        public void Unyielding_NegatesOneAttempt_AndSurges()
        {
            var (session, hero, _) = Fight();
            hero.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 4);

            Attempt(session, hero, StatusEffectType.Stun);

            Assert.IsFalse(Has(hero, StatusEffectType.Stun));
            Assert.AreEqual(24, hero.Speed, "+20% of 20");
            Assert.AreEqual(25, session.AttackBonusForTest(hero), "+25% damage");
            Assert.IsTrue(session.DrainImmediateMessages().Concat(session.DrainBeats().SelectMany(b => b.Messages))
                .Contains("Bjorn refuses the Stun - and surges!"));
        }

        [Test]
        public void Unyielding_FiresOnce_ThenTheCooldownLetsControlThrough()
        {
            var (session, hero, _) = Fight();
            hero.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 4);

            Attempt(session, hero, StatusEffectType.Stun);
            Attempt(session, hero, StatusEffectType.Rooted);

            Assert.IsFalse(Has(hero, StatusEffectType.Stun));
            Assert.IsTrue(Has(hero, StatusEffectType.Rooted), "on cooldown: the second attempt lands");
            Assert.AreEqual(24, hero.Speed, "and it did not surge twice");
        }

        [Test]
        public void Unyielding_CooldownRunsOut_AndItFiresAgain()
        {
            var (session, hero, _) = Fight();
            hero.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 3);
            Attempt(session, hero, StatusEffectType.Stun);

            // Opened on his own turn: that turn's end is spared, then 3 more.
            for (int i = 0; i < 3; i++) session.TickStatusesAtTurnEndForTest(hero);
            Assert.IsTrue(hero.CrowdControl.UnyieldingCooldown.IsOpen);
            session.TickStatusesAtTurnEndForTest(hero);
            Assert.IsFalse(hero.CrowdControl.UnyieldingCooldown.IsOpen);

            Attempt(session, hero, StatusEffectType.Feared);
            Assert.IsFalse(Has(hero, StatusEffectType.Feared));
        }

        [Test]
        public void TheSurgeEnds_SpeedAndDamageComeBack()
        {
            var (session, hero, _) = Fight();
            hero.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 4);
            Attempt(session, hero, StatusEffectType.Stun);

            for (int i = 0; i < 3; i++) session.TickStatusesAtTurnEndForTest(hero); // spared + 2

            Assert.AreEqual(20, hero.Speed);
            Assert.AreEqual(0, session.AttackBonusForTest(hero));
        }

        [Test]
        public void Unyielding_T3_GrantsFury()
        {
            var (session, hero, _) = Fight();
            hero.PrimaryPool = new ResourcePool("fury", "Fury", 100, 0, 0, 0);
            hero.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 3, furyGain: 20);

            Attempt(session, hero, StatusEffectType.Stun);

            Assert.AreEqual(20, hero.PrimaryPool.Current);
        }

        // Owner decision (plan section 4).
        [Test]
        public void UnderUnstoppable_UnyieldingNeitherFiresNorStartsItsCooldown()
        {
            var (session, hero, _) = Fight();
            hero.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 4);
            session.OpenUnstoppable(hero, 1);

            Attempt(session, hero, StatusEffectType.Stun);

            Assert.IsFalse(Has(hero, StatusEffectType.Stun), "blocked by Unstoppable");
            Assert.IsFalse(hero.CrowdControl.UnyieldingCooldown.IsOpen, "no cooldown started");
            Assert.IsFalse(hero.CrowdControl.UnyieldingSurge.IsOpen, "no surge");
            Assert.AreEqual(20, hero.Speed);

            // Unstoppable ends; Unyielding is still loaded.
            session.TickStatusesAtTurnEndForTest(hero);
            session.TickStatusesAtTurnEndForTest(hero);
            Attempt(session, hero, StatusEffectType.Stun);
            Assert.IsFalse(Has(hero, StatusEffectType.Stun));
            Assert.IsTrue(hero.CrowdControl.UnyieldingSurge.IsOpen);
        }

        [Test]
        public void Unstoppable_SaysWhyTheControlDidNotTakeHold()
        {
            var (session, hero, _) = Fight();
            session.OpenUnstoppable(hero, 2);
            session.DrainImmediateMessages();

            Attempt(session, hero, StatusEffectType.Stun);

            var lines = session.DrainImmediateMessages().Concat(session.DrainBeats().SelectMany(b => b.Messages)).ToList();
            Assert.IsTrue(lines.Contains("Bjorn is unstoppable - the Stun does not take hold."));
        }
    }
}
