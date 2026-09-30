using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Relic ids into the rows every relic list paints (plan C24).
    //
    // THE ONE LOOKUP. The Reckoning's RELICS tab, the RUN pane, the relic
    // draft and the glossary each used to search ContentDatabase.Relics for an
    // id themselves, and each decided again what an id with no definition
    // looks like. It is a row named by the raw id that says "Unknown relic":
    // the player carries SOMETHING, and a silently shorter list would be worse
    // than an honest unknown.
    public static class RelicRows
    {
        // Held order preserved: the descent's first pick is the first row.
        public static List<RelicRow> From(IReadOnlyList<string> ids)
        {
            var rows = new List<RelicRow>();
            if (ids == null) return rows;

            foreach (var id in ids) rows.Add(Row(id));
            return rows;
        }

        public static RelicRow Row(string id) => From(ContentDatabase.GetRelic(id), id);

        // The one place a definition becomes a row. `id` names the row when
        // there is no definition.
        public static RelicRow From(RelicDefinition definition, string id = null)
        {
            if (definition == null)
            {
                string raw = id ?? "";
                return new RelicRow(raw, raw, "", RelicRarity.Common,
                    UiStrings.RelicUnknown.Format(), raw, known: false);
            }

            var data = definition.Data;
            return new RelicRow(definition.id, data.DisplayName, RelicRarityNames.Of(data.Rarity), data.Rarity,
                data.Description, definition.id, known: true);
        }
    }
}
