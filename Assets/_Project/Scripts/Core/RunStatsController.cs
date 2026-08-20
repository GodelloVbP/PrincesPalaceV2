using System.Globalization;
using TMPro;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Fills the Run statistics pane.
    //
    // ONE PLACE where a row's label meets its number, and it is a switch rather
    // than a table of delegates on purpose: the design's rule for this pane is
    // that EVERY ROW BINDS TO A TRACKED FIELD OR GETS CUT, and a switch with no
    // default arm is that rule expressed as code. A key nobody supplied falls
    // through to false, RunStatsTests sweeps every key RunStatRows declares
    // through TryFigure, and a row that stops binding fails a test instead of
    // printing a plausible number.
    //
    // The dossier's Dodge, Carried, Shop-price and Morale rows are what this
    // exists to prevent: four rows that read like data and were furniture.
    public class RunStatsController : MonoBehaviour
    {
        // Keyed rather than index-aligned with RunStatRows.AllRows, the same
        // shape the Options pane's sliders use: the pane says which figure each
        // label wants, so reordering the table cannot silently swap two numbers
        // that both happen to be four digits long.
        [SerializeField] internal string[] valueKeys;
        [SerializeField] internal TMP_Text[] values;

        // What a figure reads while there is no run to describe.
        //
        // The pane is on a run-only tab, so this should be unreachable through
        // the UI -- but Select() takes an index and nothing stops a caller
        // passing this one, and a screen that prints eighteen zeroes for a run
        // that does not exist is a screen claiming a run went badly.
        private const string NoRun = "-";

        private void OnEnable() => Refresh();

        public void Refresh()
        {
            if (values == null || valueKeys == null) return;

            var run = RunManager.Run;
            int count = Mathf.Min(values.Length, valueKeys.Length);

            for (int i = 0; i < count; i++)
            {
                if (values[i] == null) continue;
                values[i].SetContent(Figure(run, valueKeys[i]));
            }
        }

        public static string Figure(RunSnapshot run, string key)
        {
            if (run == null || !run.hasRun) return NoRun;

            // A key with nothing behind it reads as absent rather than as zero.
            // Zero is a claim about the run; this is a claim about the build.
            if (!TryFigure(run, key, out long value)) return NoRun;

            // Grouped, because these run to five figures by the second floor and
            // "8420" is a number you have to count. Invariant rather than the
            // machine's culture: the rest of the UI is not localised, and a
            // German build printing 8.420 beside an English label would be
            // stranger than either alone.
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // THE BINDING. Every key RunStatRows declares has to appear here.
        public static bool TryFigure(RunSnapshot run, string key, out long value)
        {
            value = 0L;
            if (run == null) return false;

            switch (key)
            {
                // ---- Battle: the run ledger, summed across the party --------
                //
                // RunLedger.TotalDamage rather than a fourth hand-rolled sum:
                // it is already the one place that walks the ledger, and it
                // returns long specifically because a long profile passes two
                // billion and an int wraps negative.
                case "damage_dealt": value = RunLedger.TotalDamage(run); return true;
                case "damage_physical": value = SumLedger(run, e => e.physicalDealt); return true;
                case "damage_other": value = SumLedger(run, e => e.otherDealt); return true;
                case "damage_taken": value = SumLedger(run, e => e.damageTaken); return true;
                case "healed": value = SumLedger(run, e => e.healed); return true;
                case "shielded": value = SumLedger(run, e => e.shielded); return true;
                case "kills": value = SumLedger(run, e => e.kills); return true;
                case "times_downed": value = SumLedger(run, e => e.timesDowned); return true;

                // ---- The Fold: how far down it got --------------------------
                case "floor": value = run.floor; return true;
                case "rooms_cleared": value = run.roomsCleared; return true;

                // deepestStep, NOT step. The latter is where the party is
                // standing, and a run that ends is a run that stopped moving.
                case "deepest_room": value = run.deepestStep; return true;
                case "bosses": value = run.bossesKilled?.Count ?? 0; return true;

                // ---- Spoils --------------------------------------------------
                //
                // Held and earned are different numbers the moment anything is
                // spent, and a shop exists. Showing one of them makes the other
                // unanswerable.
                case "gold_held": value = run.gold; return true;
                case "gold_earned": value = run.goldEarned; return true;
                case "exp_earned": value = run.expEarned; return true;
                case "relics": value = run.relicIds?.Count ?? 0; return true;

                // Stacks counted by their contents, not by their entries: three
                // salves in one slot are three items in the pack, which is the
                // question the row asks.
                case "pack": value = PackCount(run); return true;

                default: return false;
            }
        }

        private static long SumLedger(RunSnapshot run, System.Func<RunLedgerEntry, long> pick)
        {
            if (run.ledger == null) return 0L;

            long total = 0L;
            foreach (var entry in run.ledger)
            {
                if (entry != null) total += pick(entry);
            }

            return total;
        }

        private static long PackCount(RunSnapshot run)
        {
            if (run.inventory == null) return 0L;

            long total = 0L;
            foreach (var entry in run.inventory)
            {
                if (entry != null && entry.count > 0) total += entry.count;
            }

            return total;
        }
    }
}
