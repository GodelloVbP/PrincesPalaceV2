using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The fight's motion layer, migrated from v1 (ButtonPressAnimator,
    // SubtleHoverScale, ColumnOpenAnimator) after the rebuild shipped without
    // any of it -- every button in v2 was silent and static, while
    // Sound.ButtonClick's comment still described the component that fired it.
    //
    // These are TREE-level tests, which is deliberate: the emitter decides
    // which animator a node gets, but the tree is where the DECISION is
    // recorded, and a wrong decision (a wide row popping, a column animating
    // when it is never hidden) is a design mistake rather than a wiring one.
    // The wiring itself is covered by the emitter having exactly one path.
    public class FightButtonMotionTests
    {
        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in Walk(child)) yield return descendant;
            }
        }

        private static List<UiNode> FightNodes() => Walk(FightScreen.Build().Root).ToList();

        private static UiNode Named(string name) =>
            FightNodes().FirstOrDefault(n => n.Name == name);

        // ---- the invariants ------------------------------------------------------

        // An open animation is triggered by OnEnable, so a node that is never
        // disabled plays it exactly once -- at scene load, before the player
        // can see it -- and never again. That is always a mistake, and it is
        // invisible in play, which is why it is asserted rather than trusted.
        [Test]
        public void OnlyNodesThatAreActuallyHiddenCarryAnOpenAnimation()
        {
            var wrong = FightNodes()
                .Where(n => n.OpensOnShow && !n.StartInactive)
                .Select(n => n.Name)
                .ToList();

            Assert.IsEmpty(wrong,
                "These nodes animate on show but are never hidden, so the animation plays once at scene load " +
                "and never again: " + string.Join(", ", wrong) +
                ". Either mark them Inactive() or drop Opening().");
        }

        // DEAD as an invariant since the owner's 2026-09-23 second pass:
        // Quiet() suppressing the click sound and a node carrying the
        // hover/focus rim used to be contradictory, because a Hovers() node
        // did not get the press animator Quiet()'s sound rides on. Now every
        // unthemed button gets BOTH the rim (default) and the press animator
        // (dim, not pop) -- see UiEmitter.EmitButton's own comment -- so
        // Quiet() and a rim are no longer mutually exclusive, and there is
        // nothing left here to assert. Kept as a comment, not a test, so a
        // future reader who remembers this invariant existing finds out why
        // it does not any more instead of hitting a silently-deleted method.

        // ---- what the migration actually put where -------------------------------

        // All three are shown with SetActive by FightController.Hud, which is
        // the trigger ColumnOpenAnimator relies on. This pins the set: a fourth
        // column added later without Opening() is the regression to catch.
        [TestCase("SubmenuColumn")]
        [TestCase("DetailColumn")]
        [TestCase("TargetPrompt")]
        public void TheCommandColumnsSlideOpen(string nodeName)
        {
            var node = Named(nodeName);

            Assert.IsNotNull(node, $"'{nodeName}' is not in the fight tree any more -- this test has stopped covering it");
            Assert.IsTrue(node.OpensOnShow, $"'{nodeName}' lost its open animation");
            Assert.IsTrue(node.StartInactive, $"'{nodeName}' must start hidden for the open animation to ever fire");
        }

        // The wide-row rule: a 400+ wide button carrying several labels gets
        // the gentle hover, because the press pop visibly swings its text.
        [Test]
        public void EnemyPlatesHoverGentlyRatherThanPopping()
        {
            var plates = FightNodes().Where(n => n.Name.StartsWith("EnemyPlate")
                                                 && n.Kind == UiNodeKind.Button).ToList();

            CollectionAssert.IsNotEmpty(plates, "no enemy plate buttons found - this test is not seeing the tree");
            foreach (var plate in plates)
            {
                Assert.AreEqual(1.03f, plate.HoverScale, 0.0001f,
                    $"'{plate.Name}' should hover at 1.03, the figure v1 settled on for a plate this wide");
            }
        }

        [Test]
        public void SubmenuRowsHoverGentlyThroughTheirThemedPlateNow()
        {
            // Balance-bot, 2026-09-02: the manual .Hovers(1.02f) scale-pop
            // this test used to pin is gone -- the rows wear a Violet
            // ThemedPlate now, and ThemedButtonState's own hover state is
            // what gives them their "gentle, not popping" feedback (a
            // brightened plate, not a rescale). HoverScale staying at its
            // zero default is the point, not a regression: two hover
            // mechanisms driving localScale would fight each other, which is
            // exactly why HoverScale and a themed plate are mutually
            // exclusive by construction (see UiNode.HoverScale's own header).
            var rows = FightNodes().Where(n => n.Name.StartsWith("CharacterSkill")
                                               && n.Kind == UiNodeKind.Button).ToList();

            CollectionAssert.IsNotEmpty(rows, "no submenu row buttons found - this test is not seeing the tree");
            foreach (var row in rows)
            {
                Assert.AreEqual(ButtonTheme.Violet, row.Theme, $"'{row.Name}' should wear the Violet themed plate");
                Assert.AreEqual(0f, row.HoverScale, 0.0001f,
                    $"'{row.Name}' should not also drive the manual hover-scale animator - ThemedButtonState owns hover now");
            }
        }

        // The other half of the same split, and the reason it is a split: the
        // root verbs are narrow and ARE pressed, so they keep the pop. If this
        // starts failing because someone gave everything Hovers(), the fight
        // has quietly lost its press feedback entirely.
        [Test]
        public void TheRootVerbsKeepThePressPop()
        {
            var verbs = FightNodes().Where(n => n.Name.StartsWith("Verb")
                                                && n.Kind == UiNodeKind.Button).ToList();

            CollectionAssert.IsNotEmpty(verbs, "no verb buttons found - this test is not seeing the tree");
            foreach (var verb in verbs)
            {
                Assert.AreEqual(0f, verb.HoverScale,
                    $"'{verb.Name}' asked for a hover scale, which means it no longer gets the press animation " +
                    "or the click sound that rides on it");
            }
        }

        // A sweep that finds nothing passes everything. The fight tree has well
        // over twenty buttons; this fails loudly if the walk breaks.
        [Test]
        public void TheWalkActuallySeesTheFightsButtons()
        {
            int buttons = FightNodes().Count(n => n.Kind == UiNodeKind.Button);

            Assert.Greater(buttons, 20,
                $"Only {buttons} buttons found in the fight tree - the walk is not seeing it, so every other " +
                "test in this class would vacuously pass.");
        }
    }
}
