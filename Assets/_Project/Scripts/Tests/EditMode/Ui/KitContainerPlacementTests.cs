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
    // repeated per screen (ConstellationScreenTests' Talent panel,
    // DefeatScreenTests, FightScreenTests x2, RelicDraftScreenTests). A row
    // that is also screen-specific (the Defeat/RelicDraft frames' "old flat
    // fill is gone" ColorHex check) stays in its own file -- this covers only
    // the two facts every row shares.
    //
    // FOUR ROWS WENT (RunStatsPane, OptionsPane, ExitsPane,
    // DossierColumnAFrame) on 2026-09-07: the owner called every kit frame
    // inside the system menu ugly, and Exits/Options/Party/RunStats/
    // RewardTrack/DossierColumnAFrame are plain Panels now (Ui.SystemMenuPane,
    // CharacterDossierScreen.BuildColumnAFrame) with no theme/ratio/inset for
    // this test to pin. PartyPane and RewardTrackPanel were never rows here
    // to begin with -- SystemMenuScreenTests.NoSystemMenuNodeUsesAContainer
    // OrFlagBannerSprite is the mechanised form of the rule that replaces
    // all six.
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

            // ---- HQ-kit conversions (owner's instruction, 2026-09-07) ----

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(HubScreen.Build().Root).First(n => n.Name == "CurrencyPlate")),
                "CurrencyPlateContent", ButtonTheme.Gold, ContainerRatio.FiveByOne,
                Ui.ContainerSizeForWidth(ContainerRatio.FiveByOne, 520f).X,
                Ui.ContainerSizeForWidth(ContainerRatio.FiveByOne, 520f).Y)
                .SetName("CurrencyPlate/Gold/FiveByOne");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => GlossaryScreen.Build().Frame.Node),
                "GlossaryFrameContent", ButtonTheme.Silver, ContainerRatio.TwoByOne,
                GlossaryScreen.PanelWidth, GlossaryScreen.PanelHeight)
                .SetName("GlossaryFrame/Silver/TwoByOne");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(GlossaryScreen.Build().Root).First(n => n.Name == "GlossaryDetailPlate")),
                "GlossaryDetailContent", ButtonTheme.Silver, ContainerRatio.ThreeByFour,
                Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 780f).X,
                Ui.ContainerSizeForHeight(ContainerRatio.ThreeByFour, 780f).Y)
                .SetName("GlossaryDetailPlate/Silver/ThreeByFour");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(TalentScreen.Build().Root).First(n => n.Name == "RespecDialogCard")),
                "RespecDialogContent", ButtonTheme.Violet, ContainerRatio.ThreeByTwo,
                Ui.ContainerSizeForWidth(ContainerRatio.ThreeByTwo, 780f).X,
                Ui.ContainerSizeForWidth(ContainerRatio.ThreeByTwo, 780f).Y)
                .SetName("RespecDialogCard/Violet/ThreeByTwo");

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => Walk(FightScreen.Build().Root).First(n => n.Name == "EnemyPlate0Frame")),
                "EnemyPlate0FrameContent", ButtonTheme.Crimson, ContainerRatio.TwoByOne,
                Ui.ContainerSizeForWidth(ContainerRatio.TwoByOne, 220f).X,
                Ui.ContainerSizeForWidth(ContainerRatio.TwoByOne, 220f).Y)
                .SetName("EnemyPlate0Frame/Crimson/TwoByOne");
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
                ContainerRatio.FiveByOne => "5x1",
                _ => throw new System.ArgumentOutOfRangeException(nameof(ratio), ratio, "unhandled ContainerRatio"),
            };
            return $"UI/Buttons/Processed/container_{theme.ToString().ToLowerInvariant()}_{ratioKey}.png";
        }
    }
}
