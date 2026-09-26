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

        // WHERE A CAST LEAVES THIS ACTOR'S BODY. Absent means "nowhere in
        // particular" -- the runtime falls back to whatever the call site
        // already computed before this field existed (the caster's own slot
        // origin), so an actor with no entry here is unaffected by this
        // field's existence at all.
        //
        // A JUDGEMENT, like breath and hover: no tool ever measures a launch
        // point off the art, because there is nothing to measure -- "where
        // the book is" is not a fact the pixels assert, it is a choice about
        // which pixel the spell should appear to leave from. See CastPoint
        // for the coordinate convention.
        public RawCastPoint castPoint;

        // WHERE THIS ACTOR'S HEAD IS, for the enemy plate's 34px icon
        // (FightController.Hud.EnemyIconHeadCrop). Absent means the plate
        // falls back to EnemyIconCrop's rule -- the top of the opaque figure
        // -- which frames a standing biped and misses a quadruped whose head
        // is out front at shoulder height (the beetle).
        //
        // A JUDGEMENT, like castPoint: which pixels "are the face" is a
        // choice about the drawing, not a measurement. Same coordinate
        // convention as RawCastPoint (dx from the canvas centre, dy above the
        // ground line), measured on the actor's idle still.
        public RawHeadBox head;
    }

    // THE HEAD'S CENTRE in RawCastPoint's convention, plus the side of the
    // square that frames it, in canvas pixels of the idle still.
    [Serializable]
    public class RawHeadBox
    {
        public float dx;
        public float dy;
        public float size;
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

    // PIXELS ON THE ACTOR'S OWN STANCE CANVAS, measured on its `cast` still --
    // the same still slice_actor_sheet.py cuts alongside idle/attack/hurt, so
    // there is nothing extra to commission.
    //
    // NOT canvas-absolute (x, y) from a corner, on purpose: a re-slice can
    // change the canvas size (a wider pad, a taller sheet) without moving the
    // figure's own anatomy relative to itself, and a point authored against
    // the OLD canvas's corner would silently drift off the book the moment
    // the canvas resized. `groundLine` solved the same problem for the feet
    // by anchoring to the canvas's bottom edge, which does not move under a
    // re-slice; this anchors to the two things that don't move under one
    // either -- the figure's own horizontal centre and its own ground line.
    //
    // dx: pixels from the figure's horizontal centre (canvas width / 2),
    //     POSITIVE TOWARD THE ACTOR'S OWN FACING (SpriteFacing, not the side
    //     of the stage it happens to stand on -- the mirror that flips a
    //     leftward-drawn monster onto the right side of the stage flips this
    //     too, automatically, because it rides the same transform the sprite
    //     does).
    // dy: pixels ABOVE the actor's own ground line (same "up from
    //     groundLine" direction groundLine itself is measured in, not up
    //     from the canvas edge) -- so a re-slice that changes groundLine by
    //     re-measuring the feet moves this point by the same amount, keeping
    //     it pinned to the book rather than to a row of the old canvas.
    [Serializable]
    public class RawCastPoint
    {
        public float dx;
        public float dy;
    }
}
