using UnityEngine;

namespace PrincesPalace
{
    // Switching a GameObject on or off, guarded.
    //
    // Written out seven times across Core before this -- the fight HUD, the
    // map, the Reckoning, the system menu, the dossier, the talents and the
    // exits pane -- each as its own private static with the same two lines.
    //
    // THE GUARD IS THE POINT, and it is the half that would go missing from a
    // copy. SetActive is not free when the value is already correct: Unity
    // still walks the object's children, still fires OnEnable/OnDisable down
    // the subtree, and in uGUI still dirties the canvas that owns it. These
    // are called from refresh paths that run on every repaint -- RefreshUi
    // touches every verb, every plate and every stage slot -- so an unguarded
    // version rebuilds a canvas batch every frame for a screen where nothing
    // changed.
    //
    // The OnEnable half is worse than the cost: several components in this
    // project treat "just activated" as "just opened" (ColumnOpenAnimator says
    // so in as many words, and TalentNodeInvestReveal's whole EnsureShown
    // contract depends on it), so re-activating an already-active object
    // replays animations that were never meant to fire.
    //
    // An extension rather than a static helper: `panel.Show(false)` reads at
    // the call site the way the guard should be thought about -- a property of
    // the object, not a utility applied to it. It cannot be named SetActive,
    // since C# resolves the real instance method first and the extension would
    // silently never be called.
    public static class Show
    {
        public static void SetShown(this GameObject go, bool shown)
        {
            if (go != null && go.activeSelf != shown) go.SetActive(shown);
        }

        // The same, for anything reached as a component. Saves the
        // `.gameObject` at a hundred call sites and cannot be handed a null
        // reference by accident.
        public static void SetShown(this Component component, bool shown)
        {
            if (component != null) component.gameObject.SetShown(shown);
        }
    }
}
