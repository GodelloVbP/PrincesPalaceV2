using System;

namespace PrincesPalace
{
    // Marks a serialized array that is allowed to be EMPTY in some scenes.
    //
    // UiWiringSweep fails a build on any empty serialized array, and it is
    // right to by default: an empty array is what an unwired field looks like,
    // and the whole point of the sweep is that "the controller is there and one
    // of its arrays is quietly empty" is invisible until something does not
    // happen on screen.
    //
    // But a few fields are genuinely per-scene, where wiring a placeholder
    // just to keep the sweep quiet would put a lie in the scene to satisfy
    // a test. Currently unused -- SystemMenuController's escapeConsumers
    // was the field that forced this into existence and was deleted with it
    // (docs/GAMEPAD_NAVIGATION_PLAN.md phase 2, step B: the menu no longer
    // polls Escape per-scene at all) -- kept rather than removed since the
    // shape it names is a real one and will recur.
    //
    // Carries a REASON for the same purpose AllowOverlap and AllowOverflow do:
    // an exemption that has to be written out is one that can be read back and
    // argued with. Null ELEMENTS are still failures either way -- this only
    // permits the array to be empty, never to be half-wired.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UiOptionalAttribute : Attribute
    {
        public readonly string Reason;

        public UiOptionalAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
