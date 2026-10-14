using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The descent map's geometry (docs/PLAN_RUN_MAP.md section 7): where every
    // node stands, that no two of them ever overlap, and how the camera and the
    // trails move over them.
    //
    // Every expected value is a LITERAL, not the formula re-run: a test that
    // recomputed TopMargin + row * RowPitch would pass just as happily after
    // someone changed the pitch to something the footprints do not fit in.
    public class MapLayoutTests
    {
        private static RealmMap Make(ulong seed, int floor) =>
            RealmMapGenerator.GenerateFor(seed, (floor - 1) * 8, RealmProfiles.Default(floor));

        [Test]
        public void TheLiterals()
        {
            Assert.AreEqual(224f, MapLayout.RowPitch);
            Assert.AreEqual(340f, MapLayout.TopMargin);
            Assert.AreEqual(1500f, MapLayout.SpanWidth);
            Assert.AreEqual(36, MapLayout.MaxNodes, "9 spine + 3 forks + 6 pockets of 4");
            Assert.AreEqual(340f, MapLayout.FollowOffset, "the party stands where the entry does");
        }

        [Test]
        public void RowsRunDownTheScreenFromTheEntryToTheHeart()
        {
            var map = Make(3, 10);
            var graph = MapGraph.Of(map);

            Assert.AreEqual(-340f, graph.PositionOf(map.Entry.Id).Y, 0.01f, "the entry is the first row");

            float heartY = graph.PositionOf(map.Boss.Id).Y;
            Assert.AreEqual(-(340f + (graph.Rows - 1) * 224f), heartY, 0.01f);
            Assert.AreEqual(heartY, graph.HeartCentre.Y, 0.01f);

            foreach (var node in map.Nodes)
            {
                foreach (int next in node.Next)
                {
                    Assert.Less(graph.PositionOf(next).Y, graph.PositionOf(node.Id).Y,
                        $"edge {node.Id}->{next} does not go down the screen");
                }
            }
        }

        [Test]
        public void LateralPlacesAreTheSpineThePairAndThePocketsOnAlternateSides()
        {
            var map = Make(3, 30);
            var graph = MapGraph.Of(map);

            foreach (var node in map.Nodes)
            {
                float x = graph.PositionOf(node.Id).X;
                switch (node.Role)
                {
                    case RealmNodeRole.Spine:
                    case RealmNodeRole.Entry:
                    case RealmNodeRole.Heart:
                        Assert.AreEqual(0f, x, 0.01f);
                        break;
                    case RealmNodeRole.ForkSibling:
                        Assert.AreEqual(225f, System.Math.Abs(x), 0.01f);
                        break;
                    case RealmNodeRole.Pocket:
                        Assert.AreEqual(570f, System.Math.Abs(x), 0.01f);
                        Assert.AreEqual(node.Group % 2 == 1 ? -570f : 570f, x, 0.01f,
                            "neighbouring pockets hang to opposite sides");
                        break;
                }
            }
        }

        // THE KNOWN DEFECT THIS SCREEN WAS REBUILT FOR: P2's capture of a
        // floor-14 realm showed five rooms stacked in three tile slots with
        // overlapping tiles and labels. A footprint is the tile, its caption,
        // its you-are-here tag and the widest prize chip, so two disjoint
        // footprints mean no overlapping tile, label or chip.
        [Test]
        public void NoTwoNodesOrHeartSlotsEverOverlap_OnAnyFloorOfAnySeed()
        {
            int realms = 0;
            int mostNodes = 0;
            int mostAnchors = 0;

            for (int floor = 1; floor <= 100; floor++)
            {
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var map = Make(seed, floor);
                    var graph = MapGraph.Of(map);
                    realms++;
                    mostNodes = System.Math.Max(mostNodes, map.Nodes.Count);
                    mostAnchors = System.Math.Max(mostAnchors, map.Nodes.Count(n => n.IsAnchor));

                    Assert.IsEmpty(graph.Overflow, $"floor {floor} seed {seed} outgrew the node pool");

                    var boxes = new List<(string Name, MapRect Rect)>();
                    foreach (var node in map.Nodes)
                    {
                        boxes.Add(($"node {node.Id} ({node.Type})", graph.FootprintOf(node.Id)));
                    }

                    // Every slot the heart can fill, whether or not an anchor
                    // stands in it today.
                    for (int slot = 0; slot < MapGraph.HeartSlots; slot++)
                    {
                        boxes.Add(($"heart slot {slot}", graph.SlotRect(slot)));
                    }

                    for (int a = 0; a < boxes.Count; a++)
                    {
                        for (int b = a + 1; b < boxes.Count; b++)
                        {
                            Assert.IsFalse(boxes[a].Rect.Intersects(boxes[b].Rect),
                                $"floor {floor} seed {seed}: {boxes[a].Name} overlaps {boxes[b].Name}");
                        }
                    }
                }
            }

            Assert.AreEqual(4000, realms);
            Assert.GreaterOrEqual(mostNodes, 30, "vacuity: the sample reached crowded realms");
            Assert.GreaterOrEqual(mostAnchors, 5, "vacuity: the sample reached anchor-heavy realms");
        }

        [Test]
        public void EveryFootprintStaysInsideTheCanvasAndTheContent()
        {
            for (int floor = 1; floor <= 100; floor += 3)
            {
                for (ulong seed = 1; seed <= 25; seed++)
                {
                    var map = Make(seed, floor);
                    var graph = MapGraph.Of(map);

                    foreach (var node in map.Nodes)
                    {
                        var box = graph.FootprintOf(node.Id);
                        Assert.GreaterOrEqual(box.Left, -960f, $"floor {floor} seed {seed} node {node.Id}");
                        Assert.LessOrEqual(box.Right, 960f, $"floor {floor} seed {seed} node {node.Id}");
                        Assert.LessOrEqual(box.Top, 0f, $"node {node.Id} pokes out of the content's top");
                        Assert.GreaterOrEqual(box.Bottom, -graph.ContentHeight);
                    }

                    for (int slot = 0; slot < MapGraph.HeartSlots; slot++)
                    {
                        var box = graph.SlotRect(slot);
                        Assert.GreaterOrEqual(box.Left, -960f);
                        Assert.LessOrEqual(box.Right, 960f);
                        Assert.GreaterOrEqual(box.Bottom, -graph.ContentHeight + MapScreen.FogHeight,
                            "the heart's queue must sit above the fog");
                    }
                }
            }
        }

        [Test]
        public void EveryDefaultRealmFitsThePools()
        {
            int mostEdges = 0;
            int mostRows = 0;
            float tallest = 0f;

            for (int floor = 1; floor <= 100; floor++)
            {
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var map = Make(seed, floor);
                    var graph = MapGraph.Of(map);

                    Assert.LessOrEqual(map.Nodes.Count, MapLayout.MaxNodes, $"floor {floor} seed {seed}");
                    for (int i = 0; i < map.Nodes.Count; i++)
                    {
                        Assert.AreEqual(i, map.Nodes[i].Id, "ids are dense, which is what lets the id be the pool slot");
                        Assert.AreEqual(i, graph.IndexOf(i));
                        Assert.AreSame(map.Nodes[i], graph.At(i));
                    }

                    mostEdges = System.Math.Max(mostEdges, map.Nodes.Sum(n => n.Next.Count));
                    mostRows = System.Math.Max(mostRows, graph.Rows);
                    tallest = System.Math.Max(tallest, graph.ContentHeight);
                    Assert.LessOrEqual(map.Nodes.Count(n => n.IsAnchor), MapLayout.RootCapacity);
                }
            }

            Assert.LessOrEqual(mostEdges, MapLayout.LinkCapacity, "the trail pool cannot draw every edge");
            Assert.LessOrEqual(mostRows, MapLayout.MaxRows);
            Assert.LessOrEqual(tallest, MapLayout.MaxContentHeight);
            Assert.LessOrEqual(MapLayout.BackgroundTilesFor(tallest), MapLayout.MaxBackgroundTiles);
        }

        [Test]
        public void TheHeartHoldsTwoAnchorsInFrontAndFourInTheQueue()
        {
            Assert.AreEqual(2, MapGraph.FrontSlots);
            Assert.AreEqual(4, MapGraph.QueueSlots);
            Assert.AreEqual(6, MapGraph.HeartSlots);

            var map = Make(3, 30);
            var graph = MapGraph.Of(map);
            var heart = graph.HeartCentre;

            // The strongest two flank the boss; the queue is a row behind it.
            Assert.AreEqual(heart.X - 270f, graph.SlotCentre(0).X, 0.01f);
            Assert.AreEqual(heart.X + 270f, graph.SlotCentre(1).X, 0.01f);
            for (int slot = 2; slot < 6; slot++)
            {
                Assert.AreEqual(heart.Y - 215f, graph.SlotCentre(slot).Y, 0.01f);
            }

            Assert.AreEqual(-156f, graph.SlotCentre(2).X - heart.X, 0.01f);
            Assert.AreEqual(156f, graph.SlotCentre(5).X - heart.X, 0.01f);
        }

        [Test]
        public void TouchingBoxesDoNotIntersect()
        {
            var a = new MapRect(0f, 0f, 10f, 10f);
            Assert.IsFalse(a.Intersects(new MapRect(10f, 0f, 20f, 10f)));
            Assert.IsFalse(a.Intersects(new MapRect(0f, 10f, 10f, 20f)));
            Assert.IsTrue(a.Intersects(new MapRect(9.9f, 9.9f, 20f, 20f)));
        }

        // ---- the camera -----------------------------------------------------------

        [Test]
        public void TheCameraPinsTheCurrentRoomNearTheTopAndNeverLeavesItsContent()
        {
            const float viewport = 1080f;
            float height = MapLayout.ContentHeight(12);
            Assert.AreEqual(340f + 11f * 224f + 560f, height, 0.01f);

            // The entry is already where the camera would put it.
            Assert.AreEqual(0f, MapLayout.Scroll(-340f, height, viewport), 0.01f);

            // Two rows down, the content has moved up by two pitches.
            Assert.AreEqual(448f, MapLayout.Scroll(-340f - 448f, height, viewport), 0.01f);

            // The heart is clamped to the bottom, with the fog and the queue in view.
            float heartY = -(340f + 11f * 224f);
            Assert.AreEqual(height - viewport, MapLayout.Scroll(heartY, height, viewport), 0.01f);
            Assert.AreEqual(height - viewport, MapLayout.ScrollToBottom(height, viewport), 0.01f);

            // A realm shorter than the window does not scroll at all.
            Assert.AreEqual(0f, MapLayout.Scroll(-900f, 700f, viewport), 0.01f);
        }

        [Test]
        public void TheOverviewShrinksTheRealmToFitButNeverEnlargesIt()
        {
            Assert.AreEqual(1f, MapLayout.OverviewScale(500f, 1080f), 0.0001f);
            Assert.AreEqual(780f / 3000f, MapLayout.OverviewScale(3000f, 1080f), 0.0001f);
        }

        [Test]
        public void TheForestTilesDownTheLegAndMirrorsEverySecondTile()
        {
            Assert.AreEqual(0f, MapLayout.BackgroundTileY(0));
            Assert.AreEqual(-1080f, MapLayout.BackgroundTileY(1));
            Assert.IsFalse(MapLayout.BackgroundTileIsMirrored(0));
            Assert.IsTrue(MapLayout.BackgroundTileIsMirrored(1));
            Assert.IsFalse(MapLayout.BackgroundTileIsMirrored(2));

            Assert.AreEqual(1, MapLayout.BackgroundTilesFor(1080f));
            Assert.AreEqual(2, MapLayout.BackgroundTilesFor(1081f));
            Assert.AreEqual(0, MapLayout.BackgroundTilesFor(0f));
        }

        // ---- trails ---------------------------------------------------------------

        // The wood is what the viewport shows behind everything, so wherever the
        // camera rests -- the entry, the heart, anywhere between -- the tiles
        // must reach the viewport's bottom edge. A leg shorter than the window
        // (one row) still gets a whole tile.
        [Test]
        public void TheForestCoversTheWholeViewportAtEveryScrollOfEveryRealmHeight()
        {
            const float viewport = 1080f;

            for (int rows = 1; rows <= MapLayout.MaxRows; rows++)
            {
                float content = MapLayout.ContentHeight(rows);
                float painted = MapLayout.BackgroundTilesFor(content) * MapLayout.BackgroundTileHeight;
                float deepest = MapLayout.ScrollToBottom(content, viewport) + viewport;

                Assert.GreaterOrEqual(painted, deepest, $"{rows} rows: the camera at the heart sees past the wood");
                Assert.GreaterOrEqual(painted, content, $"{rows} rows: the wood ends above the leg's own bottom");
                Assert.GreaterOrEqual(painted, viewport, $"{rows} rows: a short leg leaves bare canvas");
                Assert.LessOrEqual(MapLayout.BackgroundTilesFor(content), MapLayout.MaxBackgroundTiles, $"{rows} rows");
            }
        }

        [Test]
        public void ATrailLeavesAndArrivesVertically()
        {
            var from = new UiVec(0f, -340f);
            var to = new UiVec(570f, -788f);

            var start = MapLayout.PathPoint(from, to, 7, 0f);
            var end = MapLayout.PathPoint(from, to, 7, 1f);
            Assert.AreEqual(from.X, start.X, 0.001f);
            Assert.AreEqual(from.Y, start.Y, 0.001f);
            Assert.AreEqual(to.X, end.X, 0.001f);
            Assert.AreEqual(to.Y, end.Y, 0.001f);

            // Near the ends the curve runs along the row axis, not across it:
            // the first piece is steeper than 45 degrees downward.
            var first = MapLayout.SegmentAt(from, to, 7, 0);
            Assert.Less(first.AngleDegrees, -45f);
            Assert.Greater(first.AngleDegrees, -135f);
        }

        [Test]
        public void ARootStartsAndEndsOnItsEndpointsAndDiffersFromATrail()
        {
            var from = new UiVec(570f, -788f);
            var to = new UiVec(270f, -1300f);

            var start = MapLayout.RootPoint(from, to, 3, 0f);
            var end = MapLayout.RootPoint(from, to, 3, 1f);
            Assert.AreEqual(from.X, start.X, 0.001f);
            Assert.AreEqual(to.Y, end.Y, 0.001f);

            Assert.AreNotEqual(MapLayout.PathPoint(from, to, 3, 0.5f).X, MapLayout.RootPoint(from, to, 3, 0.5f).X,
                "a root lies on its own curve, not on a trail");
        }

        // ---- the node grammar -----------------------------------------------------

        [Test]
        public void ChipsGrowOneStepPerPip()
        {
            Assert.AreEqual(64f, MapLayout.ChipWidthFor(1), 0.001f);
            Assert.AreEqual(74f, MapLayout.ChipWidthFor(2), 0.001f);
            Assert.AreEqual(84f, MapLayout.ChipWidthFor(3), 0.001f);
            Assert.AreEqual(24f, MapLayout.ChipHeightFor(1), 0.001f);
            Assert.AreEqual(32f, MapLayout.ChipHeightFor(3), 0.001f);
            Assert.AreEqual(84f, MapLayout.MaxChipWidth, 0.001f);

            // A node with no frozen strength reads as the smallest, as the
            // prize payout does.
            Assert.AreEqual(64f, MapLayout.ChipWidthFor(0), 0.001f);
        }

        [Test]
        public void TheTallestFootprintFitsTheRowPitch()
        {
            float tallest = 0f;
            foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
            {
                var box = MapLayout.Footprint(type, UiVec.Zero);
                if (type != RoomType.Boss) tallest = System.Math.Max(tallest, box.Height);
            }

            Assert.AreEqual(209f, tallest, 0.01f);
            Assert.Less(tallest, MapLayout.RowPitch);
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

        // ---- the veil -------------------------------------------------------------

        // Taking the spine past a pocket shuts the pocket; entering the pocket
        // shuts nothing, because it rejoins the spine.
        [Test]
        public void OnlyASkippedPocketIsClosedOff()
        {
            int checkedPockets = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var map = Make(seed, 14);
                foreach (var pocket in map.Pockets)
                {
                    var before = map.SpineAt(pocket.Stretch).ToList();
                    var after = map.SpineAt(pocket.Stretch + 1).ToList();
                    if (before.Count != 1 || after.Count != 1) continue;

                    var head = map.Node(pocket.Rooms[0]);
                    var closedByPocket = map.ClosedBy(before[0].Id, head.Id);
                    Assert.IsEmpty(closedByPocket, $"seed {seed}: entering a pocket shuts nothing");

                    var closedBySpine = map.ClosedBy(before[0].Id, after[0].Id);
                    CollectionAssert.AreEquivalent(pocket.Rooms, closedBySpine,
                        $"seed {seed}: the spine choice shuts exactly the pocket's rooms");
                    checkedPockets++;
                }
            }

            Assert.Greater(checkedPockets, 100, "vacuity: the sample reached single-spine pockets");
        }

        [Test]
        public void ChoosingOneOfAForkPairClosesItsSibling()
        {
            int checkedForks = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var map = Make(seed, 14);
                foreach (var entry in map.Nodes)
                {
                    var siblings = entry.Next.Select(map.Node)
                        .Where(n => n.Role == RealmNodeRole.ForkSibling).ToList();
                    if (siblings.Count != 2) continue;

                    var closed = map.ClosedBy(entry.Id, siblings[0].Id);
                    Assert.IsTrue(closed.Contains(siblings[1].Id), $"seed {seed}: the other sibling is closed off");
                    Assert.IsFalse(closed.Contains(siblings[0].Id));
                    checkedForks++;
                }
            }

            Assert.Greater(checkedForks, 50, "vacuity: the sample reached forks");
        }
    }
}
