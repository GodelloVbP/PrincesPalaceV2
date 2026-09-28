namespace PrincesPalace.Domain.Combat.Session
{
    // HOW FAR A BLOW TRAVELS TO REACH WHAT IT HITS.
    //
    // The vocabulary a skill authors and the view obeys, in Domain because it
    // is a property of the ACTION -- a slam is a slam whether or not the actor
    // has art -- and because the alternative was the view guessing from a
    // stance name.
    public enum StageApproach
    {
        // Rooted. A cast, and any plain attack whose art is itself a stationary
        // pose (the Golem's ground slam, whose "attack" stance is a byte-for-
        // byte alias of its "cast" one).
        Hold = 0,

        // Leans in. A fraction of the way to the target and back -- the
        // ordinary melee swing, and what every plain attack has always done.
        Lunge = 1,

        // Closes the distance FIRST, then strikes, then walks it back.
        //
        // The difference from Lunge is not the distance, it is the ORDER: a
        // lunge is the swing, so the two happen at once, while this arrives
        // before the stance so much as begins. That is what "step in front of
        // it and then bring the thing down" looks like, and it is the only one
        // of the three that costs the beat any extra time.
        Close = 2,

        // Crosses the distance DURING the swing and connects on the impact
        // frame -- a committed rush rather than Lunge's lean.
        //
        // Like Lunge in ORDER (the travel and the stance run together, not one
        // then the other like Close) but nearly the whole way in DISTANCE, and
        // timed so the figure is at full extent exactly when the blow lands.
        // The bump falls out of that timing: the target's recoil, squash and
        // hit-stop already fire at impact for any damaging beat, so a charger
        // that actually arrives there meets them, where a lunge that stops a
        // third of the way leaves the hit landing on empty air between the two.
        //
        // The Beetle's Barrel Roll is the case this exists for -- the roll is
        // drawn travelling rightward across its own frames, and the stage
        // translation carries the whole figure the rest of the way so it ends
        // up against what it hit.
        Charge = 3,
    }

    // The one string -> approach map, so a skill's authored approach and an
    // enemy's plain-attack approach cannot disagree about what "charge" means.
    // Both resolvers parse through here rather than each keeping their own
    // switch -- the drift a second copy invites is the exact thing
    // docs/CODE_STANDARDS.md "Reuse" promotes a shared helper to prevent.
    public static class StageApproaches
    {
        // Unrecognised spellings return `fallback` and say nothing, the
        // graceful posture the catalogue takes everywhere: a typo should change
        // how one thing moves, not stop the content building. The caller
        // chooses the fallback because "unset" means different things -- a cast
        // that names no approach holds, a plain attack that names none lunges.
        public static StageApproach Parse(string approach, StageApproach fallback)
        {
            switch (approach?.Trim().ToLowerInvariant())
            {
                case "hold": return StageApproach.Hold;
                case "lunge": return StageApproach.Lunge;
                case "close": return StageApproach.Close;
                case "charge": return StageApproach.Charge;
                default: return fallback;
            }
        }
    }
}
