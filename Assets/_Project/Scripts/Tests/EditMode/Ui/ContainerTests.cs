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

        // --- FiveByOne: container-only, NO CALLER YET ----------------------
        //
        // container_<theme>_5x1.png arrived with the 2026-09-07 regeneration
        // and nothing in the game asks for it. These three cases are what
        // make it reachable rather than dead art on disk: the aspect a screen
        // would have to declare, the refusal if it declares a different one,
        // and the sprite key the declaration resolves to. If a caller lands
        // later it inherits an already-pinned surface.

        [Test]
        public void ContainerFiveByOne_AtItsOwnAspect_DoesNotThrow()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.FiveByOne, 120f);

            Assert.AreEqual(600f, size.X, 0.01f, "5:1 at height 120");
            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Green, ContainerRatio.FiveByOne, Place.At(0f, 0f), size));
        }

        [Test]
        public void ContainerFiveByOne_StretchedPastFivePercent_Throws()
        {
            // 600x120 is the exact 5:1 box; 660 wide is 5.5:1, 10% out.
            Assert.Throws<System.ArgumentException>(() =>
                Ui.Container("Frame", ButtonTheme.Green, ContainerRatio.FiveByOne, Place.At(0f, 0f),
                    new UiVec(660f, 120f)));
        }

        [Test]
        public void ContainerFiveByOne_HasNoFlagBannerArt()
        {
            // The kit ships no banner_flag_<theme>_5x1.png, so asking for one
            // is a mistake ContainerArt catches rather than a key that names
            // a file which does not exist.
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                Ui.FlagBanner("Banner", ButtonTheme.Green, ContainerRatio.FiveByOne, Place.At(0f, 0f),
                    new UiVec(600f, 120f)));
        }

        [TestCase(ButtonTheme.Gold, "UI/Buttons/Processed/container_gold_5x1.png")]
        [TestCase(ButtonTheme.Silver, "UI/Buttons/Processed/container_silver_5x1.png")]
        public void ContainerFiveByOne_SpriteKeyMatchesTheme(ButtonTheme theme, string expectedKey)
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.FiveByOne, 120f);
            var node = Ui.Container("Frame", theme, ContainerRatio.FiveByOne, Place.At(0f, 0f), size);

            Assert.AreEqual(expectedKey, node.Children.Single().SpriteKey);
        }

        [Test]
        public void FiveByOneContent_PassesUiAudit_AtAllFourAspects()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.FiveByOne, 120f);
            var holder = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.FiveByOne, Place.At(0f, 0f), size);
            Ui.ContainerContent(holder, ContainerRatio.FiveByOne, "FrameContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f), place: Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), holder);
            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        // --- the second delivery: ThreeByTwo/TwoByOne, container-only -------

        [Test]
        public void ContainerThreeByTwo_AtItsOwnMeasuredAspect_DoesNotThrow()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByTwo, 341f);
            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByTwo, Place.At(0f, 0f), size));
        }

        [Test]
        public void ContainerTwoByOne_AtItsOwnMeasuredAspect_DoesNotThrow()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.TwoByOne, 271f);
            Assert.DoesNotThrow(() =>
                Ui.Container("Frame", ButtonTheme.Crimson, ContainerRatio.TwoByOne, Place.At(0f, 0f), size));
        }

        [Test]
        public void ContainerThreeByTwo_StretchedPastFivePercent_Throws()
        {
            var baseline = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByTwo, 341f);
            var wrongShape = new UiVec(baseline.X * 1.10f, baseline.Y);

            Assert.Throws<System.ArgumentException>(() =>
                Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByTwo, Place.At(0f, 0f), wrongShape));
        }

        [TestCase(ButtonTheme.Gold, ContainerRatio.ThreeByTwo, "UI/Buttons/Processed/container_gold_3x2.png")]
        [TestCase(ButtonTheme.Violet, ContainerRatio.TwoByOne, "UI/Buttons/Processed/container_violet_2x1.png")]
        public void ContainerThreeByTwoOrTwoByOne_SpriteKeyMatchesThemeAndRatio(ButtonTheme theme, ContainerRatio ratio, string expectedKey)
        {
            float height = ratio == ContainerRatio.ThreeByTwo ? 341f : 271f;
            var size = Ui.ContainerSizeForHeight(ratio, height);
            var node = Ui.Container("Frame", theme, ratio, Place.At(0f, 0f), size);

            Assert.AreEqual(expectedKey, node.Children.Single().SpriteKey);
        }

        [Test]
        public void ThreeByTwoContent_PassesUiAudit_AtAllFourAspects()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.ThreeByTwo, 341f);
            var holder = Ui.Container("Frame", ButtonTheme.Gold, ContainerRatio.ThreeByTwo, Place.At(0f, 0f), size);
            Ui.ContainerContent(holder, ContainerRatio.ThreeByTwo, "FrameContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f), place: Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), holder);
            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void TwoByOneContent_PassesUiAudit_AtAllFourAspects()
        {
            var size = Ui.ContainerSizeForHeight(ContainerRatio.TwoByOne, 271f);
            var holder = Ui.Container("Frame", ButtonTheme.Crimson, ContainerRatio.TwoByOne, Place.At(0f, 0f), size);
            Ui.ContainerContent(holder, ContainerRatio.TwoByOne, "FrameContent",
                Ui.Label("Label", UiStrings.Cancel, new UiVec(100f, 30f), place: Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), holder);
            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
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

            // LITERAL, worked by hand from ContainerArt's own tables rather
            // than recomputed from the production formula this test is
            // supposed to be checking (CLAUDE.md gotcha 5 -- a test that
            // recomputes a formula to build its own expected value is a
            // tautology and cannot catch that formula breaking).
            //
            // ContainerArt.ContainerAspect3x4 = 0.75, so at height 569:
            //   size.X = 569 * 0.75 = 426.75
            //   size.Y = 569
            // ContainerArt.Inset(Container, ThreeByFour) =
            //   left 0.069, right 0.069, top 0.052, bottom 0.055, so:
            //   Left   = 426.75 * 0.069 = 29.44575
            //   Right  = 426.75 * 0.069 = 29.44575
            //   Top    = 569    * 0.052 = 29.588
            //   Bottom = 569    * 0.055 = 31.295
            Assert.AreEqual(29.44575f, content.Place.Left, 0.01f);
            Assert.AreEqual(29.44575f, content.Place.Right, 0.01f);
            Assert.AreEqual(29.588f, content.Place.Top, 0.01f);
            Assert.AreEqual(31.295f, content.Place.Bottom, 0.01f);

            // The formula check stays too, as a second, independent-in-name
            // but not in fact assertion -- kept because it still catches a
            // caller/production DRIFT (Container built at a different size
            // than ContainerContent insets against), which the literal check
            // above cannot: it only proves the numbers ContainerArt SHOULD
            // produce at height 569 today, not that this call site keeps
            // agreeing with ContainerArt as both evolve.
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
