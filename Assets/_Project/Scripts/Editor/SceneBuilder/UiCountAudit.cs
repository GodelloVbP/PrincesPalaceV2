using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

// E4: a bound array's length equals the number of nodes actually declared for
// it.
//
// This is the check that makes the ec19c3e class of bug unwritable rather than
// merely unlikely. That bug was: the builder sized a row strip from
// ContentDatabase.Items while the controller filled it from Consumables. The
// counts disagreed, blank rows ran off the panel, and nothing noticed because
// the two numbers lived in two files.
//
// Ui.Each removes the second number at the DECLARATION site. E4 closes the
// remaining gap at the BINDING site: nothing stops a Wire step from assigning
// an array built some other way, and a controller that iterates `slotButtons`
// while the screen declared a different number of slots is the same bug wearing
// a different hat.
//
// Compares against what was EMITTED, not against a count passed in - a count
// passed in would just be the third copy of the number.
public static class UiCountAudit
{
    // Declared groups the screen wants checked: the nodes it built, and the
    // array it bound them to.
    public sealed class Binding
    {
        public string Label;
        public IReadOnlyList<NodeRef> Declared;
        public System.Func<int> BoundLength;
    }

    public static void Run(string screenName, IEnumerable<Binding> bindings)
    {
        if (bindings == null) return;

        var problems = new List<string>();

        foreach (var binding in bindings)
        {
            int declared = binding.Declared?.Count ?? 0;
            int bound = binding.BoundLength();

            if (declared == bound) continue;

            problems.Add(
                $"  '{binding.Label}': the screen declared {declared} node(s) but the controller was bound " +
                $"{bound}.\n" +
                $"    This is the shape of the shipped Store bug - a strip sized from one collection and filled " +
                $"from another.\n" +
                $"    Fix by: building the array from the SAME list the nodes came from, so there is only ever " +
                $"one count.");
        }

        if (problems.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"[SceneBuilder] FAILED: '{screenName}' has {problems.Count} count mismatch(es):");
        foreach (var p in problems) sb.AppendLine(p);
        throw new System.Exception(sb.ToString());
    }
}
