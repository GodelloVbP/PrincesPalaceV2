using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrincesPalace
{
    public static class SaveSystem
    {
        public const int SlotCount = 5;

        // Test-only escape hatch. Null in every shipped build; when a test
        // sets it, PathForSlot writes there instead of the real
        // Application.persistentDataPath (AUDIT.md #23 — the PlayMode suite
        // used to delete the developer's real save_slot_0.json after every
        // test, having no spare slot of its own to use instead). Public
        // rather than internal so GameplayTestBase (a different asmdef) can
        // reach it without an InternalsVisibleTo entry.
        public static string RootOverride;

        // BOT-ONLY: THE SLOT LIVES IN RAM AND NEVER REACHES THE DISK.
        //
        // Measured, not assumed (see the phase table in the commit that added
        // BotPhaseTimers): a balance batch rewrites the save 28 times per run
        // -- on arrival at every room, on every offer taken, on every potion
        // drunk, twice in the relic draft -- and at ~5.7ms per write that was
        // 91% of the batch's entire wall clock. Persistence does not influence
        // gameplay in any of those places; it exists so a player who quits
        // mid-descent comes back where they left off, and a bot never quits.
        //
        // OFF BY DEFAULT and only ever set by BotRunDriver, which restores it
        // in the same finally that restores RootOverride. The game's own paths
        // never see it.
        //
        // Read semantics are kept honest rather than stubbed: Load returns a
        // COPY through the same JsonUtility round-trip a disk read would, so a
        // caller cannot alias the object it saved and no code can come to
        // depend on in-memory mode handing back a live reference. That cost
        // sits on the read path, which a batch never takes -- the write path,
        // which it takes 28 times a run, does nothing at all.
        public static bool InMemory;

        private static readonly Dictionary<int, SaveData> Memory = new Dictionary<int, SaveData>();
        private static readonly Dictionary<int, long> MemoryWrittenAt = new Dictionary<int, long>();
        private static long _memoryClock;

        // Called between runs. The equivalent of deleting the throwaway save
        // root, and the reason the driver no longer needs one directory per
        // run: without this, run N+1's Forget()+CurrentSave would load run N's
        // save instead of taking SaveSystem.Load's missing-file branch.
        public static void ClearMemory()
        {
            Memory.Clear();
            MemoryWrittenAt.Clear();
        }

        private static string PathForSlot(int slot)
        {
            string root = RootOverride ?? Application.persistentDataPath;
            return Path.Combine(root, $"save_slot_{slot}.json");
        }

        public static bool SlotExists(int slot)
        {
            if (InMemory) return Memory.ContainsKey(slot);
            return File.Exists(PathForSlot(slot));
        }

        // Always returns a usable SaveData. A missing, unreadable, corrupt or
        // unmigratable file falls back to a fresh profile rather than
        // throwing — a save file is the one thing a player can't repair, so
        // failing hard here would strand them at a broken main menu.
        public static SaveData Load(int slot)
        {
            if (InMemory)
            {
                // A COPY, for the same reason the disk path hands back a
                // freshly parsed object: two callers holding one SaveData is
                // the shape of "my gold reset when I left the shop", and an
                // in-memory mode that quietly allowed it would be a different
                // game from the one the batch is supposed to be measuring.
                if (!Memory.TryGetValue(slot, out var stored) || stored == null) return SaveData.CreateNew();

                var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(stored));
                if (copy == null || !copy.Migrate()) return SaveData.CreateNew();
                return copy;
            }

            string path = PathForSlot(slot);
            if (!File.Exists(path))
            {
                return SaveData.CreateNew();
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save slot {slot} could not be read ({e.GetType().Name}: {e.Message}). Starting a fresh profile.");
                return SaveData.CreateNew();
            }

            // JsonUtility returns null for empty or non-object JSON instead
            // of throwing, so this is a real case rather than paranoia.
            if (data == null)
            {
                Debug.LogWarning($"Save slot {slot} was empty or malformed. Starting a fresh profile.");
                return SaveData.CreateNew();
            }

            if (!data.Migrate())
            {
                Debug.LogWarning($"Save slot {slot} is version {data.version}, which this build cannot migrate (current is {SaveData.CurrentVersion}). Starting a fresh profile.");
                return SaveData.CreateNew();
            }

            return data;
        }

        // Called on every fight victory, purchase, and talent click — so a
        // write that tears (process killed, disk full, power loss mid-write)
        // has to leave the PREVIOUS file intact rather than a half-written
        // one Load() would then have to fall back away from (AUDIT.md #10).
        // Written to a sibling .tmp first; only a fully-written temp file
        // ever replaces the real one, and the rename itself is a single
        // filesystem operation rather than an in-place overwrite.
        public static void Save(SaveData data, int slot)
        {
            if (InMemory)
            {
                // Still counted as a write, so the phase table keeps reporting
                // how many times a run persists -- that number is the finding,
                // and losing it the moment it stops costing anything would
                // make the optimisation unfalsifiable.
                using (BotPhaseTimers.Measure(BotPhase.PersistWrite))
                {
                    Memory[slot] = data;
                    MemoryWrittenAt[slot] = ++_memoryClock;
                }

                return;
            }

            string path = PathForSlot(slot);
            string tempPath = path + ".tmp";

            string json;
            using (BotPhaseTimers.Measure(BotPhase.PersistSerialize))
            {
                json = JsonUtility.ToJson(data, true);
            }

            try
            {
                using (BotPhaseTimers.Measure(BotPhase.PersistWrite))
                {
                    File.WriteAllText(tempPath, json);

                    if (File.Exists(path))
                    {
                        File.Replace(tempPath, path, null);
                    }
                    else
                    {
                        File.Move(tempPath, path);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Save slot {slot} could not be written ({e.GetType().Name}: {e.Message}). The previous save on disk, if any, is untouched.");

                // Best-effort cleanup so a stray .tmp doesn't confuse a
                // future save attempt — failure here is not itself an error
                // worth surfacing, the write already failed.
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception)
                {
                    // Deliberately swallowed — nothing more useful to do.
                }
            }
        }

        public static void DeleteSlot(int slot)
        {
            if (InMemory)
            {
                Memory.Remove(slot);
                MemoryWrittenAt.Remove(slot);
                return;
            }

            string path = PathForSlot(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // Which slot the player most recently played, or -1 if none exist yet.
        //
        // KEYED OFF THE FILE'S OWN LAST-WRITE TIME, not a field on SaveData.
        // SaveSlotManager.CurrentSlot is process-static and resets to 0 on
        // every launch, so it cannot answer "which slot did I play last time" —
        // and the main menu is the FIRST screen shown, before anything has set
        // it this session. Adding a timestamp field to SaveData would be a
        // migration for a fact the filesystem already keeps for free.
        //
        // Safe against a torn write: Save() writes to a temp file and commits
        // with File.Replace/Move, so a slot's mtime always reflects a complete
        // write, never a partial one.
        public static int MostRecentSlot()
        {
            if (InMemory)
            {
                // The monotonic write counter stands in for the file's mtime;
                // it answers the same question ("which slot was written last")
                // without a filesystem to ask.
                int newest = -1;
                long newestAt = long.MinValue;

                foreach (var kv in MemoryWrittenAt)
                {
                    if (kv.Value <= newestAt) continue;
                    newest = kv.Key;
                    newestAt = kv.Value;
                }

                return newest;
            }

            int best = -1;
            DateTime bestWrite = DateTime.MinValue;

            for (int slot = 0; slot < SlotCount; slot++)
            {
                string path = PathForSlot(slot);
                if (!File.Exists(path)) continue;

                DateTime written = File.GetLastWriteTimeUtc(path);
                if (best < 0 || written > bestWrite)
                {
                    best = slot;
                    bestWrite = written;
                }
            }

            return best;
        }
    }
}
