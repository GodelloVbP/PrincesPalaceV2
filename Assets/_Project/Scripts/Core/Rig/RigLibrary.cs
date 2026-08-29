using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace.Core.Rig
{
    // Resolves a spritePath ("Enemies/rat") to its generated skeletal-rig
    // prefab, or null if that actor has no rig yet.
    //
    // Same posture as StanceAnimationLibrary: one Resources.Load probe per
    // folder, cached INCLUDING a miss. Only the rat resolves during the pilot;
    // every other creature's folder correctly and cheaply misses every time,
    // which is what keeps the old frame-swap Image path untouched for them.
    public static class RigLibrary
    {
        // How many source-image pixels make up 1 Unity unit in an authored
        // rig prefab (RigPrefabBuilder's own bone/mesh conversion). Shared
        // here, a Core/runtime file, rather than duplicated as a literal in
        // both RigPrefabBuilder (Editor, build-time) and wherever a rig gets
        // positioned at runtime (Core) -- those two now can't drift apart.
        public const float PixelsPerUnit = 100f;

        private static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        // TEMPORARILY DISABLED (2026-08-29) -- the rat's rig is mid-overhaul
        // (a from-scratch hand-skinned replacement was attempted and set
        // aside; the checked-in prefab is the older auto-cut-parts one) and
        // a new flat 12-frame idle sheet just shipped for this creature
        // (Assets/_Project/Art/Enemies/Giant_rat_idle_sheet_12_frame.png,
        // sliced into Resources/Enemies/rat/idle/f0..f11). Resolve() short-
        // circuits any folder in here to null -- exactly the "no rig" miss
        // every other creature's folder already produces, so
        // RefreshCombatantSprite/PlaybackFor need no branch of their own to
        // fall back to the frame-sheet Image path. Nothing about rig.json,
        // animations.json or rat.prefab is touched; remove the entry once a
        // rig is ready to come back.
        private static readonly HashSet<string> _temporarilyDisabled = new HashSet<string>
        {
            "Enemies/rat",
        };

        // A rig's bind-pose art was drawn at whatever resolution/zoom the
        // source generation happened to produce -- rig.json's
        // referenceHeightPx (the bind pose's own tight content height in
        // source px) has no reason to agree with what the CREATURE IT
        // REPLACES rendered at. The rat's old frame-sheet content (attack
        // f0, since it never got real idle art -- see docs/memory "only
        // Shawn is a real character") measures 289px tall via its own
        // Sprite.rect; rig.json's referenceHeightPx is 756. Rendering the
        // rig at raw PixelsPerUnit (no correction) puts a rat on stage at
        // roughly 2.6x its predecessor's size -- confirmed against a real
        // render, not just the arithmetic, before this table existed.
        //
        // Pilot-scope: one entry, same "only the rat resolves" posture as
        // the rest of this file. A future creature needs its own entry
        // here (or, better, this should become authored data in rig.json
        // itself -- tools/rig_actor.py already measures a tight content
        // height for other purposes -- once there's a second rig to prove
        // that design against; hardcoding a lookup table for a pilot of
        // one is the honest scope for now).
        private static readonly Dictionary<string, float> _targetContentHeightPx = new Dictionary<string, float>
        {
            ["Enemies/rat"] = 289f,
        };

        // The multiplier RefreshRigActor (FightController.StageVisuals.cs)
        // applies on top of a world slot's own SlotScale: converts the
        // rig's bind-pose-pixel-to-Unity-unit scale (PixelsPerUnit) back to
        // canvas pixels, THEN corrects for the bind pose's own content
        // height disagreeing with what this creature used to render at.
        // Falls back to no correction (raw PixelsPerUnit) for a folder with
        // no calibration entry, rather than throwing -- an uncalibrated rig
        // rendering at the wrong size is a visible, fixable bug; refusing
        // to render it at all is strictly worse.
        public static float ScaleFor(string folder, float referenceHeightPx)
        {
            if (referenceHeightPx <= 0f) return PixelsPerUnit;
            if (_targetContentHeightPx.TryGetValue(folder, out var targetPx))
            {
                return PixelsPerUnit * (targetPx / referenceHeightPx);
            }
            return PixelsPerUnit;
        }

        public static GameObject Resolve(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            if (_temporarilyDisabled.Contains(folder)) return null;

            if (_cache.TryGetValue(folder, out var cached)) return cached;

            var prefab = Resources.Load<GameObject>("Rigs/" + folder);
            _cache[folder] = prefab; // caches the null too -- one miss per folder, not one per repaint
            return prefab;
        }
    }
}
