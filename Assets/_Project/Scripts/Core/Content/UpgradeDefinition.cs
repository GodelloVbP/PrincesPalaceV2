using UnityEngine;

namespace PrincesPalace.Content
{
    // A permanent, account-wide Divine Principality upgrade. Deliberately
    // separate from talents: talents are per-character build power, these are
    // profile-wide, so the two never compete for the same currency.
    public class UpgradeDefinition : ScriptableObject
    {
        [Tooltip("Stable identifier written into save files. Never rename this after a save exists.")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        [Tooltip("Cost in Principality currency.")]
        public int cost = 50;

        [Tooltip("Lower numbers appear first in the shop.")]
        public int sortOrder;

        [Tooltip("In-run currency granted at the start of every run, if this upgrade grants that (0 for upgrades that don't).")]
        public int startingGoldBonus;
    }
}
