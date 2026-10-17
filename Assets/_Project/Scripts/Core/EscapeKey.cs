using UnityEngine;

namespace PrincesPalace
{
    // Who already acted on this frame's Escape.
    //
    // Escape means "close the thing in front of me". Listeners that share a
    // scene run in whatever order Unity serializes them, so a listener that
    // acts calls Consume() and one that runs later checks ConsumedThisFrame
    // before acting on the same press. No production listener claims the stamp
    // today; only tests and TestGlobals.ResetAll touch it.
    //
    // A frame stamp rather than a bool, so nothing has to remember to clear it.
    public static class EscapeKey
    {
        private static int _consumedFrame = -1;

        // Say so after acting on Escape, so nothing else acts on the same press.
        public static void Consume() => _consumedFrame = Time.frameCount;

        public static bool ConsumedThisFrame => _consumedFrame == Time.frameCount;

        // Tests share a process, and a stamp from a previous test would leak
        // into the next one's first frame.
        public static void Reset() => _consumedFrame = -1;
    }
}
