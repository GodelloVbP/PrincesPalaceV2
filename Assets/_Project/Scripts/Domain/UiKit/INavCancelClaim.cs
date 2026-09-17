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
}
