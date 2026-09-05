using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // An ember per boss you had never killed before.
    //
    // The whole currency rests on the word UNIQUE, and getting it wrong is not
    // a balance wobble -- paying twice for one boss is the meta-progression
    // version of the infinite money printer AUDIT P0 #1 recorded, and paying
    // zero times makes talents unreachable forever.
    public class EmberPayoutTests
    {
        [Test]
        public void AFirstKillPays()
        {
            var fresh = EmberPayout.NewBosses(new[] { "warden" }, new List<string>());

            CollectionAssert.AreEqual(new[] { "warden" }, fresh);
            Assert.AreEqual(EmberPayout.PerUniqueBoss, EmberPayout.EmbersFor(fresh.Count));
        }

        [Test]
        public void AKillYouHaveAlreadyBankedPaysNothing()
        {
            var fresh = EmberPayout.NewBosses(new[] { "warden" }, new List<string> { "warden" });

            CollectionAssert.IsEmpty(fresh);
            Assert.AreEqual(0, EmberPayout.EmbersFor(fresh.Count));
        }

        [Test]
        public void KillingTheSameBossTwiceInOneRunPaysOnce()
        {
            // Reachable if a boss room is ever re-entered, or if the fight end
            // reports twice on a resumed run. Cheap to guard, expensive to
            // discover.
            var fresh = EmberPayout.NewBosses(new[] { "warden", "warden" }, new List<string>());

            Assert.AreEqual(1, fresh.Count);
        }

        [Test]
        public void OnlyTheNewOnesInAMixedRunPay()
        {
            var fresh = EmberPayout.NewBosses(
                new[] { "warden", "hollow", "warden" },
                new List<string> { "warden" });

            CollectionAssert.AreEqual(new[] { "hollow" }, fresh);
        }

        [Test]
        public void KillOrderIsPreserved()
        {
            // The screen names them, so it names them in the order they fell.
            var fresh = EmberPayout.NewBosses(new[] { "third", "first", "second" }, new List<string>());

            CollectionAssert.AreEqual(new[] { "third", "first", "second" }, fresh);
        }

        [Test]
        public void BlanksAndNullsAreIgnoredRatherThanPaidFor()
        {
            // A boss fight with no declared enemy id is a content gap. It must
            // not mint an ember for the empty string.
            var fresh = EmberPayout.NewBosses(new[] { "", null, "warden" }, new List<string>());

            CollectionAssert.AreEqual(new[] { "warden" }, fresh);
        }

        [Test]
        public void ARunThatKilledNoBossPaysNothing()
        {
            Assert.IsEmpty(EmberPayout.NewBosses(new string[0], new List<string>()));
            Assert.IsEmpty(EmberPayout.NewBosses(null, new List<string>()));
            Assert.AreEqual(0, EmberPayout.EmbersFor(0));
            Assert.AreEqual(0, EmberPayout.EmbersFor(-3));
        }

        [Test]
        public void ANullLifetimeListTreatsEveryKillAsNew()
        {
            // A save from before this field existed deserializes it as null.
            // Treating that as "nothing killed yet" is the forgiving direction,
            // and the caller records what it pays, so it cannot repeat.
            var fresh = EmberPayout.NewBosses(new[] { "warden" }, null);

            CollectionAssert.AreEqual(new[] { "warden" }, fresh);
        }

        [Test]
        public void TheTotalObtainableIsBoundedByTheBossRoster()
        {
            // Stated as a test because it is a DESIGN POSITION, not an
            // accident: embers are finite, so trees are permanently
            // exclusive choices rather than a completion checklist. If this
            // ever needs to stop being true, this test is where the argument
            // gets had.
            var roster = new[] { "a", "b", "c", "d", "e" };
            var banked = new List<string>();

            int total = 0;
            for (int run = 0; run < 20; run++)
            {
                var fresh = EmberPayout.NewBosses(roster, banked);
                total += EmberPayout.EmbersFor(fresh.Count);
                banked.AddRange(fresh);
            }

            Assert.AreEqual(roster.Length * EmberPayout.PerUniqueBoss, total,
                "twenty runs against five bosses still only ever pays five times");
        }
    }
}
