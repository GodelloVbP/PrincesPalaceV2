using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Pins the engine-free typography table (Domain/UiKit/Typography.cs)
    // against the brief it was authored from. The engine-facing half --
    // that the named font/material assets actually exist and render with
    // these colours -- is TypographyAssetTests (PlayMode), since resolving
    // a TMP_FontAsset/Material needs AssetDatabase, which EditMode's
    // Domain-only asmdef cannot reference (CODE_STANDARDS.md 5).
    public class TypographyRoleTests
    {
        [Test]
        public void EveryRole_HasASpec()
        {
            foreach (TypographyRole role in System.Enum.GetValues(typeof(TypographyRole)))
            {
                Assert.IsTrue(Typography.Specs.ContainsKey(role), $"{role} has no TypographySpec");
            }
        }

        [TestCase(TypographyRole.CeremonialTitle, "Cinzel-SemiBold SDF", "CeremonialTitle", 52f, 64f, true, 2f, 0f)]
        [TestCase(TypographyRole.FunctionalHeading, "SourceSans3-SemiBold SDF", "FunctionalHeading", 34f, 42f, true, 2f, 0f)]
        [TestCase(TypographyRole.Body, "SourceSans3-Regular SDF", "Body", 20f, 22f, false, 0f, 5f)]
        [TestCase(TypographyRole.TacticalData, "ChakraPetch-Medium SDF", "TacticalData", 17f, 19f, false, 1f, 0f)]
        [TestCase(TypographyRole.Alert, "SourceSans3-Bold SDF", "Alert", 22f, 26f, false, 1f, 0f)]
        public void Spec_MatchesBriefedValues(
            TypographyRole role, string fontAssetName, string materialName,
            float min, float max, bool uppercase, float tracking, float lineSpacing)
        {
            var spec = Typography.Specs[role];
            Assert.AreEqual(fontAssetName, spec.FontAssetName);
            Assert.AreEqual(materialName, spec.MaterialName);
            Assert.AreEqual(min, spec.MinSize1080p);
            Assert.AreEqual(max, spec.MaxSize1080p);
            Assert.AreEqual(uppercase, spec.Uppercase);
            Assert.AreEqual(tracking, spec.Tracking);
            Assert.AreEqual(lineSpacing, spec.LineSpacing);
        }

        // ButtonLabel's tracking is deliberately dynamic (fit-dependent, not
        // an authored constant) -- see Typography.cs's own comment on why.
        [Test]
        public void ButtonLabel_TrackingIsDynamic_NotAFixedValue()
        {
            var spec = Typography.Specs[TypographyRole.ButtonLabel];
            Assert.AreEqual("SourceSans3-Bold SDF", spec.FontAssetName);
            Assert.AreEqual("ButtonLabel", spec.MaterialName);
            Assert.AreEqual(26f, spec.MinSize1080p);
            Assert.AreEqual(30f, spec.MaxSize1080p);
            Assert.IsTrue(spec.Uppercase);
            Assert.IsNull(spec.Tracking);
        }

        // --- TypographySpec.ResolveSizeRange ---------------------------------------
        //
        // The precedence rule UiEmitter.ApplyTypography hands every roled
        // label off to. ButtonLabel's 26-30 band throughout: no literal
        // keeps it untouched; a literal inside it just narrows the max down
        // to the literal; a literal below it pins BOTH ends to the literal
        // (b955ceb's reservation -- see TypographyMigrationTests.
        // AThemedButtonsLiteralBelowItsBand_PinsFontSizeMinToTheLiteral for
        // the PlayMode half of the same fact); a literal above it raises
        // only the max, the band's own min staying the floor.

        [Test]
        public void ResolveSizeRange_NoLiteral_KeepsTheRolesOwnBand()
        {
            var spec = Typography.Specs[TypographyRole.ButtonLabel];
            var (min, max) = spec.ResolveSizeRange(null);

            Assert.AreEqual(26f, min);
            Assert.AreEqual(30f, max);
        }

        [Test]
        public void ResolveSizeRange_LiteralInsideTheBand_BecomesTheMax_MinUnchanged()
        {
            var spec = Typography.Specs[TypographyRole.ButtonLabel];
            var (min, max) = spec.ResolveSizeRange(28f);

            Assert.AreEqual(26f, min);
            Assert.AreEqual(28f, max);
        }

        [Test]
        public void ResolveSizeRange_LiteralBelowTheBand_PinsBothEndsToIt()
        {
            var spec = Typography.Specs[TypographyRole.ButtonLabel];
            var (min, max) = spec.ResolveSizeRange(24f);

            Assert.AreEqual(24f, min);
            Assert.AreEqual(24f, max);
        }

        [Test]
        public void ResolveSizeRange_LiteralAboveTheBand_RaisesOnlyTheMax()
        {
            var spec = Typography.Specs[TypographyRole.ButtonLabel];
            var (min, max) = spec.ResolveSizeRange(40f);

            Assert.AreEqual(26f, min);
            Assert.AreEqual(40f, max);
        }

        // --- UiNode.Role / .Styled() ----------------------------------------------

        [Test]
        public void ANewLabel_HasNoRoleByDefault()
        {
            var label = Ui.Label("SomeLabel", UiStrings.Cancel, new UiVec(100f, 20f));

            Assert.IsNull(label.Role,
                "an unmigrated label must stay on the untouched path - see UiEmitter.ApplyTypography");
        }

        [Test]
        public void Styled_SetsRole_AndReturnsTheSameNodeForChaining()
        {
            var label = Ui.Label("SomeLabel", UiStrings.Cancel, new UiVec(100f, 20f))
                .Styled(TypographyRole.Body);

            Assert.AreEqual(TypographyRole.Body, label.Role);
        }
    }
}
