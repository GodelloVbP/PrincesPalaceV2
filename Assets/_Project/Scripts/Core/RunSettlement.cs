using System.Collections.Generic;
using PrincesPalace.Domain.Economy;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // Closing the books on a run that has ended.
    //
    // Runs BEFORE RunManager.EndRun, always, because EndRun replaces the
    // snapshot with an empty one -- taking the ledger, the boss list and the
    // unbanked gold with it. Anything owed has to be paid while the evidence
    // still exists.
    //
    // The result is returned as well as applied, because the defeat screen has
    // no way to work any of it out afterwards: by the time it draws, the run it
    // is describing is gone.
    public static class RunSettlement
    {
        public sealed class Result
        {
            // Paid out, and already on the wallet by the time you hold this.
            public int EmbersEarned;
            public readonly List<string> NewBosses = new List<string>();

            // Forfeited. Gold lives on the run and is destroyed with it -- the
            // wager CurrencyType describes, finally enforced by something
            // rather than only documented.
            public int GoldLost;

            // Kept, for a screen that wants to say what survived.
            public int RoomsCleared;
            public int DeepestStep;
            public int ExpEarned;
            public List<RunLedgerEntry> Ledger = new List<RunLedgerEntry>();
        }

        public static Result Settle(SaveData save, RunSnapshot run)
        {
            var result = new Result();
            if (run == null) return result;

            result.RoomsCleared = run.roomsCleared;
            result.DeepestStep = run.deepestStep;
            result.ExpEarned = run.expEarned;
            result.Ledger = run.ledger ?? new List<RunLedgerEntry>();

            // What the run was carrying and never banked. Reported whether or
            // not there is a save to charge it against, so a screen can say it
            // either way.
            result.GoldLost = run.gold > 0 ? run.gold : 0;

            if (save == null) return result;

            // FOLDED INTO THE LIFETIME TOTALS before the run is discarded.
            // Runs here rather than in EndRun because this is already the
            // "close the books before the evidence is thrown away" pass, and
            // splitting that responsibility across two places is how one of
            // them ends up forgotten.
            save.lifetimeRoomsCleared += run.roomsCleared;
            save.lifetimeDamageDealt += RunLedger.TotalDamage(run);
            save.lifetimeRunsEnded++;
            if (run.deepestStep > save.lifetimeDeepestStep) save.lifetimeDeepestStep = run.deepestStep;

            save.defeatedBossIds ??= new List<string>();

            // ONE ember per boss never killed before. The run knows what it
            // killed; only the save knows what was new.
            var fresh = EmberPayout.NewBosses(run.bossesKilled, save.defeatedBossIds);
            result.NewBosses.AddRange(fresh);
            result.EmbersEarned = EmberPayout.EmbersFor(fresh.Count);

            // PAID TO THE SQUAD THAT RAN, one each, not into a shared pool.
            //
            // Each member gets the full amount rather than a split: the payout
            // is per unique boss and the point is that the characters you
            // actually field progress. Splitting would make bringing a second
            // character a tax on the first, which is the opposite of the
            // intent, and a character left in the hub still earns nothing.
            if (result.EmbersEarned > 0)
            {
                foreach (var character in save.ActiveSquad())
                {
                    if (character != null) character.embers += result.EmbersEarned;
                }
            }

            // Recorded in the SAME pass that paid for them. Paying without
            // recording would let the same boss pay again on the next run,
            // which is the meta-progression version of the money printer.
            foreach (var id in fresh)
            {
                save.defeatedBossIds.Add(id);
            }

            SaveSlotManager.SaveCurrent();
            return result;
        }
    }
}
