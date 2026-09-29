using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat
{
    // Everything a combatant's unlocked talents contribute to a fight,
    // flattened once at encounter build time and then read from.
    //
    // Resolved ONCE rather than looked up per swing, exactly like
    // CombatantState.WeaponScaling/SkillScaling already are and for the same
    // reason: a talent set cannot change mid-fight (the talent screen is a
    // Hub screen), and the alternative is every damage path in the pipeline
    // walking a character's unlockedTalentIds through ContentDatabase on
    // every hit.
    //
    // Empty is the normal case. Every enemy, and every player character
    // whose tree is still the old stat-bump vocabulary, carries an empty set
    // — so every query below has to be cheap and correct at zero entries,
    // which is why this is a plain list and not a dictionary of lists.
    //
    // MAX, NOT SUM, is the rule for repeated types, and it is the single
    // most load-bearing decision in this file. A strand is one idea developed
    // three times (handoff §5), and its prereq chain means owning T2
    // necessarily means owning T1 — so Sharp Horns' 25% and 50% are both
    // present in the set at once. Summing them would give 75%, which is
    // neither tier and grows worse the longer a strand gets. Genuinely
    // additive rules do not exist in this vocabulary; if one ever does it
    // wants its own accessor here, not a change to this one.
    public sealed class TalentEffectSet
    {
        // Shared, never mutated. Handed to every combatant with no talents,
        // which is nearly all of them, so the common case allocates nothing.
        public static readonly TalentEffectSet Empty = new TalentEffectSet(new List<TalentEffect>());

        private readonly List<TalentEffect> _effects;

        public TalentEffectSet(IEnumerable<TalentEffect> effects)
        {
            _effects = effects == null ? new List<TalentEffect>() : new List<TalentEffect>(effects);
        }

        public IReadOnlyList<TalentEffect> All => _effects;

        public bool IsEmpty => _effects.Count == 0;

        // True when this type is present at all — the question the four
        // flag-shaped effects ask, where Magnitude carries no meaning.
        public bool Has(TalentEffectType type)
        {
            foreach (var effect in _effects)
            {
                if (effect.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        // The strongest magnitude authored for this type, or 0 if absent —
        // see the MAX-not-SUM note in this class' own header. 0 is a safe
        // "no rule" answer for every caller because every magnitude in this
        // vocabulary is a bonus or a percentage, so zero and absent mean the
        // same thing.
        public int Best(TalentEffectType type)
        {
            int best = 0;
            foreach (var effect in _effects)
            {
                if (effect.Type == type && effect.Magnitude > best)
                {
                    best = effect.Magnitude;
                }
            }

            return best;
        }

        // Best, restricted to the rules that name this skill. A skill-scoped
        // rule (TalentEffect.IsSkillScoped) is invisible to every other
        // skill, and an empty skill id matches nothing.
        public int BestFor(TalentEffectType type, string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return 0;

            int best = 0;
            foreach (var effect in _effects)
            {
                if (effect.Type == type && effect.SkillId == skillId && effect.Magnitude > best)
                {
                    best = effect.Magnitude;
                }
            }

            return best;
        }

        // The strongest magnitude among the entries of this type whose health
        // threshold is currently SATISFIED — the "replace, not stack" rule
        // for tiered health gates (TalentEffectType.WoolPerTurnBelowHealth's
        // own comment).
        //
        // A Ram at 30% health satisfies both the 67% tier (worth 2) and the
        // 33% tier (worth 3): the answer is 3, not 5. Stacking them was the
        // reading the handoff left open in §6.1, and it pushes the ceiling to
        // +4/turn plus on-hit gains against abilities priced at 7 — which is
        // not the economy the prices were checked against.
        public int BestBelowHealth(TalentEffectType type, CombatantState holder)
        {
            int best = 0;
            foreach (var effect in _effects)
            {
                if (effect.Type != type || !AtOrBelow(holder, effect.Threshold))
                {
                    continue;
                }

                if (effect.Magnitude > best)
                {
                    best = effect.Magnitude;
                }
            }

            return best;
        }

        // "Is this combatant at or below `threshold` percent of its maximum
        // health", in INTEGER arithmetic.
        //
        // Cross-multiplied rather than compared against current/max as a
        // float, and that is not fussiness. The float form is
        // `fraction > threshold / 100f`, and a runtime free to evaluate that
        // division at extended precision produces a value fractionally BELOW
        // float(0.67) — so a combatant sitting at exactly 67% of maximum
        // failed his own 67% gate, on some builds and not others. Found by a
        // test that asserted the boundary case rather than a comfortable
        // number either side of it, which is the same lesson CLAUDE.md
        // gotcha #5 records about pinning formulas with literals.
        //
        // At or BELOW, inclusive: BreakShield's own stagger meter breaks the
        // instant Current reaches zero, not one point past it (BreakShield.
        // Deplete's Current == 0 check) -- the same inclusive-at-threshold
        // rule this gate follows, and a rule that switched on one point of
        // health later than the number printed on the node would read as a
        // bug the same way.
        private static bool AtOrBelow(CombatantState holder, int threshold)
        {
            if (holder == null || holder.MaxHealth <= 0)
            {
                // No health pool at all cannot be above any threshold.
                return true;
            }

            return (long)holder.CurrentHealth * 100 <= (long)holder.MaxHealth * threshold;
        }

        // The health threshold authored on the first entry of this type, or
        // 0 if absent — for the flag-shaped health gates
        // (TransformHoldsBelowHealth, TransformPermanentBelowHealth) where
        // the threshold IS the payload and the magnitude is ignored.
        //
        // Lowest rather than highest, deliberately: two entries would mean
        // two tiers of the same rule, and the stricter gate is the one that
        // describes the node the player actually bought last.
        public int Threshold(TalentEffectType type)
        {
            int found = 0;
            foreach (var effect in _effects)
            {
                if (effect.Type != type)
                {
                    continue;
                }

                if (found == 0 || effect.Threshold < found)
                {
                    found = effect.Threshold;
                }
            }

            return found;
        }

        // True when the holder is at or below this type's authored health
        // threshold — the shape the two flag-shaped health gates want, in one
        // place rather than at each of their call sites.
        public bool IsBelowThreshold(TalentEffectType type, CombatantState holder)
        {
            int threshold = Threshold(type);
            if (threshold <= 0 || holder == null || holder.MaxHealth <= 0)
            {
                return false;
            }

            return AtOrBelow(holder, threshold);
        }
    }
}
