using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

// Binds a controller's UI references from the screen that declared them, using
// the CONTROLLER FIELD'S OWN IDENTIFIER as the key.
//
// WHY THIS IS NOT v1's SetField. v1 wired 330 sites through
// SetField(controller, "playButton", value): a hand-written NAME STRING per
// site, each one independently misspellable, and a misspelling produced no
// compile error, no runtime error, and a field that stayed null until something
// unrelated threw much later. This class contains zero name strings and no site
// writes one -- the key is the field's declared identifier, which the compiler
// already checked, and renaming the field renames the key with it. What a typo
// can still do is fail to MATCH, and that is loud: UiWiringSweep reads the
// serialized view of every attached controller after every screen is emitted
// and refuses the build on any null reference or empty array not marked
// [UiOptional]. A miss cannot ship; it cannot even build.
//
// Deliberately narrow, because a binder's danger is confidence:
//   - exact name, first letter's case aside (UiBindingNames). No suffix
//     stripping, no scoring. `exitLabels` does not reach `ExitButtons`.
//   - only the five types UiEmitResult has a typed lookup for, matched
//     EXACTLY: a field declared `Graphic` binds nothing.
//   - a field that is already non-null is left alone, so an explicit line
//     written before the call always wins.
//   - anything with no same-named NodeRef is skipped in silence and left to
//     the sweep. Throwing here would mean this class had to know about every
//     field a Wire step fills some other way -- a Sprite off LoadSpriteByKey,
//     an IconEntry[] off ContentDatabase, another controller.
public static class UiAutoBind
{
    private const BindingFlags DeclaredInstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // Returns what it bound, in walk order: the field, and the NODE the name
    // rule chose for it. The node is the half that matters -- a list of field
    // NAMES only ever supported "how many did it fill", which is a measure of
    // effort and not of correctness, and would pass for a screen whose every
    // field landed on the wrong object. Recorded onto the result as well, so
    // UiBindingAudit can check each one against the node it was supposed to
    // reach without SceneBuilder having to be handed the screen.
    public static List<UiEmitResult.AutoBound> Bind(UiEmitResult result, Component controller, object screen)
    {
        var bound = new List<UiEmitResult.AutoBound>();
        if (result == null || controller == null || screen == null) return bound;

        var screenType = screen.GetType();

        foreach (var field in SerializedFields(controller.GetType()))
        {
            var elementType = field.FieldType.IsArray ? field.FieldType.GetElementType() : field.FieldType;
            if (!IsBindable(elementType)) continue;
            if (AlreadyAssigned(field.GetValue(controller))) continue;

            var member = screenType.GetField(
                UiBindingNames.ScreenMemberFor(field.Name), BindingFlags.Instance | BindingFlags.Public);
            if (member == null) continue;

            var declared = member.GetValue(screen);

            if (field.FieldType.IsArray)
            {
                var refs = AsRefList(declared);
                if (refs == null || refs.Count == 0) continue;

                var values = Array.CreateInstance(elementType, refs.Count);
                for (int i = 0; i < refs.Count; i++)
                {
                    // A half-built list would serialize as an array with a null
                    // in it, which the sweep reports as the FIELD's fault rather
                    // than the screen's. Leave it entirely alone instead.
                    if (!refs[i].IsValid) { values = null; break; }
                    values.SetValue(Lookup(result, elementType, refs[i]), i);
                }

                if (values == null) continue;
                field.SetValue(controller, values);

                for (int i = 0; i < refs.Count; i++)
                {
                    bound.Add(new UiEmitResult.AutoBound
                    {
                        Controller = controller,
                        FieldName = field.Name,
                        Index = i,
                        Node = refs[i].Node,
                    });
                }
            }
            else
            {
                if (!(declared is NodeRef reference) || !reference.IsValid) continue;
                field.SetValue(controller, Lookup(result, elementType, reference));

                bound.Add(new UiEmitResult.AutoBound
                {
                    Controller = controller,
                    FieldName = field.Name,
                    Index = -1,
                    Node = reference.Node,
                });
            }
        }

        result.AutoBoundFields.AddRange(bound);
        return bound;
    }

    // Every [SerializeField] instance field the controller has, its base
    // classes included. GetFields without DeclaredOnly skips a base class's
    // PRIVATE fields, which serialize perfectly well and would silently fall
    // outside the binder -- so the hierarchy is walked a level at a time.
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

    // Exact type equality, not assignability. A field declared as a base type
    // (Graphic, Selectable, Transform) is asking for something this cannot
    // promise to have picked correctly, so it gets nothing and the sweep asks
    // for it by name.
    private static bool IsBindable(Type type) =>
        type == typeof(TMP_Text) || type == typeof(Image) || type == typeof(Button)
        || type == typeof(GameObject) || type == typeof(RectTransform);

    private static UnityEngine.Object Lookup(UiEmitResult result, Type type, NodeRef reference)
    {
        if (type == typeof(TMP_Text)) return result.Tmp(reference);
        if (type == typeof(Image)) return result.Image(reference);
        if (type == typeof(Button)) return result.Button(reference);
        if (type == typeof(RectTransform)) return result.Rect(reference);
        return result.Go(reference);
    }

    private static IReadOnlyList<NodeRef> AsRefList(object declared)
    {
        if (declared is List<NodeRef> list) return list;
        if (declared is NodeRef[] array) return array;
        return null;
    }

    // Unity's own null: a field pointing at a destroyed object must count as
    // unassigned, and == on UnityEngine.Object is what knows that.
    private static bool AlreadyAssigned(object value)
    {
        if (value is Array array) return array.Length > 0;
        return value is UnityEngine.Object reference && reference != null;
    }
}
