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
        // RunController.TryFigure's default arm and the test that sweeps
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

    // WHAT THE RUN PANE'S THIS DESCENT COLUMN SAYS: the descent's figures,
    // condensed to what a player checks mid-run.
    //
    // Battle, The Fold and Spoils, and one rule over all of them: EVERY ROW
    // BINDS TO A TRACKED FIELD OR GETS CUT. What is absent, and why:
    //
    //   elapsed / days / turns   nothing anywhere counts them.
    //   floor / room             the lintel prints it above the pane.
    //   physical / other split   a breakdown of damage dealt; the dossier is
    //                            where a character's own numbers live.
    //   shielding, gold held     shielding is not a question a player asks
    //                            mid-run; gold held is the Supplies column's
    //                            own last row.
    //
    // Everything below reads off RunSnapshot or the run's own ledger, both of
    // which are written per fight and survive quitting to the title.
    public static class RunStatRows
    {
        public static readonly IReadOnlyList<RunStatGroupDef> Groups = new[]
        {
            // PER-RUN AND NOT PER-CHARACTER, deliberately: what this answers is
            // "how did the descent go", a party-level question.
            new RunStatGroupDef("Battle", UiStrings.RunStatBattle,
                new RunStatRowDef("damage_dealt", UiStrings.RunStatDamageDealt),
                new RunStatRowDef("damage_taken", UiStrings.RunStatDamageTaken),
                new RunStatRowDef("healed", UiStrings.RunStatHealed),
                new RunStatRowDef("kills", UiStrings.RunStatKills),
                new RunStatRowDef("times_downed", UiStrings.RunStatTimesDowned)),

            new RunStatGroupDef("Fold", UiStrings.RunStatFold,
                new RunStatRowDef("floor", UiStrings.RunStatFloor),
                new RunStatRowDef("rooms_cleared", UiStrings.RunStatRoomsCleared),

                // NOT `step`. That is where the party is standing, and a run
                // that ends is a run that stopped moving -- RunSnapshot keeps
                // deepestStep separately for exactly this question.
                new RunStatRowDef("deepest_room", UiStrings.RunStatDeepestRoom),
                new RunStatRowDef("bosses", UiStrings.RunStatBosses)),

            new RunStatGroupDef("Spoils", UiStrings.RunStatSpoils,
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
