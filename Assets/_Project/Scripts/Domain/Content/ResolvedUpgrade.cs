using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // One permanent Divine Principality upgrade, exactly as typed into
    // upgrades.json.
    //
    // THE LAST CONTENT TYPE STILL WRITTEN AS C# OBJECT INITIALISERS. Two
    // upgrades lived in ContentBuilder as CreateUpgrade(...) calls with their
    // costs as positional arguments, which meant the shop's economy could not
    // be touched without a recompile, nothing validated any of it, and the
    // reasoning behind "50" and "30" lived nowhere at all. Characters were the
    // second-to-last and moved for the same reasons; this is the file that
    // makes the sentence in ContentBuilder's header ("Upgrades is the only one
    // left") stop being true.
    [Serializable]
    public class RawUpgradeEntry
    {
        [ContentDoc("Stable identifier; written into save data as a purchase, so never rename it after a save exists.")]
        public string id;

        [ContentDoc("The name shown in the Principality shop.")]
        public string displayName;

        [ContentDoc("Flavor text shown under the name; empty is allowed but reads as an unfinished row.")]
        public string description = "";

        [ContentDoc("Cost in Principality currency; must not be negative, and 0 means free rather than unbuyable.")]
        public int cost = 50;

        [ContentDoc("In-run currency granted at the start of every run, if this upgrade grants that; 0 for upgrades that do not.")]
        public int startingGoldBonus;
    }

    [Serializable]
    public class RawUpgradeFile
    {
        public RawUpgradeEntry[] upgrades = Array.Empty<RawUpgradeEntry>();
    }

    // One validated upgrade -- the shape UpgradeDefinition stores rather than
    // restates, the same wrapper-round-one-value the other eight assets are.
    //
    // [Serializable] class with public fields; System.Serializable is BCL, so
    // Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedUpgrade
    {
        // Written into save files as a purchase. NEVER rename after a save
        // exists -- SaveData.purchasedUpgradeIds holds these strings and drops
        // any that no longer resolve, so a rename reads to the player as a
        // refund they did not ask for.
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";

        // Principality currency. Zero is legal and means free; negative is
        // refused by the resolver, because a shop row that pays the player to
        // take it is a typo every time.
        public int Cost;

        // In-run currency granted at the start of every run. 0 for the
        // upgrades that do not do this, which is most of them.
        public int StartingGoldBonus;

        public int SortOrder;

        // For the serializer only.
        public ResolvedUpgrade()
        {
        }

        public ResolvedUpgrade(string id, string displayName, string description,
            int cost, int startingGoldBonus, int sortOrder)
        {
            Id = id ?? "";
            DisplayName = displayName ?? "";
            Description = description ?? "";
            Cost = cost;
            StartingGoldBonus = startingGoldBonus;
            SortOrder = sortOrder;
        }
    }

    // Validates upgrades.json.
    //
    // The negative-cost check moved here from ContentDatabase.ValidateContent,
    // where it was the only thing anyone ever checked about an upgrade and
    // where it fired at RUNTIME on the generated asset. Failing at build time
    // instead means the bad row never becomes an asset, which is the rule the
    // other seven resolvers already follow: nothing is written when any entry
    // fails, because a partial catalogue looks like content that merely lost a
    // row.
    public static class UpgradeEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawUpgradeEntry> entries,
            out List<ResolvedUpgrade> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedUpgrade>();
            errors = new List<string>();
            if (entries == null) entries = new List<RawUpgradeEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, out var single, out string error))
                {
                    resolved.Add(single);
                }
                else
                {
                    errors.Add(error);
                }
            }

            foreach (string duplicate in resolved.GroupBy(u => u.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                // These end up in save data as purchases. Two upgrades sharing
                // an id would be indistinguishable forever, and buying one
                // would grant the other.
                errors.Add($"Duplicate upgrade id '{duplicate}' -- every id must be unique.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawUpgradeEntry raw, int index, int sortOrder,
            out ResolvedUpgrade upgrade, out string error)
        {
            upgrade = default;
            // WHITESPACE, not just empty, unlike the sibling resolvers. An id of
            // "   " is rejected by the guard below either way; the difference is
            // whether the message says `upgrade '   '` -- which names nothing an
            // author can search the file for -- or the row number, which does.
            string label = string.IsNullOrWhiteSpace(raw.id)
                ? $"upgrades.json entry #{index + 1}"
                : $"upgrade '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{label}: id is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required -- the shop would show a blank row.";
                return false;
            }

            if (raw.cost < 0)
            {
                error = $"{label}: cost is {raw.cost}. An upgrade that pays the player to take it is a typo " +
                        "every time; 0 is how a free upgrade is written.";
                return false;
            }

            if (raw.startingGoldBonus < 0)
            {
                error = $"{label}: startingGoldBonus is {raw.startingGoldBonus}. A permanent upgrade that makes " +
                        "every run start poorer is not a thing this shop sells; use 0 for no bonus.";
                return false;
            }

            upgrade = new ResolvedUpgrade(raw.id, raw.displayName, raw.description ?? "",
                raw.cost, raw.startingGoldBonus, sortOrder);
            error = null;
            return true;
        }
    }
}
