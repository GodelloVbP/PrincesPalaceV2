using System;
using System.Collections.Generic;

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
    }
}
