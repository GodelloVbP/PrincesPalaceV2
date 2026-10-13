using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat
{
    // Everything a combatant's equipped items' rolled modifiers contribute to
    // a fight, flattened once at encounter build time — MIRRORS
    // TalentEffectSet exactly, same reasoning: a modifier set cannot change
    // mid-fight (gear is chosen from the Equipment screen, a Hub screen), so
    // resolving it once here beats every damage path walking a character's
    // equipment through ContentDatabase on every hit. See TalentEffectSet's
    // own header for the full argument; this repeats only what differs.
    //
    // EMPTY IS THE COMMON CASE, more emphatically than for TalentEffectSet:
    // only a worn piece that rolled affixes (ItemOfferRoll, via
    // ModifierTable.PickModifiers) or a reward-track elemental term
    // contributes anything, so a combatant in plain gear carries Empty.
    //
    // MAX, NOT SUM, for repeated types -- the identical rule TalentEffectSet
    // uses and the identical reasoning: two copies of the same modifier
    // effect do not stack additively, so Best() answers "the strongest
    // present", not "the total". A consumer that wants a total folds .All
    // itself.
    //
    // NO BestBelowHealth YET, unlike TalentEffectSet. None of Phase A2's
    // five stub ModifierEffectType members are health-gated (see that
    // enum's own header), so there is nothing to exercise a health-gate
    // query against and no way to test one honestly — a method with no
    // caller and no real test is exactly the kind of unearned surface this
    // codebase avoids. Add it, mirroring TalentEffectSet.BestBelowHealth
    // (including its AtOrBelow cross-multiplication — see that method's own
    // comment on why float comparison is unsafe there) verbatim, the moment
    // a real modifier needs one; the plan's Phase C flags a "retaliate below
    // X% health" affix as a likely candidate. CombatantState is already the
    // same holder type TalentEffectSet.BestBelowHealth takes, so the method
    // will drop in unchanged when that day comes.
    public sealed class ModifierEffectSet
    {
        // Shared, never mutated, so the common empty case allocates nothing.
        public static readonly ModifierEffectSet Empty = new ModifierEffectSet(new List<ModifierEffect>());

        private readonly List<ModifierEffect> _effects;

        public ModifierEffectSet(IEnumerable<ModifierEffect> effects)
        {
            _effects = effects == null ? new List<ModifierEffect>() : new List<ModifierEffect>(effects);
        }

        public IReadOnlyList<ModifierEffect> All => _effects;

        public bool IsEmpty => _effects.Count == 0;

        // True when this type is present at all — the question
        // GuaranteedFirstAction (and any future flag-shaped member) asks,
        // where Magnitude carries no meaning.
        public bool Has(ModifierEffectType type)
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
        //
        // For TypedResistanceFlat specifically, this answers "the strongest
        // resistance to ANY element this set grants", collapsing the
        // Against/AgainstMagical side-channel — which is exactly why a real
        // resistance consumer (Phase C) should read .All and fold per
        // element, the way RelicModifiers.ApplyResistance already does for
        // RelicModifier, rather than call Best(TypedResistanceFlat) and
        // expect a single element's number back.
        public int Best(ModifierEffectType type)
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
    }
}
