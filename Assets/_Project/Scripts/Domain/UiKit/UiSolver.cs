using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    public sealed class UiSolveException : Exception
    {
        public UiSolveException(string message) : base(message) { }
    }

    // Turns a declared tree into absolute canvas rects.
    //
    // TWO PHASE, and it has to be. UiSizeMode.FromChildren flows bottom-up (a
    // container cannot know its size until its children are measured) while
    // UiSizeMode.Fill flows top-down (a child cannot know its size until its
    // parent has one). A single pass can serve one direction or the other, never
    // both, and the combination that is genuinely circular -- Fill inside
    // FromChildren on the same axis -- is refused rather than guessed at.
    //
    // Pure arithmetic over plain structs: no Unity types, no allocation beyond
    // the result tree, and fast enough that auditing every screen at four canvas
    // aspects on every build is free.
    public static class UiSolver
    {
        public static SolvedNode Solve(UiNode root, UiVec canvasSize)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var measured = new Dictionary<UiNode, UiVec>();
            Measure(root, null, measured, root.Name);

            var canvasRect = new UiRect(UiVec.Zero, canvasSize);
            var rootRect = RectFor(root, canvasRect, measured[root]);
            return Arrange(root, rootRect, root.Name, measured);
        }

        // ---- phase 1: measure, bottom-up -----------------------------------

        private static UiVec Measure(UiNode node, UiNode parent, Dictionary<UiNode, UiVec> cache, string path)
        {
            foreach (var child in node.Children)
            {
                Measure(child, node, cache, path + "/" + child.Name);
            }

            // A5. Checked here rather than in the audit because it is not a
            // layout mistake to report -- it is a definition with no fixed
            // point, and there is no rect to hand back for it.
            // Only circular when the parent's size ACTUALLY depends on its
            // children. A Grid sizes itself from its cell spec and column count,
            // and a Pool is always placement-sized, so a Fill child inside
            // either resolves fine -- flagging those would ban a legitimate and
            // obvious pattern (a cell-filling icon) for no reason.
            if (parent != null && SizeDependsOnChildren(parent))
            {
                if (node.Size.ModeX == UiSizeMode.Fill && parent.Size.ModeX == UiSizeMode.FromChildren)
                {
                    throw new UiSolveException(CircularMessage(path, parent.Name, "width"));
                }
                if (node.Size.ModeY == UiSizeMode.Fill && parent.Size.ModeY == UiSizeMode.FromChildren)
                {
                    throw new UiSolveException(CircularMessage(path, parent.Name, "height"));
                }
            }

            var content = ContentSize(node, cache);

            float x = node.Size.ModeX switch
            {
                UiSizeMode.Fixed => node.Size.X,
                UiSizeMode.FromChildren => content.X + node.Pad.Horizontal + node.Size.X,
                _ => 0f, // Fill: resolved by the parent during arrange.
            };
            float y = node.Size.ModeY switch
            {
                UiSizeMode.Fixed => node.Size.Y,
                UiSizeMode.FromChildren => content.Y + node.Pad.Vertical + node.Size.Y,
                _ => 0f,
            };

            var size = new UiVec(x, y);
            cache[node] = size;
            return size;
        }

        private static bool SizeDependsOnChildren(UiNode parent) =>
            parent.Kind != UiNodeKind.Grid && parent.Kind != UiNodeKind.Pool;

        private static string CircularMessage(string path, string parentName, string axis) =>
            $"UiKit: '{path}' asks to Fill its {axis} while its parent '{parentName}' sizes that same {axis} " +
            $"FromChildren. That is circular - the parent would need the child's {axis} to compute its own, " +
            $"and the child needs the parent's. Fix by: giving the parent a Fixed {axis}; or giving this child a " +
            $"Fixed {axis}; or moving the Fill child into a sibling container that has one.";

        // The extent a container's children occupy, before its own padding.
        //
        // FLOW CHILDREN ONLY, on a flow container's own axis. ArrangeFlow
        // lays out and spaces exactly the children whose Place.IsFlow is true
        // -- an absolutely-placed child inside a Column or Row is legal and
        // "does not consume flow space", in that method's own words. Measuring
        // every child instead made a FromChildren container as tall as its
        // stack PLUS whatever was pinned inside it, plus one spacing gap for
        // the pin: a 210px stack behind a 500px glow measured 720. The cross
        // axis still takes every child, because that is where a pinned child
        // genuinely does sit inside the container.
        private static UiVec ContentSize(UiNode node, Dictionary<UiNode, UiVec> cache)
        {
            if (node.Children.Count == 0) return UiVec.Zero;

            switch (node.Kind)
            {
                case UiNodeKind.Column:
                {
                    float w = 0f, h = 0f;
                    int counted = 0;
                    foreach (var c in node.Children)
                    {
                        var s = cache[c];
                        w = Math.Max(w, s.X);
                        if (!c.Place.IsFlow) continue;
                        h += c.Kind == UiNodeKind.Space ? c.Size.Y : s.Y;
                        counted++;
                    }
                    return new UiVec(w, h + node.Spacing * Math.Max(0, counted - 1));
                }
                case UiNodeKind.Row:
                {
                    float w = 0f, h = 0f;
                    int counted = 0;
                    foreach (var c in node.Children)
                    {
                        var s = cache[c];
                        h = Math.Max(h, s.Y);
                        if (!c.Place.IsFlow) continue;
                        w += c.Kind == UiNodeKind.Space ? c.Size.X : s.X;
                        counted++;
                    }
                    return new UiVec(w + node.Spacing * Math.Max(0, counted - 1), h);
                }
                case UiNodeKind.Grid:
                {
                    int columns = Math.Max(1, node.Columns);
                    int rows = (int)Math.Ceiling(node.Children.Count / (double)columns);
                    return new UiVec(
                        columns * node.Cell.X + (columns - 1) * node.Gap.X,
                        rows * node.Cell.Y + (rows - 1) * node.Gap.Y);
                }
                default:
                {
                    // Absolutely-placed children: the content extent is the
                    // union of their own boxes around the origin.
                    float w = 0f, h = 0f;
                    foreach (var c in node.Children)
                    {
                        var s = cache[c];
                        w = Math.Max(w, s.X);
                        h = Math.Max(h, s.Y);
                    }
                    return new UiVec(w, h);
                }
            }
        }

        // ---- phase 2: arrange, top-down ------------------------------------

        private static SolvedNode Arrange(UiNode node, UiRect rect, string path, Dictionary<UiNode, UiVec> cache)
        {
            var solved = new SolvedNode
            {
                Name = node.Name,
                Path = path,
                Kind = node.Kind,
                Rect = rect,
                Rotation = node.Rotation,
                Scale = node.Scale,
                Source = node,
            };

            if (node.Children.Count == 0) return solved;

            var content = Deflate(rect, node.Pad);

            switch (node.Kind)
            {
                case UiNodeKind.Column:
                    ArrangeFlow(node, solved, content, path, cache, vertical: true);
                    break;
                case UiNodeKind.Row:
                    ArrangeFlow(node, solved, content, path, cache, vertical: false);
                    break;
                case UiNodeKind.Grid:
                    ArrangeGrid(node, solved, content, path, cache);
                    break;
                default:
                    foreach (var child in node.Children)
                    {
                        if (child.Place.IsFlow && node.Kind != UiNodeKind.Pool)
                        {
                            throw new UiSolveException(
                                $"UiKit: '{path}/{child.Name}' uses Place.Flow, but its parent '{node.Name}' is a " +
                                $"{node.Kind}, which does not lay children out in a flow. Fix by: giving this child an " +
                                $"explicit Place (At/Pin/Stretch/Frac); or wrapping these children in a Ui.Column or " +
                                $"Ui.Row inside '{node.Name}'.");
                        }

                        // Pool members are positioned at runtime from a tested
                        // layout function, so at build time they all sit in the
                        // content box; the audit exempts them for that reason.
                        var childRect = child.Place.IsFlow ? content : RectFor(child, content, cache[child]);
                        solved.Children.Add(Arrange(child, childRect, path + "/" + child.Name, cache));
                    }
                    break;
            }

            return solved;
        }

        private static void ArrangeFlow(UiNode node, SolvedNode solved, UiRect content, string path,
                                        Dictionary<UiNode, UiVec> cache, bool vertical)
        {
            // Absolutely-placed children inside a flow container are legal and
            // do not consume flow space - a glow pinned behind a row, say.
            var flowChildren = new List<UiNode>();
            foreach (var c in node.Children)
            {
                if (c.Place.IsFlow) flowChildren.Add(c);
            }

            float fixedExtent = 0f;
            int fillCount = 0;
            foreach (var c in flowChildren)
            {
                var s = cache[c];
                bool fills = vertical ? c.Size.ModeY == UiSizeMode.Fill : c.Size.ModeX == UiSizeMode.Fill;
                if (fills) { fillCount++; continue; }
                fixedExtent += vertical ? s.Y : s.X;
            }

            float available = vertical ? content.Height : content.Width;
            float spacingTotal = node.Spacing * Math.Max(0, flowChildren.Count - 1);
            float leftover = available - fixedExtent - spacingTotal;
            float perFill = fillCount > 0 ? Math.Max(0f, leftover / fillCount) : 0f;

            // Columns run TOP-DOWN (first child highest) because +y is up and
            // reading order is not; rows run left-to-right.
            float cursor = vertical ? content.Top : content.Left;

            foreach (var child in node.Children)
            {
                if (!child.Place.IsFlow)
                {
                    var abs = RectFor(child, content, cache[child]);
                    solved.Children.Add(Arrange(child, abs, path + "/" + child.Name, cache));
                    continue;
                }

                var measured = cache[child];
                float mainSize;
                float crossSize;

                if (vertical)
                {
                    mainSize = child.Size.ModeY == UiSizeMode.Fill ? perFill : measured.Y;
                    crossSize = child.Size.ModeX == UiSizeMode.Fill ? content.Width : measured.X;
                }
                else
                {
                    mainSize = child.Size.ModeX == UiSizeMode.Fill ? perFill : measured.X;
                    crossSize = child.Size.ModeY == UiSizeMode.Fill ? content.Height : measured.Y;
                }

                if (child.Kind == UiNodeKind.Space)
                {
                    // Occupies main-axis extent and draws nothing.
                    cursor += vertical ? -(mainSize + node.Spacing) : (mainSize + node.Spacing);
                    continue;
                }

                float crossCentre = CrossCentre(node.Align, content, crossSize, vertical);
                UiRect childRect = vertical
                    ? new UiRect(new UiVec(crossCentre, cursor - mainSize / 2f), new UiVec(crossSize, mainSize))
                    : new UiRect(new UiVec(cursor + mainSize / 2f, crossCentre), new UiVec(mainSize, crossSize));

                solved.Children.Add(Arrange(child, childRect, path + "/" + child.Name, cache));
                cursor += vertical ? -(mainSize + node.Spacing) : (mainSize + node.Spacing);
            }
        }

        private static float CrossCentre(UiAlign align, UiRect content, float crossSize, bool vertical)
        {
            if (vertical)
            {
                switch (align)
                {
                    case UiAlign.Start: return content.Left + crossSize / 2f;
                    case UiAlign.End: return content.Right - crossSize / 2f;
                    default: return content.Centre.X;
                }
            }

            switch (align)
            {
                case UiAlign.Start: return content.Bottom + crossSize / 2f;
                case UiAlign.End: return content.Top - crossSize / 2f;
                default: return content.Centre.Y;
            }
        }

        private static void ArrangeGrid(UiNode node, SolvedNode solved, UiRect content, string path,
                                        Dictionary<UiNode, UiVec> cache)
        {
            int columns = Math.Max(1, node.Columns);
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                int col = i % columns;
                int row = i / columns;

                float x = content.Left + node.Cell.X / 2f + col * (node.Cell.X + node.Gap.X);
                float y = content.Top - node.Cell.Y / 2f - row * (node.Cell.Y + node.Gap.Y);

                var cellRect = new UiRect(new UiVec(x, y), node.Cell);
                var childRect = child.Place.IsFlow ? cellRect : RectFor(child, cellRect, cache[child]);
                solved.Children.Add(Arrange(child, childRect, path + "/" + child.Name, cache));
            }
        }

        // ---- placement ------------------------------------------------------

        private static UiRect Deflate(UiRect rect, UiPad pad)
        {
            float w = Math.Max(0f, rect.Width - pad.Horizontal);
            float h = Math.Max(0f, rect.Height - pad.Vertical);
            float cx = rect.Left + pad.Left + w / 2f;
            float cy = rect.Bottom + pad.Bottom + h / 2f;
            return new UiRect(new UiVec(cx, cy), new UiVec(w, h));
        }

        // The single place anchor/pivot/offset/inset semantics are interpreted.
        // The emitter mirrors this into RectTransform terms and nothing else in
        // the codebase gets to have an opinion about it.
        private static UiRect RectFor(UiNode node, UiRect parent, UiVec measured)
        {
            var place = node.Place;

            switch (place.Kind)
            {
                case PlaceKind.Stretch:
                {
                    float left = parent.Left + place.Left;
                    float right = parent.Right - place.Right;
                    float bottom = parent.Bottom + place.Bottom;
                    float top = parent.Top - place.Top;
                    return FromEdges(left, right, bottom, top);
                }

                case PlaceKind.Frac:
                {
                    float left = parent.Left + place.AnchorMin.X * parent.Width + place.Left;
                    float right = parent.Left + place.AnchorMax.X * parent.Width - place.Right;
                    float bottom = parent.Bottom + place.AnchorMin.Y * parent.Height + place.Bottom;
                    float top = parent.Bottom + place.AnchorMax.Y * parent.Height - place.Top;
                    return FromEdges(left, right, bottom, top);
                }

                case PlaceKind.Pin:
                case PlaceKind.At:
                {
                    var size = ResolveSize(node, parent, measured);
                    float ax = parent.Left + place.AnchorMin.X * parent.Width;
                    float ay = parent.Bottom + place.AnchorMin.Y * parent.Height;

                    // The offset positions the PIVOT, so the centre shifts by
                    // however far the pivot sits from it.
                    float cx = ax + place.Offset.X + (0.5f - place.Pivot.X) * size.X;
                    float cy = ay + place.Offset.Y + (0.5f - place.Pivot.Y) * size.Y;
                    return new UiRect(new UiVec(cx, cy), size);
                }

                default:
                    // Flow at the root, or a Pool member: fill the parent.
                    return new UiRect(parent.Centre, ResolveSize(node, parent, measured));
            }
        }

        private static UiVec ResolveSize(UiNode node, UiRect parent, UiVec measured)
        {
            float x = node.Size.ModeX == UiSizeMode.Fill ? parent.Width : measured.X;
            float y = node.Size.ModeY == UiSizeMode.Fill ? parent.Height : measured.Y;
            return new UiVec(x, y);
        }

        private static UiRect FromEdges(float left, float right, float bottom, float top)
        {
            return new UiRect(
                new UiVec((left + right) / 2f, (bottom + top) / 2f),
                new UiVec(Math.Max(0f, right - left), Math.Max(0f, top - bottom)));
        }
    }
}
