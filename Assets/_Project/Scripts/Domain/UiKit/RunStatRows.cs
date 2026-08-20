using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    public readonly struct RunStatRowDef
    {
        // What the controller looks the figure up by.
        //
        // A KEY RATHER THAN A FIELD REFERENCE, and not by preference: this
        // assembly cannot see RunSnapshot. Domain has no engine references and
        // no reference to Core, which is where the run and its ledger live, so
        // the table can name a row and the controller has to supply it. Same
        // seam OptionRows uses for GameSettings.
        //
        // The cost is that a key with nothing behind it compiles. That is what
        // RunStatsController.TryValue's default arm and the test that sweeps
        // every key are for -- the rule below is mechanised rather than
        // remembered.
        public readonly string Key;

        public readonly UiString Label;

        public RunStatRowDef(string key, UiString label)
        {
            Key = key;
            Label = label;
        }
    }

    public readonly struct RunStatGroupDef
    {
        public readonly string Key;
        public readonly UiString Heading;
        public readonly IReadOnlyList<RunStatRowDef> Rows;

        public RunStatGroupDef(string key, UiString heading, params RunStatRowDef[] rows)
        {
            Key = key;
            Heading = heading;
            Rows = rows;
        }
    }

    // WHAT THE RUN STATISTICS PANE SAYS.
    //
    // The design asked for eighteen figures in three groups -- Battle, The
    // Fold, Spoils -- under a header of floor/room, elapsed, days and turns,
    // and set one rule over the whole thing: EVERY ROW BINDS TO A TRACKED FIELD
    // OR GETS CUT. Seventeen figures survive and the header does not, and both
    // of those are that rule doing its job:
    //
    //   elapsed / days / turns   nothing anywhere counts them. A run has a
    //                            step and a floor and no clock at all.
    //   floor / room             tracked, but the LINTEL ALREADY PRINTS IT,
    //                            two inches above this pane. A second copy is
    //                            not more information, it is one more thing
    //                            that can disagree.
    //
    // So the pane is three cards and no header. The tab is called RUN
    // STATISTICS; it does not also need a title telling it so.
    //
    // Everything below reads off RunSnapshot or the run's own ledger, both of
    // which are written per fight and survive quitting to the title -- which is
    // the whole reason RunSnapshot carries a history section at all.
    public static class RunStatRows
    {
        public static readonly IReadOnlyList<RunStatGroupDef> Groups = new[]
        {
            // Off the run ledger, summed across the party.
            //
            // PER-RUN AND NOT PER-CHARACTER, deliberately. RunLedgerEntry is
            // keyed by character and a table of six rows by four heroes is a
            // spreadsheet, not a summary -- and the dossier is where a single
            // character's numbers belong. What this pane answers is "how did
            // the descent go", which is a party-level question.
            new RunStatGroupDef("Battle", UiStrings.RunStatBattle,
                new RunStatRowDef("damage_dealt", UiStrings.RunStatDamageDealt),
                new RunStatRowDef("damage_physical", UiStrings.RunStatDamagePhysical),
                new RunStatRowDef("damage_other", UiStrings.RunStatDamageOther),
                new RunStatRowDef("damage_taken", UiStrings.RunStatDamageTaken),
                new RunStatRowDef("healed", UiStrings.RunStatHealed),
                new RunStatRowDef("shielded", UiStrings.RunStatShielded),
                new RunStatRowDef("kills", UiStrings.RunStatKills),
                new RunStatRowDef("times_downed", UiStrings.RunStatTimesDowned)),

            // How far down the fold the descent got.
            new RunStatGroupDef("Fold", UiStrings.RunStatFold,
                new RunStatRowDef("floor", UiStrings.RunStatFloor),
                new RunStatRowDef("rooms_cleared", UiStrings.RunStatRoomsCleared),

                // NOT `step`. That is where the party is standing, and a run
                // that ends is a run that stopped moving -- RunSnapshot keeps
                // deepestStep separately for exactly this question.
                new RunStatRowDef("deepest_room", UiStrings.RunStatDeepestRoom),
                new RunStatRowDef("bosses", UiStrings.RunStatBosses)),

            new RunStatGroupDef("Spoils", UiStrings.RunStatSpoils,
                new RunStatRowDef("gold_held", UiStrings.RunStatGoldHeld),

                // Held and earned are different numbers as soon as anything is
                // spent, and a shop exists. Showing only one of them makes the
                // other unanswerable.
                new RunStatRowDef("gold_earned", UiStrings.RunStatGoldEarned),
                new RunStatRowDef("exp_earned", UiStrings.RunStatExpEarned),
                new RunStatRowDef("relics", UiStrings.RunStatRelics),
                new RunStatRowDef("pack", UiStrings.RunStatPack)),
        };

        // Flattened in build order, so the screen's node lists and the
        // controller's arrays agree on index without either counting groups.
        public static IReadOnlyList<RunStatRowDef> AllRows
        {
            get
            {
                var rows = new List<RunStatRowDef>();
                foreach (var group in Groups) rows.AddRange(group.Rows);
                return rows;
            }
        }

        public static int RowCount => AllRows.Count;
    }
}
