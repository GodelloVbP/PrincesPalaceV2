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

        public static void SaveCurrent()
        {
            if (_cached == null) return;
            SaveSystem.Save(_cached, CurrentSlot);
        }

        // For tests, which move between slots and throwaway roots freely.
        public static void Forget()
        {
            _cached = null;
            _cachedSlot = -1;
        }
    }
}
