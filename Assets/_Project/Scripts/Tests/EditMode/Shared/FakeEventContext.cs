using System.Collections.Generic;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // A small in-memory IEventContext for event tests. Domain/Events takes
    // no dependency on RunSnapshot/SaveData/ContentDatabase, so tests hand it
    // plain state rather than anything from Core -- used by
    // EventRequirementTests, EventFlowTests and EventRollTests.
    public sealed class FakeEventContext : IEventContext
    {
        public List<string> Squad = new List<string>();
        public Dictionary<string, int> Levels = new Dictionary<string, int>();
        public Dictionary<(string, AbilityScore), int> Abilities = new Dictionary<(string, AbilityScore), int>();
        public Dictionary<string, int> Counters = new Dictionary<string, int>();
        public int GoldValue;

        public IReadOnlyList<string> SquadIds => Squad;
        public int Gold => GoldValue;

        public int LevelOf(string characterId) => Levels.TryGetValue(characterId, out int v) ? v : 0;

        public int EffectiveAbilityScore(string characterId, AbilityScore ability) =>
            Abilities.TryGetValue((characterId, ability), out int v) ? v : 0;

        public int CounterValue(string counterId) => Counters.TryGetValue(counterId, out int v) ? v : 0;
    }
}
