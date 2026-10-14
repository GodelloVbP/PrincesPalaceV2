using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The heart drawn with its anchors (docs/PLAN_RUN_MAP.md sections 2, 6, 7).
    //
    // Who stands there is RunEncounter.LivingAnchors, the same list the boss
    // fight fields, in the same formation order: the first two flank the boss,
    // the rest queue behind as smaller portraits. A painted root joins every
    // anchor on the map to the heart; it runs to the anchor's portrait while
    // the anchor lives and withers to the boss itself once the anchor is gone.
    public partial class MapController
    {
        private const string HeartStance = "idle";

        // Portraits are drawn inside their slot box with room left under them
        // for the name.
        private const float FrontPortraitHeight = 150f;
        private const float QueuePortraitHeight = 92f;
        private const float FrontNameDrop = 75f;
        private const float QueueNameDrop = 48f;
        private const float FrontPortraitRise = 15f;
        private const float QueuePortraitRise = 16f;

        private static readonly Color RootLiving = Hex(MapScreen.RootLivingHex);
        private static readonly Color RootCoreLiving = Hex(MapScreen.RootCoreLivingHex);
        private static readonly Color RootWithered = Hex(MapScreen.RootWitheredHex);
        private static readonly Color RootCoreWithered = Hex(MapScreen.RootCoreWitheredHex);

        private void PaintHeart(RealmMap map, MapGraph graph, RunSnapshot run, HashSet<int> cleared)
        {
            if (map.Boss == null || !graph.Contains(map.Boss.Id))
            {
                HideHeart();
                HideRoots(0);
                return;
            }

            var anchors = RunEncounter.LivingAnchors(run);
            PaintHeartPortraits(graph, anchors);
            PaintRoots(map, graph, anchors);
        }

        private void PaintHeartPortraits(MapGraph graph, IReadOnlyList<HeartAnchors.Anchor> anchors)
        {
            if (heartPortraits == null) return;

            for (int slot = 0; slot < heartPortraits.Length; slot++)
            {
                var portrait = heartPortraits[slot];
                var label = heartNames[slot];
                if (slot >= anchors.Count)
                {
                    portrait.gameObject.SetShown(false);
                    label.gameObject.SetShown(false);
                    continue;
                }

                var enemy = ContentDatabase.GetEnemy(anchors[slot].EnemyId)?.Data;
                var sprite = enemy != null && !string.IsNullOrWhiteSpace(enemy.SpritePath)
                    ? StanceAnimationLibrary.Resolve(enemy.SpritePath, HeartStance)
                    : null;

                bool front = graph.IsFrontSlot(slot);
                var centre = graph.SlotCentre(slot);

                portrait.gameObject.SetShown(true);
                portrait.sprite = sprite != null ? sprite : eliteIcon;
                portrait.preserveAspect = true;
                portrait.color = Color.white;
                portrait.rectTransform.sizeDelta = front
                    ? new Vector2(MapGraph.FrontWidth, FrontPortraitHeight)
                    : new Vector2(MapGraph.QueueWidth, QueuePortraitHeight);
                portrait.rectTransform.anchoredPosition = new Vector2(
                    centre.X, centre.Y + (front ? FrontPortraitRise : QueuePortraitRise));

                label.gameObject.SetShown(true);
                label.rectTransform.sizeDelta = new Vector2(
                    front ? MapGraph.FrontWidth : MapGraph.QueueWidth, MapLayout.CaptionHeight);
                label.rectTransform.anchoredPosition = new Vector2(
                    centre.X, centre.Y - (front ? FrontNameDrop : QueueNameDrop));
                label.SetContent(enemy == null ? string.Empty
                    : string.IsNullOrEmpty(enemy.DisplayName) ? enemy.Id : enemy.DisplayName);
                label.color = CaptionColour;
            }
        }

        // One root per anchor node on the map, standing or fallen.
        private void PaintRoots(RealmMap map, MapGraph graph, IReadOnlyList<HeartAnchors.Anchor> anchors)
        {
            if (rootSegments == null || rootSegments.Length == 0) return;

            var heart = graph.HeartCentre;
            int segment = 0;

            foreach (var node in map.Nodes.Where(n => n.IsAnchor && graph.Contains(n.Id)))
            {
                if (segment + MapLayout.SegmentsPerLink > rootSegments.Length) break;

                int slot = -1;
                for (int i = 0; i < anchors.Count; i++)
                {
                    if (anchors[i].NodeId == node.Id) slot = i;
                }

                bool living = slot >= 0;
                var from = graph.PositionOf(node.Id);
                var to = living ? graph.SlotCentre(slot) : heart;
                var colour = living ? RootLiving : RootWithered;
                var core = living ? RootCoreLiving : RootCoreWithered;
                float width = living ? MapScreen.RootWidthLiving : MapScreen.RootWidthWithered;

                for (int i = 0; i < MapLayout.SegmentsPerLink; i++)
                {
                    var piece = MapLayout.RootSegmentAt(from, to, node.Id, i);
                    PlaceSegment(rootSegments[segment], rootCores, segment, piece, colour, core, width);
                    segment++;
                }
            }

            HideRoots(segment);
        }

        private void HideHeart()
        {
            if (heartPortraits == null) return;
            for (int i = 0; i < heartPortraits.Length; i++)
            {
                heartPortraits[i].gameObject.SetShown(false);
                heartNames[i].gameObject.SetShown(false);
            }
        }
    }
}
