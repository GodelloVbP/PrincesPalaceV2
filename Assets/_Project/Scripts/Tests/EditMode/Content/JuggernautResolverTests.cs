using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The content rules the Juggernaut fields add: what an author may write on
    // a skill row (lifestealPercent, oncePerFight, windowTurns,
    // regenPercentOfMaxHealth) and on a talent row (the new effect types'
    // flag, threshold and skill-scope shapes).
    public class JuggernautResolverTests
    {
        private static RawSkillEntry Unbroken() => new RawSkillEntry
        {
            id = "unbroken", displayName = "Unbroken", characterId = "bear", manaCost = 50, effect = "Unbroken",
            unlockLevel = 999, windowTurns = 2, regenPercentOfMaxHealth = 15,
        };

        private static RawSkillEntry Cursed() => new RawSkillEntry
        {
            id = "cursed", displayName = "Cursed", characterId = "bear", manaCost = 50, effect = "CursedBlood",
            unlockLevel = 999, windowTurns = 2, oncePerFight = true,
        };

        private static string Error(RawSkillEntry raw)
        {
            SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out _, out var errors);
            return errors == null || errors.Count == 0 ? null : string.Join("; ", errors);
        }

        [Test]
        public void TheWindowSkills_ResolveWithTheirFields_AndTargetTheCaster()
        {
            Assert.IsTrue(SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { Unbroken(), Cursed() },
                out var resolved, out var errors), string.Join("; ", errors ?? new List<string>()));

            Assert.AreEqual(SkillTargeting.Self, resolved[0].Targeting);
            Assert.AreEqual(2, resolved[0].WindowTurns);
            Assert.AreEqual(15, resolved[0].RegenPercentOfMaxHealth);
            Assert.AreEqual(SkillTargeting.Self, resolved[1].Targeting);
            Assert.IsTrue(resolved[1].OncePerFight);
            Assert.IsFalse(resolved[0].OncePerFight);
        }

        [Test]
        public void AWindowSkill_NeedsItsTurns()
        {
            var raw = Unbroken();
            raw.windowTurns = 0;
            StringAssert.Contains("windowTurns", Error(raw));

            raw.windowTurns = 11;
            StringAssert.Contains("windowTurns", Error(raw));
        }

        [Test]
        public void Unbroken_NeedsItsRegen_AndNoOtherEffectMayCarryIt()
        {
            var raw = Unbroken();
            raw.regenPercentOfMaxHealth = 0;
            StringAssert.Contains("regenPercentOfMaxHealth", Error(raw));

            var cursed = Cursed();
            cursed.regenPercentOfMaxHealth = 15;
            StringAssert.Contains("only by an Unbroken", Error(cursed));
        }

        [Test]
        public void WindowTurns_IsRefusedOnAnyOtherEffect()
        {
            var raw = new RawSkillEntry
            {
                id = "hit", displayName = "Hit", characterId = "bear", manaCost = 5, flatAmount = 5,
                physicalMove = true, unlockLevel = 999, windowTurns = 2,
            };

            StringAssert.Contains("windowTurns is read only by", Error(raw));
        }

        [Test]
        public void Lifesteal_ResolvesOnADamageSingle_AndIsRefusedElsewhere()
        {
            var raw = new RawSkillEntry
            {
                id = "gorge", displayName = "Gorge", characterId = "bear", manaCost = 30, flatAmount = 30,
                physicalMove = true, unlockLevel = 999, lifestealPercent = 30,
            };
            Assert.IsTrue(SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out var resolved, out _));
            Assert.AreEqual(30, resolved[0].LifestealPercent);

            raw.effect = "DamageAll";
            StringAssert.Contains("lifestealPercent", Error(raw));

            raw.effect = "DamageSingle";
            raw.lifestealPercent = 101;
            StringAssert.Contains("lifestealPercent", Error(raw));
        }

        private static string TalentError(RawTalentEntry raw)
        {
            TalentEntryResolver.TryResolveAll(new List<RawTalentEntry> { raw }, out _, out var errors);
            return errors == null || errors.Count == 0 ? null : string.Join("; ", errors);
        }

        private static RawTalentEntry Node(string skillId, params RawTalentEffect[] effects) => new RawTalentEntry
        {
            id = "t", displayName = "T", characterId = "bear", effects = effects, appliesToSkillId = skillId ?? "",
        };

        [Test]
        public void TheSkillScopedRules_NeedTheSkillTheyUpgrade_AndStampIt()
        {
            foreach (string type in new[] { "SkillCooldownTurns", "SkillLifestealPercent" })
            {
                var effect = new RawTalentEffect { type = type, magnitude = 4 };
                StringAssert.Contains("appliesToSkillId", TalentError(Node(null, effect)), type);
                Assert.IsNull(TalentError(Node("second_wind", effect)), type);
            }

            var gate = new RawTalentEffect { type = "SkillDamageBonusBelowOwnHealth", magnitude = 25, threshold = 50 };
            Assert.IsTrue(TalentEntryResolver.TryResolveAll(new List<RawTalentEntry> { Node("gorge", gate) },
                out var resolved, out _));
            Assert.AreEqual("gorge", resolved[0].Effects[0].SkillId);
        }

        [Test]
        public void TheFlagEffects_TakeNoMagnitude()
        {
            foreach (string type in new[] { "FuryEngineJuggernaut", "HealReducesDelayedDamage", "CheatDeathFillsPrimary" })
            {
                Assert.IsNull(TalentError(Node(null, new RawTalentEffect { type = type })), type);
                StringAssert.Contains("flag", TalentError(Node(null, new RawTalentEffect { type = type, magnitude = 1 })), type);
            }
        }

        [Test]
        public void TheThresholdRules_NeedTheirThreshold()
        {
            StringAssert.Contains("threshold",
                TalentError(Node(null, new RawTalentEffect { type = "HealthCurveRegen", magnitude = 6 })));
            StringAssert.Contains("threshold",
                TalentError(Node(null, new RawTalentEffect { type = "DelayedDamagePercent", magnitude = 20 })));
            Assert.IsNull(TalentError(Node(null, new RawTalentEffect { type = "HealthCurveRegen", magnitude = 6, threshold = 2 })));
            Assert.IsNull(TalentError(Node(null, new RawTalentEffect { type = "DelayedDamagePercent", magnitude = 20, threshold = 3 })));
        }

        [Test]
        public void ThePlainNumberRules_RefuseAThreshold()
        {
            foreach (string type in new[] { "MaxHealthPercent", "DamagePerMissingHealth", "UnyieldingTier",
                         "ShortfallPaidInHealthPermille", "BloodPaidDamagePercent" })
            {
                Assert.IsNull(TalentError(Node(null, new RawTalentEffect { type = type, magnitude = 5 })), type);
                StringAssert.Contains("threshold",
                    TalentError(Node(null, new RawTalentEffect { type = type, magnitude = 5, threshold = 3 })), type);
            }
        }
    }
}
