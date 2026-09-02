using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The descent map's tree, audited before any scene exists.
    public class MapScreenTests
    {
        [Test]
        public void TheTreeAuditsClean()
        {
            var screen = MapScreen.Build();
            var solved = UiSolver.Solve(screen.Root, new UiVec(1920f, 1080f));
            var errors = UiAudit.Run(solved, new UiVec(1920f, 1080f));

            Assert.IsEmpty(errors,
                "first 8 of " + errors.Count + ": " +
                string.Join(" | ", errors.Take(8).Select(e => e.ToString())));
        }

        // THE FOREST IS TILED, so a tile has to be a tile.
        //
        // The backdrop was declared UiSize.Fill, which took BOTH axes from its
        // parent -- and its parent is the whole scrolling leg. While every
        // screen was pinned to 1920 that resolved to 1920 and looked
        // deliberate; the moment screens were unpinned (34098f9) all eight
        // tiles became 8824 wide, one repeat of the art was stretched across
        // the entire wood, and the visible slice was magnified about five
        // times. Nothing caught it: the audit has no opinion about a sprite
        // being too big for its art, every tile was still inside its parent,
        // and the only symptom was that the trees looked wrong.
        //
        // Solved rather than asserted against the layout constant alone, so
        // this fails for a tile sized by its PARENT as well as for one given
        // the wrong number.
        [Test]
        public void EachForestTileIsOneTileWideRatherThanTheWholeLeg()
        {
            var screen = MapScreen.Build();
            var solved = UiSolver.Solve(screen.Root, new UiVec(1920f, 1080f));

            // The trailing digit matters: the pool's own container is called
            // "MapBackdrops", so a plain prefix match picks up the 8824-wide
            // parent as a ninth tile and reports the container's width as a
            // failure of the tiles.
            var tiles = Flatten(solved)
                .Where(n => n.Source != null
                            && n.Source.Name.StartsWith("MapBackdrop")
                            && char.IsDigit(n.Source.Name[n.Source.Name.Length - 1]))
                .ToList();

            Assert.IsNotEmpty(tiles, "the map drew no forest at all");
            Assert.AreEqual(MapLayout.MaxBackgroundTiles, tiles.Count);

            foreach (var tile in tiles)
            {
                Assert.AreEqual(MapLayout.BackgroundTileWidth, tile.Rect.Width, 0.5f,
                    $"'{tile.Source.Name}' is {tile.Rect.Width:F0} wide against a tile width of " +
                    $"{MapLayout.BackgroundTileWidth:F0}. A tile as wide as the content it repeats " +
                    "across is not a tile - it is one copy of the art stretched over the whole leg.");
            }
        }

        // A repeat wider than the tile drawing it leaves unpainted stripes
        // between copies. Cheap to state, and it is the invariant that makes
        // the width above a number rather than a preference.
        [Test]
        public void TheTileIsWideEnoughToCoverItsOwnRepeat()
        {
            Assert.GreaterOrEqual(MapLayout.BackgroundTileWidth, MapLayout.BackgroundPeriod,
                $"the forest repeats every {MapLayout.BackgroundPeriod:F0} but each tile is only " +
                $"{MapLayout.BackgroundTileWidth:F0} wide, so the wood has gaps in it");
        }

        private static System.Collections.Generic.IEnumerable<SolvedNode> Flatten(SolvedNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var descendant in Flatten(child)) yield return descendant;
            }
        }

        [Test]
        public void AbandonWearsSilverLikeEveryOtherExit()
        {
            // Room nodes stay unthemed -- the painted tree IS their plate.
            Assert.AreEqual(ButtonTheme.Silver, MapScreen.Build().AbandonButton.Node.Theme);
        }

        [Test]
        public void ThePoolCoversEveryPositionALegCouldUse()
        {
            var screen = MapScreen.Build();
            Assert.AreEqual(MapLayout.Capacity, screen.NodeButtons.Count);
            Assert.AreEqual(screen.NodeButtons.Count, screen.NodeLabels.Count);
            Assert.AreEqual(screen.NodeButtons.Count, screen.NodeMarkers.Count);
        }
    }
}
