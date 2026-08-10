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
    }
}
