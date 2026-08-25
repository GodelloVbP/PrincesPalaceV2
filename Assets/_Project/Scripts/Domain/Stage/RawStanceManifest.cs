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
    // resolver below is what turns a sentinel into the documented default.
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
        // ZERO MEANS UNSET, the same in-band sentinel the three numbers on
        // RawStanceTiming use and for the same reason: JsonUtility cannot
        // express absence. The default that fills it in is not a constant --
        // an actor whose sheet already breathes wants a third of what a single
        // still drawing wants -- so see StanceManifest.BreathFor.
        //
        // A NEGATIVE VALUE MEANS NONE. It is the escape hatch the sentinel
        // costs: without it there would be no way to author "this thing does
        // not breathe", because the value that says so is the one that means
        // unset. Nothing needs it today; a statue would.
        public float breath;

        public List<RawStanceTiming> stances = new List<RawStanceTiming>();
    }

    [Serializable]
    public class RawStanceTiming
    {
        public string stance = "";

        // Zero means "unset, take the default" for all three. A real
        // animation cannot run at 0 seconds per frame or impact on frame 0
        // (frames are 1-based), so the sentinel can never collide with a
        // value someone meant to author.
        public float secondsPerFrame;
        public int impactFrame;
        public int soundFrame;

        // HOW LONG THE PEAK OF A PING-PONG IDLE IS HELD, in seconds, on top of
        // the sweep. A breath that pauses at the top of the inhale reads as
        // breathing rather than as a metronome; without this the raised cosine
        // lingers equally at both ends, so "hold the full breath a beat longer"
        // could not be said. Zero is unset and means no extra hold -- the
        // symmetric linger LoopCycle always had. Ignored on anything that does
        // not ping-pong, which has no single peak to dwell on.
        public float endHold;

        // HOW THIS STANCE PLAYS WHEN IT LOOPS. "forward" or "pingpong", and
        // empty means the default -- ping-pong, because that is what an idle
        // sheet almost always is (see LoopCycle). Only consulted for a stance
        // something actually loops, which today is idle and nothing else, so
        // authoring it on a swing is harmless and pointless.
        //
        // A STRING rather than the enum, for the reason SpellPresentation.anchor
        // is one: JsonUtility writes an enum as its ordinal, so the file would
        // read "loop": 1 and reordering the enum would silently repoint every
        // sheet in the game.
        public string loop = "";

        // WHETHER THE FIGURE'S CENTRE IS HELD STILL ACROSS THE FRAMES.
        //
        // A three-state bool, because false has to be distinguishable from
        // unset: "on" and "off" are both authored answers and the default
        // differs by whether the stance loops. See StanceTiming.Steady.
        public string steady = "";
    }
}
