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

        // This context's own optional tab strip (INavTabStrip, plan phase 3
        // item 2) -- a FUNCTION for the same reason `_claimant` is one, even
        // though today's one implementor (SystemMenuController) never
        // changes identity while pushed: a context that grows a second
        // tab-bearing state later should not have to change this shape to
        // ask "which one is live right now" the way ActivePaneCancelClaim
        // already does for Cancel.
        private readonly Func<INavTabStrip> _tabStrip;

        public NavContext(object entry, IReadOnlyDictionary<string, object> selectables, Action cancel,
            Func<INavCancelClaim> claimant = null, Func<INavTabStrip> tabStrip = null)
        {
            Entry = entry;
            Selectables = selectables ?? EmptySelectables;
            Cancel = cancel;
            _claimant = claimant;
            _tabStrip = tabStrip;
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

        // THE SHOULDER SHORTCUT'S ONLY EFFECT, offered to whichever context
        // is top the same way Cancel is (NavigationInputModule's own
        // ordinary-context branch calls both off `topAtStart`). A no-op
        // when this context declares no tab strip -- Hub, RelicDraft, the
        // glossary and every other context built without a `tabStrip`
        // argument answer null here, so a shoulder press on any of them is
        // simply absorbed, not routed anywhere by accident.
        public void RaiseTabStep(int direction) => _tabStrip?.Invoke()?.StepTab(direction);

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

        // WHAT THIS CONTEXT REMEMBERS, if the id it remembered is still one
        // of the nodes it declares -- else null.
        //
        // This is deliberately only HALF of section 4's "remembered ?? entry"
        // rule. The other half needs to know whether the remembered node is
        // still usable (shown, not destroyed), which is an engine question
        // this class cannot ask and must not pretend to
        // (docs/CODE_STANDARDS.md section 1), so the whole rule is stated
        // once in NavigationInputModule.SelectionFor and never a second time
        // here. Null is a legal answer at both halves: a context whose whole
        // group emptied has nothing to reselect (section 6's screen-level
        // fallback).
        //
        // The id, not a reference, is what survives: a repaint can destroy
        // and rebuild the node declared under a given id (Map's rooms, the
        // debug menu's rows) and the id still names the right control.
        public object RememberedSelectable()
        {
            if (RememberedId != null && Selectables.TryGetValue(RememberedId, out var remembered)) return remembered;
            return null;
        }

        // WHICH ID A HANDLE IS DECLARED UNDER, or null if this context does
        // not declare it at all. The dispatcher needs this to record focus
        // memory -- it holds the selected GameObject and has to turn it back
        // into the stable id this context stores (plan section 4, "remembered
        // focus by stable id") -- and ContainsSelectable is the same walk
        // asking a narrower question, so it is expressed in terms of this
        // one rather than duplicating the comparison.
        public string IdOf(object handle)
        {
            if (handle == null) return null;

            foreach (var pair in Selectables)
            {
                if (Equals(pair.Value, handle)) return pair.Key;
            }

            return null;
        }

        public bool ContainsSelectable(object handle) => IdOf(handle) != null;
    }
}
