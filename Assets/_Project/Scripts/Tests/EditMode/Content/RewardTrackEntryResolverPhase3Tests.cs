using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // PHASE 3's seven new reward kinds, validated through
    // RewardTrackEntryResolver -- a separate file from
    // RewardTrackEntryResolverTests so a concurrent edit to that file (phase
    // 2 is landing in its own worktree at the same time) has nothing here to
    // conflict with. Same fixture shape that file already established.
    public class RewardTrackEntryResolverPhase3Tests
    {
        private static RawTrackMilestone Milestone(int level, string reward, int amount = 0, string against = "",
            string skillId = "", string resource = "", string identityKind = "", string value = "") =>
            new RawTrackMilestone
            {
                level = level, reward = reward, amount = amount, against = against, skillId = skillId,
                resource = resource, identityKind = identityKind, value = value,
            };

        private static RawTrackFiller Filler(string reward, int count, int amount = 1, string against = "") =>
            new RawTrackFiller { reward = reward, amount = amount, against = against, count = count };

        private static RawRewardTrackEntry Track(string characterId, List<RawTrackMilestone> milestones,
            List<RawTrackFiller> filler) =>
            new RawRewardTrackEntry { characterId = characterId, milestones = milestones.ToArray(), filler = filler.ToArray() };

        // A full, otherwise-legal twelve-milestone/87-filler track (the same
        // shape RewardTrackEntryResolverTests.FullMilestones/FullFiller
        // build), with ONE milestone level free (10) for a test to overwrite
        // with the P3 kind under test.
        private static List<RawTrackMilestone> BaseMilestones() => new List<RawTrackMilestone>
        {
            Milestone(20, "Respec"),
            Milestone(25, "MaxHealth", 15),
            Milestone(30, "MaxHealth", 15),
            Milestone(40, "MaxHealth", 15),
            Milestone(45, "MaxHealth", 15),
            Milestone(50, "MaxHealth", 15),
            Milestone(60, "MaxHealth", 15),
            Milestone(70, "MaxHealth", 15),
            Milestone(80, "StatPoint", 10),
            Milestone(90, "SecondLife", 1),
            Milestone(100, "MaxHealth", 15),
        };

        private static List<RawTrackFiller> FullFiller() => new List<RawTrackFiller>
        {
            Filler("StatPoint", 40, amount: 1),
            Filler("MaxHealth", 47, amount: 2),
        };

        private static RewardTrackSkillContext SkillCtx(string name, int manaCost, int resourceCost, bool hasFlat) =>
            new RewardTrackSkillContext(name, manaCost, resourceCost, hasFlat);

        // A shear-like skill (0 mana, 3 signature) and a slam-like one (0
        // mana, 0 signature, DamageSingle/no packets -- has a flatAmount
        // path) plus a spell (8 mana) -- enough shapes for every P3
        // cross-catalogue rule below.
        private static RewardTrackCharacterContext Context(bool hasSignature = true) => new RewardTrackCharacterContext
        {
            HasSignatureResource = hasSignature,
            SignatureDisplayName = "Wool",
            Skills = new Dictionary<string, RewardTrackSkillContext>
            {
                ["shear"] = SkillCtx("Shear", 0, 3, hasFlat: false),
                ["placeholder_brawler_slam"] = SkillCtx("Slam", 0, 0, hasFlat: true),
                ["prismatic_orb"] = SkillCtx("Prismatic Orb", 8, 0, hasFlat: false),
                ["frost_flare"] = SkillCtx("Frost Flare", 6, 0, hasFlat: false), // fixed-damage: no flat path
            },
        };

        private static bool TryOne(RawRewardTrackEntry track, RewardTrackCharacterContext context,
            out List<ResolvedRewardTrack> resolved, out List<string> errors) =>
            RewardTrackEntryResolver.TryResolveAll(new List<RawRewardTrackEntry> { track },
                new Dictionary<string, RewardTrackCharacterContext> { [track.characterId] = context },
                out resolved, out errors);

        // ---- SkillCostDelta ----

        [Test]
        public void SkillCostDelta_OnTheResourceTheSkillActuallyCosts_Resolves()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "SkillCostDelta", amount: 1, skillId: "shear", resource: "Signature"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = System.Array.Find(resolved[0].Milestones, m => m.Level == 10);
            Assert.AreEqual(TrackReward.SkillCostDelta, entry.Reward);
            Assert.AreEqual(TrackResourceTarget.Signature, entry.Resource);
            Assert.AreEqual("shear", entry.SkillId);
        }

        [Test]
        public void SkillCostDelta_OnAResourceTheSkillDoesNotCost_IsRejected()
        {
            var milestones = BaseMilestones();
            // Shear costs 0 mana -- discounting Mana on it is the mistake.
            milestones.Add(Milestone(10, "SkillCostDelta", amount: 1, skillId: "shear", resource: "Mana"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("does not cost", string.Join("; ", errors));
        }

        [Test]
        public void SkillCostDelta_OnAnUnknownSkillId_IsRejected()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "SkillCostDelta", amount: 1, skillId: "no_such_skill", resource: "Mana"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a skill", string.Join("; ", errors));
        }

        [Test]
        public void SkillCostDelta_WithNoResource_IsRejected()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "SkillCostDelta", amount: 1, skillId: "shear"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs a resource", string.Join("; ", errors));
        }

        [Test]
        public void SkillCostDelta_AsFiller_IsRejected()
        {
            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 39, amount: 1),
                Filler("MaxHealth", 47, amount: 2),
                Filler("SkillCostDelta", 1, amount: 1),
            };

            bool ok = TryOne(Track("sheep", BaseMilestones(), filler), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("cannot appear as filler", string.Join("; ", errors));
        }

        // ---- SkillFlatDelta ----

        [Test]
        public void SkillFlatDelta_OnASkillWithAFlatAmountPath_Resolves()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "SkillFlatDelta", amount: 5, skillId: "placeholder_brawler_slam"));

            bool ok = TryOne(Track("bear", milestones, FullFiller()), Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = System.Array.Find(resolved[0].Milestones, m => m.Level == 10);
            Assert.AreEqual("placeholder_brawler_slam", entry.SkillId);
            Assert.AreEqual(5, entry.Amount);
        }

        [Test]
        public void SkillFlatDelta_OnAFixedDamageSkill_IsRejected()
        {
            var milestones = BaseMilestones();
            // frost_flare has no flatAmount path in the fixture (fixed
            // damage packets instead).
            milestones.Add(Milestone(10, "SkillFlatDelta", amount: 5, skillId: "frost_flare"));

            bool ok = TryOne(Track("owl", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no flatAmount path", string.Join("; ", errors));
        }

        // ---- SpellCostDelta / FuryGainOnAttack / FuryStartOfFight / SignatureAbsorbPerPoint ----
        // These four carry no cross-catalogue rule (SignatureAbsorbPerPoint
        // aside, covered by rule 5 below) -- they resolve like any other
        // numeric kind, and are pinned at the Domain-arithmetic level
        // (RewardTrackPhase3Tests) rather than here.

        [Test]
        public void SignatureAbsorbPerPoint_OnACharacterWithNoSignatureResource_IsRejected()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "SignatureAbsorbPerPoint", amount: 1));

            bool ok = TryOne(Track("owl", milestones, FullFiller()), Context(hasSignature: false), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no signature resource", string.Join("; ", errors));
        }

        [Test]
        public void SignatureAbsorbs_IsAliasedToSignatureAbsorbPerPointAtOne()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "SignatureAbsorbs"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = System.Array.Find(resolved[0].Milestones, m => m.Level == 10);
            Assert.AreEqual(TrackReward.SignatureAbsorbPerPoint, entry.Reward);
            Assert.AreEqual(1, entry.Amount);
        }

        // ---- Identity ----

        [Test]
        public void IdentityTitle_ResolvesWithItsValue()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "Identity", identityKind: "Title", value: "Contractor"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = System.Array.Find(resolved[0].Milestones, m => m.Level == 10);
            Assert.AreEqual(TrackIdentityKind.Title, entry.IdentityKind);
            Assert.AreEqual("Contractor", entry.IdentityValue);
        }

        [Test]
        public void IdentityTitle_WithNoValue_IsRejected()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "Identity", identityKind: "Title"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs a value", string.Join("; ", errors));
        }

        [Test]
        public void IdentityPlateRim_AcceptsSilverOrGold()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "Identity", identityKind: "PlateRim", value: "gold"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = System.Array.Find(resolved[0].Milestones, m => m.Level == 10);
            Assert.AreEqual("gold", entry.IdentityValue);
        }

        [Test]
        public void IdentityPlateRim_WithAnInvalidValue_IsRejected()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "Identity", identityKind: "PlateRim", value: "bronze"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("'silver' or 'gold'", string.Join("; ", errors));
        }

        [Test]
        public void IdentityPortraitFrame_NeedsNoValue()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "Identity", identityKind: "PortraitFrame"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void Identity_WithNoIdentityKind_IsRejected()
        {
            var milestones = BaseMilestones();
            milestones.Add(Milestone(10, "Identity"));

            bool ok = TryOne(Track("sheep", milestones, FullFiller()), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs an identityKind", string.Join("; ", errors));
        }

        [Test]
        public void Identity_AsFiller_IsRejected()
        {
            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 39, amount: 1),
                Filler("MaxHealth", 47, amount: 2),
                Filler("Identity", 1, amount: 0),
            };

            bool ok = TryOne(Track("sheep", BaseMilestones(), filler), Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("cannot appear as filler", string.Join("; ", errors));
        }
    }
}
