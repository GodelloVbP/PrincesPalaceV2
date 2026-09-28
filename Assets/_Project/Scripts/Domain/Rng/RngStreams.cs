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

        // THE SHOP IS THREE STREAMS, NOT ONE, and the split is what makes a
        // per-section reroll possible at all. Rerolling the gear shelf must
        // not move the relics beside it, so "which section" is carried by the
        // stream number rather than by arithmetic on a position coordinate --
        // arithmetic would make two sections' positions collide the day
        // either number grows a bound (docs/PLAN_SHOP.md F9, §7.1 point 7).
        //
        // All three open on the same position: (step, currentNodeId, that
        // section's own reroll count).
        public const uint ShopGear = 5;
        public const uint ShopBooks = 6;
        public const uint ShopRelics = 7;

        // A won fight's spell-book drop roll (docs/PLAN_SHOP.md §1e), keyed
        // to (run.step, run.currentNodeId) -- quitting mid-reward and
        // returning must not reroll it, same reason Treasure and the shop
        // streams are keyed to position rather than drawn fresh.
        public const uint SpellDrop = 8;

        // Which event an Event room opens (EventRoll.Pick), keyed to
        // (run.step, run.currentNodeId). Its own number rather than
        // Treasure's: an Event and a Treasure room are both non-fight rooms
        // at a position, and two rooms drawing from one sequence would let
        // one roll predict the other.
        public const uint Event = 9;

        // A MERCHANT SHELF (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5, 3.4) is
        // four streams, the room shop's reason for three: what the gear is,
        // which consumables sit beside it, which cards are fake and which card
        // a robbery loses must each be unable to move the others. The first
        // three open at the (step, node) where the stock is first rolled; the
        // scuffle at the (step, node) of the robbery that takes it. Never the
        // room shop's ShopGear: a caravan and a room shop at the same position
        // must not roll the same gear.
        public const uint ShelfGear = 10;
        public const uint ShelfConsumables = 11;
        public const uint ShelfFakes = 12;
        public const uint ShelfScuffle = 13;

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
        //
        // THE THIRD POSITION INPUT `c` IS AN XOR AND MUST STAY ONE. The shop
        // needs three coordinates (step, node, reroll index) and packing two
        // of them into `b` would alias two distinct positions onto one seed
        // the day either grew a bound -- silently, since an aliased seed
        // throws nothing and logs nothing (docs/PLAN_SHOP.md F9). Left as a
        // separate XOR line rather than folded into the mix because `c`
        // defaults to 0, `0 * K` is 0 and `z ^ 0` is `z`: every caller
        // written before this line existed derives the identical seed it
        // always did, which is what the "serialized format" warning above
        // demands. Pinned by RngStreamsTests, so tidying it into the
        // multiply chain fails a test rather than renumbering every save.
        public static ulong Derive(ulong runSeed, uint stream, int a, int b = 0, int c = 0)
        {
            ulong z = runSeed;
            z ^= stream * 0x9E3779B97F4A7C15UL;
            z ^= unchecked((ulong)(long)a) * 0xBF58476D1CE4E5B9UL;
            z ^= unchecked((ulong)(long)b) * 0x94D049BB133111EBUL;
            z ^= unchecked((ulong)(long)c) * 0xD6E8FEB86659FD93UL;

            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Convenience for the common shape: derive a seed and open a generator
        // on it. Callers never hold the seed itself, only the stream.
        public static SeededRandom Open(ulong runSeed, uint stream, int a, int b = 0, int c = 0)
        {
            return new SeededRandom(Derive(runSeed, stream, a, b, c));
        }
    }
}
