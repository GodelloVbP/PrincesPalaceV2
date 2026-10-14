using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Rewards
{
    // What became of one card on a page.
    public enum CardOutcome
    {
        Open,

        // Gear: equipped on its recipient. Relic: added to the run.
        Taken,

        // Gear: +1 honing on the card's slot for its recipient (hone instead,
        // or an unused choice on leaving).
        Honed,

        // Gold instead: hone instead with every shown slot at MaxPlus, or an
        // unused choice that had nothing left to hone.
        Paid,
    }

    public sealed class PendingCard
    {
        // Gear card: the copy. Null on a relic card.
        public ItemInstance Copy;

        // Relic card: the relic. Empty on a gear card.
        public string RelicId = "";

        // Gear: the member the card is made for. Empty on a relic card.
        public string RecipientId = "";

        // Gear: the slot the card fills on its recipient.
        public EquipmentSlot Slot;

        // Gear: GearCard.Rolled. Relic: unused (the rarity is the relic's).
        public Quality Rolled;
        public bool Inert;

        // Relic: the shown relic's rarity, for the unused-choice payout.
        public RelicRarity Rarity;

        // Relic: fielded members it reaches who already get its mechanic,
        // named on the card ("adds nothing for").
        public List<string> AddsNothingFor = new List<string>();

        public CardOutcome Outcome;

        // Gear, taken: the copy it displaced, now in the bag. Listed on the
        // page.
        public ItemInstance Displaced;

        // Gold this card paid out (Paid).
        public int GoldPaid;

        public bool IsGear => Copy != null;
    }

    // ONE SETTLEMENT PAGE, rolled once at settlement and held in memory on
    // the run (RunSnapshot.pendingPages, [NonSerialized]): a run found on
    // disk is settled, never resumed (docs/PLAN_ITEMS.md 2.5), so a page
    // never needs to survive a reload.
    public sealed class PendingPage
    {
        public string SourceId = "";
        public RewardPageKind Kind;
        public EncounterClass SourceClass;

        // The settlement step: what every price on the page scales by.
        public int Step;
        public int NodeId;

        // 0 gear, 1 relic: the stream's third key.
        public int Page;

        // k: one per merged chip.
        public int Choices;
        public int Resolved;

        // The player left the page; unused choices have been paid.
        public bool Closed;

        // What leaving paid for the unused choices: hones applied (gear) and
        // gold (relic, or gear with nothing left to hone).
        public int UnusedHones;
        public int UnusedGold;

        public List<PendingCard> Cards = new List<PendingCard>();

        public int ChoicesLeft => Closed ? 0 : Choices - Resolved < 0 ? 0 : Choices - Resolved;

        public const int GearPageIndex = 0;
        public const int RelicPageIndex = 1;
    }
}
