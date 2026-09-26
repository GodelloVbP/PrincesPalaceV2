using System;

namespace PrincesPalace.Domain.Stats
{
    // How hard one weapon or spell rides one ability score.
    //
    // Letters rather than numbers because the number is not the point: what a
    // player needs off a reward card in one second is "this sword wants the
    // stat I have", and S/A/B/C/D/E answers that faster than 0.10/0.07/0.05
    // ever will. The numbers live in ScalingGrades, in exactly one place, so
    // retuning the whole game's scaling is six constants.
    //
    // None is not the bottom of the ladder, it is OFF: a weapon that does not
    // care about Charisma has no Charisma grade rather than a terrible one.
    // Kept as the zero value so a profile nobody has authored scales at
    // exactly 1.0x and every existing weapon, enemy and spell behaves as it
    // did before this system existed.
    public enum ScalingGrade
    {
        None = 0,
        E = 1,
        D = 2,
        C = 3,
        B = 4,
        A = 5,
        S = 6,
    }

    public static class ScalingGrades
    {
        // The ladder as consecutive integers, which is what lets a grade be
        // INTERPOLATED between an authored floor and ceiling the same way a
        // set piece's stats are — see WeaponEntryResolver. A weapon is
        // authored as "C at plus 0, S at plus 10" and every level between
        // falls out, rather than as eleven hand-written letters that drift.
        public const int Highest = (int)ScalingGrade.S;

        // How much of the base damage one point of the stat ABOVE NEUTRAL
        // adds, as a fraction.
        //
        // Calibrated against the reachable range rather than against nothing:
        // a character who has genuinely committed to a stat lands somewhere
        // near 20, which is ten points over neutral, so an S weapon in the
        // right hands doubles its damage and a C weapon adds a third. That
        // spread is the whole design — big enough that picking the sword that
        // matches your build is the correct play, small enough that picking
        // the other one is a setback rather than a dead run.
        public static float PerPoint(ScalingGrade grade)
        {
            switch (grade)
            {
                case ScalingGrade.S: return 0.100f;
                case ScalingGrade.A: return 0.070f;
                case ScalingGrade.B: return 0.050f;
                case ScalingGrade.C: return 0.030f;
                case ScalingGrade.D: return 0.015f;
                case ScalingGrade.E: return 0.007f;
                case ScalingGrade.None: return 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade), grade, "ScalingGrades has no coefficient for this grade.");
            }
        }

        // What the UI prints. An em dash for None, because a blank cell reads
        // as "not filled in yet" while a dash reads as "deliberately nothing".
        //
        // PLAIN TEXT: authoring errors and logs. Anything a PLAYER reads goes
        // through Display below instead.
        public static string Letter(ScalingGrade grade)
        {
            return grade == ScalingGrade.None ? "—" : grade.ToString();
        }

        // The face every player-facing grade letter is drawn in. Matches the
        // generated asset name in TmpBootstrap.Typography.cs (the
        // FunctionalHeading role's face), and Core.GradeFace resolves it at
        // runtime from the shipped TTF -- see there for why a runtime font.
        public const string DisplayFontName = "SourceSans3-SemiBold SDF";

        // A grade as the player sees it: the letter in its own face.
        //
        // The UI's ChakraPetch draws D and 0, B and 8, S and 5 as one squared
        // glyph each, so "INT-D" read as "INT-0" and "STR-B" as a stat of 8
        // (QA 2026-09-26, twice). No wording or colour fixes a glyph -- a
        // gold 0 is still a 0 -- so the letter swaps face instead, to the
        // project's own humanist sans, where D is a stemmed bowl and 0 an
        // oval. Only the letter changes: the score name, the multiplier and
        // every digit on the line stay in the UI font, which is what makes
        // the letter stand apart from them.
        //
        // The dash for None stays plain: it cannot be mistaken for a digit.
        public static string Display(ScalingGrade grade)
        {
            return grade == ScalingGrade.None
                ? Letter(grade)
                : $"<font=\"{DisplayFontName}\">{grade}</font>";
        }

        public static ScalingGrade FromIndex(int index)
        {
            if (index < (int)ScalingGrade.None)
            {
                return ScalingGrade.None;
            }

            return index > Highest ? ScalingGrade.S : (ScalingGrade)index;
        }

        // Parses an authored letter. Case-insensitive, and empty or "-" or
        // "none" all mean OFF — content should be able to say "this one does
        // not scale on that" in whichever way reads best in the file.
        public static bool TryParse(string raw, out ScalingGrade grade)
        {
            grade = ScalingGrade.None;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            string trimmed = raw.Trim();
            if (trimmed == "-" || trimmed == "—" || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Enum.TryParse(trimmed, ignoreCase: true, out grade) && Enum.IsDefined(typeof(ScalingGrade), grade);
        }
    }
}
