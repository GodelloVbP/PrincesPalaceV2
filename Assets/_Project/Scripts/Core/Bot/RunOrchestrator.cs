using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;
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
    // Split across files (CODE_STANDARDS §4): this root file owns the
    // class declaration and everything a run has always done,
    // RunOrchestrator.Shop.cs owns the shop's own mutations and
    // RunOrchestrator.Event.cs the event room's -- one topic per file.
    public static partial class RunOrchestrator
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
            var alreadyHeld = RunManager.Run?.relicIds ?? new List<string>();
            var available = AvailableRelicOptions();

            // Weighted, so a Godlike relic stays a story. The seed is the run's
            // own PLUS the round, so reloading before choosing offers the same
            // three and the second round is not a repeat of the first.
            var rng = new SeededRandom(seed + (ulong)alreadyHeld.Count);
            return RelicPool.DraftWeighted(available, bound => rng.NextInt(0, bound));
        }

        // THE RELIC POOL THIS RUN COULD STILL BE OFFERED -- unlocked, gated
        // on a convergence ability where the relic asks for one, and minus
        // whatever the run already holds.
        //
        // Extracted so the draft and the shop's relic shelf ask the same
        // question. Being offered what you are already carrying reads as a
        // bug in both places, and two copies of that filter would be two
        // rulebooks (docs/PLAN_SHOP.md §2d).
        public static List<RelicOption> AvailableRelicOptions()
        {
            var save = SaveSlotManager.CurrentSave;
            var earned = Achievements.EarnedIds(save);
            var alreadyHeld = RunManager.Run?.relicIds ?? new List<string>();

            // Draftable carried through, so RelicPool.Available drops a
            // draftable: false relic from the one pool both the draft and the
            // shop's relic shelf draw from.
            var all = ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r => new RelicOption(r.id, r.Data.Rarity, r.Data.UnlockedBy,
                    r.Data.RequiresConvergenceAbility, r.Data.Draftable))
                .ToList();

            // Mechanic (g): does anybody in the squad actually have a
            // convergence/ultimate ability (a Transform skill) right now?
            // Computed from the ACTIVE SQUAD, not the fielded party --
            // relics are drafted in the hub, before a fight's party is
            // chosen.
            var squad = save?.ActiveSquad() ?? new List<Character>();
            bool hasConvergence = false;
            foreach (var c in squad)
            {
                if (c == null) continue;
                foreach (var s in ContentDatabase.AvailableSkillsFor(c))
                {
                    if (s != null && ConvergenceGate.IsConvergenceEffect(s.Data.Effect))
                    {
                        hasConvergence = true;
                        break;
                    }
                }
                if (hasConvergence) break;
            }

            // Already-drafted relics are out of the pool. Draft() draws without
            // replacement WITHIN one offer, which was the whole story when
            // there was only ever one offer; across rounds nothing stopped the
            // same relic coming back, and being offered what you are already
            // carrying reads as a bug.
            var available = RelicPool.Available(all, earned, hasConvergence)
                .Where(r => !alreadyHeld.Contains(r.Id))
                .ToList();

            return available;
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

        // Whether the draft has another round to offer. The caller decides
        // what to do when the pool has nothing left to show -- with a pool
        // smaller than the number of rounds there eventually is not one.
        //
        // RelicPool.StartingRelicsPerDescent RATHER THAN A REWARD-TRACK READ:
        // nothing on the track escalates this per character, so every
        // descent drafts the same flat count.
        public static bool DraftHasAnotherRound() =>
            (RunManager.Run?.relicIds?.Count ?? 0) < RelicPool.StartingRelicsPerDescent;

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

            // A shop. THE SECOND ROOM THAT LEADS TO A SCREEN, and therefore
            // the second that must not clear itself on arrival -- the stock
            // is rolled and persisted here, and the room clears when the
            // player leaves, through LeaveShop.
            Shop,

            // An event room with an event rolled and persisted. THE THIRD
            // ROOM THAT LEADS TO A SCREEN, so it does not clear itself either:
            // the room clears on LeaveEvent (or on a choice that leaves with
            // nothing to say). An Event node whose pool is empty is not this
            // -- it resolves on the spot and comes back Resolved.
            Event,

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

            // A SHOP RESOLVES INTO A SCREEN, NOT INTO A MESSAGE.
            //
            // The stock is rolled AFTER MoveTo above has already persisted,
            // and is persisted again itself, which is what makes quitting
            // inside a shop unable to reroll it -- the same property
            // RoomResolver's treasure stream exists for, stated as an
            // ordering rather than left to luck. No ClearCurrentRoom: the
            // room is still live until LeaveShop says otherwise.
            if (target.Type == RoomType.Shop)
            {
                RoomResolver.Reset();
                EnsureShopStock();
                return Arrival.Shop;
            }

            // AN EVENT RESOLVES INTO A SCREEN WHEN THERE IS ONE TO SHOW.
            //
            // Same ordering as the shop: rolled after MoveTo has persisted,
            // and persisted again by EnsureEvent before this returns. When
            // the floor's pool is empty (every event seen, or none authored
            // for this floor) there is nothing to show, and the node falls
            // through to RoomResolver.Resolve below -- the old "nothing built
            // here" line and a cleared room, never a throw (contract 3).
            if (target.Type == RoomType.Event)
            {
                RoomResolver.Reset();
                if (EnsureEvent()) return Arrival.Event;
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

        // THE ONE SEAM FOR "WHICH FIGHT" (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
        // 3.1). The open event's pending fight when it names one, else the
        // room under the party. BuildFight builds from it, FightBootstrap
        // takes the class from it and SettleFight branches on it once -- so
        // the screen and the bot, which both go through those three, get
        // event fights without knowing they exist.
        public static EncounterRequest CurrentEncounterRequest()
        {
            var run = RunManager.Run;

            var fight = PendingEventFightOn(run);
            if (fight != null)
            {
                return EncounterRequest.ForEventFight(RunManager.CurrentNode?.Type ?? RoomType.Event, run.eventId, fight);
            }

            // Entry is the only non-fight room that can reach this path, and
            // only via a direct scene load. Treating an unknown room as a
            // normal fight beats refusing to build one, for the same reason the
            // no-content case degrades rather than throwing.
            return EncounterRequest.ForRoom(RunManager.CurrentNode?.Type ?? RoomType.Fight);
        }

        // The real thing: this room (or this event's fight), this squad, this
        // run's seed.
        public static FightEncounterAdapter.BuiltFight BuildFight()
        {
            var run = RunManager.Run;
            var request = CurrentEncounterRequest();
            var roomType = request.RoomType;

            var roster = RunEncounter.For(SaveSlotManager.CurrentSave, run, request);
            if (roster.IsEmpty)
            {
                // An empty party here is a squad wipe that should have ended
                // the run before the map ever offered this room. Saying so is
                // worth more than an empty stage that looks like a render bug.
                //
                // AN EVENT FIGHT IS DROPPED, NOT STAGED. Its request would
                // otherwise outlive the empty stage: SettleFight never runs,
                // pendingFight stays set, every choice is refused FightPending,
                // and a reload brings the same nothing back. Cleared and
                // persisted here, the event is back on the page that launched
                // it (the fight never moved it) and FightBootstrap returns to
                // the map, which reopens the event panel.
                if (request.IsEventFight && run != null)
                {
                    Debug.LogWarning(
                        $"[RunOrchestrator] event fight '{request.EventFight.Id}' of event '{run.eventId}' fielded " +
                        $"{roster.PartyIds?.Count ?? 0} party and {roster.EnemyIds?.Count ?? 0} enemies; dropped, " +
                        "and the event is back on its page.");
                    run.pendingFight = "";
                    SaveSlotManager.SaveCurrent();
                    return null;
                }

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
                partyCharacters: SaveSlotManager.CurrentSave?.ActiveSquad(),
                // The round limit goes in AT the build, not onto the session
                // after it: an enemy's attack rides a different depth rate in
                // a round-limited fight (DifficultyCurve.ScaleEnemyAttack),
                // and the build sets Session.RoundLimit from the same value.
                roundLimit: request.RoundLimit);

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
            //
            // None in a `wake` event fight: its loss is survivable already
            // (EncounterRequest.AllowsSecondLives).
            built.Session.SecondLifeCharges = request.AllowsSecondLives ? SquadTrack.SecondLivesLeft(run) : 0;

            // Damage taken in earlier rooms, carried in. Applied after the
            // build because the adapter constructs from definitions and knows
            // nothing about a descent.
            RunEncounter.ApplyStartingHealth(built, roster.StartingHealth);
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
                    x.Item.effect == ItemEffect.RestoreMana, x.Entry.Instance))
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
        //
        // SPENDS THE STACK THAT WAS PRESSED, named by its whole ItemInstance.
        // This took an id and removed the lowest-plus copy of it, which was
        // right while every potion of one id was interchangeable and stopped
        // being right with caravan lots: a genuine potion and a fake one share
        // an id, and spending by id would let either stand in for the other
        // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.3).
        public static void SpendConsumable(ItemInstance used)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null || used == null || string.IsNullOrEmpty(used.ItemId)) return;

            InventoryOps.TryRemoveAt(save.stockpiledItems, used);
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

            // Lines the end of this fight has to SAY that are not a payout:
            // today, caravan fakes that fell apart ("<item> falls apart.",
            // then "No refunds."). Never null. Also copied onto Reward.Notices
            // when there is a Reward, so the Reckoning shows them; with no
            // Reward the screen puts them in the end log above Continue.
            public readonly IReadOnlyList<string> Notices;

            public FightSettlement(CombatReward reward, RunSettlement.Result runEnded,
                IReadOnlyList<string> notices = null)
            {
                Reward = reward;
                RunEnded = runEnded;
                Notices = notices ?? new List<string>();
            }

            public FightSettlement WithNotices(IReadOnlyList<string> notices)
            {
                if (notices == null || notices.Count == 0) return this;
                if (Reward != null) Reward.Notices.AddRange(notices);
                return new FightSettlement(Reward, RunEnded, notices);
            }
        }

        // EVERYTHING THE END OF A FIGHT DOES TO THE RUN.
        //
        // Pinned by FightSettlementTests through the real FightBootstrap door
        // before it moved here, because every line below carries an ordering
        // that once went wrong.
        //
        // AN EVENT FIGHT BRANCHES ONCE, below the part both share
        // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.2, 3.1): the ledger fold,
        // the HP write-back, second lives and the Amassing Star bank are
        // identical, and then SettleEventFight replaces "pay, clear the room,
        // advance the leg" with the event's own rules. A room fight reads the
        // request as ForRoom and runs every line it always ran, in the order
        // it always ran them.
        //
        // ONE WRITE. The lines below persist in several places (BankPayout,
        // RewardApplier.Apply, ClearCurrentRoom, AdvanceLeg, EndRun, the
        // event branch's own), and each of those writes used to land as it
        // came: a crash after the first left a file with the gold banked or
        // the fakes worn but the room uncleared, and the refight replayed
        // the ledger fold, the Amassing Star bank and the wear on top of it.
        // WritesDeferred folds them into one write at the end, so the file
        // holds the fight before settlement or all of it.
        public static FightSettlement SettleFight(FightSession session, bool won)
        {
            using (SaveSlotManager.WritesDeferred()) return SettleFightInOneWrite(session, won);
        }

        private static FightSettlement SettleFightInOneWrite(FightSession session, bool won)
        {
            if (!RunManager.HasRun) return new FightSettlement(null, null);

            var run = RunManager.Run;
            var request = CurrentEncounterRequest();

            // FOLDED BEFORE THE WIN CHECK. What a character did in the fight
            // that killed them is part of the run -- dropping it would make the
            // death screen under-report the most dramatic fight in it, which is
            // the one fight the player most wants described.
            // THE PARTY, AND ONLY THE PARTY. A CombatLedger carries both sides
            // of the fight, so the ids are worked out here the same way
            // WriteBackHealth below works them out -- the fielded party through
            // KitFor -- rather than by asking content whether an id names a
            // character. Content cannot answer for a test fixture's party, and
            // an id-shaped question that content gets wrong silently drops a
            // real character's whole run. FieldedIds is that same list, already
            // named, and reusing it is what keeps the fold and the write-back
            // from ever disagreeing about who was in the fight.
            RunLedger.Fold(run, session?.Ledger, FieldedIds(session));

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

            // A `pays: false` event fight pays nothing: no gold, no exp, no
            // spell drop, and so no Reckoning.
            var payout = won && request.Pays ? session?.Payout : null;

            // GOLD IS NOT PASSED HERE any more. The fight's gold is banked
            // below through RunManager.BankPayout, which is now the one place
            // a run records having earned anything -- so handing it to the
            // ledger too would count the same coin twice.
            //
            // An event fight clears no room (the event's Leave does), so it
            // never counts toward roomsCleared; its exp and depth still do.
            RunLedger.RecordRoom(run, won && !request.IsEventFight,
                won ? (payout?.Experience ?? 0) : 0,
                run?.step ?? 0);

            // A boss goes on the run's list the moment it dies. Whether it PAYS
            // is settled at the end of the run against the save's lifetime
            // list, because only that knows whether this was the first time.
            if (won && session != null && session.IsBossFight)
            {
                RunLedger.RecordBossKill(run, BossIdOf(session));
            }

            // Mechanic (d) / Amassing Star: whatever the fight banked stays
            // banked whether the run continues or ends here -- a kill that
            // happened is not undone by the death that followed it, the
            // same "folded on both outcomes" reasoning secondLivesUsed just
            // above already uses.
            if (session != null && run != null)
            {
                run.bonusDamagePercent += session.BonusDamagePercentEarned;
            }

            if (!won && request.EndsRunOnLoss)
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

            // CARAVAN FAKES WEAR HERE: past the run-loss return (a run that
            // ended takes its gear with it, so there is nothing to count down),
            // before either settlement branch, so a room fight and an event
            // fight count a completed fight by the same line. See FakeWear.
            var notices = WearFakes(session);

            if (request.IsEventFight) return SettleEventFight(session, won, request, payout).WithNotices(notices);

            CombatReward reward = null;
            if (payout.HasValue) reward = PayOut(session, run, payout.Value, participants: null);

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

            return new FightSettlement(reward, null).WithNotices(notices);
        }

        // One completed fight on every FIELDED member's worn fakes (FakeWear).
        // Returns the lines to show, empty when nothing broke.
        //
        // Fielded is FieldedIds -- who stood on the stage -- so a benched
        // member's fake does not count, and neither does a fake in the bag,
        // which is on nobody. A broken piece leaves the body the way an
        // unequip does, carried health rescaled against the max it took with
        // it (ScaleCarriedHealth), and goes nowhere: no refund to the bag.
        //
        // WRITES NOTHING. Every branch after it persists -- ClearCurrentRoom
        // on a room fight, PayOut or SettleEventFight's own write on an event
        // fight -- and SettleFight folds those into its one write, so the
        // wear lands on disk together with the payout and the cleared room.
        // It used to save here, mid-settlement, which put a half-settled run
        // on disk ahead of everything else.
        private static List<string> WearFakes(FightSession session)
        {
            var lines = new List<string>();
            var save = SaveSlotManager.CurrentSave;
            if (save?.roster == null || session == null) return lines;

            foreach (var id in FieldedIds(session).Distinct())
            {
                var character = save.roster.FirstOrDefault(c => c != null && c.definitionId == id);
                if (character?.equipment == null) continue;
                if (!character.equipment.slots.Any(e => e?.provenance != null && e.provenance.fake
                                                        && !string.IsNullOrEmpty(e.itemId))) continue;

                int maxBefore = ContentDatabase.EffectiveStats(character).maxHealth;
                var broken = FakeWear.WearOneFight(character.equipment);
                if (broken.Count == 0) continue;

                RunEncounter.ScaleCarriedHealth(character, maxBefore);
                foreach (var piece in broken)
                {
                    lines.Add(FakeWear.BreakLine(ContentDatabase.GetItem(piece.ItemId)?.displayName ?? piece.ItemId));
                }
            }

            if (lines.Count > 0) lines.Add(FakeWear.NoRefundsLine);
            return lines;
        }

        // WHAT A WON, PAID FIGHT GIVES: gold banked, one spell-drop roll, and
        // the experience applied, which persists (RewardApplier.Apply) -- so
        // this is also the write for anything the settlement already moved in
        // memory. Extracted unchanged from SettleFight so a paying event fight
        // pays by the same lines. `participants` narrows who the experience
        // reaches (an event fight's party override); null is the whole squad,
        // a room's rule.
        private static CombatReward PayOut(FightSession session, RunSnapshot run, VictoryRewards.Payout payout,
            IReadOnlyCollection<string> participants)
        {
            // GOLD to the run, EXPERIENCE to the characters. Two different
            // owners with two different lifetimes: the run's gold is spent
            // inside the run and lost with it, while a level survives.
            RunManager.BankPayout(payout.Gold);

            // ONE ROLL, THIS FIGHT (docs/PLAN_SHOP.md §1e). Keyed to
            // (step, node) so quitting mid-reward and returning does not
            // reroll it -- the same property Treasure and the shop
            // streams have, for the same reason. Folded into
            // run.unassignedSpellBooks BEFORE RewardApplier.Apply below,
            // so the one SaveCurrent() that call already makes is the
            // save this rides too, rather than a second write.
            var bookIds = ContentDatabase.Skills
                .Where(s => s != null && s.Data.BookTier > 0)
                .Select(s => s.id)
                .ToList();
            var spellRng = RngStreams.Open(run.runSeed, RngStreams.SpellDrop, run.step, run.currentNodeId);
            string droppedSpell = VictoryRewards.RollSpellDrop(bookIds,
                session != null && session.IsEliteFight, session != null && session.IsBossFight, spellRng);
            if (droppedSpell != null)
            {
                run.unassignedSpellBooks ??= new List<string>();
                run.unassignedSpellBooks.Add(droppedSpell);
            }

            var reward = RewardApplier.Apply(payout, FieldedIds(session), participants);

            // The fight's own counters, carried onto the reward so the
            // Reckoning's tally tab has something to read. Without this the
            // ledger existed, was folded into the run, and was visible only
            // after you died.
            if (session?.Ledger != null) reward.Ledger = session.Ledger;
            return reward;
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
            // Guarded down to the party itself, not just the session: the
            // ledger fold above runs before the win check and so before
            // anything else has had a reason to touch the encounter.
            if (session?.Encounter?.PlayerParty == null) return new List<string>();

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
            // BOSS CHECKED FIRST -- via FightSession.EncounterClass, the one
            // ranking every caller now shares. This used to read IsEliteFight
            // only, inline, so a boss kill -- which sets IsBossFight, not
            // IsEliteFight -- fell through to EncounterClass.Normal every
            // time: RarityTable's TierFloorFor(Boss)=3 guarantee, and
            // LootLadder's wider Boss step chance, never fired for the one
            // fight class they exist for. Found from a floor-3 boss paying
            // out a tier-1/+1 item, which the Normal band produces routinely
            // and the Boss floor forbids outright.
            var encounter = session?.EncounterClass ?? EncounterClass.Normal;

            int depth = session?.DepthStep ?? 0;
            return ItemOfferRoll.Roll(encounter, depth, ItemOfferRoll.CurrentSquadFavor(), nextIndex);
        }

        // Taking one. The screen keeps the "only once" guard and the repaint;
        // this is the half that touches the save.
        public static void TakeOffer(SaveData save, ItemOffer offer)
        {
            if (save == null) return;

            // Straight to the meta stash, which is the single live bag -- the
            // same one the character overlay and the fight satchel read. The
            // whole copy travels as one ItemInstance, roll included.
            InventoryOps.Add(save.stockpiledItems, offer.Instance, 1);

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

                // The SAME instance TakeOffer just added -- InventoryOps keys
                // stacks on ItemInstance.SameStack, so anything less than the
                // whole copy would look for the WRONG stack (the plain,
                // unrolled one) and silently fail to find it.
                //
                // EquipmentOps.Equip carries the "measure max health first,
                // rescale carried health after" pair this call site used to
                // spell out; TakeOffer still owns the SaveCurrent below it.
                if (!EquipmentOps.Equip(save, character, offer.Instance, itemDef.equipSlot,
                                        itemDef.IsEquippable))
                {
                    return;
                }

                return;
            }
        }
    }
}
