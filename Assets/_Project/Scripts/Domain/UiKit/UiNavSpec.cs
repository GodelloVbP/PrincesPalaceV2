using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md section 5: three group shapes, not one
    // screen-wide setting. A screen with a Rail of ribbon nodes AND a List of
    // action rows declares one of each -- wrap/clamp is a property of the
    // GROUP, never of the screen (section 12.3's owner call).
    public enum UiNavGroupKind
    {
        List,   // vertical, Up/Down steps through Members in order
        Rail,   // horizontal, Left/Right steps through Members in order
        Grid,   // GridRowLength columns; Left/Right wrap within a ROW only,
                // Up/Down step a whole row -- never diagonally
    }

    public enum UiNavWrap
    {
        Wrap,
        Clamp,
    }

    public enum UiNavDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    // GENERIC OVER WHAT A NODE IS, and that is the whole of this file's
    // unification (plan section 9, the phase-2 revision). The shape of a
    // navigable surface -- "these things in this order, wrapping this way,
    // with this jump out of the group" -- has nothing to do with whether the
    // things are Domain UiNodes or live UnityEngine.UI.Selectables. Before
    // this, Core carried its OWN copy of the prev/next math (RuntimeNav
    // Wiring.Chain) purely because these types named UiNode, which put the
    // one rule in two places -- exactly what docs/CODE_STANDARDS.md section
    // 10 forbids. TNode is never inspected here, only ordered and handed
    // back, so Domain stays engine-free (section 1) while Core is free to
    // instantiate these with Selectable.
    //
    // One row/rail/grid of navigable nodes. Member order IS navigation order
    // -- a List's first member is above its second, a Rail's first is left of
    // its second, a Grid's Nth member sits at row N/GridRowLength, column
    // N%GridRowLength.
    public sealed class UiNavGroup<TNode> where TNode : class
    {
        public readonly string Id;
        public readonly UiNavGroupKind Kind;
        public readonly int GridRowLength;
        public readonly IReadOnlyList<TNode> Members;
        private readonly UiNavWrap? _wrapOverride;

        public UiNavGroup(string id, UiNavGroupKind kind, IEnumerable<TNode> members,
            int gridRowLength = 1, UiNavWrap? wrap = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("a nav group needs an id - it is how a failure names which group failed");
            }

            Id = id;
            Kind = kind;
            Members = (members ?? throw new ArgumentNullException(nameof(members))).ToList();
            if (Members.Count == 0)
            {
                throw new ArgumentException($"nav group '{id}' declares no members - remove it or give it some");
            }

            GridRowLength = kind == UiNavGroupKind.Grid ? Math.Max(1, gridRowLength) : 1;
            _wrapOverride = wrap;
        }

        // Owner default, plan section 12.3: wrap for Rail and Grid rows,
        // clamp for List. `wrap:` above overrides it per group when a screen
        // genuinely needs the other behaviour -- nothing here forces it.
        public UiNavWrap EffectiveWrap => _wrapOverride ?? (Kind == UiNavGroupKind.List ? UiNavWrap.Clamp : UiNavWrap.Wrap);
    }

    // An explicit link from one node to another in a stated direction --
    // Map's Graph shape (no group at all, every link is one of these) and any
    // inter-group jump a group's own internal order cannot express (a Rail's
    // node Down to a footer button, a tab's Down into its pane).
    //
    // Applied AFTER every group's own links, so a link here can override or
    // extend what a group computed for the same node.
    public readonly struct UiNavLink<TNode> where TNode : class
    {
        public readonly TNode From;
        public readonly UiNavDirection Direction;
        public readonly TNode To;

        public UiNavLink(TNode from, UiNavDirection direction, TNode to)
        {
            From = from ?? throw new ArgumentNullException(nameof(from));
            To = to ?? throw new ArgumentNullException(nameof(to));
            Direction = direction;
        }
    }

    // One navigable surface's whole declaration: the groups a Move routes
    // through, plus any link a group's own shape cannot express. Read by
    // UiNavLinkBuilder (this file's sibling), which is the only thing that
    // turns it into per-node neighbours.
    //
    // NO ENTRY AND NO REQUIRED-ACTION LIST any more, and that is deliberate
    // rather than an oversight. Both were declared for a build-time consumer
    // (ScreenDef.Nav, UiAudit.CheckNavigable) that never gained one -- see
    // RuntimeNavWiring's own header for the evidence that killed it. Entry
    // has a live owner elsewhere: NavContext.Entry, which is what actually
    // gets selected on push. Two places declaring "the entry" would be the
    // same duplication this file just finished removing.
    public sealed class UiNavDeclaration<TNode> where TNode : class
    {
        public readonly IReadOnlyList<UiNavGroup<TNode>> Groups;
        public readonly IReadOnlyList<UiNavLink<TNode>> Links;

        public UiNavDeclaration(IEnumerable<UiNavGroup<TNode>> groups = null,
            IEnumerable<UiNavLink<TNode>> links = null)
        {
            Groups = groups?.Where(g => g != null).ToList() ?? new List<UiNavGroup<TNode>>();
            Links = links?.ToList() ?? new List<UiNavLink<TNode>>();
        }
    }
}
