using System.Collections;
using UnityEngine;

namespace PrincesPalace
{
    // Movement half of a stage figure's combat animation: the lunge forward
    // on an attack, the knock-back on a hit. Lives on the SLOT (the sprite's
    // parent) because that is what carries the depth scale and the bottom
    // pivot — moving the sprite itself would fight the scale, and moving the
    // slot keeps the figure's feet on the same ground line the whole way.
    //
    // Deliberately split from stance/pose: FightController owns which sprite
    // is shown (it is the only thing that knows how to resolve a combatant to
    // its art), this owns only where the figure stands. Keeping them separate
    // means a pose with no movement (defeated, victory) needs no animator
    // involvement at all, and this file never has to know what a CombatantState
    // is.
    //
    // Several of these run at once by design. An attacker lunging while its
    // target recoils reads as one exchange; sequencing them would need a
    // queue and a much larger change to how FightController resolves a turn
    // (the whole chain currently resolves synchronously in one frame).
    public class StageActorAnimator : MonoBehaviour
    {
        // The strike should read as fast and hard, so the two halves are
        // deliberately NOT symmetrical: the lunge is a snap, the return is
        // the slower recovery. Making both quick reads as a twitch; making
        // both slow reads as a stroll.
        // Internal, not private: FightController.Beats reads this to time a
        // melee beat's impact (voice grunt, hit flash, damage number)
        // against when the lunge actually finishes closing the distance,
        // rather than against an unrelated fixed hold duration.
        internal const float LungeSeconds = 0.055f;
        private const float ReturnSeconds = 0.16f;

        // How far the figure stretches along its travel, at the fastest point
        // of each leg. The strike gets nearly three times the recovery's,
        // because that is the half being emphasised — the same asymmetry the
        // two easing curves below already express.
        //
        // 0.11 is where it stopped reading as weight and started reading as a
        // wobble. Six frames of drawing cannot show acceleration; this can, and
        // it is the difference between a figure that lunges and one that slides.
        private const float OutStretch = 0.11f;
        private const float BackStretch = 0.04f;

        // How much of the horizontal stretch comes back out of the height.
        // Not 1.0 — true volume conservation on a 2D silhouette overshoots,
        // because the art is not a rubber ball and the eye reads the vertical
        // loss twice as strongly as the horizontal gain.
        private const float VolumeRatio = 0.55f;

        // HOW MUCH OF THE BREATH COMES BACK OUT OF THE WIDTH. Smaller than
        // VolumeRatio because a breath is subtler than a swing, and non-zero
        // for the reason ApplyStretch's own note gives: a figure that only
        // gets bigger reads as zooming rather than as moving.
        private const float BreathVolumeRatio = 0.35f;

        private RectTransform _rect;
        private Vector2 _home;
        private Vector3 _baseScale;
        private Coroutine _running;

        // THE TWO DEFORMATIONS, HELD SEPARATELY AND WRITTEN TOGETHER.
        //
        // A swing is transient and belongs to a beat; a breath is continuous
        // and belongs to standing still. They were always going to overlap --
        // an idle figure is breathing at the instant something hits it -- and
        // the first arrangement that suggests itself, two callers each writing
        // localScale, means whichever wrote last wins and the other's
        // deformation vanishes for a frame. Keeping the AMOUNTS apart and
        // composing them in one writer costs a field and makes that
        // impossible.
        //
        // Multiplied rather than added in WriteScale, so neither has to know
        // the other exists or what range it works in.
        private float _stretch;
        private float _breath;

        public Vector2 Home => _home;

        // The depth scale the slot carries, which the stretch multiplies onto.
        // Exposed for the same reason Home is: AnchorStageSlots has to be able
        // to ask whether the mark it is about to assign is the one already
        // held, and the live localScale is mid-stretch during a swing.
        public Vector3 BaseScale => _baseScale;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            // The mark this figure returns to, and the depth scale it returns
            // to. Every animation goes back to these rather than accumulating
            // offsets -- otherwise a fight's worth of lunges would walk the
            // figure off its mark, and the stretch would compound onto itself.
            //
            // CAPTURED HERE AND RE-CAPTURED BY Rehome(). Awake alone was only
            // correct while a slot's position was fixed at build time, and it
            // is not: see Rehome.
            Rehome();
        }

        // Re-reads the mark and the scale from where the slot ACTUALLY is now.
        //
        // THE BUG THIS EXISTS FOR. These were captured in Awake and never
        // again, which quietly assumed a slot never moves. It moves constantly:
        // FightController.AnchorStageSlots re-spreads and re-scales the live
        // slots from FightStageAnchors every time the live count changes, so
        // that two rats occupy the two ENDS of the formation rather than
        // crowding its first two positions -- and an enemy dying re-lays every
        // survivor.
        //
        // From that moment the animator was returning each figure to where its
        // slot used to be, and multiplying its stretch onto the scale the slot
        // used to have. Both are absolute writes at the end of a tween, so the
        // figure did not drift -- it SNAPPED to a stale mark the instant a
        // lunge finished, and did it again on every swing. That is the
        // "sprites constantly get misplaced".
        //
        // It bit on the first encounter too, not only after a death: the slots
        // are anchored during setup, which is after Awake.
        //
        // THE READING FORM: takes the rect as it stands to be where the figure
        // belongs. Right for Awake, where the scene's own values are the
        // answer and there is nobody to ask.
        //
        // Anything that KNOWS the mark it wants -- AnchorStageSlots is the one
        // such caller -- should say so through the overload below rather than
        // assigning the rect and having this read it back.
        public void Rehome()
        {
            // Resolved here as well as in Awake, because a caller can reach
            // this before Unity has run Awake on a freshly activated slot --
            // and a Rehome that silently did nothing would leave the mark at
            // whatever the scene authored.
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;

            // WITHOUT WHATEVER THIS ANIMATOR IS CURRENTLY APPLYING.
            //
            // Reading localScale raw was correct while the only thing that ever
            // wrote it was a swing, because a swing resets to zero at both ends
            // and nothing called this mid-arc. A breath broke that: it is on
            // every frame an idle figure exists for, so "the rect is
            // undeformed right now" stopped being true and re-homing folded 2%
            // into the base, to be multiplied again on the next write.
            //
            // Divided back out rather than assumed absent, because this
            // overload's whole job is to read what is there -- and the animator
            // is the one thing that knows exactly how much of what is there is
            // its own doing.
            Rehome(_rect.anchoredPosition, UndeformedScale());
        }

        // The authoritative form: the caller states the mark and the size, and
        // this puts the figure on them.
        //
        // TOLD, NOT SHOWN, which is the difference that matters. AnchorStageSlots
        // used to assign the rect and then call the parameterless overload to
        // have it read back what had just been written -- two writers agreeing
        // by convention about the order they run in. That convention was
        // invisible, and a breath running between the two halves would have
        // broken it silently. One writer cannot be got out of order.
        public void Rehome(Vector2 mark, Vector3 baseScale)
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;

            // Anything in flight belongs to the old mark. Stopped rather than
            // finished, and deliberately WITHOUT the snap-home ResetToHome
            // does -- that would write the stale _home back over the position
            // the caller just assigned.
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            _home = mark;
            _baseScale = baseScale;

            // Cleared, so "after a re-home the figure stands at its authored
            // size" is true by construction rather than by whoever called it
            // having got the order right. The idle driver re-establishes the
            // breath on the next frame, which is not a length of time anybody
            // can see.
            _stretch = 0f;
            _breath = 0f;

            _rect.anchoredPosition = mark;
            WriteScale();
        }

        // The live scale with this animator's own deformation taken back out.
        //
        // The divisors are (1 + stretch) and its volume partner, all of which
        // sit within a few percent of one for every value Play, Punch and
        // SetBreath can produce. Guarded anyway: a divide by something near
        // zero here would not throw, it would return an infinity and park the
        // figure at an unrenderable size, which is far harder to recognise than
        // an exception.
        private Vector3 UndeformedScale()
        {
            float x = (1f + _stretch) * (1f - _breath * BreathVolumeRatio);
            float y = (1f - _stretch * VolumeRatio) * (1f + _breath);

            return new Vector3(
                Mathf.Abs(x) < 0.01f ? _rect.localScale.x : _rect.localScale.x / x,
                Mathf.Abs(y) < 0.01f ? _rect.localScale.y : _rect.localScale.y / y,
                _rect.localScale.z);
        }

        // Convenience overload for a pure sideways move (recoils, and any
        // caller that does not care about depth).
        public void Play(float dx, float holdSeconds)
        {
            Play(new Vector2(dx, 0f), holdSeconds);
        }

        // offset is signed in canvas units, relative to this figure's mark.
        // Vector2 rather than a bare dx because slots sit at different depths:
        // an attacker crossing to a target in the back row has to climb the
        // stage as well as cross it, or it lunges past the target's feet.
        //
        // outSeconds < 0 means the default snap (LungeSeconds) that every lunge
        // and recoil uses. A charge passes its own, longer duration so the
        // travel reads as a rush across the stage rather than a flick; the
        // return is always ReturnSeconds either way, since nothing is being
        // emphasised on the way back.
        public void Play(Vector2 offset, float holdSeconds, float outSeconds = -1f)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_running != null)
            {
                // A second hit landing mid-animation restarts the move rather
                // than stacking on it — stacking would compound the offset and
                // drag the figure across the field.
                StopCoroutine(_running);
                _rect.anchoredPosition = _home;
                ApplyStretch(0f);
            }

            _running = StartCoroutine(PlayRoutine(offset, holdSeconds, outSeconds));
        }

        // Snaps home immediately, for a fight ending or the panel closing
        // mid-animation — otherwise a figure could be left parked off-mark
        // for the next encounter, since _home is only captured in Awake.
        public void ResetToHome()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            if (_punching != null)
            {
                StopCoroutine(_punching);
                _punching = null;
            }

            if (_rect != null)
            {
                _rect.anchoredPosition = _home;

                // The stretch too. A coroutine stopped mid-arc leaves the
                // figure deformed, and unlike a position offset that is not
                // self-correcting -- nothing else writes localScale, so it
                // would simply stay squashed for the rest of the fight.
                ApplyStretch(0f);

                // AND THE BREATH, for the opposite reason. Nothing STOPS
                // pushing a breath -- the driver simply stops being called
                // when the fight ends -- so the last amount pushed would sit
                // there frozen, leaving the stage parked at whatever point of
                // the cycle it happened to end on. A stage at rest should be
                // at its authored size.
                SetBreath(0f);
            }
        }

        // ---- the squash a struck figure takes ---------------------------------

        // How hard a hit compresses the thing it lands on, and for how long.
        //
        // NEGATIVE STRETCH, which is the whole trick: ApplyStretch already
        // widens on X and takes it back out of Y, so feeding it a negative
        // amount does the opposite -- narrower and taller, which is what a body
        // does when something drives into it. One set of arithmetic, one
        // VolumeRatio, and a punch that cannot disagree with a lunge about what
        // deformation looks like on this stage.
        //
        // BOTH RAISED ON REQUEST after the first pass. -0.13 over 0.13s is a
        // real deformation on paper and was invisible in play: it is gone
        // inside eight frames, which is less time than the eye needs to find
        // the figure that was hit. The longer recovery is doing as much work
        // here as the deeper squash -- what reads as impact is the SPRING BACK,
        // and there was not enough of it to see.
        private const float PunchStretch = -0.26f;
        private const float PunchSeconds = 0.19f;

        // THE OTHER HALF OF A HIT LANDING. The attacker deforms as it swings
        // (OutStretch above) and the target only ever moved -- Recoil slides it
        // back and nothing changed its shape. On art with a six-frame flinch
        // that was survivable; on the flat single-frame poses most of the
        // roster still has, a hit moved a rigid cut-out and read as a bump.
        //
        // Snap in, ease out: the compression is instant and the recovery is
        // what the eye actually reads, which is the same asymmetry the lunge
        // makes between LungeSeconds and ReturnSeconds and for the same reason.
        //
        // DELIBERATELY NOT RESTARTING a running lunge. A figure that is
        // mid-swing when something hits it keeps swinging -- Play's own
        // stop-and-restart rule is about two MOVES fighting over one position,
        // and a punch writes only scale, so it can ride along with a move
        // rather than cancel it.
        public void Punch(float strength)
        {
            if (_rect == null || !isActiveAndEnabled || strength <= 0f) return;

            if (_punching != null) StopCoroutine(_punching);
            _punching = StartCoroutine(Punching(Mathf.Clamp01(strength)));
        }

        private Coroutine _punching;

        private IEnumerator Punching(float strength)
        {
            float seconds = FightBeatPlayer.Scaled(PunchSeconds);
            float peak = PunchStretch * strength;

            // The compression itself is one frame -- there is no wind-up on
            // being hit, and easing into it is exactly what makes a punch read
            // as a stretch.
            ApplyStretch(peak);

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;

                // Ease-out back to shape, so it springs rather than melts.
                float t = Mathf.Clamp01(elapsed / seconds);
                ApplyStretch(peak * (1f - t) * (1f - t));

                yield return null;
            }

            // Only if nothing else has taken the scale in the meantime -- a
            // lunge that started mid-punch owns the deformation from then on,
            // and writing zero here would flatten it a frame before its own
            // arc did.
            if (_running == null) ApplyStretch(0f);
            _punching = null;
        }

        private IEnumerator PlayRoutine(Vector2 offset, float holdSeconds, float outSeconds = -1f)
        {
            // SCALED, through the same seam SpellVfxPlayer and StageHitFlash
            // already use. This one did not, and the mismatch is visible rather
            // than academic: at 60x the beat player finished the whole round
            // while the figure was still sliding back, leaving it parked
            // mid-stage. Anything driven by a beat has to run on the beat's own
            // clock or it desynchronises from the thing it illustrates.
            var target = _home + offset;
            float outFor = outSeconds < 0f ? LungeSeconds : outSeconds;
            yield return TweenOut(_home, target, FightBeatPlayer.Scaled(outFor));
            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            yield return TweenBack(target, _home, FightBeatPlayer.Scaled(ReturnSeconds));
            _rect.anchoredPosition = _home;
            _running = null;
        }

        // Outbound uses ease-OUT (fast off the mark, decelerating into the
        // target) rather than the symmetrical smoothstep this used to use.
        // Smoothstep eases IN as well, so the strike began slowly — which is
        // exactly what made it read as gliding rather than striking.
        private IEnumerator TweenOut(Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float n = t / seconds;
                float k = 1f - (1f - n) * (1f - n);
                _rect.anchoredPosition = Vector2.Lerp(from, to, k);
                ApplyStretch(OutStretch * Arc(n));
                yield return null;
            }

            _rect.anchoredPosition = to;
            ApplyStretch(0f);
        }

        // The recovery keeps the old symmetrical ease: nothing is being
        // emphasised on the way back, and a snap home would undo the weight
        // the strike just established.
        private IEnumerator TweenBack(Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float n = t / seconds;
                _rect.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, n));
                ApplyStretch(BackStretch * Arc(n));
                yield return null;
            }

            _rect.anchoredPosition = to;
            ApplyStretch(0f);
        }

        // Zero at both ends, one in the middle. The stretch belongs to the
        // TRAVEL, so a figure standing still — at either end of the move — is
        // never caught mid-deformation, which is what would show up as a
        // permanently squashed actor if a coroutine were ever interrupted.
        private static float Arc(float n)
        {
            return Mathf.Sin(Mathf.Clamp01(n) * Mathf.PI);
        }

        // SQUASH AND STRETCH, and the only reason it can be this simple is that
        // a slot's pivot is (0.5, 0) — its own ground line. Scaling about that
        // point keeps the feet planted by construction, so none of this can
        // lift a figure off the floor. Scaling the SPRITE instead would have
        // needed the ground offset recomputing every frame, against the
        // offsetMin/offsetMax that GroundTheFigure owns.
        //
        // Volume is roughly conserved — wider is shorter — because a figure
        // that only gets bigger reads as zooming rather than as moving.
        //
        // Multiplied onto the CAPTURED base scale, never assigned outright: the
        // slot already carries its depth scale from FightStageAnchors, and
        // writing an absolute value here would flatten the back row to the size
        // of the front one on the first swing.
        private void ApplyStretch(float amount)
        {
            if (_rect == null) return;

            _stretch = amount;
            WriteScale();
        }

        // ---- the breath a standing figure takes -------------------------------

        // How much taller than its mark this figure is standing, right now.
        //
        // PUSHED IN, NOT CLOCKED HERE, and that is deliberate on both counts.
        // This class has never known what a CombatantState is and must not
        // start -- whether a figure is idle, and how hard its particular sheet
        // wants to breathe, are questions only FightController can answer. And
        // a value pushed from outside is a value a test can pin: an animator
        // that ran its own clock would make every stage screenshot differ by
        // when it was taken.
        //
        // Domain/Stage/BreathCurve is what produces the number. Nothing here
        // knows the shape of a breath, only how to wear one.
        public void SetBreath(float amount)
        {
            if (_rect == null) return;

            // A dirty check rather than an unconditional write: this is called
            // once per idle figure per frame, and assigning localScale marks
            // the transform and everything under it for a layout pass whether
            // or not the value changed. The epsilon is far below a pixel on
            // any figure on this stage.
            if (Mathf.Abs(_breath - amount) < 0.0001f) return;

            _breath = amount;
            WriteScale();
        }

        // THE ONE PLACE localScale IS ASSIGNED.
        //
        // Both deformations scale about the slot's (0.5, 0) pivot -- its own
        // ground line -- so neither can lift the figure off the floor and the
        // two cannot disagree about where the floor is.
        //
        // Multiplied onto the CAPTURED base scale, never assigned outright:
        // the slot already carries its depth scale from FightStageAnchors, and
        // writing an absolute value here would flatten the back row to the size
        // of the front one on the first swing.
        private void WriteScale()
        {
            if (_rect == null) return;

            // The swing: wider and shorter, or the inverse when Punch feeds it
            // a negative amount.
            float x = 1f + _stretch;
            float y = 1f - _stretch * VolumeRatio;

            // The breath: taller, and a little of that taken back out of the
            // width. Never negative -- see BreathCurve, which only ever grows
            // from the authored size.
            x *= 1f - _breath * BreathVolumeRatio;
            y *= 1f + _breath;

            _rect.localScale = new Vector3(_baseScale.x * x, _baseScale.y * y, _baseScale.z);
        }
    }
}
