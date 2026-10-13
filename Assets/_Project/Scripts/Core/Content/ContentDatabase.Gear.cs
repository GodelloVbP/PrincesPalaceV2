using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Content
{
    // THE STAT ACCESSOR SEAM of the copy model (docs/PLAN_ITEMS.md 2.3). A
    // copy is a base id plus a tier; what it grants comes from here, never
    // from fields on the definition it names.
    public static partial class ContentDatabase
    {
        // The definition a base id names. Content still holds one asset per
        // tier, every one carrying the same curve, so this answers with the
        // first (tier 0) asset of the base; once content holds one asset per
        // base it is the only one.
        public static ItemDefinition GetBase(string baseId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(baseId)) return null;
            return _items.FirstOrDefault(i => i.baseId == baseId);
        }

        // What `copy` grants while worn in a slot honed to `honing`. Null for
        // an id content does not know, and for anything that is not gear.
        //
        // The copy's id may be a base id ("brigandine_boots", the copy model)
        // or a per-tier id ("brigandine_boots_p3", what every bag and slot
        // holds until content collapses to bases). A per-tier id reads its
        // own asset's tier and ignores copy.Tier, so a caller moved onto this
        // accessor gets today's numbers before any copy carries a tier. That
        // branch goes when the per-tier assets do.
        public static GearStats? Effective(ItemInstance copy, int honing = 0)
        {
            if (copy == null) return null;

            var perTier = GetItem(copy.ItemId);
            if (perTier != null && perTier.id != perTier.baseId)
            {
                return perTier.IsEquippable ? perTier.curve.At(perTier.tier).WithHoning(honing) : (GearStats?)null;
            }

            var definition = perTier ?? GetBase(copy.ItemId);
            if (definition == null || !definition.IsEquippable) return null;

            return definition.curve.At(copy.Tier).WithHoning(honing);
        }

        // How good `copy` is: derived, never stored (plan 1.4). Every
        // modifier counts as an affix until modifiers.json carries the
        // `trait` flag (plan 2.4).
        public static Quality QualityOf(ItemInstance copy) =>
            copy == null ? Quality.Common : ItemQuality.Of(copy.ModifierIds.Count, 0);
    }
}
