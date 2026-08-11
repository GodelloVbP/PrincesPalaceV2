using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

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
        [SerializeField] internal TMP_Text depthLabel;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Button abandonButton;

        private static readonly Color Reachable = new Color(0.85f, 0.76f, 0.55f, 1f);
        private static readonly Color Cleared = new Color(0.34f, 0.30f, 0.42f, 1f);
        private static readonly Color Distant = new Color(0.22f, 0.19f, 0.29f, 1f);

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
