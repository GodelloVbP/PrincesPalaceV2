using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PrincesPalace.Domain.Combat.Session
{
    // The in-fight speed presets, and the one number each row is allowed to
    // author.
    //
    // DISPLAY IS THE ONLY AUTHORED FIELD. Everything else -- the multiplier
    // FightBeatPlayer actually applies, the string the Options row shows --
    // is derived from it, because a row authored as two independent numbers
    // (a label and a factor that happens to match it) is a row that can go
    // out of sync the day somebody edits one and not the other. "Add 3x" is
    // meant to cost one line, and it only stays one line if there is nothing
    // else to keep consistent with it.
    //
    // THE BASELINE IS 1.5, NOT 1. Today's shipped pace -- the one the fight
    // has always run at, internally a factor of 1.0 -- reads to the owner as
    // too fast, and is being relabelled "1.5x" so the player's honest
    // complaint ("turn it down to 1x") lands on a genuinely slower row
    // instead of on the number that was already running. Multiplier is
    // Display / TodaysPaceDisplay for exactly this reason: the row that
    // reproduces today's speed is the one whose multiplier comes out to 1,
    // wherever it sits in the table.
    //
    // NEAREST TIES TO THE SLOWER ROW because migrating a stored value that
    // falls exactly between two rows (a removed row, most likely) into the
    // faster one changes what the player asked for in the direction they did
    // not ask for; the slower row is the smaller, more conservative surprise.
    public static class BattleSpeed
    {
        // Today's shipped pace, relabelled. Not a preset row itself unless a
        // row happens to carry this value -- see the header.
        public const float TodaysPaceDisplay = 1.5f;

        // Owner's call, not a derived choice: the fight opens on this until
        // the player says otherwise.
        public const float DefaultDisplay = 1f;

        public readonly struct Preset
        {
            // THE ONE AUTHORED NUMBER. Everything else on this type reads it.
            public readonly float Display;

            public Preset(float display)
            {
                Display = display;
            }

            public float Multiplier => Display / TodaysPaceDisplay;

            // "0.##" rather than a fixed decimal count so "1" prints as "1x",
            // not "1.00x" -- the row label is this string with an "x" appended
            // (UiStrings.OptionsBattleSpeedValue), and the sample in the plan
            // this table implements is "1.5x".
            public string DisplayNumber => Display.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // Adding a row is one line here and nowhere else: no new string, no
        // new switch case, no test edit. Keep it ascending -- Nearest below
        // depends on that order for its end-clamping and does not re-sort.
        private static readonly Preset[] _rows =
        {
            new Preset(0.5f),
            new Preset(1f),
            new Preset(1.5f),
            new Preset(2f),
        };

        // ReadOnlyCollection wrapping the array, never the array itself: a
        // caller that casts Rows back to Preset[] to mutate a row in place
        // must get null, not a live handle to the table everything else reads.
        // Exposed through the INTERFACE below, not this concrete field's type
        // -- "Rows as Preset[]" only compiles to a null cast, rather than a
        // compile error, when the call site's static type is an interface
        // that Preset[] also implements. ReadOnlyCollection<Preset> itself
        // has no such relation to Preset[] and would fail to compile.
        private static readonly ReadOnlyCollection<Preset> _readOnlyRows = Array.AsReadOnly(_rows);

        public static IReadOnlyList<Preset> Rows => _readOnlyRows;

        public static Preset Default { get; } = FindDefault();

        private static Preset FindDefault()
        {
            foreach (Preset row in _rows)
            {
                if (row.Display == DefaultDisplay)
                    return row;
            }

            throw new InvalidOperationException(
                "BattleSpeed.DefaultDisplay must equal some row's Display -- it names one of them, not a fifth value.");
        }

        public static int IndexOf(Preset preset)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                if (_rows[i].Display == preset.Display)
                    return i;
            }

            return -1;
        }

        // Contract 7: absent is the caller's problem (GameSettings.Load
        // decides what "absent" means before it gets here); everything else
        // funnels through this one rule.
        //
        // Non-finite (NaN, +-Infinity) -> the default row, the same posture
        // as a missing value: neither is a display number that was ever on
        // the table.
        //
        // Otherwise: the row with the smallest |Display - display|; a tie
        // goes to the lower-Display (slower) row -- see the header; a value
        // below the first row or above the last snaps to that end rather
        // than extrapolating past a row that does not exist.
        public static Preset Nearest(float display)
        {
            if (float.IsNaN(display) || float.IsInfinity(display))
                return Default;

            if (display <= _rows[0].Display)
                return _rows[0];

            if (display >= _rows[_rows.Length - 1].Display)
                return _rows[_rows.Length - 1];

            Preset nearest = _rows[0];
            float bestDifference = float.MaxValue;

            foreach (Preset row in _rows)
            {
                float difference = Math.Abs(row.Display - display);

                // Strictly less-than only: rows are visited in ascending
                // Display order, so the first row to reach a given
                // difference is already the slower one, and a later row
                // tying it must not replace it.
                if (difference < bestDifference)
                {
                    nearest = row;
                    bestDifference = difference;
                }
            }

            return nearest;
        }
    }
}
