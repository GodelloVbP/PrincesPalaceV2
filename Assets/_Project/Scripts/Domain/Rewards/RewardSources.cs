using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // Which rewards.json row pays a moment (docs/PLAN_ITEMS.md 1.1): one row
    // per (class, page kind), named <class>_<kind>. A class with no row of a
    // kind (normal_relic) pays no page of it.
    public static class RewardSources
    {
        public static string IdFor(EncounterClass encounter, RewardPageKind kind) =>
            ClassWord(encounter) + (kind == RewardPageKind.Gear ? "_gear" : "_relic");

        // An Elite is the map's anchor; the row names it so.
        private static string ClassWord(EncounterClass encounter)
        {
            switch (encounter)
            {
                case EncounterClass.Elite: return "anchor";
                case EncounterClass.Boss: return "boss";
                default: return "normal";
            }
        }

        // A page's class is the highest among its chips (boss > anchor >
        // normal), 1.2 "Pages".
        public static EncounterClass Highest(System.Collections.Generic.IEnumerable<EncounterClass> chips)
        {
            var best = EncounterClass.Normal;
            if (chips == null) return best;
            foreach (var chip in chips)
            {
                if (chip > best) best = chip;
            }

            return best;
        }
    }
}
