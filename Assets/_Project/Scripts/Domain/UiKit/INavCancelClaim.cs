namespace PrincesPalace.Domain.UiKit
{
    // FIRST REFUSAL ON A CANCEL PRESS, for a pane that has an open
    // transaction of its own (docs/GAMEPAD_NAVIGATION_PLAN.md section 8;
    // AUDIT.md #156 is the gap this closes).
    //
    // A NavContext has exactly one Cancel handler, decided when it is pushed
    // -- SystemMenu's is Close. That is right for every pane that is only
    // ever being LOOKED at, and wrong for one in the middle of something:
    // Party carrying a character from a seat needs Cancel to put them back,
    // not to close the whole menu out from under the player. The seam is
    // deliberately NOT a Party flag on the context (a flag would have to be
    // set and cleared by somebody, and whoever forgot would leave Cancel
    // meaning the wrong thing): a pane simply answers, at the moment of the
    // press, whether it had something to spend that press on.
    //
    // Contract: return true ONLY if this press was consumed -- there was an
    // open transaction and it is now undone. Returning false (the normal
    // answer, and the answer Options and RewardTrack give by not
    // implementing this at all) hands the press straight to the context's
    // own Cancel.
    public interface INavCancelClaim
    {
        bool ClaimCancel();
    }

    // FIRST REFUSAL ON A SUBMIT PRESS, for a context whose Submit means
    // something other than "press the selected Button" -- the dialogue stage,
    // where Submit/A/Enter advances a line with no row selected at all
    // (docs/PLAN_DIALOGUE_STAGE.md contract 17). Same shape as INavCancelClaim
    // and for the same reason: the context answers at the moment of the press
    // whether it spent it, rather than a flag somebody has to remember to
    // clear.
    //
    // Contract: return true ONLY if this press was consumed. The dispatcher
    // then clears the selection before uGUI's own Submit dispatch, so the
    // same press cannot also reach a Button. False (and every context that
    // declares no claim) leaves Submit exactly as it was: to the selected
    // Button.
    public interface INavSubmitClaim
    {
        bool ClaimSubmit();
    }

    // FIRST -- and only -- OFFER OF A TAB-STEP PRESS: the trigger (LT/RT)
    // shortcut docs/GAMEPAD_NAVIGATION_PLAN.md section 7's "Tabs" contract
    // asks for (plan phase 3, item 2; reassigned from the shoulders to the
    // triggers by the owner's 2026-09-19 hardware-round call). Same shape
    // as INavCancelClaim right above it and for the identical reason: a
    // context declares this by implementing it, and a context that does not
    // (every one but SystemMenu's) simply never sees the press --
    // NavContext.RaiseTabStep no-ops when its own tab-strip provider is
    // null, so nothing has to remember to check "am I a tab strip" at the
    // call site.
    //
    // Contract: StepTab moves the selection ONE step, wrapping, in the
    // given direction (-1 previous tab, +1 next), through the SAME
    // Select(index) the strip's own tab Buttons call on click -- never a
    // second "what is the next tab" computation, or the shoulder and the
    // mouse could disagree about where wrap lands.
    public interface INavTabStrip
    {
        void StepTab(int direction);
    }

    // FIRST -- and only -- OFFER OF A SECTION-STEP PRESS: the shoulder
    // shortcut (LB/RB, reassigned from the triggers by the owner's
    // 2026-09-19 hardware-round call), the same shape as INavTabStrip right
    // above it and declared the same way, by a context that has a second
    // axis of navigation ABOVE its tabs. A screen that does not implement it never
    // sees the press, because NavContext.RaiseSectionStep no-ops when its
    // own section-strip provider is null.
    //
    // Two strips rather than one with a "which level" argument: the
    // shoulders and the triggers are different physical controls bound to
    // different axes, and a context is free to have either, both or
    // neither. Folding them into one interface would force every tab strip
    // to answer for a level it does not have.
    //
    // Contract: StepSection moves ONE step, wrapping, in the given
    // direction (-1 previous, +1 next), through the SAME code path the
    // on-screen section buttons call on click -- never a second "what is
    // the next section" computation, or the trigger and the mouse could
    // disagree about where wrap lands.
    public interface INavSectionStrip
    {
        void StepSection(int direction);
    }
}
