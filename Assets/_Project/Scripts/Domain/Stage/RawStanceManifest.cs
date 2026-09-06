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

        // WHO OWNS THE NUMBER ABOVE -- "slicer" or "authored", matched
        // case-insensitively. Documented here in the same
        // field-carries-its-own-doc shape [ContentDoc] gives the content
        // records; this file is not a content entry (it is read at runtime,
        // never baked) so it says it in a comment rather than an attribute.
        //
        // "slicer"   tools/slice_actor_sheet.py measured it and may rewrite it
        //            on the next slice of that actor.
        // "authored" a person decided it against the art, and no tool writes
        //            over it -- the slicer prints its own measurement and the
        //            delta instead.
        //
        // EMPTY MEANS "authored", which is the conservative default and the
        // reason adding this field changed nothing: every entry written before
        // it existed was a hand-copied number, so reading absence as "hands
        // off" preserves exactly what those entries already meant.
        //
        // The runtime does not consult this at all -- GroundLineFor returns
        // the same number either way. It exists for the tools and for
        // StanceManifestValidationTests, which uses it to decide whether a
        // ground line that disagrees with the art by more than 8px is a
        // deliberate override (allowed, if the actor's README says why) or a
        // stale value left behind by a re-slice (not allowed).
        public string groundLineSource = "";

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

        // WHETHER THIS ACTOR FLIES, and how. Absent (null) or all-zero means
        // grounded, which is every actor but one. Stage pixels, not canvas
        // pixels -- see HoverSpec for why the difference matters at depth.
        public RawHover hover;
    }

    [Serializable]
    public class RawHover
    {
        // Resting altitude above the ground line.
        public float height;

        // Excursion either side of that altitude over one period.
        public float bob;

        // Zero means HoverCurve.DefaultPeriodSeconds.
        public float periodSeconds;
    }
}
