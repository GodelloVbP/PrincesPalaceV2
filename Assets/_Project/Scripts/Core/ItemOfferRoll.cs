using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;

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

        // Every modifier id in the pool, authored order. The Core-side
        // bridge ModifierTable.PickModifiers needs -- Domain cannot see
        // ContentDatabase or ModifierDefinition, the same reason Candidates()
        // exists above for items rather than ItemOfferTable reading
        // ContentDatabase itself.
        private static IReadOnlyList<string> ModifierPool()
        {
            return ContentDatabase.Modifiers
                .Where(m => m != null && !string.IsNullOrEmpty(m.id))
                .Select(m => m.id)
                .ToList();
        }

        // What one character's Favor is worth: what they were AUTHORED with,
        // plus what the reward track has GRANTED them, plus whatever their
        // CURRENTLY EQUIPPED gear grants LIVE right now (Fortunate).
        //
        // Three parts, but only two are stored state: CharacterDefinition.
        // princesFavor is content, rebuilt from characters.json by
        // ContentBuilder and the same for every save; Character.earnedFavor
        // is permanent progress belonging to one profile's one character.
        // The third part is never stored anywhere -- it is read fresh off
        // ContentDatabase.ModifierEffects(character) every time this is
        // called, which already reflects only the character's currently
        // equipped, currently LIVE loadout (ActiveLoadout). Unequip
        // Fortunate and the very next call to this method already sees it
        // gone; nothing has to be reset, decremented or expired.
        //
        // Tolerant of every side being missing, the house style: a character
        // whose definition has gone (content edited under a live save) still
        // contributes what they earned, and a character with nothing
        // equipped still contributes what they were authored with.
        public static int FavorOf(Character character, CharacterDefinition definition)
        {
            int authored = definition == null ? 0 : definition.princesFavor;
            int earned = character == null ? 0 : character.earnedFavor;
            int liveBonus = character == null
                ? 0
                : ContentDatabase.ModifierEffects(character).Best(ModifierEffectType.FortunateFavorBonusFlat);

            int total = authored + earned + liveBonus;
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

        // The offers, each with its own independently rolled plus.
        //
        // `nextIndex` is upper-bound-exclusive and injected, matching the shape
        // both Domain tables already take -- so a caller under test can hand in
        // a seeded stand-in and get the same items every time.
        //
        // `count` defaults to the base three so every existing caller reads as
        // it did. The live call site passes SquadTrack.OfferWidth(), because
        // the reward track widens the offer at level 50.
        public static List<ItemOffer> Roll(EncounterClass encounter, int depthStep, int favor, Func<int, int> nextIndex,
            int count = ItemOfferTable.OfferCount)
        {
            var offers = new List<ItemOffer>();
            if (nextIndex == null) return offers;

            var candidates = Candidates();
            if (candidates.Count == 0) return offers;

            int maxTier = MaxTier;
            int targetTier = RarityTable.RollTier(encounter, depthStep, maxTier, favor, nextIndex);
            var modifierPool = ModifierPool();

            // Tier is rolled ONCE for the offer set and plus/RiftTier/which-
            // modifiers are rolled PER ITEM. Rolling tier per item would
            // quietly widen the spread ItemOfferTable.TierSpread already
            // controls, and make the three offers three separate difficulty
            // statements rather than one; RiftTier is a property of the one
            // copy on the card, exactly like plus, so it belongs at the same
            // granularity as plus rather than tier's.
            foreach (var offer in ItemOfferTable.Choose(candidates, targetTier, maxTier, nextIndex, count))
            {
                int plus = RarityTable.RollPlus(encounter, favor, nextIndex);
                var riftTier = ModifierTable.RollRiftTier(encounter, favor, nextIndex);
                var modifiers = ModifierTable.PickModifiers(modifierPool, (int)riftTier, nextIndex);

                offers.Add(offer.WithPlus(plus).WithModifiers(riftTier, modifiers));
            }

            return offers;
        }
    }
}
