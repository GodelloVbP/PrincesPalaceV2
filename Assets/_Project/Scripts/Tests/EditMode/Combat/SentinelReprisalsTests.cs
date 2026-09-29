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
    // Plan 4f: what the Sentinel's shield does to whoever struck it. Silence
    // (Spellbreaker T2), reflect Fury (Spellbreaker T3), slow (Thornwall T2)
    // and disarm (Thornwall T3), all off by default and all reached through
    // the same post-action seam as the 4a thorns and reflect (PlantedShield
    // Tests). Every figure is a literal.
    //
    // FIXTURE ARITHMETIC, as PlantedShieldTests: Bjorn has 40 physical and 12
    // magical defence and 260 health, so raw 140 physical and raw 112 fire
    // both land as 100. His planted shield is 130.
    public class SentinelReprisalsTests
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
            var foe = new CombatantState("Foe", false, 1000, 0, 10, 10) { CritChancePercent = 0 };
            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("bjorn", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();
            session.DrainImmediateMessages();
            return new Rig { Session = session, Bjorn = bjorn, Foe = foe };
        }

        private static bool IsChilled(CombatantState c) =>
            c.Statuses.Any(s => s.Type == StatusEffectType.Chilled);

        // ---- Suppression, the state -------------------------------------------------

        [Test]
        public void EveryoneStartsUnsuppressed()
        {
            var c = new CombatantState("A", false, 10, 0, 1, 1);
            Assert.IsFalse(c.Suppression.IsSilenced);
            Assert.IsFalse(c.Suppression.IsDisarmed);
            Assert.AreEqual(0, c.Suppression.AttackPercentDelta);
            Assert.IsTrue(CombatActions.IsLegalFor(c, physicalMove: false, out _));
            Assert.IsTrue(CombatActions.IsLegalFor(c, physicalMove: true, out _));
        }

        [Test]
        public void Silence_LastsItsTurns_AndTheCooldownRunsThreeOfTheHoldersTurns()
        {
            var s = new Suppression();

            Assert.IsTrue(s.TrySilence(1, 3, onHoldersTurn: false));
            Assert.IsTrue(s.IsSilenced);
            Assert.IsFalse(s.TrySilence(1, 3, onHoldersTurn: false), "the cooldown refuses a second one");

            s.AgeAtHoldersTurnEnd();
            Assert.IsFalse(s.IsSilenced, "one turn, one age");
            Assert.IsFalse(s.TrySilence(1, 3, false), "cooldown 2 turns left");

            s.AgeAtHoldersTurnEnd();
            Assert.IsFalse(s.TrySilence(1, 3, false), "cooldown 1 turn left");

            s.AgeAtHoldersTurnEnd();
            Assert.IsTrue(s.TrySilence(1, 3, false), "three turns after it landed");
        }

        [Test]
        public void Silence_OpenedOnTheHoldersOwnTurn_SkipsThatTurnsEnd()
        {
            var s = new Suppression();
            s.TrySilence(1, 3, onHoldersTurn: true);

            s.AgeAtHoldersTurnEnd();
            Assert.IsTrue(s.IsSilenced, "the turn it was cast on does not count");

            s.AgeAtHoldersTurnEnd();
            Assert.IsFalse(s.IsSilenced);
        }

        [Test]
        public void Disarm_TakesItsPercentForItsTurns_ThenLetsGo()
        {
            var s = new Suppression();
            s.ApplyDisarm(30, 2, onHoldersTurn: false);
            Assert.AreEqual(-30, s.AttackPercentDelta);

            s.AgeAtHoldersTurnEnd();
            Assert.AreEqual(-30, s.AttackPercentDelta);

            var (silenceEnded, disarmEnded) = s.AgeAtHoldersTurnEnd();
            Assert.IsFalse(silenceEnded);
            Assert.IsTrue(disarmEnded);
            Assert.AreEqual(0, s.AttackPercentDelta);
        }

        [Test]
        public void Disarm_ReapplyingNeverWeakensTheStrongerFigure()
        {
            var s = new Suppression();
            s.ApplyDisarm(30, 2, false);
            s.ApplyDisarm(10, 1, false);

            Assert.AreEqual(-30, s.AttackPercentDelta);
            s.AgeAtHoldersTurnEnd();
            Assert.IsTrue(s.IsDisarmed, "the longer span stands");
        }

        // ---- the seams that read it -------------------------------------------------

        [Test]
        public void ASilencedActor_MayStrikeButNotCast()
        {
            var c = new CombatantState("Caster", false, 10, 0, 1, 1);
            c.Suppression.TrySilence(1, 3, false);

            Assert.IsTrue(CombatActions.IsLegalFor(c, physicalMove: true, out _));
            Assert.IsFalse(CombatActions.IsLegalFor(c, physicalMove: false, out string refusal));
            Assert.AreEqual("Caster is silenced and cannot cast - only strike.", refusal);
        }

        [Test]
        public void ADisarmedActor_HasItsAttackBonusCutByTheDisarm()
        {
            var r = Fight();
            Assert.AreEqual(0, r.Session.AttackBonusForTest(r.Foe));

            r.Foe.Suppression.ApplyDisarm(30, 2, false);
            Assert.AreEqual(-30, r.Session.AttackBonusForTest(r.Foe));
        }

        [Test]
        public void ASilencedEnemy_DrawsNoSpell_AndSwingsInstead()
        {
            var spell = new ResolvedSkill("thorn_lash", "Thorn Lash", "", "monster", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0);
            var pool = new List<EnemyAbility>
            {
                EnemyAbility.Of(spell, 1_000_000f),
                EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f, 0.01f),
            };
            var source = new ResolvedEnemy("monster", "monster", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);

            List<string> Lines(bool silencedBeforeDraw, bool silencedAfterDraw = false)
            {
                var hero = new CombatantState("Hero", true, 500, 20, 20, 10);
                var monster = new CombatantState("Monster", false, 1000, 10, 15, 9);
                var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { monster }),
                    new List<PlayerKit> { null }, new List<EnemyKit> { new EnemyKit(source, false, pool) },
                    new SeededRandom(1)) { DamageVarianceRange = 0f };
                if (silencedBeforeDraw) monster.Suppression.TrySilence(2, 3, false);
                session.Begin();
                if (silencedAfterDraw) monster.Suppression.TrySilence(2, 3, false);
                session.ExecuteAttack(monster);
                return session.DrainBeats()
                    .Where(b => b.IsAction && !b.Actor.IsPlayerSide)
                    .SelectMany(b => b.Messages).ToList();
            }

            Assert.IsTrue(Lines(silencedBeforeDraw: false).Any(m => m.Contains("Thorn Lash")),
                "fixture check: without silence the spell wins the draw");

            var drawn = Lines(silencedBeforeDraw: true);
            Assert.IsFalse(drawn.Any(m => m.Contains("Thorn Lash")));
            Assert.IsTrue(drawn.Any(m => m.Contains("attacks") && m.Contains("damage")));

            // Silenced AFTER the spell was telegraphed: the commitment is
            // voided at resolution, as a rooted charge is.
            var voided = Lines(silencedBeforeDraw: false, silencedAfterDraw: true);
            Assert.IsFalse(voided.Any(m => m.Contains("Thorn Lash")));
            Assert.IsTrue(voided.Any(m => m.Contains("silenced")));
            Assert.IsFalse(voided.Any(m => m.Contains("damage")));
        }

        // ---- Spellbreaker T2: silence ---------------------------------------------

        [Test]
        public void Spellbreaker_SilencesTheCaster_OnceThenTheCooldownHolds()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SilenceCasterOnSpellHit = true;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            Assert.IsTrue(r.Foe.Suppression.IsSilenced);

            r.Foe.Suppression.Silence.Close();
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            Assert.IsFalse(r.Foe.Suppression.IsSilenced, "inside the 3-turn cooldown");
        }

        [Test]
        public void Spellbreaker_DoesNotSilenceAPhysicalAttacker()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SilenceCasterOnSpellHit = true;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.IsFalse(r.Foe.Suppression.IsSilenced);
        }

        // ---- Spellbreaker T3: reflect Fury ---------------------------------------

        [Test]
        public void ReflectFury_IsHalfTheReflection_ThroughTheThreeToTwentyFiveClamp()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ReflectMagicPercent = 60;
            r.Bjorn.PlantedShield.ReflectGrantsFury = true;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire); // 100 lands, 60 reflected
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.AreEqual(940, r.Foe.CurrentHealth);
            Assert.AreEqual(25, r.Bjorn.PrimaryPool.Current, "half of 60 is 30, clamped to 25");
        }

        [Test]
        public void ReflectFury_SmallReflectionsRoundUpToTheClampFloor()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ReflectMagicPercent = 15;
            r.Bjorn.PlantedShield.ReflectGrantsFury = true;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire); // 15 reflected, half is 7
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.AreEqual(7, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void ReflectFury_OffWithoutTheFlag()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.ReflectMagicPercent = 15;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.AreEqual(0, r.Bjorn.PrimaryPool.Current);
        }

        [Test]
        public void TheFuryClamp_FloorsAtThreeAndKeepsZeroAtZero()
        {
            Assert.AreEqual(0, PlantedShield.ClampFuryPerHit(0));
            Assert.AreEqual(3, PlantedShield.ClampFuryPerHit(1));
            Assert.AreEqual(12, PlantedShield.ClampFuryPerHit(12));
            Assert.AreEqual(25, PlantedShield.ClampFuryPerHit(99));
        }

        // ---- Thornwall T2: slow ----------------------------------------------------

        [Test]
        public void Thornwall_SlowsAPhysicalMoveAttacker_WithChilledForOneTurn()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SlowsAttacker = true;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            var chilled = r.Foe.Statuses.Single(s => s.Type == StatusEffectType.Chilled);
            Assert.AreEqual(20, chilled.Magnitude);
            Assert.AreEqual(20, FightTuning.ChilledOnHitSpeedPercent, "the default is the on-hit chill's figure");
        }

        [Test]
        public void Thornwall_DoesNotSlowAMagicHit_OrAPhysicalHitThatWasNotAMove()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SlowsAttacker = true;

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 112, DamageType.Fire);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);
            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.IsFalse(IsChilled(r.Foe));
        }

        [Test]
        public void Thornwall_SlowRunsThroughTheCrowdControlGuard()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SlowsAttacker = true;
            r.Foe.CrowdControl.Unstoppable.Open(2, false);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.IsFalse(IsChilled(r.Foe), "Unstoppable refuses the chill at the application seam");
        }

        [Test]
        public void Thornwall_SlowNeedsNoThorns()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.SlowsAttacker = true;
            Assert.AreEqual(0, r.Bjorn.PlantedShield.ThornsPercent);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.AreEqual(1000, r.Foe.CurrentHealth);
            Assert.IsTrue(IsChilled(r.Foe));
        }

        // ---- Thornwall T3: disarm ----------------------------------------------------

        [Test]
        public void Thornwall_DisarmsTheAttackerWhoseHitBrokeTheShield()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.DisarmsOnBreak = true;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 20000, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.IsTrue(r.Foe.Suppression.IsDisarmed);
            Assert.AreEqual(-30, r.Session.AttackBonusForTest(r.Foe));
        }

        [Test]
        public void Thornwall_ANonBreakingHit_DisarmsNobody()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.DisarmsOnBreak = true;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 140, DamageType.Physical); // 100 of 130
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.IsFalse(r.Foe.Suppression.IsDisarmed);
        }

        [Test]
        public void Thornwall_AMagicHitThatBreaksTheShield_DisarmsNobody()
        {
            var r = Fight();
            r.Bjorn.PlantedShield.DisarmsOnBreak = true;
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 20000, DamageType.Fire);
            r.Session.SettleShieldReactionsForTest(physicalMove: false);

            Assert.IsFalse(r.Foe.Suppression.IsDisarmed);
        }

        [Test]
        public void Thornwall_DisarmIsOffByDefault()
        {
            var r = Fight();
            r.Session.PlantShield(r.Bjorn);

            r.Session.StrikeForTest(r.Foe, r.Bjorn, 20000, DamageType.Physical);
            r.Session.SettleShieldReactionsForTest(physicalMove: true);

            Assert.IsFalse(r.Foe.Suppression.IsDisarmed);
            Assert.IsFalse(IsChilled(r.Foe));
        }

        [Test]
        public void EveryFourFHook_IsOffByDefault()
        {
            var shield = new PlantedShield();
            Assert.IsFalse(shield.SilenceCasterOnSpellHit || shield.ReflectGrantsFury
                || shield.SlowsAttacker || shield.DisarmsOnBreak);
            Assert.AreEqual(1, shield.SilenceTurns);
            Assert.AreEqual(3, shield.SilenceCooldownTurns);
            Assert.AreEqual(50, shield.ReflectFuryPercent);
            Assert.AreEqual(1, shield.SlowTurns);
            Assert.AreEqual(30, shield.DisarmPercent);
            Assert.AreEqual(2, shield.DisarmTurns);
        }
    }
}
