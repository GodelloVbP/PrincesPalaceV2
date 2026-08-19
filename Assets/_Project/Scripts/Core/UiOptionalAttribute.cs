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
    // But a few fields are genuinely per-scene. SystemMenuController's
    // escapeConsumers is the one that forced this: the hub has a glossary and a
    // relic draft that own Escape, the fight has its reward screen, and the map
    // has nothing at all. Wiring the map a placeholder to keep the sweep quiet
    // would be worse than the check -- it would put a lie in the scene to
    // satisfy a test.
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
