using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // IEventContext over the live save and run -- the real implementation
    // Domain/Events' header promises, and the only one outside tests.
    //
    // READS LIVE, every call, and holds nothing but the two references. An
    // event choice ticks a counter and spends gold BEFORE its outcomes are
    // checked (EventFlow's contract: "the context already carries the
    // counter value the outcome check evaluates against"), so a context that
    // snapshotted either at construction would check the tenth toss against
    // the ninth.
    //
    // The squad is SaveData.ActiveSquadIds -- the one definition of "who is in
    // the party" -- and ability scores are EFFECTIVE (gear and talents), per
    // plan contract 5 and assumption 2.
    public sealed class RunEventContext : IEventContext
    {
        private readonly SaveData _save;
        private readonly RunSnapshot _run;

        public RunEventContext(SaveData save, RunSnapshot run)
        {
            _save = save;
            _run = run;
        }

        public IReadOnlyList<string> SquadIds =>
            _save != null ? _save.ActiveSquadIds() : new List<string>();

        public int LevelOf(string characterId) => Member(characterId)?.level ?? 0;

        public int EffectiveAbilityScore(string characterId, AbilityScore ability)
        {
            var character = Member(characterId);
            return character == null ? 0 : ContentDatabase.EffectiveAbilityScores(character)[ability];
        }

        public int CounterValue(string counterId) => _save?.EventCounter(counterId) ?? 0;

        public int Gold => _run?.gold ?? 0;

        public bool IsStanding(string characterId)
        {
            if (_run?.currentHealth == null || string.IsNullOrEmpty(characterId)) return true;

            foreach (var entry in _run.currentHealth)
            {
                if (entry != null && entry.characterId == characterId) return entry.hp > 0;
            }

            return true;
        }

        // Roster, not squad: IEventContext's own contract says callers ask
        // this only of ids they already know are in the squad, and the roster
        // is where the Character lives either way.
        private Character Member(string characterId)
        {
            if (_save?.roster == null || string.IsNullOrEmpty(characterId)) return null;

            foreach (var character in _save.roster)
            {
                if (character != null && character.definitionId == characterId) return character;
            }

            return null;
        }
    }
}
