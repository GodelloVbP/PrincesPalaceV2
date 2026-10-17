using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Events;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
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
        // The ceiling the tier roll clamps against: the highest tier any copy
        // reaches (docs/PLAN_ITEMS.md 1.6), not the art ladder of a base.
        public const int MaxTier = ItemInstance.TierCap;

        // WHAT THE GAME OFFERS AS A REWARD, and that is not the same set as
        // "everything wearable".
        //
        // A "choose one of three" that can offer a health potion is not a
        // choice, it is a tax on the one player who reads carefully. Potions
        // come from the shop and from drops; the fight-reward slot is where
        // gear comes from. That much was always here -- what was missing is
        // the other half of the same argument, which ContentDatabase.Offerable
        // has stated for months: the hand-authored one-offs in items.json
        // "have their own routes in and would otherwise turn up as a 'reward'
        // the player already owns six of". The filter here read IsEquippable,
        // so all six starting-kit items sat in the floor-1 band and the first
        // fight of a run could hand you the helm on your own head.
        //
        // Offerable's predicate is "was this generated with a tier", not the
        // kind, because the offer screen ranks what it shows by run depth and
        // an item with no tier cannot be placed on that axis at all. ASKED
        // RATHER THAN RESTATED: this is the only reward-side caller, the
        // shop's gear shelf and the Reckoning both come through here, and a
        // second copy of the predicate is how the two drifted apart in the
        // first place.
        public static IReadOnlyList<ItemOffer> Candidates()
        {
            var candidates = new List<ItemOffer>();
            foreach (var item in ContentDatabase.Offerable)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                // A base offers one candidate per tier, base by base and tier
                // ascending.
                for (int tier = 0; tier <= MaxTier; tier++)
                {
                    candidates.Add(new ItemOffer(item.id, tier));
                }
            }

            return candidates;
        }

        // What one character's Favor is worth: what they were AUTHORED with
        // (characters.json), the same for every save. Nothing worn adds to
        // it. Tolerant of a missing definition (content edited under a live
        // save), which reads as zero.
        public static int FavorOf(CharacterDefinition definition)
        {
            int authored = definition == null ? 0 : definition.Data.PrincesFavor;
            return authored < 0 ? 0 : authored;
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
        // Takes the per-member totals, so that this rule and FavorOf's rule
        // above are two separate facts in two separate functions: "where
        // does a member's Favor come from" and "how does a squad combine
        // it" can change independently.
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
        //
        // PLUS THE RUN'S EVENT FAVOR, added AFTER the squad max and never
        // inside it: an event's "+10 Prince's favor" is a gift to the run,
        // not to a member, so it must not change which member is the best
        // and must not vanish when that member is benched
        // (docs/PLAN_PETTING_ZOO.md, Fawns). SquadFavor itself stays the
        // pure max-not-sum rule.
        public static int CurrentSquadFavor()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return 0;

            int squad = SquadFavor(save.ActiveSquad()
                .Select(c => FavorOf(ContentDatabase.Characters
                    .FirstOrDefault(d => d != null && d.id == c.definitionId))));

            var run = save.activeRun;
            int fromEvents = run != null && run.hasRun
                ? EventBuffs.ActiveAmount(run.eventBuffs, EventBuffs.PrincesFavor, run.legStartStep)
                : 0;

            return squad + fromEvents;
        }
    }
}
