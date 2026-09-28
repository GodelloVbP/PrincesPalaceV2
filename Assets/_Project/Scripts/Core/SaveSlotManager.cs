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
        //
        // INSIDE A WritesDeferred SCOPE this only marks the save dirty and
        // answers true; the one write happens when the outermost scope closes.
        public static bool SaveCurrent()
        {
            if (_cached == null) return false;
            if (_deferDepth > 0)
            {
                _deferredDirty = true;
                return true;
            }

            bool landed = SaveSystem.Save(_cached, CurrentSlot);
            if (landed) WritesLanded++;
            return landed;
        }

        // Every write SaveCurrent has landed this process. Read by tests that
        // pin how many writes an operation makes (FightSettlement's one);
        // public because PlayMode has no InternalsVisibleTo grant.
        public static int WritesLanded { get; private set; }

        // ONE WRITE FOR A MULTI-STEP MUTATION. Every SaveCurrent inside the
        // scope -- RunManager's Persist, RewardApplier.Apply, ClearCurrentRoom
        // -- collapses into a single write when the outermost scope is
        // disposed, so a crash between two of them cannot leave a half-applied
        // state on disk. Settling a fight is the caller (RunOrchestrator
        // .SettleFight): its payout, cleared room, fake wear and ledger fold
        // are one fact, and a file holding some of them replays the rest.
        //
        // Nothing is written if nothing inside asked to be.
        public static System.IDisposable WritesDeferred()
        {
            _deferDepth++;
            return new DeferredWrites();
        }

        private static int _deferDepth;
        private static bool _deferredDirty;

        private sealed class DeferredWrites : System.IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (--_deferDepth > 0 || !_deferredDirty) return;
                _deferredDirty = false;
                SaveCurrent();
            }
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

            // A RUN DOES NOT SURVIVE THE PROCESS, and THIS LINE IS THE WHOLE
            // OF THAT RULE'S ENFORCEMENT (AUDIT #117). A descent abandoned by
            // a crash or an alt-F4 is settled the moment its slot is opened,
            // which is also the first moment anything can see it -- there used
            // to be a [RuntimeInitializeOnLoadMethod] boot check in RunManager
            // as well, but "before any scene" meant it ran with the slot still
            // at 0, so it covered nothing this line does not and wrote slot 0
            // on its way past, which is what stole Continue from the slot the
            // player last played.
            //
            // AFTER CurrentSlot above, never before: SettleOnOpening reads
            // CurrentSave, and CurrentSave is whichever slot CurrentSlot names.
            // See SaveSlotController, which is where this rule lived before
            // Continue needed it too.
            RunManager.SettleOnOpening();

            // THE ONE THIS MANAGER IS HOLDING, not a second CreateNew().
            //
            // SettleOnOpening above reaches CurrentSave -> SaveSystem.Load ->
            // missing file -> SaveData.CreateNew(), and that instance is what
            // _cached holds from here on, so this writes the same object
            // rather than building a second one that nobody else holds.
            //
            // Nothing observable comes of it today: both are built from the
            // same ContentDatabase by a deterministic method, so they are
            // value-identical. It is written this way because the promise in
            // the header above is about THIS slot's save, and two objects that
            // happen to agree is not the same claim.
            if (!SaveSystem.SlotExists(slot))
            {
                SaveSystem.Save(CurrentSave, slot);
            }
        }
    }
}
