using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace
{
    // Landing a spell's frame sequence on the thing it hit.
    //
    // Almost all of this file is one problem: WHERE the bottom of the effect
    // actually is. Three separate things move it, and every one of them was
    // found by an effect erupting somewhere anatomically wrong.
    public partial class FightController
    {
        // The box the effect is fitted into. Square, and the art is not always.
        private static readonly Vector2 SpellVfxSize = new Vector2(380f, 380f);

        // A per-pixel scan of every frame of a sheet, asked for on every cast.
        // Keyed by PATH rather than by sprite so nothing accumulates across
        // scene loads -- the same arrangement the stage's content-centre cache
        // uses.
        private static readonly Dictionary<string, float> VfxPaddingCache = new Dictionary<string, float>();

        // How long after the beat opens the blow actually lands.
        //
        // Zero for a plain swing, which is what makes the whole impact-delay
        // machinery invisible for the overwhelming majority of beats. For a
        // spell it is the fraction of the sequence the impact frame sits at --
        // computed in Domain, because "which frame is the impact" is authored
        // content and not a view decision.
        //
        // Public, so a test can assert the plain-swing case without reflection.
        public float ImpactDelayFor(CombatBeat beat)
        {
            if (spellVfxPlayer == null || beat == null || !beat.HasSpellAnimation) return 0f;

            int frameCount = spellVfxPlayer.Frames(beat.VfxPath)?.Length ?? 0;
            return beat.VfxSeconds * CombatBeat.ImpactFraction(beat.VfxImpactFrame, frameCount);
        }

        // ---- seams for the PlayMode tests -----------------------------------------
        //
        // Placement is the half of this file that has ever been wrong, and it is
        // only observable from a running scene: the maths depends on the stage's
        // depth scaling and on where the slots actually landed, neither of which
        // an EditMode test can build. InternalsVisibleTo names the EDITOR
        // assembly only, so a PlayMode test reaches these or reaches nothing.
        public void PlaySpellVfxForTest(CombatBeat beat) => PlaySpellVfx(beat);

        public RectTransform SlotForTest(CombatantState combatant) => SlotFor(combatant);

        public FightSession SessionForTest => _session;

        private void PlaySpellVfx(CombatBeat beat)
        {
            if (beat == null) return;

            if (spellVfxPlayer == null || string.IsNullOrEmpty(beat.VfxPath) || beat.Target == null) return;

            // The SLOT, not the sprite Image inside it. The Image is the art's
            // raw canvas and its bottom edge is wherever the sheet happened to
            // be cut; the slot's bottom edge is the stage's ground line, which
            // the stage visuals stand every frame's feet on. Aiming at the
            // canvas put a strike below the feet by whatever padding that sheet
            // carried -- and a MOVING amount, since the offset is per frame.
            var targetRect = SlotFor(beat.Target);
            if (targetRect == null) return;

            // The slot is nested inside the stage and scaled by depth, so its
            // anchoredPosition alone is not where the sprite actually appears.
            // Converting through the shared parent keeps the effect ON the
            // target rather than near it.
            var parent = spellVfxPlayer.transform.parent;
            if (parent == null) return;

            float deadSpaceBelow = VfxDeadSpaceBelow(beat.VfxPath);

            var rect = targetRect.rect;
            float centreX = parent.InverseTransformPoint(targetRect.TransformPoint(Vector3.zero)).x;
            float bottomY = parent.InverseTransformPoint(targetRect.TransformPoint(new Vector3(0f, rect.yMin, 0f))).y;

            if (beat.VfxFromCaster)
            {
                PlayTravellingVfx(beat, parent, centreX, bottomY);
                return;
            }

            spellVfxPlayer.SetFacing(1f);
            var anchoredPosition = new Vector2(centreX, bottomY + SpellVfxSize.y * 0.5f - deadSpaceBelow);

            spellVfxPlayer.PlayAt(beat.VfxPath, beat.VfxSeconds, anchoredPosition, SpellVfxSize);
        }

        // ---- an effect that CROSSES the stage ------------------------------------
        //
        // Everything above puts an effect where it lands. This one starts where
        // it was cast and flies, and the difference is not a nicety: mud_blast
        // is drawn as a conjuring glyph, a lance leaving it, and an impact.
        // Played on the target the glyph appears in open air with nothing
        // between it and the caster, and the spell reads as arriving from
        // somewhere rather than as being cast by anybody.
        //
        // The box does not change size or shape -- see SpellVfxPlayer.PlayFrom
        // for the stretched version that was tried first and for the
        // measurement that killed it.
        private void PlayTravellingVfx(CombatBeat beat, Transform parent, float targetX, float bottomY)
        {
            float dead = VfxDeadSpaceBelow(beat.VfxPath);
            var to = new Vector2(targetX, bottomY + SpellVfxSize.y * 0.5f - dead);

            var casterRect = SlotFor(beat.Actor);

            // No slot for the caster -- an off-stage or synthetic actor -- means
            // falling back to the placement every other spell uses rather than
            // flying in from an origin that does not exist.
            if (casterRect == null)
            {
                spellVfxPlayer.SetFacing(1f);
                spellVfxPlayer.PlayAt(beat.VfxPath, beat.VfxSeconds, to, SpellVfxSize);
                return;
            }

            float casterX = parent.InverseTransformPoint(casterRect.TransformPoint(Vector3.zero)).x;
            var from = new Vector2(casterX, to.y);

            // MIRRORED WHEN THE CASTER IS ON THE RIGHT. The sheet fires left to
            // right; the Bog Witch casts the same spell back across the stage,
            // and unmirrored her glyph would form facing away from the thing it
            // is about to hit.
            spellVfxPlayer.SetFacing(targetX >= casterX ? 1f : -1f);

            // LEAVES ON THE DEPARTURE FRAME AND ARRIVES ON THE IMPACT ONE. The
            // second is the frame the damage number is already timed to (see
            // ImpactDelayFor), so tying the flight to it keeps the burst and the
            // hit from drifting apart when a spell is retuned; the first is what
            // holds the charge where it was cast instead of letting it drift
            // through its own wind-up.
            //
            // Both are 1-based in content, like the golem's impact frame.
            spellVfxPlayer.PlayFrom(beat.VfxPath, beat.VfxSeconds, from, to, SpellVfxSize,
                beat.VfxDepartFrame - 1, beat.VfxImpactFrame - 1);
        }

        // How much empty box sits BELOW the visible art once preserveAspect has
        // fitted this sheet into SpellVfxSize. Two corrections, both earned.
        //
        // ONE: THE LETTERBOX. Anchoring the BOX's bottom edge to the target's
        // ground line is not enough on its own, because the box is square and
        // not every sheet is. preserveAspect fits the art INSIDE the box, so a
        // wider-than-square sheet gets letterboxed and the visible art no longer
        // reaches the box's own bottom edge -- it floats, centred, with dead
        // space beneath it. Shawn's two spells are 512x512, fill the square
        // exactly, and hid this completely. The golem's Boulder Slam is 598x433,
        // so in a 380x380 box it renders 380x275 with ~52 units of empty box
        // below -- which is why its ground spike erupted around the target's
        // midriff instead of under their feet.
        //
        // Measured off the sheet's own dimensions rather than assumed, because
        // the assumption is exactly what broke: this comment used to say the
        // frames "are a fixed 512x512 square", true of the only two spells that
        // existed when it was written and false the moment an enemy VFX was cut
        // at a different aspect.
        //
        // TWO: THE ART'S OWN BOTTOM MARGIN, at full extent. An earlier version
        // corrected only the letterbox, reasoning that a frame's bottom margin
        // is animation and compensating per frame would drag the effect down the
        // screen as it faded. The first half is right and the second half is the
        // wrong conclusion from it. The margin IS animation -- the lightning
        // bolt strikes to its canvas floor and its afterglow then retracts
        // upward, clearing 0, 0, 35, 66, 72, 60 across six frames -- so what the
        // effect needs is not a per-frame correction but a single one taken at
        // the moment it is fully extended. That is the MINIMUM margin across the
        // sheet, and it is the effect's own ground line: correcting by it lands
        // the strike on the feet and still lets the fade retreat upward.
        private float VfxDeadSpaceBelow(string vfxPath)
        {
            var frames = spellVfxPlayer == null ? null : spellVfxPlayer.Frames(vfxPath);
            if (frames == null || frames.Length == 0 || frames[0] == null) return 0f;

            float frameWidth = frames[0].rect.width;
            float frameHeight = frames[0].rect.height;
            if (frameWidth <= 0f || frameHeight <= 0f || SpellVfxSize.y <= 0f) return 0f;

            // Height-constrained (tall or square) art already reaches the box's
            // bottom edge; only wider-than-the-box art letterboxes.
            float frameAspect = frameWidth / frameHeight;
            float boxAspect = SpellVfxSize.x / SpellVfxSize.y;
            float renderedHeight = frameAspect <= boxAspect
                ? SpellVfxSize.y
                : SpellVfxSize.x / frameAspect;
            float letterboxBelow = (SpellVfxSize.y - renderedHeight) * 0.5f;

            return letterboxBelow + VfxContentPaddingFraction(vfxPath, frames) * renderedHeight;
        }

        // The smallest transparent bottom margin across a sheet, as a fraction of
        // frame height -- the effect at its fullest extent.
        // Public rather than internal: InternalsVisibleTo names the EDITOR
        // assembly only, and the sheet-margin maths is the part of this file
        // most worth pinning -- every one of its rules came from a bug.
        public static float VfxContentPaddingFraction(string vfxPath, Sprite[] frames)
        {
            if (vfxPath != null && VfxPaddingCache.TryGetValue(vfxPath, out var cached)) return cached;

            float smallest = float.MaxValue;
            foreach (var frame in frames)
            {
                float padding = BottomPaddingFraction(frame);
                if (padding >= 0f && padding < smallest) smallest = padding;
            }

            // No readable frame, or a sheet that is entirely blank: fall back to
            // no correction rather than throwing. "Uncorrected" is exactly the
            // old behaviour rather than something new and worse.
            float result = smallest == float.MaxValue ? 0f : smallest;
            if (vfxPath != null) VfxPaddingCache[vfxPath] = result;
            return result;
        }

        // Fraction of the frame's height that is empty below its lowest opaque
        // row.
        //
        // NEGATIVE for a frame that cannot be measured or has no opaque pixel at
        // all. golem_boulder's f0 is a deliberately blank wind-up frame, and
        // letting it report 0 would claim the effect reaches the floor before it
        // has appeared -- which is worse than not correcting, because it is
        // confidently wrong.
        public static float BottomPaddingFraction(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return -1f;

            var r = sprite.rect;
            int w = Mathf.RoundToInt(r.width);
            int h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0) return -1f;

            var pixels = sprite.texture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a > 0.02f) return y / (float)h;
                }
            }

            return -1f;
        }
    }
}
