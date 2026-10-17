using System;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Rewards
{
    // HOW EACH TREATMENT LOOKS, as pure curves of time (docs/PLAN_REWARD_SCREENS.md
    // C4). RevealSchedule says WHEN a card builds and lands; this says what the
    // build and the landing do to the card, its under-glow and the panel. The
    // controller samples these at the reveal's clock, so a skip or a dropped
    // frame never leaves an effect half-played.
    //
    // Times here are AUTHORED seconds: the controller divides wall time by the
    // preset's ReckoningTime scale before sampling, so a faster preset plays
    // the same shapes faster.
    //
    // The particles are SpellEmitter specs played by SpellEmitterSim, the fight's
    // own closed-form emitter, so a reveal particle is a function of its age the
    // same way a cast's is.
    public static class RevealFeel
    {
        // ---- the under-glow --------------------------------------------------------

        // Peak alpha the glow builds to over the anticipation (0: no build).
        public static float BuildGlow(LandStyle style)
        {
            switch (style)
            {
                case LandStyle.Puff: return 0.35f;
                case LandStyle.Shimmer: return 0.6f;
                case LandStyle.Burst: return 0.95f;
                default: return 0f;
            }
        }

        // How far past the card's own size the glow spreads by the end of the
        // build, as extra scale: the ceremony's glow spills past the edges.
        public static float Spill(LandStyle style) =>
            style == LandStyle.Burst ? 0.4f
            : style == LandStyle.Shimmer ? 0.25f
            : style == LandStyle.Puff ? 0.18f : 0.12f;

        // The flash of the glow on landing, decaying to its rest level.
        public static float LandGlow(LandStyle style)
        {
            switch (style)
            {
                case LandStyle.Puff: return 0.75f;
                case LandStyle.Shimmer: return 0.95f;
                case LandStyle.Burst: return 1f;
                default: return 0.55f;
            }
        }

        // The glow left under an open card at rest, by its quality tier
        // (1..4), so the row reads by rarity once it has landed. By tier, not
        // treatment: a top tier that landed as a Rare still rests as a top tier.
        public static float RestGlow(int tier)
        {
            switch (tier)
            {
                case 2: return 0.16f;
                case 3: return 0.22f;
                case 4: return 0.28f;
                default: return 0.1f;
            }
        }

        public static float GlowSettleSeconds(LandStyle style) => style == LandStyle.Burst ? 0.8f : 0.5f;

        // Glow alpha and scale at `build` (0..1 through the anticipation, 1
        // through the flip) or `afterLand` seconds past the landing (< 0:
        // not landed yet), settling to `rest`.
        public static void Glow(LandStyle style, float build, float afterLand, float rest, out float alpha,
                                out float scale)
        {
            if (afterLand < 0f)
            {
                float k = Smooth(build);
                alpha = BuildGlow(style) * k;
                scale = 1f + Spill(style) * k;
                return;
            }

            float u = Clamp01(afterLand / GlowSettleSeconds(style));
            float from = Math.Max(LandGlow(style), BuildGlow(style));
            float ease = 1f - (1f - u) * (1f - u);
            alpha = from + (rest - from) * ease;
            scale = 1f + Spill(style) * (1f - ease);
        }

        // ---- the card's own motion -------------------------------------------------

        // Pixels the card rises through its build; it drops back as it lands.
        public static float LiftPixels(LandStyle style) =>
            style == LandStyle.Shimmer || style == LandStyle.Burst ? 8f : 0f;

        public const float DropSeconds = 0.1f;

        public static float Lift(LandStyle style, float build, float afterLand)
        {
            if (afterLand < 0f) return LiftPixels(style) * Smooth(build);
            return LiftPixels(style) * (1f - Clamp01(afterLand / DropSeconds));
        }

        // The ceremony's tremble: a jitter that rises to TremblePixels by the
        // end of the build. Noise, not a sine (StageShake's header says why),
        // hashed from the time so a frame drawn twice draws the same offset.
        public const float TremblePixels = 3f;
        private const float TrembleRate = 40f;

        public static void Tremble(LandStyle style, float build, float authoredTime, out float dx, out float dy)
        {
            dx = dy = 0f;
            if (style != LandStyle.Burst || build <= 0f || build >= 1f) return;

            float amplitude = TremblePixels * (0.25f + 0.75f * build * build);
            int step = (int)Math.Floor(authoredTime * TrembleRate);
            dx = (Hash(step, 1) * 2f - 1f) * amplitude;
            dy = (Hash(step, 2) * 2f - 1f) * amplitude * 0.6f;
        }

        // Scale the card lands at, settling to 1: every landing punches, harder
        // by quality.
        public static float PunchFrom(LandStyle style)
        {
            switch (style)
            {
                case LandStyle.Puff: return 1.11f;
                case LandStyle.Shimmer: return 1.13f;
                case LandStyle.Burst: return 1.15f;
                default: return 1.08f;
            }
        }

        public static float PunchSeconds(LandStyle style) =>
            style == LandStyle.Burst ? 0.25f : style == LandStyle.Flat ? 0.12f : 0.18f;

        public static float Punch(LandStyle style, float afterLand)
        {
            if (afterLand < 0f) return 1f;
            float u = Clamp01(afterLand / PunchSeconds(style));
            return 1f + (PunchFrom(style) - 1f) * (1f - u) * (1f - u);
        }

        // ---- the landing's light ----------------------------------------------------

        // A ring of light bursting outward: a Rare's small, the ceremony's
        // full reach (UiBurst's ring curve, scaled by this).
        public static float RingReach(LandStyle style) =>
            style == LandStyle.Burst ? 1f : style == LandStyle.Shimmer ? 0.85f : 0f;

        public static float RingSeconds(LandStyle style) => style == LandStyle.Burst ? 0.8f : 0.45f;

        // Rays and the white-hot core behind the card: the ceremony only.
        public const float RaysSeconds = 0.8f;

        // The card's own edge (its halo) flares and fades: a Rare's pulse.
        public const float EdgePulseSeconds = 0.2f;
        public const float EdgePulseAlpha = 0.85f;

        // ---- the shake ------------------------------------------------------------

        // Peak pixels of the reward panel's kick on landing, and of the fight
        // stage's behind it. UI and stage transforms only (StageShake), never
        // Time.timeScale.
        public static float PanelShakePixels(LandStyle style) =>
            style == LandStyle.Burst ? 8f
            : style == LandStyle.Shimmer ? 3.5f
            : style == LandStyle.Puff ? 2.5f : 1.5f;

        public static float StageShakePixels(LandStyle style) => style == LandStyle.Burst ? 8f : 0f;

        public static float ShakeSeconds(LandStyle style) => style == LandStyle.Burst ? 0.25f : 0.15f;

        // ---- particles --------------------------------------------------------------

        // What a landing throws, one model with a per-style intensity: Common
        // a few motes off the bottom edge, Uncommon a puff, Rare a shimmer,
        // the ceremony an explosion. Card size
        // in canvas pixels; the spec's source is the card's centre.
        public static SpellEmitter LandEmitter(LandStyle style, float cardWidth, float cardHeight)
        {
            switch (style)
            {
                case LandStyle.Puff:
                    return new SpellEmitter
                    {
                        burst = 14, aimDegrees = 90f, spreadDegrees = 360f, speedMin = 70f, speedMax = 160f,
                        drag = 3f, gravity = 50f, lifeMin = 0.45f, lifeMax = 0.7f, sizeMin = 0.5f, sizeMax = 0.9f,
                        fadeFrom = 0.4f, lateral = cardWidth * 0.6f, seed = 2,
                    };
                case LandStyle.Shimmer:
                    return new SpellEmitter
                    {
                        burst = 26, aimDegrees = 90f, spreadDegrees = 360f, speedMin = 160f, speedMax = 340f,
                        drag = 4f, lifeMin = 0.35f, lifeMax = 0.65f, sizeMin = 0.4f, sizeMax = 0.8f,
                        fadeFrom = 0.3f, endScale = 0.4f, seed = 3,
                    };
                case LandStyle.Burst:
                    return new SpellEmitter
                    {
                        burst = 30, aimDegrees = 90f, spreadDegrees = 360f, speedMin = 280f, speedMax = 620f,
                        drag = 3.2f, gravity = -260f, lifeMin = 0.5f, lifeMax = 0.9f, sizeMin = 0.6f, sizeMax = 1.2f,
                        fadeFrom = 0.4f, endScale = 0.3f, seed = 4,
                    };
                default:
                    return new SpellEmitter
                    {
                        burst = 9, aimDegrees = 90f, spreadDegrees = 200f, speedMin = 50f, speedMax = 110f,
                        drag = 5f, gravity = -40f, lifeMin = 0.3f, lifeMax = 0.45f, sizeMin = 0.5f, sizeMax = 0.8f,
                        fadeFrom = 0.2f, endScale = 1.3f, lateral = cardWidth * 0.8f,
                        sourceDy = -cardHeight * 0.45f, seed = 1,
                    };
            }
        }

        // The ceremony's embers, drifting up off the card through its build;
        // null for every other treatment.
        public static SpellEmitter BuildEmitter(LandStyle style, float cardWidth, float cardHeight)
        {
            if (style != LandStyle.Burst) return null;
            return new SpellEmitter
            {
                rate = 22f, window = RevealSchedule.AnticipationSeconds(LandStyle.Burst), aimDegrees = 90f,
                spreadDegrees = 40f, speedMin = 40f, speedMax = 100f, drag = 0.8f, gravity = 60f,
                lifeMin = 0.7f, lifeMax = 1f, sizeMin = 0.4f, sizeMax = 0.7f, fadeFrom = 0.5f,
                lateral = cardWidth * 0.8f, sourceDy = -cardHeight * 0.3f, seed = 5,
            };
        }

        // Authored seconds past a landing until everything it started is
        // gone: the glow at rest, the ring and rays out, the last particle dead.
        public static float TailSeconds(LandStyle style)
        {
            float tail = Math.Max(GlowSettleSeconds(style), PunchSeconds(style));
            if (RingReach(style) > 0f) tail = Math.Max(tail, RingSeconds(style));
            if (style == LandStyle.Burst) tail = Math.Max(tail, RaysSeconds);
            return Math.Max(tail, LandEmitter(style, 0f, 0f).lifeMax);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int step, int channel)
        {
            unchecked
            {
                uint h = (uint)(step * 374761393 + channel * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
