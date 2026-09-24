using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Events
{
    // The five kinds an event requirement can be, spelled the way an author
    // types them in a `requires` row's `kind` field (matched case-insensitively
    // by EventEntryResolver). One model serves three places -- event
    // eligibility, choice gating and outcome branching -- per plan contract 5.
    public enum EventRequirementKind
    {
        InParty,
        MemberLevel,
        Ability,
        Counter,
        Gold,
    }

    // One requirement's answer: whether it passed, and the caption a locked
    // choice shows either way (contract 7 reads this even when Passed is
    // true, since a choice that later fails a different requirement in its
    // list still needs a caption to show).
    public readonly struct EventRequirementResult
    {
        public readonly bool Passed;
        public readonly string Reason;

        public EventRequirementResult(bool passed, string reason)
        {
            Passed = passed;
            Reason = reason ?? "";
        }
    }

    // One validated requirement. Built only through the static factories
    // below (or by EventEntryResolver during content build) so an instance
    // can never mismatch its own Kind against the fields it carries.
    //
    // [Serializable] WITH PUBLIC FIELDS AND NO Nullable<T>, the same reason
    // RelicModifier gives: this is what ResolvedEventChoice/ResolvedEventOutcome
    // store on the EventDefinition ScriptableObject, and Nullable<T> does not
    // survive Unity's serializer. Counter's optional min/max use the
    // HasMin/HasMax flag pattern RelicModifier.HasAgainst already established
    // rather than a second, unserializable nullable field.
    [Serializable]
    public sealed class EventRequirement
    {
        public EventRequirementKind Kind;
        public string CharacterId = "";

        // Baked in at content-build time (ContentBuilder already has the
        // resolved character roster before events build), so Evaluate never
        // needs a ContentDatabase lookup to say "Requires Shawn" -- see
        // EventEntryResolver's own header for why the display name travels
        // with the id rather than being looked up at runtime.
        public string CharacterDisplayName = "";
        public AbilityScore Ability;
        public int Min;
        public bool HasMin;
        public int Max;
        public bool HasMax;
        public string CounterId = "";

        // For the serializer only -- every real instance comes from a
        // factory below.
        public EventRequirement()
        {
        }

        private EventRequirement(EventRequirementKind kind, string characterId, string characterDisplayName,
            AbilityScore ability, int min, bool hasMin, int max, bool hasMax, string counterId)
        {
            Kind = kind;
            CharacterId = characterId ?? "";
            CharacterDisplayName = characterDisplayName ?? "";
            Ability = ability;
            Min = min;
            HasMin = hasMin;
            Max = max;
            HasMax = hasMax;
            CounterId = counterId ?? "";
        }

        public static EventRequirement InParty(string characterId, string characterDisplayName) =>
            new EventRequirement(EventRequirementKind.InParty, characterId, characterDisplayName,
                default, 0, false, 0, false, "");

        public static EventRequirement MemberLevel(int min, string characterId = "", string characterDisplayName = "") =>
            new EventRequirement(EventRequirementKind.MemberLevel, characterId, characterDisplayName,
                default, min, true, 0, false, "");

        public static EventRequirement AbilityAtLeast(AbilityScore ability, int min, string characterId = "", string characterDisplayName = "") =>
            new EventRequirement(EventRequirementKind.Ability, characterId, characterDisplayName,
                ability, min, true, 0, false, "");

        // min/max are each independently optional -- an omitted one carries
        // hasMin/hasMax false rather than a sentinel value, so "no lower
        // bound" cannot be confused with "lower bound is 0".
        public static EventRequirement Counter(string counterId, int? min, int? max) =>
            new EventRequirement(EventRequirementKind.Counter, "", "",
                default, min ?? 0, min.HasValue, max ?? 0, max.HasValue, counterId);

        public static EventRequirement Gold(int min) =>
            new EventRequirement(EventRequirementKind.Gold, "", "", default, min, true, 0, false, "");

        public EventRequirementResult Evaluate(IEventContext context)
        {
            switch (Kind)
            {
                case EventRequirementKind.InParty:
                {
                    bool passed = context.SquadIds != null && context.SquadIds.Contains(CharacterId);
                    return new EventRequirementResult(passed, $"Requires {CharacterDisplayName}");
                }

                case EventRequirementKind.MemberLevel:
                {
                    if (!string.IsNullOrEmpty(CharacterId))
                    {
                        bool passed = context.SquadIds != null && context.SquadIds.Contains(CharacterId)
                                      && context.LevelOf(CharacterId) >= Min;
                        return new EventRequirementResult(passed, $"Requires {CharacterDisplayName} at level {Min}");
                    }

                    bool any = context.SquadIds != null && context.SquadIds.Any(id => context.LevelOf(id) >= Min);
                    return new EventRequirementResult(any, $"Requires a level {Min} party member");
                }

                case EventRequirementKind.Ability:
                {
                    string shortName = AbilityScores.ShortName(Ability);
                    if (!string.IsNullOrEmpty(CharacterId))
                    {
                        bool passed = context.SquadIds != null && context.SquadIds.Contains(CharacterId)
                                      && context.EffectiveAbilityScore(CharacterId, Ability) >= Min;
                        return new EventRequirementResult(passed, $"Requires {CharacterDisplayName} with {Min} {shortName}");
                    }

                    bool any = context.SquadIds != null
                               && context.SquadIds.Any(id => context.EffectiveAbilityScore(id, Ability) >= Min);
                    return new EventRequirementResult(any, $"Requires {Min} {shortName}");
                }

                case EventRequirementKind.Counter:
                {
                    int value = context.CounterValue(CounterId);
                    bool passed = (!HasMin || value >= Min) && (!HasMax || value <= Max);
                    return new EventRequirementResult(passed, CounterReason());
                }

                case EventRequirementKind.Gold:
                {
                    bool passed = context.Gold >= Min;
                    return new EventRequirementResult(passed, $"Requires {Min} gold");
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind,
                        "EventRequirement has no Evaluate case for this kind.");
            }
        }

        private string CounterReason()
        {
            if (HasMin && HasMax) return $"Requires {CounterId} between {Min} and {Max}";
            if (HasMin) return $"Requires {CounterId} at least {Min}";
            if (HasMax) return $"Requires {CounterId} at most {Max}";
            return $"Requires {CounterId}";
        }

        // Contract 6: a choice whose effects spend gold N gets an implied
        // gold >= N requirement, DERIVED from the effect rather than authored
        // twice, so a cost and its gate cannot drift apart. Returns null when
        // the choice's effects spend no gold, so a caller can skip adding
        // anything.
        public static EventRequirement ImpliedGoldRequirement(IReadOnlyList<EventEffect> choiceEffects)
        {
            if (choiceEffects == null) return null;

            int spend = 0;
            foreach (var effect in choiceEffects)
            {
                if (effect != null && effect.Kind == EventEffectKind.Gold && effect.Amount < 0)
                {
                    spend += -effect.Amount;
                }
            }

            return spend > 0 ? Gold(spend) : null;
        }
    }
}
