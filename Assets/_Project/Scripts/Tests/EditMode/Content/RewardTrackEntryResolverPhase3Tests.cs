using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // PHASE 3's seven new reward kinds and PHASE 4's two, validated through
    // RewardTrackEntryResolver -- a separate file from
    // RewardTrackEntryResolverTests so a concurrent edit to that file has
    // nothing here to conflict with. Same fixture shape that file
    // establishes, retyped for the per-level format phase 4 landed.
    public class RewardTrackEntryResolverPhase3Tests
    {
        private static RawTrackLevel Level(int level, string reward, int amount = 0, string against = "",
            string skillId = "", string resource = "", string identityKind = "", string value = "") =>
            new RawTrackLevel
            {
                level = level, reward = reward, amount = amount, against = against, skillId = skillId,
                resource = resource, identityKind = identityKind, value = value,
            };

        private static RawRewardTrackEntry Track(string characterId, List<RawTrackLevel> levels) =>
            new RawRewardTrackEntry { characterId = characterId, levels = levels.ToArray() };

        // A full, otherwise-legal 39-level track with the entry at level 10
        // REPLACED by whatever a test hands in -- the one slot each case
        // below puts its kind under test into. Level 10 sits between two
        // Choice nodes in this table, so no adjacency rule fires whatever
        // kind lands there and a refusal can only be the rule under test.
        private static List<RawTrackLevel> LevelsWith(RawTrackLevel underTest)
        {
            var levels = new List<RawTrackLevel>();

            for (int level = 2; level <= 30; level++)
            {
                if (level == 10) continue;
                if (level == 8) levels.Add(Level(8, "Respec"));
                else if (level == 25) levels.Add(Level(25, "SecondLife", 1));
                else if (level % 2 == 0) levels.Add(Level(level, "MaxHealth", 30));
                else levels.Add(Level(level, "StatPoint", 4));
            }

            // Levels 9 and 11 are odd, so both are Choice nodes already.
            levels.Add(underTest);

            levels.Add(Level(31, "Identity", identityKind: "Title", value: "Contractor"));
            levels.Add(Level(32, "Identity", identityKind: "PlateRim", value: "silver"));
            levels.Add(Level(33, "Identity", identityKind: "Title", value: "Champion"));
            levels.Add(Level(34, "Identity", identityKind: "PortraitFrame"));
            levels.Add(Level(35, "Identity", identityKind: "PlateEmboss", value: "silver"));
            levels.Add(Level(36, "Identity", identityKind: "Title", value: "Veteran"));
            levels.Add(Level(37, "Identity", identityKind: "VictoryPose"));
            levels.Add(Level(38, "Identity", identityKind: "PlateRim", value: "gold"));
            levels.Add(Level(39, "Identity", identityKind: "Title", value: "Legend"));
            levels.Add(Level(40, "Identity", identityKind: "Mastery"));

            return levels;
        }

        private static RewardTrackSkillContext SkillCtx(string name, int manaCost, int resourceCost, bool hasFlat) =>
            new RewardTrackSkillContext(name, manaCost, resourceCost, hasFlat);

        // A shear-like skill (0 mana, 3 signature) and a slam-like one (0
        // mana, 0 signature, DamageSingle/no packets -- has a flatAmount
        // path) plus a spell (8 mana) -- enough shapes for every
        // cross-catalogue rule below.
        private static RewardTrackCharacterContext Context(bool hasSignature = true, bool primaryPoolStartsZero = false) => new RewardTrackCharacterContext
        {
            HasSignatureResource = hasSignature,
            PrimaryPoolStartsZero = primaryPoolStartsZero,
            SignatureDisplayName = "Wool",
            Skills = new Dictionary<string, RewardTrackSkillContext>
            {
                ["shear"] = SkillCtx("Shear", 0, 3, hasFlat: false),
                ["placeholder_brawler_slam"] = SkillCtx("Slam", 0, 0, hasFlat: true),
                ["prismatic_orb"] = SkillCtx("Prismatic Orb", 8, 0, hasFlat: false),
                ["frost_flare"] = SkillCtx("Frost Flare", 6, 0, hasFlat: false), // fixed-damage: no flat path
            },
        };

        private static bool TryOne(RawTrackLevel underTest, string characterId, RewardTrackCharacterContext context,
            out List<ResolvedRewardTrack> resolved, out List<string> errors)
        {
            var track = Track(characterId, LevelsWith(underTest));
            return RewardTrackEntryResolver.TryResolveAll(new List<RawRewardTrackEntry> { track },
                new Dictionary<string, RewardTrackCharacterContext> { [characterId] = context },
                out resolved, out errors);
        }

        private static ResolvedTrackLevel TenthOf(List<ResolvedRewardTrack> resolved) =>
            resolved[0].Levels.Single(l => l.Level == 10);

        // ---- SkillCostDelta ----

        [Test]
        public void SkillCostDelta_OnTheResourceTheSkillActuallyCosts_Resolves()
        {
            bool ok = TryOne(Level(10, "SkillCostDelta", amount: 1, skillId: "shear", resource: "Signature"),
                "sheep", Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = TenthOf(resolved);
            Assert.AreEqual(TrackReward.SkillCostDelta, entry.Reward);
            Assert.AreEqual(TrackResourceTarget.Signature, entry.Resource);
            Assert.AreEqual("shear", entry.SkillId);
        }

        [Test]
        public void SkillCostDelta_OnAResourceTheSkillDoesNotCost_IsRejected()
        {
            // Shear costs 0 mana -- discounting Mana on it is the mistake.
            bool ok = TryOne(Level(10, "SkillCostDelta", amount: 1, skillId: "shear", resource: "Mana"),
                "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("does not cost", string.Join("; ", errors));
        }

        [Test]
        public void SkillCostDelta_OnAnUnknownSkillId_IsRejected()
        {
            bool ok = TryOne(Level(10, "SkillCostDelta", amount: 1, skillId: "no_such_skill", resource: "Mana"),
                "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a skill", string.Join("; ", errors));
        }

        [Test]
        public void SkillCostDelta_WithNoResource_IsRejected()
        {
            bool ok = TryOne(Level(10, "SkillCostDelta", amount: 1, skillId: "shear"),
                "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs a resource", string.Join("; ", errors));
        }

        // ---- SkillFlatDelta ----

        [Test]
        public void SkillFlatDelta_OnASkillWithAFlatAmountPath_Resolves()
        {
            bool ok = TryOne(Level(10, "SkillFlatDelta", amount: 5, skillId: "placeholder_brawler_slam"),
                "bear", Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = TenthOf(resolved);
            Assert.AreEqual("placeholder_brawler_slam", entry.SkillId);
            Assert.AreEqual(5, entry.Amount);
        }

        [Test]
        public void SkillFlatDelta_OnAFixedDamageSkill_IsRejected()
        {
            // frost_flare has no flatAmount path in the fixture (fixed
            // damage packets instead).
            bool ok = TryOne(Level(10, "SkillFlatDelta", amount: 5, skillId: "frost_flare"),
                "owl", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no flatAmount path", string.Join("; ", errors));
        }

        // ---- SkillPowerDelta (phase 4) ----

        [Test]
        public void SkillPowerDelta_OnASkillThatSpendsAResource_Resolves()
        {
            bool ok = TryOne(Level(10, "SkillPowerDelta", amount: 1, skillId: "shear"),
                "sheep", Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = TenthOf(resolved);
            Assert.AreEqual(TrackReward.SkillPowerDelta, entry.Reward);
            Assert.AreEqual("shear", entry.SkillId);
            Assert.AreEqual("Shear", entry.SkillDisplayName);
        }

        // `power` is paid per POINT of resource spent, so a skill that
        // spends none never reads it -- the same dead-authoring refusal
        // SkillFlatDelta makes for a skill with no flatAmount path.
        [Test]
        public void SkillPowerDelta_OnASkillThatSpendsNoResource_IsRejected()
        {
            bool ok = TryOne(Level(10, "SkillPowerDelta", amount: 1, skillId: "placeholder_brawler_slam"),
                "bear", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("spends no signature", string.Join("; ", errors));
        }

        [Test]
        public void SkillPowerDelta_OnAnUnknownSkillId_IsRejected()
        {
            bool ok = TryOne(Level(10, "SkillPowerDelta", amount: 1, skillId: "no_such_skill"),
                "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a skill", string.Join("; ", errors));
        }

        // ---- SpellDamagePercent (phase 4) ----
        //
        // No cross-catalogue rule at all: it names no skill and no element.
        // Pinned here only to prove it parses and keeps its amount, since a
        // kind the resolver cannot spell is refused as "not a known
        // TrackReward" and would fail every authored track at once.
        [Test]
        public void SpellDamagePercent_Resolves()
        {
            bool ok = TryOne(Level(10, "SpellDamagePercent", amount: 5),
                "owl", Context(hasSignature: false), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = TenthOf(resolved);
            Assert.AreEqual(TrackReward.SpellDamagePercent, entry.Reward);
            Assert.AreEqual(5, entry.Amount);
        }

        // ---- SpellCostDelta / FuryGainOnAttack / SignatureAbsorbPerPoint ----
        // These three carry no cross-catalogue rule (SignatureAbsorbPerPoint
        // aside, covered by rule 5 below) -- they resolve like any other
        // numeric kind, and are pinned at the Domain-arithmetic level
        // (RewardTrackPhase3Tests) rather than here. FuryStartOfFight moved
        // out of this list once rule 6 gave it one -- see the three tests
        // below.

        [Test]
        public void SignatureAbsorbPerPoint_OnACharacterWithNoSignatureResource_IsRejected()
        {
            bool ok = TryOne(Level(10, "SignatureAbsorbPerPoint", amount: 1),
                "owl", Context(hasSignature: false), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no signature resource", string.Join("; ", errors));
        }

        // ---- FuryStartOfFight (rule 6) ----

        [Test]
        public void FuryStartOfFight_OnACharacterWhosePrimaryPoolDoesNotStartAtZero_IsRejected()
        {
            bool ok = TryOne(Level(10, "FuryStartOfFight", amount: 25),
                "bear", Context(primaryPoolStartsZero: false), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("starts at Zero", string.Join("; ", errors));
        }

        [Test]
        public void FuryStartOfFight_WithAmountNotAboveZero_IsRejected()
        {
            bool ok = TryOne(Level(10, "FuryStartOfFight", amount: 0),
                "bear", Context(primaryPoolStartsZero: true), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("starts at Zero", string.Join("; ", errors));
        }

        [Test]
        public void FuryStartOfFight_OnAZeroStartOwnerWithAPositiveAmount_Resolves()
        {
            bool ok = TryOne(Level(10, "FuryStartOfFight", amount: 25),
                "bear", Context(primaryPoolStartsZero: true), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = TenthOf(resolved);
            Assert.AreEqual(TrackReward.FuryStartOfFight, entry.Reward);
            Assert.AreEqual(25, entry.Amount);
        }

        // ---- Identity ----

        [Test]
        public void IdentityTitle_ResolvesWithItsValue()
        {
            bool ok = TryOne(Level(10, "Identity", identityKind: "Title", value: "Contractor"),
                "sheep", Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = TenthOf(resolved);
            Assert.AreEqual(TrackIdentityKind.Title, entry.IdentityKind);
            Assert.AreEqual("Contractor", entry.IdentityValue);
        }

        [Test]
        public void IdentityTitle_WithNoValue_IsRejected()
        {
            bool ok = TryOne(Level(10, "Identity", identityKind: "Title"),
                "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs a value", string.Join("; ", errors));
        }

        [Test]
        public void IdentityPlateRim_AcceptsSilverOrGold()
        {
            bool ok = TryOne(Level(10, "Identity", identityKind: "PlateRim", value: "gold"),
                "sheep", Context(), out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("gold", TenthOf(resolved).IdentityValue);
        }

        [Test]
        public void IdentityPlateRim_WithAnInvalidValue_IsRejected()
        {
            bool ok = TryOne(Level(10, "Identity", identityKind: "PlateRim", value: "bronze"),
                "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("'silver' or 'gold'", string.Join("; ", errors));
        }

        [Test]
        public void IdentityPortraitFrame_NeedsNoValue()
        {
            bool ok = TryOne(Level(10, "Identity", identityKind: "PortraitFrame"),
                "sheep", Context(), out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void Identity_WithNoIdentityKind_IsRejected()
        {
            bool ok = TryOne(Level(10, "Identity"), "sheep", Context(), out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("needs an identityKind", string.Join("; ", errors));
        }
    }
}
