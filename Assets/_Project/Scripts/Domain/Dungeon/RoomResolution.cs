using System;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Dungeon
{
    // What walking into a non-fight room does.
    //
    // The rule half, pure and seeded. Applying it -- crediting the gold,
    // healing the squad, saying so on the map -- is RoomResolver's job in Core,
    // because both of those need ContentDatabase and the save.
    //
    // This exists because v2 had regressed against a rule v1 wrote down
    // explicitly. v1's ResolveRoom gave every room something to say, and its
    // comment on the unbuilt ones is the reason why: "each says so plainly
    // rather than resolving in silence, because a room that does nothing
    // without explaining itself reads as a bug". v2's map cleared every
    // non-fight room in silence -- treasure paid nothing, rest healed nobody.
    public static class RoomResolution
    {
        // v1's numbers, ported rather than re-tuned. Inclusive at both ends,
        // which is why the roll below adds one to the max: SeededRandom.NextInt
        // takes an EXCLUSIVE upper bound.
        public const int TreasureGoldMin = 15;
        public const int TreasureGoldMax = 30;

        // What the room did, in terms the caller can apply without re-deciding
        // anything. Kind is carried so the display can pick its own wording
        // without a second switch over RoomType that could drift from this one.
        public enum Kind
        {
            // Fights are not resolved here at all -- they open a screen. Entry
            // is the room the party starts standing in.
            None,
            Treasure,
            Rest,
            EventNotBuilt,
            ItemNotBuilt,
            Empty,
        }

        public readonly struct Outcome
        {
            public readonly Kind Result;
            public readonly int Gold;
            public readonly bool HealsPartyToFull;

            public Outcome(Kind result, int gold = 0, bool healsPartyToFull = false)
            {
                Result = result;
                Gold = gold;
                HealsPartyToFull = healsPartyToFull;
            }

            // Whether the room should be marked cleared and the map redrawn.
            // False only for the rooms that hand off to a screen instead.
            public bool Resolves => Result != Kind.None;
        }

        // rng is opened by the caller against the TREASURE stream, keyed to the
        // node. Keying it to the node rather than to a counter is what makes
        // re-entering the same treasure room after a quit find the same stash
        // rather than rerolling for a better one -- the same property the fight
        // stream gives an encounter.
        public static Outcome Resolve(RoomType roomType, SeededRandom rng)
        {
            switch (roomType)
            {
                // Fights open a screen; the fight clears its own room when it
                // resolves, through the reward path. Entry is where the party
                // already is.
                case RoomType.Fight:
                case RoomType.EliteFight:
                case RoomType.Boss:
                case RoomType.Entry:
                    return new Outcome(Kind.None);

                case RoomType.Treasure:
                    // A null stream is a room entered outside a run -- a direct
                    // scene load. Paying the minimum beats throwing, and beats
                    // paying nothing, which would look exactly like the silence
                    // this whole class exists to remove.
                    int gold = rng != null
                        ? rng.NextInt(TreasureGoldMin, TreasureGoldMax + 1)
                        : TreasureGoldMin;
                    return new Outcome(Kind.Treasure, gold: gold);

                case RoomType.Rest:
                    return new Outcome(Kind.Rest, healsPartyToFull: true);

                // Shop is not a placeholder any more -- it resolves into a
                // screen. RunOrchestrator.ArriveAt handles RoomType.Shop
                // itself, BEFORE this method is ever called, so this case
                // must never run. Throwing rather than returning an Outcome
                // makes a caller that skips ArriveAt fail loudly instead of
                // quietly resolving a shop as if it had nothing behind it.
                case RoomType.Shop:
                    throw new InvalidOperationException(
                        "RoomType.Shop is resolved by RunOrchestrator.ArriveAt, " +
                        "not by RoomResolution.Resolve -- ArriveAt should have " +
                        "handled it before this was called.");

                // Placeholders. They generate, they draw on the map, they can
                // be entered and cleared -- only the content behind them is
                // missing, and each says so rather than resolving in silence.
                //
                // Event is a REGRESSION against v1 knowingly left standing: v1
                // ran a wandering-mage event that taught an event-gated spell.
                // Porting it needs the spell-teaching path, which is a larger
                // job than restoring the rooms; until then an honest placeholder
                // beats a silent one.
                case RoomType.Event:
                    return new Outcome(Kind.EventNotBuilt);

                case RoomType.ItemSpawn:
                    return new Outcome(Kind.ItemNotBuilt);

                // Unscouted. The mystery is presentation only -- by the time it
                // is entered there is nothing hidden underneath, so it resolves
                // as the empty room it is.
                case RoomType.Unknown:
                    return new Outcome(Kind.Empty);

                default:
                    return new Outcome(Kind.Empty);
            }
        }
    }
}
