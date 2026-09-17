using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE ADAPTER for TooltipFocus, and nothing else -- it holds the node ->
    // "tell this node it was entered/left" map, adds the SelectIndex that
    // turns module focus into an event, and force-closes on a context-stack
    // change. The precedence itself (focus over pointer, a stale leave
    // ignored) is Domain's TooltipFocus; this file decides no rule.
    //
    // WHY A ROUTER AND NOT A SECOND HANDLER PER SCREEN. Both screens that
    // show a tooltip already have one entry point per surface that the
    // pointer drives -- CharacterDossierController.OnSlotHover/
    // OnAttributeHover/OnPackHover and ReckoningController.OnOfferHover, each
    // taking (index, entered). Selection could have called those directly,
    // and that is precisely the mistake: two callers with no arbiter means
    // the mouse's "entered: false" can hide a box the stick is holding open,
    // and each screen would grow its own copy of the arbitration. Here the
    // hover handler becomes the ONE show/hide implementation and both inputs
    // are callers of it.
    //
    // Lifecycle: built and Registered once by the controller's own Wire()
    // (the delegates capture an index, which nothing can serialise), Attached
    // on OnEnable and Detached on OnDisable -- the stack it subscribes to is
    // NavigationInputModule.Contexts, which is replaced on every scene load,
    // so the subscription must not outlive the enable that made it.
    public sealed class TooltipFocusRouter
    {
        private readonly TooltipFocus _focus = new TooltipFocus();

        // Keyed on the GameObject rather than the Button, because that is
        // what the EventSystem selects and what a pointer handler can hand
        // back -- one handle type, so a caller cannot register under one and
        // report under the other.
        private readonly Dictionary<GameObject, Action<bool>> _shows =
            new Dictionary<GameObject, Action<bool>>();

        private NavContextStack _stack;

        // WHICH CONTEXT WAS ON TOP when the showing tooltip was shown. The
        // whole force-close rule is one comparison against this: anything
        // pushed above the screen, and the screen's own context being popped,
        // both change what Top is -- so one check covers both cases named in
        // the plan (a modal opening over the screen, and the screen closing)
        // without the screen having to hold a reference to its own context,
        // which the dossier could not do anyway (its context belongs to
        // SystemMenuController).
        private NavContext _topWhenShown;

        public GameObject Shown => _focus.Shown as GameObject;

        // One node, one show/hide delegate, plus the SelectIndex that reports
        // module focus on it. `show` is the screen's existing pointer
        // handler with its index already bound -- e.g.
        // `entered => OnPackHover(i, entered)`.
        public void Register(Selectable node, Action<bool> show)
        {
            if (node == null || show == null) return;

            var go = node.gameObject;
            _shows[go] = show;

            // Reused if something already added one (PartyController adds
            // SelectIndex to its own seats for a different job): one per
            // GameObject is all ExecuteEvents will find, and two would both
            // fire.
            var select = go.GetComponent<SelectIndex>() ?? go.AddComponent<SelectIndex>();
            select.Changed = (_, selected) => Selection(go, selected);
        }

        // The pointer's half. Called from the screen's existing HoverIndex
        // hook instead of the handler it used to call directly.
        public void Pointer(GameObject node, bool entered) => Apply(_focus.Pointer(node, entered));

        private void Selection(GameObject node, bool selected) => Apply(_focus.Selection(node, selected));

        public void Attach(NavContextStack stack)
        {
            Detach();

            _stack = stack;
            if (_stack != null) _stack.Changed += OnContextsChanged;
        }

        public void Detach()
        {
            if (_stack != null) _stack.Changed -= OnContextsChanged;
            _stack = null;

            // Nothing showing once the screen is gone: a tooltip left open
            // across a disable comes back with the screen (the box is a child
            // of the screen, so SetShown(true) survives the round trip) and
            // would be describing whatever the selection used to be.
            ForceClose();
        }

        public void ForceClose() => Apply(_focus.ForceClose());

        private void OnContextsChanged()
        {
            if (_focus.Shown == null) return;
            if (ReferenceEquals(_stack?.Top, _topWhenShown)) return;

            ForceClose();
        }

        private void Apply(TooltipFocusChange change)
        {
            if (!change.Any) return;

            // LEAVE BEFORE ENTER. A screen's "entered: false" branch does
            // more than hide the shared box -- the dossier unlights the
            // derived-stat rows an attribute lit and clears the equip
            // preview -- so the outgoing node has to be told first or its
            // highlight outlives it. And the other order would have the
            // outgoing node's hide undo the incoming node's show.
            if (change.Leave is GameObject leaving && _shows.TryGetValue(leaving, out var leave)) leave(false);
            if (change.Enter is GameObject entering && _shows.TryGetValue(entering, out var enter)) enter(true);

            _topWhenShown = change.Enter == null ? null : _stack?.Top;
        }
    }
}
