using UnityEditor;
using UnityEngine;

namespace PrincesPalace.Editor.Rigging
{
    // Ensures the "StageActors" sorting layer exists, so a rig's SpriteRenderers
    // can be sandwiched between the fight backdrop and the HUD's nested canvases
    // (Spike B confirmed this sandwich works via sortingOrder alone, on this
    // project's exact ScreenSpaceCamera + Renderer2D setup).
    //
    // Written via SerializedObject on ProjectSettings/TagManager.asset, never by
    // hand -- the same rule every other generated asset in this project follows.
    // Idempotent: a second run finds the layer already there and does nothing,
    // so this is safe to call on every scene build.
    public static class StageActorsSortingLayer
    {
        public const string LayerName = "StageActors";

        // Arbitrary but fixed and non-zero (Default's own uniqueID is 0) --
        // Unity's sorting layer uniqueID only has to be unique within this
        // project's TagManager, and this project has exactly one other layer.
        private const int UniqueId = 1958234871;

        public static void Ensure()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_SortingLayers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                var name = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (name == LayerName) return; // already present
            }

            int insertAt = layers.arraySize;
            layers.InsertArrayElementAtIndex(insertAt);
            var entry = layers.GetArrayElementAtIndex(insertAt);
            entry.FindPropertyRelative("name").stringValue = LayerName;
            entry.FindPropertyRelative("uniqueID").intValue = UniqueId;
            entry.FindPropertyRelative("locked").boolValue = false;

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"StageActorsSortingLayer: added '{LayerName}' sorting layer");
        }
    }
}
