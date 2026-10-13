using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat
{
    // Everything a combatant holds -- the run's relics that reach them, their
    // worn affixes, their reward-track terms, their unlocked talents -- flattened once when the fight
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

        public static EffectSet OfTalents(IEnumerable<Effect> effects) => Of(EffectHolder.Talent, effects);

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
        // same. Talents are best-of as well, apart from affixes: a strand is one
        // idea developed three times, so owning T2 means owning T1 and the two
        // must not add (Sharp Horns' 25 and 50 are 50, not 75).
        public int Magnitude(EffectKind kind) => Magnitude(kind, null);

        // Magnitude with one holder left out: EffectFold skips the Talent
        // holder because EffectiveStats has already folded a talent's stat.
        public int MagnitudeWithout(EffectKind kind, EffectHolder skipped) => Magnitude(kind, skipped);

        private int Magnitude(EffectKind kind, EffectHolder? skipped)
        {
            var stacking = EffectCatalog.Of(kind).Stacking;
            int bestAffix = 0;
            int bestTalent = 0;
            int others = 0;
            int strongest = 0;

            foreach (var entry in _held)
            {
                if (entry.Effect.Kind != kind) continue;
                if (skipped.HasValue && entry.Holder == skipped.Value) continue;
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
                        else if (entry.Holder == EffectHolder.Talent)
                        {
                            if (magnitude > bestTalent) bestTalent = magnitude;
                        }
                        else
                        {
                            others += magnitude;
                        }

                        break;
                }
            }

            return stacking == EffectStacking.Flag ? strongest : bestAffix + bestTalent + others;
        }

        // Magnitude, restricted to the entries that name this skill. A
        // skill-scoped rule is invisible to every other skill, and an empty
        // skill id matches nothing.
        public int BestFor(EffectKind kind, string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return 0;

            int best = 0;
            foreach (var entry in _held)
            {
                var effect = entry.Effect;
                if (effect.Kind == kind && effect.SkillId == skillId && effect.Magnitude > best)
                {
                    best = effect.Magnitude;
                }
            }

            return best;
        }

        // BestFor, restricted to the entries whose health threshold the HOLDER
        // currently satisfies (at or below Threshold percent of his own max
        // health). The shape of a skill-scoped, health-gated bonus: Gorge's
        // extra damage while he is under half health.
        public int BestForBelowHealth(EffectKind kind, string skillId, CombatantState holder)
        {
            if (string.IsNullOrEmpty(skillId)) return 0;

            int best = 0;
            foreach (var entry in _held)
            {
                var effect = entry.Effect;
                if (effect.Kind != kind || effect.SkillId != skillId || !AtOrBelow(holder, effect.Threshold))
                {
                    continue;
                }

                if (effect.Magnitude > best) best = effect.Magnitude;
            }

            return best;
        }

        // The strongest magnitude among the entries of this kind whose health
        // threshold is currently SATISFIED -- the "replace, not stack" rule for
        // tiered health gates (WoolPerTurnBelowHealth's own comment).
        //
        // A Ram at 30% health satisfies both the 67% tier (worth 2) and the
        // 33% tier (worth 3): the answer is 3, not 5. Stacking them was the
        // reading the handoff left open in section 6.1, and it pushes the ceiling to
        // +4/turn plus on-hit gains against abilities priced at 7.
        public int BestBelowHealth(EffectKind kind, CombatantState holder)
        {
            int best = 0;
            foreach (var entry in _held)
            {
                var effect = entry.Effect;
                if (effect.Kind != kind || !AtOrBelow(holder, effect.Threshold)) continue;

                if (effect.Magnitude > best) best = effect.Magnitude;
            }

            return best;
        }

        // "Is this combatant at or below `threshold` percent of its maximum
        // health", in INTEGER arithmetic.
        //
        // Cross-multiplied rather than compared against current/max as a float:
        // the float form is `fraction > threshold / 100f`, and a runtime free
        // to evaluate that division at extended precision produced a value
        // fractionally BELOW float(0.67), so a combatant sitting at exactly 67%
        // of maximum failed his own 67% gate on some builds and not others.
        // At or BELOW, inclusive, like BreakShield's own stagger meter.
        private static bool AtOrBelow(CombatantState holder, int threshold)
        {
            if (holder == null || holder.MaxHealth <= 0)
            {
                // No health pool at all cannot be above any threshold.
                return true;
            }

            return (long)holder.CurrentHealth * 100 <= (long)holder.MaxHealth * threshold;
        }

        // The lowest non-zero threshold authored for this kind, or 0 when
        // absent. Lowest rather than first: two entries mean two tiers of the
        // same rule, and the stricter gate describes the node bought last.
        public int Threshold(EffectKind kind)
        {
            int found = 0;
            foreach (var entry in _held)
            {
                if (entry.Effect.Kind != kind) continue;

                int threshold = entry.Effect.Threshold;
                if (found == 0 || (threshold != 0 && threshold < found)) found = threshold;
            }

            return found;
        }

        // True when the holder is at or below this kind's authored health
        // threshold -- the shape the flag-shaped health gates want
        // (TransformHoldsBelowHealth, TransformPermanentBelowHealth).
        public bool IsBelowThreshold(EffectKind kind, CombatantState holder)
        {
            int threshold = Threshold(kind);
            if (threshold <= 0 || holder == null || holder.MaxHealth <= 0)
            {
                return false;
            }

            return AtOrBelow(holder, threshold);
        }
    }
}
