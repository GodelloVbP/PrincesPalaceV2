using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Bjorn's Slam, step 1 of the Fury-sink plan: PoolTierResolution's own
    // pure arithmetic -- no FightSession, no cast, just a pool and the same
    // two-tier ladder skills.json authors on placeholder_brawler_slam. See
    // PoolTierSlamCastTests below for the full cast these compose into.
    public class PoolTierResolutionTests
    {
        private static ResourcePool Fury(int current)
        {
            var pool = new ResourcePool("fury", "Fury", max: 100, gainPerTurn: 0, gainOnAttack: 15, gainOnDamageTaken: 10);
            pool.Gain(current);
            return pool;
        }

        private static readonly ResolvedPoolTier[] SlamTiers =
        {
            new ResolvedPoolTier(0.5f, 2f, shake: 0.6f),
            new ResolvedPoolTier(1.0f, 4f, shake: 1f, hitStopSeconds: 0.18f),
        };

        [Test]
        public void Pick_BelowTheFirstThreshold_FiresNothing()
        {
            Assert.IsFalse(PoolTierResolution.Pick(Fury(0), SlamTiers).Fired);
            Assert.IsFalse(PoolTierResolution.Pick(Fury(49), SlamTiers).Fired);
        }

        [Test]
        public void Pick_AtHalfCapacity_FiresTheFirstTier_AndSpendsHalf()
        {
            var result = PoolTierResolution.Pick(Fury(50), SlamTiers);

            Assert.IsTrue(result.Fired);
            Assert.AreEqual(2f, result.Tier.DamageMultiplier);
            Assert.AreEqual(50, result.SpendAmount);
        }

        [Test]
        public void Pick_AboveHalfButBelowFull_StillFiresOnlyTheFirstTier()
        {
            var result = PoolTierResolution.Pick(Fury(75), SlamTiers);

            Assert.IsTrue(result.Fired);
            Assert.AreEqual(2f, result.Tier.DamageMultiplier);
            Assert.AreEqual(50, result.SpendAmount, "the first tier's own fraction, not everything on hand");
        }

        [Test]
        public void Pick_AtFullCapacity_FiresTheHighestTier_AndSpendsAllOfIt()
        {
            var result = PoolTierResolution.Pick(Fury(100), SlamTiers);

            Assert.IsTrue(result.Fired);
            Assert.AreEqual(4f, result.Tier.DamageMultiplier);
            Assert.AreEqual(100, result.SpendAmount);
        }

        [Test]
        public void Pick_WithNoTiersAuthored_NeverFires()
        {
            Assert.IsFalse(PoolTierResolution.Pick(Fury(100), System.Array.Empty<ResolvedPoolTier>()).Fired);
        }

        [Test]
        public void Pick_WithNoPool_NeverFires()
        {
            Assert.IsFalse(PoolTierResolution.Pick(null, SlamTiers).Fired);
        }

        [Test]
        public void Pick_IsPure_NothingIsActuallySpent()
        {
            var pool = Fury(50);
            PoolTierResolution.Pick(pool, SlamTiers);

            Assert.AreEqual(50, pool.Current, "Pick only decides; CastSkill is what actually spends");
        }

        [Test]
        public void ApplyDamageMultiplier_NoTierFired_LeavesTheBaseUnchanged()
        {
            var none = PoolTierResolution.Pick(Fury(0), SlamTiers);
            Assert.AreEqual(22, PoolTierResolution.ApplyDamageMultiplier(22, none));
        }

        [Test]
        public void ApplyDamageMultiplier_DoublesOrQuadruplesTheBase()
        {
            var x2 = PoolTierResolution.Pick(Fury(50), SlamTiers);
            var x4 = PoolTierResolution.Pick(Fury(100), SlamTiers);

            Assert.AreEqual(44, PoolTierResolution.ApplyDamageMultiplier(22, x2));
            Assert.AreEqual(88, PoolTierResolution.ApplyDamageMultiplier(22, x4));
        }

        [Test]
        public void Label_NamesTheFiredTier_OrJustTheSkill()
        {
            Assert.AreEqual("Slam", PoolTierResolution.Label("Slam", PoolTierResolution.Pick(Fury(0), SlamTiers)));
            Assert.AreEqual("Slam x2", PoolTierResolution.Label("Slam", PoolTierResolution.Pick(Fury(50), SlamTiers)));
            Assert.AreEqual("Slam x4", PoolTierResolution.Label("Slam", PoolTierResolution.Pick(Fury(100), SlamTiers)));
        }

        // INVARIANT CULTURE, PROVEN RATHER THAN ASSUMED. nl-NL formats a
        // fraction with a comma ("1,5"), which is exactly what ":0.#" without
        // an explicit culture would have printed here -- "Slam x1,5" instead
        // of "Slam x1.5". Restored in finally so a failure mid-test cannot
        // leave a later test running under nl-NL.
        [Test]
        public void Label_FormatsTheMultiplierWithInvariantCulture_RegardlessOfCurrentCulture()
        {
            var halfAgainTiers = new[] { new ResolvedPoolTier(1.0f, 1.5f, shake: 0.6f) };

            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");

                var result = PoolTierResolution.Pick(Fury(100), halfAgainTiers);
                Assert.AreEqual("Slam x1.5", PoolTierResolution.Label("Slam", result));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }

    // THE FULL CAST: spend, multiply, land, and the beat's own floors --
    // pinned end to end through a real FightSession because the pure tests
    // above only prove PoolTierResolution's own arithmetic, not that
    // CastSkill actually wires it the way the plan describes. Every damage
    // number is a LITERAL, never a formula recomputed here (CLAUDE.md gotcha
    // 5): BaseDamage is stated once, as the max(1, ScaledAttack + flatAmount)
    // SkillResolution.Damage would produce for Attack 10 / flatAmount 17 with
    // no authored weapon scaling (flatAmount raised from 12 to 17, 2026-09-15,
    // alongside halving every spell's damage), and every assertion below is
    // that number or a whole multiple of it.
    public class PoolTierSlamCastTests
    {
        private const int BaseDamage = 27;

        private static ResolvedSkill Slam() =>
            new ResolvedSkill("slam", "Slam", "", "bear", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 17, false,
                null, SpellPresentation.None, 0,
                poolTiers: new[]
                {
                    new ResolvedPoolTier(0.5f, 2f, shake: 0.6f),
                    new ResolvedPoolTier(1.0f, 4f, shake: 1f, hitStopSeconds: 0.18f),
                });

        // NO EnemyKit FOR THE FOE, ON PURPOSE. AffinityOf(target) falls back
        // to ElementalAffinity.Neutral when nothing is registered
        // (FightSession.AffinityOf's own header), which is what keeps
        // BaseDamage a literal instead of something an authored weakness or
        // resistance would retune.
        private static (FightSession session, CombatantState bjorn, CombatantState foe) Fight(int startingFury)
        {
            var fury = new ResourcePool("fury", "Fury", max: 100, gainPerTurn: 0, gainOnAttack: 15, gainOnDamageTaken: 10);
            fury.Gain(startingFury);

            var bjorn = new CombatantState("Bjorn", true, 999999, fury, attack: 10, speed: 10);
            var foe = new CombatantState("Dummy", false, 999999, 0, 1, 1);

            var kit = new PlayerKit("bear", CharacterRole.Tank, new List<ResolvedSkill> { Slam() }, null, DamageType.Physical);

            var session = new FightSession(new CombatEncounter(new[] { bjorn }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3)) { DamageVarianceRange = 0f };
            session.Begin();

            return (session, bjorn, foe);
        }

        private static int DamageDealt(CombatantState foe) => foe.MaxHealth - foe.CurrentHealth;

        [Test]
        public void Fury0_CastsAtBaseDamage_AndSpendsNothing()
        {
            var (session, bjorn, foe) = Fight(startingFury: 0);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(BaseDamage, DamageDealt(foe));
            // gainOnAttack fires off ANY damaging action, tiered or not
            // (ResourcePool.GainOnAttack's own header) -- 0 spent, then +15
            // for landing the hit.
            Assert.AreEqual(15, bjorn.PrimaryPool.Current);

            var beat = session.DrainBeats()[0];
            Assert.AreEqual(0f, beat.PoolTierDamageMultiplier, "no tier fired");
        }

        [Test]
        public void Fury49_StillBelowTheFirstThreshold_CastsAtBaseDamage()
        {
            var (session, bjorn, foe) = Fight(startingFury: 49);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(BaseDamage, DamageDealt(foe));
            Assert.AreEqual(64, bjorn.PrimaryPool.Current, "49 untouched, then +15 for landing the hit");
        }

        [Test]
        public void Fury50_FiresTheFirstTier_DoublesDamage_AndSpendsHalf()
        {
            var (session, bjorn, foe) = Fight(startingFury: 50);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(BaseDamage * 2, DamageDealt(foe));
            Assert.AreEqual(15, bjorn.PrimaryPool.Current, "50 spent to 0, then +15 for landing the hit");

            var beat = session.DrainBeats()[0];
            Assert.AreEqual(2f, beat.PoolTierDamageMultiplier);
            Assert.GreaterOrEqual(beat.Shake, 0.6f);
            Assert.AreEqual(0f, beat.FormHitStopSeconds, "the x2 tier authors no hit-stop floor");
        }

        [Test]
        public void Fury75_StillOnlyTheFirstTier_LeavesTwentyFiveBehind()
        {
            var (session, bjorn, foe) = Fight(startingFury: 75);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(BaseDamage * 2, DamageDealt(foe));
            Assert.AreEqual(40, bjorn.PrimaryPool.Current, "75 - 50 spent = 25, then +15 for landing the hit");
        }

        [Test]
        public void Fury100_FiresTheHighestTier_QuadruplesDamage_AndSpendsAllOfIt()
        {
            var (session, bjorn, foe) = Fight(startingFury: 100);

            Assert.IsTrue(session.CastSkill(0, foe));

            Assert.AreEqual(BaseDamage * 4, DamageDealt(foe));
            // Spend 100, hit, gain 15 -- the intended feel (AUDIT plan step 1).
            Assert.AreEqual(15, bjorn.PrimaryPool.Current, "100 spent to 0, then +15 for landing the hit");

            var beat = session.DrainBeats()[0];
            Assert.AreEqual(4f, beat.PoolTierDamageMultiplier);
            Assert.AreEqual(1f, beat.Shake);
            Assert.AreEqual(0.18f, beat.FormHitStopSeconds);
        }

        [Test]
        public void TheFightLog_NamesTheFiredTier()
        {
            var (session, _, foe) = Fight(startingFury: 100);

            session.CastSkill(0, foe);

            var beat = session.DrainBeats()[0];
            StringAssert.Contains("Slam x4", string.Join(" ", beat.Messages));
        }

        [Test]
        public void TheFightLog_NamesNoTierUnderTheFirstThreshold()
        {
            var (session, _, foe) = Fight(startingFury: 0);

            session.CastSkill(0, foe);

            var beat = session.DrainBeats()[0];
            string joined = string.Join(" ", beat.Messages);
            StringAssert.Contains("Slam", joined);
            StringAssert.DoesNotContain("Slam x", joined);
        }

        [Test]
        public void TheRowCaption_PreviewsTheTierThatWouldFireRightNow()
        {
            var (session, bjorn, _) = Fight(startingFury: 0);
            Assert.AreEqual("Slam", FightHudModel.SkillRows(session, bjorn)[0].Name);

            bjorn.PrimaryPool.Current = 50;
            Assert.AreEqual("Slam x2", FightHudModel.SkillRows(session, bjorn)[0].Name);

            bjorn.PrimaryPool.Current = 100;
            Assert.AreEqual("Slam x4", FightHudModel.SkillRows(session, bjorn)[0].Name);
        }
    }
}
