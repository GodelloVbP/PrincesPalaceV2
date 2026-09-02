using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Themed(): the plate-button vocabulary migrated screens use instead of
    // the shared button frame.
    //
    // TREE-LEVEL, deliberately, the same posture FightButtonMotionTests states
    // for the other two motion components: UiEmitter has exactly one code
    // path per node (Theme.HasValue routes to the Themed branch, which never
    // reaches the HoverScale/ButtonPressAnimator branch below it), so what is
    // worth pinning is the DECISION recorded on the node, not the GameObjects
    // an Editor-only emitter produces from it -- Tests/EditMode can only
    // reference Domain (see CODE_STANDARDS.md 1), so UiEmitter itself is not
    // reachable from here at all.
    public class ThemedButtonTests
    {
        private static UiNode Build(ButtonTheme theme, string name = "TestButton") =>
            Ui.Button(name, UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f)).Themed(theme);

        private static UiNode Find(UiNode root, string name) =>
            Walk(root).FirstOrDefault(n => n.Name == name);

        private static System.Collections.Generic.IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in Walk(child)) yield return descendant;
            }
        }

        // --- the shape ----------------------------------------------------------

        [Test]
        public void AThemedButtonsTreeHasVisualsWithGlowAndPlateAndALabel()
        {
            var button = Build(ButtonTheme.Gold);

            Assert.AreEqual(2, button.Children.Count,
                "a themed button declares exactly Visuals and its Label - no more, no less");

            var visuals = Find(button, "Visuals");
            Assert.IsNotNull(visuals, "no Visuals child - Ui.ApplyTheme should have built one");
            Assert.AreEqual(UiNodeKind.Panel, visuals.Kind);

            var glow = visuals.Children.FirstOrDefault(n => n.Name == "Glow");
            var plate = visuals.Children.FirstOrDefault(n => n.Name == "Plate");
            Assert.IsNotNull(glow, "Visuals has no Glow child");
            Assert.IsNotNull(plate, "Visuals has no Plate child");
            Assert.AreEqual(UiNodeKind.Sprite, glow.Kind);
            Assert.AreEqual(UiNodeKind.Sprite, plate.Kind);

            var label = Find(button, "TestButtonLabel");
            Assert.IsNotNull(label, "no '<name>Label' child - a themed button declares its own caption");
            Assert.AreEqual(UiNodeKind.Label, label.Kind);
        }

        [TestCase(ButtonTheme.Gold, "gold")]
        [TestCase(ButtonTheme.Crimson, "crimson")]
        [TestCase(ButtonTheme.Violet, "violet")]
        [TestCase(ButtonTheme.Blue, "blue")]
        [TestCase(ButtonTheme.Green, "green")]
        [TestCase(ButtonTheme.Silver, "silver")]
        public void ThePlateSpriteMatchesTheTheme(ButtonTheme theme, string key)
        {
            var plate = Find(Build(theme), "Plate");

            Assert.AreEqual($"UI/Buttons/Processed/button_plate_{key}.png", plate.SpriteKey);
        }

        [Test]
        public void GlowAndPlateAreLayered_NotOverlapAllowed()
        {
            var visuals = Find(Build(ButtonTheme.Gold), "Visuals");
            var glow = visuals.Children.First(n => n.Name == "Glow");
            var plate = visuals.Children.First(n => n.Name == "Plate");

            Assert.IsNotNull(glow.LayerGroup, "Glow must carry a LayerGroup token, not an AllowOverlap");
            Assert.AreSame(glow.LayerGroup, plate.LayerGroup, "Glow and Plate must share ONE LayerGroup token");
            Assert.IsNull(glow.AllowOverlapReason);
            Assert.IsNull(plate.AllowOverlapReason);
        }

        [Test]
        public void VisualsAndLabelAreLayered_NotOverlapAllowed()
        {
            var button = Build(ButtonTheme.Gold);
            var visuals = Find(button, "Visuals");
            var label = Find(button, "TestButtonLabel");

            Assert.IsNotNull(visuals.LayerGroup, "Visuals must carry a LayerGroup token, not an AllowOverlap");
            Assert.AreSame(visuals.LayerGroup, label.LayerGroup, "Visuals and its Label must share ONE LayerGroup token");
            Assert.IsNull(visuals.AllowOverlapReason);
            Assert.IsNull(label.AllowOverlapReason);
        }

        // --- no ButtonPressAnimator / SubtleHoverScale ---------------------------
        //
        // UiEmitter.EmitButton's Theme.HasValue branch returns before the
        // HoverScale/ButtonPressAnimator code is ever reached, so this is the
        // tree-level guarantee that a themed node never even asks for either:
        // a themed button that also carried HoverScale would be an
        // unreachable, silently-ignored combination, the exact shape
        // FightButtonMotionTests' QuietIsNeverCombinedWithHovers test polices
        // for a different pair of motion flags.
        [Test]
        public void AThemedButtonNeverAlsoCarriesAHoverScale()
        {
            var button = Build(ButtonTheme.Gold);

            Assert.AreEqual(0f, button.HoverScale,
                "Themed() and Hovers() would fight over which motion component the button gets - " +
                "UiEmitter's themed path never even checks HoverScale, so this would silently do nothing");
        }

        // --- the audit ------------------------------------------------------------

        [Test]
        public void AThemedRoot_PassesUiAudit_AtAllFourAspects()
        {
            var button = Build(ButtonTheme.Violet);
            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), button);

            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void TheOnlyOverlapExemptionAThemedButtonUses_IsLayerGroup_NotAllowOverlap()
        {
            // The whole point of LayerGroup over AllowOverlap: AllowOverlap is
            // a blanket that also waives a node's OTHER, unrelated overlaps.
            // A themed button must not carry one anywhere in its declared
            // subtree - see the two Layered-specific tests above for the
            // pairs that would otherwise need one.
            var offenders = Walk(Build(ButtonTheme.Gold))
                .Where(n => n.AllowOverlapReason != null)
                .Select(n => n.Name)
                .ToList();

            CollectionAssert.IsEmpty(offenders,
                "these themed-button nodes carry AllowOverlap instead of LayerGroup: " + string.Join(", ", offenders));
        }

        // --- the guard rails --------------------------------------------------------

        [Test]
        public void ThemedOnANonButtonNode_Throws()
        {
            var panel = Ui.Panel("SomePanel", UiSize.Fixed(100f, 100f));

            Assert.Throws<System.ArgumentException>(() => panel.Themed(ButtonTheme.Gold));
        }

        [Test]
        public void ThemedOnAChromelessButton_Throws()
        {
            var button = Ui.Button("SomeButton", UiStrings.Cancel, new UiVec(200f, 60f), 20).NoChrome();

            Assert.Throws<System.ArgumentException>(() => button.Themed(ButtonTheme.Gold));
        }

        // --- ButtonTracking -----------------------------------------------------

        [Test]
        public void ButtonTracking_TracksShortWordsMost()
        {
            Assert.AreEqual(3f, Ui.ButtonTracking(UiStrings.Cancel));
        }

        [Test]
        public void ButtonTracking_LongWordsNotAtAll()
        {
            // "Manage Saves" is 12 characters - past the <=10 band.
            Assert.AreEqual(0f, Ui.ButtonTracking(UiStrings.ManageSaves));
        }
    }
}
