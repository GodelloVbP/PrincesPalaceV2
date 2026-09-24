using System;

namespace PrincesPalace.Domain.Events
{
    // What a choice or outcome does to run state, spelled the way an author
    // types it in an `effects` row's `kind` field (matched case-insensitively
    // by EventEntryResolver). Phase 1 stops at this data model -- applying an
    // effect to RunManager/SaveData is RunOrchestrator.Event.cs, phase 2, and
    // is documented (not built) by plan contract 10.
    public enum EventEffectKind
    {
        Gold,
        HealPercent,
        DamagePercent,
        Exp,
        Item,
        Counter,
    }

    // One effect. [Serializable] with public fields for the same reason
    // EventRequirement is -- stored on ResolvedEventChoice/ResolvedEventOutcome,
    // which the EventDefinition ScriptableObject serializes.
    [Serializable]
    public sealed class EventEffect
    {
        public EventEffectKind Kind;

        // Gold: +/- (a spend is negative and implies its own gold
        // requirement, see EventRequirement.ImpliedGoldRequirement).
        // HealPercent/DamagePercent: 1-100. Exp: > 0. Counter: the delta
        // added to CounterId, may be negative.
        public int Amount;

        // The item id granted; only Item reads this.
        public string Item = "";

        // The counter id incremented; only Counter reads this.
        public string CounterId = "";

        // For the serializer only -- every real instance comes from a
        // factory below.
        public EventEffect()
        {
        }

        private EventEffect(EventEffectKind kind, int amount, string item, string counterId)
        {
            Kind = kind;
            Amount = amount;
            Item = item ?? "";
            CounterId = counterId ?? "";
        }

        public static EventEffect Gold(int amount) => new EventEffect(EventEffectKind.Gold, amount, "", "");
        public static EventEffect HealPercent(int amount) => new EventEffect(EventEffectKind.HealPercent, amount, "", "");
        public static EventEffect DamagePercent(int amount) => new EventEffect(EventEffectKind.DamagePercent, amount, "", "");
        public static EventEffect Exp(int amount) => new EventEffect(EventEffectKind.Exp, amount, "", "");
        public static EventEffect ItemGrant(string itemId, int amount = 1) => new EventEffect(EventEffectKind.Item, amount, itemId, "");
        public static EventEffect Counter(string counterId, int amount) => new EventEffect(EventEffectKind.Counter, amount, "", counterId);
    }
}
