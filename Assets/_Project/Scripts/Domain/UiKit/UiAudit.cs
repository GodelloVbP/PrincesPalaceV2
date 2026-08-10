using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // Re-checks a solved screen for the mistakes this project has actually
    // shipped.
    //
    // It works purely on solved geometry, which is why it needs no idea whether
    // a node was placed by flow, by anchor, or by a runtime layout function --
    // and why it subsumes the three separate one-off guards v1 grew
    // (CreateButtonStrip's width check, AssertColumnClears' downward-only
    // clearance, and the Talents screen's local upward re-implementation of the
    // same idea, which existed precisely BECAUSE the shared guard modelled only
    // one direction). Pairwise overlap has no direction, so the whole "reserved
    // band" concept disappears: every sibling is simply in the tree.
    //
    // Cheap enough to run on every screen at every canvas frame on every build.
    public static class UiAudit
    {
        public static IReadOnlyList<UiAuditError> Run(SolvedNode root, UiVec frame)
        {
            var errors = new List<UiAuditError>();
            Walk(root, null, isDecor: false, errors, frame);
            return errors;
        }

        // Solves and audits the same declaration at every audit frame. Pinned
        // and stretched nodes move relative to centred content as aspect
        // changes, so a screen clean at 16:9 can collide at 21:9.
        public static IReadOnlyList<UiAuditError> RunAllFrames(UiNode tree)
        {
            var errors = new List<UiAuditError>();
            foreach (var frame in UiFrames.All)
            {
                errors.AddRange(Run(UiSolver.Solve(tree, frame), frame));
            }
            return errors;
        }

        private static void Walk(SolvedNode node, SolvedNode parent, bool isDecor,
                                 List<UiAuditError> errors, UiVec frame)
        {
            bool decorHere = isDecor || (node.Source != null && node.Source.Decor);

            CheckZeroSize(node, errors, frame);
            if (parent != null) CheckContainment(node, parent, errors, frame);

            CheckDuplicateNames(node, errors, frame);
            CheckFlowCapacity(node, errors, frame);
            CheckSiblingOverlap(node, decorHere, errors, frame);

            foreach (var child in node.Children)
            {
                Walk(child, node, decorHere, errors, frame);
            }
        }

        // A6 -----------------------------------------------------------------
        private static void CheckZeroSize(SolvedNode node, List<UiAuditError> errors, UiVec frame)
        {
            if (node.Source == null || !node.Source.IsVisibleGraphic) return;
            if (node.Rect.Width > 0.01f && node.Rect.Height > 0.01f) return;

            errors.Add(new UiAuditError
            {
                Check = UiAuditCheck.ZeroSizeGraphic,
                Path = node.Path,
                Frame = frame,
                Message =
                    $"'{node.Path}' is a {node.Kind} solved to {node.Rect.Width:0.#}x{node.Rect.Height:0.#} - it would " +
                    $"render as nothing, or as a solid white quad if it has no sprite. " +
                    $"Fix by: giving it a Fixed size; or letting it Fill a parent that has one; or removing the node " +
                    $"if it is genuinely not meant to be seen.",
            });
        }

        // A2 -----------------------------------------------------------------
        private static void CheckContainment(SolvedNode node, SolvedNode parent,
                                             List<UiAuditError> errors, UiVec frame)
        {
            if (node.Source?.AllowOverflowReason != null) return;

            // A pool's members are positioned at runtime from a tested layout
            // function; their build-time rects are placeholders and mean nothing.
            if (parent.Kind == UiNodeKind.Pool) return;

            var inner = node.Footprint;
            if (parent.Rect.Contains(inner)) return;

            var over = parent.Rect.Overflow(inner);
            var sides = new List<string>();
            if (over.Left > 0.01f) sides.Add($"{over.Left:0.#}px past the left");
            if (over.Right > 0.01f) sides.Add($"{over.Right:0.#}px past the right");
            if (over.Bottom > 0.01f) sides.Add($"{over.Bottom:0.#}px past the bottom");
            if (over.Top > 0.01f) sides.Add($"{over.Top:0.#}px past the top");

            errors.Add(new UiAuditError
            {
                Check = UiAuditCheck.ChildContainment,
                Path = node.Path,
                OtherPath = parent.Path,
                Frame = frame,
                Message =
                    $"'{node.Path}' escapes its parent '{parent.Path}': {string.Join(", ", sides)}. " +
                    $"Fix by: shrinking the child; or growing '{parent.Path}'; or, if the overhang is the point " +
                    $"(a glow bleeding past its card), declaring AllowOverflow(\"reason\").",
            });
        }

        // A4 -----------------------------------------------------------------
        private static void CheckDuplicateNames(SolvedNode node, List<UiAuditError> errors, UiVec frame)
        {
            if (node.Children.Count < 2) return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in node.Children)
            {
                if (seen.Add(child.Name)) continue;

                errors.Add(new UiAuditError
                {
                    Check = UiAuditCheck.DuplicateName,
                    Path = child.Path,
                    Frame = frame,
                    Message =
                        $"'{node.Path}' has two children named '{child.Name}'. Names are how tests, the wiring pass " +
                        $"and the screenshot tool find nodes, so a duplicate silently resolves to whichever comes " +
                        $"first. Fix by: suffixing with the index, as Ui.Each does automatically.",
                });
            }
        }

        // A3 -----------------------------------------------------------------
        private static void CheckFlowCapacity(SolvedNode node, List<UiAuditError> errors, UiVec frame)
        {
            var source = node.Source;
            if (source == null || !source.IsFlowContainer) return;

            // A FromChildren container grew to fit by definition; only a Fixed
            // one can be too small for what it was asked to hold.
            bool vertical = source.Kind == UiNodeKind.Column;
            var mode = vertical ? source.Size.ModeY : source.Size.ModeX;
            if (mode != UiSizeMode.Fixed) return;

            int count = 0;
            float required = 0f;
            foreach (var child in node.Children)
            {
                required += vertical ? child.Rect.Height : child.Rect.Width;
                count++;
            }
            if (count == 0) return;

            required += source.Spacing * (count - 1);
            required += vertical ? source.Pad.Vertical : source.Pad.Horizontal;

            float available = vertical ? node.Rect.Height : node.Rect.Width;
            if (required <= available + 0.01f) return;

            errors.Add(new UiAuditError
            {
                Check = UiAuditCheck.FlowCapacity,
                Path = node.Path,
                Frame = frame,
                Message =
                    $"'{node.Path}' needs {required:0.#}px for {count} children at spacing {source.Spacing:0.#}, " +
                    $"but is only {available:0.#}px. " +
                    $"Fix by: growing the container; or reducing spacing; or sizing it FromChildren so it grows with " +
                    $"its content instead of silently overflowing.",
            });
        }

        // A1 -----------------------------------------------------------------
        private static void CheckSiblingOverlap(SolvedNode node, bool isDecor,
                                                List<UiAuditError> errors, UiVec frame)
        {
            if (node.Children.Count < 2) return;

            // Decoration cannot take a click (the emitter clears raycastTarget
            // across the subtree), and ambient art overlaps constantly by
            // design - 110 glows on one screen in v1. Auditing it would produce
            // thousands of errors that all mean "yes, that is the effect".
            if (isDecor) return;

            // Pool members are runtime-positioned; their build-time rects are
            // placeholders that all sit in the same box.
            if (node.Kind == UiNodeKind.Pool) return;

            for (int i = 0; i < node.Children.Count; i++)
            {
                for (int j = i + 1; j < node.Children.Count; j++)
                {
                    var a = node.Children[i];
                    var b = node.Children[j];

                    if (a.Source?.Decor == true || b.Source?.Decor == true) continue;
                    if (a.Source?.AllowOverlapReason != null || b.Source?.AllowOverlapReason != null) continue;

                    var ra = a.Footprint;
                    var rb = b.Footprint;
                    if (!ra.Overlaps(rb)) continue;

                    var extent = ra.OverlapExtent(rb);
                    errors.Add(new UiAuditError
                    {
                        Check = UiAuditCheck.SiblingOverlap,
                        Path = a.Path,
                        OtherPath = b.Path,
                        Frame = frame,
                        Message =
                            $"'{a.Path}' and '{b.Path}' overlap by {extent.X:0.#}x{extent.Y:0.#}px. uGUI draws the " +
                            $"LATER sibling on top, so '{b.Path}' takes the clicks meant for '{a.Path}' - which is " +
                            $"invisible until someone reports that a button does nothing. " +
                            $"Fix by: making them flow siblings so spacing is derived, not hardcoded; or moving one; " +
                            $"or, if they are meant to sit on top of each other, declaring AllowOverlap(\"reason\").",
                    });
                }
            }
        }
    }
}
