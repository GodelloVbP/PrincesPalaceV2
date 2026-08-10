using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Rng
{
    // Deterministic PRNG (SplitMix64) so runs, tests, and simulations are
    // reproducible from a seed. Do not use UnityEngine.Random or System.Random
    // anywhere gameplay-relevant depends on this — mixing generators breaks
    // reproducibility silently.
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong State => _state;

        public static SeededRandom FromState(ulong state)
        {
            var rng = new SeededRandom(0);
            rng._state = state;
            return rng;
        }

        private ulong NextUlong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            ulong range = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUlong() % range);
        }

        public float NextFloat()
        {
            return (NextUlong() >> 11) * (1.0f / (1UL << 53));
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return minInclusive + NextFloat() * (maxExclusive - minInclusive);
        }

        public bool NextBool(float trueProbability = 0.5f)
        {
            return NextFloat() < trueProbability;
        }

        public T Choice<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("Cannot choose from an empty or null collection.", nameof(items));
            }

            return items[NextInt(0, items.Count)];
        }

        public T WeightedChoice<T>(IReadOnlyList<T> items, IReadOnlyList<float> weights)
        {
            if (items == null || weights == null || items.Count == 0 || items.Count != weights.Count)
            {
                throw new ArgumentException("Items and weights must be non-empty and the same length.");
            }

            float total = 0f;
            foreach (var w in weights)
            {
                if (w < 0f)
                {
                    throw new ArgumentException("Weights must be non-negative.", nameof(weights));
                }

                total += w;
            }

            if (total <= 0f)
            {
                throw new ArgumentException("Weights must sum to a positive value.", nameof(weights));
            }

            float roll = NextFloat(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                {
                    return items[i];
                }
            }

            return items[items.Count - 1];
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
