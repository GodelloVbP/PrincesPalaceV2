using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

// Gamepad navigation, plan section 9a's structural rule that survives phase
// 2's unification: a custom actionable control -- anything implementing
// IPointerClickHandler, IPointerDownHandler or IDragHandler on a GameObject
// that carries NO Selectable -- must declare, on its own UiNode, why it is
// not stick-reachable (AllowUnreachable("reason")).
//
// WHY THIS ONE IS STILL A BUILD-TIME CHECK when the declaration half is not
// (RuntimeNavWiring's header has the full argument): what it inventories is
// not which nodes are navigable right now -- that is a runtime fact -- but
// WHICH CONTROLS A SCREEN EMITTED AT ALL. A screen's emitted component set is
// fixed by the build, so "this thing answers a click but no stick can reach
// it, and nobody said why" is answerable here and nowhere else.
//
// NO LONGER OPT-IN. It used to return clean whenever the screen had no
// UiNavDeclaration, which was every screen, always -- a check that never ran.
// Its own vacuity is worth stating plainly rather than hiding
// (docs/CODE_STANDARDS.md section 8): most of this project's custom
// actionable components (BarSlider, HoldToConfirm, ListScroll, RailScroll)
// are AddComponent'd by their controller at RUNTIME, where no build-time scan
// can see them, so today this fires on the build-time Attach<T> path only.
// That is the path that grows: every new custom control wired in a screen's
// own Wire step lands here and has to say something.
//
// A GAMEOBJECT THAT IS ITSELF A SELECTABLE IS EXEMPT WHOLESALE, not merely
// "its Selectable component is skipped". A Button is reachable by stick by
// construction, and whatever else sits on it (ThemedButtonState's pointer
// handlers, PartyDragSource's drag handlers) is reached through that same
// Button, not around it.
public static class UiNavControlsAudit
{
    public static void Run(string screenName, UiEmitResult result)
    {
        var problems = new List<string>();

        foreach (var pair in result.Objects)
        {
            var node = pair.Key;
            var go = pair.Value;
            if (go == null) continue;
            if (node.AllowUnreachableReason != null) continue;
            if (go.GetComponent<Selectable>() != null) continue;

            if (!HasCustomActionableComponent(go)) continue;

            problems.Add(
                $"'{node.Name}' carries a custom actionable component (IPointerClickHandler/IPointerDownHandler/" +
                "IDragHandler) on a node that is not a Selectable, so no stick or keyboard can reach it. " +
                "Fix by: making it a real Selectable and wiring it into its screen's own nav groups " +
                "(RuntimeNavWiring); or, if it is genuinely not meant to be stick-reachable, declaring " +
                "AllowUnreachable(\"reason\") on its UiNode.");
        }

        if (problems.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"[SceneBuilder] FAILED: '{screenName}' has {problems.Count} unreachable actionable control(s):");
        foreach (var problem in problems) sb.AppendLine($"  {problem}");
        throw new System.Exception(sb.ToString());
    }

    private static bool HasCustomActionableComponent(GameObject go)
    {
        foreach (var component in go.GetComponents<Component>())
        {
            if (component == null) continue; // a missing-script slot, not this check's business

            if (component is IPointerClickHandler || component is IPointerDownHandler || component is IDragHandler)
            {
                return true;
            }
        }

        return false;
    }
}
