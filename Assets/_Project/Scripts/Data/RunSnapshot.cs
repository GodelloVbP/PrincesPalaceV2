using System;
using System.Collections.Generic;
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
        // again. This is the same reason Wallet and RelicLoadout are always
        // non-null with their emptiness expressed in their contents.
        public bool hasRun;

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

        // Not optional. EndRun subtracts this back out before banking, so a
        // run resumed without it would convert the free head start into
        // permanent Gold on retreat — a straight reintroduction of P0 #1, the
        // infinite money printer, through the back door.
        public int grantedGold;

        public List<InventoryEntry> inventory = new List<InventoryEntry>();
        public List<RunHealthEntry> currentHealth = new List<RunHealthEntry>();

        // ---- the run's own history ------------------------------------------
        //
        // Stored HERE rather than accumulated in memory, and that is the whole
        // point: a run survives quitting to the main menu and coming back, so
        // anything the death screen wants to say about "this run" has to
        // survive with it. Every field below is a running total written after
        // each fight.
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
        // DEAD WEIGHT SINCE P1 OF docs/PLAN_REWARD_TRACKS.md, kept rather than
        // deleted. The reward track's offer-reroll grant and the Reckoning's
        // reroll button were both retired -- nothing sets this above zero any
        // more -- but the field stays: removing it would drop the value
        // JsonUtility already wrote for an in-flight run on an old save, for
        // no gain.
        //
        // Purely additive, so CurrentVersion does not move -- an older save's
        // in-flight run has no such field and JsonUtility leaves it at zero,
        // which is exactly "has rerolled nothing yet".
        public int offerRerollsUsed;

        // Whether this descent guarantees a rest on the step before each boss.
        //
        // NOTHING GRANTS THIS TODAY. It was level 30 of the reward track;
        // P1 of docs/PLAN_REWARD_TRACKS.md retired that milestone along with
        // seven other over-arching reward kinds, so RunManager.StartRun now
        // leaves this at its default (false) rather than reading the squad.
        // The field and the generator parameter it feeds both stay, so a
        // future reward can wire back into StartRun without DescentMap
        // changing at all.
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
        // P1 of docs/PLAN_REWARD_TRACKS.md retired the level-100 refresh
        // (TrackReward.SecondLifeRefresh, which gave the charge back on
        // entering every boss) along with seven other over-arching reward
        // kinds -- so this now climbs at most once per descent with nothing
        // to clear it back to zero mid-run.
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

        // Which generator produced shopStock (ShopStock.StockVersion). An
        // open shop from an older build is left exactly as it was rolled and
        // the new generator applies at the next node: a shop that reshuffles
        // itself because the game updated is a free reroll granted by a patch
        // note.
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
