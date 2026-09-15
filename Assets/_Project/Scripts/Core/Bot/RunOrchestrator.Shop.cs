using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // THE SHOP'S HALF OF THE RUN'S RULES.
    //
    // Here rather than in a controller for the reason the relic commit moved
    // here first: the bot is the second caller and a second copy of "what
    // buying does" is a second rulebook that measures itself rather than the
    // game (docs/PLAN_SHOP.md §2g).
    //
    // EVERY MUTATION HAS THE SAME THREE-PART SHAPE (§2f):
    //
    //   1. VALIDATE, touching nothing.  Resolve every id, check the purse,
    //      check the slot. Every refusal is decided here.
    //   2. APPLY, unable to fail.       Assignments and list operations on
    //      lists already known to exist.
    //   3. PERSIST, exactly once.       SaveSlotManager.SaveCurrent().
    //
    // Step 2 being incapable of failing is not a style preference. There is
    // no transaction around SaveSlotManager.CurrentSave -- it hands back a
    // cached, LIVE SaveData that every mutation edits in place -- so an
    // exception thrown midway through step 2 leaves that cached object half
    // mutated whether or not anything reached the disk. Deciding everything
    // in step 1 is the only way to get atomicity out of that.
    //
    // GOLD LEAVES FIRST ON A BUY AND ARRIVES LAST ON A SELL, so the failure
    // mode in both directions is the player being briefly poorer than they
    // should be rather than briefly richer. On a sell the removal is the only
    // step that can legitimately answer "no", so its answer gates the credit:
    // a sell that credited first and then failed to remove is a gold printer.
    public static partial class RunOrchestrator
    {
        // ---- the test-only refusal seam ---------------------------------------
        //
        // Returns true to refuse at the END of step 1, which is the boundary
        // the atomicity tests need and the one no production path can reach:
        // it asserts the PRE-state (gold unchanged, nothing sold, bag
        // untouched, nothing written). The other boundary -- applied but not
        // persisted -- needs no seam, because SaveSystem.Save already answers
        // false for an unwritable root (§2f).
        //
        // Null in a build, set and cleared in a test's finally, the same
        // posture SaveSystem.RootOverride and SaveSystem.InMemory already
        // take.
        public static Func<bool> RefuseShopMutationForTest;

        private static bool Refused() => RefuseShopMutationForTest != null && RefuseShopMutationForTest();

        // ---- reading the shelf --------------------------------------------------

        // The stock in front of the party, or nothing when they are not
        // standing in a shop they have not left. Never null.
        public static IReadOnlyList<ShopStockEntry> CurrentShopStock =>
            ShopIsOpen(RunManager.Run) ? RunManager.Run.shopStock : new List<ShopStockEntry>();

        // "There is a shop here and it belongs to this node." shopNodeId is
        // the guard rather than an optimisation: without it a stale list from
        // a previous shop paints the next one.
        //
        // THE REROLL ARRAY IS PART OF "OPEN", added after RerollSection was
        // found indexing it unguarded in step 2 -- after the gold had already
        // left the purse. Every mutation here is validate / apply / persist
        // precisely so that step 2 CANNOT fail; an IndexOutOfRange thrown out
        // of step 2 leaves the run holding a charge for a shelf it never got.
        //
        // Stated in the predicate rather than patched at the one index: the
        // invariant is "an open shop has a reroll counter per section", and
        // saying it here makes it true for every mutator and for the shelf the
        // screen paints, in one place. The read side (RerollsUsed below) has
        // always been guarded; this is what stops the two halves from
        // disagreeing about whether the array can be trusted. The state itself
        // is not reachable today -- Reconcile resizes the array on load,
        // carrying what was there -- so this is the invariant written down,
        // not a live repro.
        private static bool ShopIsOpen(RunSnapshot run) =>
            run != null && run.shopNodeId >= 0 && run.shopNodeId == run.currentNodeId
            && run.shopStock != null && run.shopStock.Count > 0
            && run.shopRerollsUsed != null && run.shopRerollsUsed.Length == ShopStock.SectionCount;

        public static int RerollPriceFor(int section)
        {
            var run = RunManager.Run;
            if (run == null || section < 0 || section >= ShopStock.SectionCount) return ShopPricing.RerollCeiling;

            return ShopPricing.RerollPrice(RerollsUsed(run, section));
        }

        private static int RerollsUsed(RunSnapshot run, int section)
        {
            var used = run.shopRerollsUsed;
            return used != null && section >= 0 && section < used.Length ? used[section] : 0;
        }

        // What one bag stack is worth to buy, which is what its sell price is
        // computed from. Equippables are priced by the shop's own formula;
        // everything else -- potions, the hand-authored one-offs -- keeps its
        // AUTHORED cost, which F6 found to be the one sensible number on
        // ItemDefinition.cost (a health potion at 15 is two thirds of a
        // fight's payout). Unknown content is worth nothing rather than
        // guessed at.
        public static int BuyPriceOf(InventoryEntry entry)
        {
            var definition = entry == null ? null : ContentDatabase.GetItem(entry.itemId);
            if (definition == null) return 0;

            return definition.IsEquippable
                ? ShopPricing.GearPrice(definition.tier, entry.plus, entry.riftTier)
                : definition.cost;
        }

        public static int SellPriceOf(InventoryEntry entry)
        {
            int buy = BuyPriceOf(entry);
            return buy <= 0 ? 0 : ShopPricing.SellPrice(buy);
        }

        // ---- arriving and leaving ------------------------------------------------

        // Roll this node's stock, or find the stock already rolled for it.
        //
        // Called from ArriveAt AFTER MoveTo has persisted, and persists
        // itself, which is the ordering that makes quitting inside a shop
        // unable to reroll it: the shelf is on disk before the player can see
        // it. Re-entering the same node finds the same shelf; a shop cannot
        // be re-entered after leaving, because LeaveShop clears the room.
        //
        // A shelf whose stockVersion is older than this build's is LEFT
        // EXACTLY AS IT WAS ROLLED (§2e) -- reshuffling it would be a free
        // reroll granted by a patch note.
        public static void EnsureShopStock()
        {
            var run = RunManager.Run;
            if (run == null) return;

            if (ShopIsOpen(run)) return;

            run.shopStock = new List<ShopStockEntry>();
            run.shopRerollsUsed = new int[ShopStock.SectionCount];
            run.shopNodeId = run.currentNodeId;
            run.shopStockVersion = ShopStock.StockVersion;

            for (int section = 0; section < ShopStock.SectionCount; section++)
            {
                run.shopStock.AddRange(RollOneSection(run, section));
            }

            SaveSlotManager.SaveCurrent();
        }

        // Leaving is what clears the room -- a shop is the second room that
        // does not clear itself on arrival (Arrival.Shop). The stock goes
        // with it, which is what makes "a run holds exactly one uncleared
        // shop" true and is why shopRerollsUsed can be per node without a
        // per-node dictionary.
        //
        // ClearCurrentRoom persists, so this is still one write.
        public static void LeaveShop()
        {
            var run = RunManager.Run;
            if (run == null) return;

            run.shopStock = new List<ShopStockEntry>();
            run.shopRerollsUsed = new int[ShopStock.SectionCount];
            run.shopNodeId = -1;
            run.shopStockVersion = 0;

            RunManager.ClearCurrentRoom();
        }

        // ---- the mutations ---------------------------------------------------------

        public static ShopResult BuyGear(int index)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (!ShopIsOpen(run) || save == null) return ShopResult.Refused(ShopRefusal.NoShop);

            var entry = EntryAt(run, ShopStock.GearSection, index);
            if (entry == null) return ShopResult.Refused(ShopRefusal.BadIndex);
            if (entry.noOffer || entry.sold) return ShopResult.Refused(ShopRefusal.NothingToBuy);

            var definition = ContentDatabase.GetItem(entry.contentId);
            if (definition == null) return ShopResult.Refused(ShopRefusal.NothingToBuy);
            if (run.gold < entry.price) return ShopResult.Refused(ShopRefusal.NotEnoughGold);
            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY. The quoted price, not a recomputed one.
            int price = entry.price;
            run.gold -= price;
            entry.sold = true;

            // InventoryOps.Add and NOTHING ELSE. A purchase deliberately does
            // NOT auto-equip, unlike TakeOffer: the player already decided,
            // with money, against the other cards, and placing the item for
            // them is a second decision taken on their behalf at the moment
            // they are most likely to be mid-plan (§2c).
            InventoryOps.Add(save.stockpiledItems, entry.contentId, 1, entry.plus,
                new List<string>(entry.modifiers ?? new List<string>()), entry.riftTier);

            // 3. PERSIST.
            return Persisted(-price);
        }

        public static ShopResult BuyRelic(int index)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            if (!ShopIsOpen(run)) return ShopResult.Refused(ShopRefusal.NoShop);

            var entry = EntryAt(run, ShopStock.RelicSection, index);
            if (entry == null) return ShopResult.Refused(ShopRefusal.BadIndex);
            if (entry.noOffer || entry.sold) return ShopResult.Refused(ShopRefusal.NothingToBuy);
            if (ContentDatabase.GetRelic(entry.contentId) == null) return ShopResult.Refused(ShopRefusal.NothingToBuy);
            if (run.gold < entry.price) return ShopResult.Refused(ShopRefusal.NotEnoughGold);
            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY. Straight onto the run's list -- there is no slot cap
            // to fill and none to invent (F8).
            int price = entry.price;
            run.gold -= price;
            entry.sold = true;
            run.relicIds ??= new List<string>();
            run.relicIds.Add(entry.contentId);

            // 3. PERSIST.
            return Persisted(-price);
        }

        // A book purchase names no character and calls neither CanLearn nor
        // LearnSpell (§1g, §2f) -- the player already decided, with money,
        // against the other cards, and naming a recipient here would be a
        // second decision taken on their behalf at the moment they are most
        // likely to be mid-plan, the same reasoning §2c gives for gear never
        // auto-equipping, one step earlier. It just appends to the pool the
        // dossier's assignment panel reads from.
        public static ShopResult BuyBook(int index)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            if (!ShopIsOpen(run)) return ShopResult.Refused(ShopRefusal.NoShop);

            var entry = EntryAt(run, ShopStock.BookSection, index);
            if (entry == null) return ShopResult.Refused(ShopRefusal.BadIndex);
            if (entry.noOffer || entry.sold) return ShopResult.Refused(ShopRefusal.NothingToBuy);
            if (ContentDatabase.GetSkill(entry.contentId) == null) return ShopResult.Refused(ShopRefusal.NothingToBuy);
            if (run.gold < entry.price) return ShopResult.Refused(ShopRefusal.NotEnoughGold);
            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY.
            int price = entry.price;
            run.gold -= price;
            entry.sold = true;
            run.unassignedSpellBooks ??= new List<string>();
            run.unassignedSpellBooks.Add(entry.contentId);

            // 3. PERSIST.
            return Persisted(-price);
        }

        // Sells `quantity` copies of the bag stack at `bagIndex`.
        //
        // BAG ONLY. Worn gear cannot be sold, deliberately: a sell that
        // reaches onto the body needs EquipMove's whole displace rule set
        // inside the shop, and "every method does both halves of the move or
        // neither" exists precisely to stop a half-performed unequip
        // destroying an item (§2c). The path is buy, leave, equip through the
        // dossier -- which displaces the old piece into the bag -- and sell
        // it at the next shop.
        public static ShopResult Sell(int bagIndex, int quantity)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (!ShopIsOpen(run) || save?.stockpiledItems == null) return ShopResult.Refused(ShopRefusal.NoShop);
            if (quantity <= 0) return ShopResult.Refused(ShopRefusal.BadIndex);
            if (bagIndex < 0 || bagIndex >= save.stockpiledItems.Count) return ShopResult.Refused(ShopRefusal.BadIndex);

            var entry = save.stockpiledItems[bagIndex];
            if (entry == null || entry.count < quantity) return ShopResult.Refused(ShopRefusal.NotInBag);

            int unit = SellPriceOf(entry);
            if (unit <= 0) return ShopResult.Refused(ShopRefusal.NotInBag);
            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY. The stack's identity is captured BEFORE the first
            // removal: TryRemoveAt drops the entry out of the list once its
            // count reaches zero, and the object would then be describing a
            // stack that is no longer there.
            string itemId = entry.itemId;
            int plus = entry.plus;
            int riftTier = entry.riftTier;
            var modifiers = new List<string>(entry.modifierIds ?? new List<string>());

            int sold = 0;
            for (int i = 0; i < quantity; i++)
            {
                // The removal GATES THE CREDIT even though step 1 already
                // proved the copies are there. TryRemoveAt is the only call
                // in step 2 that can answer "no", and the direction of that
                // answer is the difference between an accounting slip and a
                // gold printer.
                if (!InventoryOps.TryRemoveAt(save.stockpiledItems, itemId, plus, modifiers, riftTier)) break;
                sold++;
            }

            int paid = unit * sold;

            // THROUGH THE PAYOUT SEAM, not onto run.gold. A sale is gold the
            // run earned, and BankPayout is the one place that records that --
            // `run.gold += paid` here was the second of the two leaks that let
            // "Gold earned" read below "Gold held" (the other was treasure).
            RunManager.BankPayout(paid);

            // 3. PERSIST. BankPayout already wrote, so this is the second write
            // on a sale -- kept because ShopResult's Ok/AppliedNotPersisted
            // distinction is the caller's only signal that the shelf change
            // reached disk, and BankPayout returns nothing to read.
            return Persisted(paid);
        }

        // Rerolls ONE section, at that section's own price and its own stream
        // (§7.1 point 7). Rerolling the gear shelf cannot move the relics
        // beside it, because "which section" is the stream number rather than
        // arithmetic on a coordinate.
        public static ShopResult RerollSection(int section)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            if (!ShopIsOpen(run)) return ShopResult.Refused(ShopRefusal.NoShop);
            if (section < 0 || section >= ShopStock.SectionCount) return ShopResult.Refused(ShopRefusal.BadIndex);

            int price = ShopPricing.RerollPrice(RerollsUsed(run, section));
            if (run.gold < price) return ShopResult.Refused(ShopRefusal.NotEnoughGold);
            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY. The counter moves BEFORE the roll, because it is the
            // roll's third coordinate: rolling first would re-derive the
            // shelf the player just paid to be rid of.
            run.gold -= price;
            run.shopRerollsUsed[section]++;

            var rolled = RollOneSection(run, section);
            run.shopStock.RemoveAll(e => e == null || e.section == section);
            run.shopStock.AddRange(rolled);
            run.shopStock.Sort((a, b) => a.section != b.section ? a.section - b.section : a.index - b.index);

            // 3. PERSIST.
            return Persisted(-price);
        }

        // ---- the shared step 3 -------------------------------------------------------

        // One write per mutation, never one per step -- EquipmentOps' rule
        // followed rather than re-argued. A shop mutation's "done" is the
        // whole mutation.
        //
        // A failed write is APPLIED, NOT PERSISTED rather than an error: the
        // in-memory state is fully mutated, the previous save on disk is
        // untouched, and the divergence is now visible to the caller instead
        // of only to the console.
        private static ShopResult Persisted(int goldDelta)
        {
            return SaveSlotManager.SaveCurrent()
                ? ShopResult.Ok(goldDelta)
                : ShopResult.AppliedNotPersisted(goldDelta);
        }

        // ---- the roll ------------------------------------------------------------------

        private static ShopStockEntry EntryAt(RunSnapshot run, int section, int index)
        {
            if (index < 0 || index >= ShopStock.CountFor(section)) return null;

            return run.shopStock.FirstOrDefault(e => e != null && e.section == section && e.index == index);
        }

        private static uint StreamFor(int section)
        {
            switch (section)
            {
                case ShopStock.BookSection: return RngStreams.ShopBooks;
                case ShopStock.RelicSection: return RngStreams.ShopRelics;
                default: return RngStreams.ShopGear;
            }
        }

        // POSITION IS (step, node, that section's rerolls), the third
        // coordinate unpacked (F9). Same node, same stock, forever -- so
        // quitting is not a free reroll -- and a paid reroll moves exactly
        // one coordinate.
        private static List<ShopStockEntry> RollOneSection(RunSnapshot run, int section)
        {
            var rng = RngStreams.Open(run.runSeed, StreamFor(section), run.step, run.currentNodeId,
                RerollsUsed(run, section));
            Func<int, int> next = bound => rng.NextInt(0, bound);

            if (section == ShopStock.GearSection)
            {
                // EncounterClass.Normal: a shop is not a fight, and rolling it
                // as Elite or Boss would make browsing better than winning
                // (assumption 7).
                var context = ItemOfferRoll.BuildRollContext();
                int favor = ItemOfferRoll.CurrentSquadFavor();

                return ShopStock.RollGear(
                    ItemOfferRoll.Candidates(), run.step, ItemOfferRoll.MaxTier,
                    offer => ItemOfferRoll.RollOne(offer, context, EncounterClass.Normal, favor, next),
                    next);
            }

            if (section == ShopStock.RelicSection)
            {
                return ShopStock.RollRelics(AvailableRelicOptions(), next);
            }

            return ShopStock.RollBooks(AvailableBookOptions(), next);
        }

        // THE BOOK POOL THIS SHOP CAN OFFER: every bookTier > 0 skill, minus
        // any skillId NO fielded character could use -- because they already
        // know it, or because their primary pool refuses spell books at all
        // (§2d's "owned by everyone" rule, widened by plan P6 -- a card
        // nobody could act on is a dead card taking a live card's slot).
        // "Fielded" is the active squad
        // (SaveData.ActiveSquadIds), same definition §3e uses for the
        // dossier's row count. A skill known by SOME but not all fielded
        // characters is still offered -- the shop carries no per-character
        // ownership context (§7.1 point 4 puts those facts on the card
        // itself instead, read from run.learnedSpells/unassignedSpellBooks
        // directly by whatever paints the card).
        // The same pool, reachable from a PlayMode test. Public rather than
        // internal because InternalsVisibleTo names the Editor assembly only,
        // so a PlayMode fixture reaches this or reaches nothing -- and the
        // half of the book rule that lives here ("offered to whoever is
        // buying") is only worth anything if it is pinned beside the half
        // that lives in AvailableSkillsFor. They were allowed to disagree for
        // as long as neither was asserted against the other.
        public static IReadOnlyList<ShopStock.BookCandidate> ShopBookCandidatesForTest() =>
            AvailableBookOptions();

        private static List<ShopStock.BookCandidate> AvailableBookOptions()
        {
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            var squad = save?.ActiveSquadIds() ?? new List<string>();
            var learned = run?.learnedSpells ?? new List<LearnedSpellEntry>();

            // "EVERYONE KNOWS IT" BECAME "NOBODY CAN USE IT" (plan P6, gate
            // 2). The rule §2d states is "a card nobody could act on is a
            // dead card taking a live card's slot", and knowing the book was
            // only ever ONE way to be unable to act on it -- a character
            // whose primary pool refuses books is another, and the shelf has
            // to read them the same way or it stocks a card that cannot be
            // placed anywhere.
            //
            // THE THRESHOLD IS ALL, NOT ANY, and deliberately: one book-less
            // character in the squad must not take a book off the shelf for
            // the two who could read it. That is the same bargain the
            // knows-it half already struck (a skill known by SOME fielded
            // characters is still offered), and the shop card's own fact line
            // is what tells the player who it is for.
            //
            // "KNOWS IT" NOW INCLUDES THE TRACK (progression v2 phase 4).
            // Three book spells are handed over by a reward-track UnlockSkill
            // node -- Cinderfault to Shawn at 20, Frost Flare and Lightning
            // Bolt to Odette at 3 and 10 -- and before this the shelf only
            // asked run.learnedSpells, so a squad who had collected all three
            // could still be sold copies of them. That is the same dead card
            // §2d already refuses for a book somebody bought: nothing on it
            // can be acted on. The two routes are asked together rather than
            // the track route being bolted on afterwards, because the
            // question is one question -- "can this character already cast
            // it" -- and splitting it is how the two answers drift.
            //
            // READ OFF claimedTrackLevel, like every other track read: a node
            // reached but not collected has handed over nothing yet, so the
            // book is still worth selling.
            bool AlreadyKnows(string characterId, string skillId)
            {
                if (learned.Exists(e => e != null && e.characterId == characterId && e.skillId == skillId)) return true;

                var character = SaveSlotManager.CurrentSave?.roster
                    ?.FirstOrDefault(c => c != null && c.definitionId == characterId);
                if (character == null) return false;

                return RewardTracks.For(character).SkillsCollected(character.claimedTrackLevel).Contains(skillId);
            }

            bool NobodyCanUse(string skillId) =>
                squad.Count > 0 && squad.All(id =>
                    !ContentDatabase.CanHoldSpellBooks(id) || AlreadyKnows(id, skillId));

            return ContentDatabase.Skills
                .Where(s => s != null && s.Data.BookTier > 0 && !NobodyCanUse(s.id))
                .Select(s => new ShopStock.BookCandidate(s.id, s.Data.BookTier))
                .ToList();
        }
    }
}
