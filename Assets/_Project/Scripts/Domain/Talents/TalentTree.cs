namespace PrincesPalace.Domain.Talents
{
    // One slot's worth of authored talent, as the screen needs it.
    //
    // A flattened view rather than the content asset: Domain cannot see
    // ContentDatabase, so Core resolves the name, the description and the cost
    // and hands them over already answered. Same split BagItem uses for the
    // pack, and for the same reason.
    public readonly struct TalentSlot
    {
        // The id the SAVE holds, and the only thing combat ever matches on.
        public readonly string Id;

        public readonly string Name;
        public readonly string Description;
        public readonly int Cost;

        // How much must already be spent ON THIS PATH before the slot opens --
        // 9 at the convergence, 20 at the capstone, 0 everywhere else.
        //
        // Read twice: TalentPage.Evaluate refuses a slot whose path is short of
        // it (Refusal.Gated), and the collar drawn around the two gated stones
        // shows how far off it is. Carrying it on the slot rather than looking
        // it up per-check is what lets Domain answer the question at all --
        // ContentDatabase is on the far side of the layer boundary.
        public readonly int MinSpent;

        public TalentSlot(string id, string name, string description, int cost, int minSpent = 0)
        {
            Id = id;
            Name = name;
            Description = description;
            Cost = cost;
            MinSpent = minSpent;
        }

        // A slot the content does not fill. Real: the sheep's third path is
        // entirely unauthored -- 42 talents against the 63 slots the skeleton
        // describes -- so "there is no talent here" is a state the screen has
        // to be able to say rather than a bug.
        public bool Exists => !string.IsNullOrEmpty(Id);
    }

    // A character's whole tree, slot by slot.
    //
    // THIS IS THE SEAM: the screen's own ids ("sheep.p0.s0") and the ids
    // every consumer matches against in talents.json ("sheep_ram_root") are
    // different namespaces, and this type is what translates between them.
    // Without it an ember spent and an orb lit would apply no effect,
    // because nothing would match unlockedTalentIds against a real talent.
    //
    // The graph itself needs no fixing. talents.json declares its own
    // prerequisites and they agree with TalentSkeleton.Parents exactly, for
    // all 294 -- so the shape the screen walks and the shape combat checks are
    // the same shape, and only the names differ.
    public sealed class TalentTree
    {
        // Empty, so a character whose content is missing entirely still draws
        // a tree of unauthored slots rather than throwing on the way in.
        public static readonly TalentTree None = new TalentTree();

        private readonly TalentSlot[] _slots =
            new TalentSlot[TalentPage.PathCount * TalentSkeleton.SlotCount];

        private static int IndexOf(int path, int slot) => path * TalentSkeleton.SlotCount + slot;

        private static bool InRange(int path, int slot) =>
            path >= 0 && path < TalentPage.PathCount && slot >= 0 && slot < TalentSkeleton.SlotCount;

        public void Set(int path, int slot, TalentSlot value)
        {
            if (!InRange(path, slot)) return;
            _slots[IndexOf(path, slot)] = value;
        }

        public TalentSlot At(int path, int slot) =>
            InRange(path, slot) ? _slots[IndexOf(path, slot)] : default;

        // The id the save holds for this slot, or null where nothing is
        // authored. Callers treat null as "cannot be invested" rather than as
        // an error -- see TalentSlot.Exists.
        public string IdAt(int path, int slot) => At(path, slot).Id;

        // How many slots on this path the content actually fills. What the
        // screen needs to know whether a path is worth paging to at all.
        public int AuthoredOn(int path)
        {
            int count = 0;
            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                if (At(path, slot).Exists) count++;
            }

            return count;
        }
    }
}
