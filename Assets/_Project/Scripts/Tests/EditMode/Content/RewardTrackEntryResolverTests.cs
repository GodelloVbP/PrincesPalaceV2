using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Pins docs/PLAN_REWARD_TRACKS.md §4's five validation rules against
    // RewardTrackEntryResolver directly -- no JSON, no ContentBuilder, the
    // same shape RelicEntryResolverTests/SkillEntryResolverTests use.
    public class RewardTrackEntryResolverTests
    {
        private static RawTrackMilestone Milestone(int level, string reward, int amount = 0,
            string against = "", string skillId = "") =>
            new RawTrackMilestone { level = level, reward = reward, amount = amount, against = against, skillId = skillId };

        private static RawTrackFiller Filler(string reward, int count, int amount = 1, string against = "") =>
            new RawTrackFiller { reward = reward, amount = amount, against = against, count = count };

        private static RawRewardTrackEntry Track(string characterId, List<RawTrackMilestone> milestones,
            List<RawTrackFiller> filler) =>
            new RawRewardTrackEntry { characterId = characterId, milestones = milestones.ToArray(), filler = filler.ToArray() };

        // The twelve fixed milestone levels, filled with the same interim
        // kinds RewardTrack.cs's own Milestones table uses today (only
        // StatPoint/MaxHealth/Respec/SecondLife exist on TrackReward until
        // P3 lands the rest) -- a full, valid milestone set every refusal
        // test below starts from and breaks exactly one rule against.
        private static List<RawTrackMilestone> FullMilestones() => new List<RawTrackMilestone>
        {
            Milestone(10, "MaxHealth", 15),
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

        // 40 + 47 = 87, the track's filler-level count -- mirrors
        // RewardTrack.cs's own FillerMix.
        private static List<RawTrackFiller> FullFiller() => new List<RawTrackFiller>
        {
            Filler("StatPoint", 40, amount: 1),
            Filler("MaxHealth", 47, amount: 2),
        };

        // ---- rule 1: every milestone level, exactly once; no non-milestone level named ----

        [Test]
        public void MissingMilestoneLevel_IsRejectedNamingTheLevel()
        {
            var milestones = FullMilestones();
            milestones.RemoveAll(m => m.level == 45);

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", milestones, FullFiller()) },
                null, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("45", string.Join("; ", errors));
            StringAssert.Contains("no entry", string.Join("; ", errors));
        }

        [Test]
        public void EntryAtANonMilestoneLevel_IsRejectedNamingItNotAMilestone()
        {
            var milestones = FullMilestones();
            milestones.Add(Milestone(44, "MaxHealth", 15));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", milestones, FullFiller()) },
                null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("44", joined);
            StringAssert.Contains("not a milestone level", joined);
        }

        // ---- rule 2: filler counts sum to exactly 87 ----

        [Test]
        public void FillerSumOf86_IsRejectedNaming87()
        {
            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 40, amount: 1),
                Filler("MaxHealth", 46, amount: 2),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", FullMilestones(), filler) },
                null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("86", joined);
            StringAssert.Contains("87", joined);
        }

        [Test]
        public void FillerSumOf88_IsRejectedNaming87()
        {
            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 40, amount: 1),
                Filler("MaxHealth", 48, amount: 2),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", FullMilestones(), filler) },
                null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("88", joined);
            StringAssert.Contains("87", joined);
        }

        // ---- rule 3: no one-shot capability as filler ----

        [Test]
        public void RespecAsFiller_IsRejected()
        {
            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 40, amount: 1),
                Filler("MaxHealth", 46, amount: 2),
                Filler("Respec", 1, amount: 0),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", FullMilestones(), filler) },
                null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("Respec", joined);
            StringAssert.Contains("cannot appear as filler", joined);
        }

        [Test]
        public void SecondLifeAsFiller_IsRejected()
        {
            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 40, amount: 1),
                Filler("MaxHealth", 46, amount: 2),
                Filler("SecondLife", 1, amount: 1),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", FullMilestones(), filler) },
                null, out _, out var errors);

            Assert.IsFalse(ok);
            string joined = string.Join("; ", errors);
            StringAssert.Contains("SecondLife", joined);
            StringAssert.Contains("cannot appear as filler", joined);
        }

        // ---- rules 4 and 5: cross-catalogue ----
        //
        // ElementalDamagePercent and the four SignatureX kinds were
        // unreachable until P3 added them to TrackReward -- Enum.TryParse
        // refused any entry naming one as "not a known TrackReward" before
        // the resolver's own rule 4/5 checks were ever reached. P3 has
        // landed (docs/PLAN_REWARD_TRACKS.md's P6), so both are asserted for
        // real now.

        [Test]
        public void FillerElementalDamageOfAnUnknownElement_IsRejected()
        {
            var context = new RewardTrackCharacterContext
            {
                AttackType = Stats.DamageType.Nature,
                Level1DamageTypes = new[] { Stats.DamageType.Nature },
            };
            var characters = new Dictionary<string, RewardTrackCharacterContext> { ["sheep"] = context };

            var filler = new List<RawTrackFiller>
            {
                Filler("StatPoint", 39, amount: 1),
                Filler("MaxHealth", 47, amount: 2),
                Filler("ElementalDamagePercent", 1, amount: 1, against: "Lightning"),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("sheep", FullMilestones(), filler) },
                characters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("level 1", string.Join("; ", errors));
        }

        [Test]
        public void SignatureRewardOnACharacterWithNoSignatureResource_IsRejected()
        {
            var characters = new Dictionary<string, RewardTrackCharacterContext>
            {
                ["owl"] = new RewardTrackCharacterContext { HasSignatureResource = false },
            };

            var milestones = FullMilestones();
            milestones.RemoveAll(m => m.level == 25);
            milestones.Add(Milestone(25, "SignatureCapacity", 5));

            bool ok = RewardTrackEntryResolver.TryResolveAll(
                new List<RawRewardTrackEntry> { Track("owl", milestones, FullFiller()) },
                characters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("no signature resource", string.Join("; ", errors));
        }

        // ---- a valid two-track fixture ----

        [Test]
        public void TwoValidTracks_ResolveToTwelveMilestonesEach()
        {
            var entries = new List<RawRewardTrackEntry>
            {
                Track("sheep", FullMilestones(), FullFiller()),
                Track("owl", FullMilestones(), FullFiller()),
            };

            bool ok = RewardTrackEntryResolver.TryResolveAll(entries, null, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(2, resolved.Count);
            foreach (var track in resolved)
            {
                Assert.AreEqual(12, track.Milestones.Length, $"{track.CharacterId}'s track");
                Assert.AreEqual(87, SumCounts(track), $"{track.CharacterId}'s filler");
            }
        }

        private static int SumCounts(ResolvedRewardTrack track)
        {
            int total = 0;
            foreach (var f in track.Filler) total += f.Count;
            return total;
        }
    }
}
