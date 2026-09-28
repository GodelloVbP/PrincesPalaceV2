namespace PrincesPalace.Domain.Combat.Session
{
    // WHAT ONE COMBATANT TOOK from a beat that landed on several.
    //
    // CombatBeat.Amount is one number, and one number is the right shape for
    // the overwhelming majority of beats: a swing, a bolt, a heal. It is the
    // wrong shape for a sweep, and the way it was wrong is worth stating
    // because it looked correct on screen. ResolveDamageAll recorded the
    // LARGEST single hit as the beat's Amount and the view drew that one number
    // over the beat's primary target -- so three enemies with three different
    // resistances took three different amounts of health, and the player was
    // shown one figure, on one of them, that matched at most one of the three.
    // A Fire-weak rat and a Fire-resistant beetle in the same encounter is
    // exactly the case Cinderfault's two typed packets exist to create.
    //
    // A LIST BESIDE Amount RATHER THAN INSTEAD OF IT. Amount still means what
    // it always meant and every consumer that reads it -- the hit-stop weight,
    // the stage shake, WantsContactFx -- is unchanged. This is additive: empty
    // for every beat that hits one thing, and read in preference to Amount only
    // where a per-target answer is what the caller actually wanted.
    //
    // A READONLY STRUCT, unlike CombatBeat itself: nothing serialises this and
    // nothing mutates a result after the blow that produced it, so the type can
    // say so. See SpellPresentation for why the values that DO cross Unity's
    // serialisers are mutable classes instead.
    public readonly struct BeatTargetResult
    {
        public readonly CombatantState Target;

        // What this one combatant took (or was healed). Zero on a dodge, which
        // is why Missed is its own field and not inferred -- the same
        // discipline CombatBeat.Missed and DamagePipeline.Outcome.IsMiss both
        // follow, and for the same reason: a landed hit reduced to nothing and
        // a swing that never connected are different events.
        public readonly int Amount;
        public readonly bool Missed;

        // This target's hit was a critical hit (DamagePipeline.Outcome.IsCrit)
        // -- per target, because every target of a sweep rolls its own.
        public readonly bool Crit;

        public BeatTargetResult(CombatantState target, int amount, bool missed, bool crit = false)
        {
            Target = target;
            Amount = amount;
            Missed = missed;
            Crit = crit && !missed;
        }
    }
}
