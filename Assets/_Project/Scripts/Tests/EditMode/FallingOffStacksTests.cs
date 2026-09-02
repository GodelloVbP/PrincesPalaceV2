using NUnit.Framework;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Tests
{
    // Mechanic (c): a stacking status where each stack carries its OWN
    // expiry rather than the whole pile sharing one duration.
    //
    // ADD DOES NOT TICK -- see FallingOffStacks' own header. A Tick call
    // represents one of the HOLDER's own turns passing; three hits landed
    // across three separate turns are three Add calls each preceded by its
    // own Tick (the second and third hit's Tick is what stales the FIRST
    // stack's clock relative to the ones after it, which is what lets a
    // single later Tick call remove only the oldest survivor rather than
    // the whole pile at once).
    public class FallingOffStacksTests
    {
        private const string Key = "test_stack";

        private static CombatantState Dummy() => new CombatantState("Dummy", false, 100, 0, 10, 10);

        [Test]
        public void StacksAccumulateUpToTheCap()
        {
            var target = Dummy();

            FallingOffStacks.AddStack(target, Key, 3, 5);
            FallingOffStacks.AddStack(target, Key, 3, 5);
            FallingOffStacks.AddStack(target, Key, 3, 5);

            Assert.AreEqual(3, FallingOffStacks.Count(target, Key));
        }

        [Test]
        public void AddingPastTheCapDoesNothing()
        {
            var target = Dummy();

            for (int i = 0; i < 10; i++) FallingOffStacks.AddStack(target, Key, 3, 2);

            Assert.AreEqual(2, FallingOffStacks.Count(target, Key));
        }

        [Test]
        public void MagnitudeIsStacksTimesPerStackValueCapped()
        {
            var target = Dummy();
            FallingOffStacks.AddStack(target, Key, 3, 5);
            FallingOffStacks.AddStack(target, Key, 3, 5);

            Assert.AreEqual(6, FallingOffStacks.Magnitude(target, Key, 3, 15), "2 stacks x 3 = 6");

            for (int i = 0; i < 5; i++) FallingOffStacks.AddStack(target, Key, 3, 5);
            Assert.AreEqual(15, FallingOffStacks.Magnitude(target, Key, 3, 15), "5 stacks x 3 = 15, at the cap");
        }

        // THE EXAMPLE THE MECHANIC WAS SPECIFIED AGAINST: three hits three
        // turns apart give three stacks; a turn with no hit ages the oldest
        // out (2 remain); a hit landing before the next ageing step brings
        // it back to three, because the two survivors have not both reached
        // expiry yet. Each Add is preceded by its own Tick, matching one
        // hit per elapsed turn -- except the LAST Add, which represents a
        // hit landing in the same beat the preceding Tick already resolved,
        // with no further turn having passed since.
        [Test]
        public void EachStackFallsOffIndividually()
        {
            var target = Dummy();

            FallingOffStacks.AddStack(target, Key, 3, 5); // turn 1: hit
            FallingOffStacks.Tick(target, Key);            // turn 2 begins
            FallingOffStacks.AddStack(target, Key, 3, 5); // turn 2: hit
            FallingOffStacks.Tick(target, Key);            // turn 3 begins
            FallingOffStacks.AddStack(target, Key, 3, 5); // turn 3: hit

            Assert.AreEqual(3, FallingOffStacks.Count(target, Key), "three hits, three stacks");

            FallingOffStacks.Tick(target, Key); // turn 4 begins, no hit
            Assert.AreEqual(2, FallingOffStacks.Count(target, Key), "the oldest stack (born turn 1) falls off");

            FallingOffStacks.AddStack(target, Key, 3, 5); // hit again
            Assert.AreEqual(3, FallingOffStacks.Count(target, Key), "back to three -- the two survivors were not yet due");
        }

        [Test]
        public void TickAllAgesEveryKeyOnTheCombatant()
        {
            var target = Dummy();
            FallingOffStacks.AddStack(target, "a", 1, 5);
            FallingOffStacks.AddStack(target, "b", 3, 5);

            FallingOffStacks.TickAll(target);

            Assert.AreEqual(0, FallingOffStacks.Count(target, "a"), "duration 1 expires on the first tick");
            Assert.AreEqual(1, FallingOffStacks.Count(target, "b"), "duration 3 survives one tick");
        }
    }
}
