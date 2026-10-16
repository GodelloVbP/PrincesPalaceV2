using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // WHETHER A REWARD CARD IS GOOD NEWS (docs/PLAN_REWARD_SCREENS.md, the
    // definition above C0). The cache and the top-tier beat key on this, not on
    // quality, so the ceremony never celebrates a downgrade.
    //
    // Gear: not inert, and either a positive headline delta (the stat the swap
    // moves most, GearEvaluator.LargestDelta), or, with no stat change, an
    // affix or trait the worn copy lacks.
    // Relic: it reaches at least one fielded member who does not already get
    // its mechanic (PendingCard.AddsNothingFor).
    //
    // Pure reads over the save; nothing is equipped or written.
    public static class LiveUpgrade
    {
        public static bool Of(SaveData save, PendingCard card)
        {
            if (save == null || card == null) return false;
            return card.IsGear ? GearIsLive(save, card) : RelicIsLive(save, card);
        }

        // The card as the reveal sees it (RevealSchedule): its quality step
        // from the face (ContentDatabase.QualityOf, the one quality source),
        // whether it is good news, what its wearer wears, and whether its face
        // shows the up-chevron. `face` is the one the screen already built, or
        // null to build it here.
        public static RevealCard RevealCardOf(SaveData save, PendingCard card, RewardCardFace face = null)
        {
            if (face == null) face = card.IsGear ? RewardCardFace.OfGear(save, card) : RewardCardFace.OfRelic(card);
            bool live = Of(save, card);
            return card.IsGear
                ? new RevealCard(face.PlinthStep, live, WornTier(save, card), false, face.HasDelta && face.DeltaSign > 0)
                : RevealCard.Relic(face.PlinthStep, live);
        }

        // The tier (1..4, Common..Legendary) of the item the card's recipient
        // wears in the card's slot now; 0 for an empty slot or a relic card.
        // The reveal's cache and held beat compare against it.
        public static int WornTier(SaveData save, PendingCard card)
        {
            if (save == null || card == null || !card.IsGear) return 0;
            var recipient = save.roster?.FirstOrDefault(c => c != null && c.definitionId == card.RecipientId);
            if (recipient?.equipment == null || recipient.equipment.IsEmpty(card.Slot)) return 0;
            return (int)ContentDatabase.QualityOf(recipient.equipment.GetInstance(card.Slot)) + 1;
        }

        private static bool GearIsLive(SaveData save, PendingCard card)
        {
            if (card.Inert) return false;

            var recipient = save.roster?.FirstOrDefault(c => c != null && c.definitionId == card.RecipientId);
            if (recipient?.equipment == null) return false;

            if (GearEvaluator.LargestDelta(recipient, card.Copy, card.Slot, out _, out int delta)) return delta > 0;

            // No stat moves: an inert-in-simulation copy is no upgrade, and a
            // live one is an upgrade only for what it adds.
            if (!GearEvaluator.DeltaFor(recipient, card.Copy, card.Slot).HasValue) return false;

            var worn = recipient.equipment.IsEmpty(card.Slot)
                ? new List<string>()
                : recipient.equipment.GetModifierIds(card.Slot);
            return (card.Copy.ModifierIds ?? new List<string>()).Any(id => !worn.Contains(id));
        }

        private static bool RelicIsLive(SaveData save, PendingCard card)
        {
            var relic = ContentDatabase.GetRelic(card.RelicId)?.Data;
            if (relic == null) return false;

            int reached = save.ActiveSquad().Count(c => c != null && relic.ReachesCharacter(c.definitionId));
            int redundant = card.AddsNothingFor?.Count ?? 0;
            return reached > redundant;
        }
    }
}
