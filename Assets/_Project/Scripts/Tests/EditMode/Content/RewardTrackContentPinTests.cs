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
    // §10 P6 computed for Shawn's and Odette's shipped tracks -- and the
    // rules Bjorn's later one had to be authored inside -- so a retune
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
        private static RewardTrackDefinition Bear() => Tracks()["bear"];

        // ---- sheep's four content-specific milestones, §5 ----

        [Test]
        public void SheepLevel25IsFiveSignatureCapacity()
        {
            var entry = Sheep().At(25);
            Assert.AreEqual(TrackReward.SignatureCapacity, entry.Reward);
            Assert.AreEqual(5, entry.Amount);
        }

        // static_fleece was removed 2026-09-15 (AUDIT #150 -- it failed
        // docs/SPELL_DESIGN_STANDARD.md's universality test, being a book
        // spell that only ever worked for a Wool-holding owner). Level 30
        // now carries a PLACEHOLDER, not a real milestone: StatPoint 2, just
        // enough to keep the track's every-milestone-filled rule satisfied
        // until the owner picks a real replacement. Pinned as a placeholder
        // on purpose -- re-pin this the moment #150 is resolved, not before.
        [Test]
        public void SheepLevel30IsAPlaceholder_PendingAudit150()
        {
            var entry = Sheep().At(30);
            Assert.AreEqual(TrackReward.StatPoint, entry.Reward);
            Assert.AreEqual(2, entry.Amount);
        }

        // PHASE 3 ALIAS: reward_tracks.json still authors this node as
        // "SignatureAbsorbs" (the old boolean), but RewardTrackEntryResolver
        // now rewrites that to SignatureAbsorbPerPoint at amount 1 the
        // moment it parses the entry -- see TrackReward.
        // SignatureAbsorbPerPoint's own header. Re-pinned to the aliased
        // kind rather than the authored string, since the authored string
        // is no longer what a reader of the resolved track sees.
        [Test]
        public void SheepLevel60IsSignatureAbsorbs()
        {
            var entry = Sheep().At(60);
            Assert.AreEqual(TrackReward.SignatureAbsorbPerPoint, entry.Reward);
            Assert.AreEqual(1, entry.Amount);
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

        // ---- the spine, identical on every track, §5 "the spine stays identical" ----

        [TestCase("sheep")]
        [TestCase("bear")]
        [TestCase("owl")]
        public void TheSpineIsIdenticalOnEveryTrack(string characterId)
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
            // 50 filler + 2 from level 30's placeholder (AUDIT #150, StatPoint
            // 2 standing in for the removed static_fleece unlock) = 52. Re-pin
            // this the moment #150 gets a real replacement.
            Assert.AreEqual(52, track.GrantedBetween(TrackReward.StatPoint, RewardTrack.StartingLevel, RewardTrack.MaxLevel), "stat points");
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

        // ---- bear's track, authored 2026-09-11 on the owner's answer to
        // AUDIT #134 ----
        //
        // Until then reward_tracks.json authored nothing for him and he
        // levelled on RewardTrackDefinition.Default; the test that stood here
        // asserted exactly that absence. What replaces it pins the three
        // things about his track that are NOT free choices, because each one
        // is a rule that would otherwise be rediscovered by a build failure
        // months from now:
        //
        //  - no Signature* reward anywhere on it. He has no signatureId, so
        //    rule 5 refuses one outright -- his resource is the `fury` POOL,
        //    and TrackReward still has nothing that pays into a primary pool
        //    other than mana. That model gap is what AUDIT #134 keeps open
        //    after the authoring half of it was answered.
        //  - no MaxMana and no ManaRegen. Both resolve CLEAN on him and then
        //    pay nothing, because fury's capacityRule is Fixed and it is not
        //    restoredByManaEffects (resolver blind spot B9, AUDIT #145) --
        //    the one failure mode a green build would not have shown.
        //  - no UnlockSkill milestone. All three of his skills author
        //    unlockLevel 1, so there is no skill left for a level to grant.
        //
        // The AMOUNTS are deliberately not pinned beyond the spine and one
        // total: they are first values awaiting the balance pass that also
        // authors what Fury buys, and pinning each one would turn that pass
        // into a test edit.
        [Test]
        public void BearHasAnAuthoredTrack()
        {
            Assert.IsTrue(Tracks().ContainsKey("bear"),
                "reward_tracks.json stopped authoring a track for bear -- he falls back to the generated default.");
        }

        [Test]
        public void BearsTrackPaysNothingHisResourceCannotReceive()
        {
            var track = Bear();
            var forbidden = new[]
            {
                TrackReward.SignatureCapacity,
                TrackReward.SignatureGainPerTurn,
                TrackReward.SignatureGainOnDamageTaken,
                TrackReward.SignatureAbsorbs,
                TrackReward.MaxMana,
                TrackReward.ManaRegen,
            };

            for (int level = RewardTrack.StartingLevel; level <= RewardTrack.MaxLevel; level++)
            {
                var entry = track.At(level);
                CollectionAssert.DoesNotContain(forbidden, entry.Reward,
                    $"bear level {level}: Bjorn has no signature and spends `fury`, not mana.");
            }
        }

        [Test]
        public void BearsTrackGrantsNoSkill()
        {
            var track = Bear();

            for (int level = RewardTrack.StartingLevel; level <= RewardTrack.MaxLevel; level++)
            {
                Assert.AreNotEqual(TrackReward.UnlockSkill, track.At(level).Reward,
                    $"bear level {level}: all three of his skills unlock at level 1, so a grant here pays nothing.");
            }
        }

        // The one place a literal total earns its keep: moving him off the
        // generated default was not meant to change what he is paid by an
        // order of magnitude. The default pays 229 max health and 50 stat
        // points (RewardTrackDefinitionTests pins both); this pays 225 and 50.
        [Test]
        public void BearsFullyCollectedTotals()
        {
            var track = Bear();

            Assert.AreEqual(225, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel),
                "max health -- 6 milestone nodes at 15 plus 27 filler nodes at 5");
            Assert.AreEqual(50, track.GrantedBetween(TrackReward.StatPoint, RewardTrack.StartingLevel, RewardTrack.MaxLevel),
                "stat points -- 40 filler singles plus level 80's ten, the same 50 both other tracks pay");
            Assert.AreEqual(50, track.CollectedTotal(TrackReward.ElementalDamagePercent, DamageType.Physical, RewardTrack.MaxLevel),
                "Physical damage -- three milestones at 10 plus 20 filler nodes at 1");
        }
    }
}
