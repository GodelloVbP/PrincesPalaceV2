using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    public class SpriteFacingTests
    {
        [Test]
        public void CombatantsAlwaysFaceInward()
        {
            Assert.AreEqual(SpriteFacing.Right, StageFacing.DesiredFacingFor(StageSide.Left),
                "Someone standing on the left of the stage should be looking right, toward the enemy.");
            Assert.AreEqual(SpriteFacing.Left, StageFacing.DesiredFacingFor(StageSide.Right),
                "Someone standing on the right of the stage should be looking left, toward the party.");
        }

        // The two enemy sheets authored so far (Bog Witch, Stone Golem) are
        // both drawn facing RIGHT, and they stand on the RIGHT of the stage
        // — so both need mirroring. This is the case a global "all art faces
        // left" assumption would have got backwards.
        [Test]
        public void RightFacingArt_OnTheRightSide_IsMirrored()
        {
            Assert.IsTrue(StageFacing.ShouldMirror(SpriteFacing.Right, StageSide.Right));
            Assert.AreEqual(-1f, StageFacing.MirrorScaleX(SpriteFacing.Right, StageSide.Right));
        }

        [Test]
        public void RightFacingArt_OnTheLeftSide_IsLeftAlone()
        {
            Assert.IsFalse(StageFacing.ShouldMirror(SpriteFacing.Right, StageSide.Left));
            Assert.AreEqual(1f, StageFacing.MirrorScaleX(SpriteFacing.Right, StageSide.Left));
        }

        [Test]
        public void LeftFacingArt_MirrorsOnTheOppositeSideFromRightFacingArt()
        {
            Assert.IsTrue(StageFacing.ShouldMirror(SpriteFacing.Left, StageSide.Left));
            Assert.IsFalse(StageFacing.ShouldMirror(SpriteFacing.Left, StageSide.Right));
        }

        [TestCase("left", SpriteFacing.Left)]
        [TestCase("right", SpriteFacing.Right)]
        [TestCase("  RIGHT  ", SpriteFacing.Right)]
        [TestCase("Left", SpriteFacing.Left)]
        public void TryParse_AcceptsAuthoredValues(string authored, SpriteFacing expected)
        {
            Assert.IsTrue(StageFacing.TryParse(authored, out var facing));
            Assert.AreEqual(expected, facing);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("sideways")]
        [TestCase("east")]
        public void TryParse_RejectsAnythingElse(string authored)
        {
            Assert.IsFalse(StageFacing.TryParse(authored, out _),
                "A typo'd facing must fail loudly at content-build time, not silently ship a monster facing the wrong way.");
        }
    }
}
