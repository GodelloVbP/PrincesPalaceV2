using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // An achievement: a permanent thing the profile has done, and the gate a
    // relic can hang behind.
    //
    // Authored in Assets/_Project/ContentData/achievements.json and generated
    // by ContentBuilder -- never hand-edited under Resources/Content, which
    // ContentBuilder deletes wholesale on every build.
    public class AchievementDefinition : ScriptableObject, IOrderedContent
    {
        [Tooltip("Stable identifier written into save files the moment this is earned. NEVER rename after a save exists.")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        [Tooltip("How it is earned. Every value has a case in AchievementProgress.")]
        public AchievementCondition condition;

        [Tooltip("What the condition compares against — a level, a room count, a depth. Meaning depends on the condition.")]
        public int threshold;

        [Tooltip("The condition's subject when it has one. Today only DefeatSpecificBoss uses it, naming a boss enemy id.")]
        public string parameter;

        // Resources.LoadAll returns filename order, not authoring order.
        public int sortOrder;

        public ResolvedAchievement ToResolved() =>
            new ResolvedAchievement(id, displayName, description, condition, threshold, parameter, sortOrder);

        // Listed by the authored order ContentBuilder stamped on it.
        public int SortOrder => sortOrder;
    }
}
