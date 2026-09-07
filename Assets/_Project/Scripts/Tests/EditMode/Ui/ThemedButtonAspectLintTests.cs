using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The container kit refuses a mismatched aspect outright
    // (Ui.ValidateContainerAspect, Ui.ContainerAspectTolerance) because a
    // Container/FlagBanner is drawn Simple + preserveAspect: a wrong ratio
    // there pads or crops, not stretches. A themed BUTTON has no such refusal
    // -- UiEmitter.WireThemedButton's own comment (~line 505) states the
    // plate is stretched non-uniformly to the button rect on purpose, because
    // a fitted plate floating in dead transparent space reads as more broken
    // than a stretched one. That tradeoff only holds while the stretch is
    // small. The 2026-09-07 regeneration put every plate shape at its EXACT
    // nominal aspect (button_plate_* / _3x1 3.0, _5x1 5.0, row_plate_*_6x1
    // 6.0 -- see ButtonPlateArt's own header), which means every rect that
    // was fitted against the OLD, off-nominal art now stretches by however
    // much the two disagree -- a 220x60 button (3.67:1) wears the 3.0:1
    // plate stretched 22%, thinning its side rims relative to its top and
    // bottom.
    //
    // This test is the container-side rule's mirror for buttons: same
    // Ui.ContainerAspectTolerance band, same "declared rect vs. the art's
    // own exact aspect" comparison, but reporting rather than throwing --
    // Ui.Themed()/ThemedPlate() build the button's tree at DECLARATION time,
    // long before a screen's own size is something this suite could still
    // change, so refusing here would just move the crash into every screen's
    // Build() instead of catching it once in one place.
    //
    // EXPECTED TO FAIL on the current tree. The kit's regeneration is exactly
    // what created the mismatches below; fixing each screen's declared sizes
    // is the integrator's job, not this test's -- see the commit message for
    // why the tolerance is not the thing to move instead.
    public class ThemedButtonAspectLintTests
    {
        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in Walk(child)) yield return descendant;
            }
        }

        // Copied from ButtonFallbackLintTests.AllScreenRoots (screens are
        // built straight from their own static Build(), not through
        // ScreenRegistry, for the same Editor-assembly reason that file's own
        // comment gives) plus PartyScreen, which that list does not carry yet.
        // PartyInputs(3) matches PartyScreenTests' own roster count -- the
        // three characters characters.json actually authors.
        private static IEnumerable<UiNode> AllScreenRoots()
        {
            yield return CharacterDossierScreen.Build().Root;
            yield return DebugMenuScreen.Build().Root;
            yield return DefeatScreen.Build().Root;
            yield return ExitsScreen.Build().Root;
            yield return FightScreen.Build().Root;
            yield return GlossaryScreen.Build().Root;
            yield return HubScreen.Build().Root;
            yield return MainMenuScreen.Build(new MainMenuInputs(5)).Root;
            yield return MapScreen.Build().Root;
            yield return OptionsScreen.Build().Root;
            yield return PartyScreen.Build(new PartyInputs(3)).Root;
            yield return ReckoningScreen.Build().Root;
            yield return RelicDraftScreen.Build().Root;
            yield return RewardTrackScreen.Build().Root;
            yield return RunStatsScreen.Build().Root;
            yield return SystemMenuScreen.Build().Root;
            yield return TalentScreen.Build().Root;
        }

        // Themed()/ThemedPlate() are the only two writers of node.Theme (see
        // UiNode.Theme's own comment) -- either marks a button as wearing a
        // plate, so HasValue alone is the same test ButtonFallbackLintTests'
        // ResolvesToFallbackPlate uses for "carries no Theme".
        private static bool WearsAPlate(UiNode node) =>
            node.Kind == UiNodeKind.Button && node.Theme.HasValue;

        // The shape UiEmitter will actually draw this button with: its own
        // .Plate() override if it declared one, else whatever ShapeFor picks
        // for its rect -- exactly the `node.PlateShapeOverride ?? PlateShapeFor(...)`
        // line Ui.ApplyTheme/ApplyThemePlateOnly both use to build Visuals.
        // Ui.PlateAspectError/PlateNominalSizeFor cannot be reused as-is here
        // because their OWN contract (see ButtonPlateArt.AspectError's
        // comment) is "against the shape ShapeFor picks" -- silent on an
        // override -- so a node with .Plate() would be scored against the
        // wrong picture. Recomputing against the resolved shape with the same
        // Ui.PlateAspect table those helpers already share is what "honoured"
        // means here.
        private static ButtonPlateShape ShapeWorn(UiNode node, float width, float height) =>
            node.PlateShapeOverride ?? Ui.PlateShapeFor(width, height);

        private struct Offender
        {
            public string Text;
            public float Error;
        }

        [Test]
        public void NoThemedButtonStretchesItsPlateBeyondTheContainerKitsTolerance()
        {
            var offenders = new List<Offender>();
            int checkedCount = 0;
            int skippedCount = 0;

            foreach (var root in AllScreenRoots())
            {
                foreach (var node in Walk(root))
                {
                    if (!WearsAPlate(node)) continue;

                    // Declared Size is enough -- this walks the tree Themed()/
                    // ThemedPlate() already built at declaration time, not a
                    // solved layout, so a Fill/FromChildren button (the fight's
                    // enemy hit areas, sized from the runtime sprite on load,
                    // per Ui.Button's own Place/UiSize overload comment) has no
                    // fixed number to measure here at all. UiSolver.Solve could
                    // produce one per canvas aspect, but that rect would be the
                    // STAGE SLOT's, not an aspect the art itself was fitted to,
                    // so it would not be testing the thing this lint exists to
                    // catch.
                    if (node.Size.ModeX != UiSizeMode.Fixed || node.Size.ModeY != UiSizeMode.Fixed)
                    {
                        skippedCount++;
                        continue;
                    }

                    checkedCount++;

                    float width = node.Size.X;
                    float height = node.Size.Y;
                    var shape = ShapeWorn(node, width, height);
                    float nominalAspect = Ui.PlateAspect(shape);
                    float rectAspect = width / height;
                    float error = Math.Abs(rectAspect / nominalAspect - 1f);

                    if (error > Ui.ContainerAspectTolerance)
                    {
                        float nominalHeight = width / nominalAspect;
                        offenders.Add(new Offender
                        {
                            Error = error,
                            Text = $"{root.Name} / {node.Name}: {width:0.#}x{height:0.#} = {rectAspect:0.###}:1, " +
                                   $"wears {shape} {nominalAspect:0.###}:1, off by {error:P1} -> " +
                                   $"{width:0.#}x{nominalHeight:0.#}",
                        });
                    }
                }
            }

            var message = $"{offenders.Count} of {checkedCount} themed buttons ({skippedCount} skipped for " +
                "Fill/FromChildren sizing) stretch their plate past the " +
                $"{Ui.ContainerAspectTolerance:P0} Ui.ContainerAspectTolerance band the container kit already " +
                "enforces for itself -- UiEmitter draws a themed plate Simple + non-uniform-stretch (no " +
                "preserveAspect), so a rect this far from its plate's own exact aspect visibly thins one pair of " +
                "rims relative to the other. Resize the offending rect toward Ui.PlateNominalSizeFor(width, " +
                "height), or force a better-fitting shape with .Plate(...), or accept the theme's own shape at a " +
                "size that already matches it:\n" +
                string.Join("\n", offenders.OrderByDescending(o => o.Error).Select(o => o.Text));

            Assert.IsEmpty(offenders, message);
        }

        // ButtonPlateArt.AspectError/NominalSizeFor's own arithmetic, pinned
        // against literal expected values per docs/CODE_STANDARDS.md #8 --
        // never recomputed from the method under test. Each case is a real
        // shape ButtonPlateArtTests already pins ShapeFor to, so this only
        // adds the error/nominal-size half.
        [TestCase(340f, 66f, ButtonPlateShape.FiveByOne, 0.030f, 340f, 68f)]
        [TestCase(220f, 60f, ButtonPlateShape.Legacy, 0.222f, 220f, 73.33f)]
        [TestCase(300f, 50f, ButtonPlateShape.Row6x1, 0f, 300f, 50f)]
        public void AspectErrorAndNominalSize_ArePinned(
            float width, float height, ButtonPlateShape expectedShape,
            float expectedError, float expectedNominalWidth, float expectedNominalHeight)
        {
            Assert.AreEqual(expectedShape, Ui.PlateShapeFor(width, height));

            float error = Ui.PlateAspectError(width, height);
            Assert.AreEqual(expectedError, error, 0.001f);

            var nominal = Ui.PlateNominalSizeFor(width, height);
            Assert.AreEqual(expectedNominalWidth, nominal.X, 0.01f);
            Assert.AreEqual(expectedNominalHeight, nominal.Y, 0.01f);
        }
    }
}
