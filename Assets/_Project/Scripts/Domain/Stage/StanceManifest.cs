using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Stage
{
    // The resolved manifest: what the runtime asks instead of measuring.
    //
    // Engine-free on purpose, like the rest of Domain -- it deals in sprite
    // PATHS rather than in Sprites, so the defaulting rules can be tested in
    // EditMode without a texture, a Resources folder, or a running scene.
    //
    // TWO NUMBERS PER ACTOR AND NOTHING PER STANCE. It used to carry per-stance
    // frame timing as well (secondsPerFrame, impactFrame, soundFrame, loop,
    // endHold, returns); every stance is a single drawing now, and a still has
    // nothing to time. See docs/STANCE_SHEET_SPEC.md.
    public sealed class StanceManifest
    {
        // No ground line authored means the canvas bottom IS the ground, i.e.
        // the assumption the stage made before any of this existed. Chosen
        // over "measure it" deliberately: a missing entry should be a visible,
        // fixable gap (EnemyStanceCaptureTests asks per kit) rather than
        // something papered over by a heuristic that might be right.
        public const float DefaultGroundLine = 0f;

        // What an actor breathes at when it says nothing. Full amplitude,
        // because a stance is one drawing: the transform breath is the only
        // thing moving an idle figure, so there is nothing for it to compete
        // with. See BreathCurve.
        public const float DefaultBreath = 1f;

        // Who owns an actor's ground line. See RawStanceActor.groundLineSource
        // for the whole rule; the short version is that "slicer" is a number a
        // tool may rewrite and "authored" is one it must not, and ABSENT MEANS
        // AUTHORED so that entries written before the field existed keep
        // meaning what they already meant.
        public const string SlicerSource = "slicer";
        public const string AuthoredSource = "authored";

        private readonly Dictionary<string, float> _groundLines;
        private readonly Dictionary<string, string> _groundLineSources;
        private readonly Dictionary<string, float> _breaths;
        private readonly Dictionary<string, HoverSpec> _hovers;
        private readonly Dictionary<string, CastPointSpec> _castPoints;
        private readonly Dictionary<string, HeadBoxSpec> _heads;

        public StanceManifest(RawStanceManifest raw)
        {
            _groundLines = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _groundLineSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _breaths = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _hovers = new Dictionary<string, HoverSpec>(StringComparer.OrdinalIgnoreCase);
            _castPoints = new Dictionary<string, CastPointSpec>(StringComparer.OrdinalIgnoreCase);
            _heads = new Dictionary<string, HeadBoxSpec>(StringComparer.OrdinalIgnoreCase);

            foreach (var actor in raw?.actors ?? new List<RawStanceActor>())
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.spritePath))
                {
                    continue;
                }

                string path = Normalise(actor.spritePath);
                _groundLines[path] = actor.groundLine;
                _groundLineSources[path] = NormaliseSource(actor.groundLineSource);
                _breaths[path] = actor.breath;
                if (actor.hover != null)
                {
                    _hovers[path] = new HoverSpec(actor.hover.height, actor.hover.bob, actor.hover.periodSeconds);
                }

                if (actor.castPoint != null)
                {
                    _castPoints[path] = new CastPointSpec(actor.castPoint.dx, actor.castPoint.dy);
                }

                // A size of zero or less is JsonUtility's "absent" -- a
                // square with no side frames nothing, so it is not an entry.
                if (actor.head != null && actor.head.size > 0f)
                {
                    _heads[path] = new HeadBoxSpec(actor.head.dx, actor.head.dy, actor.head.size);
                }
            }
        }

        // Whether and how this actor flies. Grounded for anyone without a
        // hover block, which is the whole roster but Odette; the idle driver
        // asks every frame and does nothing for a grounded answer.
        public HoverSpec HoverFor(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath) || !_hovers.TryGetValue(Normalise(spritePath), out var spec))
            {
                return HoverSpec.Grounded;
            }

            return spec;
        }

        // WHERE A CAST LEAVES THIS ACTOR'S BODY, in the actor's own stance
        // canvas pixels (see RawCastPoint for the dx/dy convention). Null for
        // every actor that authors nothing -- FightController's placement
        // seam is what turns null into "the caster's own slot origin", the
        // answer every actor gave before this field existed.
        public CastPointSpec? CastPointFor(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath) || !_castPoints.TryGetValue(Normalise(spritePath), out var spec))
            {
                return null;
            }

            return spec;
        }

        // WHERE THIS ACTOR'S HEAD IS on its idle still (see RawHeadBox).
        // Null when unauthored; EnemyIconCrop turns null into its fallback.
        public HeadBoxSpec? HeadFor(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath) || !_heads.TryGetValue(Normalise(spritePath), out var spec))
            {
                return null;
            }

            return spec;
        }

        public IReadOnlyCollection<string> ActorPaths => _groundLines.Keys as IReadOnlyCollection<string>;

        public bool HasActor(string spritePath)
        {
            return !string.IsNullOrWhiteSpace(spritePath) && _groundLines.ContainsKey(Normalise(spritePath));
        }

        public float GroundLineFor(string spritePath)
        {
            if (!string.IsNullOrWhiteSpace(spritePath) && _groundLines.TryGetValue(Normalise(spritePath), out var line))
            {
                return line;
            }

            return DefaultGroundLine;
        }

        // WHO OWNS THIS ACTOR'S GROUND LINE. Always one of the two constants
        // above -- an absent, blank or unrecognised value resolves to
        // AuthoredSource, which is the answer that stops a tool writing.
        //
        // AN UNRECOGNISED VALUE IS NOT AN ERROR HERE and is one in the
        // validator. Graceful degradation is the house style at runtime, and
        // the degradation that is safe is "nobody may overwrite it"; a typo
        // that quietly turned an authored number into a tool-owned one would
        // be the failure this field exists to prevent, arriving through the
        // field itself. StanceManifestValidationTests refuses the typo.
        public string GroundLineSourceFor(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath)
                || !_groundLineSources.TryGetValue(Normalise(spritePath), out var source))
            {
                return AuthoredSource;
            }

            return source;
        }

        private static string NormaliseSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return AuthoredSource;
            }

            return source.Trim().Equals(SlicerSource, StringComparison.OrdinalIgnoreCase)
                ? SlicerSource
                : AuthoredSource;
        }

        // HOW HARD THIS ACTOR BREATHES, as a multiplier on
        // BreathCurve.FullAmplitude.
        //
        // WHY THIS IS NOT MEASURED. The obvious refinement is to look at the
        // creature's mass or height and scale against that. It is the same
        // reasoning that put the ground line in this file -- a runtime
        // measurement is confidently wrong where an authored one is visibly
        // missing -- and the same answer applies: how heavily a creature
        // breathes is a judgement about the art, not a fact about it.
        public float BreathFor(string spritePath)
        {
            if (string.IsNullOrWhiteSpace(spritePath)
                || !_breaths.TryGetValue(Normalise(spritePath), out var authored)
                || Math.Abs(authored) < float.Epsilon)
            {
                return DefaultBreath;
            }

            // Negative is the authored way to say "none" -- see
            // RawStanceActor.breath. Returned as zero rather than passed
            // through, so a caller cannot end up multiplying by a negative
            // amplitude and breathing the figure inside out.
            return authored < 0f ? 0f : authored;
        }

        // Trailing and leading slashes are the difference between a path
        // typed by hand into JSON and one built by string concatenation, and
        // neither should decide whether a golem stands on the floor.
        private static string Normalise(string spritePath)
        {
            return spritePath.Trim().Trim('/');
        }
    }

    // ONE AUTHORED POINT: where a cast leaves this actor's body, in the
    // actor's own stance canvas -- see RawCastPoint for the dx/dy
    // convention this carries verbatim (this type adds no correction of its
    // own; it exists so the resolved manifest hands out a value rather than
    // two loose floats that could be passed in the wrong order).
    public readonly struct CastPointSpec
    {
        public readonly float Dx;
        public readonly float Dy;

        public CastPointSpec(float dx, float dy)
        {
            Dx = dx;
            Dy = dy;
        }
    }

    // ONE AUTHORED SQUARE: the head's centre in CastPointSpec's convention
    // and the side of the square that frames it. See RawHeadBox.
    public readonly struct HeadBoxSpec
    {
        public readonly float Dx;
        public readonly float Dy;
        public readonly float Size;

        public HeadBoxSpec(float dx, float dy, float size)
        {
            Dx = dx;
            Dy = dy;
            Size = size;
        }
    }
}
