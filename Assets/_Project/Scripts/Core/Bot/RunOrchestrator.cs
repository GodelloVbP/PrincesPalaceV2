using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // THE RULES OF A RUN, IN ONE PLACE, WITH TWO CALLERS.
    //
    // Everything here used to live inside a MonoBehaviour: what a room does on
    // arrival was in MapController.Walk, what a fight is built from and what
    // its end does to the run were in FightBootstrap, the loot roll was in
    // FightController.Input, taking an offer was in ReckoningController, and
    // the relic draft's own roll was in RelicDraftController. That was fine
    // while a screen was the only thing that could play the game.
    //
    // docs/PLAN_BALANCE_BOT.md F2 is why it is not fine any more: a headless
    // bot that plays thousands of runs to report on balance has to obey the
    // same rules, and the alternative to this class is a second copy of them
    // inside the bot -- a copy that measures itself rather than the game the
    // moment either side moves. So the bodies moved DOWN here and the screens
    // became callers; the bot is the second caller.
    //
    // NOTHING WAS REWRITTEN ON THE WAY. The comments that came with these
    // bodies came with them, because most of them record an ordering that once
    // went wrong -- HP written back before the win check, second lives folded
    // on both outcomes, the run persisted on arrival BEFORE the room resolves.
    // They are the reason this extraction is safe to read.
    //
    // Static and stateless, exactly like RunManager and RunLedger, and for the
    // same reason: the SAVE is the state, and a second copy held here would be
    // a second thing that can disagree with the disk.
    public static class RunOrchestrator
    {
        // ---- starting a descent -------------------------------------------------

        // A run begins. The gate's own reasoning (start or resume, and never
        // re-offer a draft that has already happened) stays on the gate; this
        // is the part a bot needs too.
        public static void StartRun(ulong seed) => RunManager.StartRun(seed);

        // Whether the relic draft still stands between here and the map.
        //
        // RESUMING walks straight past it: the relic was chosen when this
        // descent began, and offering again would let a player re-roll it by
        // walking back to the hub.
        public static bool NeedsRelicDraft() =>
            RunManager.HasRun && !RunManager.Run.relicDrafted;

        // ---- the relic draft ----------------------------------------------------

        // What the CURRENT round offers.
        //
        // The round is derived from how many relics the run already holds
        // rather than counted in a field, which is what makes a mid-draft
        // reload safe: relicIds is persisted, so coming back re-derives the
        // same round and -- because the seed is offset by that same count --
        // re-offers the same cards. A counter in the screen would reset to
        // round one and hand out a fresh offer, which is a re-roll by quitting.
        public static IEnumerable<RelicOption> RelicDraftOffer(ulong seed)
        {
            var save = SaveSlotManager.CurrentSave;
            var earned = Achievements.EarnedIds(save);
            var alreadyHeld = RunManager.Run?.relicIds ?? new List<string>();

            var all = ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r => new RelicOption(r.id, r.rarity, r.unlockedBy))
                .ToList();

            // Already-drafted relics are out of the pool. Draft() draws without
            // replacement WITHIN one offer, which was the whole story when
            // there was only ever one offer; across rounds nothing stopped the
            // same relic coming back, and being offered what you are already
            // carrying reads as a bug.
            var available = RelicPool.Available(all, earned)
                .Where(r => !alreadyHeld.Contains(r.Id))
                .ToList();

            // LEVEL 70: THE WHOLE POOL, IN AUTHORED ORDER, NOT A DRAW.
            //
            // "Choose your starting relics (instead of a random draft)" read
            // literally: at this level there is no roll left to make, so there
            // is no seed involved either. ContentDatabase.Relics is ordered
            // content (IOrderedContent), so the order is the one somebody
            // authored rather than whatever Resources.LoadAll returned -- which
            // matters more here than usual, because the player is now scanning a
            // list rather than reacting to three cards.
            if (SquadTrack.HasUnlocked(TrackReward.ChosenStartingRelics))
            {
                return available;
            }

            // Weighted, so a Godlike relic stays a story. The seed is the run's
            // own PLUS the round, so reloading before choosing offers the same
            // three and the second round is not a repeat of the first.
            var rng = new SeededRandom(seed + (ulong)alreadyHeld.Count);
            return RelicPool.DraftWeighted(available, bound => rng.NextInt(0, bound));
        }

        // Writes a drafted relic onto the run. A null id is the "took nothing"
        // answer and still ensures the list exists, which is what the screen's
        // commit did before this moved.
        public static void TakeRelic(string relicId)
        {
            var run = RunManager.Run;
            if (run == null) return;

            run.relicIds ??= new List<string>();

            // Nothing selected is a legal answer. Descending with no relic is
            // worse than descending with one, which is the player's decision to
            // make and not this screen's to refuse.
            if (!string.IsNullOrEmpty(relicId)) run.relicIds.Add(relicId);
        }

        // Whether the track has earned another round. The caller decides what
        // to do when the pool has nothing left to show -- with a pool smaller
        // than the number of rounds there eventually is not one.
        public static bool DraftHasAnotherRound() =>
            (RunManager.Run?.relicIds?.Count ?? 0) < SquadTrack.StartingRelics();

        // Persisted BEFORE the next round is painted, so the round a reload
        // comes back to is the one on screen.
        public static void PersistDraft() => SaveSlotManager.SaveCurrent();

        // Marked drafted either way. Not derivable from the list being empty: a
        // player who declines must not be asked again every time they walk back
        // into the hub.
        public static void FinishDraft()
        {
            var run = RunManager.Run;
            if (run == null) return;

            run.relicDrafted = true;
            SaveSlotManager.SaveCurrent();
        }

        // ---- walking into a room -------------------------------------------------

        // What arriving somewhere means.
        public enum Arrival
        {
            // The map refused the move: not reachable from where the party
            // stands. Nothing happened.
            Refused,

            // A fight room. It is the one room that leads to a SCREEN, and it
            // clears itself on the way out through the settlement below.
            Fight,

            // Anything else resolved on the spot and the room is cleared.
            Resolved,
        }

        public static bool IsFight(RoomType type) =>
            type == RoomType.Fight || type == RoomType.EliteFight || type == RoomType.Boss;

        // What the room does. Byte for byte what MapController.Walk's Arrive
        // did, minus the navigation and the redraw, which are the screen's.
        public static Arrival ArriveAt(DescentNode target)
        {
            if (target == null || !RunManager.MoveTo(target.Id)) return Arrival.Refused;

            // A fight IS a screen, so it is the one room that leads somewhere.
            // It clears itself on the way out, through the reward path.
            //
            // The message is cleared FIRST: the fight scene loads over the map
            // and the map is rebuilt on return, so a stash line left standing
            // would reappear under whatever room came next.
            if (IsFight(target.Type))
            {
                RoomResolver.Reset();
                return Arrival.Fight;
            }

            // Everything else resolves HERE and the map redraws, which is where
            // the next choice lives anyway. This used to be a bare
            // ClearCurrentRoom: treasure paid nothing, rest healed nobody, and
            // the room cleared without saying anything had happened.
            RoomResolver.Resolve(RunManager.Run, target.Type);

            RunManager.ClearCurrentRoom();
            return Arrival.Resolved;
        }

        // ---- building the fight ---------------------------------------------------

        // The real thing: this room, this squad, this run's seed.
        public static FightEncounterAdapter.BuiltFight BuildFight()
        {
            var run = RunManager.Run;

            // Entry is the only non-fight room that can reach this path, and
            // only via a direct scene load. Treating an unknown room as a
            // normal fight beats refusing to build one, for the same reason the
            // no-content case degrades rather than throwing.
            var roomType = RunManager.CurrentNode?.Type ?? RoomType.Fight;

            var roster = RunEncounter.For(SaveSlotManager.CurrentSave, run, roomType);
            if (roster.IsEmpty)
            {
                // An empty party here is a squad wipe that should have ended
                // the run before the map ever offered this room. Saying so is
                // worth more than an empty stage that looks like a render bug.
                Debug.LogWarning(
                    $"[RunOrchestrator] Room {roomType} fielded {roster.PartyIds?.Count ?? 0} party " +
                    $"and {roster.EnemyIds?.Count ?? 0} enemies; the stage stays empty.");
                return null;
            }

            // isBoss/isElite ACTUALLY PASSED, which they were not before.
            // FightSession took its defaults, so IsBossFight was false in every
            // fight the game could reach -- and the settlement gates
            // RecordBossKill on it, so no boss kill was ever recorded and the
            // run settled without paying for any of them. EnemyKit took the
            // same false for isElite, so elite rooms fielded ordinary kits.
            var built = FightEncounterAdapter.Build(
                roster.PartyIds, roster.EnemyIds, roster.Rng,
                isBoss: roster.IsBoss,
                isElite: roster.IsElite,
                relicIds: run.relicIds,
                depthStep: run.step,
                // What they are WEARING, which is the difference between a
                // character built from their save and one built from the
                // content that named them. Without this the roster was right
                // and every one of them fought at base stats -- full plate and
                // nothing swung identically.
                partyCharacters: SaveSlotManager.CurrentSave?.ActiveSquad());

            if (built == null) return null;

            // THE DEPTH RIDES THE SESSION, here and not only in
            // FightBootstrap. The screen set it after calling this (its
            // comment: "the payout is scaled by where the fight HAPPENED"),
            // and the bot did not -- so every bot fight before this line
            // settled at DepthStep 0: VictoryRewards paid unscaled gold/exp
            // and RarityTable.RollTier centred every offer on FloorTier(0).
            // Found by the first batch that traced offer tiers -- step-40
            // offers had the step-1 distribution, which the formula cannot
            // produce. One seam, two callers, so it lives in the seam.
            built.Session.DepthStep = run.step;

            // THE CHARGE GOES IN BEFORE THE FIGHT OPENS, because Domain cannot
            // ask a save what the squad has earned. What comes back out is
            // session.SecondLivesSpent, folded into the run by SettleFight.
            built.Session.SecondLifeCharges = SquadTrack.SecondLivesLeft(run);

            // Damage taken in earlier rooms, carried in. Applied after the
            // build because the adapter constructs from definitions and knows
            // nothing about a descent.
            RunEncounter.ApplyStartingHealth(built.Party, roster.PartyIds, roster.StartingHealth);
            return built;
        }

        // The satchel, from the stash.
        //
        // stockpiledItems is the single live inventory for now -- the same list
        // the character overlay reads -- so a potion bought between runs is a
        // potion available in the next fight, and using one is visible on both
        // screens because there is only one list.
        public static IReadOnlyList<SatchelStack> BuildSatchel()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return new List<SatchelStack>();

            return save.stockpiledItems
                .Where(e => e != null && e.count > 0)
                .Select(e => new { Entry = e, Item = ContentDatabase.GetItem(e.itemId) })
                .Where(x => x.Item != null && x.Item.kind == ItemKind.Consumable)
                .Select(x => new SatchelStack(
                    x.Item.id, x.Item.displayName, x.Entry.count,
                    x.Item.effect == ItemEffect.RestoreMana))
                .ToList();
        }

        // ONE CONSUMABLE OFF THE STASH, which is the save-side half of using
        // one in a fight.
        //
        // FightBootstrap.OnItemUsed does two things in one handler: it issues
        // the session command AND spends the item. Only the first half is
        // Domain's; the second touches the save and so could not travel with
        // FightRunner, which left the bot able to drink the same potion
        // forever -- FightRunner decrements a LOCAL copy of the satchel (so a
        // policy asked twice in one fight sees an honest count), and nothing
        // wrote that back. A batch measuring consumable pressure against an
        // infinite bag is measuring nothing.
        //
        // Deliberately only the spend, not the command: the two callers issue
        // the command differently on purpose (the screen passes the item's real
        // `amount` from content, the bot passes FightAction.ItemAmountProxy,
        // which both clamp to the same top-off), and folding the command in
        // here would force one of them to lie about which item it used.
        public static void SpendConsumable(string itemId)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || string.IsNullOrEmpty(itemId)) return;

            InventoryOps.TryRemove(save.stockpiledItems, itemId);
            SaveSlotManager.SaveCurrent();
        }

        // ---- settling it ------------------------------------------------------------

        // What a finished fight left behind, for whoever has to draw it.
        //
        // Both halves are nullable on purpose and for different reasons: there
        // is no reward unless the fight was WON and paid, and no settlement
        // unless it was LOST, which is the only thing that ends a run.
        public readonly struct FightSettlement
        {
            // What the fight paid, per character. Null on a loss.
            public readonly CombatReward Reward;

            // What the run that just ended paid out and cost. Null on a win --
            // the run is still going.
            public readonly RunSettlement.Result RunEnded;

            public FightSettlement(CombatReward reward, RunSettlement.Result runEnded)
            {
                Reward = reward;
                RunEnded = runEnded;
            }
        }

        // EVERYTHING THE END OF A FIGHT DOES TO THE RUN.
        //
        // Pinned by FightSettlementTests through the real FightBootstrap door
        // before it moved here, because every line below carries an ordering
        // that once went wrong.
        public static FightSettlement SettleFight(FightSession session, bool won)
        {
            if (!RunManager.HasRun) return new FightSettlement(null, null);

            var run = RunManager.Run;

            // FOLDED BEFORE THE WIN CHECK. What a character did in the fight
            // that killed them is part of the run -- dropping it would make the
            // death screen under-report the most dramatic fight in it, which is
            // the one fight the player most wants described.
            RunLedger.Fold(run, session?.Ledger);

            // HP CARRIED FORWARD, also before the win check, and for a related
            // reason: a loss ends the run through EndRun below, and the defeat
            // screen reports on the squad that just died. Writing health back
            // only on a win would leave that screen reading whatever the party
            // had walked IN with.
            //
            // This is the other half of ApplyStartingHealth. Nothing in v2
            // wrote party health back before it, so every room opened at full
            // regardless of what the last one cost -- which also left Rest
            // rooms with nothing to restore even once they resolve again.
            RunEncounter.WriteBackHealth(run, session);

            // AFTER WriteBackHealth, so the half-health a revived character came
            // back on is what carries into the next room -- writing the spend
            // first would be harmless, but writing health after a revive is the
            // whole point and the ordering deserves to be deliberate.
            //
            // Folded on BOTH outcomes. A charge spent in a fight the party then
            // lost anyway is still spent; refunding it would make a second life
            // free whenever it failed to save the run, which is exactly when it
            // is least deserved.
            if (run != null && session != null) run.secondLivesUsed += session.SecondLivesSpent;

            var payout = won ? session?.Payout : null;
            RunLedger.RecordRoom(run, won,
                payout?.Gold ?? 0,
                won ? (payout?.Experience ?? 0) : 0,
                run?.step ?? 0);

            // A boss goes on the run's list the moment it dies. Whether it PAYS
            // is settled at the end of the run against the save's lifetime
            // list, because only that knows whether this was the first time.
            if (won && session != null && session.IsBossFight)
            {
                RunLedger.RecordBossKill(run, BossIdOf(session));
            }

            if (!won)
            {
                // A loss ends the RUN, not just the fight. Anything else would
                // let a player retry the same room until it went their way,
                // which is the whole tension a roguelike is built on.
                //
                // EndRun settles before it discards, and hands back what it
                // paid -- which is the only surviving record of the run by the
                // time the defeat screen draws.
                return new FightSettlement(null, RunManager.EndRun());
            }

            CombatReward reward = null;
            if (payout.HasValue)
            {
                // GOLD to the run, EXPERIENCE to the characters. Two different
                // owners with two different lifetimes: the run's gold is spent
                // inside the run and lost with it, while a level survives.
                RunManager.BankPayout(payout.Value.Gold);
                reward = RewardApplier.Apply(payout.Value, FieldedIds(session));

                // The fight's own counters, carried onto the reward so the
                // Reckoning's tally tab has something to read. Without this the
                // ledger existed, was folded into the run, and was visible only
                // after you died.
                if (session?.Ledger != null) reward.Ledger = session.Ledger;
            }

            RunManager.ClearCurrentRoom();

            // Out of rooms means the LEG ended, not the run. A leg is eight
            // steps and finishes on whatever the curve forces -- an elite at
            // step 8, a boss at 16 -- so ending the run here would stop every
            // descent at the first elite.
            //
            // Where to go NEXT is not decided here: the map screen is where the
            // player chooses, and this only opens the next leg when there is
            // nothing left to choose between.
            if (RunManager.LegIsOver()) RunManager.AdvanceLeg();

            return new FightSettlement(reward, null);
        }

        // Which boss died. The run records the enemy it was sent to kill rather
        // than whatever happened to be standing there, so a boss room with
        // adds cannot pay out twice or pay for the wrong thing.
        private static string BossIdOf(FightSession session)
        {
            string declared = RunManager.Run?.bossEnemyId;
            if (!string.IsNullOrEmpty(declared)) return declared;

            // A boss fight with no declared id is a content gap, not a reason
            // to lose the kill: fall back to the enemy that was actually there.
            return session.Encounter.Enemies
                .Select(session.SourceFor)
                .FirstOrDefault(k => k?.Source != null && k.Source.IsBoss)?.Source.Id;
        }

        // Who actually stood on the stage. A squad member left out of the
        // encounter (at 0 HP when it was built) is downed rather than absent.
        private static IReadOnlyList<string> FieldedIds(FightSession session)
        {
            if (session == null) return new List<string>();

            return session.Encounter.PlayerParty
                .Select(session.KitFor)
                .Where(k => k != null)
                .Select(k => k.Id)
                .ToList();
        }

        // ---- the loot on offer -------------------------------------------------------

        // Rolled against how deep the run is and what class of thing was just
        // killed.
        //
        // THE RANDOMNESS COMES IN FROM OUTSIDE, and that is the whole point of
        // the parameter: the screen hands in UnityEngine.Random because the
        // offer is not part of the fight's simulation and must not shift the
        // beats a replay would produce, while the bot hands in a seeded stream
        // so a batch is reproducible. Domain has always taken it as a Func;
        // this only keeps the choice at the caller.
        public static List<ItemOffer> RollOffers(FightSession session, Func<int, int> nextIndex)
        {
            var encounter = session != null && session.IsEliteFight
                ? EncounterClass.Elite
                : EncounterClass.Normal;

            int depth = session?.DepthStep ?? 0;
            return ItemOfferRoll.Roll(encounter, depth, ItemOfferRoll.CurrentSquadFavor(),
                nextIndex, SquadTrack.OfferWidth());
        }

        // Taking one. The screen keeps the "only once" guard and the repaint;
        // this is the half that touches the save.
        public static void TakeOffer(SaveData save, ItemOffer offer)
        {
            if (save == null) return;

            // Straight to the meta stash, which is the single live bag -- the
            // same one the character overlay and the fight satchel read. The
            // roll's affix slots travel with it: offer.Modifiers is an
            // IReadOnlyList, InventoryOps.Add wants a List<string> to copy
            // from, so ToList() rather than a cast.
            InventoryOps.Add(save.stockpiledItems, offer.ItemId, 1, offer.Plus,
                offer.Modifiers?.ToList(), (int)offer.RiftTier);

            // AUTO-EQUIP INTO AN EMPTY SLOT. A reward picked for a slot nobody
            // is wearing anything in went to the bag and sat there until the
            // player remembered to open the character sheet -- for a piece
            // that fills a hole in the loadout rather than replacing a choice
            // already made, that extra step is friction with no decision
            // behind it. Only fires when the slot is EMPTY: a slot already
            // holding something is a real choice (keep this, or swap it), and
            // that choice stays the player's.
            AutoEquipIntoAnEmptySlot(save, offer);

            SaveSlotManager.SaveCurrent();
        }

        // First squad member (in ActiveSquad order, same order the loadout
        // comparison tooltip already reads) with an empty slot this item
        // fits. Not equippable, or every candidate slot already occupied
        // across the whole squad: does nothing, and the item stays in the
        // bag exactly as it did before this existed.
        private static void AutoEquipIntoAnEmptySlot(SaveData save, ItemOffer offer)
        {
            var itemDef = ContentDatabase.GetItem(offer.ItemId);
            if (itemDef == null || !itemDef.IsEquippable) return;

            foreach (var character in save.ActiveSquad())
            {
                if (character?.equipment == null) continue;
                if (character.equipment.FirstFreeSlotFor(itemDef.equipSlot) == null) continue;

                // modifierIds/riftTier travel through the same way plus does --
                // InventoryOps.TryRemoveAt inside TryEquip keys on the full
                // (itemId, plus, modifierIds, riftTier) stack, so omitting them
                // here would look for the WRONG stack (the plain, unrolled one)
                // and silently fail to find the copy Take() just added.
                //
                // EquipmentOps.Equip carries the "measure max health first,
                // rescale carried health after" pair this call site used to
                // spell out; TakeOffer still owns the SaveCurrent below it.
                if (!EquipmentOps.Equip(save, character, offer.ItemId, itemDef.equipSlot,
                                        itemDef.IsEquippable, plus: offer.Plus,
                                        modifierIds: offer.Modifiers?.ToList(),
                                        riftTier: (int)offer.RiftTier))
                {
                    return;
                }

                return;
            }
        }
    }
}
