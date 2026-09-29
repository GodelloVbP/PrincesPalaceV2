using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Party;

namespace PrincesPalace
{
    // Core's half of Domain/Party/RequiredChoices: flattens a Character into
    // the two facts the rule needs and hands the rule the answer's inputs.
    // The hub gate, the Talents screen's arrival and the Party card flag all
    // read this, so they cannot disagree about who is blocked.
    public static class PartyReadiness
    {
        public static bool IsMissingEngineRoot(Character character)
        {
            if (character == null) return false;

            var pool = ContentDatabase.PrimaryPoolOf(character.definitionId);
            return RequiredChoices.IsMissingEngineRoot(pool, UnlockedEffectTypes(character));
        }

        // The first fielded member with a choice outstanding, or null.
        public static Character FirstBlocked(SaveData save)
        {
            if (save == null) return null;

            var squad = save.ActiveSquad();
            string id = RequiredChoices.FirstMissing(
                squad.Select(c => new ChoiceCandidate(c.definitionId, IsMissingEngineRoot(c))));
            return id == null ? null : squad.FirstOrDefault(c => c.definitionId == id);
        }

        private static IEnumerable<TalentEffectType> UnlockedEffectTypes(Character character)
        {
            foreach (string talentId in character.unlockedTalentIds ?? new List<string>())
            {
                var talent = ContentDatabase.GetTalent(talentId);
                if (talent == null) continue;

                foreach (var effect in talent.Data.Effects) yield return effect.Type;
            }
        }
    }
}
