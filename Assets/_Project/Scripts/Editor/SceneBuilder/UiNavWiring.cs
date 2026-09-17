using System.Collections.Generic;
using PrincesPalace.Domain.UiKit;

// Applies a screen's UiNavDeclaration to the Selectables UiEmitter just
// built -- the ONLY place Navigation.Explicit links get written (plan
// section 9a/5). Runs after UiAudit.CheckNavigable has already proven the
// declaration resolves against this exact tree (SceneBuilder.BuildScene
// calls that first and throws before this ever runs on a bad declaration).
//
// UnityEngine.UI.Navigation is spelled out in full throughout this file:
// PrincesPalace.Navigation (the map/graph state type) collides with it by
// name, and phase 1's own hazard list calls this out explicitly.
public static class UiNavWiring
{
    public static void Apply(UiNavDeclaration nav, UiEmitResult result)
    {
        if (nav == null) return;

        var resolved = UiNavLinkBuilder.Build(nav);
        foreach (var pair in resolved)
        {
            var selectable = ResolveSelectable(pair.Key, result);

            // A declared node that emitted no Selectable at all (a Panel
            // used only as a Graph-link anchor, say) has nothing to write
            // Navigation onto. UiNavControlsAudit is the check that cares
            // whether something ELSE on this node needed to be reachable;
            // this pass only ever writes to what actually IS a Selectable.
            if (selectable == null) continue;

            var links = pair.Value;
            var navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp = ResolveSelectable(links.Up, result),
                selectOnDown = ResolveSelectable(links.Down, result),
                selectOnLeft = ResolveSelectable(links.Left, result),
                selectOnRight = ResolveSelectable(links.Right, result),
            };
            selectable.navigation = navigation;
        }
    }

    private static UnityEngine.UI.Selectable ResolveSelectable(UiNode node, UiEmitResult result)
    {
        if (node == null) return null;
        if (!result.Objects.TryGetValue(node, out var go) || go == null) return null;
        return go.GetComponent<UnityEngine.UI.Selectable>();
    }
}
