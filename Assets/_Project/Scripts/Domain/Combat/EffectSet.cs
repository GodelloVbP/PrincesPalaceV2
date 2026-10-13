using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat
{
    // Everything a combatant holds -- the run's relics that reach them, their
    // worn affixes, their reward-track terms -- flattened once when the fight
    // is built (FightEncounterAdapter) and read by every hook through Has /
    // Magnitude / Threshold. Nothing a combatant holds changes mid-fight, so
    // resolving it once beats walking relics and equipment on every hit.
    //
    // Each entry keeps its holder, because stacking depends on who holds what
    // (EffectStacking): two affix copies give the best one, an affix and a
    // relic add. Entries keep the order they were added in -- the per-entry
    // readers (elemental riders) fire in that order.
    public sealed class EffectSet
    {
        public readonly struct Held
        {
            public readonly EffectHolder Holder;
            public readonly Effect Effect;

            public Held(EffectHolder holder, Effect effect)
            {
                Holder = holder;
                Effect = effect;
            }
        }

        // Shared, never mutated, so the common empty case allocates nothing.
        public static readonly EffectSet Empty = new EffectSet(new List<Held>());

        private readonly List<Held> _held;
        private readonly List<Effect> _all;

        public EffectSet(IEnumerable<Held> held)
        {
            _held = held == null ? new List<Held>() : new List<Held>(held);
            _all = new List<Effect>(_held.Count);
            foreach (var entry in _held) _all.Add(entry.Effect);
        }

        public static EffectSet Of(EffectHolder holder, IEnumerable<Effect> effects) => Empty.With(holder, effects);

        // A new set: these entries after the existing ones.
        public EffectSet With(EffectHolder holder, IEnumerable<Effect> effects)
        {
            var held = new List<Held>(_held);
            if (effects != null)
            {
                foreach (var effect in effects) held.Add(new Held(holder, effect));
            }

            return held.Count == _held.Count ? this : new EffectSet(held);
        }

        // The run's relics, as far as they reach this character: a relic with
        // a `bearer` gives nobody else any of its effects.
        public EffectSet WithRelics(IReadOnlyList<ResolvedRelic> relics, string characterId)
        {
            if (relics == null || relics.Count == 0) return this;

            var reaching = new List<Effect>();
            foreach (var relic in relics)
            {
                if (relic == null || relic.Effects == null || !relic.ReachesCharacter(characterId)) continue;
                reaching.AddRange(relic.Effects);
            }

            return With(EffectHolder.Relic, reaching);
        }

        public IReadOnlyList<Effect> All => _all;

        public IReadOnlyList<Held> Entries => _held;

        public bool IsEmpty => _held.Count == 0;

        public bool Has(EffectKind kind)
        {
            foreach (var entry in _held)
            {
                if (entry.Effect.Kind == kind) return true;
            }

            return false;
        }

        // The kind's combined magnitude under its catalog stacking; 0 when
        // absent. Best-of starts at 0, so an absent affix and no affix read the
        // same.
        public int Magnitude(EffectKind kind)
        {
            var stacking = EffectCatalog.Of(kind).Stacking;
            int bestAffix = 0;
            int others = 0;
            int strongest = 0;

            foreach (var entry in _held)
            {
                if (entry.Effect.Kind != kind) continue;
                int magnitude = entry.Effect.Magnitude;

                switch (stacking)
                {
                    case EffectStacking.Flag:
                        if (magnitude > strongest) strongest = magnitude;
                        break;
                    case EffectStacking.Summed:
                        others += magnitude;
                        break;
                    default:
                        if (entry.Holder == EffectHolder.Affix)
                        {
                            if (magnitude > bestAffix) bestAffix = magnitude;
                        }
                        else
                        {
                            others += magnitude;
                        }

                        break;
                }
            }

            return stacking == EffectStacking.Flag ? strongest : bestAffix + others;
        }

        // The first holder's threshold for this kind, or 0 when absent.
        public int Threshold(EffectKind kind)
        {
            foreach (var entry in _held)
            {
                if (entry.Effect.Kind == kind) return entry.Effect.Threshold;
            }

            return 0;
        }
    }
}
