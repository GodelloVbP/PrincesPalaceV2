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

        // What an orb costs when nothing else says. The real figure per slot
        // comes from the content, through TalentSlot.Cost, and it is not flat:
        // ContentDatabase prices a chain orb by its strand tier (1-3 up the
        // grid, 2-4 up the branch) and makes the root, the convergence and the
        // capstone FREE -- reaching those is the price. A single constant
        // could express none of that.
        //
        // Kept because a caller with no tree still needs a sane figure, and
        // because it reads better than a bare 1 at the call site.
        public const int EmberCost = 1;

        // Why an orb cannot be taken. Ordered by which the player should be
        // told about FIRST -- an orb they cannot reach is not "too expensive",
        // it is unreachable, and saying the wrong one sends them to grind
        // Embers they did not need.
        public enum Refusal
        {
            None,

            // Nothing is authored in this slot. FIRST, because it outranks
            // every other answer: an orb with no talent behind it is not
            // "unreachable" or "too expensive", it is not a thing. The sheep's
            // third path is entirely unauthored, so this is a real state and
            // not a defensive branch.
            NotAuthored,

            AlreadyTaken,
            PrerequisiteMissing,
            NotEnoughEmbers,
        }

        // THE IDS COME FROM THE TREE, and that is the whole of this file's
        // correction. This used to mint its own -- "sheep.p0.s0" -- and write
        // them into the save, where nothing matched them: every consumer of
        // unlockedTalentIds looks for the ids in talents.json. See TalentTree.
        public static Refusal Evaluate(
            TalentTree tree, int path, int slot,
            IReadOnlyCollection<string> unlocked, int embers)
        {
            if (slot < 0 || slot >= TalentSkeleton.SlotCount) return Refusal.NotAuthored;

            var here = (tree ?? TalentTree.None).At(path, slot);
            if (!here.Exists) return Refusal.NotAuthored;

            if (unlocked != null && unlocked.Contains(here.Id)) return Refusal.AlreadyTaken;

            // PREREQUISITES BEFORE COST, deliberately. Told "not enough Embers"
            // for an orb three tiers above anything they own, a player goes and
            // farms -- and comes back to the same refusal.
            //
            // Walked through the SKELETON rather than the content's own
            // prerequisite lists, and the two are interchangeable by
            // measurement rather than by assumption: talents.json declares its
            // prerequisites explicitly and they match TalentSkeleton.Parents
            // for all 294 authored talents. The skeleton is the cheaper of the
            // two to walk and the one this layer can see.
            foreach (int parent in TalentSkeleton.Parents[slot])
            {
                string parentId = tree.IdAt(path, parent);

                // A prerequisite the content never authored cannot be met, so
                // everything above it is unreachable rather than free.
                if (string.IsNullOrEmpty(parentId)) return Refusal.PrerequisiteMissing;
                if (unlocked == null || !unlocked.Contains(parentId)) return Refusal.PrerequisiteMissing;
            }

            if (embers < here.Cost) return Refusal.NotEnoughEmbers;

            return Refusal.None;
        }

        public static bool CanInvest(
            TalentTree tree, int path, int slot,
            IReadOnlyCollection<string> unlocked, int embers) =>
            Evaluate(tree, path, slot, unlocked, embers) == Refusal.None;

        // A root is any slot with no parents -- the entry to a path, always
        // reachable. Derived rather than listed so a skeleton change cannot
        // leave a hardcoded root behind.
        public static bool IsRoot(int slot) =>
            slot >= 0 && slot < TalentSkeleton.SlotCount && TalentSkeleton.Parents[slot].Length == 0;

        // Every slot that is one step away from being taken. This is what the
        // screen lights up: the frontier, rather than the whole tree.
        public static IReadOnlyList<int> Frontier(
            TalentTree tree, int path, IReadOnlyCollection<string> unlocked)
        {
            var frontier = new List<int>();

            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                // Embers deliberately ignored: the frontier is about SHAPE, not
                // about whether the player can afford it today. An orb that
                // greys out when the wallet empties would make the tree appear
                // to change shape as gold is spent elsewhere.
                //
                // int.MaxValue rather than EmberCost, now that a slot can cost
                // more than one: a capstone would otherwise drop out of the
                // frontier for being expensive, which is exactly the wallet
                // leaking into the shape this guards against.
                var refusal = Evaluate(tree, path, slot, unlocked, int.MaxValue);
                if (refusal == Refusal.None) frontier.Add(slot);
            }

            return frontier;
        }

        // COUNTED AGAINST THE TREE, not by matching a prefix on the id.
        //
        // The prefix worked only while this file minted the ids itself; real
        // content ids carry no path in them ("sheep_ram_root" says nothing
        // about being on path 0), so the only honest way to ask how much has
        // been spent on a path is to walk that path's slots.
        public static int SpentOn(TalentTree tree, int path, IReadOnlyCollection<string> unlocked)
        {
            if (unlocked == null || tree == null) return 0;

            int spent = 0;
            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                var here = tree.At(path, slot);
                if (here.Exists && unlocked.Contains(here.Id)) spent += here.Cost;
            }

            return spent;
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
