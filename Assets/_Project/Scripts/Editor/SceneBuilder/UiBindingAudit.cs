using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

// F9: every wired UI reference points at the node it was supposed to point at.
//
// THE CHECK UiWiringSweep DOES NOT MAKE. That sweep asks "is this field
// non-null", which a field bound to entirely the wrong object answers
// perfectly. Between the two, every serialized reference on every attached
// controller is now either declared optional and empty, or filled with the
// specific object the screen named -- and neither question is "how many
// fields did the binder fill", a number that is the same whether the binder
// was right or wrong.
//
// Two routes fill a field, and each is checked against what IT claimed:
//
//   - AUTO-BOUND. UiAutoBind records the node its name rule chose
//     (UiEmitResult.AutoBoundFields). The field must hold a component on
//     exactly that emitted object. This is the one comparison that can catch
//     the name rule matching something unintended, which is the failure
//     NodeRef exists to prevent and which nothing checked until now.
//
//   - EXPLICIT. The ~115 hand-written Wire lines are NOT wrapped -- doing so
//     would have meant editing every one of them, and a rule that costs a
//     line per site is a rule that gets skipped at site 116. Instead the
//     record is taken at the seam they all pass through: UiEmitResult's five
//     typed accessors, which are the only way a Wire line can reach an
//     emitted object at all. Each hands back a Provenance saying which
//     NodeRef produced the value. The field must hold something that came
//     from this result, on that node -- or, for a lookup that opted into
//     `searchChildren: true`, somewhere underneath it.
//
// Scope is the five bindable types (UiAutoBind.IsBindable's set). A field
// typed Sprite, IconEntry[] or another controller is filled from content or
// from another component and has no node to be checked against; those stay
// UiWiringSweep's and E4's business.
public static class UiBindingAudit
{
    private const BindingFlags DeclaredInstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static void Run(string screenName, UiEmitResult result)
    {
        if (result == null) return;

        var problems = new List<string>();
        var emitted = new HashSet<GameObject>(result.Objects.Values);

        foreach (var controller in result.AttachedControllers)
        {
            if (controller == null) continue;

            foreach (var field in SerializedFields(controller.GetType()))
            {
                var elementType = field.FieldType.IsArray ? field.FieldType.GetElementType() : field.FieldType;
                if (!IsBindable(elementType)) continue;

                if (field.FieldType.IsArray)
                {
                    if (!(field.GetValue(controller) is Array array)) continue;
                    for (int i = 0; i < array.Length; i++)
                    {
                        Check(result, emitted, controller, field.Name, i,
                            array.GetValue(i) as UnityEngine.Object, problems);
                    }
                }
                else
                {
                    Check(result, emitted, controller, field.Name, -1,
                        field.GetValue(controller) as UnityEngine.Object, problems);
                }
            }
        }

        if (problems.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine(UiBindingContract.Header(screenName, problems.Count));
        foreach (var p in problems) sb.AppendLine("  " + p);
        sb.AppendLine("  Fix by: renaming the field so the name rule reaches the node it means " +
                      "(UiBindingNames states the rule), or correcting the NodeRef on this screen's Wire line.");
        throw new Exception(sb.ToString());
    }

    private static void Check(
        UiEmitResult result,
        HashSet<GameObject> emitted,
        Component controller,
        string fieldName,
        int index,
        UnityEngine.Object value,
        List<string> problems)
    {
        // A null is UiWiringSweep's finding, not this one, and reporting it
        // twice would make one mistake read as two.
        if (value == null) return;

        var actual = GameObjectOf(value);
        if (actual == null) return;

        string path = index < 0 ? fieldName : $"{fieldName}[{index}]";
        string controllerName = controller.GetType().Name;

        var record = AutoBoundRecordFor(result, controller, fieldName, index);
        if (record != null)
        {
            // Through the emitted map rather than Go(), which would record a
            // fresh hand-out and make the audit a participant in what it audits.
            result.Objects.TryGetValue(record.Node, out var expected);
            if (!ReferenceEquals(actual, expected))
            {
                problems.Add(UiBindingContract.WrongNode(
                    controllerName, path, record.Node.Name, actual.name,
                    UiBindingContract.Source.AutoBound));
            }

            return;
        }

        if (!result.TryProvenanceOf(value, out var provenance))
        {
            // NOT EVERY CORRECT BINDING COMES THROUGH THE FIVE ACCESSORS.
            // UiEmitter wires a themed button's own Plate and Glow while it
            // is building them (WireThemedButton), which is right -- those
            // are the widget's internals, not a screen's wiring decision --
            // and they never pass through a Wire line to be recorded. What
            // can still be asserted about a value with no provenance is the
            // weaker half, and it is not vacuous: it must be an object this
            // screen DECLARED as a node, not a generated child and not
            // something from another screen entirely.
            if (!emitted.Contains(actual))
            {
                problems.Add(UiBindingContract.NotFromThisScreen(controllerName, path, actual.name));
            }

            return;
        }

        result.Objects.TryGetValue(provenance.Node, out var declared);
        if (declared == null) return;

        // An exact lookup must BE the node; an opted-in one must be somewhere
        // under it. The second is deliberately the weaker claim -- it is the
        // whole reason `searchChildren: true` has to be typed out at the two
        // sites that want a button's generated caption.
        bool ok = provenance.ViaChildren
            ? actual.transform.IsChildOf(declared.transform)
            : ReferenceEquals(actual, declared);

        if (!ok)
        {
            problems.Add(UiBindingContract.WrongNode(
                controllerName, path, provenance.Node.Name, actual.name, UiBindingContract.Source.Explicit));
        }
    }

    // Matched on the controller INSTANCE, not its type: one screen can attach
    // two of the same controller (MainMenu's two save-slot panels), and a
    // type-keyed lookup would let one's binding record answer for the other's
    // field. A linear scan because the list is one screen's worth of bindings
    // and this runs once per build -- an index would be a second structure to
    // keep true for no measurable gain.
    private static UiEmitResult.AutoBound AutoBoundRecordFor(
        UiEmitResult result, Component controller, string fieldName, int index)
    {
        foreach (var record in result.AutoBoundFields)
        {
            if (record != null
                && ReferenceEquals(record.Controller, controller)
                && record.FieldName == fieldName
                && record.Index == index)
            {
                return record;
            }
        }

        return null;
    }

    private static GameObject GameObjectOf(UnityEngine.Object value)
    {
        if (value is GameObject go) return go;
        if (value is Component component) return component.gameObject;
        return null;
    }

    // Same walk UiAutoBind does, and for the same reason: GetFields without
    // DeclaredOnly skips a base class's private fields, which serialize
    // perfectly well and would fall outside this audit exactly as they would
    // fall outside the binder.
    private static IEnumerable<FieldInfo> SerializedFields(Type type)
    {
        for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType)
        {
            foreach (var field in current.GetFields(DeclaredInstanceFields))
            {
                if (field.IsDefined(typeof(SerializeField), inherit: true)) yield return field;
            }
        }
    }

    private static bool IsBindable(Type type) =>
        type == typeof(TMP_Text) || type == typeof(Image) || type == typeof(Button)
        || type == typeof(GameObject) || type == typeof(RectTransform);
}
