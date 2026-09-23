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

            // Another path's ENGINE is already sworn. Slot 0 of each path is
            // that path's wool generation rule and exactly one may ever be lit
            // across all three -- see Evaluate for where the rule lived before
            // this, and ContentDatabase.AllegianceRootOf for why the design
            // cannot survive two of them.
            //
            // DIRECTLY UNDER AlreadyTaken, because it is the same KIND of
            // answer: not "you cannot afford this yet" but "you have already
            // decided this". A player told the price of a stone their own
            // earlier choice has closed goes and farms for nothing.
            AllegianceSworn,

            PrerequisiteMissing,

            // The path has not been spent up to this slot's gate. Only the
            // convergence and the capstone carry one -- 9 and 20 -- and they
            // are also the only two orbs that cost nothing, so the gate is
            // their entire price. Reaching them IS the payment.
            //
            // ABOVE PrerequisiteMissing IS WRONG AND BELOW IT IS RIGHT: a
            // player who cannot reach the convergence yet should be told what
            // is missing beneath it, not handed a number they will meet on the
            // way there anyway.
            Gated,

            // The character's LIFETIME BUDGET is spent. Above
            // NotEnoughEmbers because it is the more permanent of the two
            // money answers and outranks it in the same way Gated outranks a
            // price: a wallet fills again, a budget never does. Told "not
            // enough Embers" at the cap, a player goes and earns thirty more
            // and comes back to a tree that still refuses them.
            //
            // BELOW Gated, for the mirror reason -- reachability is what a
            // player can still act on, and a stone they cannot reach is not
            // yet a question about money at all.
            BudgetSpent,

            NotEnoughEmbers,
        }

        // THE IDS COME FROM THE TREE, and that is the whole of this file's
        // correction. This used to mint its own -- "sheep.p0.s0" -- and write
        // them into the save, where nothing matched them: every consumer of
        // unlockedTalentIds looks for the ids in talents.json. See TalentTree.
        //
        // `embers` is the WALLET -- what this character holds right now.
        // `budget` is what they may still COMMIT before their lifetime cap,
        // which is a different quantity and the reason the two are separate
        // parameters rather than one number the caller pre-combines: the player
        // is told a different thing by each, and a caller that folded them
        // together would have to pick one of the two sentences at the call
        // site, where the rules do not live. ContentDatabase.EmbersLeftFor is
        // the Core half that answers the second (it floors at 0, so a save from
        // before the cap existed is over budget rather than negative).
        public static Refusal Evaluate(
            TalentTree tree, int path, int slot,
            IReadOnlyCollection<string> unlocked, int embers, int budget)
        {
            if (slot < 0 || slot >= TalentSkeleton.SlotCount) return Refusal.NotAuthored;

            var here = (tree ?? TalentTree.None).At(path, slot);
            if (!here.Exists) return Refusal.NotAuthored;

            if (unlocked != null && unlocked.Contains(here.Id)) return Refusal.AlreadyTaken;

            // THE ALLEGIANCE, WHICH NOTHING WAS CHECKING.
            //
            // Same story as the gate below, one rule further along.
            // ContentDatabase.AllegianceRootOf and IsBlockedByAllegiance have
            // held this since the content layer was written and were folded
            // into MeetsGates, which has never had a caller. TalentPage
            // replaced the screen's colouring pass and carried across five
            // refusals, not this one -- so both of Shawn's roots could be lit,
            // for nothing, and TalentEffects sums every unlocked talent with no
            // allegiance filter. Two wool engines at once against abilities
            // priced at 7-8 is the economy the handoff calls load-bearing.
            //
            // IN DOMAIN RATHER THAN IN CONTENT, because the rule needs to hold
            // for the screen, the bot and Frontier alike, and only this layer
            // is beneath all three. Everything it needs is in hand: the tree
            // carries all three paths, and a root is a slot the skeleton gives
            // no parents.
            //
            // ONLY THE ROOT, and only somebody ELSE'S root: the engine is
            // exclusive, the tree is not. A player sworn on path 0 may still
            // climb path 1 for its nodes -- they simply never get its
            // generation rule.
            if (IsRoot(slot) && unlocked != null)
            {
                for (int other = 0; other < PathCount; other++)
                {
                    if (other == path) continue;

                    string rootId = tree.IdAt(other, slot);
                    if (!string.IsNullOrEmpty(rootId) && unlocked.Contains(rootId))
                    {
                        return Refusal.AllegianceSworn;
                    }
                }
            }

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
            if (!MeetsPrerequisites(tree, path, slot, unlocked)) return Refusal.PrerequisiteMissing;

            // THE GATE, WHICH NOTHING WAS CHECKING.
            //
            // ContentDatabase.MinSpentMet has held this rule since the content
            // layer was written, and its comment names four callers that agree
            // on it -- "the talent screen's colouring pass, its click handler
            // and its tests". It has none. TalentPage replaced that pass and
            // did not carry the gate across, which left the two orbs whose cost
            // is zero costing nothing at all the moment their parents lit.
            //
            // In practice the parent chain nearly always pays it: a merge needs
            // all three tiers beneath it, which is ten orbs by the time the
            // 9-gate applies. That near-redundancy is why it went unnoticed,
            // and is also why turning it on is safe.
            if (here.MinSpent > 0 && SpentOn(tree, path, unlocked) < here.MinSpent)
            {
                return Refusal.Gated;
            }

            // THE CAP, WHICH NOTHING ON THE PLAYER'S PATH WAS CHECKING.
            //
            // ContentDatabase.EmberSpendCap is 30 and its header spends a
            // paragraph on what that number buys -- the deep, wide and fused
            // archetypes the ember economy is measured on. Every enforcement of
            // it lived in the balance bot's preset builder, which clamped what
            // it GRANTED so the question was never asked; the screen asked the
            // wallet, which is shared, uncapped, and accumulates across runs.
            //
            // BEFORE THE WALLET, and the same test as the wallet uses: cost
            // against what is left. A free stone stays free at the cap, exactly
            // as ContentDatabase.MeetsGates already said ("OrbCost(talent) <=
            // EmbersLeftFor(character)") -- the convergence and the capstone
            // were paid for with a gate, not with embers, and a spent budget
            // does not take back a gate already met.
            if (budget < here.Cost) return Refusal.BudgetSpent;

            if (embers < here.Cost) return Refusal.NotEnoughEmbers;

            return Refusal.None;
        }

        private static bool MeetsPrerequisites(
            TalentTree tree, int path, int slot, IReadOnlyCollection<string> unlocked)
        {
            int[] parents = TalentSkeleton.Parents[slot];
            if (parents.Length == 0) return true;
            if (unlocked == null) return false;

            // THE FIRST CONVERGENCE IS A TWO-STRAND FUSION. One strand must
            // actually reach the merge, while a second only has to be lit;
            // the authored 9-Ember gate below supplies the rest of the price.
            // At shipped costs that is exactly 6 on one completed strand plus
            // 1+2 on a second. Requiring every direct parent made it 18 and
            // consumed so much of the 30-Ember lifetime cap that the ultimate
            // could not be reached.
            if (TalentSkeleton.Kind[slot] == "merge" && parents.Length > 1)
            {
                int completed = 0;
                int touched = 0;
                foreach (int parent in parents)
                {
                    string parentId = tree.IdAt(path, parent);
                    if (!string.IsNullOrEmpty(parentId) && unlocked.Contains(parentId)) completed++;

                    int dx = TalentSkeleton.DxSlot[parent];
                    bool strandTouched = false;
                    for (int candidate = 0; candidate < slot; candidate++)
                    {
                        if (TalentSkeleton.Depth[candidate] <= 0 || TalentSkeleton.DxSlot[candidate] != dx) continue;
                        string id = tree.IdAt(path, candidate);
                        if (!string.IsNullOrEmpty(id) && unlocked.Contains(id))
                        {
                            strandTouched = true;
                            break;
                        }
                    }
                    if (strandTouched) touched++;
                }

                return completed >= 1 && touched >= 2;
            }

            // A capstone is the culmination of ONE post-convergence branch.
            // Its authored 20-spent gate remains the price; forcing all three
            // direct parents made it exceed the lifetime budget.
            if (TalentSkeleton.Kind[slot] == "cap" && parents.Length > 1)
            {
                foreach (int parent in parents)
                {
                    string id = tree.IdAt(path, parent);
                    if (!string.IsNullOrEmpty(id) && unlocked.Contains(id)) return true;
                }
                return false;
            }

            foreach (int parent in parents)
            {
                // THE ROOT IS A SHARED GATEWAY, NOT A PER-PATH GATE. A
                // tier-1 stone's one parent is always that path's own root
                // (TalentSkeleton: "triple above a single, each of the
                // three hangs off that single"), and checking THAT
                // specific root's id cannot ever pass once a different
                // root is sworn -- AllegianceSworn refuses the unsworn
                // one forever, which made every stone above it
                // permanently unreachable, whatever points were spent
                // (owner playtest, 2026-09-23: "after Fragile Lamb every
                // stone beyond Black Ram is unreachable"). "The engine is
                // exclusive, the tree is not" (AllegianceSworn's own
                // header) means the oath itself -- ANY root, not
                // specifically this parent's -- is what a tier-1 stone
                // actually needs. No special case for the refused root
                // versus an untaken one: every root parent is checked the
                // same way, and it is still correct for the SWORN path's
                // own tier-1 stones, since their own root is one of the
                // roots "any" covers.
                if (IsRoot(parent))
                {
                    if (!AnyRootSworn(tree, unlocked)) return false;
                    continue;
                }

                string id = tree.IdAt(path, parent);
                if (string.IsNullOrEmpty(id) || !unlocked.Contains(id)) return false;
            }
            return true;
        }

        // Whether the player has sworn ANY of the three roots -- the one
        // oath MeetsPrerequisites' root case above treats as opening every
        // constellation's tier-1 stones, not only the sworn path's own.
        private static bool AnyRootSworn(TalentTree tree, IReadOnlyCollection<string> unlocked)
        {
            for (int p = 0; p < PathCount; p++)
            {
                string rootId = tree.IdAt(p, 0);
                if (!string.IsNullOrEmpty(rootId) && unlocked.Contains(rootId)) return true;
            }
            return false;
        }

        public static bool CanInvest(
            TalentTree tree, int path, int slot,
            IReadOnlyCollection<string> unlocked, int embers, int budget) =>
            Evaluate(tree, path, slot, unlocked, embers, budget) == Refusal.None;

        // A root is any slot with no parents -- the entry to a path, always
        // reachable. Derived rather than listed so a skeleton change cannot
        // leave a hardcoded root behind.
        public static bool IsRoot(int slot) =>
            slot >= 0 && slot < TalentSkeleton.SlotCount && TalentSkeleton.Parents[slot].Length == 0;

        // WHETHER ONE EDGE -- from parentSlot to childSlot, on ONE path --
        // should draw lit. BOTH endpoints must be invested on THAT path: the
        // child alone is not enough, because a merge or capstone slot
        // (TalentPage.MeetsPrerequisites) authors more than one incoming edge
        // for the same child and only requires ONE of them met, so a
        // child-only test lights every strand into it, including the one the
        // player never climbed (owner's hardware playtest, 2026-09-23 --
        // "the unchosen path still lights up with an energy beam to a
        // kindled star").
        //
        // IN DOMAIN, not the screen: the rule is "are both ends of THIS
        // specific edge unlocked", which needs nothing Core has that Domain
        // does not, and stating it here is what let TalentEdgeLightingTests
        // pin it without a scene.
        public static bool EdgeIsLit(
            TalentTree tree, int path, int parentSlot, int childSlot, IReadOnlyCollection<string> unlocked)
        {
            if (tree == null || unlocked == null) return false;

            string childId = tree.IdAt(path, childSlot);
            if (string.IsNullOrEmpty(childId) || !unlocked.Contains(childId)) return false;

            string parentId = tree.IdAt(path, parentSlot);
            return !string.IsNullOrEmpty(parentId) && unlocked.Contains(parentId);
        }

        // Every slot that is one step away from being taken. This is what the
        // screen lights up: the frontier, rather than the whole tree.
        // TAKES THE BUDGET THOUGH IT IGNORES THE WALLET, and the asymmetry is
        // the point rather than an oversight. The wallet is a state of today --
        // spend gold elsewhere and it changes, and a frontier that moved with
        // it would make the tree appear to change shape. The lifetime cap never
        // refills: past it there is no orb to reach, ever, which is a fact
        // about the shape of what this character can own.
        public static IReadOnlyList<int> Frontier(
            TalentTree tree, int path, IReadOnlyCollection<string> unlocked, int budget)
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
                var refusal = Evaluate(tree, path, slot, unlocked, int.MaxValue, budget);
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
