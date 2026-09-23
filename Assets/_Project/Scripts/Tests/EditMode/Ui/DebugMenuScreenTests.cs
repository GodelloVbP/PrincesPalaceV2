using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.DebugMenu;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The debug menu's tree.
    //
    // A developer tool still has to clear the same audit as everything else --
    // twelve 44px rows stacked on a 50px pitch is exactly the sort of thing
    // that passes by eye and overlaps by 6px, and the whole point of building
    // screens as trees is that nobody has to notice that by eye.
    //
    // REBUILT 2026-09-23 (debug menu overhaul, phase 1): the flat kind filter
    // row is a category rail plus a pooled sub-filter row now, and the list
    // is 2 columns x RowsPerColumn instead of one column of 12.
    public class DebugMenuScreenTests
    {
        private static UiNode Tree() => DebugMenuScreen.Build().Root;

        [Test]
        public void TheMenuAuditsCleanAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(Tree(), frame);
                var errors = UiAudit.Run(solved, frame);

                Assert.IsEmpty(errors,
                    $"at {UiFrames.Describe(frame)}, first 5 of {errors.Count}: " +
                    string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
            }
        }

        [Test]
        public void ItAuditsCleanInsideTheHubItMountsIn()
        {
            var errors = UiAudit.RunAllFrames(HubScreen.Build().Root);

            Assert.IsEmpty(errors,
                "first 5 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(5).Select(e => e.ToString())));
        }

        [Test]
        public void EveryChromeButtonWearsSilverIncludingThePooledRows()
        {
            // "DebugMenuScreen: Silver only" -- no recommended action, no
            // danger, in a tool rather than the fiction. Balance-bot,
            // 2026-09-02: the filter tabs moved off the gold-fallback plate
            // onto Silver too. 2026-09-07 (ThemedButtonAspectLintTests): the
            // item rows moved OFF a plate entirely -- 900x44 is 20.5:1, a
            // ratio no plate shape lands within the container kit's own
            // tolerance of, and 900x150 (Row6x1's honest nominal for that
            // width) only fits 6 of RowsPerPage on screen. Rows are now
            // chromeless with a hairline rule, same shape as
            // CharacterDossierScreen.BuildNavRow; the icon pager arrows were
            // already NoChrome. The category rail and sub-filter chips
            // (2026-09-23) are Silver too, same rule.
            var screen = DebugMenuScreen.Build();

            Assert.AreEqual(ButtonTheme.Silver, screen.CloseButton.Node.Theme);
            Assert.AreEqual(ButtonTheme.Silver, screen.CategoryButtons[0].Node.Theme);
            Assert.AreEqual(ButtonTheme.Silver, screen.SubFilterButtons[0].Node.Theme);
            Assert.AreEqual(ButtonTheme.Silver, screen.QtyButtons[0].Node.Theme);
            Assert.IsTrue(screen.RowButtons[0].Node.Chromeless, "20.5:1 fits no plate shape -- hairline row instead");
            Assert.IsTrue(screen.PrevPageButton.Node.Chromeless, "an icon arrow, not a plate button");
            Assert.IsTrue(screen.NextPageButton.Node.Chromeless, "an icon arrow, not a plate button");
            Assert.IsTrue(screen.PlusMinusButton.Node.Chromeless, "a square stepper fits no plate shape");
            Assert.IsTrue(screen.PlusPlusButton.Node.Chromeless, "a square stepper fits no plate shape");
        }

        [Test]
        public void TheMenuStartsHidden()
        {
            Assert.IsTrue(Tree().StartInactive);
        }

        [Test]
        public void ThereIsOneRowNodePerCatalogueRow()
        {
            // One number, read by the tree, the controller and the count
            // audit. A tree with fewer rows than a page holds would silently
            // drop items off the end.
            var screen = DebugMenuScreen.Build();

            Assert.AreEqual(DebugMenuCatalog.RowsPerPage, screen.RowButtons.Count);
            Assert.AreEqual(DebugMenuCatalog.RowsPerPage, screen.RowLabels.Count);
        }

        [Test]
        public void ThereIsOneCategoryButtonPerCategory()
        {
            // The controller indexes DebugCategory's values by rail position,
            // so a button added to the tree without a matching category (or
            // the reverse) would make the two disagree silently. Eight, as a
            // literal, so adding a category is a decision this test sees.
            Assert.AreEqual(8, DebugMenuScreen.Build().CategoryButtons.Count);
            Assert.AreEqual(8, System.Enum.GetValues(typeof(DebugCategory)).Length);
        }

        [Test]
        public void TheSubFilterPoolIsTwelveWide()
        {
            // The largest sub-filter axis is the tier chips (All + 0..10).
            // Equipment's 7-wide slot axis reuses the front of this same
            // pool -- see DebugMenuScreen's own field comment.
            var screen = DebugMenuScreen.Build();

            Assert.AreEqual(12, screen.SubFilterButtons.Count);
            Assert.AreEqual(12, screen.SubFilterLabels.Count);
        }

        [Test]
        public void ThereAreThreeQuantityButtons()
        {
            // x1 / x5 / x10 -- contract 3.
            Assert.AreEqual(3, DebugMenuScreen.Build().QtyButtons.Count);
        }

        [Test]
        public void TheDebugMenuDrawsOverTheCharacterScreen()
        {
            // Declaration order is painter's order. A debug tool covered by the
            // very screen you opened it to debug is the one stacking mistake
            // that makes it useless -- and both are modals over the same hub,
            // so nothing else decides this.
            //
            // The character screen is the system menu now; it used to be
            // CharacterOverlayPanel. The rule is unchanged and so is the reason
            // -- only which modal has to stay underneath.
            var root = HubScreen.Build().Root;
            var names = root.Children.Select(c => c.Name).ToList();

            int character = names.IndexOf("SystemMenuPanel");
            int debug = names.IndexOf("DebugMenuPanel");

            Assert.Greater(character, -1, "the system menu is not a direct child of HubPanel any more");
            Assert.Greater(debug, -1, "the debug menu is not a direct child of HubPanel any more");
            Assert.Greater(debug, character, "the debug menu must be declared after the system menu");
        }

        [Test]
        public void ARowLabelDoesNotCollideWithTheButtonsGeneratedCaption()
        {
            // UiEmitter names a button's own caption "<button>Label". A tree
            // child by that name produces two GameObjects with one name under
            // one parent, and every by-name lookup takes the emitter's empty
            // one. This screen shipped that bug for one build.
            // UiTreeTestHelpers.UnthemedButtons narrows past a themed
            // button's own "<Name>Label" child, which is Ui.ApplyTheme's
            // real, intended output (see UiNode.Themed), not the
            // emitter-name collision this check exists to catch.
            var offenders = UiTreeTestHelpers.UnthemedButtons(Tree())
                .SelectMany(button => button.Children
                    .Where(c => c.Name == button.Name + "Label")
                    .Select(c => c.Name))
                .ToList();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void TheCollisionCheckIsNotVacuous()
        {
            // A deliberate collision must FAIL the audit. Without this the
            // check above proves only that no such node exists today, not that
            // the audit would catch one tomorrow -- and a guard nobody has seen
            // fire is a guard nobody knows is wired up.
            var button = Ui.Button("Probe", UiStrings.Close, new UiVec(200f, 50f), 16, Place.At(0f, 0f));
            button.Children.Add(Ui.Label("ProbeLabel", UiStrings.Close, new UiVec(180f, 40f), 14,
                "#FFFFFF", Place.At(0f, 0f)).AsDecor());

            var root = Ui.Panel("ProbeRoot", UiSize.Fixed(1920f, 1080f), button);
            var errors = UiAudit.Run(UiSolver.Solve(root, UiFrames.All[0]), UiFrames.All[0]);

            Assert.IsTrue(errors.Any(e => e.Check == UiAuditCheck.DuplicateName),
                "the audit did not object to a child named exactly '<button>Label'");
        }

    }
}
