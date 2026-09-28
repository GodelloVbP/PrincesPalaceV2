using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Events
{
    // The read surface EventRequirement.Evaluate and EventRoll need, and
    // nothing else -- no save, no ContentDatabase, no RunSnapshot. The real
    // implementation wires over RunSnapshot/SaveData/ContentDatabase;
    // EditMode tests here hand it a small fake instead, which is the whole
    // point of the interface existing at this layer rather than one level up.
    public interface IEventContext
    {
        // The active squad, in whatever order the caller holds it. "In the
        // party" and "any squad member" both read this rather than a second
        // membership check that could disagree with it.
        IReadOnlyList<string> SquadIds { get; }

        // Character.level for the named id. Undefined for an id not in
        // SquadIds -- callers only ask this of ids they already know are in
        // the squad (EventRequirement.Evaluate checks membership first).
        int LevelOf(string characterId);

        // ContentDatabase.EffectiveAbilityScores(characterId)[ability] in the
        // real implementation -- gear and talents included, per contract 5.
        int EffectiveAbilityScore(string characterId, AbilityScore ability);

        // A persistent counter's current value; 0 for one that has never
        // been incremented, same as the zero-value a dictionary lookup would
        // give.
        int CounterValue(string counterId);

        // The run's current gold.
        int Gold { get; }

        // Whether the named character's carried run health is above 0. A
        // character with no health entry has not been hurt this run and is
        // standing -- the reading EncounterRoll.FieldableParty gives a
        // missing entry. Says nothing about squad membership; `inParty` with
        // `alive` asks both.
        bool IsStanding(string characterId);
    }
}
