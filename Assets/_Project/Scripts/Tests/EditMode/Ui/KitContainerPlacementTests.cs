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
    // repeated per screen (DefeatScreenTests, FightScreenTests x2,
    // RelicDraftScreenTests). A row that is also screen-specific (the
    // Defeat/RelicDraft frames' "old flat fill is gone" ColorHex check)
    // stays in its own file -- this covers only the two facts every row
    // shares.
    //
    // FIVE ROWS WENT: RunStatsPane, OptionsPane, ExitsPane and
    // DossierColumnAFrame on 2026-09-07 (the owner called every kit frame
    // inside the system menu ugly, and Exits/Options/Party/RunStats/
    // RewardTrack/DossierColumnAFrame are plain Panels now -- Ui.SystemMenuPane,
    // CharacterDossierScreen.BuildColumnAFrame -- with no theme/ratio/inset for
    // this test to pin), TalentPanelColumn on 2026-09-19 for the same reason
    // (Cases() has that row's own note), and DraftFrame on 2026-09-23 (same
    // reason again, that row's own note). PartyPane and RewardTrackPanel
    // were never rows here to begin with --
    // SystemMenuScreenTests.NoSystemMenuNodeUsesAContainerOrFlagBannerSprite
    // is the mechanised form of the rule that replaces all of them.
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
            // TalentPanelColumn/Violet/NineBySixteen WENT (2026-09-19): the
            // owner called the violet frame ugly, the same call that took
            // RunStatsPane/OptionsPane/ExitsPane/DossierColumnAFrame off this
            // list on 2026-09-07 (see this class's own header) -- the column
            // is a plain Panel now (TalentScreen.BuildPanel), with no
            // theme/ratio art for either row below to pin.

            yield return new TestCaseData(
                (System.Func<UiNode>)(() => DefeatScreen.Build().Frame.Node),
                "DefeatFrameContent", ButtonTheme.Crimson, ContainerRatio.ThreeByTwo,
                DefeatScreen.PanelWidth, DefeatScreen.FrameHeight)
                .SetName("DefeatFrame/Crimson/ThreeByTwo");

            // PartyPlate/Blue/TwoByOne is GONE (2026-09-10). The fight HUD's
            // party card is a flat Ui.OutlineBox now, not a kit container --
            // FightScreen.BuildPartyPlate's own header has the measured
            // reason. The 2x1 PNG itself still has two live users
            // (EnemyPlate*Frame, GlossaryFrame) and is still pinned against
            // its own art by UiKitAspectPinTests.

            // SubmenuContainer/Violet/ThreeByFour WENT (2026-09-23, coordinator
            // pass 2): PreserveAspect off (tried first) stopped the frame
            // rendering narrower than its own rows, but Type.Simple's
            // non-uniform stretch squashed the painted border hard at 1-3
            // rows -- the same "stretched and looks bad" complaint that took
            // DraftFrame off this list the same day (see that row's own
            // note, just above the HQ-kit section below). FightScreen.
            // BuildSubmenuFrame is a flat Solid-fill-plus-Rim panel now
            // (RelicDraftScreen's own DraftFrameFill/DraftFrame idiom), with
            // no theme/ratio/inset for this row to pin.

            // DraftFrame/Violet/ThreeByTwo WENT (2026-09-23): the owner called
            // it out by name -- "it's stretched and looks bad and the inside
            // is not black, it's more leathery" -- the same complaint that
            // took RunStatsPane/OptionsPane/ExitsPane/DossierColumnAFrame off
            // this list on 2026-09-07. RelicDraftScreen.Build().Frame is a
            // flat Solid-fill-plus-Rim panel now (SystemMenuScreen's own
            // frame idiom), with no theme/ratio/inset for this row to pin.

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
                Ui.ContainerSizeForWidth(ContainerRatio.TwoByOne, 260f).X,
                Ui.ContainerSizeForWidth(ContainerRatio.TwoByOne, 260f).Y)
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
