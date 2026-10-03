using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Rewards
{
    // Which screen a finished fight opens (docs/PLAN_BUGS_QA.md C1).
    public enum FightEndScreen
    {
        // The settlement ended the run.
        Defeat,

        // The fight belonged to a run that goes on: the Reckoning, in its
        // outcome form when the settlement paid nothing.
        Reckoning,

        // No run (a preview, a test harness): the fight's own Continue.
        Continue,
    }

    // What the outcome-form Reckoning calls the ending.
    public enum FightEnding
    {
        Victory,
        Endured,
        Fell,
    }

    // THE ROUTING, decided from two facts and never from live run state: whether
    // the fight belonged to a run (captured when it started) and what the
    // settlement said (whether it ended the run). Settlement can call EndRun
    // before routing runs, so RunManager.HasRun is already false after a fatal
    // loss and cannot be what decides it.
    //
    // TOTAL OVER FightEndReason: every value has exactly one row, and a reason
    // with no row throws rather than falling through to a default screen.
    public static class FightEndRouting
    {
        public readonly struct Row
        {
            public readonly FightEndReason Reason;

            // False only for None: the fight is not over and nothing routes.
            public readonly bool Over;

            // Only a party that went down can be the loss that ends a run.
            public readonly bool PartyDown;

            public readonly FightEnding Ending;

            public Row(FightEndReason reason, bool over, bool partyDown, FightEnding ending)
            {
                Reason = reason;
                Over = over;
                PartyDown = partyDown;
                Ending = ending;
            }
        }

        public static readonly IReadOnlyList<Row> Rows = new[]
        {
            new Row(FightEndReason.None, over: false, partyDown: false, FightEnding.Victory),
            new Row(FightEndReason.Defeated, over: true, partyDown: false, FightEnding.Victory),
            new Row(FightEndReason.Survived, over: true, partyDown: false, FightEnding.Endured),
            new Row(FightEndReason.Fell, over: true, partyDown: true, FightEnding.Fell),
        };

        public static Row RowFor(FightEndReason reason)
        {
            foreach (var row in Rows)
            {
                if (row.Reason == reason) return row;
            }

            throw new ArgumentOutOfRangeException(nameof(reason), reason,
                "FightEndRouting has no row for this ending; add one to Rows");
        }

        // First match wins: Defeat, then the Reckoning, then Continue.
        public static FightEndScreen Route(FightEndReason reason, bool settlementEndedRun, bool belongedToRun)
        {
            var row = RowFor(reason);
            if (!row.Over)
            {
                throw new InvalidOperationException("a fight that is not over has no screen to route to");
            }

            if (row.PartyDown && settlementEndedRun) return FightEndScreen.Defeat;
            if (belongedToRun) return FightEndScreen.Reckoning;
            return FightEndScreen.Continue;
        }
    }
}
