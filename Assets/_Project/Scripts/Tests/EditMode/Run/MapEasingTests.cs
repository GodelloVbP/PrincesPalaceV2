using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The easing mask (docs/PLAN_RUN_MAP.md section 1, P6) as numbers: which
    // discs a run produces and how eased the wood is at a point. The shader
    // evaluates the same curve per pixel, so these literals are its spec.
    public class MapEasingTests
    {
        [Test]
        public void TheLiterals()
        {
            Assert.AreEqual(40, MapEasing.ShaderCapacity, "MapEasing.shader declares _Discs[40]");
            Assert.AreEqual(300f, MapEasing.NodeRadius);
            Assert.AreEqual(160f, MapEasing.HeartRadiusPerAnchor);
            Assert.AreEqual(640f, MapEasing.HeartMaxRadius);
            Assert.AreEqual(0.5f, MapEasing.SolidShare, "the shader's Falloff uses radius * 0.5");
            Assert.LessOrEqual(MapLayout.MaxNodes + 1, MapEasing.ShaderCapacity, "every room of a full realm plus the heart fits");
        }

        [TestCase(0f, 1f)]
        [TestCase(150f, 1f)]
        [TestCase(225f, 0.5f)]
        [TestCase(300f, 0f)]
        [TestCase(900f, 0f)]
        public void AFullyEasedCoreFadesToTheFoundWoodAtTheRadius(float distance, float expected) =>
            Assert.AreEqual(expected, MapEasing.Falloff(distance, 300f), 0.0001f);

        [Test]
        public void ADiscOfNoRadiusEasesNothing() =>
            Assert.AreEqual(0f, MapEasing.Falloff(0f, 0f));

        [Test]
        public void NothingWalkedLeavesTheWholeRealmAsFound()
        {
            var discs = MapEasing.Discs(new UiVec[0], new UiVec(0f, -2000f), slainAnchors: 0);

            Assert.IsEmpty(discs);
            Assert.AreEqual(0f, MapEasing.Coverage(discs, 0f, -340f));
        }

        [Test]
        public void EachWalkedRoomEasesAroundItself_AndTheRoadBetweenTwoOfThem()
        {
            var discs = MapEasing.Discs(new[] { new UiVec(0f, -340f), new UiVec(0f, -564f) }, new UiVec(0f, -2000f), 0);

            Assert.AreEqual(2, discs.Count);
            Assert.AreEqual(1f, MapEasing.Coverage(discs, 0f, -340f), 0.0001f, "on a walked room");
            Assert.AreEqual(1f, MapEasing.Coverage(discs, 0f, -452f), 0.0001f, "on the road between two rooms a row apart");
            Assert.AreEqual(0f, MapEasing.Coverage(discs, 0f, -1200f), 0.0001f, "a room never walked stays found");
            Assert.AreEqual(0f, MapEasing.Coverage(discs, 700f, -340f), 0.0001f, "a lateral room never walked stays found");
        }

        [Test]
        public void TheHeartEasesOneStepPerFallenAnchor_UpToItsCeiling()
        {
            Assert.AreEqual(0f, MapEasing.HeartRadius(0));
            Assert.AreEqual(160f, MapEasing.HeartRadius(1));
            Assert.AreEqual(480f, MapEasing.HeartRadius(3));
            Assert.AreEqual(640f, MapEasing.HeartRadius(4));
            Assert.AreEqual(640f, MapEasing.HeartRadius(6));

            var heart = new UiVec(0f, -2000f);
            Assert.IsEmpty(MapEasing.Discs(null, heart, 0), "an unbroken heart is found, not eased");

            var one = MapEasing.Discs(null, heart, 1);
            Assert.AreEqual(1, one.Count);
            Assert.AreEqual(1f, MapEasing.Coverage(one, 0f, -2000f), 0.0001f);
            Assert.AreEqual(0f, MapEasing.Coverage(one, 0f, -1800f), 0.0001f, "200 away is past one anchor's 160");

            var three = MapEasing.Discs(null, heart, 3);
            Assert.Greater(MapEasing.Coverage(three, 0f, -1800f), 0.4f, "three anchors down reach it");
        }

        [Test]
        public void ARealmPastTheShadersArrayDropsTheSurplusAndKeepsTheHeart()
        {
            var many = new List<UiVec>();
            for (int i = 0; i < 100; i++) many.Add(new UiVec(0f, -i * 100f));

            var discs = MapEasing.Discs(many, new UiVec(0f, -9000f), 2);

            Assert.AreEqual(MapEasing.ShaderCapacity, discs.Count);
            Assert.AreEqual(-9000f, discs[discs.Count - 1].Y, "the heart disc is last");
            Assert.AreEqual(320f, discs[discs.Count - 1].Radius);
        }
    }
}
