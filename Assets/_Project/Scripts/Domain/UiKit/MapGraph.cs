using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.UiKit
{
    // One realm placed on the map screen (docs/PLAN_RUN_MAP.md section 7): the
    // content-local centre of every node, the box each one occupies, and the
    // slots the heart's anchors stand in. RealmLayout supplies rows and lateral
    // places in 0..1; MapLayout turns them into pixels.
    //
    // A node's pool slot IS its id. Ids are dense (the generator numbers them
    // along edges), so the slot never has to be looked up, and a node keeps the
    // same slot, name and navigation handle for the whole realm.
    public sealed class MapGraph
    {
        private readonly Dictionary<int, UiVec> _positions = new Dictionary<int, UiVec>();
        private readonly Dictionary<int, RealmNode> _nodes = new Dictionary<int, RealmNode>();

        // Ids that did not fit MapLayout.MaxNodes. Empty for every default
        // profile.
        public readonly List<int> Overflow = new List<int>();

        public int Rows { get; private set; }

        public float ContentHeight { get; private set; }

        public UiVec HeartCentre { get; private set; }

        public static MapGraph Of(RealmMap map)
        {
            var graph = new MapGraph();
            if (map == null || map.Nodes.Count == 0)
            {
                graph.ContentHeight = MapLayout.ContentHeight(0);
                return graph;
            }

            var placed = RealmLayout.Place(map);
            graph.Rows = placed.Rows;
            graph.ContentHeight = MapLayout.ContentHeight(placed.Rows);

            foreach (var node in map.Nodes.OrderBy(n => n.Id))
            {
                if (node.Id < 0 || node.Id >= MapLayout.MaxNodes)
                {
                    graph.Overflow.Add(node.Id);
                    continue;
                }

                var at = placed.Nodes[node.Id];
                float x = (at.Across - 0.5f) * MapLayout.SpanWidth;
                float y = -(MapLayout.TopMargin + at.Row * MapLayout.RowPitch);

                graph._nodes[node.Id] = node;
                graph._positions[node.Id] = new UiVec(x, y);
                if (node.Role == RealmNodeRole.Heart) graph.HeartCentre = new UiVec(x, y);
            }

            return graph;
        }

        public bool Contains(int nodeId) => _positions.ContainsKey(nodeId);

        // The pool slot a node owns, or -1 when it overflowed.
        public int IndexOf(int nodeId) => Contains(nodeId) ? nodeId : -1;

        // The node a pool slot shows, or null for an unused one.
        public RealmNode At(int index) => _nodes.TryGetValue(index, out var node) ? node : null;

        public IEnumerable<RealmNode> Nodes => _nodes.Values;

        public UiVec PositionOf(int nodeId) =>
            _positions.TryGetValue(nodeId, out var at) ? at : new UiVec(0f, -MapLayout.TopMargin);

        public MapRect FootprintOf(int nodeId) =>
            _nodes.TryGetValue(nodeId, out var node)
                ? MapLayout.Footprint(node.Type, PositionOf(nodeId))
                : default;

        // ---- the heart's anchors ------------------------------------------------

        // The boss stands in the rear, the strongest two anchors in front of it
        // on either side, and the rest queue behind as a row of smaller
        // portraits (plan section 6). Slot geometry only: who stands where is
        // HeartAnchors.Formation.
        public const float FrontOffsetX = 270f;
        public const float FrontOffsetY = -30f;
        public const float FrontWidth = 130f;
        public const float FrontHeight = 180f;

        public const float QueueOffsetY = -215f;
        public const float QueuePitch = 104f;
        public const float QueueWidth = 90f;
        public const float QueueHeight = 124f;

        public static int FrontSlots => HeartAnchors.FrontCount(RealmProfiles.MaxDefaultAnchors,
            FightHudSpec.StageSlotsPerSide);

        public static int QueueSlots => RealmProfiles.MaxDefaultAnchors - FrontSlots;

        public static int HeartSlots => FrontSlots + QueueSlots;

        // Slot i: the front slots first (left, then right), then the queue.
        public bool IsFrontSlot(int slot) => slot < FrontSlots;

        public UiVec SlotCentre(int slot)
        {
            if (slot < FrontSlots)
            {
                float side = slot % 2 == 0 ? -1f : 1f;
                return new UiVec(HeartCentre.X + side * FrontOffsetX, HeartCentre.Y + FrontOffsetY);
            }

            int queued = slot - FrontSlots;
            float start = -(QueueSlots - 1) * 0.5f * QueuePitch;
            return new UiVec(HeartCentre.X + start + queued * QueuePitch, HeartCentre.Y + QueueOffsetY);
        }

        public MapRect SlotRect(int slot) =>
            IsFrontSlot(slot)
                ? MapRect.Around(SlotCentre(slot), FrontWidth, FrontHeight)
                : MapRect.Around(SlotCentre(slot), QueueWidth, QueueHeight);
    }
}
