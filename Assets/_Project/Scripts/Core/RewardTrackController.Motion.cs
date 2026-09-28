using System.Collections;
using UnityEngine;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Everything on this screen that moves.
    //
    // TWO KINDS, and keeping them apart is what keeps the frame cost honest.
    // The AMBIENT cues -- shimmer, breathe, pulse, bob -- are loops with no
    // beginning and no end, so they are arithmetic over Time.time in one
    // Update; a coroutine per loop would be four coroutines that never finish
    // and four more objects for the GC to keep. The EVENT cues -- fly-in,
    // glide, ignite, burst -- have a start, a duration and a done, which is
    // exactly what a coroutine is for.
    //
    // Durations are authored by feel rather than tuned against a frame
    // budget. Unscaled time throughout: this panel opens over a menu, and a
    // menu that pauses the game would otherwise freeze its own animation.
    //
    // REDUCE-MOTION IS NOT IMPLEMENTED, and that is a gap rather than a
    // decision. Handoff section 7 asks for the fly-in, shimmer, breathe, pulse
    // and bob to drop while the burst, the ignite and both glides stay -- they
    // carry meaning and losing them loses feedback. There is no accessibility
    // setting in GameSettings to read, and inventing one for a single screen
    // would put the switch somewhere no other screen could find it. The split
    // below is written so that switch is a one-line guard when it exists:
    // Animate() is every cue that would go, and nothing else is in it.
    public partial class RewardTrackController
    {
        private const float FlyInDelay = 0.38f;
        private const float FlyInSeconds = 2.1f;
        private const float GlideSeconds = 0.46f;
        private const float FollowSeconds = 0.7f;
        private const float AdvanceSeconds = 0.6f;
        // 0.7: the burst's last part finishes by 70% of this duration, so a
        // longer value would just be a fully transparent node still being
        // written to every frame.
        private const float BurstSeconds = 0.7f;
        private const float BurstStagger = 0.13f;

        // The seal stamps home in the burst's first third, so it is the last
        // thing still moving when the light goes out.
        private const float SealStampSeconds = 0.24f;
        private const float CardSwapSeconds = 0.28f;
        private const float CardRisePixels = 7f;

        // 18: long enough that the light drifts across the visible rail
        // rather than crossing it, about 90 pixels a second. A cue that is
        // always present has to be slow enough to stop being an event.
        private const float ShimmerLoop = 18f;

        // How far the shimmer travels, centred on the player's node. One
        // window's width, so the light is somewhere on screen for the whole
        // loop whenever the rail is showing where the player stands -- which is
        // where it opens and where every glide returns it to.
        private const float SweepSpan = SystemMenuLayout.PanelWidth;
        private const float BreatheLoop = 3.4f;
        private const float PulseLoop = 2f;
        private const float BobLoop = 2.6f;
        private const float BobPixels = 5f;
        private const float PipLoop = 1.6f;

        // Slower than the here-node's 3.4s breathe, and deliberately: that one
        // is about the player and should read as a pulse, and twelve of these
        // are on screen at once. 5.2s is slow enough that a still frame of the
        // rail looks composed rather than caught mid-flicker.
        private const float AuraLoop = 5.2f;

        // Twenty-six seconds for one turn, which is slow enough to be noticed
        // only on purpose. A dashed ring at any speed the eye can follow is a
        // loading spinner, and a loading spinner on twelve nodes is a screen
        // that looks like it is waiting for something.
        private const float OrbitLoop = 26f;

        // THE TEST SEAM, same shape as ReckoningController.SpeedMultiplier --
        // every event cue here (fly-in, glide, follow, advance, burst, card
        // swap) is driven off Time.unscaledDeltaTime for the reason in this
        // file's header (a menu-opened panel must not freeze), so a test
        // waiting for one of them out pays real wall time regardless of
        // Time.timeScale. 1 outside a test, which changes nothing about play.
        // RewardTrackClaimTests sets/resets it in [SetUp]/[TearDown].
        public static float SpeedMultiplier = 1f;

        private Coroutine _glide;
        private Coroutine _cardSwap;
        private Coroutine _bursts;

        // Which rig the next burst takes. Kept across claims rather than reset,
        // so two collections in quick succession do not both start on rig 0 and
        // cut each other off.
        private int _nextRig;

        // Quantised depth-of-field, one entry per node.
        //
        // THE REASON THIS CACHE EXISTS is a drag: every pointer-move recomputes
        // the falloff for ninety-nine nodes, and writing a colour to a uGUI
        // Graphic dirties its mesh. Five graphics a node is five hundred
        // rebuilds a frame for a gesture where about ten nodes actually changed
        // -- the rest are pinned at the floor and were already there. Rounding
        // to 1/32 and skipping what did not move bounds the work to what moved.
        private byte[] _fade;

        private void Animate()
        {
            float t = Time.unscaledTime;

            AnimateShimmer(t);
            AnimateHalo(t);
            AnimateMilestones(t);
            AnimatePulses(t);
            AnimateCaret(t);
            AnimateCollectPip(t);
        }

        // The twelve landmarks, breathing and turning.
        //
        // NOT IN LOCKSTEP, which is the opposite of the waiting pulse below and
        // the reason is what each one means. The pulse is a STATEMENT -- these
        // levels are owed you -- and forty of them in time reads as one
        // statement about how much. This is ambience, and twelve gold auras
        // swelling together reads as a machine cycling: the eye picks up the
        // rhythm and stops seeing twelve objects.
        //
        // The offset is the index times the golden ratio's fractional part, so
        // no two of the twelve land near each other and none of them repeats
        // the spacing of the pair before it.
        //
        // ONE PHASE DRIVES BOTH the aura's scale and its opacity, and the ring
        // turns at a constant rate through all of it. A glow that brightens
        // without growing reads as a light being turned up; one that grows
        // without brightening reads as something inflating. Together they read
        // as breathing, which is the word the design asks for.
        private void AnimateMilestones(float t)
        {
            if (milestoneAuras == null && milestoneRings == null) return;

            int i = 0;
            foreach (int level in RewardTrackLayout.MilestoneLevels())
            {
                float phase = Easing.SmoothStep(PingPong(t / AuraLoop + i * 0.618f));
                float fade = FadeAt(level - RewardTrackLayout.FirstLevel);
                var state = RewardTrack.StateOf(level, _level, _claimed);

                // Brighter the further along the track you have got with it.
                // An unreached landmark is still alive -- it is a place on the
                // rail whether or not it is yours yet -- but it is not yours,
                // and the aura is the cheapest place to say so without adding
                // a fifth colour to a node.
                float weight =
                    state == TrackNodeState.ToCome ? 0.45f :
                    state == TrackNodeState.Collected ? 0.85f : 1f;

                if (i < (milestoneAuras?.Length ?? 0) && milestoneAuras[i] != null)
                {
                    // 0.22 TO 0.55, which is far higher than it looks on the
                    // screen: the glow's bright middle is behind an opaque
                    // disc, so what shows is its outer half, already down to
                    // about a third. Authored at what it should look like, it
                    // was invisible.
                    milestoneAuras[i].color = WithAlpha(
                        Gold, Mathf.Lerp(0.18f, 0.45f, phase) * weight * fade);

                    milestoneAuras[i].rectTransform.localScale =
                        Vector3.one * Mathf.Lerp(0.92f, 1.1f, phase);
                }

                // Every ring turns the same way and at the same rate. The
                // alternative -- counter-rotation, or a speed per node -- was
                // rejected on the same ground as the phase offset above:
                // twelve mechanisms disagreeing with each other is a lot of
                // movement to put behind text somebody is trying to read.
                //
                // ROTATION ONLY. The ring's colour belongs to PaintNode, which
                // already carries the state and the depth-of-field fade for
                // every ring on the rail; writing it here as well would be two
                // passes deciding one value, and the one that ran last would
                // win by accident rather than by design.
                if (i < (milestoneRings?.Length ?? 0) && milestoneRings[i] != null)
                {
                    milestoneRings[i].rectTransform.localRotation =
                        Quaternion.Euler(0f, 0f, -360f * Mathf.Repeat(t / OrbitLoop, 1f));
                }

                i++;
            }
        }

        // The dot on the collect button, breathing on a 1.6s loop.
        //
        // The button only exists when the track owes something, so this is the
        // screen's one notification -- and a notification that does not move is
        // a label. It shares the pulse's argument and not its ring: one dot
        // brightening is an alert, and the same dot throwing rings would be a
        // second claim gesture on a control that is already the shortcut for
        // the first.
        private void AnimateCollectPip(float t)
        {
            if (collectPip == null || !collectPip.gameObject.activeInHierarchy) return;

            float phase = Easing.SmoothStep(PingPong(t / PipLoop));

            collectPip.color = WithAlpha(Gold, Mathf.Lerp(0.55f, 1f, phase));
            collectPip.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.13f, phase);
        }

        // 300px of gold crossing the player's own node, linear and infinite.
        // Linear rather than eased because it never stops: an eased loop has a
        // visible seam where it restarts, which is exactly what a shimmer must
        // not do.
        //
        // The sweep is bound to the player's node instead of to the content:
        // a window's width of travel, centred on where they actually are, so
        // it stays visible rather than crossing a rail mostly out of frame.
        // The panel opens centred there, so the light is on screen from the
        // first frame and stays on it.
        private void AnimateShimmer(float t)
        {
            if (shimmerRect == null || shimmer == null) return;

            float phase = Mathf.Repeat(t / ShimmerLoop, 1f);
            float span = SweepSpan;
            float centre = RewardTrackLayout.NodeOffsetX(_level);

            shimmerRect.anchoredPosition = new Vector2(
                centre - span * 0.5f + phase * span, shimmerRect.anchoredPosition.y);

            // Faded at both ends of the sweep, so the light arrives and leaves
            // rather than appearing at one edge and vanishing at the other.
            float edge = Mathf.Min(phase, 1f - phase) / 0.08f;
            shimmer.color = WithAlpha(Gold, 0.42f * Mathf.Clamp01(edge));
        }

        // The player's own node, breathing: halo scale 1 -> 1.5, opacity
        // .25 -> .6, ease-in-out, alternating.
        private void AnimateHalo(float t)
        {
            if (hereHaloRect == null || hereHalo == null) return;
            if (!hereHaloRect.gameObject.activeSelf) return;

            float phase = Easing.SmoothStep(PingPong(t / BreatheLoop));

            hereHaloRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, phase);

            // PALE, not gold. The node it is behind draws pale metal -- a gold
            // halo round a white disc reads as the gold state seen through
            // something, which is the one thing this node must not look like.
            hereHalo.color = WithAlpha(Pale, Mathf.Lerp(0.25f, 0.6f, phase));
        }

        // A 1px gold ring leaving every waiting node: scale .9 -> 2.1, opacity
        // .9 -> 0, ease-out.
        //
        // IN LOCKSTEP, all of them. Staggering was considered and is wrong
        // here: a migrated character can have forty waiting levels in one
        // window, and forty rings on forty phases reads as noise, where forty
        // in time reads as a single statement about how much is owed.
        private void AnimatePulses(float t)
        {
            if (pulses == null) return;

            float phase = Mathf.Repeat(t / PulseLoop, 1f);
            float eased = 1f - (1f - phase) * (1f - phase) * (1f - phase);

            float scale = Mathf.Lerp(0.9f, 2.1f, eased);
            var colour = WithAlpha(Gold, Mathf.Lerp(0.9f, 0f, eased));

            for (int i = 0; i < pulses.Length; i++)
            {
                if (pulses[i] == null || !pulses[i].gameObject.activeSelf) continue;

                pulses[i].rectTransform.localScale = Vector3.one * scale;
                pulses[i].color = colour;
            }
        }

        // The NEXT mark bobbing 5px, so the one thing on the rail that is not
        // a node does not read as another one.
        private void AnimateCaret(float t)
        {
            if (nextMark == null || !nextMark.gameObject.activeSelf) return;

            float phase = Easing.SmoothStep(PingPong(t / BobLoop));

            nextMark.anchoredPosition = new Vector2(
                nextMark.anchoredPosition.x,
                RewardTrackLayout.NextMarkCentreY - BobPixels * phase);
        }

        // ---- the one gate every animation on this panel goes through ----------

        // WHETHER THIS SCREEN IS IN A POSITION TO ANIMATE ANYTHING.
        //
        // Reached from OnDisable, which is the part that is not obvious.
        // HoverIndex reports an EXIT from its own OnDisable -- deliberately, so
        // a tooltip anchored to something that vanishes mid-hover cannot stay
        // open forever -- and on this screen that exit runs the card swap.
        // Closing the panel with a node hovered therefore asked an object that
        // is already inactive to start a coroutine, and Unity refuses out loud:
        //
        //   Coroutine couldn't be started because the game object
        //   'RewardTrackPanel' is inactive!
        //
        // Reported from play, not caught here.
        //
        // OnDisable's own StopAllCoroutines looks like it covers this and does
        // not. Unity gives no ordering guarantee between a child's OnDisable
        // and its parent's, so the exit arrives while the panel is already down
        // and before the controller has torn anything off -- stopping
        // coroutines does nothing about one that has not started yet.
        private bool CanAnimate => isActiveAndEnabled && gameObject.activeInHierarchy;

        // Starts a panel animation, or does not.
        //
        // EVERY start in this file goes through here rather than the two that
        // were reachable from a hover, because "which of these can OnDisable
        // reach" is a question about call graphs that changes whenever somebody
        // wires a new control -- and the answer for all of them while the panel
        // is offscreen is the same anyway: there is nothing to animate on a
        // screen nobody is looking at.
        //
        // Returns null so the callers' `_glide`/`_bursts`/`_cardSwap` handles
        // are cleared rather than left pointing at a coroutine that never ran,
        // which is what StopCoroutine on the next open would otherwise be
        // handed.
        private Coroutine Animate(IEnumerator routine)
        {
            if (routine == null || !CanAnimate) return null;

            return StartCoroutine(routine);
        }

        // ---- the fly-in --------------------------------------------------------

        // OPENS ON THE PLAYER, not on level 2, and travels there rather than
        // cutting.
        //
        // A hundred-level rail opened at its left edge shows a wall of things
        // already collected and nothing about what is next, which is the one
        // question the screen exists to answer. Flying in from that edge says
        // how far along the player is in a way that arriving pre-scrolled
        // cannot -- the scroll bar of a nineteen-thousand-pixel rail is the
        // only honest scale on the screen, and this is the moment it is legible.
        private void BeginFlyIn()
        {
            if (content == null || viewport == null) return;

            CancelGlide();
            _glide = Animate(FlyIn());
        }

        private IEnumerator FlyIn()
        {
            float target = RewardTrackLayout.ScrollFor(_level, viewport.rect.width);
            float from = RewardTrackLayout.ClampScroll(0f, viewport.rect.width);

            SetScroll(from);

            float elapsed = -FlyInDelay;
            while (elapsed < FlyInSeconds)
            {
                elapsed += Time.unscaledDeltaTime * SpeedMultiplier;

                if (elapsed >= 0f)
                {
                    SetScroll(Mathf.Lerp(from, target,
                        Easing.OutCubic(Mathf.Clamp01(elapsed / FlyInSeconds))));
                }

                yield return null;
            }

            SetScroll(target);
            _glide = null;
        }

        // ---- glides -------------------------------------------------------------

        // Clicking a node that cannot be collected centres it instead. NEVER A
        // NO-OP: a hundred dots that do nothing when pressed teach the player
        // that none of them do.
        private void GlideTo(int level) => GlideTo(level, GlideSeconds);

        private void GlideTo(int level, float seconds)
        {
            if (content == null || viewport == null) return;

            CancelGlide();
            _glide = Animate(Glide(
                RewardTrackLayout.ScrollFor(level, viewport.rect.width), seconds));
        }

        private IEnumerator Glide(float target, float seconds)
        {
            float from = content.anchoredPosition.x;
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime * SpeedMultiplier;
                SetScroll(Mathf.Lerp(from, target, Easing.OutCubic(Mathf.Clamp01(elapsed / seconds))));
                yield return null;
            }

            SetScroll(target);
            _glide = null;
        }

        // A drag or a wheel cancels whatever is in flight. A gesture fighting an
        // animation is the single most common way a scrolled panel feels broken,
        // and it is invisible in a screenshot.
        private void CancelGlide()
        {
            if (_glide == null) return;

            StopCoroutine(_glide);
            _glide = null;
        }

        // ---- claiming -----------------------------------------------------------

        // One burst per level collected, ascending, 130ms apart.
        //
        // ONE NODE RE-FIRED rather than a hundred sleeping particles: it moves
        // to each level in turn, which is also what makes the stagger readable
        // as a sweep along the rail rather than as a hundred simultaneous
        // flashes.
        private void BeginClaimBursts(int fromLevel, int throughLevel)
        {
            if (burstRoots == null || burstRoots.Length == 0) return;
            if (throughLevel < fromLevel) return;

            if (_bursts != null) StopCoroutine(_bursts);
            _bursts = Animate(ClaimBursts(fromLevel, throughLevel));
        }

        // ONE COROUTINE PER LEVEL, on its own rig -- a shared node would cut a
        // burst off mid-flight when the next level's starts, smearing a
        // multi-level collect into one streak instead of separate bursts.
        //
        // Rigs are taken round-robin, so a burst gets four stagger periods --
        // 520ms of its 700 -- before its rig is needed again. The overlap that
        // remains is at the tail, where a burst is nearly transparent anyway.
        private IEnumerator ClaimBursts(int fromLevel, int throughLevel)
        {
            for (int level = fromLevel; level <= throughLevel; level++)
            {
                FireBurst(level);

                // ACCELERATING, slightly. A fixed stagger over thirty-five
                // levels is thirty-five identical events in a row; shortening
                // it as the chain runs turns a list into a cascade, which is
                // the shape every game that does this well uses.
                float wait = Mathf.Lerp(
                    BurstStagger, BurstStagger * 0.55f,
                    Mathf.InverseLerp(fromLevel, fromLevel + 12f, level));

                float elapsed = 0f;
                while (elapsed < wait)
                {
                    elapsed += Time.unscaledDeltaTime * SpeedMultiplier;
                    yield return null;
                }
            }

            _bursts = null;
        }

        // ONE ROUND-ROBIN RIG PICK, shared by ClaimBursts (a multi-level claim)
        // and OnLevelGained (a level arriving while the panel is open): picking
        // it two different ways would desync which rig is "next".
        private void FireBurst(int level)
        {
            if (burstRoots == null || burstRoots.Length == 0) return;

            Animate(Burst(_nextRig, level));
            _nextRig = (_nextRig + 1) % burstRoots.Length;
        }

        // One burst, from ignition to nothing.
        private IEnumerator Burst(int rig, int level)
        {
            var root = Rig(burstRoots, rig);
            if (root == null) yield break;

            root.anchoredPosition = new Vector2(RewardTrackLayout.NodeOffsetX(level), 0f);
            root.gameObject.SetActive(true);

            // The seal stamping onto the node is part of the same event and is
            // driven from here, because it belongs to a LEVEL rather than to a
            // rig -- the pip is one of the ninety-nine, not one of the four.
            int index = level - RewardTrackLayout.FirstLevel;

            float t = 0f;
            while (t < BurstSeconds)
            {
                t += Time.unscaledDeltaTime * SpeedMultiplier;
                PaintBurst(rig, Mathf.Clamp01(t / BurstSeconds));
                StampSeal(index, Mathf.Clamp01(t / SealStampSeconds));
                yield return null;
            }

            PaintBurst(rig, 1f);
            StampSeal(index, 1f);
            root.gameObject.SetActive(false);
        }

        // THE FOUR PARTS, each on its own curve, which is the whole difference
        // between an impact and a thing getting bigger.
        //
        // The flash is fastest and dies first; the shockwave outlives it and
        // travels furthest; the rays turn while they fan, so the burst is not
        // radially symmetric for its whole life; the sparks leave late, in
        // sequence, and fall as they go. Nothing here shares a duration with
        // anything else on purpose.
        private void PaintBurst(int rig, float t)
        {
            // The core: a white-hot flash cooling to gold as it dies. It is
            // over in the first 60% of the burst, which is what makes the
            // shockwave look like it is leaving something behind.
            var core = Rig(burstCores, rig);
            if (core != null)
            {
                float u = Mathf.Clamp01(t / 0.6f);

                core.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 2.1f, Easing.OutCubic(u));
                core.color = WithAlpha(
                    Color.Lerp(Color.white, Gold, Easing.OutCubic(u)),
                    (1f - u) * (1f - u));
            }

            // The shockwave: all the way out, thinning as it goes because a
            // scaled hairline does exactly that, and fading on a curve that
            // holds its brightness early and drops it late.
            var ring = Rig(burstRings, rig);
            if (ring != null)
            {
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 3.4f, Easing.OutCubic(t));
                ring.color = WithAlpha(Gold, 0.95f * Mathf.Pow(1f - t, 1.6f));
            }

            // The rays, turning as they fan. Twenty-two degrees over the whole
            // burst is not enough to read as a spin and is exactly enough to
            // stop the shape being the same shape twice.
            var rays = Rig(burstRays, rig);
            if (rays != null)
            {
                float u = Mathf.Clamp01(t / 0.8f);

                rays.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.55f, Easing.OutCubic(u));
                rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f * u);
                rays.color = WithAlpha(Gold, 0.85f * (1f - u) * (1f - u));
            }

            PaintSparks(rig, t);
        }

        // Six sparks thrown clear, each leaving a little after the one before
        // and falling as it travels.
        //
        // THE DIRECTIONS ARE AUTHORED, not random. A burst wants to look the
        // same every time it fires -- a player collecting thirty-five levels
        // sees this thirty-five times in five seconds, and randomness there
        // reads as noise rather than as variety. The spread is biased upward
        // because the level number sits directly below the node and debris
        // raining onto a figure is debris obscuring it.
        private static readonly Vector2[] SparkDirections =
        {
            new Vector2(0.26f, 0.97f),
            new Vector2(0.87f, 0.50f),
            new Vector2(-0.71f, 0.71f),
            new Vector2(-0.97f, 0.26f),
            new Vector2(0.64f, -0.77f),
            new Vector2(-0.42f, -0.91f),
        };

        private void PaintSparks(int rig, float t)
        {
            if (burstSparks == null) return;

            int count = RewardTrackLayout.BurstSparkCount;

            for (int s = 0; s < count; s++)
            {
                int i = rig * count + s;
                if (i >= burstSparks.Length || burstSparks[i] == null) continue;

                // Each leaves 55ms after the last, so the six read as one thing
                // shattering rather than as six things starting together.
                float delay = s * 0.055f / BurstSeconds;
                float u = Mathf.Clamp01((t - delay) / (1f - delay));

                var direction = SparkDirections[s % SparkDirections.Length];
                float reach = RewardTrackLayout.BurstSparkReach * Easing.OutCubic(u);

                // Gravity, as the square of the time it has been in the air.
                // Sparks that fly in straight lines are a firework; sparks that
                // arc are debris.
                float fall = 26f * u * u;

                burstSparks[i].rectTransform.anchoredPosition =
                    new Vector2(direction.x * reach, direction.y * reach - fall);

                burstSparks[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.25f, u);
                burstSparks[i].color = WithAlpha(Gold, (1f - u) * (1f - u));
            }
        }

        // The seal landing on the node it was just earned by: 2.4x down to 1
        // with a little overshoot, over the burst's first quarter.
        //
        // A pip that simply appears is a pip the player never sees appear --
        // it is nine pixels across and it arrives during an explosion. Stamped,
        // it is the last thing to settle and therefore the thing the eye ends
        // on, which is where the record of what just happened should be.
        private void StampSeal(int index, float u)
        {
            if (seals == null || index < 0 || index >= seals.Length) return;
            if (seals[index] == null || !seals[index].activeSelf) return;

            seals[index].transform.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, EaseOutBack(u));
        }

        private static T Rig<T>(T[] array, int rig) where T : class =>
            array != null && rig >= 0 && rig < array.Length ? array[rig] : null;

        // ---- the card ------------------------------------------------------------

        // 280ms fade and a 7px rise, so the card reads as a new card rather
        // than as the same one with different words in it.
        private void BeginCardSwap(int level)
        {
            // The instant repaint is also what an OFFSCREEN swap gets -- see
            // Animate, which returns null rather than starting anything. The
            // card still ends on the right content; the 280ms rise it skips was
            // going to play on a panel nobody can see.
            if (cardRect == null || !CanAnimate)
            {
                PaintCard(level);
                return;
            }

            if (_cardSwap != null) StopCoroutine(_cardSwap);
            _cardSwap = Animate(CardSwap(level));
        }

        private IEnumerator CardSwap(int level)
        {
            PaintCard(level);

            float baseY = RewardTrackLayout.CardCentreY;
            float elapsed = 0f;

            while (elapsed < CardSwapSeconds)
            {
                elapsed += Time.unscaledDeltaTime * SpeedMultiplier;
                float t = Easing.OutCubic(Mathf.Clamp01(elapsed / CardSwapSeconds));

                cardRect.anchoredPosition =
                    new Vector2(cardRect.anchoredPosition.x, baseY - CardRisePixels * (1f - t));

                yield return null;
            }

            cardRect.anchoredPosition = new Vector2(cardRect.anchoredPosition.x, baseY);
            _cardSwap = null;
        }

        // ---- levelling up while the panel is open --------------------------------

        // Ignite the new node, run the rail out to it, and follow.
        //
        // Reachable from the debug menu's level grant rather than from a fight
        // -- a descent cannot be running while the system menu is open. Built
        // anyway because it is the same three cues the fly-in and the glide
        // already own, and because a track that does not react to a level
        // arriving is the one thing a battle pass must not be.
        private void OnLevelGained(int before, int newLevel)
        {
            if (railFill != null)
            {
                Animate(AdvanceRail(RewardTrackLayout.NodeX(newLevel)));
            }

            GlideTo(newLevel, FollowSeconds);

            // THE SAME RIG THE CLAIM USES, once per level crossed.
            //
            // Ignition and collection are different events and were drawn by
            // different code against the same node; now that a burst is four
            // parts, keeping a second hand-rolled version of it would be two
            // explosions to keep in step. What separates them is not the shape
            // -- it is that this one leaves a reward WAITING and the other
            // takes it away, which the pulse and the seal already say.
            //
            // A gain can itself cross more than one level (a fight granting
            // enough XP to skip a level outright), the same as a multi-level
            // claim does -- FireBurst per level, not one burst for the jump.
            for (int level = before + 1; level <= newLevel; level++)
            {
                FireBurst(level);
            }
        }

        // WIDTH, LEFT-PIVOTED, never anchors -- the parent is a 19,000px rect
        // and an anchor-driven fill would be measured against it. This is the
        // animated form of the same rule PaintRail states.
        private IEnumerator AdvanceRail(float target)
        {
            float from = railFill.sizeDelta.x;
            float elapsed = 0f;

            while (elapsed < AdvanceSeconds)
            {
                elapsed += Time.unscaledDeltaTime * SpeedMultiplier;
                float t = Easing.OutCubic(Mathf.Clamp01(elapsed / AdvanceSeconds));

                railFill.sizeDelta = new Vector2(Mathf.Lerp(from, target, t), railFill.sizeDelta.y);
                yield return null;
            }

            railFill.sizeDelta = new Vector2(target, railFill.sizeDelta.y);
        }

        // ---- depth of field --------------------------------------------------------

        // Nodes fall from full strength to .32 once they are 300px off the
        // window's centre, over the next 620. The rail is 1600 wide and holds
        // about eight and a half nodes; without this, all eight compete equally
        // and the screen reads as a wall of captions.
        private void PaintDepthOfField()
        {
            if (dots == null || content == null) return;
            if (_fade == null || _fade.Length != dots.Length) _fade = new byte[dots.Length];

            float scroll = content.anchoredPosition.x;

            for (int i = 0; i < dots.Length; i++)
            {
                int level = RewardTrackLayout.FirstLevel + i;

                float fade = RewardTrackLayout.DepthOfFieldAt(
                    RewardTrackLayout.DistanceFromWindowCentre(level, scroll));

                // Quantised to 1/32, so a drag only repaints what actually
                // moved. See _fade's own header for why that matters.
                byte step = (byte)Mathf.RoundToInt(fade * 31f);
                if (_fade[i] == step) continue;

                _fade[i] = step;
                PaintNode(i, level);
            }
        }

        // The same falloff with the skip taken out: fills the cache for every
        // node and paints nothing.
        //
        // What Refresh needs, and what PaintDepthOfField cannot give it. That
        // one exists to do as little as possible during a drag, so it repaints
        // only the nodes whose fade moved -- which is exactly wrong after a
        // claim, where the fade did not move and the STATE did.
        private void RefreshFade()
        {
            if (dots == null || content == null) return;
            if (_fade == null || _fade.Length != dots.Length) _fade = new byte[dots.Length];

            float scroll = content.anchoredPosition.x;

            for (int i = 0; i < _fade.Length; i++)
            {
                float fade = RewardTrackLayout.DepthOfFieldAt(
                    RewardTrackLayout.DistanceFromWindowCentre(
                        RewardTrackLayout.FirstLevel + i, scroll));

                _fade[i] = (byte)Mathf.RoundToInt(fade * 31f);
            }
        }

        // What PaintNode multiplies every colour it writes by. Read back out of
        // the cache rather than recomputed, so the painted alpha and the value
        // the skip-test compares against cannot disagree.
        private float FadeAt(int i) =>
            _fade == null || i >= _fade.Length ? 1f : _fade[i] / 31f;

        // ---- easing ------------------------------------------------------------------

        // Overshoots its target and settles back, which is what makes a stamp
        // land rather than arrive. The 1.70158 is the standard back-ease
        // constant -- it is what gives roughly a 10% overshoot.
        private static float EaseOutBack(float t)
        {
            const float C1 = 1.70158f;
            const float C3 = C1 + 1f;

            float inverse = t - 1f;
            return 1f + C3 * inverse * inverse * inverse + C1 * inverse * inverse;
        }

        // 0 -> 1 -> 0 over one period, for loops that alternate rather than repeat.
        private static float PingPong(float t) => 1f - Mathf.Abs(Mathf.Repeat(t, 2f) - 1f);

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
