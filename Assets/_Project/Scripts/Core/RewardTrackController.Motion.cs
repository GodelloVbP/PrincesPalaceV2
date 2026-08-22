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
    // Durations are the handoff's, authored rather than tuned against a frame
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
        private const float IgniteSeconds = 0.9f;
        private const float BurstSeconds = 0.9f;
        private const float BurstStagger = 0.13f;
        private const float CardSwapSeconds = 0.28f;
        private const float CardRisePixels = 7f;

        // 18, up from 9. Twice as long over a twelfth of the distance, so the
        // light drifts across the visible rail rather than crossing it: about
        // 90 pixels a second against the 2,100 it used to travel at. A cue that
        // is always present has to be slow enough to stop being an event.
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

        private Coroutine _glide;
        private Coroutine _cardSwap;
        private Coroutine _bursts;

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
                float phase = EaseInOut(PingPong(t / AuraLoop + i * 0.618f));
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

            float phase = EaseInOut(PingPong(t / PipLoop));

            collectPip.color = WithAlpha(Gold, Mathf.Lerp(0.55f, 1f, phase));
            collectPip.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.13f, phase);
        }

        // 300px of gold crossing the player's own node, linear and infinite.
        // Linear rather than eased because it never stops: an eased loop has a
        // visible seam where it restarts, which is exactly what a shimmer must
        // not do.
        //
        // IT USED TO WALK THE FULL 19,000 AND WAS THEREFORE ALMOST NEVER ON
        // SCREEN. The window shows 1,600 of the rail, so a light travelling the
        // whole content was inside it for about one second in every twelve --
        // and it crossed at 2,100px a second when it did. The rail spent eleven
        // seconds out of twelve looking like a printed rule, which is the one
        // thing this cue exists to prevent, and the twelfth second looked like
        // something being flicked past.
        //
        // So the sweep is bound to the player's node instead of to the content:
        // a window's width of travel, centred on where they actually are. The
        // panel opens centred there, so the light is on screen from the first
        // frame and stays on it.
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

            float phase = EaseInOut(PingPong(t / BreatheLoop));

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

            float phase = EaseInOut(PingPong(t / BobLoop));

            nextMark.anchoredPosition = new Vector2(
                nextMark.anchoredPosition.x,
                RewardTrackLayout.NextMarkCentreY - BobPixels * phase);
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
            _glide = StartCoroutine(FlyIn());
        }

        private IEnumerator FlyIn()
        {
            float target = RewardTrackLayout.ScrollFor(_level, viewport.rect.width);
            float from = RewardTrackLayout.ClampScroll(0f, viewport.rect.width);

            SetScroll(from);

            float elapsed = -FlyInDelay;
            while (elapsed < FlyInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;

                if (elapsed >= 0f)
                {
                    SetScroll(Mathf.Lerp(from, target,
                        EaseOutCubic(Mathf.Clamp01(elapsed / FlyInSeconds))));
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
            _glide = StartCoroutine(Glide(
                RewardTrackLayout.ScrollFor(level, viewport.rect.width), seconds));
        }

        private IEnumerator Glide(float target, float seconds)
        {
            float from = content.anchoredPosition.x;
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetScroll(Mathf.Lerp(from, target, EaseOutCubic(Mathf.Clamp01(elapsed / seconds))));
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
            if (claimBurstRect == null || claimBurst == null) return;
            if (throughLevel < fromLevel) return;

            if (_bursts != null) StopCoroutine(_bursts);
            _bursts = StartCoroutine(ClaimBursts(fromLevel, throughLevel));
        }

        private IEnumerator ClaimBursts(int fromLevel, int throughLevel)
        {
            claimBurstRect.gameObject.SetActive(true);

            for (int level = fromLevel; level <= throughLevel; level++)
            {
                claimBurstRect.anchoredPosition =
                    new Vector2(RewardTrackLayout.NodeOffsetX(level), 0f);

                // A NESTED COROUTINE PER LEVEL WOULD BE WRONG. The bursts are
                // 130ms apart and each lasts 900, so they overlap five deep --
                // and with one shared node there is only ever one to draw. What
                // the player sees is the last one fired, still travelling,
                // which is what a sweep looks like.
                float elapsed = 0f;
                while (elapsed < BurstStagger)
                {
                    elapsed += Time.unscaledDeltaTime;
                    PaintBurst(elapsed / BurstSeconds);
                    yield return null;
                }
            }

            // And then let the last one finish on its own.
            float tail = BurstStagger;
            while (tail < BurstSeconds)
            {
                tail += Time.unscaledDeltaTime;
                PaintBurst(tail / BurstSeconds);
                yield return null;
            }

            claimBurstRect.gameObject.SetActive(false);
            _bursts = null;
        }

        // Ring scale .3 -> 3.4, opacity up and then out over its own life.
        private void PaintBurst(float t)
        {
            t = Mathf.Clamp01(t);

            claimBurstRect.localScale = Vector3.one * Mathf.Lerp(0.3f, 3.4f, EaseOutCubic(t));

            // In over the first sixth, out over the rest. A symmetric fade
            // makes a burst read as a pulse; this reads as an ignition.
            float alpha = t < 0.16f ? t / 0.16f : 1f - (t - 0.16f) / 0.84f;
            claimBurst.color = WithAlpha(Gold, Mathf.Clamp01(alpha));
        }

        // ---- the card ------------------------------------------------------------

        // 280ms fade and a 7px rise, so the card reads as a new card rather
        // than as the same one with different words in it.
        private void BeginCardSwap(int level)
        {
            if (cardRect == null)
            {
                PaintCard(level);
                return;
            }

            if (_cardSwap != null) StopCoroutine(_cardSwap);
            _cardSwap = StartCoroutine(CardSwap(level));
        }

        private IEnumerator CardSwap(int level)
        {
            PaintCard(level);

            float baseY = RewardTrackLayout.CardCentreY;
            float elapsed = 0f;

            while (elapsed < CardSwapSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / CardSwapSeconds));

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
        private void OnLevelGained(int newLevel)
        {
            if (railFill != null)
            {
                StartCoroutine(AdvanceRail(RewardTrackLayout.NodeX(newLevel)));
            }

            GlideTo(newLevel, FollowSeconds);

            if (claimBurstRect != null)
            {
                if (_bursts != null) StopCoroutine(_bursts);
                _bursts = StartCoroutine(Ignite(newLevel));
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
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / AdvanceSeconds));

                railFill.sizeDelta = new Vector2(Mathf.Lerp(from, target, t), railFill.sizeDelta.y);
                yield return null;
            }

            railFill.sizeDelta = new Vector2(target, railFill.sizeDelta.y);
        }

        private IEnumerator Ignite(int level)
        {
            claimBurstRect.gameObject.SetActive(true);
            claimBurstRect.anchoredPosition = new Vector2(RewardTrackLayout.NodeOffsetX(level), 0f);

            float elapsed = 0f;
            while (elapsed < IgniteSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / IgniteSeconds);

                claimBurstRect.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.6f, EaseOutCubic(t));
                claimBurst.color = WithAlpha(Gold, t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f);

                yield return null;
            }

            claimBurstRect.gameObject.SetActive(false);
            _bursts = null;
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

        private static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInOut(float t) => t * t * (3f - 2f * t);

        // 0 -> 1 -> 0 over one period, for the loops the handoff describes as
        // "alternate".
        private static float PingPong(float t) => 1f - Mathf.Abs(Mathf.Repeat(t, 2f) - 1f);

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
