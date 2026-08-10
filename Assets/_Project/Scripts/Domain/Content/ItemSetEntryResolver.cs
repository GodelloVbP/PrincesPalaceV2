using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Expands itemsets.json into finished items, with the same
    // collected-not-first-only error reporting as every other resolver here.
    //
    // The whole design question this answers is "how do you author 165 items
    // without writing 165 entries, and still be able to change the shape of
    // the system later". The answer is that a piece is described by its two
    // ENDS — what it gives at tier 0 and what it gives at max tier — and
    // everything between is interpolated.
    //
    // Anchoring at both ends rather than accumulating a per-tier step is what
    // makes the system safe to change:
    //   * raising maxTier from 10 to 15 leaves tier 0 exactly where it was
    //     and stretches the curve, instead of inflating every existing level;
    //   * retuning a piece is editing the two numbers a designer actually has
    //     an opinion about ("boots go from +1 dex to +3 dex"), not eleven
    //     rows;
    //   * adding a stat is one enum member and one string in the JSON, with
    //     no schema change anywhere.
    //
    // Only TIER is generated. The player-facing PLUS multiplies a finished
    // piece at runtime (see ItemUpgrade) rather than generating an asset per
    // combination — eleven tiers is 165 armour assets, eleven tiers times
    // eleven pluses would be 1,815.
    public static class ItemSetEntryResolver
    {
        private const int DefaultMaxTier = 10;
        private const int DefaultCost = 40;
        private const int DefaultCostPerTier = 25;

        // Armour with no authored ladder keeps the bare piece name — "Leather
        // Coif" rather than a made-up word. Unlike a weapon, where the
        // modifier already leads the name and a missing adjective would leave
        // a visible gap, a set piece reads perfectly well without one.
        public const string FallbackAdjective = "";

        // A guard, not a design limit. It exists so a typo'd maxTier of
        // 100000 fails with a sentence instead of trying to generate a
        // million ScriptableObjects.
        public const int HighestSupportedTier = 50;

        // How many drawings an item sheet holds unless a piece says otherwise.
        // Every sheet delivered so far is ten: five across, two down, read
        // left to right and top to bottom.
        public const int DefaultIconLevels = 10;

        // Which drawing a tier wears.
        //
        // Ten drawings do not divide eleven tiers, and where the duplicate
        // lands is the whole decision. Spreading proportionally puts it in
        // the MIDDLE of the range (tiers 5 and 6 share one) and keeps both
        // ends unique, so the tier-0 piece a player starts in and the top
        // piece they finish in are each their own picture. Clamping instead
        // — level = min(tier, 9) — would have been one line shorter and would
        // have made the top two tiers, the two worth showing off, identical.
        //
        // Plus deliberately does NOT reach this: honing a piece changes its
        // numbers, not what it looks like, so a +7 coif wears its tier's
        // drawing. Art belongs to the tier axis alone.
        //
        // Returns a 1-based level, matching the level_N.png the slicer writes.
        public static int IconLevelFor(int tier, int maxTier, int levels)
        {
            if (levels <= 1)
            {
                return 1;
            }

            if (maxTier <= 0 || tier <= 0)
            {
                return 1;
            }

            if (tier >= maxTier)
            {
                return levels;
            }

            // Rounded to nearest rather than floored, so the drawings sit
            // symmetrically across the range instead of every one of them
            // arriving a level late.
            int scaled = tier * (levels - 1) * 2 + maxTier;
            return 1 + scaled / (2 * maxTier);
        }

        // The full editor-time path, or empty when the piece has no sheet.
        public static string IconPathFor(string iconSheet, int tier, int maxTier, int levels)
        {
            if (string.IsNullOrWhiteSpace(iconSheet))
            {
                return "";
            }

            return $"{iconSheet.Trim().TrimEnd('/')}/level_{IconLevelFor(tier, maxTier, levels)}.png";
        }

        public static bool TryResolveAll(IReadOnlyList<RawItemSetEntry> entries, out List<ResolvedSetPiece> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedSetPiece>();
            errors = new List<string>();

            if (entries == null)
            {
                return true;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ResolveSet(entries[i], i, resolved, errors);
            }

            foreach (string duplicate in resolved.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Two generated pieces share the id '{duplicate}'. Set ids and piece ids must be unique, since the item id is built from both.");
            }

            return errors.Count == 0;
        }

        private static void ResolveSet(RawItemSetEntry raw, int index, List<ResolvedSetPiece> resolved, List<string> errors)
        {
            string label = string.IsNullOrWhiteSpace(raw?.id) ? $"set #{index}" : $"set '{raw.id}'";

            if (raw == null || string.IsNullOrWhiteSpace(raw.id))
            {
                errors.Add($"{label}: id is required — it is written into every item id this set generates, and item ids are written into save files.");
                return;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                errors.Add($"{label}: displayName is required — it is the material's name in every piece's name.");
                return;
            }

            int maxTier = raw.maxTier >= 0 ? raw.maxTier : DefaultMaxTier;
            if (maxTier > HighestSupportedTier)
            {
                errors.Add($"{label}: maxTier is {maxTier}, above the supported ceiling of {HighestSupportedTier}. Raising the ceiling is a one-line change, but this is usually a typo.");
                return;
            }

            if (raw.pieces == null || raw.pieces.Length == 0)
            {
                errors.Add($"{label}: has no pieces, so it would generate nothing.");
                return;
            }

            int cost = raw.cost >= 0 ? raw.cost : DefaultCost;
            int costPerTier = raw.costPerTier >= 0 ? raw.costPerTier : DefaultCostPerTier;
            int setOrder = raw.sortOrder >= 0 ? raw.sortOrder : index;

            var seenPieceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int p = 0; p < raw.pieces.Length; p++)
            {
                var piece = raw.pieces[p];
                string pieceLabel = $"{label}, piece #{p}";

                if (piece == null || string.IsNullOrWhiteSpace(piece.id))
                {
                    errors.Add($"{pieceLabel}: id is required.");
                    continue;
                }

                pieceLabel = $"{label}, piece '{piece.id}'";

                if (!seenPieceIds.Add(piece.id.Trim()))
                {
                    errors.Add($"{pieceLabel}: appears twice in the same set.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(piece.displayName))
                {
                    errors.Add($"{pieceLabel}: displayName is required — a set reads as a material because its pieces have their own names.");
                    continue;
                }

                if (!TryParseSlot(piece.slot, out var slot, out string slotError))
                {
                    errors.Add($"{pieceLabel}: {slotError}");
                    continue;
                }

                if (!TryParseStats(piece.baseStats, pieceLabel, "base", out var baseStat, out var baseScore, errors)
                    | !TryParseStats(piece.topStats, pieceLabel, "top", out var topStat, out var topScore, errors)
                    | !AbilityScoreLineParser.TryParse(piece.requiresAtZero, pieceLabel, out var reqAtZero, errors)
                    | !AbilityScoreLineParser.TryParse(piece.requiresAtMax, pieceLabel, out var reqAtMax, errors))
                {
                    // Every side is parsed before bailing (note the
                    // non-shortcircuiting |) so one edit shows every bad
                    // stat name at once rather than one per rebuild.
                    continue;
                }

                if (IsEmpty(baseStat) && IsEmpty(baseScore) && IsEmpty(topStat) && IsEmpty(topScore))
                {
                    errors.Add($"{pieceLabel}: grants nothing at either end, so every tier of it would be a blank item.");
                    continue;
                }

                for (int tier = 0; tier <= maxTier; tier++)
                {
                    resolved.Add(BuildPiece(raw, piece, slot, tier, maxTier,
                        baseStat, topStat, baseScore, topScore,
                        cost + costPerTier * tier,
                        setOrder * 10000 + p * 100 + tier,
                        InterpolateScores(reqAtZero, reqAtMax, tier, maxTier)));
                }
            }
        }

        private static ResolvedSetPiece BuildPiece(RawItemSetEntry set, RawSetPiece piece, EquipmentSlot slot,
            int tier, int maxTier, StatBlock baseStat, StatBlock topStat,
            AbilityScoreBlock baseScore, AbilityScoreBlock topScore, int cost, int sortOrder,
            AbilityScoreBlock requirements = default)
        {
            var stat = InterpolateStats(baseStat, topStat, tier, maxTier);
            var score = InterpolateScores(baseScore, topScore, tier, maxTier);

            string setId = set.id.Trim();
            string pieceId = piece.id.Trim();
            int levels = piece.iconLevels > 0 ? piece.iconLevels : DefaultIconLevels;

            // "Hardened Leather Coif". The tier adjective leads, exactly as
            // it does on a weapon — the two generators share ItemNaming so
            // armour and weapons cannot drift apart on this again.
            //
            // NO PLUS is baked into the name. Plus belongs to the instance,
            // so a definition that claimed "+3" would be wrong for every
            // other copy of the same item; the UI appends it at display time
            // through ItemNaming.WithPlus.
            string adjective = ItemNaming.AdjectiveAt(set.tierAdjectives, tier, FallbackAdjective);
            string baseName = $"{set.displayName.Trim()} {piece.displayName.Trim()}";

            return new ResolvedSetPiece(
                $"{setId}_{pieceId}_p{tier}",
                ItemNaming.Compose(adjective, baseName),
                set.description ?? "",
                slot,
                setId,
                set.displayName.Trim(),
                pieceId,
                tier,
                stat,
                score,
                cost,
                sortOrder,
                IconPathFor(piece.iconSheet, tier, maxTier, levels),
                requirements);
        }

        // The interpolation, and the one piece of arithmetic in this file
        // worth pinning.
        //
        // Straight-line between the two authored ends, FLOORED. C# integer
        // division truncates toward zero, which would round a descending stat
        // the opposite way from an ascending one and put a quiet asymmetry
        // into every set that ever carries a penalty — the same trap
        // AbilityDerivation.FloorDiv2 exists to avoid.
        public static int ValueAt(int atZero, int atMax, int tier, int maxTier)
        {
            if (maxTier <= 0 || tier <= 0)
            {
                return atZero;
            }

            if (tier >= maxTier)
            {
                return atMax;
            }

            int span = atMax - atZero;
            int scaled = span * tier;
            int step = scaled >= 0 ? scaled / maxTier : (scaled - maxTier + 1) / maxTier;
            return atZero + step;
        }

        private static StatBlock InterpolateStats(StatBlock atZero, StatBlock atMax, int tier, int maxTier)
        {
            var result = StatBlock.Zero;
            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                result = result + StatBlock.ForStat(stat, ValueAt(atZero[stat], atMax[stat], tier, maxTier));
            }

            return result;
        }

        // Internal rather than private: WeaponEntryResolver interpolates its
        // own requiresAtZero/requiresAtMax through this exact function too,
        // so a weapon and an armour piece cannot round their requirement
        // curves two different ways.
        internal static AbilityScoreBlock InterpolateScores(AbilityScoreBlock atZero, AbilityScoreBlock atMax, int tier, int maxTier)
        {
            return new AbilityScoreBlock(
                ValueAt(atZero.strength, atMax.strength, tier, maxTier),
                ValueAt(atZero.dexterity, atMax.dexterity, tier, maxTier),
                ValueAt(atZero.constitution, atMax.constitution, tier, maxTier),
                ValueAt(atZero.wisdom, atMax.wisdom, tier, maxTier),
                ValueAt(atZero.intelligence, atMax.intelligence, tier, maxTier),
                ValueAt(atZero.charisma, atMax.charisma, tier, maxTier));
        }

        // "dexterity 1" / "physicalResistance 9". One vocabulary covering
        // both StatType and AbilityScore, so an author names the stat they
        // mean and does not have to know which of the two blocks it lives in.
        private static bool TryParseStats(string[] lines, string pieceLabel, string which,
            out StatBlock stats, out AbilityScoreBlock scores, List<string> errors)
        {
            stats = StatBlock.Zero;
            scores = AbilityScoreBlock.Zero;
            bool ok = true;

            if (lines == null)
            {
                return true;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Trim().Split(new[] { ' ', '\t', ':', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    errors.Add($"{pieceLabel}: {which} entry '{line}' should read '<stat> <amount>', for example 'dexterity 1'.");
                    ok = false;
                    continue;
                }

                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                {
                    errors.Add($"{pieceLabel}: {which} entry '{line}' has '{parts[1]}' where a whole number should be.");
                    ok = false;
                    continue;
                }

                if (Enum.TryParse<AbilityScore>(parts[0], ignoreCase: true, out var score))
                {
                    scores = scores + AbilityScoreBlockFor(score, amount);
                    continue;
                }

                if (Enum.TryParse<StatType>(parts[0], ignoreCase: true, out var stat))
                {
                    stats = stats + StatBlock.ForStat(stat, amount);
                    continue;
                }

                errors.Add($"{pieceLabel}: {which} entry names '{parts[0]}', which is neither an ability score ({Names<AbilityScore>()}) nor a stat ({Names<StatType>()}).");
                ok = false;
            }

            return ok;
        }

        private static AbilityScoreBlock AbilityScoreBlockFor(AbilityScore score, int value)
        {
            switch (score)
            {
                case AbilityScore.Strength: return new AbilityScoreBlock(value, 0, 0, 0, 0, 0);
                case AbilityScore.Dexterity: return new AbilityScoreBlock(0, value, 0, 0, 0, 0);
                case AbilityScore.Constitution: return new AbilityScoreBlock(0, 0, value, 0, 0, 0);
                case AbilityScore.Wisdom: return new AbilityScoreBlock(0, 0, 0, value, 0, 0);
                case AbilityScore.Intelligence: return new AbilityScoreBlock(0, 0, 0, 0, value, 0);
                case AbilityScore.Charisma: return new AbilityScoreBlock(0, 0, 0, 0, 0, value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(score), score, "No AbilityScoreBlock field for this score.");
            }
        }

        private static string Names<T>() where T : Enum
        {
            return string.Join(", ", Enum.GetNames(typeof(T)).Select(n => char.ToLowerInvariant(n[0]) + n.Substring(1)));
        }

        private static bool TryParseSlot(string raw, out EquipmentSlot slot, out string error)
        {
            slot = EquipmentSlot.Head;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = $"slot is required — one of {Names<EquipmentSlot>()}.";
                return false;
            }

            string trimmed = raw.Trim();
            if (string.Equals(trimmed, "Weapon", StringComparison.OrdinalIgnoreCase))
            {
                slot = EquipmentSlot.Weapon1;
                error = null;
                return true;
            }

            if (!Enum.TryParse(trimmed, ignoreCase: true, out slot))
            {
                error = $"slot '{raw}' is not one of {Names<EquipmentSlot>()}.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsEmpty(StatBlock block) => block.Equals(StatBlock.Zero);

        private static bool IsEmpty(AbilityScoreBlock block) => block.Equals(AbilityScoreBlock.Zero);
    }
}
