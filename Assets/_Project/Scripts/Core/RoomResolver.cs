using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Runs what a non-fight room contains, the moment the party arrives.
    //
    // The plumbing half of RoomResolution: that decides, this applies. The
    // split is the usual one -- crediting gold and healing a squad need the
    // save and ContentDatabase, and a rule that lives in Core is a rule no
    // EditMode test can reach.
    //
    // Before this, MapController's Arrive cleared every non-fight room and
    // redrew. Treasure paid nothing, Rest healed nobody, and the player was
    // told none of it.
    public static class RoomResolver
    {
        // What the last resolved room did, for the map to say out loud.
        //
        // Static, so it survives the Refresh that follows resolving. Cleared by
        // Reset and whenever a fight room is entered, because a stale line
        // under a new room is worse than no line -- it would credit this room
        // with the last one's stash.
        public static RoomResolution.Outcome Last { get; private set; }
            = new RoomResolution.Outcome(RoomResolution.Kind.None);

        // Static state outlives a scene and leaks between tests (AUDIT #23), so
        // it gets an explicit reset hook rather than relying on a reload.
        public static void Reset() => Last = new RoomResolution.Outcome(RoomResolution.Kind.None);

        // Returns true when the room resolved here, false when it hands off to
        // a screen instead.
        //
        // THE RETURN IS ADVISORY: the one production caller
        // (RunOrchestrator.ArriveAt) discards it and clears unconditionally.
        // That is not a bug today and the reason is worth writing down rather
        // than rediscovering: false comes back only for Fight, EliteFight,
        // Boss and Entry (RoomResolution.Resolve), and ArriveAt routes all four
        // away before it reaches this call -- so the only answer it can
        // actually receive is true. Dead, not wrong.
        //
        // It stays a bool because a SECOND caller -- one that can hand in a
        // fight type -- would need it, and silently clearing a fight room is
        // how a fight gets skipped. If you are writing that caller, honour the
        // return.
        public static bool Resolve(RunSnapshot run, RoomType roomType)
        {
            // Keyed to the NODE, so re-entering the same treasure room after a
            // quit finds the same stash rather than rerolling for a better one.
            // A separate stream from the fight's: two rooms at one step must
            // not have their stash and their monsters drawn from the same
            // sequence, or one would predict the other.
            var rng = run != null
                ? RngStreams.Open(run.runSeed, RngStreams.Treasure, run.step, run.currentNodeId)
                : null;

            var outcome = RoomResolution.Resolve(roomType, rng);
            Last = outcome;

            if (!outcome.Resolves) return false;

            if (outcome.Gold > 0)
            {
                // Through BankPayout rather than touching run.gold, so the
                // credit persists the same way a fight's does.
                RunManager.BankPayout(outcome.Gold);
            }

            if (outcome.HealsPartyToFull) RunEncounter.HealPartyToFull(run);

            return true;
        }


        // The line the map shows, as a TEMPLATE and its arguments rather than
        // as finished text.
        //
        // This returns a UiString because the caller has to assign it through
        // label.Set: UiKitLintTests.LabelTextIsOnlyAssignedThroughUiText fails
        // the build on a direct .text assignment, so that the builder measuring
        // a box and the runtime filling it read the same template. That lint
        // caught this method's first shape, which formatted the string here and
        // handed back the result.
        //
        // Chosen from the OUTCOME's kind rather than from the room type a
        // second time, so the wording cannot drift out of step with what was
        // actually applied.
        public static bool TryMessage(
            RoomResolution.Outcome outcome, out UiString text, out object[] args)
        {
            args = NoArgs;

            switch (outcome.Result)
            {
                case RoomResolution.Kind.Treasure:
                    text = UiStrings.MapRoomTreasure;
                    args = new object[] { outcome.Gold };
                    return true;
                case RoomResolution.Kind.Rest:
                    text = UiStrings.MapRoomRest;
                    return true;
                case RoomResolution.Kind.EventNotBuilt:
                    text = UiStrings.MapRoomEvent;
                    return true;
                case RoomResolution.Kind.ItemNotBuilt:
                    text = UiStrings.MapRoomItem;
                    return true;
                case RoomResolution.Kind.Empty:
                    text = UiStrings.MapRoomEmpty;
                    return true;
                default:
                    // Nothing to say. The caller hides the label rather than
                    // setting it to "", which would leave an empty box the
                    // text-fit audit still has to reason about.
                    text = default;
                    return false;
            }
        }

        private static readonly object[] NoArgs = new object[0];
    }
}
