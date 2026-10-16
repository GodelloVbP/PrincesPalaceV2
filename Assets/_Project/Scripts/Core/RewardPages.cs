using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // A SETTLEMENT AND ITS PAGES, the save-side half (docs/PLAN_ITEMS.md
    // 1.2). Settle rolls the pages once, on RngStreams keyed (step, node,
    // page), and queues them on the run; Take, HoneInstead and Leave resolve
    // them. The rules of what a page shows are Domain's (GearPage,
    // RelicPage); this file turns the save into their inputs and applies
    // their outcomes.
    //
    // DOES NOT PERSIST, like EquipmentOps: the caller saves once after the
    // player's action.
    public static class RewardPages
    {
        // What "hone instead" would do for one card.
        public enum HoneInsteadResult
        {
            Refused,
            Hone,

            // Every shown slot is at MaxPlus: the choice pays the card's
            // sale price.
            Gold,
        }

        public static IReadOnlyList<PendingPage> Pages(RunSnapshot run) => PagesOf(run);

        // The first page with choices left, or null.
        public static PendingPage Current(RunSnapshot run) => PagesOf(run).FirstOrDefault(p => p.ChoicesLeft > 0);

        // Rolls the settlement's pages, gear first, and replaces the run's
        // queue with them. A kind with no chips, or no rewards.json row for
        // its class (normal relic), makes no page. Updates the gear-page
        // counters (legendaryPity, lastPageBestCommon) when a gear page rolls.
        public static IReadOnlyList<PendingPage> Settle(SaveData save, int step, int nodeId, int strength,
            IReadOnlyList<EncounterClass> gearChips, IReadOnlyList<EncounterClass> relicChips,
            IReadOnlyList<string> fieldedIds)
        {
            var run = save?.activeRun;
            if (run == null) return new List<PendingPage>();

            var pages = new List<PendingPage>();
            var fielded = Fielded(save, fieldedIds);

            if (gearChips != null && gearChips.Count > 0)
            {
                var page = RollGearPage(run, step, nodeId, strength, gearChips, fielded, ItemOfferRoll.CurrentSquadFavor());
                if (page != null) pages.Add(page);
            }

            if (relicChips != null && relicChips.Count > 0)
            {
                var page = RollRelicPage(run, step, nodeId, relicChips, fielded);
                if (page != null && page.Cards.Count > 0) pages.Add(page);
            }

            run.pendingPages = pages;
            return pages;
        }

        public static PendingPage RollGearPage(RunSnapshot run, int step, int nodeId, int strength,
            IReadOnlyList<EncounterClass> chips, IReadOnlyList<Character> fielded, int favor)
        {
            var cls = RewardSources.Highest(chips);
            string id = RewardSources.IdFor(cls, RewardPageKind.Gear);
            var source = ContentDatabase.GetRewardSource(id);
            if (source == null || run == null) return null;

            var rng = RngStreams.Open(run.runSeed, source.Stream, step, nodeId, PendingPage.GearPageIndex);
            var request = new GearPageRequest
            {
                Source = source,
                Chips = chips.Count,
                DepthStep = step,
                Favor = favor,
                Strength = strength,
                Members = fielded.Select(MemberOf).ToList(),
                LegendaryPity = run.legendaryPity,
                LastPageBestCommon = run.lastPageBestCommon,
            };

            var roll = GearPage.Roll(request, new ContentGearCatalog(fielded), bound => rng.NextInt(0, bound));
            if (roll.Cards.Count == 0) return null;

            run.legendaryPity = roll.LegendaryPity;
            run.lastPageBestCommon = roll.BestWasCommon;

            var page = NewPage(id, RewardPageKind.Gear, cls, step, nodeId, PendingPage.GearPageIndex, chips.Count);
            foreach (var card in roll.Cards)
            {
                page.Cards.Add(new PendingCard
                {
                    Copy = card.Copy,
                    RecipientId = card.OwnerId,
                    Slot = card.Slot,
                    Rolled = card.Rolled,
                    Inert = card.Inert,
                });
            }

            return page;
        }

        public static PendingPage RollRelicPage(RunSnapshot run, int step, int nodeId,
            IReadOnlyList<EncounterClass> chips, IReadOnlyList<Character> fielded)
        {
            var cls = RewardSources.Highest(chips);
            string id = RewardSources.IdFor(cls, RewardPageKind.Relic);
            var source = ContentDatabase.GetRewardSource(id);
            if (source == null || run == null) return null;

            var fieldedIds = fielded.Select(c => c.definitionId).ToList();
            var held = (run.relicIds ?? new List<string>())
                .Select(r => ContentDatabase.GetRelic(r)?.Data)
                .Where(r => r != null)
                .ToList();

            // Not shown when every fielded member it reaches already gets its
            // mechanic; otherwise those members are named on the card.
            var addsNothingFor = new Dictionary<string, List<string>>();
            var available = new List<RelicOption>();
            foreach (var option in RunOrchestrator.AvailableRelicOptions())
            {
                var relic = ContentDatabase.GetRelic(option.Id)?.Data;
                if (relic == null) continue;

                var reached = fieldedIds.Where(relic.ReachesCharacter).ToList();
                var already = AlreadyGetMechanic(relic.Mechanic, reached, held, fielded);
                if (reached.Count > 0 && already.Count == reached.Count) continue;

                addsNothingFor[option.Id] = already;
                available.Add(option);
            }

            var rng = RngStreams.Open(run.runSeed, source.Stream, step, nodeId, PendingPage.RelicPageIndex);
            var drawn = RelicPage.Roll(source, chips.Count, available, bound => rng.NextInt(0, bound));

            var page = NewPage(id, RewardPageKind.Relic, cls, step, nodeId, PendingPage.RelicPageIndex, chips.Count);
            foreach (var option in drawn)
            {
                page.Cards.Add(new PendingCard
                {
                    RelicId = option.Id,
                    Rarity = option.Rarity,
                    AddsNothingFor = addsNothingFor.TryGetValue(option.Id, out var names) ? names : new List<string>(),
                });
            }

            return page;
        }

        // ---- outcomes ---------------------------------------------------------------

        public static bool CanTake(PendingPage page, int cardIndex) =>
            page != null && page.ChoicesLeft > 0 && cardIndex >= 0 && cardIndex < page.Cards.Count &&
            page.Cards[cardIndex].Outcome == CardOutcome.Open;

        // Gear: equips the copy on its recipient; the displaced copy goes to
        // the bag and is recorded on the card. Relic: joins the run.
        public static bool Take(SaveData save, PendingPage page, int cardIndex)
        {
            var run = save?.activeRun;
            if (run == null || !CanTake(page, cardIndex)) return false;

            var card = page.Cards[cardIndex];
            if (card.IsGear)
            {
                var character = Recipient(save, card);
                var definition = ContentDatabase.DefinitionOf(card.Copy);
                if (character?.equipment == null || definition == null) return false;

                var displaced = character.equipment.GetInstance(card.Slot);
                InventoryOps.Add(save.stockpiledItems, card.Copy, 1);
                if (!EquipmentOps.Equip(MenuContext.Descent, save, character, card.Copy, definition.equipSlot,
                        definition.IsEquippable, card.Slot))
                {
                    InventoryOps.TryRemoveAt(save.stockpiledItems, card.Copy);
                    return false;
                }

                card.Displaced = displaced;
            }
            else
            {
                if (run.relicIds == null) run.relicIds = new List<string>();
                if (!run.relicIds.Contains(card.RelicId)) run.relicIds.Add(card.RelicId);
            }

            card.Outcome = CardOutcome.Taken;
            page.Resolved++;
            return true;
        }

        // Hone instead is not offered on a slot at MaxPlus; when every shown
        // slot is at MaxPlus it pays the card's sale price instead.
        public static HoneInsteadResult HoneInsteadFor(SaveData save, PendingPage page, int cardIndex)
        {
            var run = save?.activeRun;
            if (run == null || page == null || page.Kind != RewardPageKind.Gear || !CanTake(page, cardIndex))
            {
                return HoneInsteadResult.Refused;
            }

            var card = page.Cards[cardIndex];
            if (SlotHoning.CanHone(run.slotHoning, card.RecipientId, card.Slot)) return HoneInsteadResult.Hone;

            bool anyHonable = page.Cards.Any(c => c.IsGear && SlotHoning.CanHone(run.slotHoning, c.RecipientId, c.Slot));
            return anyHonable ? HoneInsteadResult.Refused : HoneInsteadResult.Gold;
        }

        // +1 honing on the card's slot for its recipient, or its sale price;
        // the card is discarded either way.
        public static HoneInsteadResult HoneInstead(SaveData save, PendingPage page, int cardIndex)
        {
            var result = HoneInsteadFor(save, page, cardIndex);
            if (result == HoneInsteadResult.Refused) return result;

            var card = page.Cards[cardIndex];
            if (result == HoneInsteadResult.Hone)
            {
                Hone(save, card.RecipientId, card.Slot);
                card.Outcome = CardOutcome.Honed;
            }
            else
            {
                card.GoldPaid = SalePrice(card.Copy, page.Step);
                RunManager.CreditPayout(card.GoldPaid);
                card.Outcome = CardOutcome.Paid;
            }

            page.Resolved++;
            return result;
        }

        // What leaving a page would pay, per (recipient, slot): honing in the
        // order Leave applies it, and the gold. Pure; Leave consumes it, so
        // the face and the payout cannot disagree.
        public readonly struct LeavePreview
        {
            public readonly IReadOnlyList<LeaveHone> Hones;
            public readonly int Gold;

            public LeavePreview(IReadOnlyList<LeaveHone> hones, int gold)
            {
                Hones = hones;
                Gold = gold;
            }

            // How many +1s leaving puts on one slot.
            public int HonesFor(string recipientId, EquipmentSlot slot) =>
                Hones.Where(h => h.RecipientId == recipientId && h.Slot == slot).Sum(h => h.Count);
        }

        public readonly struct LeaveHone
        {
            public readonly string RecipientId;
            public readonly EquipmentSlot Slot;
            public readonly int Count;

            public LeaveHone(string recipientId, EquipmentSlot slot, int count)
            {
                RecipientId = recipientId;
                Slot = slot;
                Count = count;
            }
        }

        // Gear: each unused choice hones the first shown card's slot that can
        // still be honed, ignoring the card's outcome; with none left, the
        // first card's sale price. Relic: 30% of the cheapest shown relic's
        // price each. A closed or null page pays nothing.
        public static LeavePreview PreviewLeave(SaveData save, PendingPage page)
        {
            var run = save?.activeRun;
            var hones = new List<LeaveHone>();
            if (run == null || page == null || page.Closed) return new LeavePreview(hones, 0);

            // Honing laid on top of the run's as the choices are spent, so a
            // slot that reaches MaxPlus hands the next choice to the next slot.
            var added = new Dictionary<(string, EquipmentSlot), int>();
            int gold = 0;
            for (int i = 0; i < page.ChoicesLeft; i++)
            {
                if (page.Kind == RewardPageKind.Relic)
                {
                    gold += RelicPage.UnusedChoicePayout(page.Cards.Select(c => c.Rarity), page.Step);
                    continue;
                }

                var target = page.Cards.FirstOrDefault(c => c.IsGear &&
                    SlotHoning.PlusOf(run.slotHoning, c.RecipientId, c.Slot) + AddedAt(added, c) < ItemUpgrade.MaxPlus);
                if (target != null)
                {
                    added[(target.RecipientId, target.Slot)] = AddedAt(added, target) + 1;
                    int at = hones.FindIndex(h => h.RecipientId == target.RecipientId && h.Slot == target.Slot);
                    if (at >= 0) hones[at] = new LeaveHone(target.RecipientId, target.Slot, hones[at].Count + 1);
                    else hones.Add(new LeaveHone(target.RecipientId, target.Slot, 1));
                }
                else if (page.Cards.Count > 0)
                {
                    gold += SalePrice(page.Cards[0].Copy, page.Step);
                }
            }

            return new LeavePreview(hones, gold);
        }

        private static int AddedAt(Dictionary<(string, EquipmentSlot), int> added, PendingCard card) =>
            added.TryGetValue((card.RecipientId, card.Slot), out int n) ? n : 0;

        // Closes the page and pays what PreviewLeave says.
        public static int Leave(SaveData save, PendingPage page)
        {
            var run = save?.activeRun;
            if (run == null || page == null || page.Closed) return 0;

            var preview = PreviewLeave(save, page);
            foreach (var hone in preview.Hones)
            {
                for (int i = 0; i < hone.Count; i++)
                {
                    Hone(save, hone.RecipientId, hone.Slot);
                    page.UnusedHones++;
                }
            }

            page.UnusedGold = preview.Gold;
            RunManager.CreditPayout(preview.Gold);
            page.Closed = true;
            return preview.Gold;
        }

        // A gear copy's sale price at this step (ShopPricing.SellPrice of its
        // authored buy price).
        public static int SalePrice(ItemInstance copy, int step) =>
            copy == null ? 0 : ShopPricing.SellPrice(ContentDatabase.AuthoredPriceOf(copy), step);

        // ---- inputs ------------------------------------------------------------------

        public static PageMember MemberOf(Character character)
        {
            var worn = character?.equipment?.EquippedEntries()
                .Select(e => new KeyValuePair<EquipmentSlot, ItemInstance>(e.slot, e.Instance));
            return new PageMember(character?.definitionId, worn);
        }

        // Members who already get this mechanic from a held relic or from a
        // trait on a worn copy.
        private static List<string> AlreadyGetMechanic(EffectKind mechanic, IEnumerable<string> reached,
            IReadOnlyList<ResolvedRelic> held, IReadOnlyList<Character> fielded)
        {
            if (mechanic == EffectKind.None) return new List<string>();
            return reached.Where(id => held.Any(h => h.Mechanic == mechanic && h.ReachesCharacter(id))
                                       || WearsTrait(fielded, id, mechanic)).ToList();
        }

        private static bool WearsTrait(IReadOnlyList<Character> fielded, string characterId, EffectKind mechanic)
        {
            var character = fielded?.FirstOrDefault(c => c != null && c.definitionId == characterId);
            if (character?.equipment == null) return false;

            return character.equipment.EquippedEntries().Any(e => ContentDatabase.TraitRowsOf(e.Instance)
                .Any(t => t.Data.Effects.Length > 0 && t.Data.Effects[0].Kind == mechanic));
        }

        public static void Hone(SaveData save, string characterId, EquipmentSlot slot)
        {
            var character = save.roster?.FirstOrDefault(c => c != null && c.definitionId == characterId);

            // Honing moves max health, and carried health is a fraction of
            // it: measured before, rescaled after, as EquipmentOps does.
            int maxBefore = character == null ? 0 : ContentDatabase.EffectiveStats(character).maxHealth;
            SlotHoning.Hone(save.activeRun.slotHoning, characterId, slot);
            if (character != null) RunEncounter.ScaleCarriedHealth(character, maxBefore);
        }

        private static Character Recipient(SaveData save, PendingCard card) =>
            save.roster?.FirstOrDefault(c => c != null && c.definitionId == card.RecipientId);

        private static List<Character> Fielded(SaveData save, IReadOnlyList<string> ids)
        {
            var roster = save.roster ?? new List<Character>();
            if (ids == null) return save.ActiveSquad();
            return ids.Select(id => roster.FirstOrDefault(c => c != null && c.definitionId == id))
                .Where(c => c != null)
                .ToList();
        }

        private static List<PendingPage> PagesOf(RunSnapshot run)
        {
            if (run == null) return new List<PendingPage>();
            if (run.pendingPages == null) run.pendingPages = new List<PendingPage>();
            return run.pendingPages;
        }

        private static PendingPage NewPage(string id, RewardPageKind kind, EncounterClass cls, int step, int nodeId,
            int pageIndex, int choices) =>
            new PendingPage
            {
                SourceId = id,
                Kind = kind,
                SourceClass = cls,
                Step = step,
                NodeId = nodeId,
                Page = pageIndex,
                Choices = choices,
            };

        // ---- content, for the pipeline -------------------------------------------------

        // IGearCatalog over ContentDatabase for one settlement's fielded
        // members.
        private sealed class ContentGearCatalog : IGearCatalog
        {
            private readonly Dictionary<string, Character> _members;
            private readonly Dictionary<EquipmentSlot, IReadOnlyList<string>> _bases =
                new Dictionary<EquipmentSlot, IReadOnlyList<string>>();
            private IReadOnlyList<string> _weaponAffixes;
            private IReadOnlyList<string> _armourAffixes;
            private IReadOnlyList<string> _weaponTraits;
            private IReadOnlyList<string> _armourTraits;

            public ContentGearCatalog(IEnumerable<Character> members)
            {
                _members = new Dictionary<string, Character>();
                foreach (var m in members)
                {
                    if (m != null && !_members.ContainsKey(m.definitionId)) _members[m.definitionId] = m;
                }
            }

            public IReadOnlyList<string> BasesFor(EquipmentSlot slot)
            {
                var itemSlot = EquipmentSlots.IsWeaponSlot(slot) ? EquipmentSlot.Weapon1 : slot;
                if (!_bases.TryGetValue(itemSlot, out var list))
                {
                    list = ContentDatabase.Offerable.Where(i => i.equipSlot == itemSlot).Select(i => i.id).ToList();
                    _bases[itemSlot] = list;
                }

                return list;
            }

            public bool Fits(string memberId, string baseId, int tier, EquipmentSlot slot)
            {
                if (!_members.TryGetValue(memberId ?? "", out var character)) return true;

                var simulated = ItemDescription.SimulateEquip(character, ItemInstance.Of(baseId, tier), slot);
                return simulated != null && ContentDatabase.ActiveLoadout(simulated).IsLive(slot);
            }

            public IReadOnlyList<string> AffixPool(EquipmentSlot slot)
            {
                BuildPools();
                return EquipmentSlots.IsWeaponSlot(slot) ? _weaponAffixes : _armourAffixes;
            }

            public IReadOnlyList<string> TraitsFor(string memberId, EquipmentSlot slot)
            {
                BuildPools();
                var pool = EquipmentSlots.IsWeaponSlot(slot) ? _weaponTraits : _armourTraits;
                if (!_members.TryGetValue(memberId ?? "", out var character)) return pool;

                var kit = KitEffectsOf(character);
                var gotten = KindsAlreadyGotten(character, slot);
                var usable = new List<string>();
                foreach (string id in pool)
                {
                    var data = ContentDatabase.GetModifier(id)?.Data;
                    if (data == null || data.Effects.Length == 0) continue;
                    if (!TraitRequirements.AllMet(data.Requires, kit)) continue;
                    if (gotten.Contains(data.Effects[0].Kind)) continue;
                    usable.Add(id);
                }

                return usable;
            }

            // The skill effects the member's kit holds right now.
            private static List<SkillEffect> KitEffectsOf(Character character) =>
                ContentDatabase.AvailableSkillsFor(character).Select(s => s.Data.Effect).ToList();

            // Mechanics the member already gets: run relics that reach them,
            // and traits on their other worn copies (the copy this card
            // would replace does not count).
            private static HashSet<EffectKind> KindsAlreadyGotten(Character character, EquipmentSlot replacing)
            {
                var kinds = new HashSet<EffectKind>();
                var run = SaveSlotManager.CurrentSave?.activeRun;
                foreach (string relicId in run?.relicIds ?? new List<string>())
                {
                    var relic = ContentDatabase.GetRelic(relicId)?.Data;
                    if (relic != null && relic.Mechanic != EffectKind.None && relic.ReachesCharacter(character.definitionId))
                    {
                        kinds.Add(relic.Mechanic);
                    }
                }

                foreach (var entry in character.equipment?.EquippedEntries() ?? Enumerable.Empty<EquipmentSlotEntry>())
                {
                    if (entry.slot == replacing) continue;
                    foreach (var trait in ContentDatabase.TraitRowsOf(entry.Instance))
                    {
                        if (trait.Data.Effects.Length > 0) kinds.Add(trait.Data.Effects[0].Kind);
                    }
                }

                return kinds;
            }

            // Affix rows and trait rows, each split by the pool modifiers.json
            // names for it.
            private void BuildPools()
            {
                if (_weaponAffixes != null) return;

                var weaponAffixes = new List<string>();
                var armourAffixes = new List<string>();
                var weaponTraits = new List<string>();
                var armourTraits = new List<string>();
                foreach (var modifier in ContentDatabase.Modifiers)
                {
                    if (modifier == null || string.IsNullOrEmpty(modifier.id)) continue;

                    bool weapon = modifier.Data.Pool == Domain.Combat.AffixPool.Weapon;
                    (modifier.Data.Trait ? (weapon ? weaponTraits : armourTraits) : (weapon ? weaponAffixes : armourAffixes))
                        .Add(modifier.id);
                }

                _weaponAffixes = weaponAffixes;
                _armourAffixes = armourAffixes;
                _weaponTraits = weaponTraits;
                _armourTraits = armourTraits;
            }
        }
    }
}
