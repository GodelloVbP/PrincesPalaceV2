using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // PHASE 3's seven new reward kinds and the node-kind validator (docs/
    // handoffs/progression_v2/PLAN_PROGRESSION_V2.md §7 phase 3), pinned at
    // the Domain layer -- the only layer runnable from this worktree (see
    // tools/test.ps1's own refusal of Unity-hosted classes from a linked
    // worktree). ContentDatabase.BuildPrimaryPool/BuildSignatureResource and
    // FightEncounterAdapter.KitFor are Core-only and cannot be exercised
    // here; every test below either pins the Domain arithmetic those methods
    // read (RewardTrackDefinition/RewardTrack), or reconstructs their glue
    // with the SAME Domain types (ResourcePool, ResolvedSkill) to prove the
    // mechanism composes the way ContentDatabase's own comments say it does.
    // The glue itself (the two-line "if override >= 0, assign it" in
    // BuildPrimaryPool/BuildSignatureResource) is reviewed by eye, not
    // executed here.
    public class RewardTrackPhase3Tests
    {
        // ---- FuryGainOnAttack / FuryStartOfFight: SET, not summed ----

        [Test]
        public void FuryGainOnAttack_LatestCollectedNodeWins()
        {
            var entries = new (int Level, TrackEntry Entry)[]
            {
                (5, new TrackEntry(TrackReward.FuryGainOnAttack, 20)),
                (15, new TrackEntry(TrackReward.FuryGainOnAttack, 25)),
            };
            var track = RewardTrackDefinition.Build("bear", entries);

            // Before either node: no override collected.
            Assert.AreEqual(-1, track.UnlockedAmount(TrackReward.FuryGainOnAttack, 4, fallback: -1));
            // After the first: 20, not summed with nothing.
            Assert.AreEqual(20, track.UnlockedAmount(TrackReward.FuryGainOnAttack, 5, fallback: -1));
            // After both: 25, the LATEST value -- not 20 + 25 = 45.
            Assert.AreEqual(25, track.UnlockedAmount(TrackReward.FuryGainOnAttack, 15, fallback: -1));
        }

        [Test]
        public void FuryGainOnAttack_25_SetsThePoolsGainOnAttack()
        {
            // Mirrors ContentDatabase.BuildPrimaryPool's own two lines:
            // build the pool from the definition, then overwrite GainOnAttack
            // with the highest collected amount, exactly as that method does.
            var fury = FuryPoolFixture();
            var pool = new ResourcePool(fury, capacity: 100, gainPerTurn: 0);
            Assert.AreEqual(15, pool.GainOnAttack, "the authored base, before any override");

            var track = RewardTrackDefinition.Build("bear",
                new (int, TrackEntry)[] { (5, new TrackEntry(TrackReward.FuryGainOnAttack, 25)) });

            int overrideAmount = track.UnlockedAmount(TrackReward.FuryGainOnAttack, level: 5, fallback: -1);
            if (overrideAmount >= 0) pool.GainOnAttack = overrideAmount;

            Assert.AreEqual(25, pool.GainOnAttack);
        }

        [Test]
        public void FuryStartOfFight_50_SetsThePoolsOpeningValue()
        {
            var fury = FuryPoolFixture(); // startRule Zero, startValue 0
            var pool = new ResourcePool(fury, capacity: 100, gainPerTurn: 0);
            Assert.AreEqual(0, pool.Current, "Zero-start, before any override");

            var track = RewardTrackDefinition.Build("bear",
                new (int, TrackEntry)[] { (15, new TrackEntry(TrackReward.FuryStartOfFight, 50)) });

            int overrideAmount = track.UnlockedAmount(TrackReward.FuryStartOfFight, level: 15, fallback: -1);
            if (overrideAmount >= 0) pool.Current = System.Math.Min(pool.Max, overrideAmount);

            Assert.AreEqual(50, pool.Current);
        }

        private static ResolvedPool FuryPoolFixture() => new ResolvedPool(
            "fury", "Fury", "FURY", PoolCapacityRule.Fixed, 100,
            gainPerTurn: 0, gainOnAttack: 15, gainOnDamageTaken: 10,
            decayPerIdleTurn: 10, decayUnless: PoolDecayTrigger.Damage,
            startRule: PoolStartRule.Zero, startValue: 0,
            brightHex: "#FF8A3A", deepHex: "#8E3A12", textHex: "#FFD2B0",
            pulse: true, allowsSpellBooks: false, restoredByManaEffects: false, absorbsDamage: false,
            sortOrder: 0);

        // ---- SpellCostDelta / SkillCostDelta / SkillFlatDelta, applied to a ResolvedSkill ----

        [Test]
        public void SpellCostDelta_1_MakesAnEightCostOrbCostSeven_AndAOneCostSkillStayAtOne()
        {
            var orb = SkillFixture("prismatic_orb", manaCost: 8, resourceCost: 0, flatAmount: 0);
            var cheap = SkillFixture("cheap_cast", manaCost: 1, resourceCost: 0, flatAmount: 0);

            // Mirrors ContentDatabase.ApplyRewardTrackSkillDeltas: SpellCostDelta
            // applies only when the skill's own manaCost is > 0.
            var discountedOrb = orb.ManaCost > 0 ? orb.WithTrackDeltas(manaCostDelta: 1, resourceCostDelta: 0, flatAmountDelta: 0) : orb;
            var discountedCheap = cheap.ManaCost > 0 ? cheap.WithTrackDeltas(manaCostDelta: 1, resourceCostDelta: 0, flatAmountDelta: 0) : cheap;

            Assert.AreEqual(7, discountedOrb.ManaCost);
            Assert.AreEqual(1, discountedCheap.ManaCost, "floored at 1, never free");
        }

        [Test]
        public void SkillCostDelta_1_MakesShearCostTwoWool()
        {
            var shear = SkillFixture("shear", manaCost: 0, resourceCost: 3, flatAmount: 0);

            var discounted = shear.WithTrackDeltas(manaCostDelta: 0, resourceCostDelta: 1, flatAmountDelta: 0);

            Assert.AreEqual(2, discounted.ResourceCost);
            Assert.AreEqual(0, discounted.ManaCost, "Shear never costs mana -- only its Wool cost moved");
        }

        [Test]
        public void SkillCostDelta_NeverDropsAPositiveCostBelowOne()
        {
            var shear = SkillFixture("shear", manaCost: 0, resourceCost: 3, flatAmount: 0);

            var discounted = shear.WithTrackDeltas(manaCostDelta: 0, resourceCostDelta: 10, flatAmountDelta: 0);

            Assert.AreEqual(1, discounted.ResourceCost);
        }

        [Test]
        public void SkillFlatDelta_5_MakesSlamsFlatAmount22_TwoNodesMake27()
        {
            // The current authored base (skills.json's placeholder_brawler_slam),
            // not the plan's own illustrative "12" -- see the phase-3 report
            // for why that number was corrected against the live content.
            var slam = SkillFixture("placeholder_brawler_slam", manaCost: 0, resourceCost: 0, flatAmount: 17);

            var oneNode = slam.WithTrackDeltas(manaCostDelta: 0, resourceCostDelta: 0, flatAmountDelta: 5);
            Assert.AreEqual(22, oneNode.FlatAmount);

            // Two SkillFlatDelta nodes SUM (RewardTrackDefinition.
            // CollectedSkillFlatDelta), unlike FuryGainOnAttack's SET --
            // mirrored here by applying the summed delta once, the same
            // shape ApplyRewardTrackSkillDeltas computes it in.
            var track = RewardTrackDefinition.Build("bear",
                new (int, TrackEntry)[]
                {
                    (12, new TrackEntry(TrackReward.SkillFlatDelta, 5, skillId: "placeholder_brawler_slam")),
                    (24, new TrackEntry(TrackReward.SkillFlatDelta, 5, skillId: "placeholder_brawler_slam")),
                });

            int summedDelta = track.CollectedSkillFlatDelta("placeholder_brawler_slam", claimedLevel: 24);
            Assert.AreEqual(10, summedDelta);

            var twoNodes = slam.WithTrackDeltas(0, 0, summedDelta);
            Assert.AreEqual(27, twoNodes.FlatAmount);
        }

        [Test]
        public void CollectedSkillCostDelta_SumsOnlyTheNamedSkillAndResource()
        {
            var track = RewardTrackDefinition.Build("sheep",
                new (int, TrackEntry)[]
                {
                    (12, new TrackEntry(TrackReward.SkillCostDelta, 1, skillId: "shear", resource: TrackResourceTarget.Signature)),
                    (24, new TrackEntry(TrackReward.SkillCostDelta, 2, skillId: "battering_ram", resource: TrackResourceTarget.Signature)),
                });

            Assert.AreEqual(1, track.CollectedSkillCostDelta("shear", TrackResourceTarget.Signature, 30));
            Assert.AreEqual(2, track.CollectedSkillCostDelta("battering_ram", TrackResourceTarget.Signature, 30));
            Assert.AreEqual(0, track.CollectedSkillCostDelta("shear", TrackResourceTarget.Mana, 30),
                "shear was never discounted on Mana");
        }

        // ---- SignatureAbsorbPerPoint ----

        [Test]
        public void SignatureAbsorbPerPoint_2_Absorbs2DamagePerWool()
        {
            var sheep = ResourcePoolFixture(absorbsDamage: true, absorbPerPoint: 2);

            int absorbed = sheep.Absorb(20);

            // Absorb(amount) = min(Current * AbsorbPerPoint, amount); a full
            // 16-point bank at 2/point covers up to 32, so all 20 lands on
            // the meter and none reaches health.
            Assert.AreEqual(20, absorbed);
            Assert.AreEqual(6, sheep.Current, "10 of the 16 points spent covering 20 damage at 2 each, rounded up");
        }

        [Test]
        public void SignatureAbsorbPerPoint_UnlockedAmount_ReadsTheHighestCollected()
        {
            var track = RewardTrackDefinition.Build("sheep",
                new (int, TrackEntry)[]
                {
                    (15, new TrackEntry(TrackReward.SignatureAbsorbPerPoint, 1)),
                    (26, new TrackEntry(TrackReward.SignatureAbsorbPerPoint, 2)),
                });

            Assert.AreEqual(0, track.UnlockedAmount(TrackReward.SignatureAbsorbPerPoint, 14, fallback: 0));
            Assert.AreEqual(1, track.UnlockedAmount(TrackReward.SignatureAbsorbPerPoint, 15, fallback: 0));
            Assert.AreEqual(2, track.UnlockedAmount(TrackReward.SignatureAbsorbPerPoint, 26, fallback: 0),
                "the later node's own value, not 1 + 2");
        }

        private static ResourcePool ResourcePoolFixture(bool absorbsDamage, int absorbPerPoint)
        {
            var pool = new ResourcePool("wool", "Wool", max: 16, gainPerTurn: 1, gainOnAttack: 0,
                gainOnDamageTaken: 0, absorbPerPoint: absorbPerPoint, absorbsDamage: absorbsDamage);
            pool.Current = 16;
            return pool;
        }

        // ---- Identity: collects and selects titles ----

        [Test]
        public void CollectedIdentity_ReturnsEveryEntryOldestFirst()
        {
            var track = RewardTrackDefinition.Build("sheep",
                new (int, TrackEntry)[]
                {
                    (31, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Contractor")),
                    (33, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Reaver")),
                    (32, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.PlateRim, identityValue: "silver")),
                });

            var collected = track.CollectedIdentity(40);

            Assert.AreEqual(3, collected.Count);
            Assert.AreEqual(31, collected[0].Level);
            Assert.AreEqual(32, collected[1].Level);
            Assert.AreEqual(33, collected[2].Level);

            // "the newest is shown" -- the last Title in level order.
            var titles = new List<(int Level, TrackEntry Entry)>();
            foreach (var item in collected)
            {
                if (item.Entry.Reward == TrackReward.Identity && item.Entry.IdentityKind == TrackIdentityKind.Title)
                {
                    titles.Add(item);
                }
            }

            Assert.AreEqual("Reaver", titles[titles.Count - 1].Entry.IdentityValue);
        }

        [Test]
        public void CollectedIdentity_StopsAtTheWatermark()
        {
            var track = RewardTrackDefinition.Build("sheep",
                new (int, TrackEntry)[]
                {
                    (31, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Contractor")),
                    (33, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Reaver")),
                });

            Assert.AreEqual(1, track.CollectedIdentity(31).Count);
            Assert.AreEqual(0, track.CollectedIdentity(30).Count);
        }

        private static ResolvedSkill SkillFixture(string id, int manaCost, int resourceCost, int flatAmount) =>
            new ResolvedSkill(id, id, "", "sheep", 1, SkillEffect.DamageSingle, SkillTargeting.SingleEnemy,
                manaCost, resourceCost, spendsAllResource: false, power: 0, flatAmount: flatAmount,
                ignoresDefense: false, damageInstances: System.Array.Empty<DamageInstance>(),
                presentation: null, sortOrder: 0);
    }
}
