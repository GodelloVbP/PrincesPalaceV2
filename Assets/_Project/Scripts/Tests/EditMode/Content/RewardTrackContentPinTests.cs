using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Reads Assets/_Project/ContentData/{pools,characters,skills,reward_tracks}.json
    // from disk through the REAL resolvers and materialises the result with
    // RewardTrackDefinition.From, the same shape a live save would read. Pins
    // the literal numbers docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md
    // §4 gives the three shipped tracks, so a retune that silently changes
    // what a level pays is caught here rather than only by eye on the screen.
    //
    // REWRITTEN BY PROGRESSION V2 PHASE 4, and the ignore that stood at the
    // top of this class through phases 2 and 3 is gone with it. Phase 2 cut
    // RewardTrack.MaxLevel from 100 to 40 while reward_tracks.json still held
    // the hundred-level milestone/filler content, so the resolver refused the
    // whole file and all 22 assertions failed at the same first line; phase 4
    // is what authored the 40-level tracks these read.
    //
    // EVERY LEVEL, NOT A SAMPLE. The old version pinned the four milestones
    // that were content-specific and left the rest to a filler mix nobody
    // authored by level. There is no filler any more, so LEVEL BY LEVEL below
    // walks all 39 rows of each track against the plan's own table -- which is
    // the only form in which "the shipped track is the designed track" can be
    // asserted at all.
    //
    // TWO PARSERS, ONE ASSERTION, same pattern as ContentStampIdsTests: this
    // class has no UnityEngine dependency, so it is a [D] class and runs
    // under plain `dotnet test`, which has no JsonUtility. RepoRoot/DataPath/
    // ParseFile live on Shared/ContentDataFiles.
    public class RewardTrackContentPinTests
    {
        // The real pools.json through the real resolver, because this file
        // reads the real characters.json -- the pool ids a character's
        // primaryPoolId is checked against have to be the ones a build would
        // actually produce, the same way ContentBuilder resolves pools first
        // and hands the ids down.
        private static List<ResolvedPool> Pools()
        {
            var raw = ContentDataFiles.ParseFile<RawPoolFile>(ContentDataFiles.DataPath("pools.json")).pools;
            bool ok = PoolEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "pools.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static List<string> PoolIds() => Pools().Select(pool => pool.Id).ToList();

        private static List<ResolvedCharacter> Characters()
        {
            var raw = ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters;
            bool ok = CharacterEntryResolver.TryResolveAll(raw, PoolIds(), out var resolved, out var errors);
            Assert.IsTrue(ok, "characters.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        // AND THE POOL SETS, for the same reason the pool ids above exist:
        // skills.json stopped resolving against itself alone the moment a
        // rule turned on the OWNER's pool. Bjorn's slam and brace are free
        // because `fury` starts at zero, and a resolve that was not told so
        // refuses the file outright. PoolOwnership is the one derivation
        // ContentBuilder uses too.
        private static List<ResolvedSkill> Skills()
        {
            var raw = ContentDataFiles.ParseFile<RawSkillFile>(ContentDataFiles.DataPath("skills.json")).skills;
            var pools = Pools();
            var characters = Characters();

            bool ok = SkillEntryResolver.TryResolveAll(
                raw,
                PoolOwnership.BookRefusers(pools, characters),
                PoolOwnership.ZeroStartOwners(pools, characters),
                PoolOwnership.PrimaryPoolOwners(pools, characters),
                out var resolved, out var errors);

            Assert.IsTrue(ok, "skills.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        // The same assembly ContentBuilder.BuildRewardTracks uses --
        // RewardTrackCharacterContext.BuildAll is the one seam both call, so
        // this pin reads the tracks through the exact context a real build
        // resolves them against rather than a hand-rolled approximation of
        // it that could quietly drift.
        private static Dictionary<string, RewardTrackDefinition> Tracks()
        {
            var characters = Characters();
            var skills = Skills();
            var contexts = RewardTrackCharacterContext.BuildAll(Pools(), characters, skills);

            var raw = ContentDataFiles.ParseFile<RawRewardTrackFile>(ContentDataFiles.DataPath("reward_tracks.json")).tracks;
            bool ok = RewardTrackEntryResolver.TryResolveAll(raw, contexts, out var resolved, out var errors);
            Assert.IsTrue(ok, "reward_tracks.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));

            return resolved.ToDictionary(t => t.CharacterId, t => RewardTrackDefinition.From(t));
        }

        private static RewardTrackDefinition Sheep() => Tracks()["sheep"];
        private static RewardTrackDefinition Owl() => Tracks()["owl"];
        private static RewardTrackDefinition Bear() => Tracks()["bear"];

        private static void AssertEntry(RewardTrackDefinition track, int level, TrackReward reward, int amount,
            string skillId = null, DamageType? against = null)
        {
            var entry = track.At(level);
            Assert.AreEqual(reward, entry.Reward, $"{track.CharacterId} level {level} reward");
            Assert.AreEqual(amount, entry.Amount, $"{track.CharacterId} level {level} amount");
            if (skillId != null) Assert.AreEqual(skillId, entry.SkillId, $"{track.CharacterId} level {level} skillId");
            if (against.HasValue) Assert.AreEqual(against.Value, entry.Against, $"{track.CharacterId} level {level} element");
        }

        private static void AssertTitle(RewardTrackDefinition track, int level, string title)
        {
            var entry = track.At(level);
            Assert.AreEqual(TrackReward.Identity, entry.Reward, $"{track.CharacterId} level {level} reward");
            Assert.AreEqual(TrackIdentityKind.Title, entry.IdentityKind, $"{track.CharacterId} level {level} kind");
            Assert.AreEqual(title, entry.IdentityValue, $"{track.CharacterId} level {level} title");
        }

        // ---- Shawn, level by level -- §4's first column --------------------

        [Test]
        public void ShawnsTrackIsTheAuthoredTable()
        {
            var t = Sheep();

            AssertEntry(t, 2, TrackReward.MaxHealth, 40);
            AssertEntry(t, 3, TrackReward.UnlockSkill, 0, skillId: "woolgathering");
            AssertEntry(t, 4, TrackReward.StatPoint, 4);
            AssertEntry(t, 5, TrackReward.SignatureGainPerTurn, 1);
            AssertEntry(t, 6, TrackReward.StatPoint, 4);
            AssertEntry(t, 7, TrackReward.ElementalDamagePercent, 5, against: DamageType.Nature);
            AssertEntry(t, 8, TrackReward.Respec, 0);
            AssertEntry(t, 9, TrackReward.StatPoint, 4);
            AssertEntry(t, 10, TrackReward.UnlockSkill, 0, skillId: "battering_ram");
            AssertEntry(t, 11, TrackReward.StatPoint, 4);
            AssertEntry(t, 12, TrackReward.SkillCostDelta, 1, skillId: "shear");
            AssertEntry(t, 13, TrackReward.StatPoint, 4);
            AssertEntry(t, 14, TrackReward.MaxHealth, 40);
            AssertEntry(t, 15, TrackReward.UnlockSkill, 0, skillId: "tuck_in");
            AssertEntry(t, 16, TrackReward.StatPoint, 4);
            AssertEntry(t, 17, TrackReward.ElementalDamagePercent, 5, against: DamageType.Nature);
            AssertEntry(t, 18, TrackReward.StatPoint, 4);
            AssertEntry(t, 19, TrackReward.SignatureGainOnDamageTaken, 2);
            AssertEntry(t, 20, TrackReward.UnlockSkill, 0, skillId: "cinderfault");
            AssertEntry(t, 21, TrackReward.StatPoint, 4);
            AssertEntry(t, 22, TrackReward.MaxHealth, 40);
            AssertEntry(t, 23, TrackReward.StatPoint, 4);
            AssertEntry(t, 24, TrackReward.SkillCostDelta, 2, skillId: "battering_ram");
            AssertEntry(t, 25, TrackReward.SecondLife, 1);
            AssertEntry(t, 26, TrackReward.SkillPowerDelta, 3, skillId: "tuck_in");
            AssertEntry(t, 27, TrackReward.StatPoint, 4);
            AssertEntry(t, 28, TrackReward.ElementalDamagePercent, 5, against: DamageType.Nature);
            AssertEntry(t, 29, TrackReward.StatPoint, 4);
            AssertEntry(t, 30, TrackReward.UnlockSkill, 0, skillId: "placeholder_shawn_capstone");
        }

        // CINDERFAULT MOVED FROM 70 TO 20, which is the single biggest
        // content change in this phase and is pinned on its own so the move
        // reads as a decision rather than as a line in a 29-row table. It
        // used to be Shawn's level-70 milestone on a hundred-level track; V3
        // is level 20 now, and the whole cap is 40.
        [Test]
        public void ShawnLearnsCinderfaultAtTwentyRatherThanSeventy()
        {
            var track = Sheep();

            Assert.AreEqual(20, LevelThatUnlocks(track, "cinderfault"),
                "Cinderfault is no longer the level-20 ability");
            CollectionAssert.DoesNotContain(track.SkillsCollected(19), "cinderfault",
                "it was collectable before its own node");
        }

        private static int LevelThatUnlocks(RewardTrackDefinition track, string skillId)
        {
            for (int level = RewardTrack.StartingLevel; level <= RewardTrack.MaxLevel; level++)
            {
                var entry = track.At(level);
                if (entry.Reward == TrackReward.UnlockSkill && entry.SkillId == skillId) return level;
            }

            return 0;
        }

        // ---- Bjorn, level by level -- §4's second column -------------------

        [Test]
        public void BjornsTrackIsTheAuthoredTable()
        {
            var t = Bear();

            AssertEntry(t, 2, TrackReward.MaxHealth, 50);
            AssertTitle(t, 3, "Recruit");
            AssertEntry(t, 4, TrackReward.StatPoint, 4);
            AssertEntry(t, 5, TrackReward.MaxHealth, 50);
            AssertEntry(t, 6, TrackReward.StatPoint, 4);
            AssertEntry(t, 7, TrackReward.ElementalDamagePercent, 5, against: DamageType.Physical);
            AssertEntry(t, 8, TrackReward.Respec, 0);
            AssertEntry(t, 9, TrackReward.StatPoint, 4);
            AssertEntry(t, 10, TrackReward.ElementalDamagePercent, 5, against: DamageType.Physical);
            AssertEntry(t, 11, TrackReward.StatPoint, 4);
            AssertEntry(t, 12, TrackReward.SkillFlatDelta, 5, skillId: "placeholder_brawler_slam");
            AssertEntry(t, 13, TrackReward.StatPoint, 4);
            AssertEntry(t, 14, TrackReward.MaxHealth, 50);
            AssertEntry(t, 15, TrackReward.FuryStartOfFight, 25);
            AssertEntry(t, 16, TrackReward.StatPoint, 4);
            AssertEntry(t, 17, TrackReward.ElementalDamagePercent, 5, against: DamageType.Physical);
            AssertEntry(t, 18, TrackReward.StatPoint, 4);
            AssertEntry(t, 19, TrackReward.ElementalDamagePercent, 5, against: DamageType.Physical);
            AssertTitle(t, 20, "Stalwart");
            AssertEntry(t, 21, TrackReward.StatPoint, 4);
            AssertEntry(t, 22, TrackReward.MaxHealth, 50);
            AssertEntry(t, 23, TrackReward.StatPoint, 4);
            AssertEntry(t, 24, TrackReward.SkillFlatDelta, 5, skillId: "placeholder_brawler_slam");
            AssertEntry(t, 25, TrackReward.SecondLife, 1);
            AssertEntry(t, 26, TrackReward.FuryStartOfFight, 50);
            AssertEntry(t, 27, TrackReward.StatPoint, 4);
            AssertEntry(t, 28, TrackReward.ElementalDamagePercent, 5, against: DamageType.Physical);
            AssertEntry(t, 29, TrackReward.StatPoint, 4);
            AssertEntry(t, 30, TrackReward.ElementalDamagePercent, 5, against: DamageType.Physical);
        }

        // NO FURY-PER-ATTACK NODE REMAINS: each of his three root talents is a
        // Fury engine that replaces the pool's flat gains, so such a node could
        // never pay. UnlockedAmount is the read site ContentDatabase
        // .BuildPrimaryPool uses; the fallback comes back at every level.
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(19)]
        [TestCase(40)]
        public void BjornsTrackAuthorsNoFuryGainOnAttack(int claimedLevel)
        {
            Assert.AreEqual(-1, Bear().UnlockedAmount(TrackReward.FuryGainOnAttack, claimedLevel, fallback: -1));
        }

        [TestCase(14, 0)]
        [TestCase(15, 25)]
        [TestCase(25, 25)]
        [TestCase(26, 50)]
        public void BjornsFuryOpeningIsTheHighestCollectedNotTheSum(int claimedLevel, int expected)
        {
            Assert.AreEqual(expected, Bear().UnlockedAmount(TrackReward.FuryStartOfFight, claimedLevel, fallback: 0));
        }

        // THE ACCEPTING CASE FOR RewardTrackEntryResolver's RULE 6, proven
        // against the REAL content rather than a fixture standing in for it:
        // Bjorn's `fury` pool authors startRule Zero (ContentData/pools.json)
        // and both of his FuryStartOfFight nodes author a positive amount
        // (25, 50 above), so the real bear track only resolves at all if
        // PrimaryPoolStartsZero reached RewardTrackEntryResolver correctly.
        // Tracks() already asserts the resolve succeeded (its own
        // Assert.IsTrue) -- this names the specific entries that prove it,
        // rather than leaving rule 6's coverage implicit in every other test
        // that happens to call Bear().
        [Test]
        public void BjornsFuryStartOfFightNodesResolveBecauseFuryStartsAtZero()
        {
            var bear = Bear();
            Assert.AreEqual(TrackReward.FuryStartOfFight, bear.At(15).Reward);
            Assert.AreEqual(25, bear.At(15).Amount);
            Assert.AreEqual(TrackReward.FuryStartOfFight, bear.At(26).Reward);
            Assert.AreEqual(50, bear.At(26).Amount);
        }

        // SLAM'S TWO +5 NODES DO SUM, unlike the Fury pair above -- base 27
        // to 32 at level 12 and to 37 at level 24, §5's own numbers.
        [TestCase(11, 0)]
        [TestCase(12, 5)]
        [TestCase(24, 10)]
        [TestCase(40, 10)]
        public void BjornsSlamFlatDeltaSums(int claimedLevel, int expected)
        {
            Assert.AreEqual(expected, Bear().CollectedSkillFlatDelta("placeholder_brawler_slam", claimedLevel));
        }

        // ---- Odette, level by level -- §4's third column -------------------

        [Test]
        public void OdettesTrackIsTheAuthoredTable()
        {
            var t = Owl();

            AssertEntry(t, 2, TrackReward.MaxHealth, 30);
            AssertEntry(t, 3, TrackReward.UnlockSkill, 0, skillId: "frost_flare");
            AssertEntry(t, 4, TrackReward.StatPoint, 4);
            AssertEntry(t, 5, TrackReward.MaxMana, 6);
            AssertEntry(t, 6, TrackReward.StatPoint, 4);
            AssertEntry(t, 7, TrackReward.SpellDamagePercent, 5);
            AssertEntry(t, 8, TrackReward.Respec, 0);
            AssertEntry(t, 9, TrackReward.StatPoint, 4);
            AssertEntry(t, 10, TrackReward.UnlockSkill, 0, skillId: "lightning_bolt");
            AssertEntry(t, 11, TrackReward.StatPoint, 4);
            AssertEntry(t, 12, TrackReward.ManaRegen, 1);
            AssertEntry(t, 13, TrackReward.StatPoint, 4);
            AssertEntry(t, 14, TrackReward.MaxHealth, 30);
            AssertEntry(t, 15, TrackReward.SpellCostDelta, 1);
            AssertEntry(t, 16, TrackReward.StatPoint, 4);
            AssertEntry(t, 17, TrackReward.MaxMana, 6);
            AssertEntry(t, 18, TrackReward.StatPoint, 4);
            AssertEntry(t, 19, TrackReward.SpellDamagePercent, 5);
            AssertEntry(t, 20, TrackReward.UnlockSkill, 0, skillId: "mend");
            AssertEntry(t, 21, TrackReward.StatPoint, 4);
            AssertEntry(t, 22, TrackReward.MaxHealth, 30);
            AssertEntry(t, 23, TrackReward.StatPoint, 4);
            AssertEntry(t, 24, TrackReward.ManaRegen, 1);
            AssertEntry(t, 25, TrackReward.SecondLife, 1);
            AssertEntry(t, 26, TrackReward.SpellCostDelta, 2);
            AssertEntry(t, 27, TrackReward.StatPoint, 4);
            AssertEntry(t, 28, TrackReward.SpellDamagePercent, 5);
            AssertEntry(t, 29, TrackReward.StatPoint, 4);
            AssertEntry(t, 30, TrackReward.UnlockSkill, 0, skillId: "prism_ward");
        }

        [TestCase(14, 0)]
        [TestCase(15, 1)]
        [TestCase(25, 1)]
        [TestCase(26, 2)]
        public void OdettesSpellDiscountIsTheHighestCollectedNotTheSum(int claimedLevel, int expected)
        {
            Assert.AreEqual(expected, Owl().UnlockedAmount(TrackReward.SpellCostDelta, claimedLevel, fallback: 0));
        }

        // ---- the identity stretch, 31 to 40 --------------------------------

        // The same ten payloads on every track, differing only in the two
        // titles §4 gives each character at 31 and 33. Walked rather than
        // sampled, because "everything above 30 is Identity" is the rule
        // RewardTrackNodeValidation enforces and this is what it is
        // enforcing against.
        [TestCase("sheep", "Contractor", "Reaver", 0)]
        // Bjorn also holds two cosmetic Title placeholders below 30 (levels 3
        // and 20) where the node-kind rule left no numeric node to place.
        [TestCase("bear", "Bruiser", "Warden", 2)]
        [TestCase("owl", "Scholar", "Adept", 0)]
        public void TheIdentityStretchIsTheAuthoredTable(string characterId, string firstTitle, string secondTitle,
            int earlyTitles)
        {
            var track = Tracks()[characterId];

            AssertIdentity(track, 31, TrackIdentityKind.Title, firstTitle);
            AssertIdentity(track, 32, TrackIdentityKind.PlateRim, "silver");
            AssertIdentity(track, 33, TrackIdentityKind.Title, secondTitle);
            AssertIdentity(track, 34, TrackIdentityKind.PortraitFrame, "");
            AssertIdentity(track, 35, TrackIdentityKind.PlateEmboss, "silver");
            AssertIdentity(track, 36, TrackIdentityKind.Title, "Veteran");
            AssertIdentity(track, 37, TrackIdentityKind.VictoryPose, "");
            AssertIdentity(track, 38, TrackIdentityKind.PlateRim, "gold");
            AssertIdentity(track, 39, TrackIdentityKind.Title, "Legend");
            AssertIdentity(track, 40, TrackIdentityKind.Mastery, "");

            Assert.AreEqual(10 + earlyTitles, track.CollectedIdentity(RewardTrack.MaxLevel).Count);
        }

        private static void AssertIdentity(RewardTrackDefinition track, int level, TrackIdentityKind kind, string value)
        {
            var entry = track.At(level);
            Assert.AreEqual(TrackReward.Identity, entry.Reward, $"{track.CharacterId} level {level}");
            Assert.AreEqual(kind, entry.IdentityKind, $"{track.CharacterId} level {level} kind");
            Assert.AreEqual(value, entry.IdentityValue, $"{track.CharacterId} level {level} value");
        }

        // ---- the spine, identical on every track ---------------------------

        [TestCase("sheep")]
        [TestCase("bear")]
        [TestCase("owl")]
        public void TheSpineIsIdenticalOnEveryTrack(string characterId)
        {
            var track = Tracks()[characterId];

            Assert.AreEqual(TrackReward.Respec, track.At(8).Reward, $"{characterId} level 8");
            Assert.AreEqual(TrackReward.SecondLife, track.At(25).Reward, $"{characterId} level 25");
            Assert.AreEqual(1, track.At(25).Amount, $"{characterId} level 25 charge count");

            Assert.AreEqual(8, track.UnlockLevel(TrackReward.Respec), $"{characterId} respec level");
            Assert.AreEqual(25, track.UnlockLevel(TrackReward.SecondLife), $"{characterId} second life level");
        }

        // FORTY-FOUR STAT POINTS on every track -- eleven Choice nodes at 4,
        // and §4's own "Stat points: 44 (today 52)". The count matters as
        // much as the total: a track paying 44 across nine nodes would be a
        // different pacing decision wearing the same number.
        [TestCase("sheep")]
        [TestCase("bear")]
        [TestCase("owl")]
        public void EveryTrackPaysFortyFourStatPointsAcrossElevenChoices(string characterId)
        {
            var track = Tracks()[characterId];

            Assert.AreEqual(44, track.GrantedBetween(TrackReward.StatPoint, RewardTrack.StartingLevel, RewardTrack.MaxLevel),
                $"{characterId} total stat points");
            Assert.AreEqual(11, track.AllLevels().Count(pair => pair.Entry.Reward == TrackReward.StatPoint),
                $"{characterId} Choice nodes");
        }

        // ---- fully-collected totals ----------------------------------------

        [Test]
        public void ShawnsFullyCollectedTotals()
        {
            var track = Sheep();

            Assert.AreEqual(120, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel), "max health, 40 at 2/14/22");
            Assert.AreEqual(1, track.CollectedTotal(TrackReward.SignatureGainPerTurn, RewardTrack.MaxLevel), "wool per turn, 1 to 2");
            Assert.AreEqual(2, track.CollectedTotal(TrackReward.SignatureGainOnDamageTaken, RewardTrack.MaxLevel), "wool when hurt");
            Assert.AreEqual(15, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Nature, RewardTrack.MaxLevel), "Nature damage, 5 at 7/17/28");
            Assert.AreEqual(1, track.CollectedSkillCostDelta("shear", TrackResourceTarget.Signature, RewardTrack.MaxLevel), "Shear 3 to 2 Wool");
            Assert.AreEqual(2, track.CollectedSkillCostDelta("battering_ram", TrackResourceTarget.Signature, RewardTrack.MaxLevel), "Battering Ram 6 to 4 Wool");
            Assert.AreEqual(3, track.CollectedSkillPowerDelta("tuck_in", RewardTrack.MaxLevel), "Tuck In shields 8 points per Wool rather than 5 -- 32 at a full four, against 20");
        }

        [Test]
        public void BjornsFullyCollectedTotals()
        {
            var track = Bear();

            Assert.AreEqual(200, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel), "max health, 50 at 2/5/14/22");
            Assert.AreEqual(30, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Physical, RewardTrack.MaxLevel), "Physical damage, 5 at 7/10/17/19/28/30");
            Assert.AreEqual(10, track.CollectedSkillFlatDelta("placeholder_brawler_slam", RewardTrack.MaxLevel), "Slam base 27 to 37");
        }

        [Test]
        public void OdettesFullyCollectedTotals()
        {
            var track = Owl();

            Assert.AreEqual(90, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel), "max health, 30 at 2/14/22");
            Assert.AreEqual(12, track.CollectedTotal(TrackReward.MaxMana, RewardTrack.MaxLevel), "max mana, 6 at 5 and 17 -- 30 base to 42");
            Assert.AreEqual(2, track.CollectedTotal(TrackReward.ManaRegen, RewardTrack.MaxLevel), "mana regen, 1 at 12 and 24");
            Assert.AreEqual(15, track.CollectedTotal(TrackReward.SpellDamagePercent, RewardTrack.MaxLevel), "spell damage, 5 at 7/19/28");
        }

        // ---- what each track teaches, and when -----------------------------

        [TestCase("sheep", new[] { "woolgathering", "battering_ram", "tuck_in", "cinderfault", "placeholder_shawn_capstone" })]
        [TestCase("bear", new string[0])]
        [TestCase("owl", new[] { "frost_flare", "lightning_bolt", "mend", "prism_ward" })]
        public void ATrackTeachesExactlyTheseSkillsInThisOrder(string characterId, string[] expected)
        {
            CollectionAssert.AreEqual(expected, Tracks()[characterId].SkillsCollected(RewardTrack.MaxLevel));
        }

        // Nothing arrives before its own node -- the whole point of a
        // COLLECTED unlock. Asserted at the level below each of the four
        // ability nodes rather than only at the top, because a track read off
        // `level` instead of `claimedTrackLevel` would still pass a
        // fully-collected check.
        [TestCase("sheep", 2)]
        [TestCase("sheep", 9)]
        [TestCase("owl", 2)]
        public void NothingIsTaughtBeforeItsOwnNode(string characterId, int claimedLevel)
        {
            var track = Tracks()[characterId];
            var collected = track.SkillsCollected(claimedLevel);
            var all = track.SkillsCollected(RewardTrack.MaxLevel);

            CollectionAssert.IsSubsetOf(collected, all);
            Assert.Less(collected.Count, all.Count,
                $"{characterId} at claimed level {claimedLevel} already knows everything the track teaches");
        }

        // ---- captions, one per new reward kind -----------------------------
        //
        // A caption is the only thing the player ever reads off a node, and
        // four of these kinds are new enough that nothing else has ever
        // rendered one. Read through RewardTrackNames off the RESOLVED entry,
        // so the baked SkillDisplayName/ResourceDisplayName the resolver
        // attaches are exercised rather than the blank fallbacks.
        [Test]
        public void TheNewRewardKindsCaptionThemselves()
        {
            Assert.AreEqual("+1 WOOL PER TURN", RewardTrackNames.Of(Sheep().At(5)));
            Assert.AreEqual("+2 WOOL WHEN HURT", RewardTrackNames.Of(Sheep().At(19)));
            Assert.AreEqual("SHEAR COSTS 1 LESS", RewardTrackNames.Of(Sheep().At(12)));
            Assert.AreEqual("TUCK IN +3 PER POINT", RewardTrackNames.Of(Sheep().At(26)));
            Assert.AreEqual("LEARN CINDERFAULT", RewardTrackNames.Of(Sheep().At(20)));
            Assert.AreEqual("TITLE: CONTRACTOR", RewardTrackNames.Of(Sheep().At(31)));

                        Assert.AreEqual("OPENS AT 25 FURY", RewardTrackNames.Of(Bear().At(15)));
            Assert.AreEqual("SLAM +5", RewardTrackNames.Of(Bear().At(12)));

            Assert.AreEqual("+5% SPELL DAMAGE", RewardTrackNames.Of(Owl().At(7)));
            Assert.AreEqual("SPELLS COST 1 LESS", RewardTrackNames.Of(Owl().At(15)));
            Assert.AreEqual("+6 MAX MANA", RewardTrackNames.Of(Owl().At(5)));
            Assert.AreEqual("PLATE RIM, GOLD", RewardTrackNames.Of(Owl().At(38)));
        }

        // ---- the rules Bjorn's track has to be authored inside --------------
        //
        // Kept from the pre-phase-4 version of this file, because each one is
        // a rule that would otherwise be rediscovered by a build failure
        // months from now rather than read here.
        [Test]
        public void BjornsTrackPaysNothingHisResourceCannotReceive()
        {
            var track = Bear();
            var forbidden = new[]
            {
                TrackReward.SignatureCapacity,
                TrackReward.SignatureGainPerTurn,
                TrackReward.SignatureGainOnDamageTaken,
                TrackReward.SignatureAbsorbPerPoint,
                TrackReward.MaxMana,
                TrackReward.ManaRegen,
            };

            foreach (var (level, entry) in track.AllLevels())
            {
                CollectionAssert.DoesNotContain(forbidden, entry.Reward,
                    $"bear level {level}: Bjorn has no signature and spends `fury`, not mana.");
            }
        }

        // RULE 7's WIRING (AUDIT #145 B9): BuildAll reads the flag the rule
        // turns on off the owner's real primary pool. Bjorn's `fury` is
        // Fixed, so a MaxMana/ManaRegen node on his track would pay nothing
        // and must be refused; Shawn's pool takes outside bonuses.
        [Test]
        public void BuildAllMarksAFixedCapacityPrimaryPoolAsTakingNoManaBonuses()
        {
            var contexts = RewardTrackCharacterContext.BuildAll(Pools(), Characters(), Skills());

            Assert.IsFalse(contexts["bear"].PrimaryPoolTakesManaBonuses,
                "bear's fury is capacityRule Fixed, which PoolPrecedence gives no outside bonus");
            Assert.IsTrue(contexts["sheep"].PrimaryPoolTakesManaBonuses);
            Assert.IsTrue(contexts["owl"].PrimaryPoolTakesManaBonuses);
        }

        // Every level pays something -- rule 1 of the resolver, asserted
        // against the shipped content rather than only against a fixture.
        [TestCase("sheep")]
        [TestCase("bear")]
        [TestCase("owl")]
        public void NoLevelOfAShippedTrackPaysNothing(string characterId)
        {
            foreach (var (level, entry) in Tracks()[characterId].AllLevels())
            {
                Assert.IsTrue(entry.IsSomething, $"{characterId} level {level} pays nothing");
            }
        }
    }
}
