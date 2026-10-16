using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // The Reckoning's speed rule and the reveal's timed beats, on literals
    // (docs/PLAN_REWARD_SCREENS.md C3, C4, C10). The controller plays the
    // schedule back; PlayMode only checks that it does.
    public class ReckoningTimingTests
    {
        private const float Tolerance = 0.0005f;

        private static readonly RevealCard Common = new RevealCard(1, false);
        // A live Uncommon relic: below the relic line, so it never earns the cache.
        private static readonly RevealCard Uncommon = RevealCard.Relic(2, true);
        private static readonly RevealCard RareLive = new RevealCard(3, true);
        private static readonly RevealCard RareDown = new RevealCard(3, false);
        private static readonly RevealCard TopLive = new RevealCard(4, true);
        private static readonly RevealCard TopDown = new RevealCard(4, false);

        private static RevealSchedule At(float preset, int choices, params RevealCard[] cards) =>
            RevealSchedule.For(cards, choices, preset);

        private static RevealBeat[] Of(RevealSchedule s, RevealBeatKind kind) =>
            s.Beats.Where(b => b.Kind == kind).ToArray();

        // ReckoningTime

        [Test]
        public void TheSpeedRule_FollowsFasterPresetsOnly()
        {
            Assert.AreEqual(1f, ReckoningTime.Scale(0.5f), Tolerance);
            Assert.AreEqual(1f, ReckoningTime.Scale(1f), Tolerance);
            Assert.AreEqual(2f / 3f, ReckoningTime.Scale(1.5f), Tolerance);
            Assert.AreEqual(0.5f, ReckoningTime.Scale(2f), Tolerance);
            Assert.AreEqual(1f, ReckoningTime.Scale(0f), Tolerance, "a non-positive preset reads as the default");
        }

        [Test]
        public void Wall_MultipliesTheTestSeam_AndNeverGoesNegative()
        {
            Assert.AreEqual(0.25f, ReckoningTime.Wall(0.5f, 2f), Tolerance);
            Assert.AreEqual(0.1f, ReckoningTime.Wall(0.5f, 2f, 0.4f), Tolerance);
            Assert.AreEqual(0f, ReckoningTime.Wall(0.5f, 1f, -3f), Tolerance);
        }

        [Test]
        public void Scale_IsPinnedPerPreset_AndTheRunUpUsesIt()
        {
            Assert.AreEqual(1f, ReckoningTime.Scale(0.5f), Tolerance);
            Assert.AreEqual(1f, ReckoningTime.Scale(1f), Tolerance);
            Assert.AreEqual(0.6667f, ReckoningTime.Scale(1.5f), 0.0001f);
            Assert.AreEqual(0.5f, ReckoningTime.Scale(2f), Tolerance);

            // The run-up's lift is authored 0.25 s: its wall time follows.
            Assert.AreEqual(0.25f, RunUpSchedule.For(EncounterClass.Normal, 1f / 3f, 0.5f, 0.35f, 0.6f).LiftSecondsWall, Tolerance);
            Assert.AreEqual(0.125f, RunUpSchedule.For(EncounterClass.Normal, 4f / 3f, 2f, 0.35f, 0.6f).LiftSecondsWall, Tolerance);
        }

        // THE ONE-BY-ONE REVEAL (C4), on the three pages the contract is pinned
        // on. A Common's cadence is flip 0.18 + settle 0.27; the cache stands
        // 0.5 s before its 0.12 s seal; every card slides out face-down in 0.25 s.

        private static readonly RevealCard UncommonDown = new RevealCard(2, false);

        private static void Starts(RevealSchedule s, RevealBeatKind kind, params float[] expected)
        {
            var beats = Of(s, kind);
            Assert.AreEqual(expected.Length, beats.Length, kind + " count");
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], beats[i].Start, Tolerance, kind + " " + i);
        }

        [Test]
        public void ThreeCommons_NoCache_DealFaceDown_ThenFlipOneByOne()
        {
            var s = At(1f, 1, Common, Common, Common);
            Assert.IsFalse(s.HasCache);
            Starts(s, RevealBeatKind.Slide, 0.34f);
            Assert.AreEqual(0.25f, Of(s, RevealBeatKind.Slide)[0].Duration, Tolerance);
            Starts(s, RevealBeatKind.Deal, 0.4233f, 0.5067f, 0.59f);
            Assert.IsEmpty(Of(s, RevealBeatKind.Anticipate), "a Common has no build");
            Starts(s, RevealBeatKind.Flip, 0.59f, 1.04f, 1.49f);
            Assert.AreEqual(0.18f, Of(s, RevealBeatKind.Flip)[0].Duration, Tolerance);
            Starts(s, RevealBeatKind.Land, 0.77f, 1.22f, 1.67f);
            Assert.AreEqual(1.67f, s.ChoosableAt, Tolerance);
            CollectionAssert.AreEqual(new[] { LandStyle.Flat, LandStyle.Flat, LandStyle.Flat },
                Of(s, RevealBeatKind.Land).Select(b => b.Style).ToArray());
            Assert.IsEmpty(Of(s, RevealBeatKind.Seal));
            Assert.IsEmpty(Of(s, RevealBeatKind.Linger));
        }

        [Test]
        public void FiveCards_OneTopLiveUpgrade_BuildsSixTenths_ThenTheCeremony()
        {
            var s = At(1f, 3, Common, Common, Common, Common, TopLive);
            Assert.IsTrue(s.HasCache);
            Assert.IsTrue(s.HasTopLiveUpgrade);
            Starts(s, RevealBeatKind.OpenWait, 0.34f);
            Assert.AreEqual(0.5f, Of(s, RevealBeatKind.OpenWait)[0].Duration, Tolerance, "the cache stands and hums");
            Starts(s, RevealBeatKind.Seal, 0.84f);
            Starts(s, RevealBeatKind.Slide, 0.96f);
            Assert.IsEmpty(Of(s, RevealBeatKind.Deal), "a cache page deals nothing");
            Starts(s, RevealBeatKind.Anticipate, 3.01f);
            Assert.AreEqual(0.6f, Of(s, RevealBeatKind.Anticipate)[0].Duration, Tolerance);
            Assert.AreEqual(LandStyle.Burst, Of(s, RevealBeatKind.Anticipate)[0].Style);
            Assert.AreEqual(4, Of(s, RevealBeatKind.Anticipate)[0].Card);
            Starts(s, RevealBeatKind.Flip, 1.21f, 1.66f, 2.11f, 2.56f, 3.61f);
            Starts(s, RevealBeatKind.Land, 1.39f, 1.84f, 2.29f, 2.74f, 3.79f);
            Assert.AreEqual(LandStyle.Burst, Of(s, RevealBeatKind.Land)[4].Style);
            Assert.AreEqual(3.79f, s.ChoosableAt, Tolerance);
            Starts(s, RevealBeatKind.Linger, 3.79f);
        }

        // A top tier that is no upgrade lands as a Rare in its own colour: the
        // Rare's 0.25 s build, no ceremony, no cache of its own.
        [Test]
        public void FiveCards_ANonUpgradeTopTier_LandsWithTheRareTreatment()
        {
            var s = At(1f, 3, Common, UncommonDown, TopDown, Common, Common);
            Assert.IsFalse(s.HasCache);
            Assert.IsFalse(s.HasTopLiveUpgrade);
            Starts(s, RevealBeatKind.Deal, 0.39f, 0.44f, 0.49f, 0.54f, 0.59f);
            Starts(s, RevealBeatKind.Anticipate, 1.04f, 1.59f);
            Assert.AreEqual(0.1f, Of(s, RevealBeatKind.Anticipate)[0].Duration, Tolerance);
            Assert.AreEqual(0.25f, Of(s, RevealBeatKind.Anticipate)[1].Duration, Tolerance);
            CollectionAssert.AreEqual(new[] { LandStyle.Puff, LandStyle.Shimmer },
                Of(s, RevealBeatKind.Anticipate).Select(b => b.Style).ToArray());
            Starts(s, RevealBeatKind.Flip, 0.59f, 1.14f, 1.84f, 2.29f, 2.74f);
            Starts(s, RevealBeatKind.Land, 0.77f, 1.32f, 2.02f, 2.47f, 2.92f);
            CollectionAssert.AreEqual(
                new[] { LandStyle.Flat, LandStyle.Puff, LandStyle.Shimmer, LandStyle.Flat, LandStyle.Flat },
                Of(s, RevealBeatKind.Land).Select(b => b.Style).ToArray());
            Assert.AreEqual(2.92f, s.ChoosableAt, Tolerance);
            Assert.IsEmpty(Of(s, RevealBeatKind.Linger));
        }

        // Each card lands before the next one's build starts, whatever the mix.
        [Test]
        public void EveryCard_LandsBeforeTheNextBuilds_LeftToRight()
        {
            var s = At(1f, 3, TopLive, RareLive, Uncommon, Common, TopDown);
            var lands = Of(s, RevealBeatKind.Land);
            var flips = Of(s, RevealBeatKind.Flip);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, flips.Select(b => b.Card).ToArray());
            for (int i = 1; i < flips.Length; i++)
            {
                var build = Of(s, RevealBeatKind.Anticipate).Where(b => b.Card == i).ToArray();
                float next = build.Length > 0 ? build[0].Start : flips[i].Start;
                Assert.Greater(next, lands[i - 1].Start, "card " + i + " waits for card " + (i - 1));
            }
        }

        // The ceremony settles 0.55 s before the next card, not 0.27.
        [Test]
        public void AfterTheCeremony_TheNextCardWaitsLonger()
        {
            var s = At(1f, 1, TopLive, Common, Common);
            Starts(s, RevealBeatKind.Anticipate, 1.21f);
            Starts(s, RevealBeatKind.Flip, 1.81f, 2.54f, 2.99f);
            Assert.AreEqual(3.17f, s.ChoosableAt, Tolerance);
        }

        [Test]
        public void TheTableAtTwoX_IsHalved()
        {
            Assert.AreEqual(0.835f, At(2f, 1, Common, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(1.895f, At(2f, 3, Common, Common, Common, Common, TopLive).ChoosableAt, Tolerance);
            Assert.AreEqual(1.46f, At(2f, 3, Common, UncommonDown, TopDown, Common, Common).ChoosableAt, Tolerance);
        }

        [Test]
        public void TheTable_NeverSlowsBelowOneX()
        {
            Assert.AreEqual(1.67f, At(0.5f, 1, Common, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(3.79f, At(0.5f, 3, Common, Common, Common, Common, TopLive).ChoosableAt, Tolerance);
            Assert.AreEqual(1.1133f, At(1.5f, 1, Common, Common, Common).ChoosableAt, Tolerance);
        }

        // Schedule order

        [Test]
        public void CachePage_BeatsRunWipeWaitSealSlideBuildFlips_InOrder()
        {
            var s = At(1f, 1, RareLive, Common, Common);
            var timed = s.Beats.Where(b => b.Kind != RevealBeatKind.Land && b.Kind != RevealBeatKind.Ember
                                        && b.Kind != RevealBeatKind.Chevron).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    RevealBeatKind.Wipe, RevealBeatKind.OpenWait, RevealBeatKind.Seal, RevealBeatKind.Slide,
                    RevealBeatKind.Anticipate, RevealBeatKind.Flip, RevealBeatKind.Flip, RevealBeatKind.Flip,
                    RevealBeatKind.Choosable,
                },
                timed.Select(b => b.Kind).ToArray());
            float[] starts = { 0f, 0.34f, 0.84f, 0.96f, 1.21f, 1.46f, 1.91f, 2.36f, 2.54f };
            for (int i = 0; i < starts.Length; i++) Assert.AreEqual(starts[i], timed[i].Start, Tolerance, "beat " + i);
        }

        [Test]
        public void CacheRequiresARarePlusLiveUpgrade_NotJustQuality()
        {
            Assert.IsFalse(At(1f, 1, RareDown, TopDown, Common).HasCache, "a downgrade never earns the cache");
            Assert.IsFalse(At(1f, 1, Uncommon, Common, Common).HasCache, "a live Uncommon relic is below the line");
            Assert.IsTrue(At(1f, 1, Common, RareLive, Common).HasCache);
        }

        // The cache and the ceremony key on the worn item (A9, plan C3)

        private static RevealCard Gear(int tier, int worn, bool live = true) => new RevealCard(tier, live, worn);

        private static RevealSchedule Page(EncounterClass cls, params RevealCard[] cards) =>
            RevealSchedule.For(cards, 1, 1f, cls);

        [Test]
        public void GearCache_AUpgradeOverALowerWornQuality_Earns()
        {
            Assert.IsTrue(Page(EncounterClass.Normal, Gear(3, 2), Gear(1, 1, false), Gear(1, 1, false)).HasCache);
            Assert.IsTrue(Page(EncounterClass.Normal, Gear(4, 3), Gear(1, 1, false), Gear(1, 1, false)).HasCache);
        }

        [Test]
        public void GearCache_OverAnEqualWornQuality_DoesNot()
        {
            Assert.IsFalse(Page(EncounterClass.Normal, Gear(3, 3), Gear(1, 1, false), Gear(1, 1, false)).HasCache);
            Assert.IsFalse(Page(EncounterClass.Normal, Gear(2, 2), Gear(2, 3), Gear(1, 1, false)).HasCache,
                "a live card over a better or equal worn item");
        }

        [Test]
        public void GearCache_OverAnEmptySlot_Earns()
        {
            Assert.IsTrue(Page(EncounterClass.Normal, Gear(3, 0), Gear(1, 1, false), Gear(1, 1, false)).HasCache,
                "a live Rare over an empty slot");
        }

        [Test]
        public void GearCache_BelowRare_NeverEarns_EvenOverAnEmptySlotOrALowerWornItem()
        {
            Assert.IsFalse(Page(EncounterClass.Normal, Gear(1, 0), Gear(1, 1, false), Gear(1, 1, false)).HasCache,
                "a live Common over an empty slot");
            Assert.IsFalse(Page(EncounterClass.Normal, Gear(2, 0), Gear(1, 1, false), Gear(1, 1, false)).HasCache,
                "a live Uncommon over an empty slot");
            Assert.IsFalse(Page(EncounterClass.Normal, Gear(2, 1), Gear(1, 1, false), Gear(1, 1, false)).HasCache,
                "a live Uncommon over a Common");
        }

        [Test]
        public void GearCache_ANonLiveCardNeverEarns_HoweverHighItsQuality()
        {
            Assert.IsFalse(Page(EncounterClass.Normal, Gear(4, 0, false), Gear(3, 0, false), Gear(1, 1, false)).HasCache);
        }

        // The ceremony (a top-tier live upgrade's tremble, rays, shake and
        // bell) keys on the worn item, boss page or not.

        private static RevealBeat[] Ceremonies(RevealSchedule s) =>
            Of(s, RevealBeatKind.Anticipate).Where(b => b.Style == LandStyle.Burst).ToArray();

        [Test]
        public void TheCeremony_OverAWornItemBelowLegendary_Plays()
        {
            foreach (int worn in new[] { 0, 1, 2, 3 })
            {
                var s = Page(EncounterClass.Normal, Gear(4, worn), Gear(1, 1, false), Gear(1, 1, false));
                Assert.IsTrue(s.HasTopLiveUpgrade, "worn " + worn);
                Assert.AreEqual(1, Ceremonies(s).Length, "worn " + worn);
                Assert.AreEqual(1, Of(s, RevealBeatKind.Linger).Length, "worn " + worn);
            }
        }

        [Test]
        public void ALegendaryOverALegendary_LandsAsARare_AndEarnsNoCache()
        {
            var s = Page(EncounterClass.Normal, Gear(4, 4), Gear(1, 1, false), Gear(1, 1, false));
            Assert.IsFalse(s.HasCache);
            Assert.IsFalse(s.HasTopLiveUpgrade);
            Assert.IsEmpty(Ceremonies(s));
            Assert.IsEmpty(Of(s, RevealBeatKind.Linger));
            Assert.AreEqual(LandStyle.Shimmer, Of(s, RevealBeatKind.Land)[0].Style);
            Assert.AreEqual(1.92f, s.ChoosableAt, Tolerance);
        }

        [Test]
        public void BossPage_AlwaysGetsTheCache_EvenWithNoQualifyingCard()
        {
            var s = Page(EncounterClass.Boss, Gear(2, 3), Gear(1, 1, false), Gear(3, 0, false));
            Assert.IsTrue(s.HasCache);
            Assert.IsEmpty(Ceremonies(s));
            Assert.AreEqual(2.64f, s.ChoosableAt, Tolerance);
            Assert.AreEqual(3, Of(s, RevealBeatKind.Flip).Length);
        }

        [Test]
        public void BossPage_TheCeremonyStillFollowsTheWornRule()
        {
            var over = Page(EncounterClass.Boss, Gear(4, 2), Gear(1, 1, false), Gear(1, 1, false));
            Assert.AreEqual(1, Ceremonies(over).Length);
            Assert.AreEqual(3.17f, over.ChoosableAt, Tolerance);
            var equal = Page(EncounterClass.Boss, Gear(4, 4), Gear(1, 1, false), Gear(1, 1, false));
            Assert.IsTrue(equal.HasCache);
            Assert.IsEmpty(Ceremonies(equal));
        }

        [Test]
        public void ElitePage_FollowsTheWornRuleLikeNormal()
        {
            Assert.IsFalse(Page(EncounterClass.Elite, Gear(3, 3), Gear(1, 1, false), Gear(1, 1, false)).HasCache);
            Assert.IsTrue(Page(EncounterClass.Elite, Gear(3, 1), Gear(1, 1, false), Gear(1, 1, false)).HasCache);
        }

        [Test]
        public void RelicPage_KeepsTheQualityOnlyRule_IgnoringAnyWornItem()
        {
            Assert.IsTrue(Page(EncounterClass.Normal, RevealCard.Relic(3, true), Common, Common).HasCache);
            Assert.IsFalse(Page(EncounterClass.Normal, RevealCard.Relic(2, true), Common, Common).HasCache);
            Assert.IsFalse(Page(EncounterClass.Normal, RevealCard.Relic(4, false), Common, Common).HasCache);
            Assert.AreEqual(1, Ceremonies(Page(EncounterClass.Normal, RevealCard.Relic(4, true), Common, Common)).Length);
            Assert.AreEqual(0, new RevealCard(4, true, 4, true).WornTier, "a relic has no worn item");
        }

        // Builds

        [Test]
        public void TheBuild_FollowsTheTreatment()
        {
            Assert.AreEqual(0f, RevealSchedule.AnticipationSeconds(LandStyle.Flat), Tolerance);
            Assert.AreEqual(0.1f, RevealSchedule.AnticipationSeconds(LandStyle.Puff), Tolerance);
            Assert.AreEqual(0.25f, RevealSchedule.AnticipationSeconds(LandStyle.Shimmer), Tolerance);
            Assert.AreEqual(0.6f, RevealSchedule.AnticipationSeconds(LandStyle.Burst), Tolerance);
        }

        [Test]
        public void CeremoniesBelongToTopTierLiveUpgradesOnly()
        {
            Assert.IsEmpty(Ceremonies(At(1f, 1, RareLive, Common, Common)));
            Assert.IsEmpty(Ceremonies(At(1f, 1, RareLive, TopDown, Common)), "top tier that is a downgrade");
            Assert.IsEmpty(Ceremonies(At(1f, 1, RareDown, TopDown, Common)));
            Assert.IsEmpty(Ceremonies(At(1f, 1, RareLive, Common, RareLive)));
            var ceremony = Ceremonies(At(1f, 1, RareLive, TopLive, Common));
            Assert.AreEqual(1, ceremony.Length);
            Assert.AreEqual(1, ceremony[0].Card);
        }

        [Test]
        public void TheCeremony_PrecedesItsCardsFlip_AndDelaysTheOnesAfter()
        {
            var s = At(1f, 1, RareLive, TopLive, Common);
            var ceremony = Ceremonies(s)[0];
            Assert.AreEqual(1.91f, ceremony.Start, Tolerance);
            Assert.AreEqual(0.6f, ceremony.Duration, Tolerance);
            Starts(s, RevealBeatKind.Flip, 1.46f, 2.51f, 3.24f);
            Assert.AreEqual(3.42f, s.ChoosableAt, Tolerance);
        }

        // No cap: two ceremonies each keep their whole build.
        [Test]
        public void TwoTopLiveUpgrades_EachKeepTheirWholeBuild()
        {
            var s = At(1f, 2, TopLive, Common, TopLive, Common);
            var ceremonies = Ceremonies(s);
            Assert.AreEqual(2, ceremonies.Length);
            Assert.AreEqual(0.6f, ceremonies[0].Duration, Tolerance);
            Assert.AreEqual(0.6f, ceremonies[1].Duration, Tolerance);
            Starts(s, RevealBeatKind.Flip, 1.81f, 2.54f, 3.59f, 4.32f);
            Assert.AreEqual(4.50f, s.ChoosableAt, Tolerance);
        }

        [Test]
        public void LandStyles_FollowQualityAndLiveStatus()
        {
            var s = At(1f, 1, Common, Uncommon, RareLive, TopLive);
            CollectionAssert.AreEqual(
                new[] { LandStyle.Flat, LandStyle.Puff, LandStyle.Shimmer, LandStyle.Burst },
                Of(s, RevealBeatKind.Land).Select(b => b.Style).ToArray());
            Assert.AreEqual(LandStyle.Shimmer, RevealSchedule.StyleOf(TopDown), "no ceremony for a downgrade");
        }

        // Pitch steps

        [Test]
        public void FlipPitch_IsTheQualityStep_InRowOrder()
        {
            var s = At(1f, 2, Common, Uncommon, RareLive, TopLive);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, Of(s, RevealBeatKind.Flip).Select(b => b.PitchStep).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Of(s, RevealBeatKind.Flip).Select(b => b.Card).ToArray());
        }

        [Test]
        public void RelicRarityTiers_MapToTheTablesPitchSteps()
        {
            Assert.AreEqual(1, RevealCard.Gear(Quality.Common, false).Tier);
            Assert.AreEqual(2, RevealCard.Gear(Quality.Uncommon, false).Tier);
            Assert.AreEqual(3, RevealCard.Gear(Quality.Rare, false).Tier);
            Assert.AreEqual(4, RevealCard.Gear(Quality.Legendary, false).Tier);
            Assert.AreEqual(4, new RevealCard(9, false).Tier, "clamped to the top clip");
            Assert.AreEqual(1, new RevealCard(0, false).Tier);
        }

        [Test]
        public void ChevronNotes_RiseOneStepPerUpCard_AndADowngradeIsSilent()
        {
            var up = new RevealCard(2, true, showsUp: true);
            var rareUp = new RevealCard(3, true, showsUp: true);
            var s = At(1f, 1, up, Common, rareUp, RareDown, new RevealCard(4, true, showsUp: true));
            var chevrons = Of(s, RevealBeatKind.Chevron);
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, chevrons.Select(b => b.Card).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, chevrons.Select(b => b.PitchStep).ToArray());
            Assert.IsEmpty(Of(At(1f, 1, Common, RareDown, TopDown), RevealBeatKind.Chevron), "a page with nothing for anyone is quiet");
        }

        [Test]
        public void EveryPageHasEmbers_OnePerChoice_AtTheLandings()
        {
            var noCache = At(1f, 2, Common, Common, Common, Common);
            CollectionAssert.AreEqual(new[] { 0, 1 }, Of(noCache, RevealBeatKind.Ember).Select(b => b.Card).ToArray());
            var cache = At(1f, 1, RareLive, Common, Common);
            Assert.AreEqual(1, Of(cache, RevealBeatKind.Ember).Length);
            Assert.AreEqual(Of(cache, RevealBeatKind.Land)[0].Start, Of(cache, RevealBeatKind.Ember)[0].Start, Tolerance);
        }

        // Linger and guard

        [Test]
        public void TheLinger_ExistsOnlyForATopLiveUpgrade_AndStartsAtChoosable()
        {
            var s = At(1f, 1, RareLive, Common, TopLive);
            var linger = Of(s, RevealBeatKind.Linger);
            Assert.AreEqual(1, linger.Length);
            Assert.AreEqual(3.14f, linger[0].Start, Tolerance);
            Assert.AreEqual(1.2f, linger[0].Duration, Tolerance);
            Assert.IsEmpty(Of(At(1f, 1, RareLive, Common, Common), RevealBeatKind.Linger));
            Assert.AreEqual(0.6f, Of(At(2f, 1, RareLive, Common, TopLive), RevealBeatKind.Linger)[0].Duration, Tolerance);
        }

        [Test]
        public void TheGuard_SwallowsASubmitForPointOneFiveAfterTheLastLanding()
        {
            var s = At(1f, 1, RareLive, Common, Common);
            Assert.IsTrue(s.IsRunningAt(2.53f));
            Assert.IsFalse(s.GuardsAt(2.53f));
            Assert.IsFalse(s.IsRunningAt(2.545f));
            Assert.IsTrue(s.GuardsAt(2.6f));
            Assert.IsFalse(s.GuardsAt(2.7f));
        }

        // Skip

        [Test]
        public void Skip_ReachesTheEndState_AndAFlareRemainsOnlyForATopLiveUpgrade()
        {
            var top = At(1f, 1, RareLive, Common, TopLive);
            var skipped = top.SkippedAt(0.5f);
            Assert.IsTrue(skipped.Skipped);
            Assert.AreEqual(0.5f, skipped.ChoosableAt, Tolerance, "choosable now");
            Assert.AreEqual(0.3f, skipped.SkipFlareSeconds, Tolerance);
            Assert.IsEmpty(Of(skipped, RevealBeatKind.Linger), "a press ends the linger");
            Assert.IsTrue(skipped.HasCache);

            var plain = At(1f, 1, RareLive, Common, Common).SkippedAt(0.5f);
            Assert.AreEqual(0f, plain.SkipFlareSeconds, Tolerance);

            Assert.AreEqual(0.15f, At(2f, 1, RareLive, Common, TopLive).SkippedAt(0.2f).SkipFlareSeconds, Tolerance);
        }

        [Test]
        public void Skip_AfterTheRowIsChoosable_ChangesNothing()
        {
            var s = At(1f, 1, RareLive, Common, TopLive);
            var late = s.SkippedAt(4f);
            Assert.IsFalse(late.Skipped);
            Assert.AreEqual(3.14f, late.ChoosableAt, Tolerance);
            Assert.AreEqual(0f, late.SkipFlareSeconds, Tolerance);
        }

        [Test]
        public void Skip_OnANoCachePage_HasNoCacheToOpen()
        {
            Assert.IsFalse(At(1f, 1, Common, Common, Common).SkippedAt(0.4f).HasCache);
        }
    }
}
