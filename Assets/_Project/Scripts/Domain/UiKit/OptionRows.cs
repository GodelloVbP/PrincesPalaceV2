using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    public enum OptionKind
    {
        // A bar you click or drag. 0..1, continuous.
        Slider,

        // A value with a previous and a next. Discrete, and the only shape that
        // works for a list like "1920 x 1080" where the steps are not numbers.
        Stepper,
    }

    public readonly struct OptionRowDef
    {
        public readonly string Key;
        public readonly UiString Label;
        public readonly OptionKind Kind;

        // A quiet line under the label, or default. Used where a control is
        // honest about doing less than it looks like it does.
        public readonly UiString Note;

        public bool HasNote => Note.IsValid;

        public OptionRowDef(string key, UiString label, OptionKind kind, UiString note = default)
        {
            Key = key;
            Label = label;
            Kind = kind;
            Note = note;
        }
    }

    public readonly struct OptionGroupDef
    {
        public readonly string Key;
        public readonly UiString Heading;
        public readonly IReadOnlyList<OptionRowDef> Rows;

        // Which column it lands in.
        public readonly int Column;

        public OptionGroupDef(string key, UiString heading, int column, params OptionRowDef[] rows)
        {
            Key = key;
            Heading = heading;
            Column = column;
            Rows = rows;
        }
    }

    // THE options screen's contents.
    //
    // EVERY ROW BINDS TO A REAL SETTING. That is the design pass's own rule --
    // stated for Run statistics, applied here for the same reason: a control
    // that stores nothing is worse than an absent one, because the player moves
    // it, believes something changed, and is wrong.
    //
    // The design asked for six groups. Three survive, because GameSettings
    // now holds six values (docs/PLAN_BATTLE_SPEED.md added BattleSpeed) and
    // nothing else exists to bind to. What was cut and what each would need
    // is written down in docs/handoffs/system_menu/REMAINING.md rather than
    // shipped as furniture:
    //
    //   Readability (text size, tooltip delay)  - neither value exists
    //   Keybinds                                - no rebind screen and no
    //                                             keybind storage; the design
    //                                             lists that screen as open
    //   Language                                - no localisation table, so the
    //                                             stepper would have one entry,
    //                                             which is not a choice
    //   V-sync                                  - not stored; the frame limit
    //                                             below is the real setting
    //                                             next to it
    //
    // Audio is two sliders rather than three for the same reason: there are two
    // channels. AudioLevels is a per-sound gain table, not a third bus.
    public static class OptionRows
    {
        public static readonly IReadOnlyList<OptionGroupDef> Groups = new[]
        {
            new OptionGroupDef("Audio", UiStrings.OptionsAudio, column: 0,
                new OptionRowDef("sound", UiStrings.OptionsSound, OptionKind.Slider),

                // The note is not decoration. GameSettings stores this and
                // nothing reads it, because there is no music yet, and its own
                // header says the label should say so rather than pretend.
                new OptionRowDef("music", UiStrings.OptionsMusic, OptionKind.Slider,
                    note: UiStrings.OptionsMusicNote)),

            new OptionGroupDef("Display", UiStrings.OptionsDisplay, column: 0,
                new OptionRowDef("resolution", UiStrings.OptionsResolution, OptionKind.Stepper),
                new OptionRowDef("window", UiStrings.OptionsWindow, OptionKind.Stepper),
                new OptionRowDef("fps", UiStrings.OptionsFrameLimit, OptionKind.Stepper)),

            // docs/PLAN_BATTLE_SPEED.md: the one Gameplay setting that binds
            // to a real value. The note says what the design's own footer
            // now also says for this one row specifically -- a stepped
            // battle speed does not retime a beat already under way (contract
            // 4), so "applies immediately" would be wrong here even though
            // it is true of every other row on this screen.
            new OptionGroupDef("Gameplay", UiStrings.OptionsGameplay, column: 0,
                new OptionRowDef("battlespeed", UiStrings.OptionsBattleSpeed, OptionKind.Stepper,
                    note: UiStrings.OptionsBattleSpeedNote)),
        };

        // Flattened, in build order, so the controller's arrays and the tree
        // agree on index without either counting groups.
        public static IReadOnlyList<OptionRowDef> AllRows
        {
            get
            {
                var rows = new List<OptionRowDef>();
                foreach (var group in Groups) rows.AddRange(group.Rows);
                return rows;
            }
        }

        public static int RowCount => AllRows.Count;
    }
}
