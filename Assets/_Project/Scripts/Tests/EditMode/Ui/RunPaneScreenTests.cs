using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The RUN pane's geometry and tree (plan C23, spec section 4). UiAudit
    // re-solves the built tree at four aspects (SystemMenuScreen's own audit),
    // so overlap and overflow are covered there; what is pinned here is the
    // arithmetic, in literals, and the pairing of nodes to what fills them.
    public class RunPaneScreenTests
    {
        private const float Floor = -378f;   // the working area's bottom edge: -AreaHeight / 2

        // ---- the columns ---------------------------------------------------------------

        [Test]
        public void TheWorkingAreaIsTheSpecs1520By756()
        {
            Assert.AreEqual(1520f, RunLayout.AreaWidth, 0.001f);
            Assert.AreEqual(756f, RunLayout.AreaHeight, 0.001f);
            Assert.AreEqual(-760f, RunLayout.Left, 0.001f);
            Assert.AreEqual(378f, RunLayout.Top, 0.001f);
        }

        [Test]
        public void TheThreeColumnsFillTheAreaExactly()
        {
            Assert.AreEqual(-760f, RunLayout.RelicColumnLeft, 0.001f);
            Assert.AreEqual(-96f, RunLayout.EffectColumnLeft, 0.001f);
            Assert.AreEqual(328f, RunLayout.DescentColumnLeft, 0.001f);
            Assert.AreEqual(432f, RunLayout.DescentColumnWidth, 0.001f);
            Assert.AreEqual(760f, RunLayout.DescentColumnLeft + RunLayout.DescentColumnWidth, 0.001f,
                "the last column does not end at the area's right edge");
        }

        // ---- relics ---------------------------------------------------------------------

        [Test]
        public void TheRelicRowsSitUnderTheHeadingAndStayInsideTheArea()
        {
            Assert.AreEqual(5, RunLayout.RelicRows);
            Assert.AreEqual(326f, RunLayout.RelicRowsTop, 0.001f);
            Assert.AreEqual(260f, RunLayout.RelicRowCentreY(0), 0.001f);
            Assert.AreEqual(-284f, RunLayout.RelicRowCentreY(4), 0.001f);
            Assert.AreEqual(-350f, RunLayout.RelicRowsBottom, 0.001f);
            Assert.GreaterOrEqual(RunLayout.RelicRowsBottom, Floor);
        }

        [Test]
        public void ARelicRowsTextColumnClearsItsIconAndItsEdges()
        {
            Assert.AreEqual(-272f, RunLayout.RelicIconX, 0.001f);
            Assert.AreEqual(-224f, RunLayout.RelicTextLeft, 0.001f);
            Assert.AreEqual(304f, RunLayout.RelicTextRight, 0.001f);
            Assert.AreEqual(528f, RunLayout.RelicTextWidth, 0.001f);

            float iconRight = RunLayout.RelicIconX + RunLayout.RelicIconSize * 0.5f;
            Assert.Less(iconRight, RunLayout.RelicTextLeft, "the icon runs into the text column");
            Assert.AreEqual(RunLayout.RelicTextWidth, RunLayout.RelicNameWidth + RunLayout.RelicRarityWidth, 0.001f,
                "the name and the rarity do not share the line exactly");
        }

        [Test]
        public void TheDescriptionBoxSitsUnderTheNameLineInsideTheRow()
        {
            float nameBottom = RunLayout.RelicNameY - RunLayout.RelicNameHeight * 0.5f;
            float bodyTop = RunLayout.RelicBodyY + RunLayout.RelicBodyHeight * 0.5f;
            float bodyBottom = RunLayout.RelicBodyY - RunLayout.RelicBodyHeight * 0.5f;

            Assert.AreEqual(45f, RunLayout.RelicNameY, 0.001f);
            Assert.LessOrEqual(bodyTop, nameBottom, "the description starts over the name line");
            Assert.GreaterOrEqual(bodyBottom, -RunLayout.RelicRowHeight * 0.5f, "the description leaves the row");
        }

        [Test]
        public void ThePagerSharesTheHeadingLineAtTheColumnsRightEnd()
        {
            Assert.AreEqual(-142f, RunLayout.PagerNextX, 0.001f);
            Assert.AreEqual(-206f, RunLayout.PagerLabelX, 0.001f);
            Assert.AreEqual(-270f, RunLayout.PagerPrevX, 0.001f);
            Assert.LessOrEqual(RunLayout.PagerNextX + RunLayout.PagerArrowWidth * 0.5f,
                RunLayout.RelicColumnLeft + RunLayout.RelicColumnWidth + 0.001f);
        }

        // ---- effects and supplies ----------------------------------------------------------

        [Test]
        public void TheEffectsListLeavesRoomForEveryEffectThePaneCanShow()
        {
            // The buff kinds, plus bonus damage and second lives.
            int most = RunEffectRows.Buffs.Count + 2;

            Assert.GreaterOrEqual(RunLayout.EffectRows, most,
                "a new buff kind has no row to be drawn in: the effects list is full");
        }

        [Test]
        public void TheEffectsThenTheSuppliesThenTheGoldStackInsideTheColumn()
        {
            Assert.AreEqual(294f, RunLayout.EffectRowCentreY(0), 0.001f);
            Assert.AreEqual(58f, RunLayout.EffectRowsBottom, 0.001f);
            Assert.AreEqual(28f, RunLayout.SupplyHeadingCentreY, 0.001f);
            Assert.AreEqual(-28f, RunLayout.SupplyRowCentreY(0), 0.001f);
            Assert.AreEqual(-264f, RunLayout.SupplyRowsBottom, 0.001f);
            Assert.AreEqual(-282f, RunLayout.SupplyMoreCentreY, 0.001f);
            Assert.AreEqual(-328f, RunLayout.GoldRowCentreY, 0.001f);

            float supplyHeadingTop = RunLayout.SupplyHeadingCentreY + RunLayout.SupplyHeadingHeight * 0.5f;
            Assert.Less(supplyHeadingTop, RunLayout.EffectRowsBottom, "the supplies heading is inside the effects list");

            float goldBottom = RunLayout.GoldRowCentreY - RunLayout.GoldRowHeight * 0.5f;
            Assert.GreaterOrEqual(goldBottom, Floor, "the gold row leaves the area");

            float moreBottom = RunLayout.SupplyMoreCentreY - RunLayout.SupplyMoreHeight * 0.5f;
            float goldTop = RunLayout.GoldRowCentreY + RunLayout.GoldRowHeight * 0.5f;
            Assert.LessOrEqual(goldTop, moreBottom, "the gold row runs into the overflow line");
        }

        // ---- this descent ---------------------------------------------------------------------

        [Test]
        public void TheStatGroupsStackUnderTheHeadingAndStayInsideTheArea()
        {
            var groups = RunStatRows.Groups;

            Assert.AreEqual(3, groups.Count);
            CollectionAssert.AreEqual(new[] { 5, 4, 4 }, groups.Select(g => g.Rows.Count).ToList(),
                "the spec condenses the descent to Battle 5, The Fold 4, Spoils 4");

            Assert.AreEqual(308f, RunLayout.StatHeadingCentreY(groups, 0), 0.001f);
            Assert.AreEqual(268f, RunLayout.StatRowCentreY(groups, 0, 0), 0.001f);
            Assert.AreEqual(68f, RunLayout.StatHeadingCentreY(groups, 1), 0.001f);
            Assert.AreEqual(-302f, RunLayout.StatsBottom(groups), 0.001f);
            Assert.GreaterOrEqual(RunLayout.StatsBottom(groups), Floor);
        }

        // ---- the tree ---------------------------------------------------------------------------

        [Test]
        public void TheScreenAuthorsExactlyTheRowsTheLayoutHasRoomFor()
        {
            var screen = RunScreen.Build();

            Assert.AreEqual(RunLayout.RelicRows, screen.RelicButtons.Count);
            Assert.AreEqual(RunLayout.RelicRows, screen.RelicIcons.Count);
            Assert.AreEqual(RunLayout.RelicRows, screen.RelicNames.Count);
            Assert.AreEqual(RunLayout.RelicRows, screen.RelicRarities.Count);
            Assert.AreEqual(RunLayout.RelicRows, screen.RelicBodies.Count);
            Assert.AreEqual(RunLayout.EffectRows, screen.EffectButtons.Count);
            Assert.AreEqual(RunLayout.EffectRows, screen.EffectLabels.Count);
            Assert.AreEqual(RunLayout.EffectRows, screen.EffectValues.Count);
            Assert.AreEqual(RunLayout.SupplyRows, screen.SupplyButtons.Count);
            Assert.AreEqual(RunLayout.SupplyRows, screen.SupplyNames.Count);
        }

        // The pad reads the pane by moving focus, so every list row is a Button.
        [Test]
        public void EveryListRowIsFocusable()
        {
            var screen = RunScreen.Build();

            foreach (var row in screen.RelicButtons.Concat(screen.EffectButtons).Concat(screen.SupplyButtons))
            {
                Assert.AreEqual(UiNodeKind.Button, row.Node.Kind, $"{row.Node.Name} cannot take focus");
                Assert.IsTrue(row.Node.StartInactive, $"{row.Node.Name} shows before it has anything to say");
            }
        }

        // The pairing of a figure to its node is provable, not assumed: one
        // value node per declared row, in order, carrying that row's key.
        [Test]
        public void TheScreenDeclaresOneValueNodePerStatRow()
        {
            var screen = RunScreen.Build();

            CollectionAssert.AreEqual(RunStatRows.AllRows.Select(r => r.Key).ToList(), screen.StatKeys,
                "the pane's value nodes do not match its own row table");
            Assert.AreEqual(screen.StatKeys.Count, screen.StatValues.Count);
        }

        [Test]
        public void EveryRunStatRowHasAKeyAndALabelAndNoTwoShareAKey()
        {
            var rows = RunStatRows.AllRows;

            foreach (var row in rows)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(row.Key), "a run-stat row has no key");
                Assert.IsTrue(row.Label.IsValid, $"run-stat row '{row.Key}' has no label");
            }

            var keys = rows.Select(r => r.Key).ToList();
            CollectionAssert.AreEquivalent(keys.Distinct().ToList(), keys,
                "two run-stat rows share a key, so one of them prints the other's number");
            Assert.AreEqual(13, rows.Count, "Battle 5, The Fold 4 and Spoils 4");
        }
    }
}
