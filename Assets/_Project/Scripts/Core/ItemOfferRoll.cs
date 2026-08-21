using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Rolls the three items a won fight offers.
    //
    // Every RULE here already existed and had no caller: RarityTable decides
    // what a depth and an encounter class are worth, ItemOfferTable picks which
    // items given a target tier, and both are pure, injected-randomness and
    // fully tested. What was missing was the one Core-side step neither can
    // take -- turning ContentDatabase's ItemDefinitions into candidates -- so
    // the whole reward table sat unreachable behind it.
    //
    // Kept out of the controller because "what does a fight drop" is a rule
    // about the game, not about a screen, and the Reckoning is not the only
    // thing that will ever want to ask.
    public static class ItemOfferRoll
    {
        // The ceiling the tier roll clamps against, taken from content rather
        // than declared. A content pass that adds a tier 11 set should widen
        // the roll without anyone remembering to update a constant here.
        public static int MaxTier =>
            ContentDatabase.Items.Count == 0 ? 0 : ContentDatabase.Items.Max(i => i.tier);

        // EQUIPPABLES ONLY.
        //
        // A "choose one of three" that can offer a health potion is not a
        // choice, it is a tax on the one player who reads carefully. Potions
        // come from the shop and from drops; the fight-reward slot is where
        // gear comes from.
        public static IReadOnlyList<ItemOffer> Candidates()
        {
            return ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && !string.IsNullOrEmpty(i.id))
                .Select(i => new ItemOffer(i.id, i.tier))
                .ToList();
        }

        // What one character's Favor is worth: what they were AUTHORED with,
        // plus what the reward track has GRANTED them.
        //
        // The two halves live in different places because they have different
        // lifetimes -- CharacterDefinition.princesFavor is content, rebuilt
        // from characters.json by ContentBuilder and the same for every save;
        // Character.earnedFavor is progress, and belongs to one profile's one
        // character. This is the only place they meet, which is deliberate: a
        // second place that added them would be a second place that could
        // forget to.
        //
        // Tolerant of either side being missing, the house style: a character
        // whose definition has gone (content edited under a live save) still
        // contributes what they earned, and a character who has earned nothing
        // still contributes what they were authored with.
        public static int FavorOf(Character character, CharacterDefinition definition)
        {
            int authored = definition == null ? 0 : definition.princesFavor;
            int earned = character == null ? 0 : character.earnedFavor;

            int total = authored + earned;
            return total < 0 ? 0 : total;
        }

        // The squad's Prince's Favor: the HIGHEST among the fielded party,
        // never the sum.
        //
        // Highest rather than total because Favor is meant to be a reason to
        // FIELD a particular character, not a reason to field five. Summing
        // would make the stat scale with squad size, so the real decision
        // would become "bring more bodies" -- which is not a decision about
        // Favor at all.
        //
        // Takes the per-member totals rather than the definitions it used to,
        // so that this rule and the authored-plus-earned rule above are two
        // separate facts in two separate functions. It previously read
        // princesFavor off the definition itself, which meant "where does a
        // member's Favor come from" and "how does a squad combine it" were the
        // same four lines and could not be changed independently.
        public static int SquadFavor(IEnumerable<int> memberFavors)
        {
            if (memberFavors == null) return 0;

            int best = 0;
            foreach (int favor in memberFavors)
            {
                if (favor > best) best = favor;
            }

            return best;
        }

        // The fielded squad's Favor, read off the save.
        //
        // Here rather than at the call site so the rule -- which squad, and
        // highest-not-sum -- lives with the roll it feeds, and so the fight
        // controller does not have to learn how a definitionId maps to a
        // definition.
        public static int CurrentSquadFavor()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return 0;

            return SquadFavor(save.ActiveSquad()
                .Select(c => FavorOf(c, ContentDatabase.Characters
                    .FirstOrDefault(d => d != null && d.id == c.definitionId))));
        }

        // How many items the fielded squad is offered.
        //
        // Best level in the squad; SquadTrack owns that rule and why.
        public static int CurrentOfferWidth() => OfferRowLayout.CardsFor(SquadTrack.BestLevel());

        // How many rerolls the fielded squad gets per descent.
        public static int CurrentRerollAllowance() => RewardTrack.RerollsPerRun(SquadTrack.BestLevel());

        // The offers, each with its own independently rolled plus.
        //
        // `nextIndex` is upper-bound-exclusive and injected, matching the shape
        // both Domain tables already take -- so a caller under test can hand in
        // a seeded stand-in and get the same items every time.
        //
        // `count` defaults to the base three so every existing caller reads as
        // it did. The live call site passes CurrentOfferWidth(), because the
        // reward track widens the offer at level 50.
        public static List<ItemOffer> Roll(EncounterClass encounter, int depthStep, int favor, Func<int, int> nextIndex,
            int count = ItemOfferTable.OfferCount)
        {
            var offers = new List<ItemOffer>();
            if (nextIndex == null) return offers;

            var candidates = Candidates();
            if (candidates.Count == 0) return offers;

            int maxTier = MaxTier;
            int targetTier = RarityTable.RollTier(encounter, depthStep, maxTier, favor, nextIndex);

            // Tier is rolled ONCE for the offer set and plus is rolled PER
            // ITEM. Rolling the tier per item would quietly widen the spread
            // ItemOfferTable.TierSpread already controls, and make the three
            // offers three separate difficulty statements rather than one.
            foreach (var offer in ItemOfferTable.Choose(candidates, targetTier, maxTier, nextIndex, count))
            {
                offers.Add(offer.WithPlus(RarityTable.RollPlus(encounter, favor, nextIndex)));
            }

            return offers;
        }
    }
}
