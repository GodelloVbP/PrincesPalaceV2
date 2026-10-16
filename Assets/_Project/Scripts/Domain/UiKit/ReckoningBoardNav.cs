using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit
{
    // The Reckoning board's pad graph (docs/PLAN_REWARD_SCREENS.md C8): the
    // groups and links ReckoningController hands RuntimeNavWiring, and that
    // ReckoningBoardNavTests resolves through the same UiNavLinkBuilder.
    //
    // A column of three rows under one entry: Continue at the foot, the haul
    // strip above it (a clamped rail, so Left/Right reads the strip end to end),
    // the portraits above that (a wrapped rail, as every party row is). Up
    // climbs, Down returns; a missing row closes the column up.
    public sealed class ReckoningBoardNav<TNode> where TNode : class
    {
        public const string PortraitsId = "reckoningPortraits";
        public const string HaulId = "reckoningHaul";

        public readonly List<UiNavGroup<TNode>> Groups = new List<UiNavGroup<TNode>>();
        public readonly List<UiNavLink<TNode>> Links = new List<UiNavLink<TNode>>();
        public readonly TNode Entry;

        private ReckoningBoardNav(TNode entry)
        {
            Entry = entry;
        }

        public static ReckoningBoardNav<TNode> For(IEnumerable<TNode> portraits, IEnumerable<TNode> haul,
            TNode continueNode)
        {
            var faces = portraits?.Where(p => p != null).ToList() ?? new List<TNode>();
            var chips = haul?.Where(h => h != null).ToList() ?? new List<TNode>();
            var board = new ReckoningBoardNav<TNode>(continueNode);

            if (faces.Count > 0) board.Groups.Add(new UiNavGroup<TNode>(PortraitsId, UiNavGroupKind.Rail, faces));
            if (chips.Count > 0)
                board.Groups.Add(new UiNavGroup<TNode>(HaulId, UiNavGroupKind.Rail, chips, wrap: UiNavWrap.Clamp));

            TNode firstFace = faces.Count > 0 ? faces[0] : null;
            TNode firstChip = chips.Count > 0 ? chips[0] : null;

            foreach (var chip in chips)
            {
                if (firstFace != null) board.Add(chip, UiNavDirection.Up, firstFace);
                board.Add(chip, UiNavDirection.Down, continueNode);
            }

            foreach (var face in faces) board.Add(face, UiNavDirection.Down, firstChip ?? continueNode);

            board.Add(continueNode, UiNavDirection.Up, firstChip ?? firstFace);
            return board;
        }

        private void Add(TNode from, UiNavDirection direction, TNode to)
        {
            if (from == null || to == null) return;
            Links.Add(new UiNavLink<TNode>(from, direction, to));
        }
    }
}
