using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Content
{
    // THE STAT ACCESSOR SEAM of the copy model (docs/PLAN_ITEMS.md 2.3). A
    // copy is a base id plus a tier; what it grants comes from here, never
    // from fields on the definition it names.
    public static partial class ContentDatabase
    {
        // The definition a copy names. Null for an id content does not know.
        public static ItemDefinition DefinitionOf(ItemInstance copy) =>
            copy == null ? null : GetItem(copy.ItemId);

        // What `copy` grants while worn in a slot honed to `honing`. Null for
        // an id content does not know, and for anything that is not gear.
        //
        public static GearStats? Effective(ItemInstance copy, int honing = 0)
        {
            var definition = DefinitionOf(copy);
            if (definition == null || !definition.IsEquippable) return null;

            return definition.curve.At(copy.Tier).WithHoning(honing);
        }

        // What a copy is called: the tier adjective and base name for gear,
        // the authored name for anything else, the id for content that is
        // gone. No honing suffix (ItemNaming.WithPlus adds that).
        public static string NameOf(ItemInstance copy)
        {
            if (copy == null) return "";

            var gear = Effective(copy);
            if (gear.HasValue && !string.IsNullOrEmpty(gear.Value.DisplayName)) return gear.Value.DisplayName;

            var definition = DefinitionOf(copy);
            return definition == null || string.IsNullOrEmpty(definition.displayName) ? copy.ItemId : definition.displayName;
        }

        // Every icon key one item can show. A consumable has its one path;
        // gear has one per tier on its curve (an art frame, repeated past the
        // art ladder), so a screen's icon table holds all of them and a copy
        // is looked up by ItemIcons.KeyOf.
        public static IEnumerable<string> IconPathsOf(ItemDefinition definition)
        {
            if (definition == null) yield break;

            if (!definition.IsEquippable)
            {
                if (!string.IsNullOrEmpty(definition.iconPath)) yield return definition.iconPath;
                yield break;
            }

            var seen = new HashSet<string>();
            for (int tier = 0; tier <= ItemInstance.TierCap; tier++)
            {
                string path = definition.curve.At(tier).IconPath;
                if (!string.IsNullOrEmpty(path) && seen.Add(path)) yield return path;
            }
        }

        // How good `copy` is: derived, never stored (plan 1.4). Every
        // modifier counts as an affix until modifiers.json carries the
        // `trait` flag (plan 2.4).
        public static Quality QualityOf(ItemInstance copy) =>
            copy == null ? Quality.Common : ItemQuality.Of(copy.ModifierIds.Count, 0);
    }
}
