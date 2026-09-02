using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Ui.Container / Ui.FlagBanner: the exact-ratio themed frame kit, and
    // Ui.ContainerContent, the padded surface real content sits on.
    public class ContainerTests
    {
        // --- aspect refusal -------------------------------------------------

        [Test]
        public void Container_AtItsOwnMeasuredAspect_DoesNotThrow()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size));
        }

        [Test]
        public void Container_StretchedPastFivePercent_Throws()
        {
            // 569 tall at the 9:16 width instead of 3:4 - nowhere near either
            // measured aspect's 5% band.
            var wrongShape = new UiVec(200f, 569f);

            var ex = Assert.Throws<System.ArgumentException>(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), wrongShape));

            StringAssert.Contains("aspect", ex.Message);
        }

        [Test]
        public void Container_JustInsideFivePercent_DoesNotThrow()
        {
            var baseline = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var nudged = new UiVec(baseline.X * 1.04f, baseline.Y);

            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), nudged));
        }

        [Test]
        public void Container_JustOutsideFivePercent_Throws()
        {
            var baseline = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var nudged = new UiVec(baseline.X * 1.10f, baseline.Y);

            Assert.Throws<System.ArgumentException>(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), nudged));
        }

        [Test]
        public void FlagBanner_StretchedPastFivePercent_Throws()
        {
            var wrongShape = new UiVec(600f, 641f); // 3:4 banner height, way-too-wide width

            Assert.Throws<System.ArgumentException>(() =>
                Ui.FlagBanner("Banner", ButtonTheme.Silver, ContainerRatio.ThreeByFour, Place.At(0f, 0f), wrongShape));
        }

        [Test]
        public void FlagBanner_AtItsOwnMeasuredAspect_DoesNotThrow()
        {
            var size = Ui.FlagBannerSizeForHeight(ContainerRatio.NineBySixteen, 810f);
            Assert.DoesNotThrow(() =>
                Ui.FlagBanner("Banner", ButtonTheme.Blue, ContainerRatio.NineBySixteen, Place.At(0f, 0f), size));
        }

        [TestCase(0f, 100f)]
        [TestCase(100f, 0f)]
        [TestCase(-10f, 100f)]
        public void Container_NonPositiveSize_Throws(float w, float h)
        {
            Assert.Throws<System.ArgumentException>(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), new UiVec(w, h)));
        }

        // --- the size helper --------------------------------------------------

        [Test]
        public void ContainerSizeForHeight_ProducesAnAspectThatPasses()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.NineBySixteen, 400f);

            Assert.AreEqual(400f, size.Y);
            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Crimson, ContainerRatio.NineBySixteen, Place.At(0f, 0f), size));
        }

        [Test]
        public void ContainerSizeForWidth_ProducesAnAspectThatPasses()
        {
            var size = Ui.ContainerSizeForWidth(ContainerRatio.ThreeByFour, 250f);

            Assert.AreEqual(250f, size.X);
            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Violet, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size));
        }

        [Test]
        public void FlagBannerSizeForHeight_ProducesAnAspectThatPasses()
        {
            var size = Ui.FlagBannerSizeForHeight(ContainerRatio.ThreeByFour, 300f);

            Assert.DoesNotThrow(() =>
                Ui.FlagBanner("Banner", ButtonTheme.Green, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size));
        }

        // --- shape / flags ------------------------------------------------------
        //
        // Container()/FlagBanner() return a plain, non-Decor Panel wrapping the
        // themed frame art as its own Decor Sprite child -- not the Sprite
        // itself -- so that content added later via ContainerContent lands as
        // the frame's SIBLING and is never shadowed by the frame's own Decor
        // exemption. See Ui.BuildFrameHolder for why.

        [Test]
        public void Container_IsAPanel_WrappingADecorSpriteFrame_NotAButton()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var node = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);

            Assert.AreEqual(UiNodeKind.Panel, node.Kind);
            var frame = node.Children.Single();
            Assert.AreEqual(UiNodeKind.Sprite, frame.Kind);
        }

        [Test]
        public void Container_WrapperIsNotDecor_ButItsFrameArtIs()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var node = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);

            Assert.IsFalse(node.Decor,
                "the wrapper must not be Decor, or ContainerContent's content would inherit the exemption again");
            Assert.IsTrue(node.Children.Single().Decor, "the frame art itself is still non-interactive decoration");
        }

        [Test]
        public void Container_FrameArt_PreservesAspect()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var node = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);

            Assert.IsTrue(node.Children.Single().PreserveAspect);
        }

        [Test]
        public void FlagBanner_WrapperIsNotDecor_ButItsFrameArtIs()
        {
            var size = Ui.FlagBannerSizeForHeight(ContainerRatio.ThreeByFour, 640f);
            var node = Ui.FlagBanner("Banner", ButtonTheme.Silver, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);

            Assert.IsFalse(node.Decor);
            Assert.AreEqual(UiNodeKind.Panel, node.Kind);

            var frame = node.Children.Single();
            Assert.IsTrue(frame.Decor);
            Assert.IsTrue(frame.PreserveAspect);
            Assert.AreEqual(UiNodeKind.Sprite, frame.Kind);
        }

        [TestCase(ButtonTheme.Gold, ContainerRatio.ThreeByFour, "UI/Buttons/Processed/container_gold_3x4.png")]
        [TestCase(ButtonTheme.Crimson, ContainerRatio.NineBySixteen, "UI/Buttons/Processed/container_crimson_9x16.png")]
        [TestCase(ButtonTheme.Silver, ContainerRatio.ThreeByFour, "UI/Buttons/Processed/container_silver_3x4.png")]
        public void Container_SpriteKeyMatchesThemeAndRatio(ButtonTheme theme, ContainerRatio ratio, string expectedKey)
        {
            var height = ratio == ContainerRatio.ThreeByFour ? 569f : 699f;
            var size = Ui.ContainerSizeForHeight(ratio, height);
            var node = Ui.Container("Frame", theme, ratio, Place.At(0f, 0f), size);

            Assert.AreEqual(expectedKey, node.Children.Single().SpriteKey);
        }

        [TestCase(ButtonTheme.Violet, ContainerRatio.NineBySixteen, "UI/Buttons/Processed/banner_flag_violet_9x16.png")]
        [TestCase(ButtonTheme.Blue, ContainerRatio.ThreeByFour, "UI/Buttons/Processed/banner_flag_blue_3x4.png")]
        public void FlagBanner_SpriteKeyMatchesThemeAndRatio(ButtonTheme theme, ContainerRatio ratio, string expectedKey)
        {
            var height = ratio == ContainerRatio.ThreeByFour ? 640f : 810f;
            var size = Ui.FlagBannerSizeForHeight(ratio, height);
            var node = Ui.FlagBanner("Banner", theme, ratio, Place.At(0f, 0f), size);

            Assert.AreEqual(expectedKey, node.Children.Single().SpriteKey);
        }

        // --- ContainerContent: the inset --------------------------------------

        [Test]
        public void ContainerContent_IsAddedAsASiblingOfTheFrameArt_UnderTheHolder()
        {
            // Not a child of the frame -- see BuildFrameHolder/ContainerContent
            // in Ui.cs for why content has to sit BESIDE the Decor frame art
            // rather than under it.
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var holder = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);
            var frame = holder.Children.Single();
            var label = Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f));

            var content = Ui.ContainerContent(holder, ContainerRatio.ThreeByFour, "FrameContent", label);

            CollectionAssert.AreEqual(new[] { frame, content }, holder.Children);
            Assert.AreSame(label, content.Children.Single());
        }

        [Test]
        public void ContainerContent_DrawsNothingItself()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var holder = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);
            var content = Ui.ContainerContent(holder, ContainerRatio.ThreeByFour, "FrameContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f)));

            Assert.IsTrue(content.EmitsNoGraphic,
                "ContainerContent's own panel must draw nothing, or it would collide with the art it sits on");
        }

        [Test]
        public void ContainerContent_UsesStretchPlacement_InsetFromTheHolderOnAllFourSides()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var holder = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);
            var content = Ui.ContainerContent(holder, ContainerRatio.ThreeByFour, "FrameContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f)));

            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);

            var inset = Ui.ContainerContentInset(ContainerRatio.ThreeByFour);
            Assert.AreEqual(size.X * inset.Left, content.Place.Left, 0.01f);
            Assert.AreEqual(size.X * inset.Right, content.Place.Right, 0.01f);
            Assert.AreEqual(size.Y * inset.Top, content.Place.Top, 0.01f);
            Assert.AreEqual(size.Y * inset.Bottom, content.Place.Bottom, 0.01f);
        }

        [Test]
        public void FlagBannerContent_BottomInset_IsLargerThanContainers_ToClearTheV()
        {
            var containerInset = Ui.ContainerContentInset(ContainerRatio.ThreeByFour);
            var bannerInset = Ui.FlagBannerContentInset(ContainerRatio.ThreeByFour);

            Assert.Greater(bannerInset.Bottom, containerInset.Bottom,
                "a flag banner's bottom inset must clear the V-notch, which a plain container's border does not have");
        }

        [Test]
        public void ContainerContent_WithNullHolder_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                Ui.ContainerContent(null, ContainerRatio.ThreeByFour, "Content",
                    Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f))));
        }

        // --- the audit: content inside the inset passes at all four aspects ---

        [Test]
        public void AContainerWithLabelInsideItsInset_PassesUiAudit_AtAllFourAspects()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 569f);
            var holder = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);
            Ui.ContainerContent(holder, ContainerRatio.ThreeByFour, "FrameContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f), place: Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), holder);
            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void AFlagBannerWithLabelAboveTheV_PassesUiAudit_AtAllFourAspects()
        {
            var size = Ui.FlagBannerSizeForHeight(ContainerRatio.ThreeByFour, 640f);
            var holder = Ui.FlagBanner("Banner", ButtonTheme.Silver, ContainerRatio.ThreeByFour, Place.At(0f, 0f), size);
            Ui.ContainerContent(holder, ContainerRatio.ThreeByFour, "BannerContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(80f, 24f), place: Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), holder);
            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }
    }
}
