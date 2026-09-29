using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Party
{
    // A choice a character's kit cannot work without, made before the party
    // may descend.
    //
    // The one such choice today is the Fury engine root. A pool that says
    // engineFed gains nothing from its own flat triggers, so a character on
    // that pool with no root taken has a meter that never fills and skills
    // priced against it that are never affordable. The rule is derived from
    // content (the pool's flag, the root effects) rather than from a
    // character's name, so a second engine-fed character needs no code here.
    //
    // Domain cannot see Character or ContentDatabase; Core flattens each
    // squad member into a ChoiceCandidate and this answers.
    public static class RequiredChoices
    {
        // The three slot-0 talents of Bjorn's columns carry one of these.
        public static bool IsEngineRoot(TalentEffectType type) =>
            type == TalentEffectType.FuryEngineSentinel
            || type == TalentEffectType.FuryEngineEinherjar
            || type == TalentEffectType.FuryEngineJuggernaut;

        public static bool HasEngineRoot(IEnumerable<TalentEffectType> unlockedEffects)
        {
            if (unlockedEffects == null) return false;

            foreach (var type in unlockedEffects)
            {
                if (IsEngineRoot(type)) return true;
            }

            return false;
        }

        // Missing when the primary pool is engine-fed and no root is taken.
        // A null pool (a character with no resolvable kit) is never blocked:
        // nothing to choose, and the party must still be able to field them.
        public static bool IsMissingEngineRoot(ResolvedPool primaryPool, IEnumerable<TalentEffectType> unlockedEffects) =>
            primaryPool != null && primaryPool.EngineFed && !HasEngineRoot(unlockedEffects);

        // The first squad member with a choice outstanding, in seat order,
        // or null when the squad may descend.
        public static string FirstMissing(IEnumerable<ChoiceCandidate> squad)
        {
            if (squad == null) return null;

            foreach (var member in squad)
            {
                if (member.MissingEngineRoot) return member.Id;
            }

            return null;
        }
    }

    public readonly struct ChoiceCandidate
    {
        public readonly string Id;
        public readonly bool MissingEngineRoot;

        public ChoiceCandidate(string id, bool missingEngineRoot)
        {
            Id = id;
            MissingEngineRoot = missingEngineRoot;
        }
    }
}
