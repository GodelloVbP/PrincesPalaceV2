using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

// Gamepad navigation, plan section 9a's fourth structural rule: a custom
// actionable control -- anything implementing IPointerClickHandler,
// IPointerDownHandler or IDragHandler that is NOT itself a Selectable -- must
// either sit inside a declared nav group or carry AllowUnreachable("reason").
// A Selectable (Button, Toggle, Scrollbar...) is already reachable through
// the ordinary stick+Submit path and is exempt by construction; this check
// exists for everything else -- "not only HoverIndex by name", closing the
// review's "not a complete inventory" point the plan cites.
//
// Lives in the Editor layer, not UiAudit, because it needs to see the actual
// emitted GameObjects and their Unity components -- something a Domain-layer
// UiNode can never reference (docs/CODE_STANDARDS.md section 1). UiAudit's
// CheckNavigable covers the structural rules that only need the declared
// UiNavDeclaration; this one needs the built result too, the same reason
// UiWiringSweep and UiBindingAudit are Editor-side rather than Domain-side.
//
// OPT-IN FOR PHASE 2, same as UiAudit.CheckNavigable: `nav == null` returns
// clean, so an undeclared screen does not fail the build. Phase 3 removes
// the opt-in once every screen has a declaration.
public static class UiNavControlsAudit
{
    public static void Run(string screenName, UiNavDeclaration nav, UiEmitResult result)
    {
        if (nav == null) return;

        var declared = new HashSet<UiNode>();
        foreach (var group in nav.Groups)
        {
            foreach (var member in group.Members) declared.Add(member);
        }

        var problems = new List<string>();

        foreach (var pair in result.Objects)
        {
            var node = pair.Key;
            var go = pair.Value;
            if (go == null) continue;
            if (declared.Contains(node)) continue;
            if (node.AllowUnreachableReason != null) continue;

            if (!HasCustomActionableComponent(go)) continue;

            problems.Add(
                $"'{node.Name}' carries a custom actionable component (IPointerClickHandler/IPointerDownHandler/" +
                "IDragHandler, not a Selectable) but is not a member of any declared nav group. " +
                "Fix by: adding it to the group it belongs in; or, if it is genuinely not meant to be " +
                "stick-reachable, declaring AllowUnreachable(\"reason\") on its UiNode.");
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
            if (component is Selectable) continue;

            if (component is IPointerClickHandler || component is IPointerDownHandler || component is IDragHandler)
            {
                return true;
            }
        }

        return false;
    }
}
