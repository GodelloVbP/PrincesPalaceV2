using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // A relic: a run-long combat effect, assigned to exactly one character
    // at a time. Authored in Assets/_Project/ContentData/relics.json and
    // generated into an asset by ContentBuilder — never hand-edited under
    // Resources/Content, which ContentBuilder deletes wholesale on every
    // build.
    public class RelicDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier written into save files. Never rename this after a save exists.")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        [Tooltip("What this relic actually does — one case in FightController per value.")]
        public RelicEffect effect;

        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order — the same trap Characters/Enemies/Talents/
        // Upgrades already guard against with an explicit sortOrder.
        public int sortOrder;

        [Tooltip("Editor-time path under Assets/_Project/Art/Items/Relics/Processed/, loaded by SceneBuilder. Empty until the art lands.")]
        public string iconPath;
    }
}
