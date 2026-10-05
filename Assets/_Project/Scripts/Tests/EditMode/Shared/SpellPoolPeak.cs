using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // WHAT A CAST HOLDS AT ONCE, which is what the pools actually have to cover.
    // Particles go back to the pool as they die and a renderer goes back when its
    // layer clears, so the number that overflows a pool is the most that are alive
    // at one instant, not everything a cast ever spawns.
    //
    // Peaks are read at the instants something is born or opens, the only moments a
    // count can rise. A particle is alive through its death time (the player frees
    // it on the first frame past it), a layer through its cleared time.
    public static class SpellPoolPeak
    {
        // The most particles alive together, with each emitter's own authored seed
        // (an emitter with none is sampled on seed 1: a bound, not a replay).
        public static int ParticlesOf(SpellPerformance performance)
        {
            var spans = new List<(float birth, float death)>();
            foreach (var instance in performance.Instances.Where(i => i.Layer.Render == SpellRender.Emitter))
            {
                var spec = instance.Layer.emitter;
                int seed = spec.seed != 0 ? spec.seed : 1;
                int total = SpellEmitterSim.CountOf(spec);
                for (int i = 0; i < total; i++)
                {
                    float birth = SpellEmitterSim.BirthOf(spec, i, instance.StartSeconds);
                    spans.Add((birth, birth + SpellEmitterSim.LifeOf(spec, seed, i)));
                }
            }

            return Peak(spans);
        }

        // The most effects-band renderers open together: each sprite or still layer,
        // and one more per afterimage it authors.
        public static int EffectsRenderersOf(SpellPerformance performance)
        {
            var spans = new List<(float birth, float death)>();
            foreach (var instance in performance.Instances.Where(i =>
                         i.Layer.Render != SpellRender.Emitter && i.Layer.Sort != SpellSort.Ground))
            {
                int copies = 1 + (instance.Layer.echo != null && instance.Layer.echo.IsAuthored ? instance.Layer.echo.count : 0);
                for (int c = 0; c < copies; c++) spans.Add((instance.StartSeconds, instance.ClearedSeconds));
            }

            return Peak(spans);
        }

        private static int Peak(List<(float birth, float death)> spans)
        {
            int peak = 0;
            foreach (var (birth, _) in spans)
            {
                int alive = spans.Count(s => s.birth <= birth && birth <= s.death);
                if (alive > peak) peak = alive;
            }

            return peak;
        }
    }
}
