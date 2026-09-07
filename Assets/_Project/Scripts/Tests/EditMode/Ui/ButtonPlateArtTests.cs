using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Ui.PlateShapeFor / ButtonPlateArt: which of the four plate shapes
    // (Legacy 3.0, ThreeByOne 3.0, FiveByOne 5.0, Row6x1 6.0) a themed
    // button's own declared rect resolves to, and UiNode.Plate()'s override
    // of that pick.
    //
    // THE WHOLE TABLE MOVED 2026-09-07, when the kit was regenerated at true
    // nominal aspect: 2.79/3.10/4.91/5.92 became 3.0/3.0/5.0/6.0. Two
    // consequences the cases below pin. First, Legacy and ThreeByOne are now
    // the same number (and the same PNG, byte for byte), so ShapeFor's loop
    // ties and Legacy -- first in the loop -- always wins; ThreeByOne is
    // reachable only through an explicit .Plate() override, which costs
    // nothing because the two load identical pixels. Second, the boundary
    // between the 3.0 cluster and FiveByOne moved from 3.902 to 3.873 and
    // the FiveByOne/Row6x1 boundary from 5.39 to 5.477.
    //
    // Each real rect below whose pick changed is renamed and re-pinned to
    // the new pick rather than just re-asserted, so a future reader isn't
    // left wondering why a "PicksThreeByOne" test asserts Legacy.
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
            // FightScreen.VerbRowW/VerbRowH. Aspect 5.769 sits 0.039 (ln)
            // from Row6x1 (was 0.026 against the old 5.92) and 0.143 from
            // FiveByOne, its nearest rival.
            Assert.AreEqual(ButtonPlateShape.Row6x1, Ui.PlateShapeFor(300f, 52f));
        }

        [Test]
        public void HubMainMenuButton_220x60_PicksLegacy()
        {
            // HubScreen.MainMenuButton (also DamagePopup's own incidental
            // 220x60, and close enough to MainMenuScreen's various 200-260
            // wide Silver buttons to stand in for the whole cluster). Aspect
            // 3.667 sits 0.201 (ln) from the 3.0 cluster and 0.310 from
            // FiveByOne. It used to pick ThreeByOne at 3.10; now that Legacy
            // and ThreeByOne share 3.0 the tie goes to Legacy, which loads
            // the same PNG -- so the picture is unchanged and only the key
            // that names it moved.
            Assert.AreEqual(ButtonPlateShape.Legacy, Ui.PlateShapeFor(220f, 60f));
        }

        [Test]
        public void LegacyAndThreeByOne_TieAt3_0_AndLegacyWins()
        {
            // The tie is the point: button_plate_<theme>.png and
            // button_plate_<theme>_3x1.png are byte-identical after the
            // 2026-09-07 regeneration, both at 1536x512. Nothing in the game
            // can tell the two picks apart, and this pins WHICH ONE the
            // selection names so a reordering of ShapeFor's loop is a visible
            // change rather than a silent one.
            Assert.AreEqual(ButtonPlateShape.Legacy, Ui.PlateShapeFor(300f, 100f));
        }

        [Test]
        public void TargetCancelButton_88x30_PicksLegacy()
        {
            // FightScreen.TargetCancelButton. Aspect 2.933 sits 0.023 (ln)
            // from the 3.0 cluster and 0.533 from FiveByOne. It was the
            // closest contest in the codebase when Legacy (2.79) and
            // ThreeByOne (3.10) straddled it; they are the same number now,
            // so it is not close any more and the pick is unchanged.
            Assert.AreEqual(ButtonPlateShape.Legacy, Ui.PlateShapeFor(88f, 30f));
        }

        [Test]
        public void RelicDraftContinueButton_320x64_PicksFiveByOne()
        {
            // RelicDraftScreen's continue button. Aspect 5.0 is FiveByOne
            // EXACTLY now (it was 0.018 ln away from the spliced 4.91), and
            // 0.182 from Row6x1.
            Assert.AreEqual(ButtonPlateShape.FiveByOne, Ui.PlateShapeFor(320f, 64f));
        }

        [Test]
        public void SquareButton_PicksLegacy_TheNarrowestShape()  // narrowest is 3.0
        {
            Assert.AreEqual(ButtonPlateShape.Legacy, Ui.PlateShapeFor(100f, 100f));
        }

        [Test]
        public void ExactlyAtAShapesOwnAspect_PicksThatShape()
        {
            // 3.0 is pinned by LegacyAndThreeByOne_TieAt3_0_AndLegacyWins,
            // which owns that case and says why Legacy is the answer.
            Assert.AreEqual(ButtonPlateShape.FiveByOne, Ui.PlateShapeFor(500f, 100f));
            Assert.AreEqual(ButtonPlateShape.Row6x1, Ui.PlateShapeFor(600f, 100f));
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
