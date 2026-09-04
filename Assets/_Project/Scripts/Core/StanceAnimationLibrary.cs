using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // Resolves a stage stance ("Enemies/rat/attack") into the one drawing that
    // IS that stance.
    //
    // ONE DRAWING, NEVER A SEQUENCE. This used to probe for an f0..fN frame
    // folder first and fall back to a flat file; the frame folders are gone
    // from every actor in the game and the policy that removed them
    // (docs/STANCE_SHEET_SPEC.md) says why: an image model redraws a creature
    // rather than moving it, so six frames of one pose are six illustrations
    // that disagree. A pose that cannot drift is one that has nothing to drift
    // between, and the motion is carried by the transform instead -- see
    // StaticSwing and Domain/Stage/BreathCurve.
    //
    // Spell VFX and the house's contact effects are a different question and
    // keep their frame folders (FrameSequenceLoader): a flash is drawn, not
    // posed, and nothing about it has to stay recognisably the same creature.
    public static class StanceAnimationLibrary
    {
        // Cached even when the answer is null, the same posture SpellVfxPlayer
        // takes: RefreshUi repaints once per animation frame, so a monster
        // with no art for a stance must cost one failed probe rather than one
        // per repaint.
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // Exposed so a test can assert a stance actually loads, rather than
        // discovering a bad path by seeing a nameplate where a monster should
        // be.
        public static Sprite Resolve(string folder, string stance)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(stance))
            {
                return null;
            }

            string key = $"{folder}/{stance}";
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(key);
            _cache[key] = sprite;
            return sprite;
        }
    }
}
