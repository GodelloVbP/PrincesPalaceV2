using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // THE MENU TYPE FLOOR (plan C14, spec section 1), as a lint over the solved
    // trees of the screens that have been migrated to it.
    //
    // Every label and button carries a Typography role and draws at or over the
    // floor: mixed case 20, all caps 24. The walk visits EVERY node whatever
    // its AsDecor, because AsDecor is a raycast and audit flag and the menu's
    // readable headings and values carry it. The one exemption is
    // UiNode.AllowSmallText, which needs a reason.
    //
    // THE SCOPE IS A LIST, and it is the only thing a later phase edits: a pane
    // joins Scoped when its own phase migrates it, and the last phase deletes
    // the list. Panes still on literal sizes are not on it yet -- and each
    // scoped screen names the subtrees the lint leaves to those phases.
    public class MenuTypeLintTests
    {
        private readonly struct Scope
        {
            public readonly string Name;
            public readonly Func<UiNode> Build;

            // Subtrees left to the phase that migrates them, by root node name.
            public readonly string[] SkippedSubtrees;

            // When set, only nodes whose name starts with it are checked: a
            // screen outside the menu joins the list by the rows it touched,
            // not wholesale.
            public readonly string OnlyNamePrefix;

            // Fewer than this checked means the walk found nothing, or the
            // part lost its labels.
            public readonly int MinimumNodes;

            public Scope(string name, Func<UiNode> build, int minimumNodes, string onlyNamePrefix,
                         params string[] skipped)
            {
                Name = name;
                Build = build;
                MinimumNodes = minimumNodes;
                OnlyNamePrefix = onlyNamePrefix;
                SkippedSubtrees = skipped;
            }
        }

        // The frame -- lintel, bar, footer -- of the system menu, and the panes
        // migrated so far: CHARACTER (the reward track with it), RUN and
        // GLOSSARY. The other panes are their phases' to add. Then
        // the rows outside the menu that share its presenter: the Reckoning's
        // relic tab and the relic draft's held strip.
        //
        // The frame authors the tab buttons, the lintel's title, context,
        // pill, gold, embers, Esc and close, and the footer's notice and its
        // prompt slots of two labels each, so 20 is what it cannot fall under.
        private static readonly Scope[] Scoped =
        {
            new Scope("system menu frame, CHARACTER, RUN and GLOSSARY panes", () => SystemMenuScreen.Build().Root, 400, null,
                "SystemPaneParty", "SystemPaneOptions", "SystemPaneExit"),
            new Scope("reckoning relics tab", () => ReckoningScreen.Build().Root, 15, "ReckoningRelic"),
            new Scope("relic draft held strip", () => RelicDraftScreen.Build().Root, 9, "DraftHeld"),
        };

        private static IEnumerable<SolvedNode> Walk(SolvedNode node, string[] skipped)
        {
            if (skipped.Contains(node.Name)) yield break;

            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child, skipped))
                {
                    yield return found;
                }
            }
        }

        // Every problem in a tree, as "path: what is wrong", and how many
        // labels and buttons it looked at.
        private static List<string> Problems(UiNode root, string[] skipped, out int checkedCount,
                                             string onlyNamePrefix = null)
        {
            var solved = UiSolver.Solve(root, UiFrames.Reference);
            var problems = new List<string>();
            checkedCount = 0;

            foreach (var node in Walk(solved, skipped))
            {
                if (node.Source.Kind != UiNodeKind.Label && node.Source.Kind != UiNodeKind.Button) continue;
                if (onlyNamePrefix != null && !node.Name.StartsWith(onlyNamePrefix, StringComparison.Ordinal)) continue;

                checkedCount++;
                var problem = MenuTypeFloor.Problem(node.Source);
                if (problem != null) problems.Add($"{node.Path}: {problem}");
            }

            return problems;
        }

        [Test]
        public void EveryScopedScreenHoldsTheFloor()
        {
            foreach (var scope in Scoped)
            {
                var problems = Problems(scope.Build(), scope.SkippedSubtrees, out int count, scope.OnlyNamePrefix);

                Assert.GreaterOrEqual(count, scope.MinimumNodes,
                    $"'{scope.Name}': only {count} labels and buttons were checked, so the walk is not " +
                    "looking at the screen");
                Assert.IsEmpty(problems, $"'{scope.Name}' has text under the menu type floor:\n" +
                    string.Join("\n", problems));
            }
        }

        // A lint that skipped decor would pass the frame vacuously: its title,
        // values and prompts are all AsDecor.
        [Test]
        public void TheLintLooksAtDecorLabelsToo()
        {
            var solved = UiSolver.Solve(SystemMenuScreen.Build().Root, UiFrames.Reference);

            var decorLabels = Walk(solved, new[] { "SystemMenuContent" })
                .Where(n => n.Source.Kind == UiNodeKind.Label && n.Source.Decor)
                .ToList();

            Assert.Greater(decorLabels.Count, 10,
                "the frame's readable text is decor, and the lint must be covering it");
        }

        [Test]
        public void ThePaneSubtreesAreLeftToTheirOwnPhases()
        {
            var solved = UiSolver.Solve(SystemMenuScreen.Build().Root, UiFrames.Reference);

            Assert.IsTrue(solved.Descendants().Any(n => n.Name == "SystemMenuContent"),
                "fixture: the frame has no content host to skip");
            Assert.IsFalse(Walk(solved, new[] { "SystemMenuContent" }).Any(n => n.Name == "SystemMenuContent"),
                "the skipped subtree was walked");
        }

        // ---- the lint can fail ------------------------------------------------------

        private static UiNode Tree(params UiNode[] labels) =>
            Ui.Panel("Host", Place.At(0f, 0f), UiSize.Fixed(800f, 200f), labels);

        [Test]
        public void AnUndersizedLabelIsCaught()
        {
            var tree = Tree(Ui.Label("Tiny", UiStrings.PromptSelect, new UiVec(200f, 30f), 14, null, Place.At(0f, 0f))
                .Styled(TypographyRole.Body));

            var problems = Problems(tree, Array.Empty<string>(), out int count);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("Tiny", problems[0]);
            StringAssert.Contains("under the floor of 20", problems[0]);
        }

        [Test]
        public void AnUnroledLabelIsCaughtWhateverItsSize()
        {
            var tree = Tree(Ui.Label("Bare", UiStrings.PromptSelect, new UiVec(200f, 30f), 40, null, Place.At(0f, 0f)));

            var problems = Problems(tree, Array.Empty<string>(), out _);

            Assert.AreEqual(1, problems.Count, "a big literal is still not held to the floor without a role");
            StringAssert.Contains("no Typography role", problems[0]);
        }

        [Test]
        public void AllCapsTextNeedsTheHigherFloor()
        {
            // "AT CAMP" is authored in capitals, so 20 is under its floor of 24
            // though it would pass as mixed case.
            var capsAt20 = Ui.Label("Caps", UiStrings.SystemModeCamp, new UiVec(200f, 30f), 20, null, Place.At(0f, 0f))
                .Styled(TypographyRole.Body);
            var mixedAt20 = Ui.Label("Mixed", UiStrings.PromptSelect, new UiVec(200f, 30f), 20, null, Place.At(0f, 0f))
                .Styled(TypographyRole.Body);
            var capsAt24 = Ui.Label("Fine", UiStrings.SystemModeCamp, new UiVec(200f, 30f), 24, null, Place.At(0f, 0f))
                .Styled(TypographyRole.Body);

            Assert.IsNotNull(MenuTypeFloor.Problem(capsAt20));
            StringAssert.Contains("under the floor of 24", MenuTypeFloor.Problem(capsAt20));
            Assert.IsNull(MenuTypeFloor.Problem(mixedAt20));
            Assert.IsNull(MenuTypeFloor.Problem(capsAt24));
        }

        // The role decides for text the tree cannot see: a FunctionalHeading
        // upper-cases whatever it is given.
        [Test]
        public void AnUppercaseRoleIsAllCapsWhateverItsAuthoredText()
        {
            var heading = Ui.Label("Heading", UiStrings.PromptSelect, new UiVec(200f, 30f), 22, null, Place.At(0f, 0f))
                .Styled(TypographyRole.FunctionalHeading);

            StringAssert.Contains("under the floor of 24", MenuTypeFloor.Problem(heading));
        }

        [Test]
        public void AButtonIsHeldToTheFloorToo()
        {
            var button = Ui.Button("Small", UiStrings.SystemClose, new UiVec(40f, 40f), 16, Place.At(0f, 0f))
                .Styled(TypographyRole.FunctionalHeading);

            Assert.IsNotNull(MenuTypeFloor.Problem(button));
        }

        [Test]
        public void AllowSmallTextExemptsOneNodeAndNeedsAReason()
        {
            var ornament = Ui.Label("Numeral", UiStrings.PromptSelect, new UiVec(200f, 30f), 12, null, Place.At(0f, 0f))
                .Styled(TypographyRole.Body)
                .AllowSmallText("an ornament numeral that repeats the row's own position and carries nothing else");

            Assert.IsNull(MenuTypeFloor.Problem(ornament));

            Assert.Throws<ArgumentException>(() =>
                Ui.Label("NoReason", UiStrings.PromptSelect, new UiVec(200f, 30f), 12, null, Place.At(0f, 0f))
                    .AllowSmallText(""));
        }

        [Test]
        public void TheFloorsAreThoseTheSpecStates()
        {
            Assert.AreEqual(20, MenuTypeFloor.MixedCase);
            Assert.AreEqual(24, MenuTypeFloor.AllCaps);
        }

        // ---- the frame's own audit ----------------------------------------------------

        [Test]
        public void TheFrameAuditsCleanAtEveryAspect()
        {
            var errors = UiAudit.RunAllFrames(SystemMenuScreen.Build().Root);

            Assert.IsEmpty(errors, string.Join("\n", errors.Select(e => e.ToString())));
        }
    }
}
