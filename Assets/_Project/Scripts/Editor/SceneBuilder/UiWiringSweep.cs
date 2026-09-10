using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using PrincesPalace;

// E3: every [SerializeField] reference on every attached controller is non-null.
//
// Reads the SERIALIZED view, not the C# field, and that distinction is the
// point. A direct field assignment can be perfectly correct in memory and still
// fail to serialize -- and what ships is what serialized. Checking the object
// in memory would pass in exactly the case that breaks.
//
// This is the backstop for Contract B. Direct assignment already turns a typo
// into a compile error; this catches the other half: a field nobody assigned at
// all, which in v1 produced a NullReferenceException somewhere else entirely,
// naming the symptom rather than the cause.
public static class UiWiringSweep
{
    public static void Run(string screenName, UiEmitResult result)
    {
        var problems = new List<string>();

        foreach (var controller in result.AttachedControllers)
        {
            if (controller == null) continue;

            var so = new SerializedObject(controller);
            var property = so.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script") continue;

                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (property.objectReferenceValue == null)
                    {
                        problems.Add($"  {controller.GetType().Name}.{property.propertyPath} is null");
                    }
                }
                else if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
                {
                    for (int i = 0; i < property.arraySize; i++)
                    {
                        var element = property.GetArrayElementAtIndex(i);

                        if (element.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (element.objectReferenceValue == null)
                            {
                                problems.Add($"  {controller.GetType().Name}.{property.propertyPath}[{i}] is null");
                            }
                            continue;
                        }

                        // A SERIALIZABLE STRUCT ELEMENT, whose own object
                        // references this would otherwise walk straight past.
                        //
                        // Added with IconEntry, which collapsed five pairs of
                        // parallel `string[] ids` / `Sprite[] sprites` fields
                        // into one array. Without this, `icons[i].Sprite` being
                        // null -- which LoadSpriteByKey returns on a miss, by
                        // design -- would have stopped being a build failure and
                        // started being a white quad on somebody's screen. The
                        // check the pair had was worth keeping through the
                        // change that removed the pair.
                        foreach (var member in Members(element))
                        {
                            if (member.propertyType == SerializedPropertyType.ObjectReference
                                && member.objectReferenceValue == null)
                            {
                                problems.Add(
                                    $"  {controller.GetType().Name}.{property.propertyPath}[{i}]." +
                                    $"{member.name} is null");
                            }
                        }
                    }

                    // Empty is a failure UNLESS the field says otherwise. Null
                    // elements above are checked regardless, so a [UiOptional]
                    // array may be empty but never half-wired.
                    if (property.arraySize == 0 && !IsOptional(controller, property.propertyPath))
                    {
                        problems.Add($"  {controller.GetType().Name}.{property.propertyPath} is an empty array");
                    }
                }
                else if (property.propertyType == SerializedPropertyType.Generic)
                {
                    // A SINGULAR (non-array) serializable struct/class field.
                    // enterChildren is false for every NextVisible call past
                    // the first, so without this the outer walk reports
                    // "Generic, not an array" and moves straight to the next
                    // sibling -- never inspecting this field's own object
                    // references. Mirrors the array-element case above
                    // (Members()), for the day a type like IconEntry is used
                    // as a single field instead of only ever IconEntry[].
                    // Nothing in this project does that today, so this path
                    // is currently unreached by any real controller -- it
                    // exists so the NEXT one that does it doesn't silently
                    // lose E3 coverage the way the array case did before
                    // Members() was added.
                    foreach (var member in Members(property))
                    {
                        if (member.propertyType == SerializedPropertyType.ObjectReference
                            && member.objectReferenceValue == null)
                        {
                            problems.Add(
                                $"  {controller.GetType().Name}.{property.propertyPath}." +
                                $"{member.name} is null");
                        }
                    }
                }
            }
        }

        if (problems.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"[SceneBuilder] FAILED: '{screenName}' has {problems.Count} unwired reference(s):");
        foreach (var p in problems) sb.AppendLine(p);
        sb.AppendLine("  Fix by: assigning it in this screen's Wire step; or, if it is optional, ");
        sb.AppendLine("  dropping the [SerializeField] so nothing claims it will be there.");
        throw new System.Exception(sb.ToString());
    }

    // The immediate members of one struct element, and no deeper.
    //
    // SerializedProperty.Next(enterChildren) walks the whole remaining object
    // in one flat sequence, so the only way to stop at the end of this element
    // is to compare against where it ends -- which is what GetEndProperty is
    // for. Copy() because Next advances the property it is called on, and the
    // caller is still using theirs.
    private static IEnumerable<SerializedProperty> Members(SerializedProperty element)
    {
        var end = element.GetEndProperty();
        var walker = element.Copy();

        bool enterChildren = true;
        while (walker.NextVisible(enterChildren) && !SerializedProperty.EqualContents(walker, end))
        {
            enterChildren = false;
            yield return walker.Copy();
        }
    }

    // propertyPath is the field name for a top-level field, which is the only
    // shape this sweep walks.
    private static bool IsOptional(Component controller, string fieldName)
    {
        var field = controller.GetType().GetField(
            fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return field != null && field.IsDefined(typeof(UiOptionalAttribute), inherit: true);
    }
}
