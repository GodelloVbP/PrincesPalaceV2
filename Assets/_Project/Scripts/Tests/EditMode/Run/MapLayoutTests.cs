using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The descent map's geometry, which is the half of this screen that has to
    // agree with a painting.
    //
    // Every expected value here is a LITERAL, not the formula re-run: these
    // numbers were measured off forest_map_background.png, and a test that
    // recomputed ClearingColumnX[0] + depth * ColumnGap would pass just as
    // happily after someone changed the art's pitch to something the art does
    // not have.
    public class MapLayoutTests
    {
        [Test]
        public void ColumnsStandOnThePaintedClearings()
        {
            Assert.AreEqual(520f, MapLayout.ColumnX(0), 0.01f, "column 0 is the left painted clearing");
            Assert.AreEqual(1383f, MapLayout.ColumnX(1), 0.01f, "column 1 is the right painted clearing");

            // Past the pair the art paints, the grid continues at the same
            // stride because the background tiles at exactly two columns. If
            // this drifts, rooms from column 2 on stand on canopy.
            Assert.AreEqual(2246f, MapLayout.ColumnX(2), 0.01f);
            Assert.AreEqual(7424f, MapLayout.ColumnX(8), 0.01f);
        }

        [Test]
        public void RowsSnapToAClearingBySlotAndAreNotCentred()
        {
            Assert.AreEqual(322f, MapLayout.RowY(0), 0.01f);
            Assert.AreEqual(11f, MapLayout.RowY(1), 0.01f);
            Assert.AreEqual(-302f, MapLayout.RowY(2), 0.01f);

            // The property that matters, stated directly: a column of two rooms
            // uses the top and middle clearings and leaves the bottom one empty.
            // Centring it -- which is what this screen did before the backdrop
            // was tiled -- would put both rooms between painted clearings.
            Assert.AreEqual(MapLayout.RowY(0), MapLayout.ClearingRowY[0], 0.01f);
            Assert.AreEqual(MapLayout.RowY(1), MapLayout.ClearingRowY[1], 0.01f);
        }

        [Test]
        public void TheBackgroundRepeatsEveryTwoColumns()
        {
            Assert.AreEqual(1726f, MapLayout.BackgroundPeriod, 0.01f);

            // Tile i's own left clearing has to land on column 2i, which is the
            // whole reason the period is what it is.
            for (int tile = 0; tile < 4; tile++)
            {
                Assert.AreEqual(MapLayout.ColumnX(tile * 2),
                    MapLayout.BackgroundTileX(tile) + MapLayout.ClearingColumnX[0], 0.01f,
                    $"tile {tile}'s left clearing must sit under column {tile * 2}");
            }
        }

        [Test]
        public void EveryLegHasEnoughForest()
        {
            Assert.AreEqual(5, MapLayout.BackgroundTilesFor(9), "a 9-column leg needs 5 tiles");
            Assert.AreEqual(5, MapLayout.BackgroundTilesFor(10));
            Assert.AreEqual(1, MapLayout.BackgroundTilesFor(1));
            Assert.AreEqual(0, MapLayout.BackgroundTilesFor(0));

            Assert.LessOrEqual(MapLayout.BackgroundTilesFor(MapLayout.Columns), MapLayout.MaxBackgroundTiles,
                "the declared pool has to cover a standard leg");
        }

        // The bug this screen shipped with: nine columns crushed into a fixed
        // 1500px track. The fix is only real if the camera can actually carry
        // the LAST column to the clearing the current room stands on.
        [Test]
        public void TheCameraCanReachEveryColumnIncludingTheLast()
        {
            const float viewport = 1920f;
            float width = MapLayout.ContentWidth(MapLayout.Columns, viewport);

            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                float x = MapLayout.ColumnX(depth);
                float scroll = MapLayout.Scroll(x, width, viewport);

                // Where the room ends up on screen, measured from the
                // viewport's own left edge.
                float onScreen = x + scroll;

                // Column 0 cannot reach the follow offset -- there is nothing to
                // its left to scroll in -- so it sits further right, which is
                // correct and is what the clamp is for.
                if (depth == 0)
                {
                    Assert.AreEqual(520f, onScreen, 0.01f, "the entrance starts already on its clearing");
                    continue;
                }

                Assert.AreEqual(MapLayout.FollowOffset, onScreen, 0.01f,
                    $"column {depth} must pin to the left clearing");
            }
        }

        [Test]
        public void TheColumnBeingChosenIntoLandsOnTheOtherClearing()
        {
            const float viewport = 1920f;
            float width = MapLayout.ContentWidth(MapLayout.Columns, viewport);

            // Standing at column 3, the rooms on offer are column 4 -- and they
            // have to land on the right painted clearing, which is the whole
            // point of pinning the current room to the left one.
            float scroll = MapLayout.Scroll(MapLayout.ColumnX(3), width, viewport);
            Assert.AreEqual(1383f, MapLayout.ColumnX(4) + scroll, 0.01f);

            // And the column after that is off the right edge of a 1920 window,
            // which is what "you see the choice, not the whole leg" means.
            Assert.Greater(MapLayout.ColumnX(5) + scroll, viewport);
        }

        // Re-derived, not ported: v1 measured the fog from the content edge
        // while measuring columns from the first clearing, so its 520-wide
        // opaque fade started 470 units short of the boss and covered it.
        [Test]
        public void TheFogSitsPastTheLastColumnRatherThanOnTopOfIt()
        {
            for (int depth = 1; depth <= MapLayout.Columns; depth++)
            {
                float lastColumnRightEdge = MapLayout.ColumnX(depth - 1) + MapLayout.MaxTileWidth * 0.5f;
                Assert.GreaterOrEqual(MapLayout.FogX(depth), lastColumnRightEdge,
                    $"a {depth}-column leg's fog must start past its last room");
            }
        }

        [Test]
        public void TheContentIsWideEnoughToHoldTheFogItPlaces()
        {
            const float viewport = 1920f;
            for (int depth = 1; depth <= MapLayout.Columns; depth++)
            {
                float width = MapLayout.ContentWidth(depth, viewport);
                Assert.GreaterOrEqual(width, MapLayout.FogX(depth) + MapLayout.FogWidth,
                    $"a {depth}-column leg's content must contain its own fog");
            }
        }

        // A completeness guard, not a spot check: adding a room type should
        // fail here rather than silently render as a bare tree forever.
        [Test]
        public void EveryRoomTypeEitherHasPaintedArtOrIsKnownNotTo()
        {
            var unpainted = new[]
            {
                RoomType.Entry, RoomType.Shop, RoomType.ItemSpawn, RoomType.Unknown,
            };

            foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
            {
                string key = MapScreen.IconKeyFor(type);
                bool expected = System.Array.IndexOf(unpainted, type) < 0;

                Assert.AreEqual(expected, key != null,
                    expected
                        ? $"{type} has painted art on disk and should be showing it"
                        : $"{type} has no painted icon yet - add it to this list once it does");
            }
        }
    }
}
