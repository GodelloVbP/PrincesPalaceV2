using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

        // THE CROUCH BEFORE THE SNAP, for a figure whose art cannot draw one.
        //
        // An animated actor's wind-up frames already say "this is about to
        // happen"; a single drawing says nothing at all, so its blow arrives
        // with no warning and reads as a slide rather than a strike. This is
        // the transform standing in for those frames: a short load away from
        // the target, and the outbound tween is the release of it.
        //
        // Internal, and read by StaticSwing: the impact instant has
        // to be pushed back by exactly this much or the flash and the damage
        // number fire while the figure is still loading. One home, two
        // readers, no chance of them disagreeing.
        //
        // 0.07s is under two thirds of a beat's own gap and just over four
        // frames at 60Hz -- long enough to register as a decision, short
        // enough that a round of swings does not read as hesitation.
        internal const float AnticipationSeconds = 0.07f;

        // How far back the load goes, as a fraction of the travel it precedes.
        // A tenth: the eye reads the DIRECTION reversing, not the distance,
        // and anything larger turns a lean into a wind-up step.
        private const float AnticipationFraction = 0.10f;

        // Wider and shorter at the bottom of the crouch -- positive stretch,
        // the same sign a lunge uses and the opposite of Punch's. Half the
        // lunge's own OutStretch, because this is the figure gathering rather
        // than the figure travelling.
        private const float AnticipationStretch = 0.06f;

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

        // Whether a Play() is still under way -- exposed so a test can poll
        // for the lunge/recoil finishing rather than sleep out a fixed real
        // duration guessed at FightBeatPlayer.BeatSpeedMultiplier's default.
        public bool IsPlaying => _running != null;

        // THE AFTERIMAGE, a hint of motion blur on the fast parts.
        //
        // uGUI cannot blur a sprite honestly -- the atlas neighbours bleed into
        // any kernel that samples sideways -- so the fast-motion cue is the one
        // 2D animation has always used: a few faint copies of the drawing left
        // behind along the path, fading as the figure pulls away. A charge or a
        // roll leaves a short trail; a slow lean leaves nothing, because the
        // trail is spaced by DISTANCE travelled and a lean does not cover it.
        //
        // The source sprite is bound by FightController through the same door it
        // syncs the hit flash, because only it knows how to resolve a combatant
        // to its art. Absent that binding this whole thing is a no-op -- which
        // is the case for every headless test that never shows a sprite.
        private Image _spriteImage;
        private readonly List<Image> _ghosts = new List<Image>();
        private float _sinceGhost;

        // How many the OUTBOUND leg dropped, read by TweenBack to decide
        // whether the return earns its single contact ghost. Reset at the top
        // of each outbound leg rather than accumulated across a fight.
        private int _ghostsOutbound;

        // How far the figure must travel between two afterimages. Wide enough
        // that a lunge's short lean drops at most one and a cast drops none;
        // narrow enough that a charge across the stage leaves a readable trail.
        private const float GhostSpacing = 55f;

        // Faint and brief. A trail that reads as a second figure rather than as
        // speed is worse than none, so it starts low and is gone within a
        // couple of frames of real time.
        private const float GhostStartAlpha = 0.28f;
        private const float GhostFadeSeconds = 0.14f;

        // A hard cap on how many live at once, so a very long travel cannot
        // spawn an unbounded crowd. The pool grows to this and then reuses.
        private const int MaxGhosts = 5;

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

        // The art the afterimage copies, handed over by FightController whenever
        // the sprite under this slot changes -- the same seam it syncs the hit
        // flash through. Null-safe: an unbound animator simply never trails.
        public void BindSprite(Image spriteImage)
        {
            _spriteImage = spriteImage;
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
        //
        // leadSeconds > 0 buys an anticipation leg in FRONT of the travel --
        // see PlayRoutine. Zero by default and only ever passed for a
        // single-drawing actor's lunge, so every other caller in the game
        // moves byte-for-byte as it did before the parameter existed.
        public void Play(Vector2 offset, float holdSeconds, float outSeconds = -1f, float leadSeconds = 0f)
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

            _running = StartCoroutine(PlayRoutine(offset, holdSeconds, outSeconds, leadSeconds));
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
        // back and nothing changed its shape. Every pose in the game is one
        // drawing, so without this a hit moves a rigid cut-out and reads as a
        // bump rather than as a blow.
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

        private IEnumerator PlayRoutine(Vector2 offset, float holdSeconds, float outSeconds = -1f,
                                        float leadSeconds = 0f)
        {
            // SCALED, through the same seam SpellVfxPlayer and StageHitFlash
            // already use. This one did not, and the mismatch is visible rather
            // than academic: at 60x the beat player finished the whole round
            // while the figure was still sliding back, leaving it parked
            // mid-stage. Anything driven by a beat has to run on the beat's own
            // clock or it desynchronises from the thing it illustrates.
            var target = _home + offset;

            // The strike leaves from wherever the crouch got to, not from the
            // mark. Written as a variable rather than reading the live rect
            // unconditionally so the no-lead path still passes _home exactly,
            // which is what makes this change invisible to every existing
            // caller: a rect nudged by something else would otherwise start
            // the tween somewhere new.
            var from = _home;
            if (leadSeconds > 0f)
            {
                yield return Anticipate(offset, FightBeatPlayer.Scaled(leadSeconds));
                from = _rect.anchoredPosition;
            }

            float outFor = outSeconds < 0f ? LungeSeconds : outSeconds;
            yield return TweenOut(from, target, FightBeatPlayer.Scaled(outFor));
            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            yield return TweenBack(target, _home, FightBeatPlayer.Scaled(ReturnSeconds));
            _rect.anchoredPosition = _home;
            _running = null;
        }

        // The load before the release: a short drift AWAY from the travel,
        // squashing wider and shorter as it goes.
        //
        // EASE-IN, and it is the whole reason this reads as a wind-up rather
        // than as a stumble backwards. n*n leaves the figure almost still for
        // the first half of the lead and gathers it fast at the end, so the
        // motion the eye catches is the loading, immediately before the snap.
        // A linear drift over the same distance reads as the figure being
        // pushed.
        //
        // FEET STAY PLANTED BY CONSTRUCTION: this only ever writes
        // anchoredPosition on the x/y it was handed and ApplyStretch, and the
        // stretch scales about the slot's own (0.5, 0) ground pivot. There is
        // no rotation here on purpose -- the pilot has no verified ground
        // contact for a rotating actor, and a rotation about the wrong point
        // slides a figure through the floor.
        private IEnumerator Anticipate(Vector2 offset, float seconds)
        {
            var back = _home - offset * AnticipationFraction;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float n = t / seconds;
                float k = n * n;
                _rect.anchoredPosition = Vector2.Lerp(_home, back, k);
                ApplyStretch(AnticipationStretch * k);
                yield return null;
            }

            _rect.anchoredPosition = back;
            ApplyStretch(AnticipationStretch);
        }

        // Outbound uses ease-OUT (fast off the mark, decelerating into the
        // target) rather than the symmetrical smoothstep this used to use.
        // Smoothstep eases IN as well, so the strike began slowly — which is
        // exactly what made it read as gliding rather than striking.
        private IEnumerator TweenOut(Vector2 from, Vector2 to, float seconds)
        {
            // The TRAIL is dropped on the OUTBOUND leg only -- that is the
            // fast, emphasised half (ease-out, off the mark hard), and the one
            // the eye reads as the strike. The recovery is slow and unemphasised
            // and a trail on it would just look like the figure smearing home.
            // The return gets exactly ONE ghost, at the contact position, and
            // TweenBack's own note says why that is not the same thing.
            //
            // Accumulated from ZERO, so the first ghost lands one GhostSpacing
            // into the travel and a move shorter than that spacing -- a cast's
            // stillness, a small recoil -- leaves nothing behind. The trail is a
            // property of DISTANCE crossed, which is what makes it a fast-part
            // cue rather than something on every twitch.
            _sinceGhost = 0f;
            _ghostsOutbound = 0;
            Vector2 previous = from;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float n = t / seconds;
                float k = 1f - (1f - n) * (1f - n);
                Vector2 at = Vector2.Lerp(from, to, k);

                _sinceGhost += Vector2.Distance(at, previous);
                if (_sinceGhost >= GhostSpacing)
                {
                    EmitGhost();
                    _sinceGhost = 0f;
                    _ghostsOutbound++;
                }

                previous = at;
                _rect.anchoredPosition = at;
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
            // ONE GHOST AT THE CONTACT POSITION, and only one.
            //
            // Not a trail: TweenBack's own spacing would smear the figure home
            // and undo the recovery, which is what the outbound-only rule
            // above exists to prevent. This is a single copy left standing
            // where the blow landed, so the eye has something marking the
            // point of contact while the body pulls back off it. In a still
            // drawing that residue is most of what says a hit HAPPENED there.
            //
            // GATED ON THE OUTBOUND LEG having trailed, which is what keeps it
            // off every small move: a 45px recoil never reaches GhostSpacing,
            // so it emitted nothing on the way out and gets nothing here. The
            // ghost is still a property of distance crossed; this only borrows
            // the outbound leg's verdict rather than measuring again.
            if (_ghostsOutbound > 0)
            {
                EmitGhost();
                _ghostsOutbound = 0;
            }

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

        // ---- the afterimage ----------------------------------------------------

        // Leaves one faint copy of the current drawing where the figure is now
        // and lets it fade as the figure pulls away. A no-op without a bound
        // sprite or with no art on it, which is every headless test that never
        // shows a figure.
        private void EmitGhost()
        {
            if (_spriteImage == null || _spriteImage.sprite == null) return;
            var stage = _rect != null ? _rect.parent as RectTransform : null;
            if (stage == null) return;

            var ghost = FreeGhost(stage);
            if (ghost == null) return;

            var src = _spriteImage.rectTransform;

            // Snap the ghost onto the live sprite in the slot's own space, copy
            // its exact local transform, then peel it off into the stage with
            // the world transform PRESERVED. That hands the depth scale and the
            // mirror flip to Unity's own reparent math instead of recomputing a
            // lossy scale by hand -- and because the ghost then lives on the
            // stage rather than the slot, it stays put while the slot moves on.
            // The lag is the trail.
            ghost.rectTransform.SetParent(src.parent, worldPositionStays: false);
            ghost.rectTransform.localPosition = src.localPosition;
            ghost.rectTransform.localRotation = src.localRotation;
            ghost.rectTransform.localScale = src.localScale;
            ghost.rectTransform.pivot = src.pivot;
            ghost.rectTransform.sizeDelta = src.sizeDelta;
            ghost.rectTransform.SetParent(stage, worldPositionStays: true);
            ghost.rectTransform.SetAsFirstSibling();   // behind the figures

            ghost.sprite = _spriteImage.sprite;
            ghost.preserveAspect = _spriteImage.preserveAspect;
            ghost.color = new Color(1f, 1f, 1f, GhostStartAlpha);
            ghost.gameObject.SetActive(true);

            StartCoroutine(FadeGhost(ghost));
        }

        // An inactive pooled ghost, a fresh clone of the live sprite node while
        // the pool is under its cap, or the oldest reused once it is full.
        // Cloning the sprite GameObject inherits its Image, anchoring and
        // canvas setup for free rather than hand-building a node.
        private Image FreeGhost(RectTransform stage)
        {
            foreach (var g in _ghosts)
            {
                if (g != null && !g.gameObject.activeSelf) return g;
            }

            if (_ghosts.Count < MaxGhosts)
            {
                var clone = Instantiate(_spriteImage.gameObject, stage);
                clone.name = "Afterimage";

                // A ghost is scenery, never a target: it must not eat a click
                // meant for the figure it trails, and it carries none of the
                // slot's own behaviours.
                StripToImage(clone);

                var image = clone.GetComponent<Image>();
                image.raycastTarget = false;
                clone.SetActive(false);
                _ghosts.Add(image);
                return image;
            }

            // Full: reuse the oldest by rotating it to the back of the list.
            var oldest = _ghosts[0];
            _ghosts.RemoveAt(0);
            _ghosts.Add(oldest);
            return oldest;
        }

        // A clone of the sprite node can drag along whatever else sat on it;
        // for a ghost none of it should run. Only the Image (and its
        // RectTransform/CanvasRenderer) is wanted.
        private static void StripToImage(GameObject clone)
        {
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
            {
                if (child != clone.transform) Destroy(child.gameObject);
            }

            foreach (var behaviour in clone.GetComponents<MonoBehaviour>())
            {
                if (!(behaviour is Image)) Destroy(behaviour);
            }
        }

        private IEnumerator FadeGhost(Image ghost)
        {
            float fade = FightBeatPlayer.Scaled(GhostFadeSeconds);
            if (fade <= 0f)
            {
                ghost.gameObject.SetActive(false);
                yield break;
            }

            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                if (ghost == null) yield break;
                float a = GhostStartAlpha * (1f - t / fade);
                ghost.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }

            if (ghost != null) ghost.gameObject.SetActive(false);
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
