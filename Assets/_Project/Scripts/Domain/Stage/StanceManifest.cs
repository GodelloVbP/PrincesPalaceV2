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

        public StanceTiming(float secondsPerFrame, int impactFrame, int soundFrame)
        {
            SecondsPerFrame = secondsPerFrame;
            ImpactFrame = impactFrame;
            SoundFrame = soundFrame;
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
        private readonly Dictionary<string, StanceTiming> _timings;

        public StanceManifest(RawStanceManifest raw)
        {
            _groundLines = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            _timings = new Dictionary<string, StanceTiming>(StringComparer.OrdinalIgnoreCase);

            foreach (var actor in raw?.actors ?? new List<RawStanceActor>())
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.spritePath))
                {
                    continue;
                }

                string path = Normalise(actor.spritePath);
                _groundLines[path] = actor.groundLine;

                foreach (var stance in actor.stances ?? new List<RawStanceTiming>())
                {
                    if (stance == null || string.IsNullOrWhiteSpace(stance.stance))
                    {
                        continue;
                    }

                    _timings[Key(path, stance.stance)] = new StanceTiming(
                        stance.secondsPerFrame > 0f ? stance.secondsPerFrame : DefaultSecondsPerFrame,
                        stance.impactFrame > 0 ? stance.impactFrame : 0,
                        stance.soundFrame > 0 ? stance.soundFrame : 0);
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

            var timing = new StanceTiming(DefaultSecondsPerFrame, midpoint, midpoint);
            if (!string.IsNullOrWhiteSpace(spritePath)
                && !string.IsNullOrWhiteSpace(stance)
                && _timings.TryGetValue(Key(Normalise(spritePath), stance), out var authored))
            {
                timing = new StanceTiming(
                    authored.SecondsPerFrame,
                    authored.ImpactFrame > 0 ? authored.ImpactFrame : midpoint,
                    authored.SoundFrame > 0 ? authored.SoundFrame : midpoint);
            }

            return new StanceTiming(
                timing.SecondsPerFrame,
                Math.Min(timing.ImpactFrame, frames),
                Math.Min(timing.SoundFrame, frames));
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
