using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

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
                        if (element.propertyType == SerializedPropertyType.ObjectReference
                            && element.objectReferenceValue == null)
                        {
                            problems.Add($"  {controller.GetType().Name}.{property.propertyPath}[{i}] is null");
                        }
                    }

                    if (property.arraySize == 0)
                    {
                        problems.Add($"  {controller.GetType().Name}.{property.propertyPath} is an empty array");
                    }
                }
            }
        }

        if (problems.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"[SceneBuilder] FAILED: '{screenName}' has {problems.Count} unwired reference(s):");
        foreach (var p in problems) sb.AppendLine(p);
        sb.AppendLine("  Fix by: assigning it in this screen's Wire step; or, if it is genuinely optional, ");
        sb.AppendLine("  dropping the [SerializeField] so nothing claims it will be there.");
        throw new System.Exception(sb.ToString());
    }
}
