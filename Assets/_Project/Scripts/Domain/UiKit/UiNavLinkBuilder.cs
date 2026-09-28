using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // THE ONE PLACE a group declaration becomes per-node neighbours. Pure C#,
    // no scene, no Selectable -- generic over what a node is (UiNavSpec's own
    // header explains why), so the same algorithm serves a Domain UiNode tree
    // and Core's live UnityEngine.UI.Selectables without either the algorithm
    // or Domain's engine-free boundary bending (docs/CODE_STANDARDS.md
    // "Layering" and "Build the model"). RuntimeNavWiring is a thin adapter over this, not a
    // second implementation of it.
    public static class UiNavLinkBuilder
    {
        // One node's resolved neighbours. Any of the four may be null -- no
        // link that direction, which Navigation.Explicit leaves unset.
        public sealed class NavLinkTargets<TNode> where TNode : class
        {
            public TNode Up;
            public TNode Down;
            public TNode Left;
            public TNode Right;
        }

        public static IReadOnlyDictionary<TNode, NavLinkTargets<TNode>> Build<TNode>(UiNavDeclaration<TNode> nav)
            where TNode : class
        {
            var result = new Dictionary<TNode, NavLinkTargets<TNode>>();
            if (nav == null) return result;

            foreach (var group in nav.Groups)
            {
                BuildGroup(group, result);
            }

            // Explicit links are laid down AFTER every group's own defaults,
            // so a surface can override or extend what its groups computed --
            // Map's Graph shape has no groups at all and is entirely this
            // loop.
            foreach (var link in nav.Links)
            {
                Set(Get(result, link.From), link.Direction, link.To);
            }

            return result;
        }

        private static NavLinkTargets<TNode> Get<TNode>(Dictionary<TNode, NavLinkTargets<TNode>> result, TNode node)
            where TNode : class
        {
            if (!result.TryGetValue(node, out var links))
            {
                links = new NavLinkTargets<TNode>();
                result[node] = links;
            }
            return links;
        }

        private static void Set<TNode>(NavLinkTargets<TNode> links, UiNavDirection dir, TNode to)
            where TNode : class
        {
            switch (dir)
            {
                case UiNavDirection.Up: links.Up = to; break;
                case UiNavDirection.Down: links.Down = to; break;
                case UiNavDirection.Left: links.Left = to; break;
                case UiNavDirection.Right: links.Right = to; break;
            }
        }

        private static void BuildGroup<TNode>(UiNavGroup<TNode> group, Dictionary<TNode, NavLinkTargets<TNode>> result)
            where TNode : class
        {
            var members = group.Members;
            int count = members.Count;

            // A single-member group has no neighbour to link to itself --
            // without this guard, Wrap's modulo math links a lone node to
            // itself in every direction, which is not "no link", it is a
            // link nowhere that looks like one until somebody presses it.
            if (count < 2) return;

            bool wrap = group.EffectiveWrap == UiNavWrap.Wrap;

            if (group.Kind == UiNavGroupKind.Grid)
            {
                BuildGrid(group, result, wrap);
                return;
            }

            bool horizontal = group.Kind == UiNavGroupKind.Rail;

            for (int i = 0; i < count; i++)
            {
                var links = Get(result, members[i]);

                TNode prev = null;
                TNode next = null;
                if (wrap)
                {
                    prev = members[(i - 1 + count) % count];
                    next = members[(i + 1) % count];
                }
                else
                {
                    if (i > 0) prev = members[i - 1];
                    if (i < count - 1) next = members[i + 1];
                }

                if (horizontal)
                {
                    links.Left = prev;
                    links.Right = next;
                }
                else
                {
                    links.Up = prev;
                    links.Down = next;
                }
            }
        }

        private static void BuildGrid<TNode>(UiNavGroup<TNode> group,
            Dictionary<TNode, NavLinkTargets<TNode>> result, bool wrapRows)
            where TNode : class
        {
            var members = group.Members;
            int count = members.Count;
            int cols = group.GridRowLength;

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int rowStart = row * cols;
                int rowLen = System.Math.Min(cols, count - rowStart);

                var links = Get(result, members[i]);

                // Left/Right NEVER cross into a neighbouring row, wrap or
                // not -- that would step diagonally, which is not what a
                // Grid's two axes mean. Owner default (section 12.3): wrap
                // within a row, and a SHORT last row wraps within its own
                // length (rowLen), not the full column count.
                if (rowLen > 1)
                {
                    if (wrapRows)
                    {
                        links.Left = members[rowStart + (col - 1 + rowLen) % rowLen];
                        links.Right = members[rowStart + (col + 1) % rowLen];
                    }
                    else
                    {
                        if (col > 0) links.Left = members[rowStart + col - 1];
                        if (col < rowLen - 1) links.Right = members[rowStart + col + 1];
                    }
                }

                int upIndex = i - cols;
                int downIndex = i + cols;
                links.Up = upIndex >= 0 ? members[upIndex] : null;
                links.Down = downIndex < count ? members[downIndex] : null;
            }
        }
    }
}
