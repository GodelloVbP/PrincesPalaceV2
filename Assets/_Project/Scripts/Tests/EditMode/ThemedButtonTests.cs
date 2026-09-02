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

        // --- typography role -----------------------------------------------------

        [Test]
        public void AThemedButtonsLabel_CarriesTheButtonLabelRole()
        {
            var button = Build(ButtonTheme.Gold);
            var label = Find(button, "TestButtonLabel");

            Assert.AreEqual(TypographyRole.ButtonLabel, label.Role,
                "Ui.ApplyTheme should give every themed button's own label Role.ButtonLabel automatically - " +
                "that is the whole point of step 1, a screen never says .Styled(ButtonLabel) itself");
        }

        [Test]
        public void TheButtonNodeItself_CarriesNoRole()
        {
            // Role belongs to the LABEL, not the button that wears it - the
            // button node has no text of its own to be typeset.
            var button = Build(ButtonTheme.Gold);

            Assert.IsNull(button.Role);
        }

        // Forces Legacy so this stays a pure theme-to-key check: Build()'s
        // 200x60 rect resolves to FiveByOne on its own merits under
        // Ui.PlateShapeFor (see ButtonPlateArtTests, which covers shape
        // SELECTION), and that is a separate concern from whether a given
        // shape's key names the right theme.
        [TestCase(ButtonTheme.Gold, "gold")]
        [TestCase(ButtonTheme.Crimson, "crimson")]
        [TestCase(ButtonTheme.Violet, "violet")]
        [TestCase(ButtonTheme.Blue, "blue")]
        [TestCase(ButtonTheme.Green, "green")]
        [TestCase(ButtonTheme.Silver, "silver")]
        public void ThePlateSpriteMatchesTheTheme(ButtonTheme theme, string key)
        {
            var button = Ui.Button("TestButton", UiStrings.Cancel, new UiVec(200f, 60f), 20, Place.At(0f, 0f))
                .Plate(ButtonPlateShape.Legacy)
                .Themed(theme);
            var plate = Find(button, "Plate");

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

        // --- ThemedPlate: caption-preserving mode --------------------------------
        //
        // A composite button (FightScreen's verb rows) that already declares
        // its own caption children instead of a single centred string. See
        // UiNode.CaptionPreserving's own comment for why this needs a
        // separate entry point rather than a flag on Themed().

        private static UiNode BuildPlateOnly(ButtonTheme theme, string name = "TestButton")
        {
            var button = Ui.Button(name, UiString.Runtime, new UiVec(200f, 60f), 1, Place.At(0f, 0f))
                .ThemedPlate(theme);
            var caption = Ui.Label(name + "Caption", UiStrings.Cancel, new UiVec(160f, 30f), 20, null, Place.At(0f, 0f));
            button.Children.Add(caption);
            button.LayerCaptionWithVisuals(caption);
            return button;
        }

        [Test]
        public void AThemedPlateButtonsTree_HasVisualsButNoGeneratedLabel()
        {
            var button = BuildPlateOnly(ButtonTheme.Crimson);

            var visuals = Find(button, "Visuals");
            Assert.IsNotNull(visuals, "no Visuals child - Ui.ApplyThemePlateOnly should have built one");
            Assert.AreEqual(UiNodeKind.Panel, visuals.Kind);
            Assert.IsNotNull(visuals.Children.FirstOrDefault(n => n.Name == "Glow"));
            Assert.IsNotNull(visuals.Children.FirstOrDefault(n => n.Name == "Plate"));

            var generatedLabel = Find(button, "TestButtonLabel");
            Assert.IsNull(generatedLabel,
                "ThemedPlate() must not generate its own '<name>Label' - the caller declares its own caption");
        }

        [Test]
        public void AThemedPlateButton_IsMarkedCaptionPreserving()
        {
            var button = BuildPlateOnly(ButtonTheme.Crimson);

            Assert.IsTrue(button.CaptionPreserving);
            Assert.AreEqual(ButtonTheme.Crimson, button.Theme);
        }

        [Test]
        public void LayerCaptionWithVisuals_SharesOneTokenBetweenVisualsAndTheCaption()
        {
            var button = BuildPlateOnly(ButtonTheme.Blue);
            var visuals = Find(button, "Visuals");
            var caption = Find(button, "TestButtonCaption");

            Assert.IsNotNull(visuals.LayerGroup);
            Assert.AreSame(visuals.LayerGroup, caption.LayerGroup);
            Assert.IsNull(visuals.AllowOverlapReason);
            Assert.IsNull(caption.AllowOverlapReason);
        }

        [Test]
        public void LayerCaptionWithVisuals_WithNoVisuals_Throws()
        {
            var button = Ui.Button("Bare", UiStrings.Cancel, new UiVec(200f, 60f), 20);

            Assert.Throws<System.InvalidOperationException>(
                () => button.LayerCaptionWithVisuals(Ui.Label("Caption", UiStrings.Cancel, new UiVec(100f, 20f))));
        }

        [Test]
        public void AThemedPlateRoot_PassesUiAudit_AtAllFourAspects()
        {
            var button = BuildPlateOnly(ButtonTheme.Green);
            var root = Ui.Panel("Root", UiSize.Fixed(1920f, 1080f), button);

            var errors = UiAudit.RunAllFrames(root);

            CollectionAssert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " + string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void ThemedPlateOnANonButtonNode_Throws()
        {
            var panel = Ui.Panel("SomePanel", UiSize.Fixed(100f, 100f));

            Assert.Throws<System.ArgumentException>(() => panel.ThemedPlate(ButtonTheme.Gold));
        }

        [Test]
        public void ThemedPlateOnAChromelessButton_Throws()
        {
            var button = Ui.Button("SomeButton", UiString.Runtime, new UiVec(200f, 60f), 20).NoChrome();

            Assert.Throws<System.ArgumentException>(() => button.ThemedPlate(ButtonTheme.Gold));
        }
    }
}
