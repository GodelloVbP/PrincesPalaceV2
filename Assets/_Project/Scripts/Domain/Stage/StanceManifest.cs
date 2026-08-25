using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Stage
{
    // How one multi-frame pose is timed.
    public readonly struct StanceTiming
    {
        public readonly float SecondsPerFrame;

        // Both 1-BASED, matching the f0..fN files being f1..fN+1 to a human
        // and matching StanceAnimation's own convention. Frame 0 is not a
        // legal value, which is what lets 0 mean "unset" in the raw entry.
        public readonly int ImpactFrame;
        public readonly int SoundFrame;

        // How this stance plays when something loops it. Meaningless for a
        // stance that plays once, which is every stance but idle today.
        public readonly StanceLoop Loop;

        // WHETHER THE FIGURE IS PINNED SIDEWAYS ACROSS ITS OWN FRAMES.
        //
        // A frame is a rectangle and the creature drawn on it is not
        // necessarily in the middle. Where that varies frame to frame the
        // figure SLIDES, and on a loop it slides back and forth forever.
        //
        // Measured on the Treant's idle, which is the sheet this exists for:
        // its content centre wanders +12, +3, -16, +12, +7, -10 pixels across
        // six drawings on a canvas whose figure is 380 wide. That is a 28-pixel
        // shuffle with no pattern to it, and it is why the pose read as "quite
        // bad" rather than as breathing -- the creature is not moving, its
        // frames are.
        //
        // ON FOR A LOOP, OFF FOR A SWING, and the asymmetry is the whole rule.
        // Drift in an idle is a cropping artefact and cancelling it is free.
        // Drift in an attack is the ANIMATION -- a lunge drawn into the frames
        // is exactly this signal -- and cancelling it would nail the figure to
        // the spot mid-swing. Authored per stance either way, so a sheet that
        // disagrees with the default can say so.
        public readonly bool Steady;

        // Seconds the peak of a ping-pong is held on top of the sweep. Zero is
        // the default and the old behaviour -- a symmetric linger at both ends.
        // See RawStanceTiming.endHold and LoopCycle.FrameAt.
        public readonly float EndHoldSeconds;

        // Whether this one-shot plays back down to its first frame after
        // reaching its last -- the shell uncurling. See RawStanceTiming.returns.
        public readonly bool ReturnsToStart;

        public StanceTiming(float secondsPerFrame, int impactFrame, int soundFrame,
                            StanceLoop loop = StanceLoop.PingPong, bool steady = true,
                            float endHoldSeconds = 0f, bool returnsToStart = false)
        {
            SecondsPerFrame = secondsPerFrame;
            ImpactFrame = impactFrame;
            SoundFrame = soundFrame;
            Loop = loop;
            Steady = steady;
            EndHoldSeconds = endHoldSeconds < 0f ? 0f : endHoldSeconds;
            ReturnsToStart = returnsToStart;
        }
    }

    // The resolved manifest: what the runtime asks instead of measuring.
    //
    // Engine-free on purpose, like the rest of Domain -- it takes a frame
    // COUNT rather than a Sprite[], so the defaulting rules can be tested in
    // EditMode without a texture, a Resources folder, or a running scene.
    public sealed class StanceManifest
    {
        // The pace every animated stance ran at when timing was a single
        // global constant. Kept as the default rather than removed, so an
        // actor only needs an entry when it wants to differ -- and so this
        // commit's behaviour is identical to the constant it replaces.
        public const float DefaultSecondsPerFrame = 0.08f;

        // No ground line authored means the canvas bottom IS the ground, i.e.
        // the assumption the stage made before any of this existed. Chosen
        // over "measure it" deliberately: a missing entry should be a visible,
        // fixable gap that StanceManifestValidationTests names, not something
        // papered over by a heuristic that might be right.
        public const float DefaultGroundLine = 0f;

        private readonly Dictionary<string, float> _groundLines;
        private readonly Dictionary<string, float> _breaths;
        private readonly Dictionary<string, StanceTiming> _timings;

        public StanceManifest(RawStanceManifest raw)
        {
            _groundLines = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _breaths = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _timings = new Dictionary<string, StanceTiming>(StringComparer.OrdinalIgnoreCase);

            foreach (var actor in raw?.actors ?? new List<RawStanceActor>())
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.spritePath))
                {
                    continue;
                }

                string path = Normalise(actor.spritePath);
                _groundLines[path] = actor.groundLine;
                _breaths[path] = actor.breath;

                foreach (var stance in actor.stances ?? new List<RawStanceTiming>())
                {
                    if (stance == null || string.IsNullOrWhiteSpace(stance.stance))
                    {
                        continue;
                    }

                    _timings[Key(path, stance.stance)] = new StanceTiming(
                        stance.secondsPerFrame > 0f ? stance.secondsPerFrame : DefaultSecondsPerFrame,
                        stance.impactFrame > 0 ? stance.impactFrame : 0,
                        stance.soundFrame > 0 ? stance.soundFrame : 0,
                        ParseLoop(stance.loop),
                        ParseSteady(stance.steady, LoopsByDefault(stance.stance)),
                        stance.endHold > 0f ? stance.endHold : 0f,
                        stance.returns);
                }
            }
        }

        public IReadOnlyCollection<string> ActorPaths => _groundLines.Keys as IReadOnlyCollection<string>;

        public bool HasActor(string spritePath)
        {
            return !string.IsNullOrWhiteSpace(spritePath) && _groundLines.ContainsKey(Normalise(spritePath));
        }

        public bool HasStance(string spritePath, string stance)
        {
            return !string.IsNullOrWhiteSpace(spritePath)
                   && !string.IsNullOrWhiteSpace(stance)
                   && _timings.ContainsKey(Key(Normalise(spritePath), stance));
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
        // `idleFrameCount` decides the DEFAULT and nothing else, the same
        // shape TimingFor takes with its own frameCount: a still drawing gets
        // the full amplitude because nothing else is moving it, and a sheet
        // that already breathes gets a third of it so the two do not compete.
        // That is a rule rather than a table, so a new actor is right without
        // anybody authoring anything -- and it is only the default, so a sheet
        // that disagrees says so in the manifest.
        //
        // WHY THIS IS NOT MEASURED. The obvious refinement is to look at how
        // far a sheet's own frames actually move the figure and scale against
        // that. It is the same reasoning that put the ground line in this file
        // -- a runtime measurement is confidently wrong where an authored one
        // is visibly missing -- and the same answer applies: the frame count
        // is a FACT about the sheet, and how much its drawings move is a
        // judgement about the art.
        public float BreathFor(string spritePath, int idleFrameCount)
        {
            float fallback = idleFrameCount > 1 ? BreathCurve.SheetScale : 1f;

            if (string.IsNullOrWhiteSpace(spritePath)
                || !_breaths.TryGetValue(Normalise(spritePath), out var authored)
                || Math.Abs(authored) < float.Epsilon)
            {
                return fallback;
            }

            // Negative is the authored way to say "none" -- see
            // RawStanceActor.breath. Returned as zero rather than passed
            // through, so a caller cannot end up multiplying by a negative
            // amplitude and breathing the figure inside out.
            return authored < 0f ? 0f : authored;
        }

        // `frameCount` is only consulted to fill in what was not authored, and
        // to clamp what was: an impact frame past the end of a re-sliced
        // animation would otherwise wait for a frame that never arrives.
        public StanceTiming TimingFor(string spritePath, string stance, int frameCount)
        {
            int frames = Math.Max(1, frameCount);

            // The old midpoint guess, kept ONLY as the unauthored fallback --
            // "frame 3 of 6" was never a fact about the art, just an average
            // that happened to be tolerable. Anything that cares now says so.
            int midpoint = Math.Max(1, (int)Math.Ceiling(frames / 2f));

            var timing = new StanceTiming(DefaultSecondsPerFrame, midpoint, midpoint,
                StanceLoop.PingPong, LoopsByDefault(stance));
            if (!string.IsNullOrWhiteSpace(spritePath)
                && !string.IsNullOrWhiteSpace(stance)
                && _timings.TryGetValue(Key(Normalise(spritePath), stance), out var authored))
            {
                timing = new StanceTiming(
                    authored.SecondsPerFrame,
                    authored.ImpactFrame > 0 ? authored.ImpactFrame : midpoint,
                    authored.SoundFrame > 0 ? authored.SoundFrame : midpoint,
                    authored.Loop,
                    authored.Steady,
                    authored.EndHoldSeconds,
                    authored.ReturnsToStart);
            }

            return new StanceTiming(
                timing.SecondsPerFrame,
                Math.Min(timing.ImpactFrame, frames),
                Math.Min(timing.SoundFrame, frames),
                timing.Loop,
                timing.Steady,
                timing.EndHoldSeconds,
                timing.ReturnsToStart);
        }

        // THE ONE STANCE NAME THIS FILE KNOWS, and it is worth being explicit
        // about why. `steady` defaults differently for a loop than for a swing
        // -- cancelling drift is right for one and destroys the other -- so the
        // default has to be able to tell them apart, and today "does it loop"
        // and "is it called idle" are the same question. When a second looping
        // stance arrives this becomes a set, not a heuristic.
        private static bool LoopsByDefault(string stance) =>
            string.Equals(stance?.Trim(), "idle", StringComparison.OrdinalIgnoreCase);

        // Unrecognised spellings fall back to the default rather than throwing,
        // the same graceful posture SpellAnchorNames takes: a typo should
        // change how one sheet breathes, not stop the fight loading.
        private static StanceLoop ParseLoop(string loop)
        {
            return string.Equals(loop?.Trim(), "forward", StringComparison.OrdinalIgnoreCase)
                ? StanceLoop.Forward
                : StanceLoop.PingPong;
        }

        private static bool ParseSteady(string steady, bool fallback)
        {
            string value = steady?.Trim();
            if (string.IsNullOrEmpty(value)) return fallback;

            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;

            return fallback;
        }

        // Trailing and leading slashes are the difference between a path
        // typed by hand into JSON and one built by string concatenation, and
        // neither should decide whether a golem stands on the floor.
        private static string Normalise(string spritePath)
        {
            return spritePath.Trim().Trim('/');
        }

        private static string Key(string normalisedPath, string stance)
        {
            return normalisedPath + "/" + stance.Trim();
        }
    }
}
