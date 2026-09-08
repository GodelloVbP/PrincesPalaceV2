using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Presentation
{
    // WHAT A SPRITE LAYER LOOKS LIKE AT ONE INSTANT: which frame, which frame
    // is fading up over it, how far, and how visible the whole thing is.
    public readonly struct SpellFrameSample
    {
        // False before the layer opens and after its fade has run out. The one
        // question a renderer asks before anything else.
        public readonly bool Visible;

        public readonly int Index;

        // The frame dissolving in over Index, or -1 when there is none -- the
        // last frame of a sequence that does not loop, or a still.
        public readonly int Next;

        // 0 while Index is shown on its own, rising to 1 as Next takes over.
        public readonly float Blend;

        // The layer's own fade-out, 1 until its lifetime ends.
        public readonly float Alpha;

        public SpellFrameSample(bool visible, int index, int next, float blend, float alpha)
        {
            Visible = visible;
            Index = index;
            Next = next;
            Blend = blend;
            Alpha = alpha;
        }

        public static SpellFrameSample Hidden => new SpellFrameSample(false, 0, -1, 0f, 0f);
    }

    // WHICH DRAWING IS ON SCREEN AT TIME t, as arithmetic rather than as a
    // coroutine's accumulated position.
    //
    // A COROUTINE CANNOT ANSWER THIS AT AN ARBITRARY t. It answers "what is on
    // screen now", where `now` is however many times it happened to resume --
    // so a long frame, a scene load or a test at 60x gets a different answer
    // for the same clock, and a test asserting what is drawn at the impact
    // instant is asserting how fast the machine is. A pure function of the
    // instant has one answer, and a held clock makes it observable.
    public static class SpellFrameCursor
    {
        // HOW MUCH OF EACH FRAME'S TIME IS SPENT DISSOLVING, and the number
        // matters. A frame that cross-fades from the moment it appears is never
        // itself: at 0.5 through, two drawings are on screen at half strength
        // each and neither is legible, which for a lightning bolt means two
        // bolts. Holding for the first 55% and dissolving over the last 45%
        // keeps every frame readable and removes the step between them.
        public const float DissolveFraction = 0.45f;

        public static SpellFrameSample SampleOf(SpellLayerInstance instance, int frameCount, float seconds)
        {
            if (instance == null || frameCount <= 0) return SpellFrameSample.Hidden;

            var layer = instance.Layer;
            float age = seconds - instance.StartSeconds;
            if (age < 0f) return SpellFrameSample.Hidden;

            float lifetime = instance.EndSeconds - instance.StartSeconds;
            float alpha = AlphaAt(instance, age, lifetime);
            if (alpha <= 0f) return SpellFrameSample.Hidden;

            // A STILL IS ONE FRAME OF A FOLDER, held. Its path names the folder
            // and not the file, which is mechanical rather than tidy: the drift
            // tests match a played path's last segment against recipe filenames
            // and require it to be a directory on disk, so `..._wake/f0` would
            // present `f0` as its provenance and fail both.
            int first = FirstIndex(layer, frameCount);
            if (layer.Render == SpellRender.Still)
            {
                return new SpellFrameSample(true, first, -1, 0f, alpha);
            }

            int playable = frameCount - first;
            if (playable <= 0) return new SpellFrameSample(true, frameCount - 1, -1, 0f, alpha);

            float perFrame = PerFrame(layer, lifetime, playable);
            if (perFrame <= 0f) return new SpellFrameSample(true, first, -1, 0f, alpha);

            float at = age / perFrame;

            // WRAPPED FOR A LOOP, CLAMPED OTHERWISE. `once` and `hold` select
            // the same frame -- the last one -- and differ only in intent,
            // because a layer's LIFETIME is what ends it either way. That is
            // deliberate: making `once` stop drawing at the end of its sequence
            // would cut a travelling sheet short at its arrival, and today such
            // a sheet keeps playing its remaining frames at the target until
            // the sequence runs out.
            int index;
            float within;
            bool loops = layer.Until == SpellEnd.Loop;

            if (loops)
            {
                float wrapped = at % playable;
                index = first + (int)wrapped;
                within = wrapped - (int)wrapped;
            }
            else
            {
                float clamped = at < playable - 1 ? at : playable - 1;
                index = first + (int)clamped;
                within = clamped - (int)clamped;
            }

            if (index >= frameCount) index = frameCount - 1;

            int next = NextIndex(index, first, frameCount, loops);
            float blend = next < 0 ? 0f : BlendAt(within);

            return new SpellFrameSample(true, index, next, blend, alpha);
        }

        // The 1-based startFrame as an index; 0 means the first frame.
        public static int FirstIndex(SpellLayer layer, int frameCount)
        {
            int first = layer.startFrame > 1 ? layer.startFrame - 1 : 0;
            return first >= frameCount ? frameCount - 1 : first;
        }

        // AN AUTHORED RATE, OR THE FOLDER FITTED INTO THE LIFETIME. `fps: 0` is
        // what every pre-layer block becomes, and it reproduces
        // `perFrame = total / frames.Length` exactly -- which matters because a
        // pre-layer block's frame rate IS its duration over its frame count and
        // was never authored.
        private static float PerFrame(SpellLayer layer, float lifetime, int playable)
        {
            if (layer.fps > 0f) return 1f / layer.fps;
            return lifetime > 0f ? lifetime / playable : 0f;
        }

        private static float AlphaAt(SpellLayerInstance instance, float age, float lifetime)
        {
            if (age <= lifetime) return 1f;
            if (instance.FadeSeconds <= 0f) return 0f;

            float into = (age - lifetime) / instance.FadeSeconds;
            return into >= 1f ? 0f : 1f - into;
        }

        private static int NextIndex(int index, int first, int frameCount, bool loops)
        {
            if (index + 1 < frameCount) return index + 1;
            return loops ? first : -1;
        }

        private static float BlendAt(float within)
        {
            float held = 1f - DissolveFraction;
            return within <= held ? 0f : (within - held) / DissolveFraction;
        }
    }
}
