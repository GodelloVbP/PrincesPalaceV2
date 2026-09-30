using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // An ellipsising TMP label draws NO characters when its rect is shorter
    // than one line of its font: a Body 20 line measures 26.53 px, and a 26 px
    // box showed nothing (text set, node active, characterCount 0, no error).
    // A line is 1.33 x the size (docs/SPEC_SYSTEM_MENU.md section 1 has the
    // measured ratios).
    //
    // WHICH LABELS: only those UiEmitter gives TextOverflowModes.Ellipsis --
    // Truncated(), Clamped(), and a themed button's own "<Name>Label" caption
    // (WireThemedButton). A default-overflow label in a short box still draws,
    // running over its box, and UiAudit's overflow check owns that; several
    // hundred Body 20 labels sit in 26 px boxes and draw fine.
    //
    // The size checked is the smallest the label can autosize to: a roled label
    // shrinks inside its role's band, and TMP only draws when the box holds a
    // line at some size in it (TypographySpec.ResolveSizeRange). An unroled
    // label is its literal. A themed button itself is skipped: its caption is
    // its own Label child and is checked as one.
    //
    // Screens are built straight from Build() because ScreenRegistry is
    // Editor-only; the list below is every Screens/ type that emits labels.
    public class LabelLineHeightLintTests
    {
        private const float LineRatio = 1.33f;

        // Fewer than this (2868 today, counted per screen and frame) means the walk
        // stopped seeing them.
        private const int MinimumChecked = 1500;

        private static IEnumerable<KeyValuePair<string, UiNode>> AllScreenRoots()
        {
            yield return new KeyValuePair<string, UiNode>("CharacterDossier", CharacterDossierScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("DebugMenu", DebugMenuScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Defeat", DefeatScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Event", EventScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Exits", ExitsScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Fight", FightScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Glossary", GlossaryScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Hub", HubScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("MainMenu", MainMenuScreen.Build(new MainMenuInputs(5)).Root);
            yield return new KeyValuePair<string, UiNode>("Map", MapScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Options", OptionsScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Party", PartyScreen.Build(new PartyInputs(3)).Root);
            yield return new KeyValuePair<string, UiNode>("Reckoning", ReckoningScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("RelicDraft", RelicDraftScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("RewardTrack", RewardTrackScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Run", RunScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Shop", ShopScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("SystemMenu", SystemMenuScreen.Build().Root);
            yield return new KeyValuePair<string, UiNode>("Talent", TalentScreen.Build().Root);
        }

        private static IEnumerable<(SolvedNode Node, SolvedNode Parent)> Walk(SolvedNode node, SolvedNode parent = null)
        {
            yield return (node, parent);
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child, node)) yield return found;
            }
        }

        // The smallest size the label draws at, or 0 when it has none to check.
        private static float SmallestSize(UiNode node)
        {
            if (node.FontSize <= 0) return 0f;
            if (!node.Role.HasValue) return node.FontSize;
            return Typography.Specs[node.Role.Value].ResolveSizeRange(node.FontSize).Min;
        }

        // True when the emitter gives this label ellipsis overflow, the mode
        // that drops every character of a line its box cannot hold.
        private static bool Ellipsises(SolvedNode node, SolvedNode parent)
        {
            var source = node.Source;
            if (source.Kind != UiNodeKind.Label) return false;
            if (source.Truncates || source.Clamps) return true;

            var owner = parent?.Source;
            return owner != null && owner.Kind == UiNodeKind.Button && owner.Theme.HasValue
                   && !owner.CaptionPreserving && source.Name == owner.Name + "Label";
        }

        // "path: box < line at size", for every node boxed below one line.
        private static List<string> Problems(UiNode root, out int checkedCount)
        {
            var problems = new List<string>();
            checkedCount = 0;

            foreach (var frame in UiFrames.All)
            {
                var solved = UiSolver.Solve(root, frame);
                foreach (var (node, parent) in Walk(solved))
                {
                    if (!Ellipsises(node, parent)) continue;

                    float size = SmallestSize(node.Source);
                    if (size <= 0f) continue;

                    checkedCount++;
                    float line = size * LineRatio;
                    float height = node.Rect.Height;
                    if (height >= line - 0.01f) continue;

                    problems.Add($"{node.Path}: box {height:0.##} < line {line:0.##} (font {size:0.#})");
                }
            }

            return problems.Distinct().ToList();
        }

        [Test]
        public void NoEllipsisLabelIsBoxedBelowOneLineOfItsFont()
        {
            int total = 0;
            var problems = new List<string>();

            foreach (var screen in AllScreenRoots())
            {
                problems.AddRange(Problems(screen.Value, out int count).Select(p => $"{screen.Key}/{p}"));
                total += count;
            }

            // A walk that finds a fraction of today's labels has stopped
            // seeing the trees.
            Assert.Greater(total, MinimumChecked, "the lint checked too few labels to be looking at the screens");
            Assert.IsEmpty(problems, "boxed below one line, so TMP draws nothing:\n" + string.Join("\n", problems));
        }

        [Test]
        public void ABoxBelowOneLineIsCaught()
        {
            var tree = Ui.Panel("Host", Place.At(0f, 0f), UiSize.Fixed(800f, 200f),
                Ui.Label("Short", UiStrings.PromptSelect, new UiVec(200f, 26f), 20, null, Place.At(0f, 0f))
                    .Styled(TypographyRole.Body).Truncated(),
                Ui.Label("Overflows", UiStrings.PromptSelect, new UiVec(200f, 26f), 20, null, Place.At(0f, 80f))
                    .Styled(TypographyRole.Body),
                Ui.Label("Fits", UiStrings.PromptSelect, new UiVec(200f, 27f), 20, null, Place.At(0f, 40f))
                    .Styled(TypographyRole.Body).Clamped());

            var problems = Problems(tree, out int count);

            Assert.AreEqual(2 * UiFrames.All.Length, count);
            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("Short", problems[0]);
        }
    }
}
