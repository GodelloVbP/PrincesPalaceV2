using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The descent map, painted from the run.
    //
    // The tree built a pool at every position a leg COULD use; this switches on
    // the ones the current leg actually has and re-anchors each column to its
    // real width -- through MapLayout.RowY, the same function the build used.
    public class MapController : MonoBehaviour
    {
        [SerializeField] internal Button[] nodeButtons;
        [SerializeField] internal TMP_Text[] nodeLabels;
        [SerializeField] internal Image[] nodeMarkers;
        [SerializeField] internal Image[] trailSegments;
        [SerializeField] internal TMP_Text depthLabel;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Button abandonButton;

        private static readonly Color Reachable = new Color(0.85f, 0.76f, 0.55f, 1f);
        private static readonly Color Cleared = new Color(0.34f, 0.30f, 0.42f, 1f);
        private static readonly Color Distant = new Color(0.22f, 0.19f, 0.29f, 1f);

        // Parsed from the SCREEN's own constants rather than restated here, so
        // the declared tree and the runtime cannot disagree about what a walked
        // path looks like.
        //
        // ColorUtility, not SceneBuilder.ParseHex: that one lives in the Editor
        // assembly and this is Core, which cannot see it. Unity's own parser
        // takes the same "#RRGGBBAA" the DSL writes.
        private static readonly Color TrailTaken = Hex(MapScreen.TrailTakenHex);
        private static readonly Color TrailOpen = Hex(MapScreen.TrailOpenHex);
        private static readonly Color TrailAhead = Hex(MapScreen.TrailAheadHex);
        private static readonly Color TrailClosed = Hex(MapScreen.TrailClosedHex);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.white;

        private bool _wired;

        private void Start()
        {
            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < nodeButtons.Length; i++)
            {
                int index = i;
                nodeButtons[i].onClick.AddListener(() => OnNodePressed(index));
            }

            abandonButton.onClick.AddListener(() =>
            {
                // EndRun settles the books itself. This call used to discard
                // the run without paying it, so walking away from a descent
                // threw away every ember its bosses had earned -- silently,
                // because a discarded snapshot looks the same either way.
                RunManager.EndRun();
                Navigation.Go(Navigation.Hub);
            });
        }

        public void Refresh()
        {
            var map = RunManager.Map;
            var current = RunManager.CurrentNode;

            // No run: every node off, and the labels say nothing rather than
            // lying about a descent that is not happening.
            if (map == null || current == null)
            {
                foreach (var button in nodeButtons) SetActive(button.gameObject, false);
                HideTrails(0);
                return;
            }

            var reachable = new HashSet<int>(RunManager.Choices().Select(n => n.Id));
            var cleared = new HashSet<int>(RunManager.Run.clearedNodeIds);

            depthLabel.Set(UiStrings.MapDepth, RunManager.Run.step, RunManager.Run.floor);
            goldLabel.Set(UiStrings.MapGold, RunManager.Run.gold);

            var occupied = new HashSet<int>();

            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                var column = map.AtDepth(depth).ToList();

                for (int slot = 0; slot < column.Count && slot < MapLayout.Rows; slot++)
                {
                    var node = column[slot];
                    int index = MapLayout.IndexFor(depth, slot);
                    if (index >= nodeButtons.Length) continue;

                    occupied.Add(index);
                    PaintNode(index, node, column.Count, slot,
                        isCurrent: node.Id == current.Id,
                        isReachable: reachable.Contains(node.Id),
                        isCleared: cleared.Contains(node.Id));
                }
            }

            // Everything the leg did not use. Hidden rather than dimmed: an
            // empty slot is not a room the player failed to reach, it is a room
            // that does not exist.
            for (int i = 0; i < nodeButtons.Length; i++)
            {
                if (!occupied.Contains(i)) SetActive(nodeButtons[i].gameObject, false);
            }

            PaintTrails(map, current.Id, cleared, reachable);
        }

        // The paths between rooms, which is what makes this a map rather than a
        // grid of buttons. Without them nothing on screen says which room leads
        // to which, and a fork is indistinguishable from two unrelated rooms
        // that happen to sit in the same column.
        private void PaintTrails(DescentMap map, int currentId, HashSet<int> cleared, HashSet<int> reachable)
        {
            if (trailSegments == null || trailSegments.Length == 0) return;

            var positions = NodePositions(map);
            int segment = 0;
            int seed = 0;

            foreach (var node in map.Nodes)
            {
                if (!positions.TryGetValue(node.Id, out var from)) continue;

                foreach (int nextId in node.Next)
                {
                    seed++;
                    if (!positions.TryGetValue(nextId, out var to)) continue;

                    // The pool is sized to the worst case a leg can generate,
                    // so running out means the generator changed shape rather
                    // than that this needs a bigger number. Stop rather than
                    // draw half a trail.
                    if (segment + MapLayout.SegmentsPerLink > trailSegments.Length) break;

                    // WALKED means both ends are behind you. Colouring a trail
                    // by its destination alone would light up every path INTO a
                    // cleared room, including ones never taken.
                    bool taken = cleared.Contains(node.Id) && cleared.Contains(nextId);
                    bool fromHere = node.Id == currentId;

                    Color colour;
                    float width;
                    if (taken) { colour = TrailTaken; width = MapScreen.TrailWidthTaken; }
                    else if (fromHere) { colour = TrailOpen; width = MapScreen.TrailWidthOpen; }
                    else if (reachable.Contains(node.Id) || cleared.Contains(node.Id))
                    { colour = TrailAhead; width = MapScreen.TrailWidthAhead; }
                    else { colour = TrailClosed; width = MapScreen.TrailWidthClosed; }

                    for (int i = 0; i < MapLayout.SegmentsPerLink; i++)
                    {
                        var piece = MapLayout.SegmentAt(from, to, seed, i);
                        var image = trailSegments[segment++];

                        SetActive(image.gameObject, true);
                        image.color = colour;

                        var rect = image.rectTransform;
                        rect.sizeDelta = new Vector2(piece.Length, width);
                        rect.anchoredPosition = new Vector2(piece.Centre.X, piece.Centre.Y);
                        rect.localRotation = Quaternion.Euler(0f, 0f, piece.AngleDegrees);
                    }
                }
            }

            HideTrails(segment);
        }

        // Where every room in this leg actually sits, by the same MapLayout the
        // nodes were placed with -- so a trail can never land somewhere its
        // room is not.
        private static Dictionary<int, UiVec> NodePositions(DescentMap map)
        {
            var positions = new Dictionary<int, UiVec>();

            for (int depth = 0; depth < MapLayout.Columns; depth++)
            {
                var column = map.AtDepth(depth).ToList();
                for (int slot = 0; slot < column.Count && slot < MapLayout.Rows; slot++)
                {
                    positions[column[slot].Id] =
                        new UiVec(MapLayout.ColumnX(depth), MapLayout.RowY(column.Count, slot));
                }
            }

            return positions;
        }

        private void HideTrails(int from)
        {
            if (trailSegments == null) return;
            for (int i = from; i < trailSegments.Length; i++)
            {
                if (trailSegments[i] != null) SetActive(trailSegments[i].gameObject, false);
            }
        }

        private void PaintNode(int index, DescentNode node, int columnWidth, int slot,
                               bool isCurrent, bool isReachable, bool isCleared)
        {
            var button = nodeButtons[index];
            SetActive(button.gameObject, true);

            // RE-ANCHORED to the column's real width, through the same function
            // the tree placed it with. Built at full width and moved here, so a
            // two-room column sits centred rather than leaving a gap where the
            // third would have been.
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(
                MapLayout.ColumnX(node.Depth),
                MapLayout.RowY(columnWidth, slot));

            nodeLabels[index].SetContent(Caption(node.Type));

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = isCleared ? Cleared : isReachable ? Reachable : Distant;
            }

            // Only what the party can actually walk to. The map is the authority
            // on adjacency and RunManager refuses anything else anyway, but a
            // button that visibly does nothing is worse than one that is plainly
            // not offered.
            button.interactable = isReachable;

            SetActive(nodeMarkers[index].gameObject, isCurrent);
        }

        private static string Caption(RoomType type)
        {
            switch (type)
            {
                case RoomType.Entry: return "START";
                case RoomType.Fight: return "FIGHT";
                case RoomType.EliteFight: return "ELITE";
                case RoomType.Boss: return "BOSS";
                case RoomType.Treasure: return "TREASURE";
                case RoomType.Rest: return "REST";
                case RoomType.Shop: return "SHOP";
                case RoomType.Event: return "EVENT";
                default: return type.ToString().ToUpperInvariant();
            }
        }

        private void OnNodePressed(int index)
        {
            var map = RunManager.Map;
            if (map == null) return;

            int depth = index / MapLayout.Rows;
            int slot = index % MapLayout.Rows;
            var node = map.AtDepth(depth).ElementAtOrDefault(slot);
            if (node == null) return;

            if (!RunManager.MoveTo(node.Id)) return;

            // Only rooms that are FIGHTS lead anywhere yet. The rest are screens
            // that do not exist, so the party arrives and the map redraws rather
            // than loading an empty scene.
            if (IsFight(node.Type))
            {
                Navigation.Go(Navigation.Fight);
                return;
            }

            RunManager.ClearCurrentRoom();
            Refresh();
        }

        private static bool IsFight(RoomType type) =>
            type == RoomType.Fight || type == RoomType.EliteFight || type == RoomType.Boss;

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
