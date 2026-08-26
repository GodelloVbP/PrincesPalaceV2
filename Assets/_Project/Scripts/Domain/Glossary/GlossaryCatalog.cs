using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Glossary
{
    // What the glossary can show.
    //
    // ORDER IS THE RAIL ORDER, top to bottom. Appending a value adds a button
    // and nothing else -- the screen builds one per enum member and the
    // controller fills whatever the adapter hands it, so a new category is a
    // value here plus a case in the Core adapter, never a new screen.
    public enum GlossaryCategory
    {
        Relics,
        Monsters,
        Spells,
        Items,
        Talents,

        // BALANCE REDESIGN PHASE 6 (D7.4): the six ability scores, the
        // Physical/Magical Defense curve, and honing -- none of them a
        // ScriptableObject, so nothing else on this rail has ever needed a
        // category that is not "one row per asset". See GlossaryEntries.
        // Mechanics() for why its rows are hand-authored rather than swept
        // off ContentDatabase like every other category here.
        Mechanics,

        Achievements,
    }

    // One row, flattened.
    //
    // Deliberately NOT a union over the six content types. Every one of them
    // has an id, a name, a line of meta and a body, and the glossary's whole
    // job is showing exactly those four -- a per-type view model would be six
    // near-identical classes and six code paths that could disagree about how
    // to render the same four fields.
    public readonly struct GlossaryEntry
    {
        public readonly string Id;
        public readonly string Name;

        // The one-line qualifier under the name: a rarity band, a tier, an
        // enemy's floor, a talent's constellation.
        public readonly string Meta;

        public readonly string Body;

        // Content the player has not unlocked yet is LISTED, not hidden.
        //
        // A glossary that hides what you have not found cannot tell you what
        // there is to find, which is most of the reason to open one. Locked
        // rows show their name and what unlocks them; the rest is withheld.
        public readonly bool Locked;
        public readonly string LockedBy;

        // Sprite key, empty when the entry has no art. The controller decides
        // what to do about that; Domain has no opinion about textures.
        public readonly string IconId;

        public GlossaryEntry(string id, string name, string meta, string body,
                             bool locked = false, string lockedBy = "", string iconId = "")
        {
            Id = id;
            Name = name;
            Meta = meta ?? "";
            Body = body ?? "";
            Locked = locked;
            LockedBy = lockedBy ?? "";
            IconId = iconId ?? "";
        }
    }

    // Paging over one category's entries.
    //
    // The same shape as BagView and DebugMenuCatalog, and for the same reason:
    // page arithmetic is cheap to get subtly wrong, invisible when it is, and
    // free to test when it lives somewhere without an engine in it.
    public static class GlossaryCatalog
    {
        public const int RowsPerPage = 10;

        public static readonly IReadOnlyList<GlossaryCategory> Categories =
            System.Enum.GetValues(typeof(GlossaryCategory)).Cast<GlossaryCategory>().ToList();

        // Spelled out rather than ToString(), so a two-word category is not one
        // day discovered to be rendering as "UltraRare" would.
        public static string DisplayName(GlossaryCategory category)
        {
            switch (category)
            {
                case GlossaryCategory.Relics: return "RELICS";
                case GlossaryCategory.Monsters: return "MONSTERS";
                case GlossaryCategory.Spells: return "SPELLS";
                case GlossaryCategory.Items: return "ITEMS";
                case GlossaryCategory.Talents: return "TALENTS";
                case GlossaryCategory.Mechanics: return "MECHANICS";
                default: return "DEEDS";
            }
        }

        // Delegated to Paging, which is the single copy of this arithmetic and
        // the single place its two off-by-one traps are guarded.
        public static int PageCount(int count) => UiKit.Paging.PageCount(count, RowsPerPage);

        public static int ClampPage(int page, int count) => UiKit.Paging.Clamp(page, count, RowsPerPage);

        public static IReadOnlyList<GlossaryEntry> Page(IReadOnlyList<GlossaryEntry> all, int page) =>
            UiKit.Paging.Slice(all, page, RowsPerPage);

        // How much of a category the player has actually seen. Shown on the
        // rail, because "17 of 40" is the number that makes a glossary
        // something to fill in rather than a reference table.
        public static int UnlockedCount(IReadOnlyList<GlossaryEntry> all) =>
            all == null ? 0 : all.Count(e => !e.Locked);
    }
}
