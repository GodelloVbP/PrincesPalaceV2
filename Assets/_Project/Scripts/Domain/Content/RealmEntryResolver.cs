using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Content
{
    // Validates the human-edited realms.json. Every row error is collected; any
    // error refuses the whole file (.claude/rules/content.md).
    //
    // The generator clamps whatever profile it is handed, so these bounds are
    // not what keeps it from crashing: they refuse a profile that would be
    // silently clamped into something other than what was authored, and keep
    // the shape inside what the map layout is sized for
    // (RealmProfiles.MaxDefault*).
    public static class RealmEntryResolver
    {
        public static readonly RoomType[] WaypointKinds =
            { RoomType.Fight, RoomType.Event, RoomType.Treasure, RoomType.Rest, RoomType.Cache };

        public static readonly PrizeKind[] PrizeKinds =
            { PrizeKind.Gold, PrizeKind.Consumable, PrizeKind.Item, PrizeKind.Book, PrizeKind.Relic };

        public static bool TryResolveAll(IReadOnlyList<RawRealmEntry> entries,
            out List<ResolvedRealm> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRealm>();
            errors = new List<string>();
            var seenFloors = new HashSet<int>();
            var rows = entries ?? new List<RawRealmEntry>();

            for (int i = 0; i < rows.Count; i++)
            {
                var raw = rows[i];
                string label = $"realms.json entry #{i + 1}" + (raw != null && raw.floor > 0 ? $" (floor {raw.floor})" : "");
                if (raw == null)
                {
                    errors.Add($"{label}: the row is missing.");
                    continue;
                }

                int before = errors.Count;

                if (raw.floor < 1) errors.Add($"{label}: floor {raw.floor} must be 1 or more.");
                else if (!seenFloors.Add(raw.floor)) errors.Add($"{label}: floor {raw.floor} is used twice.");

                if (string.IsNullOrWhiteSpace(raw.name)) errors.Add($"{label}: name is required.");

                if (raw.pocketCount < 1 || raw.pocketCount > RealmMap.MaxPockets)
                    errors.Add($"{label}: pocketCount {raw.pocketCount} is outside 1-{RealmMap.MaxPockets}.");
                if (raw.pocketLengthMin < 1)
                    errors.Add($"{label}: pocketLengthMin {raw.pocketLengthMin} must be 1 or more.");
                if (raw.pocketLengthMax < raw.pocketLengthMin || raw.pocketLengthMax > RealmProfiles.MaxDefaultPocketLength)
                    errors.Add($"{label}: pocketLengthMax {raw.pocketLengthMax} must be {raw.pocketLengthMin}-{RealmProfiles.MaxDefaultPocketLength}.");
                if (raw.forkCount < 0 || raw.forkCount > RealmProfiles.MaxDefaultForks)
                    errors.Add($"{label}: forkCount {raw.forkCount} is outside 0-{RealmProfiles.MaxDefaultForks}.");
                if (raw.anchorCount < 0 || raw.anchorCount > RealmProfiles.MaxDefaultAnchors)
                    errors.Add($"{label}: anchorCount {raw.anchorCount} is outside 0-{RealmProfiles.MaxDefaultAnchors}.");
                if (!(raw.shopChance >= 0f && raw.shopChance <= 1f))
                    errors.Add($"{label}: shopChance {raw.shopChance} is outside 0-1.");

                var waypoints = ParseWeights<RoomType, RoomWeight>(raw.waypointWeights, WaypointKinds, "waypointWeights", label, errors,
                    (kind, weight) => new RoomWeight(kind, weight), out int waypointTotal);
                if (waypointTotal <= 0 && errors.Count == before)
                    errors.Add($"{label}: waypointWeights needs at least one weight above 0.");

                var prizes = ParseWeights<PrizeKind, PrizeWeight>(raw.prizeWeights, PrizeKinds, "prizeWeights", label, errors,
                    (kind, weight) => new PrizeWeight(kind, weight), out _);
                var anchorPrizes = ParseWeights<PrizeKind, PrizeWeight>(raw.anchorPrizeWeights, PrizeKinds, "anchorPrizeWeights", label, errors,
                    (kind, weight) => new PrizeWeight(kind, weight), out _);

                var boss = raw.bossPool ?? Array.Empty<string>();
                if (boss.Any(string.IsNullOrWhiteSpace)) errors.Add($"{label}: bossPool holds a blank id.");
                else if (boss.Distinct(StringComparer.Ordinal).Count() != boss.Length) errors.Add($"{label}: bossPool lists an id twice.");

                if (errors.Count > before) continue;

                resolved.Add(new ResolvedRealm(raw.floor, raw.name.Trim(), new RealmProfile
                {
                    pocketCount = raw.pocketCount,
                    pocketLengthMin = raw.pocketLengthMin,
                    pocketLengthMax = raw.pocketLengthMax,
                    forkCount = raw.forkCount,
                    anchorCount = raw.anchorCount,
                    shopChance = raw.shopChance,
                    waypointWeights = waypoints,
                    prizeWeights = prizes,
                    anchorPrizeWeights = anchorPrizes,
                    bossPool = boss.ToList(),
                    restBeforeBoss = raw.restBeforeBoss,
                }));
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            resolved = resolved.OrderBy(r => r.Floor).ToList();
            return true;
        }

        private static List<T> ParseWeights<TKind, T>(RawRealmWeight[] rows, TKind[] allowed, string field, string label,
            List<string> errors, Func<TKind, int, T> make, out int total) where TKind : struct
        {
            total = 0;
            var result = new List<T>();
            var seen = new HashSet<TKind>();

            foreach (var row in rows ?? Array.Empty<RawRealmWeight>())
            {
                if (row == null)
                {
                    errors.Add($"{label}: {field} holds a missing row.");
                    continue;
                }

                // IsDefined first: TryParse alone also accepts "3" as an ordinal.
                TKind kind = default;
                if (row.kind == null || !Enum.IsDefined(typeof(TKind), row.kind)
                    || !Enum.TryParse(row.kind, out kind) || !allowed.Contains(kind))
                {
                    errors.Add($"{label}: {field} kind '{row.kind}' is not one of {string.Join(", ", allowed)}.");
                    continue;
                }

                if (!seen.Add(kind))
                {
                    errors.Add($"{label}: {field} lists {kind} twice.");
                    continue;
                }

                if (row.weight < 0)
                {
                    errors.Add($"{label}: {field} weight for {kind} is {row.weight}; it may not be negative.");
                    continue;
                }

                total += row.weight;
                result.Add(make(kind, row.weight));
            }

            return result;
        }
    }
}
