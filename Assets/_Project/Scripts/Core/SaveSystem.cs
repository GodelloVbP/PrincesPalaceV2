using System;
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

        private static string PathForSlot(int slot)
        {
            string root = RootOverride ?? Application.persistentDataPath;
            return Path.Combine(root, $"save_slot_{slot}.json");
        }

        public static bool SlotExists(int slot)
        {
            return File.Exists(PathForSlot(slot));
        }

        // Always returns a usable SaveData. A missing, unreadable, corrupt or
        // unmigratable file falls back to a fresh profile rather than
        // throwing — a save file is the one thing a player can't repair, so
        // failing hard here would strand them at a broken main menu.
        public static SaveData Load(int slot)
        {
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
            string path = PathForSlot(slot);
            string tempPath = path + ".tmp";
            string json = JsonUtility.ToJson(data, true);

            try
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
            string path = PathForSlot(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
