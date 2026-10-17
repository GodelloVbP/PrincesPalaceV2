using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace
{
    // WHERE ONE PARTICULAR COPY CAME FROM (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
    // 3.3). Everything an ordinary item carries is on the definition or in the
    // four identity values beside this; provenance is the part only one copy
    // has.
    //
    //   lot         -- a caravan copy's own id. Non-empty means "this exact
    //                  object": it never stacks, not with another lot and not
    //                  with an ordinary copy of the same item, so a fake can
    //                  never be told apart by being the one stack that did not
    //                  merge. Genuine caravan copies carry one too, for that
    //                  reason.
    //   fake        -- a caravan fake. Gear works and then falls apart; a
    //                  consumable does nothing.
    //   fightsLeft  -- a fake piece of gear's countdown, dropped once per
    //                  completed fight it was worn in by a fielded member.
    //
    // [Serializable] and nested ONCE on the entries that carry it, so the
    // flat identity fields every save already has stay
    // exactly where they are. JsonUtility writes a nested class as an object
    // and, on a save that predates it, leaves the field initializer's plain
    // instance in place -- an ordinary copy, which is what such a save holds.
    [Serializable]
    public class Provenance
    {
        public string lot = "";
        public bool fake;
        public int fightsLeft;

        public Provenance()
        {
        }

        public Provenance(string lot, bool fake = false, int fightsLeft = 0)
        {
            this.lot = lot ?? "";
            this.fake = fake;
            this.fightsLeft = fightsLeft;
        }

        public bool HasLot => !string.IsNullOrEmpty(lot);

        public Provenance Copy() => new Provenance(lot, fake, fightsLeft);

        // Null reads as an ordinary copy, the same "empty and absent read the
        // same way" rule the list fields beside this follow.
        public static Provenance CopyOrPlain(Provenance source) => source == null ? new Provenance() : source.Copy();
    }

    // ONE COPY OF AN ITEM, AS A VALUE (docs/PLAN_ITEMS.md 2.3): which base,
    // at which tier, what rolled on it, and where it came from. Its gear
    // numbers come from ContentDatabase.Effective, which reads the base's
    // curve at Tier and the slot's honing; nothing about a copy's power is
    // baked into the definition it names.
    //
    // Immutable. The entries that hold a copy on disk (InventoryEntry,
    // EquipmentSlotEntry, ShopStockEntry) keep flat serialized fields; an
    // instance is read off one (`.Instance`) and written into another, so a
    // move carries every axis or none and no caller can forget one.
    //
    // SameStack is the ONE merge predicate. Whether two copies are "the same
    // object for counting purposes" is decided here and nowhere else.
    //
    public sealed class ItemInstance
    {
        private static readonly IReadOnlyList<string> NoModifiers = new List<string>();

        // The highest tier any copy reaches: the last frame of the art ladder
        // (content maxTier, 10 for every authored base), so no copy exists
        // that has no art or validated numbers behind it. Roll, honing, a
        // loaded save and ClampTier all stop here.
        public const int TierCap = 10;

        public readonly string ItemId;

        // 0..TierCap. Clamped on construction, so no reader has to.
        public readonly int Tier;

        // Never null; a private copy, so a caller's list cannot alias it.
        public readonly IReadOnlyList<string> ModifierIds;

        private readonly Provenance _provenance;

        // THE constructor of the copy model.
        public static ItemInstance Of(string baseId, int tier = 0, IEnumerable<string> modifierIds = null,
            Provenance provenance = null) =>
            new ItemInstance(baseId, tier, modifierIds, provenance);

        private ItemInstance(string itemId, int tier, IEnumerable<string> modifierIds, Provenance provenance)
        {
            ItemId = itemId ?? "";
            Tier = ClampTier(tier);
            ModifierIds = modifierIds == null ? NoModifiers : new List<string>(modifierIds);
            _provenance = Provenance.CopyOrPlain(provenance);
        }

        public static int ClampTier(int tier) => tier < 0 ? 0 : tier > TierCap ? TierCap : tier;

        public string Lot => _provenance.lot ?? "";
        public bool HasLot => _provenance.HasLot;
        public bool IsFake => _provenance.fake;
        public int FightsLeft => _provenance.fightsLeft;

        // A fresh copy for an entry to own. Never the instance's own object:
        // an entry mutates its provenance (the wear countdown), an instance
        // must not change underneath whoever read it.
        public Provenance ProvenanceCopy() => _provenance.Copy();

        // A fresh List for the entry constructors and the legacy positional
        // APIs, which take List<string>.
        public List<string> ModifierList() => new List<string>(ModifierIds);

        public ItemInstance WithProvenance(Provenance provenance) =>
            new ItemInstance(ItemId, Tier, ModifierIds, provenance);

        // THE MERGE PREDICATE.
        //
        // Ordinary copies: the same (base id, tier, modifier SET) -- the roll
        // that produced ["fiery","swift"] is the same item as
        // ["swift","fiery"].
        //
        // A copy with a lot is only ever the same stack as the SAME lot. Not
        // "same lot and same everything else": a lot names one physical copy,
        // so matching it is the whole question.
        public static bool SameStack(ItemInstance a, ItemInstance b)
        {
            if (a == null || b == null) return false;
            if (a.ItemId != b.ItemId) return false;
            if (a.HasLot || b.HasLot) return a.Lot == b.Lot;

            return a.Tier == b.Tier && ModifiersMatch(a.ModifierIds, b.ModifierIds);
        }

        private static bool ModifiersMatch(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var setA = new HashSet<string>(a ?? NoModifiers);
            var setB = new HashSet<string>(b ?? NoModifiers);
            return setA.SetEquals(setB);
        }

        public override string ToString()
        {
            string mods = ModifierIds.Count == 0 ? "" : " [" + string.Join(",", ModifierIds.ToArray()) + "]";
            string lot = HasLot ? " lot=" + Lot + (IsFake ? " fake(" + FightsLeft + ")" : "") : "";
            return ItemId + " t" + Tier + mods + lot;
        }
    }
}
