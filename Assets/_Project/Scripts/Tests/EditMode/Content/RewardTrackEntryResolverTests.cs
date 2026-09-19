using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // Pins docs/archive/PLAN_REWARD_TRACKS.md §4's validation rules against
    // RewardTrackEntryResolver directly -- no JSON, no ContentBuilder, the
    // same shape RelicEntryResolverTests/SkillEntryResolverTests use.
    //
    // THREE RULES SINCE PROGRESSION V2 PHASE 4, not five. Every level from 2
    // to MaxLevel carries exactly one entry (which is what the old
    // "every milestone level, exactly once" and "the filler counts sum to
    // 29" rules said between them), a one-shot capability appears at most
    // once, and a signature reward needs a signature resource. The two
    // filler rules went with the filler mix -- see RawTrackLevel's header.
    public class RewardTrackEntryResolverTests
    {
        private static RawTrackLevel Level(int level, string reward, int amount = 0,
            string against = "", string skillId = "") =>
            new RawTrackLevel { level = level, reward = reward, amount = amount, against = against, skillId = skillId };

        private static RawRewardTrackEntry Track(string characterId, List<RawTrackLevel> levels) =>
            new RawRewardTrackEntry { characterId = characterId, levels = levels.ToArray() };

        // A FULL, VALID 39-LEVEL TRACK every refusal test below starts from
        // and breaks exactly one rule against. Same shape as
        // RewardTrackDefinition.DefaultLevels: Bump and Choice alternating
        // through the combat stretch so no two neighbours share a kind, the
        // two utilities where the real tracks put them, Identity above 30.
        // It has to satisfy RewardTrackNodeValidation as well as the format,
        // because the resolver runs both.
        private static List<RawTrackLevel> FullLevels()
        {
            var levels = new List<RawTrackLevel>();

            for (int level = 2; level <= 30; level++)
            {
                if (level == 8) levels.Add(Level(8, "Respec"));
                else if (level == 25) levels.Add(Level(25, "SecondLife", 1));
                else if (level % 2 == 0) levels.Add(Level(level, "MaxHealth", 30));
                else levels.Add(Level(level, "StatPoint", 4));
            }

            levels.Add(Identity(31, "Title", "Contractor"));
            levels.Add(Identity(32, "PlateRim", "silver"));
            levels.Add(Identity(33, "Title", "Champion"));
            levels.Add(Identity(34, "PortraitFrame"));
            levels.Add(Identity(35, "PlateEmboss", "silver"));
            levels.Add(Identity(36, "Title", "Veteran"));
            levels.Add(Identity(37, "VictoryPose"));
            levels.Add(Identity(38, "PlateRim", "gold"));
            levels.Add(Identity(39, "Title", "Legend"));
            levels.Add(Identity(40, "Mastery"));

            return levels;
        }

        private static RawTrackLevel Identity(int level, string kind, string value = "") =>
            new RawTrackLevel { level = level, reward = "Identity", identityKind = kind, value = value };

        private static void Replace(List<RawTrackLevel> levels, RawTrackLevel replacement)
        {
            levels.RemoveAll(l => l.level == replacement.level);
            levels.Add(replacement);
        }

        // ---- rule 1: every level from 2 to MaxLevel, exactly once ----

        [Test]
        public void AMissingLevel_IsRejectedNamingTheLevel()
        {
            var levels = FullLevels();
            levels.RemoveAll(l => l.level == 17);

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", levels) }, null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("17", joined);
            StringAssert.Contains("no entry", joined);
        }

        [Test]
        public void ALevelAuthoredTwice_IsRejectedNamingTheLevel()
        {
            var levels = FullLevels();
            levels.Add(Level(17, "MaxHealth", 30));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", levels) }, null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("17", joined);
            StringAssert.Contains("more than one entry", joined);
        }

        // Level 1 is where a character starts, not somewhere they arrive --
        // so it is outside the span, the same as 41 is.
        [TestCase(1)]
        [TestCase(41)]
        public void ALevelOutsideTheTracksSpan_IsRejected(int level)
        {
            var levels = FullLevels();
            levels.Add(Level(level, "MaxHealth", 30));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", levels) }, null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains(level.ToString(), joined);
            StringAssert.Contains("outside the track's span", joined);
        }

        // ---- rule 1, continued: a one-shot capability at most once ----

        [Test]
        public void SecondLifeAtTwoLevels_IsRejectedNamingBothLevels()
        {
            var levels = FullLevels();
            // FullLevels already carries SecondLife at 25; level 21 is a
            // Choice between two Bumps, so swapping it for a Utility breaks
            // no adjacency rule and leaves the duplicate as the only fault.
            Replace(levels, Level(21, "SecondLife", 1));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", levels) }, null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("SecondLife", joined);
            StringAssert.Contains("21", joined);
            StringAssert.Contains("25", joined);
        }

        // ---- rule 5: a signature reward needs a signature resource ----

        [Test]
        public void SignatureRewardOnACharacterWithNoSignatureResource_IsRejected()
        {
            var characters = new Dictionary<string, RewardTrackCharacterContext>
            {
                ["owl"] = new RewardTrackCharacterContext { HasSignatureResource = false },
            };

            var levels = FullLevels();
            Replace(levels, Level(16, "SignatureCapacity", 5));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("owl", levels) }, characters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no signature resource", string.Join("; ", errors));
        }

        // ---- the two filler rules are GONE, and their premise with them ----
        //
        // A one-shot capability as filler, and a filler element the
        // character cannot deal at level 1, were both refused because a
        // filler row's LEVEL was computed. Nothing computes a level now, so
        // what those rules refused is simply authorable: an element nobody
        // can deal yet sits at whichever level an author put it at, which is
        // the same freedom a MILESTONE always had (the old rule 4 exempted
        // milestones by name, for exactly this reason). Asserted rather than
        // left to be inferred from an absence, so a reader who goes looking
        // for the rule finds out here that it was retired on purpose.
        [Test]
        public void AnElementTheCharacterCannotYetDeal_IsAcceptedAtAnyLevel()
        {
            var characters = new Dictionary<string, RewardTrackCharacterContext>
            {
                ["sheep"] = new RewardTrackCharacterContext
                {
                    Level1DamageTypes = new[] { Stats.DamageType.Nature },
                },
            };

            var levels = FullLevels();
            Replace(levels, Level(6, "ElementalDamagePercent", 5, against: "Lightning"));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", levels) }, characters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            var entry = resolved[0].Levels.Single(l => l.Level == 6);
            Assert.AreEqual(TrackReward.ElementalDamagePercent, entry.Reward);
            Assert.AreEqual(Stats.DamageType.Lightning, entry.Against);
        }

        // ---- a valid two-track fixture ----

        [Test]
        public void TwoValidTracks_ResolveToThirtyNineLevelsEach()
        {
            var entries = new List<RawRewardTrackEntry>
            {
                Track("sheep", FullLevels()),
                Track("owl", FullLevels()),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(entries, null, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(2, resolved.Count);
            foreach (var track in resolved)
            {
                Assert.AreEqual(39, track.Levels.Length, $"{track.CharacterId}'s track");
                Assert.AreEqual(RewardTrack.StartingLevel + 1, track.Levels.Min(l => l.Level), $"{track.CharacterId}'s first level");
                Assert.AreEqual(RewardTrack.MaxLevel, track.Levels.Max(l => l.Level), $"{track.CharacterId}'s last level");
            }
        }
    }
}
