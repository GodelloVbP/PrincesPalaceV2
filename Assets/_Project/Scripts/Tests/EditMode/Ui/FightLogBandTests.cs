using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // THE COMBAT LOG STAYS IN ITS OWN BAND. QA 2026-09-26: "...It's not very
    // effective." printed into the ENEMIES heading at x~1390 and a long line
    // slid under the enemy plates, because the log's label ran 1780 wide to the
    // screen's right edge. It now stops one plate gap short of the enemy
    // column, at every canvas aspect the audit solves.
    public class FightLogBandTests
    {
        private static SolvedNode Find(SolvedNode root, string name) =>
            root.Name == name ? root : root.Descendants().FirstOrDefault(n => n.Name == name);

        [Test]
        public void TheLogEndsBeforeTheEnemyHeadingAndEveryPlateAtEveryFrame()
        {
            foreach (var frame in UiFrames.All)
            {
                var root = UiSolver.Solve(FightScreen.Build().Root, frame);
                var log = Find(root, "MessageLabel");
                var heading = Find(root, "EnemiesHeading");
                Assert.IsNotNull(log, "no MessageLabel in the fight tree");
                Assert.IsNotNull(heading, "no EnemiesHeading in the fight tree");

                // 16 = the plate gap the log stops short by.
                Assert.LessOrEqual(log.Rect.Right, heading.Rect.Left - 16f + 0.01f,
                    $"{frame.X}x{frame.Y}: the log reaches the ENEMIES heading");

                for (int i = 0; ; i++)
                {
                    var plate = Find(root, $"EnemyPlate{i}");
                    if (plate == null) break;
                    Assert.LessOrEqual(log.Rect.Right, plate.Rect.Left,
                        $"{frame.X}x{frame.Y}: the log slides under EnemyPlate{i}");
                }
            }
        }

        [Test]
        public void TheLogKeepsItsLeftEdgeAndHeight()
        {
            // Left -820 (clear of the portrait column), right 920 - 536 - 16 =
            // 368 (the plate block's left edge less one gap): 1188 wide, and the
            // same 108 tall that holds four 20pt lines.
            var root = UiSolver.Solve(FightScreen.Build().Root, UiFrames.Reference);
            var log = Find(root, "MessageLabel");
            Assert.AreEqual(1188f, log.Rect.Width, 0.01f);
            Assert.AreEqual(108f, log.Rect.Height, 0.01f);
        }

        // READABLE ON ANY BACKDROP. QA 2026-09-26 (REPORT 1.6): white log text
        // over the treant stage's pale canopy was hard to read, and the muted
        // ENEMIES heading above the plates was not readable at all. Every
        // label that floats on the stage art with no plate of its own carries
        // the dark edge; FightFlowTests checks the edge actually draws.
        [TestCase("MessageLabel")]
        [TestCase("EnemiesHeading")]
        [TestCase("EnemiesHint")]
        public void TextFloatingOnTheStageArtCarriesTheDarkEdge(string name)
        {
            var root = UiSolver.Solve(FightScreen.Build().Root, UiFrames.Reference);
            var label = Find(root, name);
            Assert.IsNotNull(label, $"no {name} in the fight tree");
            Assert.IsTrue(label.Source.DrawsOverArt, $"{name} sits on the stage art without the dark edge");
        }
    }
}
