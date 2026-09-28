using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace
{
    // WHAT THE CHARACTER SHEET WOULD SAY, ASKED BY A BOT INSTEAD OF A PLAYER.
    //
    // Measuring a bot in starting gear it never re-equips would answer "how
    // deep does GreedyAggressive get on Late" with a question about a
    // level-60 character wearing a level-1 loadout, which is not a
    // configuration the game can produce and not a number worth reading.
    //
    // NO FORMULA IS REIMPLEMENTED HERE, which is the whole discipline of this
    // file. Every number below comes back from the same readers
    // CharacterDossierController fills its rows from:
    //
    //   the swap        ItemDescription.SimulateEquip -- the clone-and-resolve
    //                   the sheet's hover preview and the Reckoning's
    //                   comparison both already run
    //   health/def/spd  ContentDatabase.EffectiveStats
    //   DMG             ContentDatabase.EquippedWeaponPower + EquippedWeapon,
    //                   through WeaponPower.DisplayDamage -- the sheet's own
    //                   "DMG 87 -> 104" line
    //   legality        ContentDatabase.ActiveLoadout(...).IsLive
    //
    // THE ONE TRAP THIS FILE EXISTS TO AVOID: StatBlock.attack is NOT where a
    // weapon's damage lives. ContentDatabase.EffectiveStats zeroes every worn
    // item's attack contribution on purpose (balance redesign D3) and the
    // weapon reaches combat as WeaponPower at the FightEncounterAdapter seam
    // instead. An evaluator that scored `StatDelta.attack` -- the obvious
    // reading, and what ItemComparison hands you -- would price every sword in
    // the game at exactly zero offence and equip armour forever.
    //
    // The PREFERENCE is not here. It arrives as an IRunPolicy's GearWeights,
    // because the archetypes are the variable a balance batch is measuring and
    // a weight vector chosen in Core would be this file quietly becoming a
    // fifth archetype. See GearWeights' header for why it travels down rather
    // than the numbers travelling up.
    public static class GearEvaluator
    {
        // One thing the bot put on, for the trace.
        public sealed class Equipped
        {
            public string CharacterId;
            public string ItemId;
            public string Slot;
            public int Plus;
        }

        // ---- measuring one character ---------------------------------------------

        // The five axes GearWeights prices, read off a character exactly as the
        // sheet reads them.
        private static StatDeltas Measure(Character character)
        {
            if (character == null) return StatDeltas.Zero;

            var stats = ContentDatabase.EffectiveStats(character);

            return new StatDeltas(
                offence: CombatDamageOf(character, stats),
                health: stats.maxHealth,
                // Physical and magical summed -- one axis, see GearWeights.
                defence: stats.physicalDefense + stats.magicalDefense,
                speed: stats.speed,
                manaRegen: stats.manaRegen);
        }

        // THE SHEET'S DMG NUMBER. WeaponPower.DisplayDamage of whatever is live
        // in the main hand, scaled by this character's own ability scores --
        // which is how STR and INT reach offence at all (AbilityDerivation
        // derives nothing from either; the weapon's ScalingProfile reads them
        // at the moment of the swing).
        //
        // Falls back to the character's own authored Attack when the hand is
        // empty, inert, or holds something that is not a weapon -- the SAME
        // null-means-unarmed reading FightEncounterAdapter.ToCombatant takes
        // (`EquippedWeaponPower(character) ?? stats.attack`), rather than
        // scoring an unarmed character at zero damage.
        private static int CombatDamageOf(Character character, StatBlock stats)
        {
            int? power = ContentDatabase.EquippedWeaponPower(character);
            if (!power.HasValue) return stats.attack;

            var weapon = ContentDatabase.EquippedWeapon(character);
            if (weapon == null) return stats.attack;

            return WeaponPower.DisplayDamage(
                power.Value, weapon.scaling, ContentDatabase.EffectiveAbilityScores(character));
        }

        private static StatDeltas Difference(StatDeltas after, StatDeltas before) =>
            new StatDeltas(
                after.Offence - before.Offence,
                after.Health - before.Health,
                after.Defence - before.Defence,
                after.Speed - before.Speed,
                after.ManaRegen - before.ManaRegen);

        // ---- scoring one candidate -------------------------------------------------

        // What wearing `item` in `slot` would move, against what is worn now.
        // Null when the swap is not legal: the item does not fit that slot, or
        // it would be INERT the moment it is worn (its requirements unmet even
        // with everything else this swap settles). An inert piece is not a
        // choice a player has -- the sheet greys it and says why -- so it is
        // not a candidate here either.
        //
        // The cascade is priced for free and deliberately not modelled by hand:
        // displacing a piece something else was leaning on turns that piece
        // inert, EffectiveStats stops counting it, and the loss shows up in the
        // delta. Same for the other direction. This is exactly the property
        // ItemDescription.Compare's own header claims for the sheet.
        public static StatDeltas? DeltaFor(
            Character character, ItemDefinition item, int plus, EquipmentSlot slot)
        {
            if (character?.equipment == null || item == null || !item.IsEquippable) return null;
            if (!EquipmentSlots.Accepts(slot, item.equipSlot)) return null;

            var after = ItemDescription.SimulateEquip(character, item, plus, slot);
            if (after == null) return null;
            if (!ContentDatabase.ActiveLoadout(after).IsLive(slot)) return null;

            return Difference(Measure(after), Measure(character));
        }

        // WHAT ONE OFFER IS WORTH TO THE SQUAD, which is what RunView.
        // OfferScores carries and what ChooseOffer now ranks on.
        //
        // THE BEST MEMBER, not the sum. An offer that transforms one character
        // and does nothing for the other two is a good offer; averaging would
        // report it as a mediocre one, and summing would prefer a mediocre
        // upgrade for everybody over a transformative one for the character who
        // actually fights. ItemOfferRoll.SquadFavor takes the same "highest,
        // never the sum" read of the squad and for the same kind of reason.
        //
        // Scored against the BEST SLOT the piece could go in, since that is
        // where the equip pass would actually put it.
        public static float ScoreOffer(SaveData save, string itemId, int plus, GearWeights weights)
        {
            var item = ContentDatabase.GetItem(itemId);
            if (save == null || item == null || !item.IsEquippable) return 0f;

            float best = 0f;
            bool found = false;

            foreach (var character in save.ActiveSquad())
            {
                if (character?.equipment == null) continue;

                foreach (var slot in EquipmentSlots.All)
                {
                    var delta = DeltaFor(character, item, plus, slot);
                    if (!delta.HasValue) continue;

                    float score = weights.Score(delta.Value);
                    if (!found || score > best)
                    {
                        best = score;
                        found = true;
                    }
                }
            }

            return best;
        }

        // Every offer on the table, index-aligned, ready for RunView.
        public static IReadOnlyList<float> ScoreOffers(
            SaveData save, IReadOnlyList<Domain.Rewards.ItemOffer> offers, GearWeights weights)
        {
            if (offers == null || offers.Count == 0) return Array.Empty<float>();

            var scores = new float[offers.Count];
            for (int i = 0; i < offers.Count; i++)
            {
                scores[i] = ScoreOffer(save, offers[i].ItemId, offers[i].Plus, weights);
            }

            return scores;
        }

        // ---- the equip pass ---------------------------------------------------------

        // WEAR THE BEST THING IN THE BAG, slot by slot, for every fielded
        // character. What a player does between fights, and what the bot did
        // none of before this.
        //
        // SLOT BY SLOT rather than item by item, because that is the shape of
        // the decision on the sheet: eight squares, each with a "what is the
        // best thing I own that goes here" behind it. It also terminates by
        // construction -- eight slots, one decision each, no re-scoring loop
        // that a scoring tie could spin in.
        //
        // NOTHING IS EVER TAKEN OFF. RunManager.EndRun clears both gear and bag
        // at the end of every run (the plan's F5), so an unequip would only
        // ever be work done on the way to a state the settlement discards. The
        // one thing it costs: a slot filled early with something later made
        // inert by a swap elsewhere stays filled. That is the same thing a
        // player who does not tidy up gets, and DeltaFor already refuses to
        // ADD a piece that would be inert.
        //
        // Returns what it put on, for the trace.
        public static List<Equipped> EquipBestGear(SaveData save, IRunPolicy policy, SeededRandom rng)
        {
            var equipped = new List<Equipped>();
            if (save == null || policy == null) return equipped;

            var weights = policy.Gear;

            foreach (var character in save.ActiveSquad())
            {
                if (character?.equipment == null) continue;

                foreach (var slot in EquipmentSlots.All)
                {
                    var pick = ChooseForSlot(save, character, slot, weights, rng);
                    if (pick == null) continue;

                    var item = ContentDatabase.GetItem(pick.itemId);
                    if (item == null) continue;

                    // The entry's whole copy -- InventoryOps keys stacks on
                    // ItemInstance.SameStack, so anything less would look for
                    // the wrong stack and fail to find the very entry that was
                    // just scored. An instance is a snapshot, so the entry
                    // leaving the bag underneath it cannot change it.
                    if (!EquipmentOps.Equip(save, character, pick.Instance, item.equipSlot,
                                            item.IsEquippable, preferredSlot: slot))
                    {
                        continue;
                    }

                    equipped.Add(new Equipped
                    {
                        CharacterId = character.definitionId,
                        ItemId = pick.itemId,
                        Slot = slot.ToString(),
                        Plus = pick.plus,
                    });
                }
            }

            return equipped;
        }

        // The bag entry this archetype would put in `slot`, or null for "leave
        // it as it is".
        //
        // TWO ARCHETYPE SHAPES, and the difference is one line. A RANKING
        // archetype takes the best candidate that actually beats what is worn
        // (a strictly positive delta), ties by rng. An INDIFFERENT one --
        // RandomLegal, whose weight vector ranks nothing, so every candidate
        // scores an identical zero -- draws uniformly from the legal candidates
        // AND from the option of leaving the slot alone. Without that second
        // branch the fuzzer would never change clothes at all, because nothing
        // ever scores above zero for it, and the archetype floor the whole
        // report is measured against would be a naked one.
        private static InventoryEntry ChooseForSlot(
            SaveData save, Character character, EquipmentSlot slot, GearWeights weights, SeededRandom rng)
        {
            var candidates = new List<InventoryEntry>();
            var scores = new List<float>();

            foreach (var entry in save.stockpiledItems)
            {
                if (entry == null || entry.count <= 0) continue;

                var item = ContentDatabase.GetItem(entry.itemId);
                if (item == null || !item.IsEquippable) continue;

                var delta = DeltaFor(character, item, entry.plus, slot);
                if (!delta.HasValue) continue;

                candidates.Add(entry);
                scores.Add(weights.Score(delta.Value));
            }

            if (candidates.Count == 0) return null;

            if (weights.RanksNothing)
            {
                // One extra ticket for "keep wearing what I have", so the draw
                // is over the choices a player has rather than only over the
                // ones that involve changing.
                int draw = rng.NextInt(0, candidates.Count + 1);
                return draw == candidates.Count ? null : candidates[draw];
            }

            var tied = new List<int>();
            float best = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                // STRICTLY BETTER, against a floor of zero rather than against
                // the best candidate: zero IS what is currently worn (every
                // delta is measured against it), so a candidate that ties with
                // the status quo is not an upgrade and swapping to it would be
                // churn the trace would then report as a decision.
                if (scores[i] <= 0f) continue;

                if (tied.Count == 0 || scores[i] > best)
                {
                    best = scores[i];
                    tied.Clear();
                    tied.Add(i);
                }
                else if (Math.Abs(scores[i] - best) < 0.0001f)
                {
                    tied.Add(i);
                }
            }

            if (tied.Count == 0) return null;
            return candidates[tied.Count == 1 ? tied[0] : tied[rng.NextInt(0, tied.Count)]];
        }

        // ---- spending a level-up ------------------------------------------------------

        // WHERE ONE STAT POINT WOULD GO, measured rather than tabled.
        //
        // Each of the six is placed on a throwaway copy and the sheet's numbers
        // re-read, which is the only honest way to ask the question: Strength
        // and Intelligence derive NOTHING through AbilityDerivation and reach
        // damage through the equipped weapon's ScalingProfile instead, so a
        // hardcoded "aggressive spends on Strength" would be right for a
        // greatsword and wrong for a dagger, and would never notice which one
        // the character is holding.
        public static IReadOnlyList<StatOption> StatOptionsFor(Character character)
        {
            var options = new List<StatOption>();
            if (character == null) return options;

            var before = Measure(character);

            foreach (var score in AbilityScores.All)
            {
                var clone = CloneWithOnePointIn(character, score);
                options.Add(new StatOption(
                    (int)score, AbilityScores.ShortName(score), Difference(Measure(clone), before)));
            }

            return options;
        }

        // A copy with one more point in `score`, placed through Character.Invest
        // -- the real door -- rather than by writing investedAbilityScores
        // directly, so a future rule inside Invest is one this simulation
        // obeys too. The copy is given a point to spend first, because Invest
        // refuses when there is nothing unspent and the caller may be asking
        // hypothetically.
        private static Character CloneWithOnePointIn(Character character, AbilityScore score)
        {
            var clone = CloneOf(character);
            clone.unspentStatPoints += 1;
            clone.Invest(score);
            return clone;
        }

        private static Character CloneOf(Character character)
        {
            var clone = UnityEngine.JsonUtility.FromJson<Character>(UnityEngine.JsonUtility.ToJson(character));
            clone.equipment = character.equipment?.Clone() ?? new EquipmentLoadout();
            return clone;
        }

        // ---- valuing a talent -----------------------------------------------------------

        // What kindling `talentId` would move, on the same five axes -- the
        // talent's stat block and ability-score bonus resolved by the same
        // EffectiveStats pass everything else here reads, by unlocking the id
        // on a throwaway copy.
        //
        // A talent whose effect is a COMBAT RULE (an extra attack, an execute
        // threshold, a wool income change) derives nothing measurable and comes
        // back as zero. That is a stated limit, not a bug: it is the same limit
        // GreedyAggressive's damage estimate already has for the eight
        // non-previewable skills, and the alternative is a hand-written table
        // of "which talents are offensive" that would rot the first time
        // talents.json moved.
        public static StatDeltas DeltaForTalent(Character character, string talentId)
        {
            if (character == null || string.IsNullOrEmpty(talentId)) return StatDeltas.Zero;

            var before = Measure(character);

            var clone = CloneOf(character);
            clone.unlockedTalentIds ??= new List<string>();
            if (!clone.unlockedTalentIds.Contains(talentId)) clone.unlockedTalentIds.Add(talentId);

            return Difference(Measure(clone), before);
        }
    }
}
