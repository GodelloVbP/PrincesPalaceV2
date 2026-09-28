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

        // Appended, never inserted: the EventDefinition assets serialize this
        // enum as an int (docs/PLAN_PETTING_ZOO.md, P1).
        Relic,
        PrincesFavor,
        FillSpecialPool,

        // docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.5, Stage A. Fight starts
        // one of the event's own fights (FightId); Finish marks a returning
        // event seen, so it never rolls again this run.
        Fight,
        Finish,

        // Stage B (the caravan). Shelf opens one of the event's own merchant
        // shelves (ShelfId), revealing its fakes when Reveal; TakeShelf hands
        // the party that shelf's unsold cards, Amount of them lost to the
        // scuffle first.
        Shelf,
        TakeShelf,
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

        // HealPercent and Exp, optional: empty means the whole squad. On
        // HealPercent a character id heals that one member and never revives
        // them (EventHealth.HealedWithoutRevive); on Exp it pays that member
        // alone (applied from M2). The display name is baked in at
        // content build for the effects line, the same reason
        // EventRequirement.CharacterDisplayName travels with its id.
        public string CharacterId = "";
        public string CharacterDisplayName = "";

        // Relic only: the relic granted to the run, and its display name for
        // the effects line (baked at build, as above).
        public string RelicId = "";
        public string RelicDisplayName = "";

        // Fight only: the id of one of the event's own fights
        // (ResolvedEventDefinition.FightById).
        public string FightId = "";

        // Shelf and TakeShelf: the id of one of the event's own shelves
        // (ResolvedEventDefinition.ShelfById). Reveal is Shelf's alone: this
        // visit marks the fakes, and the mark stays with the stock.
        //
        // AN APPLIED TakeShelf (the effects line's record, never authored)
        // carries the card the scuffle took in Item, empty when there was
        // nothing left to lose.
        public string ShelfId = "";
        public bool Reveal;

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

        public static EventEffect HealMemberPercent(string characterId, string characterDisplayName, int amount) =>
            new EventEffect(EventEffectKind.HealPercent, amount, "", "")
            {
                CharacterId = characterId ?? "",
                CharacterDisplayName = characterDisplayName ?? "",
            };
        public static EventEffect DamagePercent(int amount) => new EventEffect(EventEffectKind.DamagePercent, amount, "", "");
        public static EventEffect Exp(int amount) => new EventEffect(EventEffectKind.Exp, amount, "", "");

        // Exp to one member rather than split across the party (the Bell's
        // endings pay Shawn alone). Name baked for the effects line.
        public static EventEffect ExpTo(string characterId, string characterDisplayName, int amount) =>
            new EventEffect(EventEffectKind.Exp, amount, "", "")
            {
                CharacterId = characterId ?? "",
                CharacterDisplayName = characterDisplayName ?? "",
            };
        public static EventEffect ItemGrant(string itemId, int amount = 1) => new EventEffect(EventEffectKind.Item, amount, itemId, "");
        public static EventEffect Counter(string counterId, int amount) => new EventEffect(EventEffectKind.Counter, amount, "", counterId);

        public static EventEffect RelicGrant(string relicId, string relicDisplayName) =>
            new EventEffect(EventEffectKind.Relic, 0, "", "")
            {
                RelicId = relicId ?? "",
                RelicDisplayName = relicDisplayName ?? "",
            };

        // A run buff (EventBuffs): Prince's favor for the whole run.
        public static EventEffect PrincesFavor(int amount) => new EventEffect(EventEffectKind.PrincesFavor, amount, "", "");

        // A run buff (EventBuffs) for the current leg only. Amount carries
        // nothing and is always 1.
        public static EventEffect FillSpecialPool() => new EventEffect(EventEffectKind.FillSpecialPool, 1, "", "");

        public static EventEffect StartFight(string fightId) =>
            new EventEffect(EventEffectKind.Fight, 0, "", "") { FightId = fightId ?? "" };

        public static EventEffect Finish() => new EventEffect(EventEffectKind.Finish, 0, "", "");

        public static EventEffect OpenShelf(string shelfId, bool reveal) =>
            new EventEffect(EventEffectKind.Shelf, 0, "", "") { ShelfId = shelfId ?? "", Reveal = reveal };

        // `amount`: how many unsold cards the scuffle loses before the rest
        // are handed over.
        public static EventEffect TakeShelf(string shelfId, int amount) =>
            new EventEffect(EventEffectKind.TakeShelf, amount, "", "") { ShelfId = shelfId ?? "" };

        // What a TakeShelf did, for the effects line: the card it lost, or
        // "" when nothing was left to lose.
        public static EventEffect ShelfLoss(string shelfId, string lostItemId) =>
            new EventEffect(EventEffectKind.TakeShelf, 0, lostItemId, "") { ShelfId = shelfId ?? "" };

        public bool TargetsOneMember => !string.IsNullOrEmpty(CharacterId);
    }
}
