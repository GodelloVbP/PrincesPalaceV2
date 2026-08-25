namespace PrincesPalace.Domain.Combat.Session
{
    // How hard a beat hit, and how long the game holds still to say so.
    //
    // HIT-STOP: everything freezes for a moment at the point of contact -- the
    // attacker mid-swing, the target mid-flinch, the number already on screen.
    // The eye reads the pause as the blow meeting resistance, which is the one
    // thing a drawn frame cannot show and a timing change can. It is most of
    // why Darkest Dungeon's cut-outs and Vlambeer's flat sprites hit harder
    // than a lot of real animation, and it costs no art at all.
    //
    // IN DOMAIN, not beside the coroutine that yields on it, and for the reason
    // Domain/Stage/FrameHoldCurve and Domain/Ambience/FlickerCurve are here:
    // this is arithmetic about a number, it decides how a whole round is paced,
    // and a rule that cannot be reached from an EditMode test is a rule nothing
    // checks. FightBeatPlayer keeps the yielding, which genuinely needs an
    // engine.
    //
    // (An earlier draft of this comment cited "DamagePopupCurve" as the
    // precedent. There is no such type -- DamagePopup's curves are in Core and
    // reachable only from PlayMode, which is the counter-example rather than
    // the precedent. FlickerCurve is the real one: its own header records
    // moving to Domain because in Core it was "pure, seam-shaped, and
    // unreachable by every test in the project".)
    public static class HitStop
    {
        // What fraction of a health bar counts as "as hard as it gets".
        //
        // A quarter, and the ceiling matters more than the number: past it,
        // every effect keyed off this would keep climbing for hits the player
        // already reads as enormous. The useful range of the scale is the
        // distance between a scratch and a real blow, not between enormous and
        // more enormous.
        public const float FullWeightFraction = 0.25f;

        // Every landed blow gets SOME stop. The effect is punctuation, and
        // punctuation that only appears on long sentences reads as an accident
        // rather than as emphasis.
        public const float MinSeconds = 0.055f;

        // And none gets much.
        //
        // RAISED FROM 0.10 ON REQUEST, after the first pass went in and the
        // blows still did not land. The old comment argued that past a tenth of
        // a second the player reads a hitch rather than weight; against this
        // game's pacing -- a whole beat is 0.45s and a stance 0.48s on top of
        // it -- a tenth was simply lost inside the animation it was meant to
        // punctuate. Still a judgement about perception rather than a
        // derivation, which is why it stays a named constant with a test on it.
        public const float MaxSeconds = 0.18f;

        // HOW MUCH OF AN EFFECT A GIVEN WEIGHT BUYS, and the second half of
        // the same request.
        //
        // Weight is linear in damage, and the honest linear mapping is why
        // amplitude alone was not enough: an ordinary exchange on this roster
        // sits at 0.1 to 0.35 of the ceiling -- a rat swing lands 5 into a
        // 200-point bar -- so the typical hit was reading at a tenth of the
        // strength every one of these effects was tuned at. Doubling the
        // ceilings would only have made the rare enormous hit twice as loud
        // while leaving the common one exactly as limp.
        //
        // A SQUARE ROOT, so the curve is steep where the fights actually are
        // and flat where the number was already obviously huge: a 0.15 hit now
        // reads at 0.39, a 0.5 hit at 0.71, and full weight is unchanged at 1.
        // Monotonic, so a harder hit can never register as softer.
        //
        // SHARED, and that is the point of it being here rather than in each
        // effect: the stop, the stage kick and the target's squash are three
        // statements about one blow, and three separate curves would let them
        // disagree about how hard it was.
        public static float Response(float weight)
        {
            if (weight <= 0f) return 0f;

            return weight >= 1f ? 1f : (float)System.Math.Sqrt(weight);
        }

        // How hard this beat hit, 0..1.
        //
        // AGAINST MAX HEALTH, NOT REMAINING. The same blow is the same size
        // whether it lands first or last; dividing by what is left would make
        // every finishing blow read as apocalyptic and every opening one as a
        // tap.
        //
        // A heal weighs nothing. It is not a small impact, it is a different
        // event, and shaking the stage for one would teach the player that the
        // shake means nothing.
        public static float Weight(int amount, int maxHealth, bool isHealing)
        {
            if (isHealing || amount <= 0 || maxHealth <= 0) return 0f;

            float ceiling = maxHealth * FullWeightFraction;
            if (ceiling <= 0f) return 0f;

            float weight = amount / ceiling;
            return weight < 0f ? 0f : (weight > 1f ? 1f : weight);
        }

        // Through Response, not off raw weight, so the freeze grows on the
        // same curve the shake and the squash do.
        public static float SecondsFor(float weight)
        {
            if (weight <= 0f) return 0f;

            return MinSeconds + (MaxSeconds - MinSeconds) * Response(weight);
        }
    }
}
