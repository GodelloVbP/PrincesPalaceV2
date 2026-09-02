using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Ui.PlateShapeFor / ButtonPlateArt: which of the four measured plate
    // shapes (Legacy 2.79, ThreeByOne 3.10, FiveByOne 3.42, Row6x1 4.92) a
    // themed button's own declared rect resolves to, and UiNode.Plate()'s
    // override of that pick.
    public class ButtonPlateArtTests
    {
        // --- the selection rule itself, pinned against the real rects this
        // codebase actually declares (FightScreen's verb rows, HubScreen's
        // MainMenuButton, FightScreen's TargetCancelButton, RelicDraftScreen's
        // continue button) -- see ButtonPlateArt.ShapeFor's own comment for
        // why it is ratio (min |ln(rect/plate)|), not absolute, distance.

        [Test]
        public void VerbRow_300x52_PicksRow6x1()
        {
            // FightScreen.VerbRowW/VerbRowH. Aspect 5.769 sits 0.160 (ln) from
            // Row6x1 and at least 0.36 from every other shape.
            Assert.AreEqual(ButtonPlateShape.Row6x1, Ui.PlateShapeFor(300f, 52f));
        }

        [Test]
        public void HubMainMenuButton_220x60_PicksFiveByOne()
        {
            // HubScreen.MainMenuButton (also DamagePopup's own incidental
            // 220x60, and close enough to MainMenuScreen's various 200-260
            // wide Silver buttons to stand in for the whole cluster). Aspect
            // 3.667 sits 0.070 (ln) from FiveByOne, its nearest neighbour.
            Assert.AreEqual(ButtonPlateShape.FiveByOne, Ui.PlateShapeFor(220f, 60f));
        }

        [Test]
        public void TargetCancelButton_88x30_PicksLegacy()
        {
            // FightScreen.TargetCancelButton. Aspect 2.933 sits 0.050 (ln)
            // from Legacy versus 0.055 from ThreeByOne - the closest contest
            // in the codebase's real rects, and Legacy still wins it.
            Assert.AreEqual(ButtonPlateShape.Legacy, Ui.PlateShapeFor(88f, 30f));
        }

        [Test]
        public void RelicDraftContinueButton_320x64_PicksRow6x1()
        {
            // RelicDraftScreen's continue button. Aspect 5.0 sits 0.016 (ln)
            // from Row6x1 - not close to any other shape.
            Assert.AreEqual(ButtonPlateShape.Row6x1, Ui.PlateShapeFor(320f, 64f));
        }

        [Test]
        public void SquareButton_PicksLegacy_TheNarrowestShape()
        {
            Assert.AreEqual(ButtonPlateShape.Legacy, Ui.PlateShapeFor(100f, 100f));
        }

        [Test]
        public void ExactlyAtAShapesOwnAspect_PicksThatShape()
        {
            Assert.AreEqual(ButtonPlateShape.ThreeByOne, Ui.PlateShapeFor(310f, 100f));
            Assert.AreEqual(ButtonPlateShape.FiveByOne, Ui.PlateShapeFor(342f, 100f));
        }

        [TestCase(0f, 52f)]
        [TestCase(300f, 0f)]
        [TestCase(-10f, 52f)]
        public void NonPositiveRect_Throws(float w, float h)
        {
            Assert.Throws<System.ArgumentException>(() => Ui.PlateShapeFor(w, h));
        }

        // --- SpriteKey per shape, so the selection actually reaches the
        // right PNG rather than just naming the right enum value ------------

        [TestCase(ButtonTheme.Gold, ButtonPlateShape.Legacy, "UI/Buttons/Processed/button_plate_gold.png")]
        [TestCase(ButtonTheme.Crimson, ButtonPlateShape.ThreeByOne, "UI/Buttons/Processed/button_plate_crimson_3x1.png")]
        [TestCase(ButtonTheme.Violet, ButtonPlateShape.FiveByOne, "UI/Buttons/Processed/button_plate_violet_5x1.png")]
        [TestCase(ButtonTheme.Blue, ButtonPlateShape.Row6x1, "UI/Buttons/Processed/row_plate_blue_6x1.png")]
        public void Themed_PlateSpriteKey_MatchesThemeAndSelectedShape(ButtonTheme theme, ButtonPlateShape shape, string expectedKey)
        {
            // .Plate() forces the shape directly - the rect below (100x100,
            // square) would otherwise pick Legacy on its own, so this proves
            // the override reaches ApplyTheme's plate key rather than
            // relying on a rect shaped to land on each case naturally.
            var node = Ui.Button("Btn", UiStrings.Cancel, new UiVec(100f, 100f)).Plate(shape).Themed(theme);

            var visuals = node.Children.Single(c => c.Name == "Visuals");
            var plate = visuals.Children.Single(c => c.Name == "Plate");
            Assert.AreEqual(expectedKey, plate.SpriteKey);
        }

        // --- .Plate() override: forces a shape the rect's own aspect would
        // not have picked ------------------------------------------------------

        [Test]
        public void Plate_Override_WinsOverTheRectsOwnNearestShape()
        {
            // 88x30 (Legacy, by the earlier pinned test) forced to Row6x1.
            var node = Ui.Button("Btn", UiStrings.Cancel, new UiVec(88f, 30f))
                .Plate(ButtonPlateShape.Row6x1)
                .Themed(ButtonTheme.Silver);

            var visuals = node.Children.Single(c => c.Name == "Visuals");
            var plate = visuals.Children.Single(c => c.Name == "Plate");
            Assert.AreEqual("UI/Buttons/Processed/row_plate_silver_6x1.png", plate.SpriteKey);
        }

        [Test]
        public void Plate_Override_AlsoAppliesThroughThemedPlate_TheCaptionPreservingPath()
        {
            var node = Ui.Button("Btn", UiString.Runtime, new UiVec(88f, 30f))
                .Plate(ButtonPlateShape.ThreeByOne)
                .ThemedPlate(ButtonTheme.Green);

            var visuals = node.Children.Single(c => c.Name == "Visuals");
            var plate = visuals.Children.Single(c => c.Name == "Plate");
            Assert.AreEqual("UI/Buttons/Processed/button_plate_green_3x1.png", plate.SpriteKey);
        }

        [Test]
        public void NoOverride_UsesTheRectNearestShape_ForThemedPlateToo()
        {
            var node = Ui.Button("VerbRow", UiString.Runtime, new UiVec(300f, 52f))
                .ThemedPlate(ButtonTheme.Crimson);

            var visuals = node.Children.Single(c => c.Name == "Visuals");
            var plate = visuals.Children.Single(c => c.Name == "Plate");
            Assert.AreEqual("UI/Buttons/Processed/row_plate_crimson_6x1.png", plate.SpriteKey);
        }
    }
}
