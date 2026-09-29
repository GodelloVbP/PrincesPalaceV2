using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Talents;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // THE SCREEN'S TIME, and nothing else.
    //
    // Split from TalentController for one reason: everything here is a pure
    // function of Time.unscaledTime and needs no state, while everything there
    // is a function of the save and repaints only when the save changes. Mixing
    // them is how a repaint ends up restarting a loop -- the exact bug the
    // reveal component had, where moving the selection replayed twenty-one
    // flourishes because the paint pass touched the animation.
    //
    // The one piece of state on this side is the kindling beat, because a beat
    // has a beginning. Loops do not.
    //
    // UNSCALED, all of it. This screen is reachable from the hub with the game
    // paused behind it, and a sky that stops breathing when the timescale drops
    // reads as the screen having frozen.
    public partial class TalentController
    {
        // THE TEST SEAM FOR THIS SCREEN'S OWN CLOCK. Nothing here reads
        // FightBeatPlayer.BeatSpeedMultiplier -- this screen is not a fight,
        // and its whole clock is already unscaled by Time.timeScale on
        // purpose (see the header above). Scales only `delta` below, so
        // DriveRestingLoops/DriveSky -- which read Time.unscaledTime directly
        // rather than an accumulated elapsed -- are untouched: nothing in the
        // suite times those. 1 outside a test, so play is unchanged.
        public static float MotionSpeedMultiplier = 1f;

        private int _kindlingSlot = -1;
        private int _kindlingPath = -1;
        private float _kindlingElapsed;

        // The push-in, held rather than run as a coroutine for the same reason
        // the slide is: a second selection mid-push retargets this one instead
        // of starting a second that fights it for the same rect.
        private float _pushElapsed;
        private float _pushFrom;
        private float _pushTo;

        private void BeginKindling(int path, int slot)
        {
            _kindlingPath = path;
            _kindlingSlot = slot;
            _kindlingElapsed = 0f;
        }

        // Aims the push at whatever is selected now. Called from the selection
        // rather than from Update, so a repaint that changes nothing about the
        // selection does not restart the lean.
        private void AimPushIn()
        {
            _pushFrom = CurrentPush();
            _pushTo = _selectedSlot < 0
                ? 0f
                : ConstellationLayout.PushInOffset(ConstellationLayout.StarX(Current?.definitionId, _path, _selectedSlot));
            _pushElapsed = 0f;
        }

        private float CurrentPush()
        {
            if (_pushElapsed >= ConstellationLayout.PushInSeconds) return _pushTo;

            float t = ConstellationLayout.Phase(_pushElapsed, ConstellationLayout.PushInSeconds);
            return _pushFrom + (_pushTo - _pushFrom) * ConstellationLayout.Breathe(t * 0.5f);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            float delta = Time.unscaledDeltaTime * MotionSpeedMultiplier;

            DriveSlide(delta);
            DrivePushIn(delta);
            DriveKindling(delta);
            DriveRestingLoops(time);
            DriveSky(time);
        }

        // ---- paging ----------------------------------------------------------

        private void DriveSlide(float delta)
        {
            if (sky == null) return;

            if (!_sliding)
            {
                sky.anchoredPosition = new Vector2(SettledX() + CurrentPush(), sky.anchoredPosition.y);
                return;
            }

            _slideElapsed += delta;
            float progress = ConstellationLayout.SlideProgress(_slideElapsed);

            sky.anchoredPosition = new Vector2(
                ConstellationLayout.SlideOffset(_slideFrom, _path, progress) + CurrentPush(),
                sky.anchoredPosition.y);

            if (progress >= 1f) _sliding = false;
        }

        private float SettledX() => ConstellationLayout.SlideOffset(_path, _path, 1f);

        // ---- the lean --------------------------------------------------------
        //
        // 2% and a seventh of the offset. Small enough that a player never
        // notices the sky moving and large enough that the selected stone stops
        // sharing the frame equally with everything else -- which is the whole
        // job. Applied to SCALE on the sky rather than to a camera, because
        // this is a Screen Space Overlay canvas and there is no camera to push.
        private void DrivePushIn(float delta)
        {
            if (sky == null) return;

            if (_pushElapsed < ConstellationLayout.PushInSeconds)
            {
                _pushElapsed += delta;
            }

            float t = ConstellationLayout.Phase(_pushElapsed, ConstellationLayout.PushInSeconds);
            float eased = ConstellationLayout.Breathe(t * 0.5f);

            float scale = _selectedSlot < 0
                ? Mathf.Lerp(ConstellationLayout.PushInScale, 1f, eased)
                : Mathf.Lerp(1f, ConstellationLayout.PushInScale, eased);

            sky.localScale = new Vector3(scale, scale, 1f);
        }

        // ---- the kindling beat -----------------------------------------------
        //
        // Five parts over 1.12s, overlapping: the crust cracks (0.52s), the
        // stone catches and overshoots (0.90s), the edge below it runs bright
        // (0.62s), the drop-shadow grows out from the centre (0.57s) with the
        // ring's spark answering 120ms behind it (0.48s), and six motes leave
        // on widening gaps (0.70s each, staggered to 0.42s). Overlapping is
        // the point -- played end to end it reads as a machine finishing
        // steps rather than as something igniting. These constants live here
        // rather than in ConstellationLayout because that file is Domain/UiKit,
        // owned by another session while this branch is live -- see the
        // session brief. Kept private to this file for the same reason
        // CatchScale's own peak constant is local to its method.
        private const float GlowKindleStartScale = 0.2f;
        private const float GlowKindleOvershootScale = 1.08f;
        private const float GlowKindleRiseSeconds = 0.35f;
        private const float GlowKindleOvershootSeconds = 0.10f;
        private const float GlowKindleSettleSeconds = 0.12f;

        private const float RingKindleDelaySeconds = 0.12f;
        private const float RingKindleRiseSeconds = 0.14f;
        private const float RingKindleFadeSeconds = 0.22f;

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        // 0.2 to 1.0 ease-out over 350ms -- the expand-from-centre the design
        // asked for -- then a brief overshoot to 1.08 and an ease-out settle
        // back to 1, the same shape CatchScale already gives the stone
        // itself, so the glow reads as catching alight with it rather than
        // as a second, unrelated animation layered underneath.
        private static float GlowKindleScale(float elapsed)
        {
            if (elapsed <= 0f) return GlowKindleStartScale;

            if (elapsed < GlowKindleRiseSeconds)
            {
                return Mathf.Lerp(GlowKindleStartScale, 1f, EaseOut(elapsed / GlowKindleRiseSeconds));
            }

            float afterRise = elapsed - GlowKindleRiseSeconds;
            if (afterRise < GlowKindleOvershootSeconds)
            {
                return Mathf.Lerp(1f, GlowKindleOvershootScale, afterRise / GlowKindleOvershootSeconds);
            }

            float afterOvershoot = afterRise - GlowKindleOvershootSeconds;
            if (afterOvershoot < GlowKindleSettleSeconds)
            {
                return Mathf.Lerp(
                    GlowKindleOvershootScale, 1f, EaseOut(afterOvershoot / GlowKindleSettleSeconds));
            }

            return 1f;
        }

        // A spark, not a rise-and-hold: a taken stone carries no invitation
        // ring at rest (PaintOrbs paints RingOff for AlreadyTaken), so this
        // climbs to RingReady's own alpha 120ms after the glow starts and
        // fades straight back to nothing, rather than settling on a state
        // PaintOrbs would then have to fight every subsequent repaint.
        private static float RingKindleAlpha(float elapsed)
        {
            float sinceDelay = elapsed - RingKindleDelaySeconds;
            if (sinceDelay <= 0f) return 0f;

            if (sinceDelay < RingKindleRiseSeconds)
            {
                return RingReady.a * (sinceDelay / RingKindleRiseSeconds);
            }

            float afterRise = sinceDelay - RingKindleRiseSeconds;
            if (afterRise < RingKindleFadeSeconds)
            {
                return RingReady.a * (1f - EaseOut(afterRise / RingKindleFadeSeconds));
            }

            return 0f;
        }

        private void DriveKindling(float delta)
        {
            if (_kindlingSlot < 0) return;

            _kindlingElapsed += delta;

            int index = TalentScreen.OrbIndex(_kindlingPath, _kindlingSlot);

            // The catch, on the stone itself.
            float catchPhase = ConstellationLayout.Phase(
                _kindlingElapsed, ConstellationLayout.KindleCatchSeconds);
            float scale = ConstellationLayout.CatchScale(catchPhase);

            if (index >= 0 && index < orbs.Length && orbs[index] != null)
            {
                var rect = orbs[index].transform as RectTransform;
                if (rect != null) rect.localScale = new Vector3(scale, scale, 1f);
            }

            // The crust cracking: the core comes up from nothing under the
            // shell, which is what makes the stone read as breaking open rather
            // than as being swapped.
            float crust = ConstellationLayout.Phase(
                _kindlingElapsed, ConstellationLayout.KindleCrustSeconds);

            if (Has(orbCores, index))
            {
                var c = orbCores[index].color;
                orbCores[index].color = new Color(c.r, c.g, c.b, crust);
            }

            // The aura arriving with the edge run below it, so the light and
            // the connection it travelled along land together.
            float edge = ConstellationLayout.Phase(
                _kindlingElapsed, ConstellationLayout.KindleEdgeSeconds);

            if (Has(orbAuras, index))
            {
                var a = orbAuras[index].color;
                orbAuras[index].color = new Color(a.r, a.g, a.b, a.a * edge);
            }

            // The drop-shadow growing from the centre out, so the stone reads
            // as coming alive rather than as switching on. PaintOrbs already
            // wrote the settled GlowTaken colour this same frame (see Kindle
            // -- the beat has to own the stone before the repaint lands); this
            // starts the scale small and the alpha at zero and animates both
            // up to exactly that colour, the same "paint the end state, then
            // let the beat override it for its own duration" shape the crust
            // and the aura already use above.
            if (Has(orbGlows, index))
            {
                float glowScale = GlowKindleScale(_kindlingElapsed);

                var rect = orbGlows[index].rectTransform;
                if (rect != null) rect.localScale = new Vector3(glowScale, glowScale, 1f);

                float glowRise = ConstellationLayout.Phase(_kindlingElapsed, GlowKindleRiseSeconds);
                orbGlows[index].color = new Color(GlowTaken.r, GlowTaken.g, GlowTaken.b, GlowTaken.a * glowRise);
            }

            // The ring answers the glow rather than leading it -- a fixed
            // delay so the eye lands on the stone first. It is not the
            // stone's steady state (a taken stone carries no invitation ring,
            // see PaintOrbs), so this is a flash that decays back to nothing
            // rather than a rise that holds: the spark of the stone catching,
            // travelling out along the ring that fed it, then fading once the
            // stone can stand on its own light.
            if (Has(orbRings, index))
            {
                float ringAlpha = RingKindleAlpha(_kindlingElapsed);
                orbRings[index].color = new Color(RingReady.r, RingReady.g, RingReady.b, ringAlpha);
            }

            if (_kindlingElapsed >= ConstellationLayout.KindleSeconds)
            {
                // Handed back to the resting loops. The scale is reset
                // explicitly rather than left at whatever the last frame
                // produced: CatchScale returns exactly 1 at its end, but a beat
                // interrupted by a respec never reaches its end.
                if (index >= 0 && index < orbs.Length && orbs[index] != null)
                {
                    var rect = orbs[index].transform as RectTransform;
                    if (rect != null) rect.localScale = Vector3.one;
                }

                if (Has(orbGlows, index))
                {
                    orbGlows[index].rectTransform.localScale = Vector3.one;
                    orbGlows[index].color = GlowTaken;
                }

                if (Has(orbRings, index)) orbRings[index].color = RingOff;

                _kindlingSlot = -1;
                _kindlingPath = -1;
            }
        }

        // ---- the resting loops -----------------------------------------------
        //
        // Every lit stone breathes, and no two breathe together: the phase
        // walks 491ms per slot around a 3200ms cycle, which never lands twice
        // in 21 slots. A tree that pulsed in unison would blink.
        //
        // Driven off alpha only. Nothing here moves a rect -- the layout is
        // audited at build time and an animation that walked a stone out of its
        // cell would pass every check and still be wrong.
        private void DriveRestingLoops(float time)
        {
            if (orbCores == null) return;

            for (int i = 0; i < orbCores.Length; i++)
            {
                // The stone mid-beat belongs to the beat, not to the loop.
                if (i == TalentScreen.OrbIndex(_kindlingPath, _kindlingSlot)) continue;

                int slot = TalentScreen.SlotOf(i);
                float phase = ConstellationLayout.RestingPhase(slot);

                // THE HOT HEART GUTTERS. Scale and alpha together, because a
                // flame that only changes brightness reads as a lamp on a
                // dimmer -- what says "burning" is the hot part growing and
                // shrinking against a body that stays put.
                if (Has(orbCores, i) && orbCores[i].color.a > 0f)
                {
                    float flick = ConstellationLayout.EmberFlicker(time, phase);

                    var c = orbCores[i].color;
                    orbCores[i].color = new Color(c.r, c.g, c.b, Mathf.Lerp(
                        ConstellationLayout.EmberCoreMinAlpha,
                        ConstellationLayout.EmberCoreMaxAlpha, flick));

                    float s = Mathf.Lerp(
                        ConstellationLayout.EmberCoreMinScale,
                        ConstellationLayout.EmberCoreMaxScale, flick);

                    orbCores[i].rectTransform.localScale = new Vector3(s, s, 1f);
                }

                // THE HALO ANSWERS THE HEART, at about half its amplitude and
                // over a slower crackle underneath. Two rates rather than one:
                // a halo locked to the core is the same event drawn twice, and
                // a halo on its own clock reads as a separate light source
                // sitting behind the stone.
                if (Has(orbAuras, i) && orbAuras[i].color.a > 0f)
                {
                    float crackle = ConstellationLayout.Breathe(
                        ConstellationLayout.Cycle(time, ConstellationLayout.HaloCrackleSeconds, phase));
                    float flick = ConstellationLayout.EmberFlicker(time, phase);

                    var a = orbAuras[i].color;
                    orbAuras[i].color = new Color(a.r, a.g, a.b,
                        Mathf.Lerp(0.22f, 0.40f, crackle * 0.6f + flick * 0.4f));
                }

                // The warm ground the stone sits on, moving least of the three.
                // It is what carries the ember's light onto the sky around it,
                // so a visible pulse here would look like the sky flashing.
                if (Has(orbGlows, i) && orbGlows[i].color.a > 0f)
                {
                    float flick = ConstellationLayout.EmberFlicker(time, phase);

                    var g = orbGlows[i].color;
                    orbGlows[i].color = new Color(g.r, g.g, g.b, Mathf.Lerp(0.34f, 0.48f, flick));
                }

                // THE READY PULSE IS THE ONLY INVITATION ON THE SCREEN, which
                // is why it is the fastest loop here: at 2.4s against the
                // stones' 3.2 and 4.6 it is the thing the eye lands on.
                if (Has(orbRings, i) && orbRings[i].color.a > 0f)
                {
                    float pulse = ConstellationLayout.Breathe(
                        ConstellationLayout.Cycle(time, ConstellationLayout.ReadyPulseSeconds, phase));

                    // 70% TO FULL, NOT 45%. The ring is an invitation and it
                    // has to stay legible at the bottom of its swing; taking it
                    // to under half made a reachable stone look like it was
                    // going out, which is the opposite of the message.
                    var r = orbRings[i].color;
                    float peak = r.a > 0.5f ? 1f : 0.4f;
                    orbRings[i].color = new Color(r.r, r.g, r.b, Mathf.Lerp(peak * 0.7f, peak, pulse));
                }
            }
        }

        // ---- the backdrop ----------------------------------------------------
        //
        // Two pan tracks and three drifts, all on periods that share no useful
        // multiple. The sky is never the same frame twice, and that is the only
        // thing separating it from a painting.
        private void DriveSky(float time)
        {
            DriveStarFields(time);
            DriveClouds(time);
            DriveDust(time);
            DriveStreaks(time);
        }

        private void DriveStarFields(float time)
        {
            if (starFields == null) return;

            for (int i = 0; i < starFields.Length; i++)
            {
                if (starFields[i] == null) continue;

                // The near pair drift further and slower than the far pair, in
                // the opposite direction. That inequality IS the parallax.
                bool near = i >= starFields.Length / 2;
                var travel = near ? ConstellationLayout.StarPanNear : ConstellationLayout.StarPanFar;
                float period = near
                    ? ConstellationLayout.StarPanNearSeconds
                    : ConstellationLayout.StarPanFarSeconds;

                float cycle = ConstellationLayout.Cycle(time, period, i * 7f);
                float swing = ConstellationLayout.Breathe(cycle) * 2f - 1f;

                starFields[i].anchoredPosition = new Vector2(travel.X * swing, travel.Y * swing);
            }
        }

        private void DriveClouds(float time)
        {
            if (cloudWashes == null) return;

            for (int i = 0; i < cloudWashes.Length; i++)
            {
                if (cloudWashes[i] == null) continue;

                float drift = ConstellationLayout.Cycle(time, ConstellationLayout.CloudDrift(i), i * 13f);
                float swing = ConstellationLayout.Breathe(drift) * 2f - 1f;

                cloudWashes[i].anchoredPosition = new Vector2(swing * 120f, swing * -46f);

                // The third wash fades in and out entirely rather than drifting
                // with the other two, which is what stops the backdrop settling
                // into a fixed painting behind the tree.
                if (!Has(cloudImages, i)) continue;

                float breathe = ConstellationLayout.Breathe(
                    ConstellationLayout.Cycle(time, ConstellationLayout.CloudBreatheSeconds, i * 9f));

                var c = cloudImages[i].color;
                cloudImages[i].color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.35f, 1f, breathe) * BaseCloudAlpha(i));
            }
        }

        // The tint the screen built the wash with, remembered on the first
        // frame. Reading it back off the Image every frame would compound the
        // fade into itself and walk the layer to zero.
        private float[] _cloudAlphas;

        private float BaseCloudAlpha(int i)
        {
            if (_cloudAlphas == null)
            {
                _cloudAlphas = new float[cloudImages.Length];
                for (int k = 0; k < cloudImages.Length; k++)
                {
                    _cloudAlphas[k] = cloudImages[k] != null ? cloudImages[k].color.a : 0f;
                }
            }

            return i >= 0 && i < _cloudAlphas.Length ? _cloudAlphas[i] : 0f;
        }

        // Seven motes, rising and fading. All of them start MID-FLIGHT, which
        // is what the stagger is for: the first frame of the screen should not
        // be seven embers leaving the ground in formation.
        private void DriveDust(float time)
        {
            if (dustMotes == null) return;

            for (int i = 0; i < dustMotes.Length; i++)
            {
                if (dustMotes[i] == null) continue;

                float cycle = ConstellationLayout.Cycle(
                    time, ConstellationLayout.DustRiseSeconds, i * 1.37f);

                var at = dustMotes[i].anchoredPosition;
                dustMotes[i].anchoredPosition =
                    new Vector2(at.x, -400f + cycle * ConstellationLayout.DustRise);

                if (!Has(dustImages, i)) continue;

                var c = dustImages[i].color;
                dustImages[i].color = new Color(c.r, c.g, c.b, ConstellationLayout.Breathe(cycle) * 0.8f);
            }
        }

        // Two streaks, idle for almost all of their cycle. This is the one
        // EVENT in the backdrop and it stays rare to read as one: 38s and 57s
        // apart with 23s between them, so they never arrive together.
        private void DriveStreaks(float time)
        {
            if (shootingStars == null) return;

            for (int i = 0; i < shootingStars.Length; i++)
            {
                if (shootingStars[i] == null) continue;

                float period = ConstellationLayout.StreakPeriod(i);
                float offset = i * ConstellationLayout.StreakOffsetSeconds;
                float cycle = ConstellationLayout.Cycle(time, period, offset);

                // Everything past the travel window is the idle stretch, and
                // most of the cycle is idle stretch.
                float travelled = cycle * period / ConstellationLayout.StreakTravelSeconds;
                bool flying = travelled <= 1f;

                if (Has(shootingStarImages, i))
                {
                    var c = shootingStarImages[i].color;
                    shootingStarImages[i].color = new Color(
                        c.r, c.g, c.b, flying ? ConstellationLayout.Breathe(travelled) : 0f);
                }

                if (!flying) continue;

                var at = shootingStars[i].anchoredPosition;
                shootingStars[i].anchoredPosition = new Vector2(
                    -ConstellationLayout.StreakTravel * 0.5f + travelled * ConstellationLayout.StreakTravel,
                    at.y);
            }
        }
    }
}
