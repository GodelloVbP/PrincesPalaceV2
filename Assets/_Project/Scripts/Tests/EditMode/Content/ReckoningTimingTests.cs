using System.Collections.Generic;
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

        // The plan's wall-time table (C4), at 1x, then at the other presets.

        [Test]
        public void NoCachePage_IsChoosableAtPointFiveNine_ForAnyCount()
        {
            Assert.AreEqual(0.59f, At(1f, 1, Common, Uncommon, RareDown).ChoosableAt, Tolerance);
            Assert.AreEqual(0.59f, At(1f, 3, Common, Common, Common, Uncommon, TopDown).ChoosableAt, Tolerance);
            Assert.IsFalse(At(1f, 1, Common, Uncommon, TopDown).HasCache);
        }

        [Test]
        public void CacheThreeCards_IsChoosableAtOnePointOneFive()
        {
            Assert.AreEqual(1.15f, At(1f, 1, RareLive, Common, Common).ChoosableAt, Tolerance);
        }

        [Test]
        public void CacheThreeCards_WithTopLive_IsChoosableAtOnePointFour()
        {
            Assert.AreEqual(1.40f, At(1f, 1, RareLive, Common, TopLive).ChoosableAt, Tolerance);
        }

        [Test]
        public void CacheFiveCards_IsOnePointTwoSeven_AndWithTopLive_OnePointFiveTwo()
        {
            Assert.AreEqual(1.27f, At(1f, 3, RareLive, Common, Common, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(1.52f, At(1f, 3, Common, Common, Common, Common, TopLive).ChoosableAt, Tolerance);
        }

        [Test]
        public void TheTableAtTwoX_IsHalved()
        {
            Assert.AreEqual(0.295f, At(2f, 1, Common, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(0.575f, At(2f, 1, RareLive, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(0.70f, At(2f, 1, RareLive, Common, TopLive).ChoosableAt, Tolerance);
            Assert.AreEqual(0.635f, At(2f, 3, RareLive, Common, Common, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(0.76f, At(2f, 3, Common, Common, Common, Common, TopLive).ChoosableAt, Tolerance);
        }

        [Test]
        public void TheTable_NeverSlowsBelowOneX()
        {
            Assert.AreEqual(1.15f, At(0.5f, 1, RareLive, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(0.59f, At(0.5f, 1, Common, Common, Common).ChoosableAt, Tolerance);
            Assert.AreEqual(0.7667f, At(1.5f, 1, RareLive, Common, Common).ChoosableAt, Tolerance);
        }

        // Schedule order

        [Test]
        public void CachePage_BeatsRunWipeWaitSealSlideFlips_InOrder()
        {
            var s = At(1f, 1, RareLive, Common, Common);
            var timed = s.Beats.Where(b => b.Kind != RevealBeatKind.Land && b.Kind != RevealBeatKind.Ember
                                        && b.Kind != RevealBeatKind.Chevron).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    RevealBeatKind.Wipe, RevealBeatKind.OpenWait, RevealBeatKind.Seal, RevealBeatKind.Slide,
                    RevealBeatKind.Flip, RevealBeatKind.Flip, RevealBeatKind.Flip, RevealBeatKind.Choosable,
                },
                timed.Select(b => b.Kind).ToArray());
            float[] starts = { 0f, 0.34f, 0.54f, 0.66f, 0.91f, 0.97f, 1.03f, 1.15f };
            for (int i = 0; i < starts.Length; i++) Assert.AreEqual(starts[i], timed[i].Start, Tolerance, "beat " + i);
            Assert.AreEqual(0.12f, timed[4].Duration, Tolerance);
        }

        [Test]
        public void NoCachePage_DealsLeftToRight_ThenIsChoosable()
        {
            var s = At(1f, 1, Common, Uncommon, RareDown);
            Assert.AreEqual(0.34f, Of(s, RevealBeatKind.Deal)[0].Start, Tolerance);
            Assert.AreEqual(0.25f, Of(s, RevealBeatKind.Deal)[0].Duration, Tolerance);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Of(s, RevealBeatKind.Land).Select(b => b.Card).ToArray());
            float[] lands = { 0.4233f, 0.5067f, 0.59f };
            for (int i = 0; i < 3; i++) Assert.AreEqual(lands[i], Of(s, RevealBeatKind.Land)[i].Start, Tolerance);
            Assert.IsEmpty(Of(s, RevealBeatKind.Flip));
            Assert.IsEmpty(Of(s, RevealBeatKind.Seal));
        }

        [Test]
        public void CacheRequiresARarePlusLiveUpgrade_NotJustQuality()
        {
            Assert.IsFalse(At(1f, 1, RareDown, TopDown, Common).HasCache, "a downgrade never earns the cache");
            Assert.IsFalse(At(1f, 1, Uncommon, Common, Common).HasCache, "a live Uncommon relic is below the line");
            Assert.IsTrue(At(1f, 1, Common, RareLive, Common).HasCache);
        }

        // The cache and the held beat key on the worn item (A9, plan C3)

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

        [Test]
        public void LegendaryBeat_OverAWornItemBelowLegendary_Holds()
        {
            foreach (int worn in new[] { 0, 1, 2, 3 })
            {
                var s = Page(EncounterClass.Normal, Gear(4, worn), Gear(1, 1, false), Gear(1, 1, false));
                Assert.IsTrue(s.HasTopLiveUpgrade, "worn " + worn);
                Assert.AreEqual(1, Of(s, RevealBeatKind.Hold).Length, "worn " + worn);
                Assert.AreEqual(1, Of(s, RevealBeatKind.Linger).Length, "worn " + worn);
            }
        }

        [Test]
        public void LegendaryBeat_ALegendaryOverALegendary_DoesNotHold_AndEarnsNoCache()
        {
            var s = Page(EncounterClass.Normal, Gear(4, 4), Gear(1, 1, false), Gear(1, 1, false));
            Assert.IsFalse(s.HasCache);
            Assert.IsFalse(s.HasTopLiveUpgrade);
            Assert.IsEmpty(Of(s, RevealBeatKind.Hold));
            Assert.IsEmpty(Of(s, RevealBeatKind.Linger));
            Assert.AreEqual(0.59f, s.ChoosableAt, Tolerance);
        }

        [Test]
        public void BossPage_AlwaysGetsTheCache_EvenWithNoQualifyingCard()
        {
            var s = Page(EncounterClass.Boss, Gear(2, 3), Gear(1, 1, false), Gear(3, 0, false));
            Assert.IsTrue(s.HasCache);
            Assert.IsEmpty(Of(s, RevealBeatKind.Hold));
            Assert.AreEqual(1.15f, s.ChoosableAt, Tolerance);
            Assert.AreEqual(3, Of(s, RevealBeatKind.Flip).Length);
        }

        [Test]
        public void BossPage_TheBeatStillFollowsTheWornRule()
        {
            var over = Page(EncounterClass.Boss, Gear(4, 2), Gear(1, 1, false), Gear(1, 1, false));
            Assert.AreEqual(1, Of(over, RevealBeatKind.Hold).Length);
            Assert.AreEqual(1.40f, over.ChoosableAt, Tolerance);
            var equal = Page(EncounterClass.Boss, Gear(4, 4), Gear(1, 1, false), Gear(1, 1, false));
            Assert.IsTrue(equal.HasCache);
            Assert.IsEmpty(Of(equal, RevealBeatKind.Hold));
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
            Assert.AreEqual(1, Of(Page(EncounterClass.Normal, RevealCard.Relic(4, true), Common, Common), RevealBeatKind.Hold).Length);
            Assert.AreEqual(0, new RevealCard(4, true, 4, true).WornTier, "a relic has no worn item");
        }

        // Holds

        [Test]
        public void HoldsBelongToTopTierLiveUpgradesOnly()
        {
            Assert.IsEmpty(Of(At(1f, 1, RareLive, Common, Common), RevealBeatKind.Hold));
            Assert.IsEmpty(Of(At(1f, 1, RareLive, TopDown, Common), RevealBeatKind.Hold), "top tier that is a downgrade");
            Assert.IsEmpty(Of(At(1f, 1, RareDown, TopDown, Common), RevealBeatKind.Hold));
            Assert.IsEmpty(Of(At(1f, 1, RareLive, Common, RareLive), RevealBeatKind.Hold));
            var hold = Of(At(1f, 1, RareLive, TopLive, Common), RevealBeatKind.Hold);
            Assert.AreEqual(1, hold.Length);
            Assert.AreEqual(1, hold[0].Card);
        }

        [Test]
        public void TheHold_PrecedesItsCardsFlip_AndDelaysTheOnesAfter()
        {
            var s = At(1f, 1, RareLive, TopLive, Common);
            var hold = Of(s, RevealBeatKind.Hold)[0];
            var flips = Of(s, RevealBeatKind.Flip);
            Assert.AreEqual(0.97f, hold.Start, Tolerance);
            Assert.AreEqual(0.25f, hold.Duration, Tolerance);
            Assert.AreEqual(1.22f, flips[1].Start, Tolerance, "the top tier flips after its hold");
            Assert.AreEqual(0.91f, flips[0].Start, Tolerance, "the card before it flips on time, as the slide ends");
            Assert.AreEqual(1.28f, flips[2].Start, Tolerance, "the card after it waits");
            Assert.AreEqual(1.40f, s.ChoosableAt, Tolerance);
        }

        // Four cards, two holds: 1.71 s authored, 0.11 over. The cache's wait
        // is already at its floor (0.2 + 0.12 seal < 0.35), the stagger gives
        // 0.06 down to 0.04, and the second hold gives the last 0.05.
        [Test]
        public void TwoTopLiveUpgrades_HoldTwice_TheSecondShrinksFirst()
        {
            var s = At(1f, 2, TopLive, Common, TopLive, Common);
            var holds = Of(s, RevealBeatKind.Hold);
            Assert.AreEqual(2, holds.Length);
            Assert.AreEqual(1.6f, s.ChoosableAt, Tolerance);
            Assert.AreEqual(0.2f, Of(s, RevealBeatKind.OpenWait)[0].Duration, Tolerance);
            Assert.AreEqual(0.25f, holds[0].Duration, Tolerance);
            Assert.AreEqual(0.20f, holds[1].Duration, Tolerance);
            var flips = Of(s, RevealBeatKind.Flip);
            Assert.AreEqual(0.04f, flips[2].Start - holds[1].Duration - flips[1].Start, Tolerance);
        }

        // THE CACHETOP PAGE (ReckoningPageFixtures.CacheTopTierPages): five
        // cards, two top tiers. 1.77 s authored, 0.17 over: the stagger gives
        // 0.08 (0.06 -> 0.04), the second hold 0.09 (0.25 -> 0.16). Every flip
        // still lands on its own.
        [Test]
        public void FiveCardsTwoTopTiers_FitTheCap_EachTopTierLandingAlone()
        {
            var s = At(1f, 3, TopLive, Common, TopLive, Common, Common);
            Assert.AreEqual(1.6f, s.ChoosableAt, Tolerance);

            var holds = Of(s, RevealBeatKind.Hold);
            Assert.AreEqual(2, holds.Length);
            Assert.AreEqual(0.91f, holds[0].Start, Tolerance, "the first top tier holds as the slide ends");
            Assert.AreEqual(0.25f, holds[0].Duration, Tolerance);
            Assert.AreEqual(1.24f, holds[1].Start, Tolerance);
            Assert.AreEqual(0.16f, holds[1].Duration, Tolerance);

            float[] flips = { 1.16f, 1.20f, 1.40f, 1.44f, 1.48f };
            var flipBeats = Of(s, RevealBeatKind.Flip);
            for (int i = 0; i < flips.Length; i++) Assert.AreEqual(flips[i], flipBeats[i].Start, Tolerance, "flip " + i);
        }

        // One top tier on five cards fits unshortened: 1.52 s.
        [Test]
        public void FiveCardsOneTopTier_KeepsTheAuthoredStaggerAndHold()
        {
            var s = At(1f, 3, Common, Common, Common, Common, TopLive);
            Assert.AreEqual(1.52f, s.ChoosableAt, Tolerance);
            float[] flips = { 0.91f, 0.97f, 1.03f, 1.09f, 1.40f };
            var flipBeats = Of(s, RevealBeatKind.Flip);
            for (int i = 0; i < flips.Length; i++) Assert.AreEqual(flips[i], flipBeats[i].Start, Tolerance, "flip " + i);
            Assert.AreEqual(1.15f, Of(s, RevealBeatKind.Hold)[0].Start, Tolerance);
            Assert.AreEqual(0.25f, Of(s, RevealBeatKind.Hold)[0].Duration, Tolerance);
        }

        [Test]
        public void LandStyles_FollowQualityAndLiveStatus()
        {
            var s = At(1f, 1, Common, Uncommon, RareLive, TopLive);
            CollectionAssert.AreEqual(
                new[] { LandStyle.Flat, LandStyle.Puff, LandStyle.Shimmer, LandStyle.Burst },
                Of(s, RevealBeatKind.Land).Select(b => b.Style).ToArray());
            Assert.AreEqual(LandStyle.Shimmer, RevealSchedule.StyleOf(TopDown), "no flare for a downgrade");
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
            Assert.AreEqual(1.40f, linger[0].Start, Tolerance);
            Assert.AreEqual(1.2f, linger[0].Duration, Tolerance);
            Assert.IsEmpty(Of(At(1f, 1, RareLive, Common, Common), RevealBeatKind.Linger));
            Assert.AreEqual(0.6f, Of(At(2f, 1, RareLive, Common, TopLive), RevealBeatKind.Linger)[0].Duration, Tolerance);
        }

        [Test]
        public void TheGuard_SwallowsASubmitForPointOneFiveAfterTheLastLanding()
        {
            var s = At(1f, 1, RareLive, Common, Common);
            Assert.IsTrue(s.IsRunningAt(1.14f));
            Assert.IsFalse(s.GuardsAt(1.14f));
            Assert.IsFalse(s.IsRunningAt(1.15f));
            Assert.IsTrue(s.GuardsAt(1.2f));
            Assert.IsFalse(s.GuardsAt(1.31f));
        }

        // Skip

        [Test]
        public void Skip_ReachesTheEndState_AndAFlareRemainsOnlyForATopLiveUpgrade()
        {
            var top = At(1f, 1, RareLive, Common, TopLive);
            var skipped = top.SkippedAt(0.5f);
            Assert.IsTrue(skipped.Skipped);
            Assert.AreEqual(0.5f, skipped.ChoosableAt, Tolerance, "choosable now");
            Assert.AreEqual(0.12f, skipped.SkipFlareSeconds, Tolerance);
            Assert.IsEmpty(Of(skipped, RevealBeatKind.Linger), "a press ends the linger");
            Assert.IsTrue(skipped.HasCache);

            var plain = At(1f, 1, RareLive, Common, Common).SkippedAt(0.5f);
            Assert.AreEqual(0f, plain.SkipFlareSeconds, Tolerance);

            Assert.AreEqual(0.06f, At(2f, 1, RareLive, Common, TopLive).SkippedAt(0.2f).SkipFlareSeconds, Tolerance);
        }

        [Test]
        public void Skip_AfterTheRowIsChoosable_ChangesNothing()
        {
            var s = At(1f, 1, RareLive, Common, TopLive);
            var late = s.SkippedAt(2f);
            Assert.IsFalse(late.Skipped);
            Assert.AreEqual(1.40f, late.ChoosableAt, Tolerance);
            Assert.AreEqual(0f, late.SkipFlareSeconds, Tolerance);
        }

        [Test]
        public void Skip_OnANoCachePage_HasNoCacheToOpen()
        {
            Assert.IsFalse(At(1f, 1, Common, Common, Common).SkippedAt(0.4f).HasCache);
        }

        // Cap

        private static IEnumerable<RevealCard[]> Pages()
        {
            yield return new[] { Common, Common, Common };
            yield return new[] { RareLive, Common, Common };
            yield return new[] { RareLive, Common, TopLive };
            yield return new[] { Common, Common, Common, Common, TopLive };
            yield return new[] { TopLive, TopLive, TopLive, TopLive, TopLive, TopLive, TopLive };
            yield return Enumerable.Repeat(TopLive, 9).ToArray();
            yield return Enumerable.Repeat(TopLive, 12).ToArray();
        }

        [Test]
        public void TheCap_HoldsAtEveryPreset_ForEveryPage()
        {
            foreach (float preset in new[] { 0.5f, 1f, 1.5f, 2f })
                foreach (var page in Pages())
                    Assert.LessOrEqual(RevealSchedule.For(page, 3, preset).ChoosableAt, RevealSchedule.CapSeconds + Tolerance,
                        $"preset {preset}, {page.Length} cards");
        }

        [Test]
        public void ALongPage_ShortensTheStagger_ThenTheHolds_LastFirst()
        {
            // Seven cards, one hold, 1x: 1.64 s authored, 0.04 over; the stagger gives it up.
            var seven = At(1f, 5, TopLive, Common, Common, Common, Common, Common, Common);
            Assert.AreEqual(1.6f, seven.ChoosableAt, Tolerance);
            Assert.AreEqual(0.2f, Of(seven, RevealBeatKind.OpenWait)[0].Duration, Tolerance);
            Assert.AreEqual(0.25f, Of(seven, RevealBeatKind.Hold)[0].Duration, Tolerance);
            var flips = Of(seven, RevealBeatKind.Flip);
            Assert.AreEqual(0.0533f, flips[2].Start - flips[1].Start, Tolerance);

            // Twelve cards, one hold: 0.34 over. The stagger reaches its floor
            // (0.22), the hold its floor (0.10), and the hold gives the last 0.02.
            var twelve = At(1f, 10, new[] { TopLive }.Concat(Enumerable.Repeat(Common, 11)).ToArray());
            Assert.AreEqual(1.6f, twelve.ChoosableAt, Tolerance);
            Assert.AreEqual(0.13f, Of(twelve, RevealBeatKind.Hold)[0].Duration, Tolerance);
            var twelveFlips = Of(twelve, RevealBeatKind.Flip);
            Assert.AreEqual(0.04f, twelveFlips[2].Start - twelveFlips[1].Start, Tolerance);

            // Nine top tiers: past every floor the holds go to nothing from the
            // last; the first keeps its floor and the second 0.10.
            var nine = At(1f, 7, Enumerable.Repeat(TopLive, 9).ToArray());
            Assert.AreEqual(1.6f, nine.ChoosableAt, Tolerance);
            var nineHolds = Of(nine, RevealBeatKind.Hold);
            Assert.AreEqual(2, nineHolds.Length, "a hold shrunk to nothing is no beat");
            Assert.AreEqual(0.15f, nineHolds[0].Duration, Tolerance);
            Assert.AreEqual(0.10f, nineHolds[1].Duration, Tolerance);
            Assert.AreEqual(0.2f, Of(nine, RevealBeatKind.OpenWait)[0].Duration, Tolerance);
        }

        [Test]
        public void ThePlainFiveCardTopLivePage_IsNotShortened()
        {
            var s = At(1f, 3, Common, Common, Common, Common, TopLive);
            Assert.AreEqual(0.2f, Of(s, RevealBeatKind.OpenWait)[0].Duration, Tolerance);
            Assert.AreEqual(0.25f, Of(s, RevealBeatKind.Hold)[0].Duration, Tolerance);
        }
    }
}
