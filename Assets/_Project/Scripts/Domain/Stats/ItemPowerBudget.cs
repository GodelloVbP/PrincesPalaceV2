namespace PrincesPalace.Domain.Stats
{
    // A single comparable "power score" for a weapon's two levers — its flat
    // Attack bonus and its scaling grades — so two different ways of being
    // balanced (a flat-heavy modifier vs. a scaling-heavy one) can be
    // compared on one axis instead of trusted by eye.
    //
    // This is deliberately NOT a universal currency covering every stat in
    // the game (health, resistance, speed, ability scores all move by their
    // own established rates — see AbilityDerivation and CombatMath — and
    // forcing them onto one shared number would mean inventing exchange
    // rates nothing else in the codebase needs or justifies). It answers
    // exactly one question, the one WeaponEntryResolver's own modifier axis
    // exists to raise: at a given tier, is "Sturdy" worth about the same as
    // "Nimble"? A player should choose based on which stat they have, not
    // because one modifier is silently the better pick regardless.
    public static class ItemPowerBudget
    {
        // How many points above neutral a character who has genuinely
        // committed to a stat is assumed to have — the SAME anchor
        // ScalingGrades.PerPoint's own header comment calibrates against
        // ("lands somewhere near 20, ten points over neutral"). Used only to
        // convert a grade into a comparable flat-Attack-equivalent number for
        // this budget; it plays no part in real combat math, which reads the
        // wielder's ACTUAL scores through ScalingProfile.MultiplierFor.
        public const int AssumedCommittedPoints = 10;

        // A scaling grade's worth, in the SAME units as attackBonus: how many
        // points of flat Attack a fully-committed character's grade is worth
        // against a reference Attack value. An S grade (0.10 per point) at 10
        // committed points is worth 1.0x the reference — roughly a second
        // weapon's finished bonus stacked on top, which is the scale a
        // player already reads "S" as promising from ScalingProfile.Describe.
        public static float ScalingGradeWorth(ScalingGrade grade, float referenceAttack)
        {
            return ScalingGrades.PerPoint(grade) * AssumedCommittedPoints * referenceAttack;
        }

        // Total power score for one weapon modifier at one tier: its flat
        // bonus, plus what its scaling profile (summed across every graded
        // stat) is worth against that same weapon's own reference Attack —
        // so a comparison between two modifiers in the same family at the
        // same tier is comparing like against like, not against an arbitrary
        // outside number.
        //
        // spellScaling counts too, appended last with a default so every
        // existing call site (which only ever passed the Attack-axis
        // `scaling`) keeps compiling and keeps comparing the same numbers it
        // always has for content that authors no spellScaling at all — a
        // weapon that spends its whole budget on spell power rather than the
        // swing should not read as artificially cheap next to a sibling that
        // spent the same budget on `scaling` instead.
        public static float TotalPower(int attackBonus, ScalingProfile scaling, float referenceAttack, ScalingProfile spellScaling = default)
        {
            float total = attackBonus;
            foreach (AbilityScore score in AbilityScores.All)
            {
                var grade = scaling[score];
                if (grade != ScalingGrade.None)
                {
                    total += ScalingGradeWorth(grade, referenceAttack);
                }

                var spellGrade = spellScaling[score];
                if (spellGrade != ScalingGrade.None)
                {
                    total += ScalingGradeWorth(spellGrade, referenceAttack);
                }
            }

            return total;
        }

        // The same score for one particular COPY of a weapon, honed to
        // `plus`.
        //
        // Only the flat half moves. Plus scales what an item grants; it does
        // not touch scaling grades, which belong to tier alone — so this
        // deliberately does NOT scale the grade contribution, and a test
        // asserting monotonicity in both arguments is asserting exactly that
        // separation rather than a single blended number.
        //
        // The reference Attack moves with the flat bonus, because a grade's
        // worth is expressed against the weapon's own Attack: a +10 sword
        // scaling at S really is worth more absolute damage than a +0 one.
        public static float TotalPowerAt(int attackBonus, ScalingProfile scaling, int plus, ScalingProfile spellScaling = default)
        {
            int scaledAttack = ItemUpgrade.Apply(attackBonus, plus);
            return TotalPower(scaledAttack, scaling, referenceAttack: scaledAttack, spellScaling: spellScaling);
        }
    }
}
