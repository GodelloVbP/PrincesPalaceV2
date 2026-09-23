using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.Domain.Tests
{
    // THE UNCHOSEN STRAND STAYED DARK, WHICH NOTHING WAS CHECKING.
    //
    // TalentController.PaintEdges used to light an edge whenever its CHILD
    // slot was unlocked, on the stated assumption that "the parent
    // necessarily already is [invested], because that is what a prerequisite
    // means". That is false for the convergence (slot 10): TalentPage.
    // MeetsPrerequisites lets a merge slot in on ONE completed strand of its
    // three, so a player who only ever climbed slot 7 still lit the edges
    // out of slots 8 and 9 too, the moment slot 10 itself lit -- an energy
    // beam into a kindled star from stones the player never touched (owner's
    // hardware playtest, 2026-09-23).
    //
    // TalentPage.EdgeIsLit is the fix, pinned here with literal ids rather
    // than through TalentController -- Core cannot compile under dotnet
    // (PrincesPalace.Domain.csproj only includes Assets/_Project/Scripts/
    // Domain), and the rule itself needs nothing Core has that Domain does
    // not.
    public class TalentEdgeLightingTests
    {
        private const int Path = 0;

        // Slot 10 is the convergence (TalentSkeleton.Kind[10] == "merge"),
        // gathering slots 7, 8 and 9 -- the three tiers immediately below it.
        private const int Convergence = 10;
        private const int Strand0 = 7;
        private const int Strand1 = 8;
        private const int Strand2 = 9;

        // A tree with every slot authored under a stable, readable id, and
        // the given ids marked unlocked -- built by hand rather than from
        // content, the same reasoning TalentGateTests.TreeWithGate states:
        // the point is the RULE, not the authoring.
        private static TalentTree TreeWithIds()
        {
            var tree = new TalentTree();

            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                tree.Set(Path, slot, new TalentSlot($"p{Path}s{slot}", $"Star {slot}", "", 1));
            }

            return tree;
        }

        private static HashSet<string> Unlocked(params int[] slots)
        {
            var set = new HashSet<string>();
            foreach (var slot in slots) set.Add($"p{Path}s{slot}");
            return set;
        }

        [Test]
        public void DarkWhenNeitherEndpointIsUnlocked()
        {
            var tree = TreeWithIds();
            var unlocked = Unlocked();

            Assert.IsFalse(TalentPage.EdgeIsLit(tree, Path, Strand0, Convergence, unlocked));
        }

        [Test]
        public void DarkWhenOnlyTheChildIsUnlocked()
        {
            // Exactly the shipped bug: the convergence lit through Strand0
            // (below), but nothing invested Strand1 -- its own edge must stay
            // dark even though the child it feeds is lit.
            var tree = TreeWithIds();
            var unlocked = Unlocked(Strand0, Convergence);

            Assert.IsFalse(TalentPage.EdgeIsLit(tree, Path, Strand1, Convergence, unlocked));
        }

        [Test]
        public void DarkWhenOnlyTheParentIsUnlocked()
        {
            // The reverse gap: a strand climbed but the convergence not yet
            // taken. Both endpoints still have to agree.
            var tree = TreeWithIds();
            var unlocked = Unlocked(Strand0);

            Assert.IsFalse(TalentPage.EdgeIsLit(tree, Path, Strand0, Convergence, unlocked));
        }

        [Test]
        public void LitOnlyOnTheStrandActuallyClimbed()
        {
            // The convergence taken through Strand0 alone (TalentPage.
            // MeetsPrerequisites' merge rule: one completed parent, two
            // touched strands is enough) -- Strand0's edge lights, Strand1
            // and Strand2's do not, even though all three feed the same lit
            // child.
            var tree = TreeWithIds();
            var unlocked = Unlocked(Strand0, Convergence);

            Assert.IsTrue(TalentPage.EdgeIsLit(tree, Path, Strand0, Convergence, unlocked));
            Assert.IsFalse(TalentPage.EdgeIsLit(tree, Path, Strand1, Convergence, unlocked));
            Assert.IsFalse(TalentPage.EdgeIsLit(tree, Path, Strand2, Convergence, unlocked));
        }

        [Test]
        public void LitWhenEveryFedStrandIsUnlockedToo()
        {
            var tree = TreeWithIds();
            var unlocked = Unlocked(Strand0, Strand1, Strand2, Convergence);

            Assert.IsTrue(TalentPage.EdgeIsLit(tree, Path, Strand0, Convergence, unlocked));
            Assert.IsTrue(TalentPage.EdgeIsLit(tree, Path, Strand1, Convergence, unlocked));
            Assert.IsTrue(TalentPage.EdgeIsLit(tree, Path, Strand2, Convergence, unlocked));
        }

        [Test]
        public void UnauthoredEndpointIsNeverLit()
        {
            var tree = new TalentTree(); // nothing authored at all
            var unlocked = new HashSet<string>();

            Assert.IsFalse(TalentPage.EdgeIsLit(tree, Path, Strand0, Convergence, unlocked));
        }
    }
}
