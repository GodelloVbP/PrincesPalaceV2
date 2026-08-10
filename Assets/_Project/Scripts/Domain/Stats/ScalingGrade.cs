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
        public static string Letter(ScalingGrade grade)
        {
            return grade == ScalingGrade.None ? "—" : grade.ToString();
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
