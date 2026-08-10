using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

// E1: does the text actually fit the box it was given?
//
// Domain deliberately has no idea how wide a glyph is -- it is engine-free, and
// giving it font metrics would mean giving it TMP. So text nodes carry explicit
// sizes, and the ONE place real measurement can happen is here, after the
// emitter has attached a real TMP_Text with a real font.
//
// Templated strings are measured at their AuditSample ("Gold: 999999    Relics:
// 99"), not their template. Measuring "Gold: {0}    Relics: {1}" would pass
// happily and every real overflow would ship.
public static class UiTextFitAudit
{
    // A pixel or two of slack: TMP's preferred values and a hand-authored box
    // will not agree exactly, and failing a build over sub-pixel rounding would
    // train people to widen every box "just in case".
    private const float Tolerance = 1f;

    public static void Run(string screenName, UiNode tree, UiEmitResult result)
    {
        var problems = new List<string>();

        foreach (var pair in result.Objects)
        {
            var node = pair.Key;
            if (node.Kind != UiNodeKind.Label && node.Kind != UiNodeKind.Button) continue;
            if (!node.Text.IsValid) continue;

            // Content-derived text is data, not authored copy; its length is
            // whatever the JSON says and cannot be pinned at build time.
            if (node.Text.IsContent) continue;

            var label = pair.Value.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (label == null) continue;

            var rect = (RectTransform)label.transform;
            float boxWidth = rect.rect.width;
            float boxHeight = rect.rect.height;
            if (boxWidth <= 0f || boxHeight <= 0f) continue;

            string sample = node.Text.AuditSample;
            var preferred = label.GetPreferredValues(sample, boxWidth, 0f);

            if (preferred.x <= boxWidth + Tolerance && preferred.y <= boxHeight + Tolerance) continue;

            problems.Add(
                $"  '{node.Name}' ({node.Kind}) is {boxWidth:0.#}x{boxHeight:0.#} but \"{sample}\" needs " +
                $"{preferred.x:0.#}x{preferred.y:0.#} at font size {node.FontSize}.\n" +
                $"    Fix by: widening the node; or reducing fontSize; or shortening the string in UiStrings; " +
                $"or giving the entry a tighter AuditSample if this worst case is not real.");
        }

        if (problems.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"[SceneBuilder] FAILED: '{screenName}' has {problems.Count} label(s) whose text does not fit:");
        foreach (var p in problems) sb.AppendLine(p);
        throw new System.Exception(sb.ToString());
    }
}
