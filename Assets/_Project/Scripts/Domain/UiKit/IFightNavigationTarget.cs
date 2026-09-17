namespace PrincesPalace.Domain.UiKit
{
    // The three calls the Fight branch of the input dispatcher makes, moved
    // verbatim out of FightController.Input.cs's own PollGamepadNavigation
    // (see docs/GAMEPAD_NAVIGATION_PLAN.md section 3). A small interface
    // rather than a direct FightController reference so NavContext (engine-
    // free) can hold a target without ever seeing a MonoBehaviour, and so a
    // PlayMode test can stand a stub in for FightController without touching
    // a scene at all.
    public interface IFightNavigationTarget
    {
        void MoveFocus(int delta);
        void ConfirmFocus();
        void OnBackPressed();
    }
}
