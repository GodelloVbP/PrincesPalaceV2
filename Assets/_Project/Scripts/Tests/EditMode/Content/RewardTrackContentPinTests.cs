using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Reads Assets/_Project/ContentData/{characters,skills,reward_tracks}.json
    // from disk through the REAL resolvers -- CharacterEntryResolver,
    // SkillEntryResolver, RewardTrackEntryResolver -- and materialises the
    // result with RewardTrackDefinition.From, the same shape a live save
    // would read. Pins the literal numbers docs/PLAN_REWARD_TRACKS.md §5 and
    // §10 P6 computed for Shawn's and Odette's shipped tracks, so a retune
    // that silently changes what a level pays is caught here rather than
    // only by eye on the screen.
    //
    // TWO PARSERS, ONE ASSERTION, same pattern as ContentStampIdsTests: this
    // class has no UnityEngine dependency, so it is a [D] class and runs
    // under plain `dotnet test`, which has no JsonUtility. RepoRoot/DataPath/
    // ParseFile live on Shared/ContentDataFiles -- ContentStampIdsTests uses
    // the same trio.
    public class RewardTrackContentPinTests
    {
        // The real pools.json through the real resolver, because this file
        // reads the real characters.json -- the pool ids a character's
        // primaryPoolId is checked against have to be the ones a build would
        // actually produce, the same way ContentBuilder resolves pools first
        // and hands the ids down.
        private static List<string> PoolIds()
        {
            var raw = ContentDataFiles.ParseFile<RawPoolFile>(ContentDataFiles.DataPath("pools.json")).pools;
            bool ok = PoolEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "pools.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved.Select(pool => pool.Id).ToList();
        }

        private static List<ResolvedCharacter> Characters()
        {
            var raw = ContentDataFiles.ParseFile<RawCharacterFile>(ContentDataFiles.DataPath("characters.json")).characters;
            bool ok = CharacterEntryResolver.TryResolveAll(raw, PoolIds(), out var resolved, out var errors);
            Assert.IsTrue(ok, "characters.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static List<ResolvedSkill> Skills()
        {
            var raw = ContentDataFiles.ParseFile<RawSkillFile>(ContentDataFiles.DataPath("skills.json")).skills;
            bool ok = SkillEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "skills.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        // The same assembly ContentBuilder.BuildRewardTracks uses --
        // RewardTrackCharacterContext.BuildAll is the one seam both call, so
        // this pin reads the tracks through the exact context a real build
        // resolves them against rather than a hand-rolled approximation of
        // it that could quietly drift.
        private static Dictionary<string, RewardTrackCharacterContext> Contexts(
            IReadOnlyList<ResolvedCharacter> characters, IReadOnlyList<ResolvedSkill> skills) =>
            RewardTrackCharacterContext.BuildAll(characters, skills);

        // The two shipped tracks, materialised through
        // RewardTrackDefinition.From -- what every assertion below reads.
        private static Dictionary<string, RewardTrackDefinition> Tracks()
        {
            var characters = Characters();
            var skills = Skills();
            var contexts = Contexts(characters, skills);

            var raw = ContentDataFiles.ParseFile<RawRewardTrackFile>(ContentDataFiles.DataPath("reward_tracks.json")).tracks;
            bool ok = RewardTrackEntryResolver.TryResolveAll(raw, contexts, out var resolved, out var errors);
            Assert.IsTrue(ok, "reward_tracks.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));

            return resolved.ToDictionary(t => t.CharacterId, t => RewardTrackDefinition.From(t));
        }

        private static RewardTrackDefinition Sheep() => Tracks()["sheep"];
        private static RewardTrackDefinition Owl() => Tracks()["owl"];

        // ---- sheep's four content-specific milestones, §5 ----

        [Test]
        public void SheepLevel25IsFiveSignatureCapacity()
        {
            var entry = Sheep().At(25);
            Assert.AreEqual(TrackReward.SignatureCapacity, entry.Reward);
            Assert.AreEqual(5, entry.Amount);
        }

        [Test]
        public void SheepLevel30UnlocksStaticFleece()
        {
            var entry = Sheep().At(30);
            Assert.AreEqual(TrackReward.UnlockSkill, entry.Reward);
            Assert.AreEqual("static_fleece", entry.SkillId);
        }

        [Test]
        public void SheepLevel60IsSignatureAbsorbs()
        {
            Assert.AreEqual(TrackReward.SignatureAbsorbs, Sheep().At(60).Reward);
        }

        [Test]
        public void SheepLevel100IsTenSignatureCapacity()
        {
            var entry = Sheep().At(100);
            Assert.AreEqual(TrackReward.SignatureCapacity, entry.Reward);
            Assert.AreEqual(10, entry.Amount);
        }

        // ---- owl's four content-specific milestones, §5 ----

        [Test]
        public void OwlLevel10UnlocksFrostFlare()
        {
            var entry = Owl().At(10);
            Assert.AreEqual(TrackReward.UnlockSkill, entry.Reward);
            Assert.AreEqual("frost_flare", entry.SkillId);
        }

        [Test]
        public void OwlLevel40IsTenPercentFireDamage()
        {
            var entry = Owl().At(40);
            Assert.AreEqual(TrackReward.ElementalDamagePercent, entry.Reward);
            Assert.AreEqual(DamageType.Fire, entry.Against);
            Assert.AreEqual(10, entry.Amount);
        }

        // The authoring hazard §5 names by name: frost_flare's own
        // skills.json entry authors "Frost", an alias only
        // SkillEntryResolver.TryParseDamageType accepts; reward_tracks.json's
        // resolver does not, so the correct spelling here is "Ice", never
        // "Frost". A copy-paste of the alias would fail to resolve at all
        // (Enum.TryParse<DamageType> refuses "Frost"), which is what makes
        // this pin meaningful rather than redundant with the two above.
        [Test]
        public void OwlLevel45IsFifteenPercentIceDamage()
        {
            var entry = Owl().At(45);
            Assert.AreEqual(TrackReward.ElementalDamagePercent, entry.Reward);
            Assert.AreEqual(DamageType.Ice, entry.Against);
            Assert.AreEqual(15, entry.Amount);
        }

        [Test]
        public void OwlLevel70IsTwentyPercentLightningDamage()
        {
            var entry = Owl().At(70);
            Assert.AreEqual(TrackReward.ElementalDamagePercent, entry.Reward);
            Assert.AreEqual(DamageType.Lightning, entry.Against);
            Assert.AreEqual(20, entry.Amount);
        }

        // ---- the spine, identical on both tracks, §5 "the spine stays identical" ----

        [TestCase("sheep")]
        [TestCase("owl")]
        public void TheSpineIsIdenticalOnBothTracks(string characterId)
        {
            var track = Tracks()[characterId];

            Assert.AreEqual(TrackReward.Respec, track.At(20).Reward, $"{characterId} level 20");

            var eighty = track.At(80);
            Assert.AreEqual(TrackReward.StatPoint, eighty.Reward, $"{characterId} level 80");
            Assert.AreEqual(10, eighty.Amount, $"{characterId} level 80");

            var ninety = track.At(90);
            Assert.AreEqual(TrackReward.SecondLife, ninety.Reward, $"{characterId} level 90");
            Assert.AreEqual(1, ninety.Amount, $"{characterId} level 90");
        }

        // ---- fully-collected totals, §5 ----

        [Test]
        public void SheepsFullyCollectedTotals()
        {
            var track = Sheep();

            Assert.AreEqual(27, track.CollectedTotal(TrackReward.SignatureCapacity, RewardTrack.MaxLevel), "wool capacity");
            Assert.AreEqual(2, track.CollectedTotal(TrackReward.SignatureGainPerTurn, RewardTrack.MaxLevel), "wool per turn");
            Assert.AreEqual(2, track.CollectedTotal(TrackReward.SignatureGainOnDamageTaken, RewardTrack.MaxLevel), "wool when hurt");
            Assert.AreEqual(30, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Nature, RewardTrack.MaxLevel), "Nature damage");
            Assert.AreEqual(150, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel), "max health");
            Assert.AreEqual(50, track.GrantedBetween(TrackReward.StatPoint, RewardTrack.StartingLevel, RewardTrack.MaxLevel), "stat points");
        }

        [Test]
        public void OwlsFullyCollectedTotals()
        {
            var track = Owl();

            Assert.AreEqual(28, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Fire, RewardTrack.MaxLevel), "Fire damage");
            Assert.AreEqual(26, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Arcane, RewardTrack.MaxLevel), "Arcane damage");
            Assert.AreEqual(20, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Lightning, RewardTrack.MaxLevel), "Lightning damage");
            Assert.AreEqual(15, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Ice, RewardTrack.MaxLevel), "Ice damage");
            Assert.AreEqual(50, track.CollectedTotal(TrackReward.MaxMana, RewardTrack.MaxLevel), "max mana");
            Assert.AreEqual(3, track.CollectedTotal(TrackReward.ManaRegen, RewardTrack.MaxLevel), "mana regen");
            Assert.AreEqual(150, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel), "max health");
            Assert.AreEqual(50, track.GrantedBetween(TrackReward.StatPoint, RewardTrack.StartingLevel, RewardTrack.MaxLevel), "stat points");
        }

        // ---- level-by-level claims, §5 ----

        // "Wool capacity first reaches 20 at level 39" -- the owner's own
        // "wool capacity upgraded to 20" example. The DISPLAYED number is the
        // character's base SignatureCapacity (characters.json) plus the
        // track's own CollectedTotal, since the base is authored on the
        // roster and is not itself a track entry. Walked level by level
        // against the real resolved definition rather than hand-added, and
        // pinned against the literal computed answer (39) -- not a formula
        // this test recomputes, gotcha 5.
        [Test]
        public void SheepsWoolCapacityFirstReaches20AtLevel39()
        {
            int baseCapacity = Characters().Single(c => c.Id == "sheep").SignatureCapacity;
            var track = Sheep();
            int firstLevelAt20 = 0;

            for (int level = RewardTrack.StartingLevel; level <= RewardTrack.MaxLevel; level++)
            {
                int total = baseCapacity + track.CollectedTotal(TrackReward.SignatureCapacity, level);
                if (total >= 20)
                {
                    firstLevelAt20 = level;
                    break;
                }
            }

            Assert.AreEqual(39, firstLevelAt20,
                "10 base + 10 collected (filler at 6/14/21, the level-25 milestone's 5, filler at 31/39)");
        }

        [Test]
        public void SheepsLevelThreeCaptionIsOnePercentNatureDamage()
        {
            Assert.AreEqual("+1% NATURE DAMAGE", RewardTrackNames.Of(Sheep().At(3)));
        }

        // Shawn's first SignatureCapacity filler (level 6, the same one
        // SheepsWoolCapacityFirstReaches20AtLevel39's comment names as the
        // first of the 6/14/21 run) -- pins that RewardTrackDefinition.From
        // forwards a filler's ResourceDisplayName the same way it forwards a
        // milestone's, so the caption reads "WOOL" rather than falling back
        // to the blank-name default "SIGNATURE".
        [Test]
        public void SheepsLevelSixCaptionIsOneWoolCapacity()
        {
            Assert.AreEqual("+1 WOOL CAPACITY", RewardTrackNames.Of(Sheep().At(6)));
        }

        [Test]
        public void OwlsLevelFourCaptionIsTwoMaxMana()
        {
            Assert.AreEqual("+2 MAX MANA", RewardTrackNames.Of(Owl().At(4)));
        }

        [Test]
        public void OwlsLevelSixCaptionIsTwoPercentFireDamage()
        {
            Assert.AreEqual("+2% FIRE DAMAGE", RewardTrackNames.Of(Owl().At(6)));
        }

        // ---- no bear entry ----
        //
        // The bear-uses-the-default assertion belongs to P4's
        // RewardTracks.For tests (Core), not here -- this only pins that
        // reward_tracks.json authors nothing for it, which is P6's own scope.
        [Test]
        public void BearHasNoAuthoredTrack()
        {
            Assert.IsFalse(Tracks().ContainsKey("bear"));
        }
    }
}
