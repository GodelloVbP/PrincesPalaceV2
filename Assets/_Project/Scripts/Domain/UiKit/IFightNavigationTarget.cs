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

        // THE HORIZONTAL AXIS, and the one thing a fight does with it: step
        // sideways off the verb column onto the actors themselves, so that
        // "what is this monster carrying" is a question a pad can ask without
        // first committing to ATTACK or a spell (the owner's 2026-09-19 call).
        //
        // A SECOND METHOD RATHER THAN A SECOND ARGUMENT ON MoveFocus, because
        // the two axes do not do the same thing at any depth: vertical walks
        // whatever list the current MenuDepth makes live, and this one walks a
        // ring of combatants that no depth owns. Folding them together would
        // mean every implementor -- and every existing direct-call test of
        // MoveFocus -- growing an axis argument to answer a question only one
        // of the two ever asks.
        //
        // +1 is right/down the ring, -1 is left/up it. What a step actually
        // does, including where the ring starts and which end leads back to
        // the verb column, is the implementor's rule: see
        // FightController.Input.cs's InspectMove.
        void InspectMove(int delta);

        // WHAT THE PAD IS POINTING AT RIGHT NOW -- the verb plate, the
        // submenu row, the enemy plate or the ally plate, whichever this
        // fight's menu depth makes the live one. Null while there is nothing
        // to point at (no session, mid-resolution, an empty rack).
        //
        // An opaque `object`, the same handle convention NavContext uses for
        // its own Selectables, because this assembly is engine-free and a
        // GameObject is a UnityEngine type. NavigationInputModule casts it
        // back at the boundary, which is the only place it ever leaves an
        // implementor's hands.
        //
        // A READ-ONLY WINDOW ONTO STATE FIGHT ALREADY KEPT. Nothing about
        // MoveFocus/ConfirmFocus/OnBackPressed changes to serve this: the
        // implementation reads _focusedVerb, _menu.RowSelection,
        // _hoveredEnemyIndex and _hoveredAllyIndex, every one of which
        // existed and was already driven by those three.
        object FocusedElement { get; }
    }
}
