using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The content rules the Einherjar fields add: what an author may write on
    // a skill row (hitCount, damagePerMissingHealthPercent,
    // refundsSpentOnKillPercent, the Berserk transform fields) and on a talent
    // row (the new effect types' flag and threshold shapes).
    public class EinherjarResolverTests
    {
        private static RawSkillEntry Strike(string id = "hack") => new RawSkillEntry
        {
            id = id, displayName = "Hack", characterId = "bear", manaCost = 5, flatAmount = 5,
            physicalMove = true, unlockLevel = 999,
        };

        private static string Error(RawSkillEntry raw)
        {
            SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out _, out var errors);
            return errors == null ? null : string.Join("; ", errors);
        }

        [Test]
        public void HitCountTwo_ResolvesOnDamageSingle()
        {
            var raw = Strike();
            raw.hitCount = 2;

            Assert.IsTrue(SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out var resolved, out _));
            Assert.AreEqual(2, resolved[0].HitCount);
        }

        [Test]
        public void HitCountDefaultsToOneBlow()
        {
            Assert.IsTrue(SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { Strike() }, out var resolved, out _));
            Assert.AreEqual(1, resolved[0].HitCount);
        }

        [Test]
        public void HitCount_IsRefusedOnASweep_AndPastTheCeiling()
        {
            var sweep = Strike();
            sweep.effect = "DamageAll";
            sweep.hitCount = 2;
            StringAssert.Contains("hitCount only means anything on a DamageSingle", Error(sweep));

            var many = Strike();
            many.hitCount = 6;
            StringAssert.Contains("outside 0-5", Error(many));
        }

        [Test]
        public void TheRefund_NeedsASpendAllPrimarySkill()
        {
            var raw = Strike("headsplitter");
            raw.refundsSpentOnKillPercent = 50;
            StringAssert.Contains("spendsAllPrimary", Error(raw));

            raw.spendsAllPrimary = true;
            raw.manaCost = 30;
            Assert.IsTrue(SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out var resolved, out _));
            Assert.AreEqual(50, resolved[0].RefundsSpentOnKillPercent);
        }

        [Test]
        public void WoundScaling_IsRefusedOffDamageSingle()
        {
            var raw = Strike();
            raw.effect = "DamageAll";
            raw.damagePerMissingHealthPercent = 100;

            StringAssert.Contains("damagePerMissingHealthPercent", Error(raw));
        }

        private static RawSkillEntry Berserk() => new RawSkillEntry
        {
            id = "berserk", displayName = "Berserk", characterId = "bear", manaCost = 50, effect = "Transform",
            unlockLevel = 999,
            transform = new TransformGrant { defenseToAttackPercent = 50, primaryDrainPerTurn = 10 },
        };

        [Test]
        public void ADrainOnlyForm_NeedsNoTurnCount()
        {
            Assert.IsTrue(SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { Berserk() }, out var resolved, out var errors),
                string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(10, resolved[0].Transform.primaryDrainPerTurn);
        }

        [Test]
        public void ForbiddenEffects_MustNameARealSkillEffect()
        {
            var raw = Berserk();
            raw.transform.forbidsEffects = new[] { "Provoke", "Shout" };

            StringAssert.Contains("'Shout'", Error(raw));
        }

        private static string TalentError(params RawTalentEffect[] effects)
        {
            var raw = new RawTalentEntry { id = "t", displayName = "T", characterId = "bear", effects = effects };
            TalentEntryResolver.TryResolveAll(new List<RawTalentEntry> { raw }, out _, out var errors);
            return errors == null || errors.Count == 0 ? null : string.Join("; ", errors);
        }

        [Test]
        public void TheFlagEffects_TakeNoMagnitude()
        {
            Assert.IsNull(TalentError(new RawTalentEffect { type = "FuryEngineEinherjar" }));
            Assert.IsNull(TalentError(new RawTalentEffect { type = "TwinRampage" }));
            StringAssert.Contains("flag", TalentError(new RawTalentEffect { type = "FirstIdleTurnFree", magnitude = 1 }));
        }

        [Test]
        public void TheWoundedTargetCritRule_NeedsItsHealthGate()
        {
            Assert.IsNull(TalentError(new RawTalentEffect { type = "CritChanceBelowTargetHealth", magnitude = 25, threshold = 30 }));
            StringAssert.Contains("threshold", TalentError(new RawTalentEffect { type = "CritChanceBelowTargetHealth", magnitude = 25 }));
        }

        [Test]
        public void TheTierEffects_TakeAPositiveMagnitudeAndNoThreshold()
        {
            Assert.IsNull(TalentError(new RawTalentEffect { type = "MomentumTier", magnitude = 3 }));
            StringAssert.Contains("magnitude", TalentError(new RawTalentEffect { type = "BattleTranceTier" }));
        }
    }
}
