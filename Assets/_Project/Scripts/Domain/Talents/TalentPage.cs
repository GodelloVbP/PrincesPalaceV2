using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Talents
{
    // What the player can actually press on a constellation, and why not.
    //
    // Engine-free and content-free: it is handed the shape of the tree, the set
    // already unlocked, and how many Embers are in hand, and answers questions
    // about them. That is what makes every rule below testable in milliseconds
    // instead of by clicking orbs in a scene.
    public static class TalentPage
    {
        // Which page a slot belongs to. The skeleton describes ONE path; a
        // character has three, laid out identically and paged between, so the
        // page index and the slot index are separate coordinates.
        public const int PathCount = 3;

        // What an orb costs. Flat rather than scaling with depth: a deep orb is
        // already expensive in the prerequisites it demands, and charging twice
        // for the same distance is how a tree stops being explorable.
        public const int EmberCost = 1;

        // Why an orb cannot be taken. Ordered by which the player should be
        // told about FIRST -- an orb they cannot reach is not "too expensive",
        // it is unreachable, and saying the wrong one sends them to grind
        // Embers they did not need.
        public enum Refusal
        {
            None,
            AlreadyTaken,
            PrerequisiteMissing,
            NotEnoughEmbers,
        }

        // A stable id for one orb on one path. The skeleton is shared by all
        // three, so a slot number alone names three different orbs.
        public static string SlotId(string characterId, int path, int slot) =>
            $"{characterId}.p{path}.s{slot}";

        public static Refusal Evaluate(
            string characterId, int path, int slot,
            IReadOnlyCollection<string> unlocked, int embers)
        {
            if (slot < 0 || slot >= TalentSkeleton.SlotCount) return Refusal.PrerequisiteMissing;

            string id = SlotId(characterId, path, slot);
            if (unlocked != null && unlocked.Contains(id)) return Refusal.AlreadyTaken;

            // PREREQUISITES BEFORE COST, deliberately. Told "not enough Embers"
            // for an orb three tiers above anything they own, a player goes and
            // farms -- and comes back to the same refusal.
            foreach (int parent in TalentSkeleton.Parents[slot])
            {
                string parentId = SlotId(characterId, path, parent);
                if (unlocked == null || !unlocked.Contains(parentId)) return Refusal.PrerequisiteMissing;
            }

            if (embers < EmberCost) return Refusal.NotEnoughEmbers;

            return Refusal.None;
        }

        public static bool CanInvest(
            string characterId, int path, int slot,
            IReadOnlyCollection<string> unlocked, int embers) =>
            Evaluate(characterId, path, slot, unlocked, embers) == Refusal.None;

        // A root is any slot with no parents -- the entry to a path, always
        // reachable. Derived rather than listed so a skeleton change cannot
        // leave a hardcoded root behind.
        public static bool IsRoot(int slot) =>
            slot >= 0 && slot < TalentSkeleton.SlotCount && TalentSkeleton.Parents[slot].Length == 0;

        // Every slot that is one step away from being taken. This is what the
        // screen lights up: the frontier, rather than the whole tree.
        public static IReadOnlyList<int> Frontier(
            string characterId, int path, IReadOnlyCollection<string> unlocked)
        {
            var frontier = new List<int>();

            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                // Embers deliberately ignored: the frontier is about SHAPE, not
                // about whether the player can afford it today. An orb that
                // greys out when the wallet empties would make the tree appear
                // to change shape as gold is spent elsewhere.
                var refusal = Evaluate(characterId, path, slot, unlocked, EmberCost);
                if (refusal == Refusal.None) frontier.Add(slot);
            }

            return frontier;
        }

        public static int SpentOn(string characterId, int path, IReadOnlyCollection<string> unlocked)
        {
            if (unlocked == null) return 0;

            string prefix = $"{characterId}.p{path}.s";
            return unlocked.Count(id => id.StartsWith(prefix));
        }

        // How deep the constellation runs, for the layout to space rows by.
        public static int DepthCount
        {
            get
            {
                int deepest = 0;
                for (int i = 0; i < TalentSkeleton.SlotCount; i++)
                {
                    if (TalentSkeleton.Depth[i] > deepest) deepest = TalentSkeleton.Depth[i];
                }

                return deepest + 1;
            }
        }

        // The widest lateral offset any slot uses, so StarX can normalise
        // against it rather than against a hardcoded half-width.
        public static int MaxAbsDx
        {
            get
            {
                int widest = 0;
                for (int i = 0; i < TalentSkeleton.SlotCount; i++)
                {
                    int dx = TalentSkeleton.DxSlot[i];
                    if (dx < 0) dx = -dx;
                    if (dx > widest) widest = dx;
                }

                return widest;
            }
        }
    }
}
