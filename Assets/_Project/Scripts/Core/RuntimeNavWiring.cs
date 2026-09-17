using System.Collections.Generic;

namespace PrincesPalace
{
    // The RUNTIME counterpart to UiNavWiring (Editor, build-time, plan
    // section 9a): wires UnityEngine.UI.Navigation.Explicit across a chain of
    // real Selectables.
    //
    // WHY THIS EXISTS SEPARATELY FROM UiNavWiring/UiNavLinkBuilder, rather
    // than reusing them. Those operate over a UiNode tree declared once at
    // scene-build time under a top-level ScreenDef -- and SystemMenu, Options,
    // RewardTrack and Party are all EMBEDDED inside another screen's tree
    // (SystemMenuScreen.Build is called from HubScreen/FightScreen/
    // MapScreen/TalentScreen, never registered in ScreenRegistry.All on its
    // own), so none of them has a ScreenDef of its own to carry a
    // UiNavDeclaration through. Their existing wiring shape is each
    // controller's own Wire()/Refresh() (SystemMenuController, Options
    // Controller, RewardTrackController, PartyController) -- which is
    // "whichever fits the existing wiring shape" the plan asks for in this
    // case (section 9a). This is that shape's one shared helper, so the same
    // clamp/wrap chain math is not copy-pasted at each of those four call
    // sites -- see docs/CODE_STANDARDS.md section 2 on promoting the second
    // copy rather than the third.
    //
    // Mirrors UiNavLinkBuilder's own List/Rail algorithm (Domain, pure,
    // UiNode-keyed) exactly, one level down at real Selectables -- the two
    // are not unified because doing so would mean threading UiNode through
    // Core, which Domain's engine-free boundary (docs/CODE_STANDARDS.md
    // section 1) does not allow.
    //
    // UnityEngine.UI.Selectable/Navigation are spelled out in full throughout:
    // PrincesPalace.Navigation (Core/Navigation.cs, the map/graph state type)
    // sits in this same namespace and would otherwise shadow the UI one --
    // phase 1's own hazard list calls this out explicitly.
    public static class RuntimeNavWiring
    {
        // Clamp or wrap a horizontal (Left/Right) or vertical (Up/Down) chain
        // -- Rail's and List's shape respectively. A null entry in `members`
        // is skipped in place (its own neighbours still link past it), which
        // is what lets a caller pass an array with some slots hidden/absent
        // without compacting it first.
        public static void Chain(IReadOnlyList<UnityEngine.UI.Selectable> members, bool horizontal, bool wrap)
        {
            int count = members.Count;

            // A single (or no) member has nothing to link to -- see
            // UiNavLinkBuilder's own comment: wrap's modulo math would
            // otherwise link a lone member to itself, which is not "no
            // link", it is a link nowhere that looks like one.
            if (count < 2) return;

            for (int i = 0; i < count; i++)
            {
                var selectable = members[i];
                if (selectable == null) continue;

                UnityEngine.UI.Selectable prev = null;
                UnityEngine.UI.Selectable next = null;
                if (wrap)
                {
                    prev = members[(i - 1 + count) % count];
                    next = members[(i + 1) % count];
                }
                else
                {
                    if (i > 0) prev = members[i - 1];
                    if (i < count - 1) next = members[i + 1];
                }

                var nav = selectable.navigation;
                nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
                if (horizontal)
                {
                    nav.selectOnLeft = prev;
                    nav.selectOnRight = next;
                }
                else
                {
                    nav.selectOnUp = prev;
                    nav.selectOnDown = next;
                }

                selectable.navigation = nav;
            }
        }

        // One node's single cross-group link -- a Rail's last node Down into
        // a footer button, a tab's Down into its pane's own entry. Plan
        // section 9a: "explicit inter-group links".
        public static void Link(UnityEngine.UI.Selectable from, UnityEngine.UI.Selectable to, bool isDown)
        {
            if (from == null) return;

            var nav = from.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            if (isDown) nav.selectOnDown = to; else nav.selectOnUp = to;
            from.navigation = nav;
        }
    }
}
