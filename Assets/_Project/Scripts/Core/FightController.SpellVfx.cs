using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

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
        //
        // AUTHORED PER SPELL NOW. This was a constant for every effect there
        // had ever been -- fine for a bolt, wrong in both directions for a
        // boss's slam and for a status glint.
        private static Vector2 BoxFor(CombatBeat beat)
        {
            float size = beat?.Vfx?.size ?? SpellPresentation.DefaultSize;
            if (size <= 0f) size = SpellPresentation.DefaultSize;
            return new Vector2(size, size);
        }

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
            var primary = PrimaryPlayer;
            if (primary == null || beat == null || !beat.HasSpellAnimation) return 0f;

            int frameCount = primary.Frames(beat.Vfx.path)?.Length ?? 0;
            return beat.Vfx.seconds * CombatBeat.ImpactFraction(beat.Vfx.impactFrame, frameCount);
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

        public StageShake[] StageShakesForTest => stageShakes;

        // The shared ground layer. Public for the same reason the seams above
        // are: InternalsVisibleTo names the EDITOR assembly only, so a PlayMode
        // test reaches this or reaches nothing -- and "is there exactly one
        // fault, behind everyone" is only answerable from a running scene.
        public SpellVfxPlayer GroundVfxPlayerForTest => spellGroundVfxPlayer;

        // Index 0, and the only one the measurement helpers ever touch. Frames
        // and dead space are properties of the SHEET, so asking any member
        // gives the same answer -- asking a fixed one keeps that obvious.
        private SpellVfxPlayer PrimaryPlayer =>
            spellVfxPlayers != null && spellVfxPlayers.Length > 0 ? spellVfxPlayers[0] : null;

        // ONE EFFECT PER THING THE SPELL LANDED ON.
        //
        // The beat's Target is the primary and gets member 0; SplashTargets is
        // everyone else an all-enemies cast struck, and each gets its own
        // member. Beyond what the pool holds the extras are simply not drawn --
        // the pool is sized to the stage, so running out means more combatants
        // than there are slots to stand in, and a silent drop beats an
        // exception mid-fight.
        private void PlaySpellVfx(CombatBeat beat)
        {
            if (beat == null) return;

            // FIRST, and outside the path guard below, because the two layers
            // are independent: a spell is allowed to author a ground fault and
            // no per-target sheet, or the reverse. Missing art on either side
            // costs that side and nothing else.
            PlaySpellGroundVfx(beat);

            if (PrimaryPlayer == null || string.IsNullOrEmpty(beat.Vfx.path) || beat.Target == null) return;

            int member = 0;
            foreach (var struck in StruckBy(beat))
            {
                if (member >= spellVfxPlayers.Length) break;

                PlaySpellVfxOn(beat, struck, member);
                member++;
            }
        }

        // ---- the shared ground layer ---------------------------------------------
        //
        // ONE FAULT UNDER THE WHOLE FORMATION, sized to the enemies actually
        // standing in it rather than to a slot or to a constant.
        //
        // WHY THE SPAN IS MEASURED AND NOT AUTHORED. A formation is one, two or
        // three figures at different depths, and a fault authored wide enough
        // for three reaches half the stage past a lone rat -- the effect then
        // describes ground nothing is standing on, which is the same class of
        // wrongness as an impact landing off the target. The slots know where
        // they are; nothing else does.
        //
        // MEASURED THROUGH EACH SLOT'S OWN EDGES, not from its centre plus a
        // margin. A back-row slot is depth-scaled, so its half-width in the
        // renderer's coordinates is not the same number as a front-row slot's
        // -- one authored margin would over-reach on one rank and under-reach
        // on the other. Transforming xMin and xMax gets both for free and
        // leaves no constant to be wrong.
        private void PlaySpellGroundVfx(CombatBeat beat)
        {
            var player = spellGroundVfxPlayer;
            if (player == null || beat?.Vfx == null || !beat.Vfx.HasGroundLayer) return;

            var parent = player.transform.parent;
            if (parent == null) return;

            // THE PRE-DAMAGE SNAPSHOT, which is the whole reason the beat
            // carries one: an enemy killed by this very cast still stood in the
            // fault when it opened, and reading the living list here would
            // shrink the fault away from a corpse that is still on screen.
            float left = float.MaxValue;
            float right = float.MinValue;
            float ground = 0f;
            int standing = 0;

            foreach (var struck in StruckBy(beat))
            {
                var slot = SlotFor(struck);
                if (slot == null) continue;

                var rect = slot.rect;
                float slotLeft = parent.InverseTransformPoint(
                    slot.TransformPoint(new Vector3(rect.xMin, 0f, 0f))).x;
                float slotRight = parent.InverseTransformPoint(
                    slot.TransformPoint(new Vector3(rect.xMax, 0f, 0f))).x;

                if (slotLeft > slotRight) (slotLeft, slotRight) = (slotRight, slotLeft);

                left = Mathf.Min(left, slotLeft);
                right = Mathf.Max(right, slotRight);
                ground += parent.InverseTransformPoint(
                    slot.TransformPoint(new Vector3(0f, rect.yMin, 0f))).y;
                standing++;
            }

            // Nobody with a slot: an off-stage or synthetic target. Drawing a
            // fault of no width would be a zero-sized graphic, so draw none.
            if (standing == 0 || right <= left) return;

            // THE AVERAGE GROUND LINE across the ranks it opens under, not the
            // front one's. The racks stand at different depths and the fault is
            // one flat drawing: pinned to the front rank it would float above
            // the back one, and pinned to the back it would cut through the
            // front one's feet. Halfway is the only choice that is wrong by the
            // same small amount at both ends.
            ground /= standing;

            var box = GroundBoxFor(beat.Vfx, right - left);
            if (box.x <= 0f || box.y <= 0f) return;

            // The sheet's own ground line onto the formation's, the same
            // arithmetic BoxCentreFor does for an authored impact point --
            // against the RENDERED size rather than the box, because a box
            // whose aspect the author overrode letterboxes inside itself.
            var art = RenderedSize(beat.Vfx.groundPath, box);
            float sheetGround = beat.Vfx.HasGroundImpactY ? beat.Vfx.groundImpactY : 0.5f;

            // A fault is symmetrical about the rack it opens under, so it has
            // no direction to mirror -- set anyway, because a pool member keeps
            // whatever facing the last thing to use it left behind.
            player.SetFacing(1f);
            player.PlayAt(beat.Vfx.groundPath, beat.Vfx.GroundSeconds,
                new Vector2((left + right) * 0.5f, ground + (0.5f - sheetGround) * art.y),
                box);
        }

        // Everyone this beat's effect is drawn on, primary first. The same walk
        // PlaySpellVfx makes over the pool, extracted so the ground layer and
        // the per-target layer cannot disagree about who was struck.
        private static IEnumerable<CombatantState> StruckBy(CombatBeat beat)
        {
            if (beat.Target != null) yield return beat.Target;
            if (beat.SplashTargets == null) yield break;

            foreach (var also in beat.SplashTargets)
            {
                if (also == null || ReferenceEquals(also, beat.Target)) continue;
                yield return also;
            }
        }

        // The box the fault is fitted into: as wide as the formation, and as
        // tall as its own shape says.
        //
        // NOT SQUARE, which is the one thing that separates it from BoxFor. A
        // square box for a sheet drawn as a wide fault is not merely inelegant
        // -- preserveAspect would letterbox the drawing into a strip a third of
        // the width it was asked to span, so the fault would stop short of the
        // enemies at both ends of the rack it is supposed to run under.
        //
        // ZERO groundAspect means "the sheet's own", which is the right default
        // and not a fallback: a frame's width over its height IS the shape the
        // artist drew, and authoring a second copy of it would be a number with
        // two homes. Authored only where the box must differ from the frame.
        private Vector2 GroundBoxFor(SpellPresentation vfx, float span)
        {
            float aspect = vfx.groundAspect;

            if (aspect <= 0f)
            {
                var frames = PrimaryPlayer?.Frames(vfx.groundPath);
                var frame = frames != null && frames.Length > 0 ? frames[0] : null;
                aspect = frame != null && frame.rect.height > 0f
                    ? frame.rect.width / frame.rect.height
                    : 1f;
            }

            if (aspect <= 0f) aspect = 1f;

            return new Vector2(span, span / aspect);
        }

        private void PlaySpellVfxOn(CombatBeat beat, CombatantState target, int member)
        {
            var spellVfxPlayer = spellVfxPlayers[member];
            if (spellVfxPlayer == null) return;

            // The SLOT, not the sprite Image inside it. The Image is the art's
            // raw canvas and its bottom edge is wherever the sheet happened to
            // be cut; the slot's bottom edge is the stage's ground line, which
            // the stage visuals stand every frame's feet on. Aiming at the
            // canvas put a strike below the feet by whatever padding that sheet
            // carried -- and a MOVING amount, since the offset is per frame.
            var targetRect = SlotFor(target);
            if (targetRect == null) return;

            // The slot is nested inside the stage and scaled by depth, so its
            // anchoredPosition alone is not where the sprite actually appears.
            // Converting through the shared parent keeps the effect ON the
            // target rather than near it.
            var parent = spellVfxPlayer.transform.parent;
            if (parent == null) return;

            // The box first: every measurement below depends on it, and it
            // stopped being a constant when spells got their own sizes.
            var box = BoxFor(beat);

            // WHERE THE ART HAPPENS, which is not the same question as who the
            // skill hits -- see SpellAnchor. Three independent choices, read as
            // three: whose body, where on it, and whether it flies there.
            var anchor = beat.Vfx.Anchor;

            // WHOSE BODY. A caster-anchored effect moves its origin, not its
            // shape: a self-buff aura belongs on whoever cast it even when the
            // skill is aimed at an ally. Falling back to the target when the
            // caster has no slot -- an off-stage or synthetic actor -- is the
            // same graceful default the travelling branch takes.
            //
            // A travelling effect always AIMS at the target; where it comes
            // from is the travelling branch's business, not this one's.
            var on = SpellAnchorNames.OnCaster(anchor) && !SpellAnchorNames.Travels(anchor)
                ? SlotFor(beat.Actor) ?? targetRect
                : targetRect;

            // WHERE ON IT: the point the effect is supposed to touch. The
            // ground line under the body, or the middle of it.
            var aim = AimPoint(parent, on, SpellAnchorNames.Centred(anchor));

            if (SpellAnchorNames.Travels(anchor))
            {
                PlayTravellingVfx(beat, spellVfxPlayer, parent, aim, box,
                    standing: !SpellAnchorNames.Centred(anchor));
                return;
            }

            spellVfxPlayer.SetFacing(1f);

            spellVfxPlayer.PlayAt(beat.Vfx.path, beat.Vfx.seconds,
                BoxCentreFor(beat, aim, box, facing: 1f, standing: !SpellAnchorNames.Centred(anchor)), box);
        }

        // ---- the house's own contact language, for a swing that authored none --

        // THE ATTACK GRAPHIC AND THE IMPACT BURST for a single-drawing melee
        // blow. Fired at the impact instant by FightBeatPlayer, which owns the
        // decision of WHICH beats get this (see its WantsContactFx); all this
        // owns is where the two effects go.
        //
        // MEMBERS 0 AND 1 OF THE SPELL POOL, borrowed rather than pooled
        // separately. FightBeatPlayer only calls this for a beat with no
        // authored VFX path, and PlaySpellVfx returns on its first line for
        // exactly that beat -- so the whole pool is idle whenever this runs,
        // and a second pool would be three more Images on the canvas for a
        // case that cannot overlap the first.
        //
        // THE ARC IS SKIPPED FOR A CHARGE. The slash arc reads a lean-and-cut
        // (docs/archive/STATIC_COMBAT_ART_DEEP_DIVE.md's "Attack families" draws that
        // language for Slash, not for a committed rush), and a Charge is a
        // bump rather than a cut -- the same table's "Blunt" row calls for a
        // burst and a longer hit-stop instead of an arc. This is the burst
        // half of that: the simplest honest version of the distinction, and
        // it costs nothing the pool did not already have. The hit-stop half
        // is not implemented -- HitStopFor is keyed on the blow's weight for
        // every approach alike, and giving Charge a longer one is a separate,
        // undecided change to that shared curve.
        private void PlayContactFx(CombatBeat beat)
        {
            if (beat?.Target == null) return;
            if (spellVfxPlayers == null || spellVfxPlayers.Length < 2) return;

            var arc = spellVfxPlayers[0];
            var burst = spellVfxPlayers[1];
            if (arc == null || burst == null) return;

            var parent = arc.transform.parent;
            if (parent == null) return;

            var targetRect = SlotFor(beat.Target);
            if (targetRect == null) return;

            // THE CONTENT CENTRE, not the ground line. A slash lands on the
            // body; the standing/letterbox correction BoxCentreFor applies is
            // for an effect drawn erupting from a floor, and neither of these
            // sheets is. Both are generated with their impact at the exact
            // centre of the frame (tools/make_contact_fx.py), so the aim point
            // IS the box centre and there is no offset to cancel.
            var aim = AimPoint(parent, targetRect, centred: true);
            var box = ContactBoxFor(targetRect);

            bool wantsArc = beat.Approach != StageApproach.Charge;

            if (wantsArc)
            {
                // MIRRORED FOR A MONSTER, the same rule PlayTravellingVfx
                // follows and for the same reason: the arc is drawn sweeping
                // left to right, so a blow coming back across the stage has
                // to run the other way or the sweep trails the strike instead
                // of leading it. Read off which SIDE the attacker is on
                // rather than off slot positions, so a back-row monster hit
                // by a status tick cannot flip it (the identical trap
                // Recoil's own header records).
                arc.SetFacing(beat.Actor != null && !beat.Actor.IsPlayerSide ? -1f : 1f);
                arc.PlayAt(ContactCues.SlashArcPath, ContactCues.SlashSeconds, aim, box);
            }

            // The burst is radially symmetrical, so it has no direction to
            // mirror -- set anyway, because the pool member keeps whatever
            // facing the last thing to use it left behind.
            burst.SetFacing(1f);
            burst.PlayAt(ContactCues.ImpactBurstPath, ContactCues.BurstSeconds, aim, box);

            SoundController.PlayClip(ContactCues.ThudClipPath);
        }

        // The effect box, shrunk to the depth the target is standing at.
        //
        // A back-row figure is drawn smaller (FightStageAnchors.SlotScale), so
        // an effect at a fixed canvas size would be correct on the front rank
        // and swallow the one behind it. Taken from the animator's BaseScale
        // rather than the live localScale because the target is being punched
        // on this exact frame and its localScale is mid-squash -- the same
        // distinction AnchorStageSlots draws when it compares marks.
        //
        // The spell path deliberately does NOT do this: a spell authors its
        // own size per skill and a caster tunes it against what they see. This
        // one has no authored size to tune, so the depth has to come from
        // somewhere.
        private static Vector2 ContactBoxFor(RectTransform slot)
        {
            var animator = slot.GetComponent<StageActorAnimator>();
            float depth = animator != null
                ? Mathf.Abs(animator.BaseScale.x)
                : Mathf.Abs(slot.localScale.x);

            // A zero scale is a slot that has not been anchored yet; drawing
            // the effect at full size beats drawing it at nothing.
            if (depth <= 0.01f) depth = 1f;

            float size = ContactCues.BoxSize * depth;
            return new Vector2(size, size);
        }

        // THE POINT ON A COMBATANT AN EFFECT IS AIMED AT, in the effect pool's
        // own coordinates.
        //
        // The SLOT, not the sprite Image inside it. The Image is the art's raw
        // canvas and its bottom edge is wherever the sheet happened to be cut;
        // the slot's bottom edge is the stage's ground line, which the stage
        // visuals stand every frame's feet on. Aiming at the canvas put a
        // strike below the feet by whatever padding that sheet carried -- and a
        // MOVING amount, since the offset is per frame.
        //
        // Converted through the shared parent rather than read off
        // anchoredPosition, because the slot is nested inside the stage and
        // scaled by depth: its own coordinates are not where it appears.
        //
        // KNOWN, AND FINE FOR EVERY ACTOR THAT USES IT TODAY: `centred` takes
        // the slot's midpoint, and the slot is sized to the sprite's raw
        // CANVAS rather than to the figure drawn on it. The two agree closely
        // for the rat (canvas mid 153, content mid ~142) and would not for the
        // golem, whose idle frame carries 123px of empty headroom -- the same
        // disagreement PlaceIntentBadge already had to stop measuring around.
        // Nothing centred is aimed at the golem yet; when something is, the
        // measured answer is ContentTopForActor, not another constant.
        private static Vector2 AimPoint(Transform parent, RectTransform slot, bool centred)
        {
            var rect = slot.rect;
            float x = parent.InverseTransformPoint(slot.TransformPoint(Vector3.zero)).x;
            float y = parent.InverseTransformPoint(
                slot.TransformPoint(new Vector3(0f, centred ? rect.center.y : rect.yMin, 0f))).y;

            return new Vector2(x, y);
        }

        // WHERE THE BOX GOES so that the SHEET'S OWN POINT OF CONTACT lands on
        // the point the spell is aimed at.
        //
        // Two rules, and which one applies is the sheet's choice rather than
        // this file's.
        //
        // AUTHORED (SpellPresentation.impactX/impactY): the offset from the
        // box's centre to the drawn impact is arithmetic, and the box is placed
        // to cancel it. Correct on BOTH axes, which the measured rule never
        // was: mud_burst's impact sits two thirds of the way across its frame,
        // so centring the box put the detonation a sixth of a box to one side
        // of everything it hit.
        //
        // MEASURED (the fallback, for any sheet that does not state a point):
        // the box's bottom edge on the ground line, less the transparent margin
        // the sheet carries below its landed content. Kept verbatim rather than
        // replaced -- it is right for an effect drawn standing on a floor, it
        // is what golem_boulder is tuned against, and a sheet that says nothing
        // must not move.
        //
        // `facing` is +1 as drawn and -1 mirrored, and only the horizontal
        // correction cares: a mirrored sheet's impact is the same distance from
        // the other edge, so the box has to sit on the other side of the target.
        // Getting this wrong is invisible on Shawn (who always casts rightward)
        // and doubles the error on every enemy.
        private Vector2 BoxCentreFor(CombatBeat beat, Vector2 aim, Vector2 box, float facing, bool standing)
        {
            var vfx = beat.Vfx;

            if (vfx.HasImpactPoint)
            {
                var art = RenderedSize(vfx.path, box);
                return new Vector2(
                    aim.x + (0.5f - vfx.impactX) * art.x * (facing < 0f ? -1f : 1f),
                    aim.y + (0.5f - vfx.impactY) * art.y);
            }

            if (!standing) return aim;

            return new Vector2(aim.x, aim.y + box.y * 0.5f - VfxDeadSpaceBelow(vfx.path, box, vfx.impactFrame));
        }

        // How big the art actually draws once preserveAspect has fitted it into
        // the box. The box is square and not every sheet is, so a fraction of
        // the FRAME is only a distance on screen after this.
        //
        // Falls back to the box itself for a sheet with no readable frames --
        // the same answer a square sheet gives, and the one that degrades to
        // "no correction" rather than to a division by zero.
        private Vector2 RenderedSize(string vfxPath, Vector2 box)
        {
            var frames = PrimaryPlayer?.Frames(vfxPath);
            if (frames == null || frames.Length == 0 || frames[0] == null) return box;

            float frameWidth = frames[0].rect.width;
            float frameHeight = frames[0].rect.height;
            if (frameWidth <= 0f || frameHeight <= 0f || box.x <= 0f || box.y <= 0f) return box;

            float frameAspect = frameWidth / frameHeight;
            float boxAspect = box.x / box.y;

            return frameAspect <= boxAspect
                ? new Vector2(box.y * frameAspect, box.y)
                : new Vector2(box.x, box.x / frameAspect);
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
        private void PlayTravellingVfx(CombatBeat beat, SpellVfxPlayer spellVfxPlayer, Transform parent,
                                       Vector2 aim, Vector2 box, bool standing)
        {
            var casterRect = SlotFor(beat.Actor);

            // No slot for the caster -- an off-stage or synthetic actor -- means
            // falling back to the placement every other spell uses rather than
            // flying in from an origin that does not exist.
            if (casterRect == null)
            {
                spellVfxPlayer.SetFacing(1f);
                spellVfxPlayer.PlayAt(beat.Vfx.path, beat.Vfx.seconds,
                    BoxCentreFor(beat, aim, box, facing: 1f, standing), box);
                return;
            }

            float casterX = parent.InverseTransformPoint(casterRect.TransformPoint(Vector3.zero)).x;

            // MIRRORED WHEN THE CASTER IS ON THE RIGHT. The sheet fires left to
            // right; the Bog Witch casts the same spell back across the stage,
            // and unmirrored her glyph would form facing away from the thing it
            // is about to hit.
            //
            // DECIDED BEFORE the arrival is placed, not after, because the
            // horizontal impact correction depends on it: a mirrored sheet's
            // point of contact is the same distance in from the OTHER edge.
            float facing = aim.x >= casterX ? 1f : -1f;
            spellVfxPlayer.SetFacing(facing);

            var to = BoxCentreFor(beat, aim, box, facing, standing);

            // THE LAUNCH IS NOT IMPACT-CORRECTED, and that asymmetry is on
            // purpose. `to` is placed so the sheet's IMPACT sits on the target;
            // the same offset applied at the other end would shift the sheet's
            // conjuring glyph -- a different part of the drawing entirely --
            // away from the caster by that amount rather than onto them. For
            // mud_burst that would drag the glyph a further sixth of a box out
            // into open air, which is the exact complaint the travelling anchor
            // was written to fix.
            //
            // Correcting the launch properly needs a second authored point (the
            // sheet's origin, as against its impact), and no sheet has yet
            // wanted one. Until one does, the launch keeps the rule it has
            // always had: the box centred on the caster.
            var from = new Vector2(casterX, to.y);

            // LEAVES ON THE DEPARTURE FRAME AND ARRIVES ON THE IMPACT ONE. The
            // second is the frame the damage number is already timed to (see
            // ImpactDelayFor), so tying the flight to it keeps the burst and the
            // hit from drifting apart when a spell is retuned; the first is what
            // holds the charge where it was cast instead of letting it drift
            // through its own wind-up.
            //
            // Both are 1-based in content, like the golem's impact frame.
            spellVfxPlayer.PlayFrom(beat.Vfx.path, beat.Vfx.seconds, from, to, box,
                beat.Vfx.departFrame - 1, beat.Vfx.impactFrame - 1);
        }

        // How much empty box sits BELOW the visible art once preserveAspect has
        // fitted this sheet into the box. Two corrections, both earned.
        //
        // THE BOX IS A PARAMETER, not the old constant: the letterbox is a
        // function of both the art's aspect and the box's, so a spell that
        // authored its own size and kept reading 380 here would be corrected by
        // the wrong margin and land off the ground line.
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
        private float VfxDeadSpaceBelow(string vfxPath, Vector2 box, int impactFrame)
        {
            var frames = PrimaryPlayer?.Frames(vfxPath);
            if (frames == null || frames.Length == 0 || frames[0] == null) return 0f;
            if (box.y <= 0f) return 0f;

            // Height-constrained (tall or square) art already reaches the box's
            // bottom edge; only wider-than-the-box art letterboxes. How much is
            // RenderedSize's answer rather than a second copy of the same fit --
            // two copies of a preserveAspect calculation is exactly the shape of
            // the bug this method's own header records, one layer up.
            float renderedHeight = RenderedSize(vfxPath, box).y;
            float letterboxBelow = (box.y - renderedHeight) * 0.5f;

            return letterboxBelow + VfxContentPaddingFraction(vfxPath, frames, impactFrame) * renderedHeight;
        }

        // The smallest transparent bottom margin from the IMPACT frame onward,
        // as a fraction of frame height -- the LANDED effect at its fullest
        // extent.
        //
        // NOT the whole sheet, though it used to be, and that was the bug:
        // lightning_bolt's pre-impact travel frames (a thin descending bolt
        // tip) happen to touch the canvas floor harder than the actual impact
        // burst (a wide starburst that never reaches as low) ever does, so
        // scanning from frame 0 measured the wind-up's ground line and applied
        // it to the landed effect -- the strike rendered floating above
        // whatever it hit instead of on it. Starting the scan one frame before
        // impactFrame (rather than exactly at it) keeps a frame of lead-in
        // margin without reintroducing the travel frames that caused this.
        //
        // Public rather than internal: InternalsVisibleTo names the EDITOR
        // assembly only, and the sheet-margin maths is the part of this file
        // most worth pinning -- every one of its rules came from a bug.
        // impactFrame defaults to 0, which scans the whole sheet -- the
        // original behaviour, still correct for content whose margin is
        // consistent across every frame (Shawn's two spells, this file's own
        // pinned test fixtures).
        public static float VfxContentPaddingFraction(string vfxPath, Sprite[] frames, int impactFrame = 0)
        {
            if (vfxPath != null && VfxPaddingCache.TryGetValue(vfxPath, out var cached)) return cached;

            int start = Mathf.Clamp(impactFrame - 1, 0, frames.Length - 1);
            float smallest = float.MaxValue;
            for (int i = start; i < frames.Length; i++)
            {
                float padding = BottomPaddingFraction(frames[i]);
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
