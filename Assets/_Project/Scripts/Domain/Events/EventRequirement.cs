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

        // The author's own caption for a locked choice. When set it replaces
        // the generated one for every kind: a counter's generated caption
        // cannot say what the counter means to the player, and a character
        // or level caption may want the story's voice. Empty means generated.
        public string AuthoredReason = "";

        // Shown for a counter gate with no authored reason. Deliberately
        // generic: the counter id is an internal name ("wishing_well_tosses")
        // and a number range says nothing a player can act on. "Not yet"
        // while below min, "No longer" once past max.
        public const string CounterBelowMinReason = "Not yet";
        public const string CounterAboveMaxReason = "No longer";

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

        // Sets the author's caption (see AuthoredReason) and returns this, so
        // the resolver can chain it onto a factory. Whitespace-only is empty.
        public EventRequirement WithAuthoredReason(string reason)
        {
            AuthoredReason = string.IsNullOrWhiteSpace(reason) ? "" : reason.Trim();
            return this;
        }

        // Every caption this requirement can ever show, for the content
        // build's length cap. Independent of the run, so the build can check
        // it once rather than hoping each run's state produces a short one.
        public IEnumerable<string> PossibleReasons()
        {
            if (!string.IsNullOrEmpty(AuthoredReason))
            {
                yield return AuthoredReason;
                yield break;
            }

            if (Kind == EventRequirementKind.Counter)
            {
                if (HasMin || !HasMax) yield return CounterBelowMinReason;
                if (HasMax) yield return CounterAboveMaxReason;
                yield break;
            }

            yield return GeneratedReason(0);
        }

        public EventRequirementResult Evaluate(IEventContext context)
        {
            int counterValue = Kind == EventRequirementKind.Counter ? context.CounterValue(CounterId) : 0;
            bool passed = Passes(context, counterValue);
            string reason = string.IsNullOrEmpty(AuthoredReason) ? GeneratedReason(counterValue) : AuthoredReason;
            return new EventRequirementResult(passed, reason);
        }

        private bool Passes(IEventContext context, int counterValue)
        {
            bool named = !string.IsNullOrEmpty(CharacterId);
            bool namedInSquad = named && context.SquadIds != null && context.SquadIds.Contains(CharacterId);

            switch (Kind)
            {
                case EventRequirementKind.InParty:
                    return namedInSquad;

                case EventRequirementKind.MemberLevel:
                    if (named) return namedInSquad && context.LevelOf(CharacterId) >= Min;
                    return context.SquadIds != null && context.SquadIds.Any(id => context.LevelOf(id) >= Min);

                case EventRequirementKind.Ability:
                    if (named) return namedInSquad && context.EffectiveAbilityScore(CharacterId, Ability) >= Min;
                    return context.SquadIds != null
                           && context.SquadIds.Any(id => context.EffectiveAbilityScore(id, Ability) >= Min);

                case EventRequirementKind.Counter:
                    return (!HasMin || counterValue >= Min) && (!HasMax || counterValue <= Max);

                case EventRequirementKind.Gold:
                    return context.Gold >= Min;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind,
                        "EventRequirement has no Evaluate case for this kind.");
            }
        }

        // Player-facing: never an internal id. Characters show the display
        // name baked in at content build, abilities their short name, and a
        // counter only the generic wording above. counterValue picks between
        // the two counter captions; the other kinds ignore it.
        private string GeneratedReason(int counterValue)
        {
            switch (Kind)
            {
                case EventRequirementKind.InParty:
                    return $"Requires {CharacterDisplayName}";

                case EventRequirementKind.MemberLevel:
                    return string.IsNullOrEmpty(CharacterId)
                        ? $"Requires a level {Min} party member"
                        : $"Requires {CharacterDisplayName} at level {Min}";

                case EventRequirementKind.Ability:
                {
                    string shortName = AbilityScores.ShortName(Ability);
                    return string.IsNullOrEmpty(CharacterId)
                        ? $"Requires {Min} {shortName}"
                        : $"Requires {CharacterDisplayName} with {Min} {shortName}";
                }

                case EventRequirementKind.Counter:
                    return HasMax && counterValue > Max ? CounterAboveMaxReason : CounterBelowMinReason;

                case EventRequirementKind.Gold:
                    return $"Requires {Min} gold";

                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind,
                        "EventRequirement has no reason for this kind.");
            }
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
