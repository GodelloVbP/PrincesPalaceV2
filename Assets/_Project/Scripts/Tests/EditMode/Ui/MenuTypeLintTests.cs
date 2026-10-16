using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // THE MENU TYPE FLOOR (plan C14, spec section 1), as a lint over the solved
    // tree of the whole system menu and the rows outside it that share its
    // presenter.
    //
    // Every label and button carries a Typography role and draws at or over the
    // floor: mixed case 20, all caps 24. The walk visits EVERY node whatever
    // its AsDecor, because AsDecor is a raycast and audit flag and the menu's
    // readable headings and values carry it. The one exemption is
    // UiNode.AllowSmallText, which needs a reason.
    //
    // THE SCOPE IS THE SYSTEM MENU TREE ITSELF -- frame and every pane -- so a
    // pane added tomorrow is held to the floor without anyone remembering to
    // list it. The rows that live in other screens are named by the prefix of
    // the nodes they own.
    public class MenuTypeLintTests
    {
        private readonly struct Scope
        {
            public readonly string Name;
            public readonly Func<UiNode> Build;

            // When set, only nodes whose name starts with it are checked: a
            // screen outside the menu joins the list by the rows it touched,
            // not wholesale.
            public readonly string OnlyNamePrefix;

            // Fewer than this checked means the walk found nothing, or the
            // screen lost its labels.
            public readonly int MinimumNodes;

            public Scope(string name, Func<UiNode> build, int minimumNodes, string onlyNamePrefix)
            {
                Name = name;
                Build = build;
                MinimumNodes = minimumNodes;
                OnlyNamePrefix = onlyNamePrefix;
            }
        }

        private static readonly Scope[] Scoped =
        {
            new Scope("system menu: frame and every pane", () => SystemMenuScreen.Build().Root, MenuMinimumNodes, null),
            new Scope("reckoning relics tab", () => ReckoningScreen.Build().Root, 15, "ReckoningRelic"),
            new Scope("relic draft held strip", () => RelicDraftScreen.Build().Root, 9, "DraftHeld"),
        };

        // What the whole menu checks today is 627 labels and buttons; a walk
        // that finds well under that has stopped seeing a pane (the per-pane test below names which).
        private const int MenuMinimumNodes = 500;

        private static IEnumerable<SolvedNode> Walk(SolvedNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child))
                {
                    yield return found;
                }
            }
        }

        // Every problem in a tree, as "path: what is wrong", and how many
        // labels and buttons it looked at.
        private static List<string> Problems(UiNode root, out int checkedCount, string onlyNamePrefix = null)
        {
            var solved = UiSolver.Solve(root, UiFrames.Reference);
            var problems = new List<string>();
            checkedCount = 0;

            foreach (var node in Walk(solved))
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
                var problems = Problems(scope.Build(), out int count, scope.OnlyNamePrefix);

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

            var decorLabels = Walk(solved)
                .Where(n => n.Source.Kind == UiNodeKind.Label && n.Source.Decor)
                .ToList();

            Assert.Greater(decorLabels.Count, 100,
                "the menu's readable text is decor, and the lint must be covering it");
        }

        // Every pane is inside the walk: one label from each pane's own tree.
        [TestCase("SystemPaneCharacter")]
        [TestCase("SystemPaneParty")]
        [TestCase("SystemPaneRun")]
        [TestCase("SystemPaneGlossary")]
        [TestCase("SystemPaneOptions")]
        [TestCase("SystemPaneExit")]
        public void EveryPaneIsInsideTheWalk(string pane)
        {
            var solved = UiSolver.Solve(SystemMenuScreen.Build().Root, UiFrames.Reference);

            var host = Walk(solved).FirstOrDefault(n => n.Name == pane);
            Assert.IsNotNull(host, $"the menu tree has no '{pane}'");

            int labels = Walk(host).Count(n => n.Source.Kind == UiNodeKind.Label || n.Source.Kind == UiNodeKind.Button);
            Assert.Greater(labels, 5, $"'{pane}' has no labels for the lint to look at");
        }

        // ---- the lint can fail ------------------------------------------------------

        private static UiNode Tree(params UiNode[] labels) =>
            Ui.Panel("Host", Place.At(0f, 0f), UiSize.Fixed(800f, 200f), labels);

        [Test]
        public void AnUndersizedLabelIsCaught()
        {
            var tree = Tree(Ui.Label("Tiny", UiStrings.PromptSelect, new UiVec(200f, 30f), 14, null, Place.At(0f, 0f))
                .Styled(TypographyRole.Body));

            var problems = Problems(tree, out int count);

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("Tiny", problems[0]);
            StringAssert.Contains("under the floor of 20", problems[0]);
        }

        [Test]
        public void AnUnroledLabelIsCaughtWhateverItsSize()
        {
            var tree = Tree(Ui.Label("Bare", UiStrings.PromptSelect, new UiVec(200f, 30f), 40, null, Place.At(0f, 0f)));

            var problems = Problems(tree, out _);

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
