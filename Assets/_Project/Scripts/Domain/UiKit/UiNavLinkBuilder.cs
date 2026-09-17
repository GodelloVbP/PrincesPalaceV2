using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Turns a UiNavDeclaration into the four-directional neighbour every
    // declared node resolves to. Pure C#, no scene, no Selectable -- the seam
    // both UiNavWiring's Navigation.Explicit pass (Editor, real scene) and
    // UiAudit.CheckNavigable's build-fidelity diff (a -BuildScenes test) read
    // from, so the ALGORITHM is testable against a fake tree with nothing
    // Unity involved (plan section 9a).
    public static class UiNavLinkBuilder
    {
        // One node's resolved neighbours. Any of the four may be null -- no
        // link that direction, which Navigation.Explicit leaves unset.
        public sealed class NavLinkTargets
        {
            public UiNode Up;
            public UiNode Down;
            public UiNode Left;
            public UiNode Right;
        }

        public static IReadOnlyDictionary<UiNode, NavLinkTargets> Build(UiNavDeclaration nav)
        {
            var result = new Dictionary<UiNode, NavLinkTargets>();
            if (nav == null) return result;

            foreach (var group in nav.Groups)
            {
                BuildGroup(group, result);
            }

            // Explicit links are laid down AFTER every group's own defaults,
            // so a screen can override or extend what its groups computed --
            // Map's Graph shape has no groups at all and is entirely this
            // loop, rewritten by MapController.Refresh() at runtime.
            foreach (var link in nav.Links)
            {
                Get(result, link.From).Set(link.Direction, link.To);
            }

            return result;
        }

        private static NavLinkTargets Get(Dictionary<UiNode, NavLinkTargets> result, UiNode node)
        {
            if (!result.TryGetValue(node, out var links))
            {
                links = new NavLinkTargets();
                result[node] = links;
            }
            return links;
        }

        private static void Set(this NavLinkTargets links, UiNavDirection dir, UiNode to)
        {
            switch (dir)
            {
                case UiNavDirection.Up: links.Up = to; break;
                case UiNavDirection.Down: links.Down = to; break;
                case UiNavDirection.Left: links.Left = to; break;
                case UiNavDirection.Right: links.Right = to; break;
            }
        }

        private static void BuildGroup(UiNavGroup group, Dictionary<UiNode, NavLinkTargets> result)
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

                UiNode prev = null;
                UiNode next = null;
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

        private static void BuildGrid(UiNavGroup group, Dictionary<UiNode, NavLinkTargets> result, bool wrapRows)
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
                // within a row.
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
