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

    // One row/rail/grid of navigable nodes within a screen's declared
    // UiNavDeclaration. Member order IS navigation order -- a List's first
    // member is above its second, a Rail's first is left of its second, a
    // Grid's Nth member sits at row N/GridRowLength, column N%GridRowLength.
    public sealed class UiNavGroup
    {
        public readonly string Id;
        public readonly UiNavGroupKind Kind;
        public readonly int GridRowLength;
        public readonly IReadOnlyList<UiNode> Members;
        private readonly UiNavWrap? _wrapOverride;

        public UiNavGroup(string id, UiNavGroupKind kind, IEnumerable<UiNode> members,
            int gridRowLength = 1, UiNavWrap? wrap = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("a nav group needs an id - it is how UiAudit names which group failed");
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
    // Map's Graph shape (no group at all, every link is one of these,
    // rewritten at MapController.Refresh()-time per plan section 4) and any
    // inter-group jump a group's own internal order cannot express (a Rail's
    // last node Down to a footer button, a tab's Down into its pane).
    //
    // Applied AFTER every group's own links, so a link here can override or
    // extend what a group computed for the same node.
    public readonly struct UiNavLink
    {
        public readonly UiNode From;
        public readonly UiNavDirection Direction;
        public readonly UiNode To;

        public UiNavLink(UiNode from, UiNavDirection direction, UiNode to)
        {
            From = from ?? throw new ArgumentNullException(nameof(from));
            To = to ?? throw new ArgumentNullException(nameof(to));
            Direction = direction;
        }
    }

    // A node that must stay reachable by Submit while State holds. Null/empty
    // State means "always" -- UiAudit.CheckNavigable requires every declared
    // required action's node to actually resolve in the tree, which is the
    // structural half of plan section 9's split; only a PlayMode test proves
    // stick+Submit actually reaches it in that state (section 9b, phase 3).
    public readonly struct UiRequiredAction
    {
        public readonly UiNode Node;
        public readonly string State;

        public UiRequiredAction(UiNode node, string state = null)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            State = state;
        }
    }

    // One screen's whole navigation declaration: the node its NavContext
    // selects on push (Entry), the groups the module's Move/Submit route
    // through, any link a group's own shape can't express, and the actions
    // that must stay reachable. Declared beside a screen's tree
    // (Domain/UiKit/Screens/), read by UiNavLinkBuilder (this file's sibling)
    // at scene-build time and by UiAudit.CheckNavigable to prove it is
    // complete BEFORE a scene is ever built.
    //
    // Screens not yet declaring one of these are simply not checked -- see
    // UiAudit.CheckNavigable's own comment for why that opt-in is scoped to
    // phase 2 and not a permanent exemption.
    public sealed class UiNavDeclaration
    {
        public readonly UiNode Entry;
        public readonly IReadOnlyList<UiNavGroup> Groups;
        public readonly IReadOnlyList<UiNavLink> Links;
        public readonly IReadOnlyList<UiRequiredAction> RequiredActions;

        public UiNavDeclaration(UiNode entry, IEnumerable<UiNavGroup> groups = null,
            IEnumerable<UiNavLink> links = null, IEnumerable<UiRequiredAction> requiredActions = null)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Groups = groups?.ToList() ?? new List<UiNavGroup>();
            Links = links?.ToList() ?? new List<UiNavLink>();
            RequiredActions = requiredActions?.ToList() ?? new List<UiRequiredAction>();
        }
    }
}
