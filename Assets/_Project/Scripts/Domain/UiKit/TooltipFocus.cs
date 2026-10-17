namespace PrincesPalace.Domain.UiKit
{
    // WHO OWNS THE ONE TOOLTIP BOX: the pointer, or the selection
    // (docs/GAMEPAD_NAVIGATION_PLAN.md section 7, "Tooltips").
    //
    // Two screens show a tooltip for the thing under the cursor -- the
    // dossier's slots/scores/pack cells and the Reckoning's offer cards --
    // and a stick has no cursor, so selection has to drive the same box. The
    // trap is writing that as a second show/hide path per screen: the two
    // would then disagree about which node the box belongs to the moment
    // both a hover and a selection exist, which is the ordinary case (a
    // player who touches the mouse once and then goes back to the pad).
    //
    // So the precedence is decided HERE, once, and each screen keeps exactly
    // one show/hide implementation that this thing calls. The rules, as
    // trigger -> condition -> outcome:
    //
    //   Selection lands on a node            -> show that node.
    //   Pointer enters another node while a  -> nothing: focus wins, the
    //     selected node's tooltip is up         selected node's box stays.
    //   Pointer enters the node already      -> nothing (it is already up).
    //     showing
    //   Selection leaves a node, pointer     -> the box stays, now owned by
    //     is over that same node                the pointer (today's mouse
    //                                           behaviour, unchanged).
    //   Selection leaves a node, pointer     -> hide.
    //     is elsewhere or nowhere
    //   A context is pushed above, or the    -> force close (ForceClose),
    //     screen's own context is popped        whatever owned it.
    //   The screen selects a node itself     -> nothing for that selection
    //     and marks it quiet (Quiet)            (the pointer still shows what
    //                                           it is over), until the
    //                                           selection moves: the player
    //                                           has not asked about it yet.
    //
    // Pure C# and engine-free (docs/CODE_STANDARDS.md "Layering"): a node is
    // an opaque handle this class only ever compares by REFERENCE and hands
    // back, the same bargain NavContext makes for its Selectables. Reference
    // equality rather than Equals on purpose -- a UnityEngine.Object that has
    // been destroyed compares EQUAL TO NULL through its own overridden
    // Equals, so an Equals-based match could quietly decide a destroyed node
    // is the node a caller just asked about.
    public sealed class TooltipFocus
    {
        private object _hovered;
        private object _selected;
        private object _shown;
        private object _quiet;

        // Which node's tooltip is up, or null. Exposed for a test and for a
        // caller that wants to re-ask its own show path for the same node
        // (the dossier re-hovers a cell whose contents changed under it).
        public object Shown => _shown;

        // The pointer entered or left `node`. A leave is only believed for
        // the node still recorded as hovered: a real pointer can report
        // leaving A after it has already reported entering B (and the
        // EventSystem does exactly this on a fast drag), and clearing the
        // record on the stale event would lose B.
        public TooltipFocusChange Pointer(object node, bool entered)
        {
            if (entered) _hovered = node;
            else if (ReferenceEquals(_hovered, node)) _hovered = null;

            return Settle();
        }

        // The module selected or deselected `node` -- Core's SelectIndex
        // (ISelectHandler/IDeselectHandler) is what reports this. Same
        // stale-leave rule as Pointer above, and it is not hypothetical
        // here: Unity raises OnDeselect for the old node AFTER OnSelect for
        // the new one in some orders, which PartyController's own
        // OnSeatSelectionChanged already had to defend against.
        public TooltipFocusChange Selection(object node, bool selected)
        {
            if (selected)
            {
                _selected = node;
                if (!ReferenceEquals(node, _quiet)) _quiet = null;
            }
            else if (ReferenceEquals(_selected, node))
            {
                // A deselect with nothing taking over spends the quiet: the next
                // selection of this node is the player's own.
                _selected = null;
                if (ReferenceEquals(node, _quiet)) _quiet = null;
            }

            return Settle();
        }

        // The screen is about to select `node` by itself (a screen opening on
        // its entry): that selection shows nothing until the player moves it.
        // A Reckoning page opening with its first card's detail drawn over the
        // next cards is the case this exists for.
        public TooltipFocusChange Quiet(object node)
        {
            _quiet = node;
            return Settle();
        }

        // Everything is dropped, not just the showing node: the reason to
        // force close is that the screen is no longer the one being
        // operated (a modal went up over it, or its own context came off the
        // stack), so a remembered hover or selection underneath would be a
        // claim about a screen the player has left. The next real pointer or
        // selection event re-establishes both.
        public TooltipFocusChange ForceClose()
        {
            _hovered = null;
            _selected = null;
            _quiet = null;

            var leaving = _shown;
            _shown = null;
            return leaving == null ? TooltipFocusChange.None : new TooltipFocusChange(leaving, null);
        }

        // FOCUS OVER POINTER, in one line, which is the whole precedence
        // rule -- and why there is no fourth field remembering "who is
        // driving": the answer is derivable from the two records, so it
        // cannot drift out of step with them.
        private TooltipFocusChange Settle()
        {
            var selected = ReferenceEquals(_selected, _quiet) ? null : _selected;
            var wanted = selected ?? _hovered;
            if (ReferenceEquals(wanted, _shown)) return TooltipFocusChange.None;

            var leaving = _shown;
            _shown = wanted;
            return new TooltipFocusChange(leaving, wanted);
        }
    }

    // What the caller must now do to its own single show/hide path: tell the
    // outgoing node it was left, then tell the incoming node it was entered
    // -- in that order, and both through the SAME entry point the pointer
    // handlers already call (ShowPreviewFor/OnPackHover with
    // entered: true/false).
    //
    // Both halves are needed rather than just the incoming one, because a
    // screen's "entered: false" branch does more than hide the box: the
    // dossier's attribute hover also unlights the derived-stat rows it lit,
    // and its pack hover clears the equip preview. Showing B without
    // leaving A would leave A's highlight behind.
    public readonly struct TooltipFocusChange
    {
        public static readonly TooltipFocusChange None = default;

        public readonly object Leave;
        public readonly object Enter;

        public TooltipFocusChange(object leave, object enter)
        {
            Leave = leave;
            Enter = enter;
        }

        // Nothing to do. A struct rather than a nullable so a caller cannot
        // dereference a "no change" answer by accident.
        public bool Any => Leave != null || Enter != null;
    }
}
