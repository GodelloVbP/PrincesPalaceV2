using System;

namespace PrincesPalace.Domain.Content
{
    // One validated row of the level cost table -- and the shape
    // LevelCurveDefinition STORES rather than restates.
    //
    // [Serializable] class with public fields, for the reason ResolvedSkill
    // records. System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedLevelCost
    {
        // The level this row is the price of entering.
        public int Level;

        // What entering it costs, in experience.
        public int Cost;

        // For the serializer only.
        public ResolvedLevelCost()
        {
        }

        public ResolvedLevelCost(int level, int cost)
        {
            Level = level;
            Cost = cost;
        }
    }
}
