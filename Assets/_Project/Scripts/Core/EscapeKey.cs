using UnityEngine;

namespace PrincesPalace
{
    // Who already acted on this frame's Escape.
    //
    // Escape means "close the thing in front of me", and more than one
    // component listens for it in the same scene: the hub closes its debug menu
    // and its glossary, and the system menu opens and closes itself. They sit
    // on the SAME GameObject, so Unity's Update order between them is whatever
    // serialization order happens to be -- which is not a thing to build
    // behaviour on.
    //
    // The race this prevents is real and was introduced by fixing a different
    // bug. While SystemMenuController lived on its own inactive modal it could
    // not run at all with the menu closed, so it could never see an Escape the
    // hub was about to consume. Moving it to the scene root so that Escape can
    // OPEN the menu also put it in the same frame as everyone else: with the
    // glossary up, whichever ran first decided whether Escape dismissed the
    // glossary or dismissed it AND opened the system menu on top.
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
