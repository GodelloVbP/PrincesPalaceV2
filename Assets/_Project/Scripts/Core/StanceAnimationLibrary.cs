using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // Resolves a stage stance ("Enemies/rat/attack") into its frame
    // sequence, discovering frames from disk rather than being told a
    // count — the same posture SpellVfxPlayer already takes for spell VFX,
    // applied to actor sprites.
    //
    // Two shapes coexist so nothing existing has to change:
    //   {folder}/{stance}/f0.png, f1.png, ...   -- animated, NEW
    //   {folder}/{stance}.png                   -- flat, today's only shape
    // A flat file resolves to a Frames array of length 1, which is what
    // makes every downstream consumer (LoadStanceSprite, PlayBeats'
    // frame-stepping, impact/sound timing) a single path with no
    // "is this animated?" branch to regress.
    //
    // This is also the one seam a future config source (per-actor JSON,
    // authored impact/sound frames) would plug into: it is the only place
    // that decides an animation's timing. Nothing downstream would need to
    // change to consume it.
    public static class StanceAnimationLibrary
    {
        // Per-actor authoring now EXISTS — Resources/StanceManifest.json, via
        // StanceManifestLoader — so the single global pace this class used to
        // apply to every creature alike has moved to
        // StanceManifest.DefaultSecondsPerFrame, where it serves only as the
        // fallback for a stance nobody has authored.
        //
        // Its history is worth keeping: it was 0.05f (a 6-frame animation over
        // in ~0.3s — a flicker, not a readable slam; playtest: "goes quite
        // fast"), then 0.08f. A golem and a rat should not swing at the same
        // speed, and until the manifest they had no way not to.

        private static readonly Dictionary<string, StanceAnimation> _cache = new Dictionary<string, StanceAnimation>();

        // Exposed so a test can assert a stance's frames actually load
        // (and in the right order), rather than discovering a bad path by
        // seeing nothing animate.
        public static StanceAnimation Resolve(string folder, string stance)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(stance))
            {
                return StanceAnimation.Empty;
            }

            string key = $"{folder}/{stance}";
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var built = Build(key);
            // Cached even when empty, same reasoning as SpellVfxPlayer and
            // the old per-sprite cache this replaces: a monster with no art
            // for a stance costs one failed probe, not one per repaint.
            _cache[key] = built;
            return built;
        }

        private static StanceAnimation Build(string key)
        {
            // The f0..fN probe is FrameSequenceLoader's, shared with
            // SpellVfxPlayer and HubBuildingAnimator. The flat-file fallback
            // below stays here: it is specific to actor stances (a pose is
            // allowed to be a single PNG), and neither of the other two has
            // any such shape.
            // key is never blank — Resolve guards both halves of it above —
            // so Load's null-for-blank case is unreachable here.
            var frames = new List<Sprite>(FrameSequenceLoader.Load(key) ?? System.Array.Empty<Sprite>());

            if (frames.Count == 0)
            {
                // No frame folder — the flat shape, a single file at the
                // stance's own path.
                var flat = Resources.Load<Sprite>(key);
                if (flat != null)
                {
                    frames.Add(flat);
                }
            }

            if (frames.Count == 0)
            {
                return StanceAnimation.Empty;
            }

            // AUTHORED, not guessed. `key` is "{folder}/{stance}", which is
            // exactly what the manifest is keyed by, so it splits back apart
            // here rather than being threaded through Build as two arguments.
            //
            // An unauthored stance still resolves to the old midpoint guess
            // (StanceManifest.TimingFor), so nothing that has no entry
            // changes behaviour — but StanceManifestValidationTests fails on
            // any MULTI-FRAME stance without one, which is the only case
            // where the guess was ever load-bearing.
            int split = key.LastIndexOf('/');
            string folder = split > 0 ? key.Substring(0, split) : key;
            string stance = split > 0 ? key.Substring(split + 1) : "";

            var timing = StanceManifestLoader.Manifest.TimingFor(folder, stance, frames.Count);
            return new StanceAnimation(frames.ToArray(), timing.SecondsPerFrame,
                timing.ImpactFrame, timing.SoundFrame, timing.Loop, timing.Steady,
                timing.EndHoldSeconds);
        }
    }
}
