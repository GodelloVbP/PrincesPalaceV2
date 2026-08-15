using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.EditModeTests
{
    // What walking into a non-fight room does.
    //
    // v2 had regressed against a rule v1 stated outright: every room says
    // something, including the ones with nothing behind them yet, "because a
    // room that does nothing without explaining itself reads as a bug". The
    // map had been clearing treasure and rest rooms in silence -- no gold, no
    // healing, no message.
    public class RoomResolutionTests
    {
        private static SeededRandom Rng(ulong seed) => new SeededRandom(seed);

        // The sweep RoomType.cs says went missing with the grid map: "the sweep
        // that used to assert every value was reachable belonged to
        // MapGeneratorTests and went with the grid map". Adding a room type and
        // forgetting to resolve it is exactly the silent gap this catches.
        [Test]
        public void EveryRoomTypeResolvesToSomethingDeliberate()
        {
            foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
            {
                var outcome = RoomResolution.Resolve(type, Rng(1));

                bool handsOffToAScreen =
                    type == RoomType.Fight || type == RoomType.EliteFight
                    || type == RoomType.Boss || type == RoomType.Entry;

                if (handsOffToAScreen)
                {
                    Assert.That(outcome.Resolves, Is.False,
                        $"{type} opens a screen and must not resolve on the map");
                }
                else
                {
                    Assert.That(outcome.Resolves, Is.True,
                        $"{type} resolves on the map and must say so, not clear in silence");
                }
            }
        }

        // ---- treasure ------------------------------------------------------

        [Test]
        public void TreasurePaysWithinItsBand()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var outcome = RoomResolution.Resolve(RoomType.Treasure, Rng(seed));

                Assert.That(outcome.Result, Is.EqualTo(RoomResolution.Kind.Treasure));
                Assert.That(outcome.Gold, Is.InRange(15, 30), $"seed {seed}");
            }
        }

        // The band is inclusive at BOTH ends. NextInt takes an exclusive upper
        // bound, so a max of 30 has to be asked for as 31 -- an off-by-one here
        // silently costs the player the best roll in the game and nothing else
        // would notice.
        [Test]
        public void TreasureCanPayBothEndsOfItsBand()
        {
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 4000; seed++)
            {
                seen.Add(RoomResolution.Resolve(RoomType.Treasure, Rng(seed)).Gold);
            }

            Assert.That(seen.Contains(15), Is.True, "the minimum roll never came up");
            Assert.That(seen.Contains(30), Is.True, "the maximum roll never came up - off by one?");
            Assert.That(seen.Max(), Is.EqualTo(30), "paid above the band");
            Assert.That(seen.Min(), Is.EqualTo(15), "paid below the band");
        }

        // Re-entering the same treasure room after a quit finds the SAME stash
        // rather than rerolling for a better one. The caller keys the stream to
        // the node; this pins that the roll itself is a pure function of it.
        [Test]
        public void TheSameRoomFindsTheSameStash()
        {
            var first = RoomResolution.Resolve(RoomType.Treasure,
                RngStreams.Open(90210UL, RngStreams.Treasure, 5, 12));
            var second = RoomResolution.Resolve(RoomType.Treasure,
                RngStreams.Open(90210UL, RngStreams.Treasure, 5, 12));

            Assert.That(second.Gold, Is.EqualTo(first.Gold));
        }

        [Test]
        public void DifferentTreasureRoomsMostlyPayDifferently()
        {
            int differing = 0;

            for (ulong seed = 1; seed <= 60; seed++)
            {
                int left = RoomResolution.Resolve(RoomType.Treasure,
                    RngStreams.Open(seed, RngStreams.Treasure, 4, 1)).Gold;
                int right = RoomResolution.Resolve(RoomType.Treasure,
                    RngStreams.Open(seed, RngStreams.Treasure, 4, 2)).Gold;

                if (left != right) differing++;
            }

            // Not "always" -- a sixteen-wide band collides often enough that
            // demanding every pair differ would be asserting away the dice.
            Assert.That(differing, Is.GreaterThan(40));
        }

        // A room entered outside a run has no stream. Paying the minimum beats
        // throwing, and beats paying nothing -- which would look exactly like
        // the silence this class exists to remove.
        [Test]
        public void TreasureWithoutAStreamStillPays()
        {
            var outcome = RoomResolution.Resolve(RoomType.Treasure, null);

            Assert.That(outcome.Gold, Is.EqualTo(15));
            Assert.That(outcome.Resolves, Is.True);
        }

        [Test]
        public void TreasureDoesNotHeal()
        {
            Assert.That(RoomResolution.Resolve(RoomType.Treasure, Rng(7)).HealsPartyToFull, Is.False);
        }

        // ---- rest ----------------------------------------------------------

        [Test]
        public void RestHealsAndPaysNothing()
        {
            var outcome = RoomResolution.Resolve(RoomType.Rest, Rng(3));

            Assert.That(outcome.Result, Is.EqualTo(RoomResolution.Kind.Rest));
            Assert.That(outcome.HealsPartyToFull, Is.True);
            Assert.That(outcome.Gold, Is.Zero, "a rest room is not a payday");
        }

        // Rest is not a dice roll. The same room heals the same amount -- all
        // of it -- however many times it is looked at.
        [Test]
        public void RestIsNotSeeded()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Assert.That(RoomResolution.Resolve(RoomType.Rest, Rng(seed)).HealsPartyToFull, Is.True);
            }
        }

        // ---- fights hand off, they do not resolve ---------------------------

        [TestCase(RoomType.Fight)]
        [TestCase(RoomType.EliteFight)]
        [TestCase(RoomType.Boss)]
        public void AFightIsNotResolvedOnTheMap(RoomType type)
        {
            var outcome = RoomResolution.Resolve(type, Rng(5));

            Assert.That(outcome.Resolves, Is.False);
            Assert.That(outcome.Gold, Is.Zero, "a fight pays through its own reward path, not on arrival");
            Assert.That(outcome.HealsPartyToFull, Is.False);
        }

        // The room the party is already standing in.
        [Test]
        public void TheEntryRoomDoesNothing()
        {
            Assert.That(RoomResolution.Resolve(RoomType.Entry, Rng(1)).Resolves, Is.False);
        }

        // ---- placeholders say so --------------------------------------------

        // The whole point of the rule: an unbuilt room is entered, cleared, and
        // ADMITS it. Paying nothing while saying nothing is the bug.
        [TestCase(RoomType.Shop, RoomResolution.Kind.ShopNotBuilt)]
        [TestCase(RoomType.Event, RoomResolution.Kind.EventNotBuilt)]
        [TestCase(RoomType.ItemSpawn, RoomResolution.Kind.ItemNotBuilt)]
        [TestCase(RoomType.Unknown, RoomResolution.Kind.Empty)]
        public void AnUnbuiltRoomResolvesAndNamesItself(RoomType type, RoomResolution.Kind expected)
        {
            var outcome = RoomResolution.Resolve(type, Rng(2));

            Assert.That(outcome.Result, Is.EqualTo(expected));
            Assert.That(outcome.Resolves, Is.True, $"{type} must clear rather than trap the party");
            Assert.That(outcome.Gold, Is.Zero);
            Assert.That(outcome.HealsPartyToFull, Is.False);
        }
    }
}
