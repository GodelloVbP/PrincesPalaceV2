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

        private readonly Dictionary<string, float> _groundLines;
        private readonly Dictionary<string, float> _breaths;
        private readonly Dictionary<string, HoverSpec> _hovers;

        public StanceManifest(RawStanceManifest raw)
        {
            _groundLines = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _breaths = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _hovers = new Dictionary<string, HoverSpec>(StringComparer.OrdinalIgnoreCase);

            foreach (var actor in raw?.actors ?? new List<RawStanceActor>())
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.spritePath))
                {
                    continue;
                }

                string path = Normalise(actor.spritePath);
                _groundLines[path] = actor.groundLine;
                _breaths[path] = actor.breath;
                if (actor.hover != null)
                {
                    _hovers[path] = new HoverSpec(actor.hover.height, actor.hover.bob, actor.hover.periodSeconds);
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
}
