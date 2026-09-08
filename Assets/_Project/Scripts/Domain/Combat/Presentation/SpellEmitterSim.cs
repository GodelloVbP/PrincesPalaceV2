using System;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Combat.Presentation
{
    // ONE PARTICLE AT ONE INSTANT.
    public readonly struct SpellParticle
    {
        public readonly bool Alive;
        public readonly UiVec Position;
        public readonly float Scale;
        public readonly float Alpha;
        public readonly float Rotation;

        // Which still of the emitter's folder this drop draws, chosen by hash
        // rather than by a field: a folder with one sprite in it selects that
        // one and a folder with eight selects evenly, which is the brief's
        // "sprite selection" with nothing to author.
        public readonly int Frame;

        public SpellParticle(bool alive, UiVec position, float scale, float alpha, float rotation, int frame)
        {
            Alive = alive;
            Position = position;
            Scale = scale;
            Alpha = alpha;
            Rotation = rotation;
            Frame = frame;
        }

        public static SpellParticle Dead => new SpellParticle(false, UiVec.Zero, 0f, 0f, 0f, 0);
    }

    // BALLISTIC DROPS, COMPUTED FROM AGE AND NEVER INTEGRATED.
    //
    // The closed form is the load-bearing decision, and three things the brief
    // asks for fall out of it rather than being implemented:
    //
    //   A LARGE CLOCK STEP IS CORRECT BY CONSTRUCTION. Nothing accumulates, so
    //   sampling at t gives the same answer whether one tick or fifty got
    //   there. An integrator would give a different field for a 200ms frame
    //   than for fifty 4ms ones, and the difference would only ever show up on
    //   a slow machine.
    //
    //   SEEDED PREVIEWS AND TESTS REPEAT EXACTLY. Direction and speed are
    //   hashed from (seed, index) rather than drawn from a shared RNG stream,
    //   so a particle's history does not depend on how many other particles
    //   existed -- which is what makes a picture reproducible after an
    //   unrelated spell was added to the same fight.
    //
    //   THE SIM NEVER REACHES FOR ITS SOURCE. The birth position and velocity
    //   are ARGUMENTS, sampled once at the particle's own birth time by whoever
    //   owns the source's path. So this is a function of its inputs and the
    //   back-dating that makes a shed look like a shed lives in one place.
    //
    // ENGINE-FREE: UiVec, not Vector2, and System.Math for the exponential.
    // The assembly declares noEngineReferences, so a reflex `using UnityEngine`
    // here does not compile.
    public static class SpellEmitterSim
    {
        // WHEN PARTICLE `index` IS BORN, as a time rather than as an event.
        //
        // A tick births every particle whose birth time lands inside its
        // window, so a tick spanning six spawn intervals births six particles
        // AT SIX DIFFERENT TIMES and a tick spanning none births none. That is
        // the difference between a shed laid along the path the projectile took
        // and six drops stacked where the step happened to end.
        //
        // A burst is every particle born at the window's opening instant, which
        // is the same rule with an infinite rate rather than a second mechanism.
        public static float BirthOf(SpellEmitter spec, int index, float windowStart)
        {
            if (spec == null) return windowStart;
            if (index < spec.burst) return windowStart;
            if (spec.rate <= 0f) return windowStart;

            return windowStart + (index - spec.burst) / spec.rate;
        }

        // How many particles this emitter will ever throw. Finite by
        // construction: a burst plus a rate over a window, which is why an
        // emitter is the one layer kind the unbounded-lifetime rule does not
        // have to police.
        public static int CountOf(SpellEmitter spec)
        {
            if (spec == null) return 0;

            int streamed = spec.rate > 0f && spec.window > 0f
                ? (int)Math.Ceiling(spec.rate * spec.window)
                : 0;

            return spec.burst + streamed;
        }

        // THE DIRECTION AND SPEED A PARTICLE LEAVES WITH, before whatever it
        // inherits from its source. Hashed from (seed, index), so the caller
        // can add the inherited half and hand the total back as `v0`.
        public static UiVec LaunchOf(SpellEmitter spec, int seed, int index, float facing)
        {
            if (spec == null) return UiVec.Zero;

            float pick = Hashed(seed, index, 1);
            float degrees = spec.aimDegrees + (pick - 0.5f) * spec.spreadDegrees;

            // MIRRORED WITH THE CAST, so a monster's spray leaves the way its
            // blow arrived. The whole ANGLE is reflected about the vertical --
            // aim and the drop's own offset within the cone together -- because
            // reflecting only the cone's centre would leave every offset
            // rotating the same way and turn the mirrored fan inside out.
            if (facing < 0f) degrees = 180f - degrees;

            float speed = Between(spec.speedMin, spec.speedMax, Hashed(seed, index, 2));

            double radians = degrees * Math.PI / 180.0;
            return new UiVec((float)Math.Cos(radians) * speed, (float)Math.Sin(radians) * speed);
        }

        public static float LifeOf(SpellEmitter spec, int seed, int index) =>
            spec == null ? 0f : Between(spec.lifeMin, spec.lifeMax, Hashed(seed, index, 3));

        // p(t) = p0 + (v0 - a/k)/k * (1 - e^(-k t)) + (a/k) t     for drag k > 0
        // p(t) = p0 + v0 t + a t^2 / 2                            for k = 0
        //
        // `age` is the particle's own, so a drop discovered on the tick after
        // its birth is already as old as the back-dating says -- which is what
        // makes the shed survive the first frame as well as the birth instant.
        public static SpellParticle At(SpellEmitter spec, int seed, int index,
            UiVec p0, UiVec v0, float age, int frameCount)
        {
            if (spec == null || age < 0f) return SpellParticle.Dead;

            float life = LifeOf(spec, seed, index);
            if (life <= 0f || age > life) return SpellParticle.Dead;

            var acceleration = new UiVec(0f, spec.gravity);
            UiVec position;

            if (spec.drag > 0f)
            {
                float k = spec.drag;
                var terminal = acceleration * (1f / k);
                float decayed = 1f - (float)Math.Exp(-k * age);
                position = p0 + (v0 - terminal) * (decayed / k) + terminal * age;
            }
            else
            {
                position = p0 + v0 * age + acceleration * (0.5f * age * age);
            }

            float through = life <= 0f ? 1f : age / life;
            float size = Between(spec.sizeMin, spec.sizeMax, Hashed(seed, index, 4));
            float scale = size * Lerp(1f, spec.endScale, through);
            float alpha = AlphaThrough(spec.fadeFrom, through);
            float spin = Between(spec.spinMin, spec.spinMax, Hashed(seed, index, 5));
            int frame = FrameOf(spec.weights, seed, index, frameCount);

            return new SpellParticle(true, position, scale, alpha, spin * age, frame);
        }

        // WHICH STILL OF THE FOLDER THIS PARTICLE DRAWS, over the SAME
        // hash(seed, index, 6) an unweighted emitter always used -- not a
        // second random draw, so a preview or a test stays repeatable
        // whichever branch below runs, and turning `weights` off for an
        // emitter that shipped without it changes nothing about the picture
        // (AUDIT #108's whole compatibility promise).
        //
        // ABSENT OR EMPTY IS THE OLD MODULUS RULE, uniform across the folder
        // -- unchanged arithmetic, not merely an equivalent one, because
        // "equivalent" would still have re-seeded every existing preview's
        // stills the moment weights shipped.
        //
        // A weighted PICK is a cumulative sum walked against one hash sample
        // scaled to the weights' own total, which is the standard weighted
        // sampling construction and needs no second field: `total` folds the
        // normalisation in rather than requiring an author to make their
        // weights sum to 1. SpellLayerRules.CheckWeights has already refused
        // a negative or all-zero array by the time this runs in a real fight,
        // so `total <= 0f` here is a defensive fallback to uniform rather
        // than a case Content is expected to reach.
        private static int FrameOf(float[] weights, int seed, int index, int frameCount)
        {
            if (weights == null || weights.Length == 0 || frameCount <= 0)
            {
                return frameCount <= 0 ? 0 : (int)(Hashed(seed, index, 6) * frameCount) % frameCount;
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];

            if (total <= 0f)
            {
                return (int)(Hashed(seed, index, 6) * frameCount) % frameCount;
            }

            float r = Hashed(seed, index, 6) * total;
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (r < cumulative) return i;
            }

            // Float rounding can leave `r` a hair past the last partial sum
            // even though Hashed() is strictly < 1 -- the last weighted cell
            // is the correct answer, not cell 0, which would silently favour
            // the first index every time that happens.
            return weights.Length - 1;
        }

        // Full strength until `fadeFrom` of the way through life, then down to
        // nothing at the end. 1 means "no fade at all", which is why it is the
        // field's default.
        public static float AlphaThrough(float fadeFrom, float through)
        {
            if (through >= 1f) return 0f;
            if (fadeFrom >= 1f || through <= fadeFrom) return 1f;

            float remaining = 1f - fadeFrom;
            return remaining <= 0f ? 0f : 1f - (through - fadeFrom) / remaining;
        }

        private static float Between(float low, float high, float fraction) =>
            high <= low ? low : low + (high - low) * fraction;

        private static float Lerp(float from, float to, float fraction) => from + (to - from) * fraction;

        // A 0..1 value from (seed, index, channel), deterministic and with no
        // shared stream behind it. The channels are what keep a particle's
        // speed from being a function of its direction -- one hash reused would
        // make every fast drop leave at the same angle.
        private static float Hashed(int seed, int index, int channel)
        {
            unchecked
            {
                uint h = (uint)(seed * 374761393 + index * 668265263 + channel * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
