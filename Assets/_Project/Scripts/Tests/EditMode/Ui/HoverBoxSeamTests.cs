using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Ui.Button(): the one seam the owner's 2026-09-23 (second pass)
    // hover-pop removal goes through. Before this pass, only the ~15
    // screens that remembered to call .Hovers() got a rim at all; now
    // Ui.Button's own AttachDefaultHoverBox builds one for EVERY fixed-size,
    // unthemed button, and .Hovers() itself is a no-op kept only so its
    // ~15 existing call sites keep compiling (HoverScale stays an inert
    // flag -- see UiNode.HoverScale's own header). TREE-LEVEL, same posture
    // ThemedButtonTests states for its own seam: UiEmitter has exactly one
    // code path per node, so what is worth pinning here is the shape
    // Ui.Button/Ui.ApplyHoverBox records, not the GameObjects an
    // Editor-only emitter produces from it.
    public class HoverBoxSeamTests
    {
        private static UiNode Build(string name = "TestButton") =>
            Ui.Button(name, UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f));

        [Test]
        public void APlainButtonGetsAHoverRimChildWithFourDecorEdges()
        {
            var button = Build();

            var rim = button.Children.SingleOrDefault(c => c.Name == "HoverRim");
            Assert.IsNotNull(rim, "Hovers() must build the HoverRim child Ui.ApplyHoverBox/UiEmitter.WireHoverBox depend on");
            Assert.IsTrue(rim.StartInactive, "the rim must start hidden -- HoverBox is what ever shows it");
            Assert.IsTrue(rim.Decor, "the rim must not steal clicks or trip UiAudit's sibling-overlap check");
            Assert.IsNotNull(rim.AllowOverflowReason,
                "the rim pads outward past the button on purpose and must say why, or UiAudit refuses it");

            // Owner's call, 2026-09-23 (second pass): pad 4, not 6 -- pinned
            // literal so a future pad change has to touch this test on
            // purpose, not slide past it (AUDIT.md #18's tautology lesson:
            // these are the authored numbers, not re-derived from HoverRimPad).
            //
            // STRETCHED OVER THE BUTTON, pad as a negative inset -- not a
            // fixed size copied from the button at build time. A map room is
            // declared at the boss tile's size and shrunk at runtime, and a
            // fixed-size rim kept drawing the boss frame around every smaller
            // room.
            const float pad = 4f;
            Assert.AreEqual(PlaceKind.Stretch, rim.Place.Kind, "the rim must follow the button's runtime size");
            Assert.AreEqual(-pad, rim.Place.Left, 0.001f);
            Assert.AreEqual(-pad, rim.Place.Right, 0.001f);
            Assert.AreEqual(-pad, rim.Place.Bottom, 0.001f);
            Assert.AreEqual(-pad, rim.Place.Top, 0.001f);

            // 2px edges -- a SOLID box, not the kit's 1px hairline Rim()
            // other hollow boxes (Options, Run statistics, Exits) use. The
            // hover rim deliberately does not share Rim()'s thickness. Each
            // edge is anchored to its own side, its thickness the one inset
            // that is not zero.
            const float thickness = 2f;
            var edgeNames = new[] { "HoverRimTop", "HoverRimBottom", "HoverRimLeft", "HoverRimRight" };
            foreach (var name in edgeNames)
            {
                var edge = rim.Children.SingleOrDefault(c => c.Name == name);
                Assert.IsNotNull(edge, $"'{name}' is missing from the rim");
                Assert.IsTrue(edge.Decor, $"'{name}' must be Decor, same as every other kit rim edge");
                Assert.AreEqual(PlaceKind.Frac, edge.Place.Kind, $"'{name}' must be anchored, not sized");

                float inset = name switch
                {
                    "HoverRimTop" => edge.Place.Bottom,
                    "HoverRimBottom" => edge.Place.Top,
                    "HoverRimLeft" => edge.Place.Right,
                    _ => edge.Place.Left,
                };
                Assert.AreEqual(-thickness, inset, 0.001f, $"'{name}' must be a {thickness}px solid edge, not a 1px hairline");
            }
        }

        // Hovers() is a no-op now, but every one of its ~15 existing call
        // sites still calls it, and none of them are Labels -- so the throw
        // this used to pin (a non-Button asking for the rim) has moved to
        // Ui.Button's own construction: a Label never goes through
        // AttachDefaultHoverBox in the first place, and Hovers() itself
        // no longer touches Ui.ApplyHoverBox at all. What is still true, and
        // still worth pinning, is that .Hovers() keeps compiling against a
        // non-Button node without throwing -- the no-op has no Kind check
        // to trip.
        [Test]
        public void HoversStaysANoOpEvenOnANonButtonNode()
        {
            var label = Ui.Label("TestLabel", UiStrings.Cancel, new UiVec(200f, 60f), 20, "#FFFFFF", Place.At(0f, 0f));

            Assert.DoesNotThrow(() => label.Hovers(1.02f),
                "Hovers() only sets the inert HoverScale flag now -- it must not throw on any node kind");
            Assert.IsEmpty(label.Children, "a no-op must not build a rim, on a Label or anything else");
        }

        // The field the seam keeps compiling every existing call site
        // against (see UiNode.HoverScale's own header) is still just an
        // inert flag underneath -- not read as a scale, and not read to
        // decide who gets the rim, by anything any more.
        [Test]
        public void TheHoverScaleFieldSurvivesAsAnInertFlag()
        {
            var button = Build().Hovers(1.03f);

            Assert.AreEqual(1.03f, button.HoverScale, 0.0001f,
                "the number itself is kept only so .Hovers(1.03f) call sites keep compiling unchanged");
        }

        // ---- the default itself ---------------------------------------------------
        //
        // Owner's rule, 2026-09-23 (second pass): every hoverable/focusable
        // control shows the rim, not only the ones a screen remembered to
        // opt into. These three pin who gets it and who does not.

        [Test]
        public void APlainButtonGetsTheRimWithoutCallingHoversAtAll()
        {
            var button = Ui.Button("PlainButton", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f));

            Assert.IsNotNull(button.Children.SingleOrDefault(c => c.Name == "HoverRim"),
                "an unthemed button must get the rim from Ui.Button itself, with no .Hovers() call needed");
        }

        [Test]
        public void AThemedButtonDoesNotGetTheRim()
        {
            var button = Ui.Button("ThemedButton", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f))
                .Themed(ButtonTheme.Gold);

            Assert.IsNull(button.Children.SingleOrDefault(c => c.Name == "HoverRim"),
                "a themed button wears ThemedButtonState's own Glow/Plate focus visual -- it must not also carry the rim");
        }

        [Test]
        public void ANoHoverBoxButtonDoesNotGetTheRim()
        {
            var button = Ui.Button("OptedOutButton", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f))
                .NoHoverBox("this cell already shows a selected-halo of its own, so a second rim would double it");

            Assert.IsNull(button.Children.SingleOrDefault(c => c.Name == "HoverRim"),
                "NoHoverBox() must remove the default rim Ui.Button already attached");
        }

        [Test]
        public void NoHoverBoxRequiresAReason()
        {
            var button = Ui.Button("NoReasonButton", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f));

            Assert.Throws<System.ArgumentException>(() => button.NoHoverBox(""),
                "an unexplained NoHoverBox() is indistinguishable from a bug someone silenced, same as AllowOverlap/AllowOverflow");
        }

        [Test]
        public void NoHoverBoxRemovesTheRimRegardlessOfCallOrder()
        {
            // AllowOverlap() first, NoHoverBox() last -- the opposite order
            // from ANoHoverBoxButtonDoesNotGetTheRim above, since nothing
            // about NoHoverBox()'s removal may depend on when it runs.
            var button = Ui.Button("LateOptOut", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f))
                .AllowOverlap("its own icon and label sit inside its declared rect by construction")
                .NoHoverBox("this row already shows its own selection arrow, so the rim would double it");

            Assert.IsNull(button.Children.SingleOrDefault(c => c.Name == "HoverRim"),
                "NoHoverBox() must strip the rim no matter what else was chained before it");
        }

        // A runtime-resized button (UiSize.Fill/FillWidth/FillHeight -- the
        // fight's hit areas) has no box to pad a rim around at declaration
        // time, so it must not get one by default.
        [Test]
        public void AFillSizedButtonDoesNotGetTheRim()
        {
            var button = Ui.Button("RuntimeSizedButton", UiStrings.Cancel,
                Place.Frac(new UiVec(0.1f, 0.1f), new UiVec(0.9f, 0.9f)), UiSize.Fill);

            Assert.IsNull(button.Children.SingleOrDefault(c => c.Name == "HoverRim"),
                "a Fill-sized button's box is not known at declaration time -- it must not get a padded rim built around a zero-size rect");
        }

        // No emitted button carries a scale-on-hover component any more --
        // this is the domain-level half of that guarantee (the other half,
        // that UiEmitter never adds a scaling MonoBehaviour, is not
        // reachable from a dotnet-host test at all, since EmitButton is
        // Editor/Unity-only). What the tree records is node.Scale, which
        // drives the ONE place a runtime scale could still come from
        // (EmitNode's `rect.localScale = ...` line); Hovers() writing only
        // the inert HoverScale float and never touching Scale is what makes
        // that line dead for every button Ui.Button ever builds.
        [Test]
        public void NoButtonEverCarriesAScaleTransformFromHovers()
        {
            var plain = Ui.Button("PlainScaleCheck", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f));
            var hovered = Ui.Button("HoveredScaleCheck", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f))
                .Hovers(1.16f);
            var themed = Ui.Button("ThemedScaleCheck", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f))
                .Themed(ButtonTheme.Gold);

            foreach (var button in new[] { plain, hovered, themed })
            {
                Assert.IsTrue(button.Scale.Equals(UiVec.One),
                    $"'{button.Name}' carries a non-default Scale -- the rim/plate replaced the scale-pop entirely, " +
                    "nothing should ever write node.Scale from a hover/focus path");
            }
        }
    }
}
