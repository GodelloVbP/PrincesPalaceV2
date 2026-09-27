using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // One squad member's HP, as a list entry rather than a dictionary pair.
    // Same reason every other map in SaveData is spelled this way: JsonUtility
    // serializes fields, and Dictionary is not one of the shapes it can write.
    [Serializable]
    public class RunHealthEntry
    {
        public string characterId;
        public int hp;
    }

    // A run, flattened for the save file.
    //
    // A separate DTO rather than making RunState itself [Serializable], on
    // purpose. RunState holds a readonly Wallet, a readonly HashSet and a
    // Dictionary — none of which JsonUtility can write — and its own header
    // states it is in-memory only, which is a live design contract rather than
    // an oversight: nothing in a run should be reaching for save semantics.
    // Flattening at the boundary keeps that true.
    //
    // NOTE WHAT IS ABSENT: the map. It is not stored because it does not need
    // to be. Given `runSeed` and `legStartStep`, DescentMapGenerator produces
    // the identical leg again — node ids are assigned in generation order and
    // generation reads no content, so `currentNodeId` and `clearedNodeIds`
    // stay meaningful across a regeneration. That is what the seed work was
    // for, and it means a content update cannot corrupt an in-progress run's
    // geography.
    [Serializable]
    public class RunSnapshot
    {
        // The discriminator, and it has to be in-band.
        //
        // `activeRun != null` cannot do this job: JsonUtility writes a null
        // object field as an empty object and instantiates missing fields on
        // read, so after one save/load cycle the reference is never null
        // again. This is the same reason Wallet is always non-null with its
        // emptiness expressed in its contents.
        public bool hasRun;

        // WHETHER THE PARTY HAS ACTUALLY GONE DOWN, which is a different
        // question from `hasRun` and the one most callers mean.
        //
        // A run EXISTS from the moment the descent gate rolls the relic draft,
        // and that draft is offered IN THE HUB and deliberately survives
        // leaving and coming back (RelicDraftTests pins it). So `hasRun` is
        // true while the player is standing in the hub with nothing under way
        // -- SystemMenuController's `inDescent` comment states exactly this,
        // and had to carry a second flag because of it.
        //
        // RunManager.EndRun read `hasRun` and treated the two alike, so the
        // hub's Main Menu button on a drafted-but-unwalked run stripped every
        // roster character's gear and the whole stockpile and counted a
        // lifetimeRunsEnded -- the same symptom 95c0b8b3 fixed for the
        // no-run-at-all case, reached through the door that guard left open.
        //
        // DERIVED, not a fourth serialized flag. Three fields already record
        // "the party moved": `step` leaves 0 only through RunManager.MoveTo,
        // `roomsCleared` and `clearedNodeIds` only through a room that
        // finished. A flag would be a fourth thing to write and a fourth
        // thing to forget, and JsonUtility would carry it into saves that
        // predate it as false anyway -- which is the right answer for an
        // in-flight run only by luck.
        //
        // NOT what SystemMenuController asks. That is "is THIS SCENE part of a
        // descent", answered at build time, and it is true on the map at the
        // entry node -- where this is still false because nothing has moved
        // yet. Two honest questions, deliberately not merged.
        public bool DescentIsUnderWay =>
            hasRun && (step > 0 || roomsCleared > 0 || (clearedNodeIds != null && clearedNodeIds.Count > 0));

        public ulong runSeed;

        // BOTH are needed and they are not the same number. `step` advances
        // room by room; `legStartStep` is the value the current leg was
        // generated at and stays put until the next leg is built. Regenerating
        // needs the latter — deriving it from the former produces a different
        // map, and only on resume, which is the worst place to find out.
        public int legStartStep;
        public int step;

        public int floor = 1;
        public int currentNodeId = -1;
        public List<int> clearedNodeIds = new List<int>();
        public string bossEnemyId = "";

        public int gold;

        // `grantedGold` used to sit here, with a header claiming "EndRun
        // subtracts this back out before banking, so a run resumed without it
        // would convert the free head start into permanent Gold on retreat -- a
        // straight reintroduction of P0 #1, the infinite money printer, through
        // the back door."
        //
        // None of that was true. A tree-wide grep found exactly one hit: the
        // declaration. Neither EndRun nor RunSettlement mentioned it, and there
        // is no banking left to subtract from -- a run's gold is forfeited
        // whole (RunSettlement.cs, GoldLost). The "start with N gold" reward it
        // was for does not exist.
        //
        // Deleted rather than re-commented, because the hazard was not the
        // field, it was a stated safety mechanism that would be TRUSTED. The
        // next person adding a head-start reward would have read that header as
        // "already handled" and shipped the money printer it describes. An
        // absent field asks the question; a lying one answers it wrong.
        //
        // Nothing on disk carries a meaning for it: JsonUtility ignores a field
        // it cannot map, so an old save's `grantedGold: 0` simply drops, and
        // there was never a non-zero one to drop.

        public List<InventoryEntry> inventory = new List<InventoryEntry>();
        public List<RunHealthEntry> currentHealth = new List<RunHealthEntry>();

        // ---- the run's own history ------------------------------------------
        //
        // Stored HERE rather than accumulated in memory. The reason this used
        // to give -- "a run survives quitting to the main menu and coming
        // back" -- is not true of this build and the correction matters,
        // because it is the premise every run-scoped prune in SaveData.Reconcile
        // rests on. RunManager says the opposite in as many words: "A RUN DOES
        // NOT SURVIVE THE PROCESS ... Deliberately not a resume." Boot settles
        // whatever is in slot 0, EnterSlot settles every other slot before its
        // first scene, and every in-process route to the main menu goes through
        // EndRun -- so nothing ever loads a run and plays it.
        //
        // What IS true, and is reason enough for these to be stored: the death
        // screen draws after EndRun has already replaced the snapshot, so the
        // totals have to survive the fight that ends the run and reach
        // RunSettlement, which reads them one last time on the way out. The
        // rest is PLUMBING KEPT READY for the day a resume is built; it costs
        // a few fields on disk and is not a claim that a resume exists.
        //
        // Every field below is a running total written after each fight.
        //
        // Nothing here is read by combat. It exists so the run can be described
        // afterwards, which is a thing the game previously could not do at all:
        // experience is applied per fight and saved immediately, so there was
        // no before-state left anywhere to diff against.

        public int goldEarned;
        public int expEarned;
        public int roomsCleared;

        // The furthest step reached, which is NOT `step` -- that is where the
        // party currently stands, and a run that ends is a run that stopped
        // moving. Kept separately so the summary can say how deep they got
        // rather than where they happened to die.
        public int deepestStep;

        // Bosses put down during THIS run. Settled against the save's
        // lifetime list when the run ends, which is what makes an ember payout
        // per UNIQUE boss possible -- the run knows what it killed, the save
        // knows what was already killed, and neither alone can answer it.
        public List<string> bossesKilled = new List<string>();

        public List<RunLedgerEntry> ledger = new List<RunLedgerEntry>();

        // ---- relics ------------------------------------------------------------
        //
        // Drafted at the start of a descent and GONE when it ends -- which is
        // why they live here rather than on SaveData. Nothing about a relic is
        // permanent progression; what persists is the ACHIEVEMENT that unlocked
        // it, on the save, and the pool re-offers it every run afterwards.
        //
        // A list rather than a single id, deliberately. Only one is drafted
        // today, but the design is "infinite slots per run" -- mid-run relic
        // rewards from elites or bosses drop straight in here with no shape
        // change, which is the whole reason not to write `string relicId`.
        public List<string> relicIds = new List<string>();

        // Whether the start-of-run draft has been resolved. Not derivable from
        // relicIds being empty: a player who is OFFERED three and somehow ends
        // with none (a pool with nothing unlocked in it) must not be asked
        // again every time they reload the hub.
        public bool relicDrafted;

        // How many item-offer rerolls this descent has spent.
        //
        // DEAD WEIGHT, KEPT RATHER THAN DELETED. Nothing sets this above zero
        // any more, but removing the field would drop the value JsonUtility
        // already wrote for an in-flight run on an old save, for no gain.
        //
        // Purely additive, so CurrentVersion does not move -- an older save's
        // in-flight run has no such field and JsonUtility leaves it at zero,
        // which is exactly "has rerolled nothing yet".
        public int offerRerollsUsed;

        // Whether this descent guarantees a rest on the step before each boss.
        //
        // NOTHING GRANTS THIS TODAY. RunManager.StartRun leaves this at its
        // default (false) rather than reading the squad. The field and the
        // generator parameter it feeds both stay, so a future reward can
        // wire back into StartRun without DescentMap changing at all.
        //
        // SNAPSHOT AT StartRun RATHER THAN READ LIVE, which is the opposite of
        // how the reroll allowance works, and the difference matters. The map
        // is not serialised: RunManager regenerates it from the seed whenever
        // it is asked (that is what makes a reloaded descent identical). If
        // this were read from the squad each time, a character levelling
        // MID-DESCENT would change what the generator produces -- and the leg
        // the player is standing in would silently reshape underneath them,
        // rooms they had already seen turning into different rooms.
        //
        // A reroll allowance changing mid-run is harmless. A map changing
        // mid-run is the same class of problem as a draft that reshuffles on
        // reload, which the relic draft's seed exists to prevent.
        //
        // The cost is that this reward starts applying on the NEXT descent
        // rather than the current one, which is the ordinary behaviour of a
        // rule fixed at run start.
        public bool restBeforeBoss;

        // How many second lives this descent has spent -- level 90 of the
        // reward track.
        //
        // NO MID-RUN REFRESH: this climbs at most once per descent, with
        // nothing that clears it back to zero before the run ends.
        //
        // USED rather than remaining, the same shape as offerRerollsUsed and
        // for the same reason: the allowance stays a pure function of the
        // track, so a character reaching level 90 mid-descent is owed one from
        // that moment rather than being stuck with a count snapshotted before
        // they earned it. Unlike restBeforeBoss this is safe to read live,
        // because nothing regenerates from it -- it changes what a fight does
        // next, not what the map already looked like.
        //
        // Purely additive, so CurrentVersion does not move.
        public int secondLivesUsed;

        // Mechanic (d), RUN-WIDE STATS: a per-run accumulator combat reads
        // back every fight, as opposed to a per-fight bonus that resets
        // when the encounter ends. Amassing Star adds 2 here per kill
        // (RunOrchestrator.SettleFight, from FightSession.
        // BonusDamagePercentEarned); FightEncounterAdapter reads it back
        // onto FightSession.RunWideBonusDamagePercent when the NEXT fight
        // is built. Reset for free exactly like relicIds: StartRun replaces
        // the whole snapshot, so a new run starts at zero without anything
        // having to remember to clear it.
        //
        // Purely additive, so CurrentVersion does not move.
        public int bonusDamagePercent;

        // ---- the shop ------------------------------------------------------
        //
        // What the shop at shopNodeId has on its shelves, quoted prices and
        // all. Empty when the party is not standing in an unresolved shop --
        // LeaveShop clears it, which is also what makes "a run holds exactly
        // one uncleared shop" true (docs/PLAN_SHOP.md §2e).
        public List<ShopStockEntry> shopStock = new List<ShopStockEntry>();

        // Rerolls spent AT THIS NODE, one count per section
        // (ShopStock.SectionCount, indexed ShopStock.GearSection etc).
        //
        // PER SECTION rather than one scalar because reroll is per section
        // (§7.1 point 7), and this array position is BOTH the reroll price's
        // n and the RNG's third coordinate -- naming it after the section is
        // what makes those two read the same number rather than two numbers
        // that agree by coincidence.
        //
        // JsonUtility instantiates through the default constructor, so a save
        // written before this field existed keeps the initialiser below and
        // loads a correctly-sized zero array -- which is exactly "has
        // rerolled nothing". What it does NOT protect against is a save
        // written when SectionCount was a different number, or an explicit
        // null in hand-edited JSON: both load an array of the wrong length
        // and every read site would then need a bounds check. Normalised once
        // in SaveData.Reconcile instead.
        public int[] shopRerollsUsed = new int[ShopStock.SectionCount];

        // WHICH NODE THE STOCK ABOVE BELONGS TO. The guard, not an
        // optimisation: without it a stale list from a previous shop paints
        // the next one. -1 is "no shop stock" -- and it must be, since 0 is a
        // real node id, which is the same reason currentNodeId above starts
        // at -1 rather than 0.
        public int shopNodeId = -1;

        // Which generator produced shopStock (ShopStock.StockVersion).
        //
        // A RECORD, NOT A GATE, and the difference matters enough to say. This
        // header used to read as though something enforced a rule with it --
        // "an open shop from an older build is left exactly as it was rolled
        // and the new generator applies at the next node: a shop that
        // reshuffles itself because the game updated is a free reroll granted
        // by a patch note." The rule does hold, and nothing reads this field to
        // make it hold: it holds because the shelf is STORED. EnsureShopStock
        // returns early when ShopIsOpen, so an existing shelf is never
        // re-rolled whatever version produced it.
        //
        // Written in three places (EnsureShopStock, LeaveShop, and both of
        // SaveData.ReconcileShopStock's discard paths) and compared in none.
        // ReconcileShopStock is the one place that WOULD enforce it, and it
        // never looks -- so a future version of that method deciding to re-roll
        // a stale shelf would read the old header as "already handled".
        //
        // Kept rather than deleted, unlike grantedGold above: this one is
        // written to every save that has a shop open, a test asserts it after a
        // roll, and "which generator made this shelf" is a real fact worth
        // having on disk the day the shelf format changes. What was wrong was
        // the header, not the field. Filed as AUDIT #112.
        //
        // ShopMutationTests.AShelfRolledByAnOlderGeneratorIsLeftAsItWasRolled
        // pins the rule itself against the storage, rather than against this.
        public int shopStockVersion;

        // ---- learned spells (docs/PLAN_SHOP.md §1b) -------------------------
        //
        // A flat list of {characterId, skillId, slot}, not a dictionary --
        // same reason RunHealthEntry above is a list entry: JsonUtility
        // serializes fields and Dictionary is not one of the shapes it can
        // write. Nothing carries over between runs: StartRun replaces the
        // whole snapshot, the same mechanism that already resets relicIds
        // and offerRerollsUsed.
        //
        // Purely additive. SaveData.CurrentVersion does not move -- an
        // older in-flight run's JsonUtility deserialize leaves this empty,
        // and empty is exactly "has learned nothing".
        public List<LearnedSpellEntry> learnedSpells = new List<LearnedSpellEntry>();

        // A bought or dropped book, between "acquired" and "learned into a
        // slot" -- one skillId per copy owned and not yet placed, so buying
        // the same book twice for two different characters is two entries.
        // The shop's own commit only ever appends here (§2f); LearnSpell is
        // what removes one matching entry, and Replace* returns the
        // displaced book here instead of destroying it (§7.1 point 5).
        public List<string> unassignedSpellBooks = new List<string>();

        // ---- the event room (docs/EVENTS.md, plan contracts 1-2, 15) --------
        //
        // THE DISCRIMINATOR IS eventId, NOT eventNodeId. An empty id is "no
        // event open". eventNodeId cannot do that job: a save written before
        // these fields existed reads a missing int as 0 on some paths, and 0
        // is a real node. So an old save loads with eventId "" and no event,
        // whatever eventNodeId comes back as. Same reasoning as hasRun above.
        //
        // Open means eventId is non-empty AND eventNodeId is the node the
        // party stands on (RunOrchestrator.EventIsOpen); Reconcile closes one
        // that fails the second half, the way ReconcileShopStock drops a shelf
        // that is not under the party.
        public string eventId = "";
        public int eventNodeId = -1;

        // The page the event is on. EMPTY WHILE eventId IS SET means the
        // event has CONCLUDED: a choice ended in Leave but had a result to
        // show, and only LeaveEvent is left (EventView.Concluded).
        public string eventPageId = "";

        // Every event opened this run, so none shows twice (plan assumption
        // 4). Run-scoped by construction: StartRun replaces the snapshot.
        public List<string> eventsSeen = new List<string>();

        // The last choice's outcome, stored rather than held in memory so a
        // Map scene reload repaints the same result line the player was
        // reading. The effects are the APPLIED ones and the effects line is
        // derived from them on read (EventEffectSummary), not stored as text.
        public string eventResult = "";
        public List<EventEffect> eventResultEffects = new List<EventEffect>();

        // THE PENDING ENCOUNTER REQUEST (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
        // 3.1): the id of one of the open event's own fights, set by the pick
        // whose outcome carries a `fight` effect and cleared by the fight's
        // settlement. While it names a fight, the Fight screen and the bot
        // build THAT fight instead of a room roll
        // (RunOrchestrator.CurrentEncounterRequest), and SettleFight settles
        // it as an event fight: no room cleared, no leg advanced, the event
        // moved on to its onDefeated / onSurvived / onFell outcome.
        //
        // Persisted so a quit mid-fight relaunches the same fight on the same
        // (step, node) stream. Meaningful only while the event is open;
        // Reconcile drops one whose event is not. Save version 7 exists
        // because an older build would settle it as a room fight and skip
        // its ending.
        public string pendingFight = "";

        // Buffs events granted this run (docs/PLAN_PETTING_ZOO.md): Prince's
        // favor for the run, a full special pool for one leg. One model for
        // both scopes, see EventBuffEntry; read through EventBuffs, never by
        // hand. Run-scoped by construction: StartRun replaces the snapshot,
        // and a leg buff goes quiet when AdvanceLeg moves legStartStep, so
        // nothing clears this list.
        //
        // Purely additive, so CurrentVersion does not move.
        public List<EventBuffEntry> eventBuffs = new List<EventBuffEntry>();
    }

    // One learned spell, in one of a character's three slots this run.
    // `slot` is 0..MaxSpellSlots-1 and is the identity of the SLOT, not an
    // ordering hint -- CanLearn/LearnSpell/ReplaceSpell all address a
    // specific slot by this number so a replace overwrites the one the
    // player actually picked rather than "whichever entry sorts there".
    [Serializable]
    public class LearnedSpellEntry
    {
        public string characterId;
        public string skillId;
        public int slot;
    }
}
