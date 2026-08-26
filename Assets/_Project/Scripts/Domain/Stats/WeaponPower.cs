namespace PrincesPalace.Domain.Stats
{
    // THE damage number -- balance redesign Phase 3 (D3).
    //
    //     WP = round(attackAtTier x (1 + 0.15 x plus))
    //
    // Pure Domain arithmetic, kept OFF ItemDefinition (a ScriptableObject,
    // Core-layer only) even though it has exactly one production caller
    // there (WeaponPowerAt) -- every other piece of this game's hone math
    // (ItemUpgrade) and its rounding convention (Rounding) already lives
    // here engine-free, and putting the formula itself in Core would make
    // it untestable from the EditMode assembly, which references Domain
    // only (see PrincesPalace.Domain.Tests.asmdef).
    //
    // DELIBERATELY NOT ItemUpgrade.Apply, even though Apply exists for
    // exactly this pairing (a stat, and a plus). Apply rounds UP (ceiling)
    // on any positive fraction, so a +1 is never a lie on a 1-point armour
    // stat -- see its own header. WeaponPower rounds to NEAREST instead
    // (away from zero on the tie), because it is the authored formula the
    // balance redesign was tuned against and WeaponDamageTests pins
    // literals computed that way. The two conventions only diverge below
    // the halfway point of a plus step, but that is exactly where they
    // would silently disagree if this reused Apply.
    public static class WeaponPower
    {
        public static int Compute(int attackAtTier, int plus)
        {
            return Rounding.AwayFromZero(attackAtTier * ItemUpgrade.MultiplierFor(plus));
        }

        // BALANCE REDESIGN PHASE 6 (D7.2): the number a player actually cares
        // about on a weapon card -- not WeaponPower alone, which says nothing
        // about whether THIS character's scores make the weapon hit hard or
        // limp, and not the bare "STR A" grade either, which says how hard
        // without saying how much. This is both, combined exactly the way a
        // real swing combines them: `scaling` is the candidate weapon's own
        // ScalingProfile (its main-hand grades), read against `viewerScores`
        // -- the character whose tooltip this is, not necessarily whoever has
        // the weapon equipped right now.
        //
        // Same shape as CombatMath.ScaledAttack's WeaponPower term
        // (WeaponPower x MultiplierFor, rounded away from zero) rather than a
        // second convention: a lone ScalingProfile converts to a ScalingSet
        // with only the main-hand slot populated (see ScalingSet's implicit
        // operator), which is exactly what a plain swing rides, so this reads
        // the same number DetailForStrike's POWER row would show for a
        // character actually wielding this weapon.
        public static int DisplayDamage(int weaponPower, ScalingProfile scaling, AbilityScoreBlock viewerScores)
        {
            return Rounding.AwayFromZero(weaponPower * scaling.MultiplierFor(viewerScores));
        }
    }
}
