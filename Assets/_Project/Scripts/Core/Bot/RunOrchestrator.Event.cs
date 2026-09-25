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
            var lines = EventLineView.ListFor(page, id => ContentDatabase.GetCharacter(id)?.Data);

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

            run.eventsSeen ??= new List<string>();
            if (!run.eventsSeen.Contains(eventId)) run.eventsSeen.Add(eventId);

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

            var choice = page.Choices[index];
            var context = new RunEventContext(save, run);
            if (!EventChoiceGate.Evaluate(choice, context).Enabled)
            {
                return EventChoiceResult.Refused(EventRefusal.Locked);
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
