using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.Domain.Tests
{
    // THE GATE, WHICH NOTHING WAS CHECKING.
    //
    // ContentDatabase.MinSpentMet has held this rule since the content layer
    // was written, and its own comment names four callers that agree on it --
    // "the talent screen's colouring pass, its click handler and its tests".
    // It had none. TalentPage replaced that colouring pass and did not carry
    // the gate across, which left the convergence and the capstone -- the only
    // two orbs priced at zero embers -- costing nothing at all the moment their
    // parents lit.
    //
    // It went unnoticed because the parent chain very nearly pays the gate on
    // its own: a merge requires all three tiers beneath it, which is ten orbs
    // by the time the 9-gate applies. Near-redundancy is not redundancy, and
    // the near-miss is the case worth pinning.
    public class TalentGateTests
    {
        private const int Path = 0;

        // TWO PATHS, EACH WITH A ROOT AT SLOT 0 AND NO GATES -- the shape the
        // shipped sheep tree actually has (sheep_ram_root at column 0,
        // sheep_lamb_root at column 1, both row 0, both priced 0 because
        // ContentDatabase.OrbCost makes depth-0 free).
        private static TalentTree TwoRootedPaths()
        {
            var tree = new TalentTree();

            for (int path = 0; path < 2; path++)
            {
                for (int i = 0; i < TalentSkeleton.SlotCount; i++)
                {
                    tree.Set(path, i, new TalentSlot(
                        $"p{path}s{i}", $"Star {path}.{i}", "", i == 0 ? 0 : 1));
                }
            }

            return tree;
        }

        // A tree with a gate on one slot and nothing else in the way. Built by
        // hand rather than from content, because the point is the RULE and a
        // content-shaped fixture would test the authoring instead.
        private static TalentTree TreeWithGate(int slot, int gate, int cost = 0)
        {
            var tree = new TalentTree();

            for (int i = 0; i < TalentSkeleton.SlotCount; i++)
            {
                tree.Set(Path, i, new TalentSlot(
                    $"t{i}", $"Star {i}", "", i == slot ? cost : 3, i == slot ? gate : 0));
            }

            return tree;
        }

        private static TalentTree ShippedPriceShape()
        {
            var tree = new TalentTree();
            for (int i = 0; i < TalentSkeleton.SlotCount; i++)
            {
                int depth = TalentSkeleton.Depth[i];
                int cost = depth == 0 || depth == 4 || depth == 8
                    ? 0
                    : depth <= 3 ? depth : depth - 3;
                int gate = i == 10 ? 9 : i == 20 ? 20 : 0;
                tree.Set(Path, i, new TalentSlot($"t{i}", $"Star {i}", "", cost, gate));
            }
            return tree;
        }

        [Test]
        public void FirstConvergenceUnlocksWithNineSpentAcrossTwoStrands()
        {
            var tree = ShippedPriceShape();
            var owned = new HashSet<string>
            {
                // One completed strand: 1 + 2 + 3 = 6.
                "t0", "t1", "t4", "t7",
                // Two tiers lit on a second strand: 1 + 2 = 3.
                "t2", "t5",
            };

            Assert.AreEqual(9, TalentPage.SpentOn(tree, Path, owned));
            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, Path, 10, owned, embers: 0, budget: 21));
        }

        [Test]
        public void FirstConvergenceStillNeedsOneCompletedStrandAndTwoDistinctStrands()
        {
            var tree = ShippedPriceShape();

            var oneStrandOnly = new HashSet<string> { "t0", "t1", "t4", "t7" };
            Assert.AreEqual(TalentPage.Refusal.PrerequisiteMissing,
                TalentPage.Evaluate(tree, Path, 10, oneStrandOnly, embers: 99, budget: 24));

            var twoIncomplete = new HashSet<string> { "t0", "t1", "t4", "t2", "t5", "t3", "t6" };
            Assert.AreEqual(TalentPage.Refusal.PrerequisiteMissing,
                TalentPage.Evaluate(tree, Path, 10, twoIncomplete, embers: 99, budget: 18));
        }

        [Test]
        public void CapstoneIsReachableAtTwentySpentThroughOneCompletedBranch()
        {
            var tree = ShippedPriceShape();
            var owned = new HashSet<string>
            {
                // First convergence at 9 spent.
                "t0", "t1", "t4", "t7", "t2", "t5", "t10",
                // One complete post-convergence branch: +2 +3 +4 = 18.
                "t11", "t14", "t17",
                // Two more spent elsewhere reaches the authored 20 gate.
                "t12",
            };

            Assert.AreEqual(20, TalentPage.SpentOn(tree, Path, owned));
            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, Path, 20, owned, embers: 0, budget: 10));
        }

        // Every parent of `slot`, and their parents, all the way down.
        private static HashSet<string> Ancestry(TalentTree tree, int slot)
        {
            var owned = new HashSet<string>();
            var pending = new Stack<int>();
            pending.Push(slot);

            while (pending.Count > 0)
            {
                foreach (int parent in TalentSkeleton.Parents[pending.Pop()])
                {
                    if (owned.Add(tree.IdAt(Path, parent))) pending.Push(parent);
                }
            }

            return owned;
        }

        [Test]
        public void APathShortOfItsGateIsRefused()
        {
            // The convergence: everything beneath it lit, and the gate set one
            // ember above what that ancestry is worth.
            var tree = TreeWithGate(TalentSkeleton.SlotCount - 1, gate: 9999);
            var owned = Ancestry(tree, TalentSkeleton.SlotCount - 1);

            var refusal = TalentPage.Evaluate(
                tree, Path, TalentSkeleton.SlotCount - 1, owned, embers: 999, budget: 999);

            Assert.AreEqual(TalentPage.Refusal.Gated, refusal,
                "a slot whose path is short of its gate was offered anyway - this is the rule " +
                "ContentDatabase.MinSpentMet has always described and nothing enforced");
        }

        [Test]
        public void AMetGateStopsRefusing()
        {
            int slot = TalentSkeleton.SlotCount - 1;
            var tree = TreeWithGate(slot, gate: 1);
            var owned = Ancestry(tree, slot);

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, Path, slot, owned, embers: 999, budget: 999),
                "the gate is met and the slot is still refused");
        }

        [Test]
        public void AGateOfZeroIsNoGateAtAll()
        {
            int slot = TalentSkeleton.SlotCount - 1;
            var tree = TreeWithGate(slot, gate: 0);
            var owned = Ancestry(tree, slot);

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, Path, slot, owned, embers: 999, budget: 999),
                "an ungated slot is being gated");
        }

        // ORDER MATTERS, AND THIS IS WHICH WAY.
        //
        // Told "spend further along this path" for a stone whose parent is
        // still dark, a player goes and spends -- and comes back to the same
        // refusal, because what was missing was never the embers. The reachable
        // answer is the one to give.
        [Test]
        public void AMissingParentIsReportedAheadOfTheGate()
        {
            int slot = TalentSkeleton.SlotCount - 1;
            var tree = TreeWithGate(slot, gate: 9999);

            var refusal = TalentPage.Evaluate(
                tree, Path, slot, new HashSet<string>(), embers: 999, budget: 999);

            Assert.AreEqual(TalentPage.Refusal.PrerequisiteMissing, refusal,
                "a gated slot with no parents lit reports the gate, which sends the player to " +
                "spend embers on a stone they still cannot reach");
        }

        // And the other way: a gate outranks affordability, because a stone
        // behind a gate is not expensive, it is shut.
        [Test]
        public void AGateIsReportedAheadOfThePrice()
        {
            int slot = TalentSkeleton.SlotCount - 1;
            var tree = TreeWithGate(slot, gate: 9999, cost: 50);
            var owned = Ancestry(tree, slot);

            Assert.AreEqual(TalentPage.Refusal.Gated,
                TalentPage.Evaluate(tree, Path, slot, owned, embers: 0, budget: 999),
                "a gated stone the player also cannot afford reports the price, which is the " +
                "smaller of the two obstacles");
        }

        // THE ALLEGIANCE, WHICH NOTHING WAS CHECKING EITHER.
        //
        // Slot 0 of each path is that path's wool GENERATION rule, and exactly
        // one may ever be lit across all three -- ContentDatabase.
        // AllegianceRootOf's own header calls it "the load-bearing rule of the
        // whole design", talents.json says it to the player's face ("you may
        // swear to only one path's engine at a time"), and TalentEffect.cs
        // repeats it. The rule was written in Core, folded into
        // ContentDatabase.MeetsGates, and MeetsGates has never had a caller:
        // TalentPage.Evaluate is the only gate on the live click path and it
        // refused five things, none of them this.
        //
        // Both of Shawn's roots cost nothing (depth 0 is free), so the bug was
        // not "an ember was misspent" -- it was two stacking wool engines for
        // free, against abilities priced at 7-8.
        [Test]
        public void ASecondAllegianceRootIsRefusedOnceOneIsSworn()
        {
            var tree = TwoRootedPaths();
            var unlocked = new HashSet<string> { "p0s0" };

            Assert.AreEqual(TalentPage.Refusal.AllegianceSworn,
                TalentPage.Evaluate(tree, path: 1, slot: 0, unlocked, embers: 0, budget: 999),
                "a second path's engine was offered while the first is sworn - three generation " +
                "rules stacking is the case ContentDatabase.AllegianceRootOf says the economy " +
                "cannot survive");
        }

        // AND THE ONE ALREADY SWORN STILL ANSWERS "taken", not "sworn". The
        // refusal a player is shown decides what they do next, and "you have
        // already sworn elsewhere" pointed at the stone they swore ON would be
        // a lie about their own choice.
        [Test]
        public void TheSwornRootItselfStillReadsAsTaken()
        {
            var tree = TwoRootedPaths();
            var unlocked = new HashSet<string> { "p0s0" };

            Assert.AreEqual(TalentPage.Refusal.AlreadyTaken,
                TalentPage.Evaluate(tree, path: 0, slot: 0, unlocked, embers: 0, budget: 999));
        }

        // NOTHING ABOVE A ROOT IS BLOCKED BY THE OATH, which is the half of the
        // rule that makes it liveable: the engine is exclusive, the tree is
        // not. Green before this change as well as after -- it is here because
        // the obvious over-fix is to refuse the whole path, and because every
        // save written before today holds two roots already (that is the bug),
        // so those saves must keep climbing rather than seize up.
        [Test]
        public void OnlyTheROOTOfTheOtherPathIsBlocked()
        {
            var tree = TwoRootedPaths();
            var unlocked = new HashSet<string> { "p0s0", "p1s0" };

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, path: 1, slot: 1, unlocked, embers: 99, budget: 999),
                "a non-root stone on the unsworn path was refused, which locks the whole path " +
                "rather than its engine");
        }

        // OWNER PLAYTEST, 2026-09-23, SECOND PASS: the first fix here
        // (AllegianceSworn scoped to IsRoot) was necessary but not
        // sufficient -- MeetsPrerequisites' generic loop still checked THIS
        // path's own root id for every tier-1 stone, and that id can never
        // be in `unlocked` once a different root is sworn (AllegianceSworn
        // refuses it forever). So every stone above an unsworn path's root
        // was permanently unreachable, whatever points were spent -- the
        // bug was real, the previous test only passed because it PRESET
        // "p0s0" as already unlocked, a state no player sworn to path 1 can
        // ever reach.
        //
        // WALKED FROM AN EMPTY PAGE, no preset prerequisites: swear
        // Fragile Lamb, then invest into path 0 exactly as the screen would
        // -- Evaluate, then add the id to `unlocked` -- one stone at a
        // time, up through the convergence and the capstone, the two
        // owner-named checkpoints. Every step must read None before it is
        // "invested".
        [Test]
        public void FragileLambThenAnEmptyPage_ReachesBlackRamsWholeTree_RootAlone()
        {
            var tree = TwoRootedPaths();
            var unlocked = new HashSet<string>();

            void Invest(int path, int slot, string reason)
            {
                Assert.AreEqual(TalentPage.Refusal.None,
                    TalentPage.Evaluate(tree, path, slot, unlocked, embers: 99, budget: 999), reason);
                unlocked.Add($"p{path}s{slot}");
            }

            // The oath: swear Fragile Lamb (path 1's root). Free, no
            // prerequisites of its own.
            Invest(1, 0, "path 1's own root should be free to swear on an empty page");

            // Black Ram (path 0's root) must now be refused -- and
            // specifically by the allegiance, not by anything else.
            Assert.AreEqual(TalentPage.Refusal.AllegianceSworn,
                TalentPage.Evaluate(tree, path: 0, slot: 0, unlocked, embers: 99, budget: 999),
                "Black Ram, the other path's own generation engine, must stay locked out");

            // A CROSS-CONSTELLATION TALENT, reached with NO preset state --
            // this is the exact stone the previous version of this test had
            // to fake into `unlocked` to pass. TwoRootedPaths gives every
            // non-root slot cost 1, well inside the 999 budget above.
            Invest(0, 1, "a tier-1 stone on the unsworn path must be reachable from the sworn root " +
                         "alone, with no preset prerequisites");

            // ALL THE WAY UP path 0's OWN chain to its convergence --
            // TwoRootedPaths gives every path the flat depth-1 skeleton
            // (TalentSkeleton.Parents chains each slot to the one directly
            // below), so walking 2..9 in order reaches it honestly.
            for (int slot = 2; slot <= 9; slot++)
            {
                Invest(0, slot, $"path 0 slot {slot} should chain off the stone below it, " +
                                 "never off its own (permanently refused) root");
            }

            Invest(0, 10, "the convergence must be reachable once its own strands are lit -- " +
                          "still true once the tier-1 root-check no longer blocks the strands beneath it");

            // AND THE CAPSTONE, the other owner-named checkpoint, up path
            // 0's post-convergence branch.
            for (int slot = 11; slot <= 19; slot++)
            {
                Invest(0, slot, $"path 0 slot {slot} (post-convergence) should stay reachable");
            }

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, path: 0, slot: 20, unlocked, embers: 99, budget: 999),
                "the capstone of the UNSWORN path must be reachable too, once its own gathered " +
                "parents are lit -- the oath never should have gated the tree, only the engine");
        }

        // THE REAL COST SHAPE, on two paths at once -- ShippedPriceShape's
        // own pricing (ContentDatabase.OrbCost: 1/2/3 climbing the grid, 0
        // at the root/convergence/capstone, 2/3/4 climbing the branch, a
        // 9-gate at the convergence and a 20-gate at the capstone), but with
        // a real root on BOTH path 0 and path 1 -- TwoRootedPaths' own
        // shape -- so an allegiance scenario can be played on realistically
        // priced ground instead of the flat depth-1 TwoRootedPaths gives it.
        private static TalentTree TwoRootedRealPriceShape()
        {
            var tree = new TalentTree();
            for (int path = 0; path < 2; path++)
            {
                for (int i = 0; i < TalentSkeleton.SlotCount; i++)
                {
                    int depth = TalentSkeleton.Depth[i];
                    int cost = depth == 0 || depth == 4 || depth == 8
                        ? 0
                        : depth <= 3 ? depth : depth - 3;
                    int gate = i == 10 ? 9 : i == 20 ? 20 : 0;
                    tree.Set(path, i, new TalentSlot($"p{path}s{i}", $"Star {path}.{i}", "", cost, gate));
                }
            }
            return tree;
        }

        // OWNER, 2026-09-23 (confirming 1523266d's rule): the exclusivity
        // covers only the wool GENERATOR -- Fragile Lamb and Black Ram can
        // never both be taken -- and everything else on the Black Ram path
        // stays open. The owner's own worked example: 21 points climbed
        // into Fragile Lamb's tree plus 9 points on the Black Ram grid
        // reach Black Ram Mode (sheep_ram_converge) without ever taking
        // Black Ram itself. Played here from an empty page with real picks
        // and real prices, not preset state:
        //
        //   path 1 (Fragile Lamb): two FULL pre-convergence strands (6+6),
        //   one strand touched to tier 2 (1+2=3) -- 15 spent, the
        //   convergence's own 9-gate and completed>=1/touched>=2 both
        //   already met -- then the convergence itself (free) and one
        //   tier-5 branch stone in EACH of the three post-convergence
        //   columns (2+2+2=6). 15 + 6 = 21.
        //
        //   path 0 (Black Ram): one full strand (1+2+3=6) plus a second
        //   strand touched to tier 2 (1+2=3) -- exactly
        //   FirstConvergenceUnlocksWithNineSpentAcrossTwoStrands' own
        //   shape, 9 spent, clearing the convergence's 9-gate with room to
        //   spare on completed/touched -- then Black Ram Mode (slot 10)
        //   itself, still never touching slot 0.
        [Test]
        public void FragileLambThenRealPicks_ReachesBlackRamModeOnNinePoints_RootStaysSworn()
        {
            var tree = TwoRootedRealPriceShape();
            var unlocked = new HashSet<string>();

            void Invest(int path, int slot, string reason)
            {
                Assert.AreEqual(TalentPage.Refusal.None,
                    TalentPage.Evaluate(tree, path, slot, unlocked, embers: 99, budget: 999), reason);
                unlocked.Add($"p{path}s{slot}");
            }

            Invest(1, 0, "Fragile Lamb (path 1's root) should be free to swear on an empty page");

            // Fragile Lamb's tree: two full strands (dx -1 and dx 0: slots
            // 1/4/7 and 2/5/8) plus the third (dx +1: slots 3/6) touched to
            // tier 2. 1+2+3 + 1+2+3 + 1+2 = 15.
            foreach (int slot in new[] { 1, 4, 7, 2, 5, 8, 3, 6 })
            {
                Invest(1, slot, $"path 1 slot {slot} should be reachable while climbing Fragile Lamb's own grid");
            }

            Assert.AreEqual(15, TalentPage.SpentOn(tree, 1, unlocked), "fixture: the pre-convergence spend moved");

            Invest(1, 10, "Fragile Lamb's own convergence should open on 15 spent across two full strands " +
                          "and a touched third");

            // One tier-5 branch stone in each of the three post-convergence
            // columns: slots 11, 12, 13. 2+2+2 = 6. 15 + 6 = 21.
            foreach (int slot in new[] { 11, 12, 13 })
            {
                Invest(1, slot, $"path 1 slot {slot} (post-convergence) should be reachable");
            }

            Assert.AreEqual(21, TalentPage.SpentOn(tree, 1, unlocked),
                "fixture: the owner's own '21 points in Fragile Lamb's tree' number");

            // Black Ram's grid, path 0, with NO root of its own -- the exact
            // shape FirstConvergenceUnlocksWithNineSpentAcrossTwoStrands
            // pins for one path, replayed here on the UNSWORN one: one full
            // strand (1+2+3=6) plus a second touched to tier 2 (1+2=3) = 9.
            foreach (int slot in new[] { 1, 4, 7, 2, 5 })
            {
                Invest(0, slot, $"path 0 slot {slot} should be reachable from the sworn root alone, " +
                                "with no preset prerequisites");
            }

            Assert.AreEqual(9, TalentPage.SpentOn(tree, 0, unlocked),
                "fixture: the owner's own '9 points on the Black Ram path' number");

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, path: 0, slot: 10, unlocked, embers: 99, budget: 999),
                "Black Ram Mode (the convergence) should open on the same 9-point, two-strand shape as " +
                "the sworn path's own convergence -- the oath never should have gated the tree, only " +
                "the engine");

            Assert.AreEqual(TalentPage.Refusal.AllegianceSworn,
                TalentPage.Evaluate(tree, path: 0, slot: 0, unlocked, embers: 99, budget: 999),
                "Black Ram itself (the wool generator, sheep_ram_root) must still be locked out -- the " +
                "exclusivity the owner confirmed covers only the two roots, nothing this deep picks up");
        }

        // THE LIFETIME BUDGET, WHICH NOTHING WAS CHECKING EITHER.
        //
        // ContentDatabase.EmberSpendCap is 30 and its header spells out what
        // the number buys ("30 deep into one path ... or 9+9+9 wide ... or 20+9
        // fused") -- the three archetypes the whole ember economy is measured
        // on. It was enforced in exactly one place: the balance bot's own
        // preset builder, which clamped the GRANT so the question never came
        // up. On the player's path nothing asked, because the wallet is shared,
        // uncapped and accumulates across runs, and asking the wallet is all
        // TalentPage.Evaluate did.
        //
        // THE LITERAL 30 rather than the constant. The point of the test is
        // that the number the design argues about is the number the gate uses;
        // reading the constant back would pass whatever it became.
        [Test]
        public void KindlingIsRefusedOnceThirtyEmbersAreCommitted()
        {
            var tree = TwoRootedPaths();

            // Path 0 climbed to slot 19: the root is free and eighteen stones
            // at 1 each, plus the free convergence -- 19 committed. Path 1's
            // root is NOT taken (that is the allegiance rule above), so its
            // stones are unreachable and the fixture stays on one path.
            var unlocked = new HashSet<string>();
            for (int i = 0; i <= 19; i++) unlocked.Add($"p0s{i}");

            Assert.AreEqual(19, TalentPage.SpentOn(tree, 0, unlocked),
                "fixture: the shape of this hand-built tree moved");

            // A wallet with plenty in it, a budget with nothing left.
            Assert.AreEqual(TalentPage.Refusal.BudgetSpent,
                TalentPage.Evaluate(tree, 0, 20, unlocked, embers: 99, budget: 0),
                "a character who has committed their whole lifetime budget was offered another " +
                "stone because their wallet still had embers in it");
        }

        // AND THE BUDGET IS NOT THE WALLET, which is the whole reason this is a
        // seventh refusal rather than a second reading of the fifth. A player
        // holding no embers today and a player who has committed all thirty are
        // told different things, because one of them can go and earn.
        [Test]
        public void AnEmptyWalletAndASpentBudgetAreDifferentAnswers()
        {
            var tree = TwoRootedPaths();
            var unlocked = new HashSet<string> { "p0s0" };

            Assert.AreEqual(TalentPage.Refusal.NotEnoughEmbers,
                TalentPage.Evaluate(tree, 0, 1, unlocked, embers: 0, budget: 30));

            Assert.AreEqual(TalentPage.Refusal.BudgetSpent,
                TalentPage.Evaluate(tree, 0, 1, unlocked, embers: 30, budget: 0));
        }

        // A FREE STONE IS STILL FREE AT THE CAP, and this is the rule
        // ContentDatabase.MeetsGates already stated: `OrbCost(talent) <=
        // EmbersLeftFor(character)`, which is 0 <= 0 for the convergence and
        // the capstone. Their price was always the gate rather than the ember,
        // and a budget spent to the last point does not take back a gate the
        // player has already paid in full.
        [Test]
        public void TheFreeLandmarksAreStillReachableWithNothingLeftToCommit()
        {
            var tree = new TalentTree();
            for (int i = 0; i < TalentSkeleton.SlotCount; i++)
            {
                tree.Set(Path, i, new TalentSlot($"t{i}", $"Star {i}", "", i == 20 ? 0 : 1));
            }

            var owned = Ancestry(tree, 20);

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, Path, 20, owned, embers: 0, budget: 0),
                "the capstone costs nothing, so a spent budget cannot be what is in the way");
        }

        // The near-redundancy that hid the missing rule for as long as it did.
        // Worth pinning: if the skeleton ever grows a gated slot whose ancestry
        // does NOT pay for it, this is the test that says the gate started
        // biting for the first time.
        [Test]
        public void TheAuthoredGatesArePaidByTheirOwnAncestry()
        {
            foreach (var authored in new[] { (slot: 10, gate: 9), (slot: 20, gate: 20) })
            {
                var tree = TreeWithGate(authored.slot, authored.gate);
                var owned = Ancestry(tree, authored.slot);

                Assert.AreEqual(TalentPage.Refusal.None,
                    TalentPage.Evaluate(tree, Path, authored.slot, owned, embers: 999, budget: 999),
                    $"slot {authored.slot}'s gate of {authored.gate} is no longer covered by the " +
                    "orbs required to reach it, so enforcing it now blocks a purchase that used " +
                    "to succeed. That may be correct - but it is a balance change, not a screen one");
            }
        }
    }
}
