using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The two facts every screen that hosts a Container/FlagBanner needs to
    // be true of it -- it wears the theme/ratio art it claims to, and its
    // content sits inside the kit's measured inset rather than the painted
    // border -- collapsed to one parameterised test instead of the same pair
    // repeated per screen (SystemMenuPaneTests x3, CharacterDossierScreenTests,
    // ConstellationScreenTests' Talent panel, DefeatScreenTests, FightScreenTests
    // x2, RelicDraftScreenTests). A row that is also screen-specific (the
    // Defeat/RelicDraft frames' "old flat fill is gone" ColorHex check) stays
    // in its own file -- this covers only the two facts every row shares.
    public class KitContainerPlacementTests
    {
        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }

        // Each row: (name, container-node getter, content node name, theme,
        // ratio, container width, container height).
        private static IEnumerable Cases()
        {
            yield return new TestCaseData(
                (System.Func<UiNode>)(() => RunStatsScreen.Build().Root),
                "RunStatsPaneContent", ButtonTheme.Silver, ContainerRatio.TwoByOne,
                RunStatsLayout.PaneWidth, RunStatsLayout.PaneHeight)
                .SetName("RunStatsPane/Silver/TwoByOne");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => OptionsScreen.Build().Root),
                "OptionsPaneContent", ButtonTheme.Silver, ContainerRatio.TwoByOne,
                OptionsLayout.PaneWidth, OptionsLayout.PaneHeight)
                .SetName("OptionsPane/Silver/TwoByOne");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => ExitsScreen.Build().Root),
                "ExitsPaneContent", ButtonTheme.Silver, ContainerRatio.TwoByOne,
                ExitsLayout.PaneWidth, ExitsLayout.PaneHeight)
                .SetName("ExitsPane/Silver/TwoByOne");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(CharacterDossierScreen.Build().Root).First(n => n.Name == "DossierColumnAFrame")),
                "DossierColumnAContent", ButtonTheme.Blue, ContainerRatio.ThreeByFour,
                DossierLayout.ColumnAWidth, DossierLayout.ColumnAFrameHeight)
                .SetName("DossierColumnAFrame/Blue/ThreeByFour");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(TalentScreen.Build().Root).First(n => n.Name == "TalentPanelColumn")),
                "TalentPanelContent", ButtonTheme.Violet, ContainerRatio.NineBySixteen,
                ConstellationLayout.PanelWidth, ConstellationLayout.PanelHeight)
                .SetName("TalentPanelColumn/Violet/NineBySixteen");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => DefeatScreen.Build().Frame.Node),
                "DefeatFrameContent", ButtonTheme.Crimson, ContainerRatio.ThreeByTwo,
                DefeatScreen.PanelWidth, DefeatScreen.FrameHeight)
                .SetName("DefeatFrame/Crimson/ThreeByTwo");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(FightScreen.Build().Root).First(n => n.Name == "PartyPlate")),
                "PartyPlateContent", ButtonTheme.Blue, ContainerRatio.TwoByOne,
                FightScreen.PartyPlateWidth, FightScreen.PartyPlateHeight)
                .SetName("PartyPlate/Blue/TwoByOne");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(FightScreen.Build().Root).First(n => n.Name == "SubmenuContainer")),
                "SubmenuFrameContent", ButtonTheme.Violet, ContainerRatio.ThreeByFour,
                FightSubmenuLayout.FrameWidth, FightSubmenuLayout.FrameHeight)
                .SetName("SubmenuContainer/Violet/ThreeByFour");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => RelicDraftScreen.Build().Frame.Node),
                "DraftFrameContent", ButtonTheme.Violet, ContainerRatio.ThreeByTwo,
                1500f, 1000f)
                .SetName("DraftFrame/Violet/ThreeByTwo");
        }

        [TestCaseSource(nameof(Cases))]
        public void ContainerWearsItsDeclaredThemeAndRatioArt(
            System.Func<UiNode> containerNode, string contentName, ButtonTheme theme, ContainerRatio ratio,
            float width, float height)
        {
            var container = containerNode();

            Assert.IsFalse(container.Decor,
                "the wrapper must stay non-Decor, or content beneath it audits clean against itself");
            var art = container.Children.Single(child => child.Kind == UiNodeKind.Sprite);
            Assert.AreEqual(ContainerArtKeyFor(theme, ratio), art.SpriteKey);
            Assert.IsTrue(art.Decor);
        }

        [TestCaseSource(nameof(Cases))]
        public void ContainerContentSitsInsideTheMeasuredInset(
            System.Func<UiNode> containerNode, string contentName, ButtonTheme theme, ContainerRatio ratio,
            float width, float height)
        {
            var container = containerNode();
            var content = container.Children.Single(child => child.Name == contentName);
            var inset = Ui.ContainerContentInset(ratio);

            Assert.AreEqual(PlaceKind.Stretch, content.Place.Kind);
            Assert.AreEqual(width * inset.Left, content.Place.Left, 0.01f);
            Assert.AreEqual(width * inset.Right, content.Place.Right, 0.01f);
            Assert.AreEqual(height * inset.Top, content.Place.Top, 0.01f);
            Assert.AreEqual(height * inset.Bottom, content.Place.Bottom, 0.01f);
        }

        // container_<theme>_<ratio>.png -- ContainerArt.Key is internal to
        // the Editor-visible assembly and this test assembly carries no
        // InternalsVisibleTo grant to it, so the filename is restated here,
        // same as ContainerTests'/DefeatScreenTests' own SpriteKey literals.
        private static string ContainerArtKeyFor(ButtonTheme theme, ContainerRatio ratio)
        {
            string ratioKey = ratio switch
            {
                ContainerRatio.ThreeByFour => "3x4",
                ContainerRatio.NineBySixteen => "9x16",
                ContainerRatio.ThreeByTwo => "3x2",
                ContainerRatio.TwoByOne => "2x1",
                _ => throw new System.ArgumentOutOfRangeException(nameof(ratio), ratio, "unhandled ContainerRatio"),
            };
            return $"UI/Buttons/Processed/container_{theme.ToString().ToLowerInvariant()}_{ratioKey}.png";
        }
    }
}
