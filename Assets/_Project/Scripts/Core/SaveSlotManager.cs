namespace PrincesPalace
{
    // Carries the chosen save slot -- and the save itself -- from scene to
    // scene.
    //
    // Plain static state is enough: it only has to survive SceneManager.LoadScene
    // within one play session, and the authority is the file on disk either way.
    //
    // The save is CACHED rather than re-read per query. Not for speed: a run
    // mutates the same object across three scenes, and re-loading would hand a
    // caller a second SaveData whose edits the first one never sees -- which is
    // the shape of "my gold reset when I left the shop".
    public static class SaveSlotManager
    {
        private static SaveData _cached;
        private static int _cachedSlot = -1;

        public static int CurrentSlot
        {
            get => _slot;
            set
            {
                if (_slot == value) return;
                _slot = value;

                // Dropped rather than reloaded here: the next reader takes the
                // cost, and a setter that touches the disk is a surprise.
                _cached = null;
                _cachedSlot = -1;
            }
        }

        private static int _slot = 0;

        public static SaveData CurrentSave
        {
            get
            {
                if (_cached != null && _cachedSlot == CurrentSlot) return _cached;

                _cached = SaveSystem.Load(CurrentSlot);
                _cachedSlot = CurrentSlot;
                return _cached;
            }
        }

        // Whether the write landed, passed straight through from
        // SaveSystem.Save -- see its header for why it answers rather than
        // throwing. Nothing with no save loaded to write is a false: it is
        // not an error, but it is also not a persisted mutation, and a caller
        // asking "did this reach the disk" is asking about the disk.
        public static bool SaveCurrent()
        {
            if (_cached == null) return false;
            return SaveSystem.Save(_cached, CurrentSlot);
        }

        // For tests, which move between slots and throwaway roots freely.
        public static void Forget()
        {
            _cached = null;
            _cachedSlot = -1;
        }

        // THE SHARED HALF OF "PICK THIS SLOT AND PLAY IT" -- everything a
        // caller needs to do BEFORE navigating, in one place rather than
        // duplicated between the slot list and the main menu's Continue
        // button. Deliberately does NOT navigate itself: SaveSlotController
        // refreshes its own labels from the freshly-written file before
        // leaving, and Continue has no labels to refresh, so the one step
        // that differs between the two callers is the one step left out here.
        //
        // A brand new slot is written immediately rather than on first save —
        // picking a slot and finding it still "Empty" next launch reads as
        // lost progress. Harmless for Continue's own caller, which only ever
        // passes a slot MostRecentSlot found on disk and so always takes the
        // already-exists branch; kept here rather than split out so the two
        // callers cannot drift on what "entering a slot" means.
        public static void EnterSlot(int slot)
        {
            CurrentSlot = slot;

            // A RUN DOES NOT SURVIVE THE PROCESS. RunManager's boot check runs
            // before any scene, when the slot is still 0, so a descent
            // abandoned by a crash or an alt-F4 in any other slot came back
            // alive — and dropped the player back into the fight they had
            // quit. See SaveSlotController, which is where this rule lived
            // before Continue needed it too.
            RunManager.SettleOnOpening();

            if (!SaveSystem.SlotExists(slot))
            {
                SaveSystem.Save(SaveData.CreateNew(), slot);
            }
        }
    }
}
