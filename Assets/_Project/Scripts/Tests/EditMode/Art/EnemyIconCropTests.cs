using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // THE ENEMY PLATE ICON'S CROP (EnemyIconCrop.HeadSquare), pinned with
    // literal numbers off the real beetle and rat stills (QA 2026-09-26: the
    // beetle's icon was empty and the rat's an ~8x4px sliver, because the
    // crop was the top band of the padded CANVAS, full width).
    public class EnemyIconCropTests
    {
        // beetle/idle.png: 830x413 canvas, opaque box (PIL, top-left origin)
        // x 285..753, y 161..405 -> bottom-left origin (285, 8, 468, 244).
        private static readonly CanvasRect BeetleOpaque = new CanvasRect(285f, 8f, 468f, 244f);

        [Test]
        public void TheFallbackCrop_IsASquareOnTheFigure_NotABandOfCanvasAir()
        {
            var crop = EnemyIconCrop.HeadSquare(830f, 413f, 8f, null, BeetleOpaque, 0.35f);

            // 0.35 * 244 = 85.4 on a side, top edge on the figure's top (252).
            Assert.AreEqual(85.4f, crop.Width, 0.01f, "the crop is square, so it fills a square icon");
            Assert.AreEqual(85.4f, crop.Height, 0.01f);
            Assert.AreEqual(252f, crop.Y + crop.Height, 0.01f,
                "top-aligned to the FIGURE's top, not the canvas's (413) -- the old band above 268 was empty");
            Assert.AreEqual(519f - 42.7f, crop.X, 0.01f, "centred across the figure (285 + 468/2 = 519)");
        }

        [Test]
        public void AnAuthoredHead_WinsAndUsesCastPointsConvention()
        {
            // The beetle's authored head: dx 245, dy 154, size 150 on an
            // 830-wide canvas with groundLine 8 -> centre (660, 162).
            var head = new HeadBoxSpec(245f, 154f, 150f);

            var crop = EnemyIconCrop.HeadSquare(830f, 413f, 8f, head, BeetleOpaque, 0.35f);

            Assert.AreEqual(585f, crop.X, 0.01f);
            Assert.AreEqual(87f, crop.Y, 0.01f);
            Assert.AreEqual(150f, crop.Width, 0.01f);
            Assert.AreEqual(150f, crop.Height, 0.01f);
        }

        [Test]
        public void ASquarePastTheCanvasEdge_IsClampedInsideIt()
        {
            // Rat canvas 616x306; a head authored hard against the top edge.
            var head = new HeadBoxSpec(0f, 290f, 130f);

            var crop = EnemyIconCrop.HeadSquare(616f, 306f, 8f, head, new CanvasRect(74f, 8f, 391f, 270f), 0.35f);

            Assert.AreEqual(306f, crop.Y + crop.Height, 0.01f, "never past the canvas top");
            Assert.AreEqual(233f, crop.Y, 0.01f, "centre 298, bottom edge 298 - 65");
            Assert.AreEqual(130f, crop.Width, 0.01f, "the unclamped axis keeps its full side");
        }

        [Test]
        public void TheManifest_ResolvesAnAuthoredHead_AndTreatsSizeZeroAsAbsent()
        {
            var manifest = new StanceManifest(new RawStanceManifest
            {
                actors = new List<RawStanceActor>
                {
                    new RawStanceActor { spritePath = "Enemies/beetle", groundLine = 8f,
                        head = new RawHeadBox { dx = 245f, dy = 154f, size = 150f } },
                    new RawStanceActor { spritePath = "Enemies/rat", groundLine = 8f,
                        head = new RawHeadBox() },
                },
            });

            var beetle = manifest.HeadFor("/Enemies/beetle/");
            Assert.IsTrue(beetle.HasValue);
            Assert.AreEqual(245f, beetle.Value.Dx, 0.001f);
            Assert.AreEqual(154f, beetle.Value.Dy, 0.001f);
            Assert.AreEqual(150f, beetle.Value.Size, 0.001f);

            Assert.IsNull(manifest.HeadFor("Enemies/rat"),
                "an all-zero block is JsonUtility's absence, not a zero-sized head");
            Assert.IsNull(manifest.HeadFor("Enemies/nobody"));
        }
    }
}
