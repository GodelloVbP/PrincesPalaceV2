using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // WHERE IN THE RUN a leg is has to reach the generator, not just the
    // forced-room cadence.
    //
    // ForcedTypeAt is periodic mod StepsPerBoss and legs are aligned to that
    // grid, so startStep on its own cannot make two legs differ: leg 2's
    // forced rooms land at exactly the same column offsets as leg 1's. The
    // only thing left that can tell one leg from the next is the generator's
    // SEED -- which is why RngStreams keys a stream to (run seed, what it is
    // for, where in the run it is) rather than to the run seed alone.
    public class DescentLegSeedingTests
    {
        private const ulong RunSeed = 4242UL;

        // Column widths, room types and forward links -- everything a player
        // would recognise as "this is the same map".
        private static string Shape(DescentMap map)
        {
            var parts = new List<string>();
            for (int d = 0; d < map.DepthCount; d++)
            {
                foreach (var node in map.AtDepth(d))
                {
                    parts.Add($"{d}.{node.Slot}:{node.Type}->[{string.Join(",", node.Next.OrderBy(n => n))}]");
                }
            }

            return string.Join("|", parts);
        }

        [Test]
        public void EachLegOfOneDescentIsItsOwnMap()
        {
            var shapes = new List<string>();
            for (int leg = 0; leg < 8; leg++)
            {
                int startStep = leg * DescentMapGenerator.DefaultLegLength;
                shapes.Add(Shape(DescentMapGenerator.GenerateLegFor(RunSeed, startStep)));
            }

            CollectionAssert.AllItemsAreUnique(shapes,
                "every leg of one descent generated the identical map");
        }

        // The other half of the same contract: position carries the state, so
        // re-asking for a leg already visited has to hand back the leg that
        // was left. Quitting mid-leg and resuming must not reroll the dungeon.
        [Test]
        public void AskingForTheSameLegTwiceGivesTheSameMap()
        {
            Assert.AreEqual(
                Shape(DescentMapGenerator.GenerateLegFor(RunSeed, 24, restBeforeBoss: true)),
                Shape(DescentMapGenerator.GenerateLegFor(RunSeed, 24, restBeforeBoss: true)));
        }

        // The old caller's shape, kept as the reason GenerateLegFor exists:
        // seeding on the run alone cannot tell one leg from the next, so a
        // future caller reaching for GenerateLeg directly is reintroducing
        // the same bug rather than choosing a different generator.
        [Test]
        public void SeedingOnTheRunAloneRepeatsOneMapForTheWholeDescent()
        {
            Assert.AreEqual(
                Shape(DescentMapGenerator.GenerateLeg(new SeededRandom(RunSeed), 0)),
                Shape(DescentMapGenerator.GenerateLeg(new SeededRandom(RunSeed), 8)));
        }
    }
}
