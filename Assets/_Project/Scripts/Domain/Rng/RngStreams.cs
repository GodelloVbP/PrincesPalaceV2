namespace PrincesPalace.Domain.Rng
{
    // Turns one run seed into many independent, reproducible streams.
    //
    // The alternative — holding a single live SeededRandom for the whole run —
    // makes every draw depend on how many draws came before it, which is the
    // property that stops a run being resumable. You would have to persist the
    // generator's state, and then any change to how many numbers a fight
    // consumes silently invalidates every save in existence.
    //
    // So nothing here is stateful. A stream is derived from (run seed, what it
    // is for, where in the run it is), and POSITION carries the state: the
    // fight at step 12 node 7 always draws the same numbers, whether it is
    // reached by playing there or by loading a save. That also means resuming
    // cannot replay a reward already taken, because taking it advanced the
    // step, and that quitting mid-fight and returning re-fights the SAME
    // fight rather than rerolling for a better one.
    //
    // Streams are kept disjoint on purpose. Map generation and the boss pick
    // could share one generator, but then adding an enemy to content would
    // change how many numbers the boss pick consumed and shift every map
    // afterwards. Separate streams mean a content change can only affect the
    // thing it is actually about.
    public static class RngStreams
    {
        // Arbitrary but FIXED. Changing one of these renumbers every run that
        // was ever seeded — treat them as a serialized format, not as
        // constants that can be tidied. New streams take a new number.
        public const uint Leg = 1;
        public const uint Boss = 2;
        public const uint Fight = 3;
        public const uint Treasure = 4;

        // SplitMix64's finalizer, the same mixing SeededRandom itself uses.
        // Applied to the packed inputs rather than to a running state, so this
        // is a pure hash: same inputs, same answer, forever, with no ordering
        // to preserve.
        //
        // The odd-constant multiplies before mixing are there so that inputs
        // which differ in only one small field — adjacent steps, adjacent node
        // ids — still land far apart. Without them, neighbouring positions
        // produce neighbouring seeds, and neighbouring seeds are exactly the
        // case a player would notice as "the next room felt the same".
        public static ulong Derive(ulong runSeed, uint stream, int a, int b = 0)
        {
            ulong z = runSeed;
            z ^= stream * 0x9E3779B97F4A7C15UL;
            z ^= unchecked((ulong)(long)a) * 0xBF58476D1CE4E5B9UL;
            z ^= unchecked((ulong)(long)b) * 0x94D049BB133111EBUL;

            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Convenience for the common shape: derive a seed and open a generator
        // on it. Callers never hold the seed itself, only the stream.
        public static SeededRandom Open(ulong runSeed, uint stream, int a, int b = 0)
        {
            return new SeededRandom(Derive(runSeed, stream, a, b));
        }
    }
}
