using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Stage
{
    // The on-disk shape of Resources/StanceManifest.json, in the same
    // Raw* -> Resolver -> Resolved* arrangement the content entries use.
    //
    // Public mutable fields with no properties, because JsonUtility only
    // populates fields -- see RawEnemyEntry for the same constraint. Numbers
    // are deliberately NOT nullable: JsonUtility cannot express absence, so
    // "unset" has to be an in-band sentinel rather than null, and the
    // resolver is what turns a sentinel into the documented default.
    [Serializable]
    public class RawStanceManifest
    {
        public List<RawStanceActor> actors = new List<RawStanceActor>();
    }

    [Serializable]
    public class RawStanceActor
    {
        public string spritePath = "";

        // Pixels from the canvas's bottom edge up to the ground the figure
        // stands on. Float rather than int because a measured value can land
        // between two pixels and rounding it would move every figure that
        // uses it -- which is the exact class of thing this file exists to
        // stop happening by accident.
        public float groundLine;

        // HOW HARD THIS CREATURE BREATHES, as a multiplier on
        // BreathCurve.FullAmplitude. See BreathCurve for the shape and for
        // what it is worth in pixels.
        //
        // ZERO MEANS UNSET, an in-band sentinel because JsonUtility cannot
        // express absence, and the default it stands for is 1: a stance is a
        // single drawing, so the transform breath is the only thing moving an
        // idle figure and has nothing to share the motion with.
        //
        // A NEGATIVE VALUE MEANS NONE. It is the escape hatch the sentinel
        // costs: without it there would be no way to author "this thing does
        // not breathe", because the value that says so is the one that means
        // unset. Nothing needs it today; a statue would.
        public float breath;
    }
}
