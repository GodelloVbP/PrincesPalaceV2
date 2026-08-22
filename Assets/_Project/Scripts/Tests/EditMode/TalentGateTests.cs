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
                tree, Path, TalentSkeleton.SlotCount - 1, owned, embers: 999);

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
                TalentPage.Evaluate(tree, Path, slot, owned, embers: 999),
                "the gate is met and the slot is still refused");
        }

        [Test]
        public void AGateOfZeroIsNoGateAtAll()
        {
            int slot = TalentSkeleton.SlotCount - 1;
            var tree = TreeWithGate(slot, gate: 0);
            var owned = Ancestry(tree, slot);

            Assert.AreEqual(TalentPage.Refusal.None,
                TalentPage.Evaluate(tree, Path, slot, owned, embers: 999),
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
                tree, Path, slot, new HashSet<string>(), embers: 999);

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
                TalentPage.Evaluate(tree, Path, slot, owned, embers: 0),
                "a gated stone the player also cannot afford reports the price, which is the " +
                "smaller of the two obstacles");
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
                    TalentPage.Evaluate(tree, Path, authored.slot, owned, embers: 999),
                    $"slot {authored.slot}'s gate of {authored.gate} is no longer covered by the " +
                    "orbs required to reach it, so enforcing it now blocks a purchase that used " +
                    "to succeed. That may be correct - but it is a balance change, not a screen one");
            }
        }
    }
}
