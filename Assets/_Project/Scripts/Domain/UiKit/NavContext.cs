using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // One entry on the NavContextStack -- a screen, a modal, or Fight's own
    // membership marker. Engine-free by construction (docs/CODE_STANDARDS.md
    // section 1): a Button or a GameObject is an opaque `object` handle here,
    // supplied and interpreted by the caller (NavigationInputModule, in
    // Core), never inspected by this class.
    //
    // Two shapes, not one class trying to be both: an ordinary context owns
    // a Selectable set and a Cancel action; a Fight context owns neither --
    // it has no GameObject to select, ever (its membership in the stack IS
    // the dispatcher's branch condition, plan section 4) -- and instead
    // carries the target the dispatcher's Fight branch calls. ForFight is
    // the only way to build the second shape, so the two can never be
    // confused at a call site.
    public sealed class NavContext
    {
        private static readonly IReadOnlyDictionary<string, object> EmptySelectables =
            new Dictionary<string, object>();

        public object Entry { get; private set; }
        public IReadOnlyDictionary<string, object> Selectables { get; private set; }
        public Action Cancel { get; }
        public bool IsNonSelecting { get; }
        public IFightNavigationTarget FightTarget { get; }

        // By STABLE STRING ID, not a reference (plan section 4) -- a node
        // can be destroyed and rebuilt (a repaint) while the id it was
        // declared under stays the same, so remembering the id survives a
        // rebuild remembering a reference could not.
        public string RememberedId { get; private set; }

        // The active pane's optional first refusal on a Cancel press
        // (INavCancelClaim). A FUNCTION, not a stored claimant, because
        // WHICH pane is active changes while this one context stays pushed
        // -- the menu's tab strip swaps panes underneath it -- so anything
        // captured once would be answering for the wrong pane by the time a
        // press arrived.
        private readonly Func<INavCancelClaim> _claimant;

        public NavContext(object entry, IReadOnlyDictionary<string, object> selectables, Action cancel,
            Func<INavCancelClaim> claimant = null)
        {
            Entry = entry;
            Selectables = selectables ?? EmptySelectables;
            Cancel = cancel;
            _claimant = claimant;
        }

        private NavContext(IFightNavigationTarget fightTarget)
        {
            FightTarget = fightTarget;
            IsNonSelecting = true;
            Selectables = EmptySelectables;
        }

        public static NavContext ForFight(IFightNavigationTarget target) => new NavContext(target);

        public void Remember(string stableId) => RememberedId = stableId;

        // CANCEL RESOLUTION, IN ONE PLACE -- the dispatcher calls this, never
        // Cancel directly, so the precedence is stated once here rather than
        // at every caller: the active pane is offered the press first, and
        // only a pane that declines (or is absent) lets the context's own
        // Cancel run. See INavCancelClaim for why a pane answers per press
        // instead of setting a flag.
        public void RaiseCancel()
        {
            var claimant = _claimant?.Invoke();
            if (claimant != null && claimant.ClaimCancel()) return;

            Cancel?.Invoke();
        }

        // Map's own case (plan section 4): a context whose navigable set
        // genuinely changes contents across repaints -- a new floor has
        // different reachable rooms -- rather than a fixed declared set some
        // of whose members are hidden. Mutates IN PLACE rather than being
        // popped and re-pushed, which would either lose this context's place
        // on the stack (if something sits above it) or, worse, put it back
        // ON TOP of whatever now sits above it. RememberedId is left alone on
        // purpose: a repaint that still contains the same id should not
        // forget what was focused, the ordinary case a runtime repaint is
        // built for.
        public void Reconfigure(object entry, IReadOnlyDictionary<string, object> selectables)
        {
            Entry = entry;
            Selectables = selectables ?? EmptySelectables;
        }

        // Remembered node if one is set and still declared, else the entry --
        // one rule for the reselection cases section 4 lists (a background
        // click, a stray SetSelectedGameObject, a context just pushed with
        // nothing remembered yet). Null is a legal answer: an ordinary
        // context whose whole group emptied has nothing to reselect either
        // (section 6's screen-level-fallback case) -- the caller decides
        // what a null resolution means for its own selection call, this
        // class only ever states what is declared.
        public object ResolveSelection()
        {
            if (RememberedId != null && Selectables.TryGetValue(RememberedId, out var remembered)) return remembered;
            return Entry;
        }

        public bool ContainsSelectable(object handle)
        {
            if (handle == null) return false;

            foreach (var value in Selectables.Values)
            {
                if (Equals(value, handle)) return true;
            }

            return false;
        }
    }
}
