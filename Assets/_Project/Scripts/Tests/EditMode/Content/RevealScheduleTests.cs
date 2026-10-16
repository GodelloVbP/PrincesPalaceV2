using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // The reveal's skip guard, the chevron note following the face, the haul
    // ticks, an empty page and the pitch ladder, on literals
    // (docs/PLAN_REWARD_SCREENS.md C2, C3, C10, C11). ReckoningTimingTests
    // holds the timing table and the cap.
    public class RevealScheduleTests
    {
        private const float Tolerance = 0.0005f;

        private static readonly RevealCard Common = new RevealCard(1, false);
        private static readonly RevealCard RareLive = new RevealCard(3, true);
        private static readonly RevealCard TopLive = new RevealCard(4, true);

        private static RevealBeat[] Of(RevealSchedule s, RevealBeatKind kind) =>
            s.Beats.Where(b => b.Kind == kind).ToArray();

        // A cache page, 3 cards: choosable at 2.54 s unskipped.
        [Test]
        public void ASkip_MovesTheGuardToThePress()
        {
            var s = RevealSchedule.For(new[] { RareLive, Common, Common }, 1, 1f).SkippedAt(0.5f);
            Assert.IsFalse(s.IsRunningAt(0.5f), "input is live over the face-up row");
            Assert.IsTrue(s.GuardsAt(0.5f));
            Assert.IsTrue(s.GuardsAt(0.6f));
            Assert.IsFalse(s.GuardsAt(0.66f));
            Assert.AreEqual(0.65f, s.GuardUntil, Tolerance);
        }

        [Test]
        public void ASkip_AtTwoX_GuardsForHalfTheWindow()
        {
            var s = RevealSchedule.For(new[] { RareLive, Common, Common }, 1, 2f).SkippedAt(0.3f);
            Assert.AreEqual(0.375f, s.GuardUntil, Tolerance);
        }

        [Test]
        public void ASecondSkip_ChangesNothing()
        {
            var once = RevealSchedule.For(new[] { RareLive, Common, TopLive }, 1, 1f).SkippedAt(0.5f);
            var twice = once.SkippedAt(0.55f);
            Assert.AreEqual(0.5f, twice.ChoosableAt, Tolerance);
            Assert.AreEqual(0.65f, twice.GuardUntil, Tolerance);
        }

        // An affix-only gain is live (it earns a cache when Rare+) but its
        // face shows the flat bar, so it has no note.
        [Test]
        public void TheChevronNote_FollowsTheFace_NotTheLiveFlag()
        {
            var affixOnly = new RevealCard(3, true, showsUp: false);
            var statUp = new RevealCard(2, true, showsUp: true);
            var s = RevealSchedule.For(new[] { affixOnly, statUp, Common }, 1, 1f);
            Assert.IsTrue(s.HasCache, "the affix-only Rare still earns the cache");
            var chevrons = Of(s, RevealBeatKind.Chevron);
            CollectionAssert.AreEqual(new[] { 1 }, chevrons.Select(b => b.Card).ToArray());
            CollectionAssert.AreEqual(new[] { 1 }, chevrons.Select(b => b.PitchStep).ToArray());
        }

        [Test]
        public void ARelicFace_HasNoChevron()
        {
            Assert.IsFalse(new RevealCard(3, true, isRelic: true, showsUp: true).ShowsUp);
        }

        [Test]
        public void HaulTicks_LandInsideTheWipe_AtMostEight()
        {
            var s = RevealSchedule.For(new[] { Common, Common, Common }, 1, 1f, haulTicks: 4);
            var ticks = Of(s, RevealBeatKind.HaulTick);
            Assert.AreEqual(4, ticks.Length);
            float[] expected = { 0f, 0.085f, 0.17f, 0.255f };
            for (int i = 0; i < 4; i++) Assert.AreEqual(expected[i], ticks[i].Start, Tolerance);
            Assert.AreEqual(8, Of(RevealSchedule.For(new[] { Common }, 1, 1f, haulTicks: 20), RevealBeatKind.HaulTick).Length);
            Assert.IsEmpty(Of(RevealSchedule.For(new[] { Common }, 1, 1f), RevealBeatKind.HaulTick));
            Assert.AreEqual(1.67f, s.ChoosableAt, Tolerance, "the haul adds no time");
        }

        [Test]
        public void ABossPageWithNoCards_IsChoosableAfterTheWipe()
        {
            var s = RevealSchedule.For(new RevealCard[0], 0, 1f, EncounterClass.Boss);
            Assert.IsFalse(s.HasCache);
            Assert.AreEqual(0.34f, s.ChoosableAt, Tolerance);
        }

        // The treatments' look (RevealFeel): only a Rare and the ceremony
        // shake, only the ceremony shakes the stage, and the punch grows with
        // the treatment.
        [Test]
        public void TheTreatments_ShakeAndPunch_ByQuality()
        {
            Assert.AreEqual(0f, RevealFeel.PanelShakePixels(LandStyle.Flat));
            Assert.AreEqual(0f, RevealFeel.PanelShakePixels(LandStyle.Puff));
            Assert.AreEqual(3f, RevealFeel.PanelShakePixels(LandStyle.Shimmer));
            Assert.AreEqual(8f, RevealFeel.PanelShakePixels(LandStyle.Burst));
            Assert.AreEqual(0f, RevealFeel.StageShakePixels(LandStyle.Shimmer));
            Assert.AreEqual(8f, RevealFeel.StageShakePixels(LandStyle.Burst));
            Assert.AreEqual(0.25f, RevealFeel.ShakeSeconds(LandStyle.Burst), Tolerance);
            Assert.AreEqual(1.15f, RevealFeel.Punch(LandStyle.Burst, 0f), Tolerance);
            Assert.AreEqual(1f, RevealFeel.Punch(LandStyle.Burst, 0.25f), Tolerance);
            Assert.AreEqual(8f, RevealFeel.Lift(LandStyle.Shimmer, 1f, -1f), Tolerance);
            Assert.AreEqual(0f, RevealFeel.Lift(LandStyle.Flat, 1f, -1f), Tolerance);
        }

        // The tremble rises to at most 3 px and is the same at the same instant.
        [Test]
        public void TheTremble_StaysWithinThreePixels_AndRepeats()
        {
            for (int f = 0; f < 60; f++)
            {
                RevealFeel.Tremble(LandStyle.Burst, 0.99f, f / 60f, out float dx, out float dy);
                Assert.LessOrEqual(System.Math.Abs(dx), 3f);
                Assert.LessOrEqual(System.Math.Abs(dy), 3f);
                RevealFeel.Tremble(LandStyle.Burst, 0.99f, f / 60f, out float dx2, out float dy2);
                Assert.AreEqual(dx, dx2);
                Assert.AreEqual(dy, dy2);
            }

            RevealFeel.Tremble(LandStyle.Shimmer, 0.5f, 0.3f, out float rx, out float ry);
            Assert.AreEqual(0f, rx);
            Assert.AreEqual(0f, ry);
        }

        [Test]
        public void TheLadder_IsAPentatonicFromTheRoot()
        {
            Assert.AreEqual(1f, RevealSchedule.LadderPitch(1), 0.0001f);
            Assert.AreEqual(1.1225f, RevealSchedule.LadderPitch(2), 0.0001f);
            Assert.AreEqual(1.2599f, RevealSchedule.LadderPitch(3), 0.0001f);
            Assert.AreEqual(1.4983f, RevealSchedule.LadderPitch(4), 0.0001f);
            Assert.AreEqual(1.6818f, RevealSchedule.LadderPitch(5), 0.0001f);
            Assert.AreEqual(2f, RevealSchedule.LadderPitch(6), 0.0001f);
            Assert.AreEqual(1f, RevealSchedule.LadderPitch(0), 0.0001f, "below the root clamps to it");
        }
    }
}
