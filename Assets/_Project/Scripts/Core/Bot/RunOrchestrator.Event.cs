using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // THE EVENT ROOM'S HALF OF THE RUN'S RULES (plan: event rooms, phase 2).
    //
    // Here and not in a controller for the shop's reason: the bot is the
    // second caller, and a second copy of "what picking a choice does" would
    // be a second rulebook. The panel (phase 3) paints CurrentEvent() and
    // calls ChooseEventOption / LeaveEvent; the bot does the same.
    //
    // THE LIFECYCLE, and it is the shop's:
    //
    //   ArriveAt(Event node) -> EnsureEvent  rolls one event from this floor's
    //                                         pool and PERSISTS the pick before
    //                                         anything is shown. The room is
    //                                         NOT cleared.
    //   ChooseEventOption(i, page) (0..n)    validate / apply / persist once;
    //                                         a stale page is refused.
    //   LeaveEvent                           closes the event and clears the
    //                                         room -- the only thing that does,
    //                                         exactly as LeaveShop.
    //
    // AN EVENT FIGHT (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.2) sits inside
    // that lifecycle without changing it. A pick whose outcome carries a
    // `fight` effect persists run.pendingFight and leaves the event open on
    // the page that launched it; the Fight screen (or the bot) builds that
    // fight through CurrentEncounterRequest; SettleFight hands the ending to
    // SettleEventFight below, which applies the fight's onDefeated /
    // onSurvived / onFell outcome through the same ApplyEffect a pick uses,
    // clears the request and writes once. The room is still cleared only by
    // the event's own Leave.
    //
    // A RETURNING EVENT (`mayReturn`, plan 1.1) is not marked seen when it
    // opens; a `finish` effect marks it, wherever it applies.
    //
    // An empty pool is not an event: EnsureEvent answers false and ArriveAt
    // falls through to RoomResolver.Resolve, which says the old "nothing
    // built here" line and clears the room (contract 3).
    //
    // CHOOSING HAS THE SHOP'S THREE-PART SHAPE (RunOrchestrator.Shop.cs):
    // every refusal is decided before anything moves, then the effects apply
    // in memory, then ONE write. The effects go through the game's own rules
    // with their writes split off (RunManager.CreditPayout,
    // RewardApplier.ApplyUnsaved, RunEncounter.Heal/HurtPartyByPercent), which
    // is how "one save per choice" and "one home per rule" hold together.
    public static partial class RunOrchestrator
    {
        // ---- reading -----------------------------------------------------------------

        // An event is open when the run names one AND the party is standing
        // on the node it was opened at. eventId is the discriminator, never
        // eventNodeId (contract 15).
        public static bool EventIsOpen => EventIsOpenOn(RunManager.Run);

        private static bool EventIsOpenOn(RunSnapshot run) =>
            run != null && run.hasRun
            && !string.IsNullOrEmpty(run.eventId)
            && run.eventNodeId >= 0 && run.eventNodeId == run.currentNodeId
            && FindEvent(run.eventId) != null;

        // Whether the open event has a fight waiting to be fought. The event
        // panel launches the Fight screen on this; the bot plays the fight.
        public static bool EventFightPending => PendingEventFightOn(RunManager.Run) != null;

        // The open event's pending fight, or null: no event open, no request,
        // or a request naming a fight its event no longer has (content
        // changed under a save -- Reconcile drops that one too).
        private static ResolvedEventFight PendingEventFightOn(RunSnapshot run)
        {
            if (!EventIsOpenOn(run) || string.IsNullOrEmpty(run.pendingFight)) return null;
            return FindEvent(run.eventId)?.FightById(run.pendingFight);
        }

        // The open event as the panel paints it, or null when none is open.
        // Every choice is listed with its authored index, Visible/Enabled and
        // lock caption -- the panel shows Visible ones and greys the rest.
        public static EventView CurrentEvent()
        {
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (!EventIsOpenOn(run) || save == null) return null;

            var definition = FindEvent(run.eventId);
            string effectsLine = EventEffectSummary.Describe(run.eventResultEffects, ItemDisplayName);

            if (string.IsNullOrEmpty(run.eventPageId))
            {
                // Concluded: the last choice left, with something to say.
                // The last page's art and title would be a guess; the result
                // text is what the player is reading, so the header is empty.
                // An event with lines anywhere concludes on the stage (D4),
                // drawn over the event's own backdrop when no page is held.
                return new EventView(run.eventId, "", "", "", "", true,
                    run.eventResult, effectsLine, new List<EventChoiceView>(),
                    definition.BackdropKey, null, definition.HasAnyLines);
            }

            var page = definition.PageById(run.eventPageId);
            if (page == null) return null;

            var context = new RunEventContext(save, run);
            var choices = new List<EventChoiceView>();
            for (int i = 0; i < (page.Choices?.Length ?? 0); i++)
            {
                var choice = page.Choices[i];
                choices.Add(new EventChoiceView(i, choice?.Text, EventChoiceGate.Evaluate(choice, context)));
            }

            // The dialogue stage's half (docs/PLAN_DIALOGUE_STAGE.md D2): the
            // page's backdrop and its lines joined to their speakers.
            var lines = EventLineView.ListFor(page, id => ContentDatabase.GetCharacter(id)?.Data, definition.SpeakerById);

            return new EventView(run.eventId, page.Id, page.ArtKey, page.Title, page.Body, false,
                run.eventResult, effectsLine, choices, page.BackdropKey, lines, definition.HasAnyLines);
        }

        // By id, over the authored catalogue. Null for an id content no longer
        // has -- SaveData.Reconcile closes such an event rather than trust it.
        internal static ResolvedEventDefinition FindEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;

            foreach (var definition in ContentDatabase.Events)
            {
                if (definition != null && definition.Data != null && definition.Data.Id == eventId)
                {
                    return definition.Data;
                }
            }

            return null;
        }

        // ---- arriving ------------------------------------------------------------------

        // Roll this node's event, or find the one already open on it. Returns
        // whether an event is now open.
        //
        // Called from ArriveAt AFTER MoveTo has persisted, and persists the
        // pick itself BEFORE returning, so the event is on disk before the
        // panel can show it: quitting inside an event cannot reroll it, the
        // same property EnsureShopStock gives the shelf.
        //
        // The roll's stream is RngStreams.Event at (step, node) -- its own
        // number, never Treasure's (contract 3) -- and the pool is this
        // floor's, minus every event already seen this run, minus any whose
        // event-level requirements fail. EventRoll owns those rules.
        public static bool EnsureEvent()
        {
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (run == null || save == null) return false;

            if (EventIsOpenOn(run)) return true;

            var pool = ContentDatabase.Events
                .Where(e => e != null && e.Data != null)
                .Select(e => e.Data)
                .ToList();

            var rng = RngStreams.Open(run.runSeed, RngStreams.Event, run.step, run.currentNodeId);
            run.eventsSeen ??= new List<string>();
            string picked = EventRoll.Pick(pool, run.floor, run.eventsSeen, new RunEventContext(save, run), rng);

            return !string.IsNullOrEmpty(picked) && OpenEventHere(run, picked);
        }

        // THE DEBUG MENU'S DOOR: open a named event where the party stands,
        // bypassing the pool (floor, seen, event-level requirements). The
        // state it leaves is exactly EnsureEvent's, so everything after it --
        // choices, reload, Leave -- is the real path.
        //
        // Replaces an event already open here. Returns false for an id
        // content does not have.
        public static bool OpenEventForDebug(string eventId)
        {
            var run = RunManager.Run;
            if (run == null || !run.hasRun || run.currentNodeId < 0) return false;

            return FindEvent(eventId) != null && OpenEventHere(run, eventId);
        }

        private static bool OpenEventHere(RunSnapshot run, string eventId)
        {
            var definition = FindEvent(eventId);
            var start = definition?.StartPage;
            if (start == null) return false;

            run.eventId = eventId;
            run.eventNodeId = run.currentNodeId;
            run.eventPageId = start.Id;
            run.eventResult = "";
            run.eventResultEffects = new List<EventEffect>();
            run.pendingFight = "";

            // ONCE PER RUN, unless it may return (plan 1.1): a returning event
            // stays in the pool until a `finish` effect marks it seen, so
            // Walk away brings it back at a later Event node, any number of
            // times. EventRoll.Pick is unchanged -- it only reads this list.
            run.eventsSeen ??= new List<string>();
            if (!definition.MayReturn && !run.eventsSeen.Contains(eventId)) run.eventsSeen.Add(eventId);

            SaveSlotManager.SaveCurrent();
            return true;
        }

        // ---- choosing ------------------------------------------------------------------

        // Picks choice `index` (its AUTHORED position on the page, as
        // EventChoiceView.Index gives it) on page `pageId`, the page the
        // caller painted or read the index from (EventView.PageId).
        //
        // THE PAGE IS PART OF THE PICK. An index means nothing without the
        // page it was read off: two presses before a repaint would otherwise
        // read the second against the page the first one opened, and pick a
        // row the player never saw. A pick for any page but the run's current
        // one is refused as StalePage and touches nothing.
        //
        // 1. VALIDATE, touching nothing: an event is open, the caller's page
        //    is the current one, the index is on the page, and
        //    EventChoiceGate passes -- the same gate the panel greys with, so
        //    a locked choice is refused here too (contract 7).
        // 2. APPLY: the choice's own effects first, THEN the outcome is
        //    resolved against the updated state (a counter ticked by this
        //    very pick is what the tenth-toss outcome reads -- EventFlow's
        //    documented contract), then the outcome's effects. The next page,
        //    or the concluded state, or a closed room.
        // 3. PERSIST, once.
        public static EventChoiceResult ChooseEventOption(int index, string pageId)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (!EventIsOpenOn(run) || save == null) return EventChoiceResult.Refused(EventRefusal.NoEvent);

            // Exact and ordinal. A press left over from a page reaches the
            // concluded state ("") as StalePage too, never as a pick.
            if (!string.Equals(pageId ?? "", run.eventPageId ?? "", System.StringComparison.Ordinal))
            {
                return EventChoiceResult.Refused(EventRefusal.StalePage);
            }

            var definition = FindEvent(run.eventId);
            var page = string.IsNullOrEmpty(run.eventPageId) ? null : definition.PageById(run.eventPageId);
            if (page == null || page.Choices == null || index < 0 || index >= page.Choices.Length)
            {
                return EventChoiceResult.Refused(EventRefusal.BadIndex);
            }

            // A fight this event started is still to be fought: its page is
            // shown only as the ground the fight launched from.
            if (!string.IsNullOrEmpty(run.pendingFight))
            {
                return EventChoiceResult.Refused(EventRefusal.FightPending);
            }

            // A shelf this event opened is still in front: its page is the
            // one the Browse went to, and it is picked from once the shelf
            // is left (LeaveShelf).
            if (!string.IsNullOrEmpty(run.pendingShelf))
            {
                return EventChoiceResult.Refused(EventRefusal.ShelfOpen);
            }

            var choice = page.Choices[index];
            var context = new RunEventContext(save, run);
            if (!EventChoiceGate.Evaluate(choice, context).Enabled)
            {
                return EventChoiceResult.Refused(EventRefusal.Locked);
            }

            // AN EMPTY REQUEST IS REFUSED, NEVER FOUGHT (plan section 2, item
            // 5). Decided before anything moves, against the outcome the pick
            // would take now; the choice's own effects cannot start a fight
            // (the build refuses it there), so only the outcome can.
            string fightId = FightStartedBy(EventFlow.Resolve(page, index, context));
            if (fightId.Length > 0)
            {
                var fight = definition.FightById(fightId);
                var request = EncounterRequest.ForEventFight(RoomType.Event, run.eventId, fight);
                if (fight == null || RunEncounter.FightersFor(save, run, request).Count == 0)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[RunOrchestrator] event '{run.eventId}' refused a pick that starts fight '{fightId}': " +
                        "nobody in its party is standing. The choice should require one of them alive.");
                    return EventChoiceResult.Refused(EventRefusal.NoFighters);
                }
            }

            // 2. APPLY.
            var applied = new List<EventEffect>();
            int ownEffects = choice.Effects?.Length ?? 0;
            for (int i = 0; i < ownEffects; i++) ApplyEffect(save, run, choice.Effects[i], applied);

            // Resolve returns the choice's effects followed by the chosen
            // outcome's; the first `ownEffects` of them are already applied.
            var resolution = EventFlow.Resolve(page, index, context);
            for (int i = ownEffects; i < resolution.Effects.Count; i++) ApplyEffect(save, run, resolution.Effects[i], applied);

            string effectsLine = EventEffectSummary.Describe(applied, ItemDisplayName);

            // A FIGHT STARTED: the event stays open ON THIS PAGE, the request
            // is on the run (ApplyEffect's Fight case), and the fight's own
            // result is what moves the event on. The outcome has no goTo of
            // its own (the build refuses one), so there is no page to take.
            if (!string.IsNullOrEmpty(run.pendingFight))
            {
                run.eventResult = resolution.Result;
                run.eventResultEffects = applied;
                bool saved = SaveSlotManager.SaveCurrent();
                return EventChoiceResult.Applied(saved, resolution.Result, effectsLine, closed: false);
            }

            bool nothingToSay = string.IsNullOrEmpty(resolution.Result) && string.IsNullOrEmpty(effectsLine);

            // A LEAVE WITH NOTHING TO SAY CLOSES AT ONCE -- the plain "Leave"
            // button. A Leave that has a result shows it first and waits for
            // LeaveEvent (contract 11: "the choices become ... a single
            // Leave"), which is the concluded state: eventPageId "".
            if (resolution.IsLeave && nothingToSay)
            {
                CloseEventFields(run);

                // ClearCurrentRoom persists: still exactly one write. It does
                // not report whether the write landed (LeaveShop has the same
                // blind spot), so a closed choice always reads as Ok.
                RunManager.ClearCurrentRoom();
                return EventChoiceResult.Applied(true, "", "", closed: true);
            }

            run.eventPageId = resolution.IsLeave ? "" : resolution.NextPageId;
            run.eventResult = resolution.Result;
            run.eventResultEffects = applied;

            // 3. PERSIST.
            bool persisted = SaveSlotManager.SaveCurrent();
            return EventChoiceResult.Applied(persisted, resolution.Result, effectsLine, closed: false);
        }

        // ---- leaving -------------------------------------------------------------------

        // Leaving is what clears the room -- an event is the third room that
        // does not clear itself on arrival (Arrival.Event), after the fight
        // and the shop. ClearCurrentRoom persists, so this is one write.
        //
        // Callable on any open event, concluded or not: the panel's own Leave
        // on a concluded event, and the bot's way out.
        public static void LeaveEvent()
        {
            var run = RunManager.Run;
            if (run == null) return;

            if (!EventIsOpenOn(run))
            {
                // Nothing under the party. Stale fields (an event on another
                // node) are tidied, but no room is cleared -- this node's
                // room is not the event's.
                if (string.IsNullOrEmpty(run.eventId)) return;
                CloseEventFields(run);
                SaveSlotManager.SaveCurrent();
                return;
            }

            CloseEventFields(run);
            RunManager.ClearCurrentRoom();
        }

        // eventsSeen is deliberately NOT touched: an event left is still an
        // event seen this run.
        internal static void CloseEventFields(RunSnapshot run)
        {
            run.eventId = "";
            run.eventNodeId = -1;
            run.eventPageId = "";
            run.eventResult = "";
            run.eventResultEffects = new List<EventEffect>();
            run.pendingFight = "";
            run.pendingShelf = "";
        }

        // ---- the event fight's end ----------------------------------------------------

        // THE EVENT HALF OF SettleFight, reached only for a request that is an
        // event fight, after the shared half (ledger, HP write-back, second
        // lives, Amassing Star) and after an `endRun` loss has already ended
        // the run there. What differs from a room, all of it here:
        //
        //   - NO ClearCurrentRoom, NO AdvanceLeg. The event is still open and
        //     its Leave clears the room, as it always has.
        //   - `wake`: every FIELDED member written back at 0 stands at 1 HP,
        //     and the run goes on. Benched members were never fielded, so the
        //     write-back never touched them and neither does this.
        //   - The ending picks the result outcome -- Survived when the round
        //     limit ran out, Defeated on any other win, Fell on a loss -- and
        //     it applies through ApplyEffect, with its result text and goTo,
        //     exactly as a pick's outcome would.
        //   - The request is cleared, and the run is written ONCE: by the
        //     payout (RewardApplier.Apply persists) when the fight pays, by
        //     SaveCurrent here when it does not. Every change above is in
        //     memory by then, so either write carries all of it.
        private static FightSettlement SettleEventFight(FightSession session, bool won, EncounterRequest request,
            VictoryRewards.Payout? payout)
        {
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;

            if (!won) WakeTheFallen(run, session);

            var ending = EndingOf(session, won);
            var outcome = request.EventFight.OutcomeFor(ending);

            run.pendingFight = "";

            var applied = new List<EventEffect>();
            if (outcome != null && save != null)
            {
                foreach (var effect in outcome.Effects ?? new EventEffect[0]) ApplyEffect(save, run, effect, applied);
            }

            // An outcome the build did not require (a Fell the author could
            // not reach) concludes the event with nothing to say; the Leave
            // row still stands, so the room is never stranded.
            run.eventPageId = outcome == null || outcome.IsLeave ? "" : outcome.GoTo;
            run.eventResult = outcome?.Result ?? "";
            run.eventResultEffects = applied;

            if (payout.HasValue)
            {
                var reward = PayOut(session, run, payout.Value, request.PartyOverride);
                return new FightSettlement(reward, null);
            }

            SaveSlotManager.SaveCurrent();
            return new FightSettlement(null, null);
        }

        // Which result outcome the fight's end selects. The session's own
        // EndReason when it has one; otherwise the caller's `won`, which is
        // what a room fight has always been settled on.
        internal static EventFightResult EndingOf(FightSession session, bool won)
        {
            if (session != null && session.EndReason == FightEndReason.Survived) return EventFightResult.Survived;
            return won ? EventFightResult.Defeated : EventFightResult.Fell;
        }

        // `wake`: the fielded fallen stand back up at 1 HP.
        private static void WakeTheFallen(RunSnapshot run, FightSession session)
        {
            if (run?.currentHealth == null) return;

            var fielded = new HashSet<string>(FieldedIds(session));
            foreach (var entry in run.currentHealth)
            {
                if (entry != null && fielded.Contains(entry.characterId) && entry.hp <= 0) entry.hp = 1;
            }
        }

        // The fight a resolved pick starts, or "" (at most one: the build).
        private static string FightStartedBy(EventChoiceResolution resolution)
        {
            foreach (var effect in resolution.Effects)
            {
                if (effect != null && effect.Kind == EventEffectKind.Fight) return effect.FightId ?? "";
            }

            return "";
        }

        // ---- the effects -------------------------------------------------------------

        // One effect, through the path the game already uses for it (plan
        // contract 10), none of which writes -- ChooseEventOption persists
        // once. `applied` records what actually moved, which is what the
        // effects line is built from.
        private static void ApplyEffect(SaveData save, RunSnapshot run, EventEffect effect, List<EventEffect> applied)
        {
            if (effect == null) return;

            switch (effect.Kind)
            {
                case EventEffectKind.Gold:
                    if (effect.Amount > 0)
                    {
                        // Held AND earned, BankPayout's rule.
                        if (RunManager.CreditPayout(effect.Amount)) applied.Add(effect);
                    }
                    else if (effect.Amount < 0)
                    {
                        // A SPEND TOUCHES run.gold AND NEVER goldEarned -- the
                        // shop's spend path (RunOrchestrator.Shop.cs, "run.gold
                        // -= price"), and BankPayout's header is why: held and
                        // earned diverge only through spending. Clamped at the
                        // purse, since an OUTCOME's spend carries no implied
                        // gate (only a choice's own effects do).
                        int spent = System.Math.Min(-effect.Amount, run.gold);
                        if (spent <= 0) return;

                        run.gold -= spent;
                        applied.Add(spent == -effect.Amount ? effect : EventEffect.Gold(-spent));
                    }
                    return;

                case EventEffectKind.HealPercent:
                    if (effect.TargetsOneMember)
                    {
                        // Never a revive, and absent or downed is no heal and
                        // no line (RunEncounter.HealMemberByPercent).
                        if (RunEncounter.HealMemberByPercent(run, effect.CharacterId, effect.Amount)) applied.Add(effect);
                        return;
                    }

                    RunEncounter.HealPartyByPercent(run, effect.Amount);
                    applied.Add(effect);
                    return;

                case EventEffectKind.DamagePercent:
                    RunEncounter.HurtPartyByPercent(run, effect.Amount);
                    applied.Add(effect);
                    return;

                case EventEffectKind.Exp:
                {
                    // RewardApplier's own split: everyone standing gets the
                    // full amount, a downed squad member half, rounded up --
                    // fielded is decided exactly the way a fight decides it.
                    var fielded = EncounterRoll.FieldableParty(save.ActiveSquadIds(), RunEncounter.HealthByCharacter(run));

                    // `character`: that one member alone, by the same rule
                    // (full standing, half downed). Not in the squad is no
                    // exp and no line.
                    if (effect.TargetsOneMember)
                    {
                        if (!save.ActiveSquadIds().Contains(effect.CharacterId)) return;
                        RewardApplier.ApplyUnsaved(new VictoryRewards.Payout(effect.Amount, 0), fielded,
                            new[] { effect.CharacterId });
                        applied.Add(effect);
                        return;
                    }

                    RewardApplier.ApplyUnsaved(new VictoryRewards.Payout(effect.Amount, 0), fielded);
                    applied.Add(effect);
                    return;
                }

                case EventEffectKind.Item:
                    // The Reckoning's destination (TakeOffer): the stockpile,
                    // the single live bag. No auto-equip -- that is a reward
                    // screen's convenience for a choice the player made among
                    // three cards, not a rule of acquiring an item.
                    if (ContentDatabase.GetItem(effect.Item) == null) return;
                    InventoryOps.Add(save.stockpiledItems, effect.Item, effect.Amount);
                    applied.Add(effect);
                    return;

                case EventEffectKind.Counter:
                    save.AddEventCounter(effect.CounterId, effect.Amount);
                    applied.Add(effect);
                    return;

                case EventEffectKind.Relic:
                    // IDEMPOTENT: a relic already held is not a second copy
                    // and not a second line. An id content no longer has is
                    // skipped, the Item case's rule.
                    if (ContentDatabase.GetRelic(effect.RelicId) == null) return;
                    run.relicIds ??= new List<string>();
                    if (run.relicIds.Contains(effect.RelicId)) return;
                    run.relicIds.Add(effect.RelicId);
                    applied.Add(effect);
                    return;

                case EventEffectKind.PrincesFavor:
                    AddEventBuff(run, EventBuffs.PrincesFavor, effect.Amount, EventBuffs.WholeRun);
                    applied.Add(effect);
                    return;

                case EventEffectKind.FillSpecialPool:
                    // THIS LEG: the step the leg began on is stored, and the
                    // buff goes quiet when AdvanceLeg moves past it.
                    AddEventBuff(run, EventBuffs.FillSpecialPool, 1, run.legStartStep);
                    applied.Add(effect);
                    return;

                case EventEffectKind.Fight:
                    // THE ENCOUNTER REQUEST. Persisted by the pick's one write;
                    // ChooseEventOption already refused a fight nobody can
                    // fight. Not an effects-line entry: the fight itself is
                    // what the player sees next.
                    run.pendingFight = effect.FightId ?? "";
                    return;

                case EventEffectKind.Finish:
                    // A returning event is marked seen: it never rolls again
                    // this run (plan 1.1). No line.
                    run.eventsSeen ??= new List<string>();
                    if (!string.IsNullOrEmpty(run.eventId) && !run.eventsSeen.Contains(run.eventId))
                    {
                        run.eventsSeen.Add(run.eventId);
                    }

                    // Its merchant stock ends with it (plan 1.5): the only
                    // thing besides the run's end that does.
                    EndShelvesOf(run, run.eventId);
                    return;

                case EventEffectKind.Shelf:
                    ApplyOpenShelf(run, effect);
                    return;

                case EventEffectKind.TakeShelf:
                    ApplyTakeShelf(save, run, effect, applied);
                    return;
            }
        }

        private static void AddEventBuff(RunSnapshot run, string kind, int amount, int legStartStep)
        {
            run.eventBuffs ??= new List<EventBuffEntry>();
            run.eventBuffs.Add(new EventBuffEntry { kind = kind, amount = amount, legStartStep = legStartStep });
        }

        private static string ItemDisplayName(string itemId)
        {
            var item = ContentDatabase.GetItem(itemId);
            return item == null || string.IsNullOrEmpty(item.displayName) ? itemId : item.displayName;
        }
    }
}
