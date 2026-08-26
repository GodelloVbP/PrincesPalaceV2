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

            // The style's weights, in hundredths. Empty is legal and means
            // this set grants no ability scores at all.
            bool derivesScores = raw.styleWeights != null && raw.styleWeights.Length > 0;
            var weights = default(AbilityScoreBlock);
            if (derivesScores && !AbilityScoreLineParser.TryParse(raw.styleWeights, $"{label} styleWeights", out weights, errors))
            {
                return;
            }

            // The combat-stat profile, in percent -- Phase 4 (D4). Empty is
            // legal and means this set grants no combat stats at all (a
            // score-only material, if one is ever authored). Parsed ONCE per
            // set, same reasoning as styleWeights: every piece spends the
            // same profile, just at its own slot's share of the budget.
            bool derivesStats = raw.statProfile != null && raw.statProfile.Length > 0;
            var profile = StatBlock.Zero;
            if (derivesStats && !TryParseStatProfile(raw.statProfile, $"{label} statProfile", out profile, errors))
            {
                return;
            }

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

                // Assets-relative, and the FOLDER rather than a file —
                // IconPathFor appends '/level_N.png'. A Resources-style value
                // here generates eleven paths that all resolve to nothing.
                if (!ArtPathConvention.Check(pieceLabel, "iconSheet", piece.iconSheet, out string sheetError))
                {
                    errors.Add(sheetError);
                    continue;
                }

                // baseStats/topStats are GONE from the schema (D4) -- reject
                // loudly rather than silently ignore, so a stale JSON file
                // fails the content build with a sentence rather than quietly
                // losing the numbers someone typed. Both are checked (note
                // the non-shortcircuiting |) so one edit shows every stale
                // piece at once.
                bool staleBase = piece.baseStats != null && piece.baseStats.Length > 0;
                bool staleTop = piece.topStats != null && piece.topStats.Length > 0;
                if (staleBase | staleTop)
                {
                    errors.Add($"{pieceLabel}: authors baseStats/topStats, which the Phase 4 budget rewrite (D4) " +
                               $"removed from the schema -- every combat stat is now derived from the set's " +
                               $"statProfile (and every ability score from styleWeights). Remove this piece's " +
                               $"baseStats/topStats and put the intent in the set's statProfile instead.");
                    continue;
                }

                if (!AbilityScoreLineParser.TryParse(piece.requiresAtZero, pieceLabel, out var reqAtZero, errors)
                    | !AbilityScoreLineParser.TryParse(piece.requiresAtMax, pieceLabel, out var reqAtMax, errors))
                {
                    continue;
                }

                var baseScore = AbilityScoreBlock.Zero;
                var topScore = AbilityScoreBlock.Zero;
                if (derivesScores)
                {
                    baseScore = DeriveScores(weights, slot, maxTier, atTop: false);
                    topScore = DeriveScores(weights, slot, maxTier, atTop: true);
                }

                var baseStat = StatBlock.Zero;
                var topStat = StatBlock.Zero;
                if (derivesStats)
                {
                    baseStat = DeriveCombatStats(profile, slot, maxTier, atTop: false);
                    topStat = DeriveCombatStats(profile, slot, maxTier, atTop: true);
                }

                // Requirements derive too, unless the piece states its own.
                // requiresAtZero is its own string[]: empty means "said
                // nothing" and is distinguishable from "asked for zero".
                if (derivesScores && piece.requiresAtZero.Length == 0 && piece.requiresAtMax.Length == 0)
                {
                    reqAtZero = DeriveRequirements(weights, maxTier, atTop: false);
                    reqAtMax = DeriveRequirements(weights, maxTier, atTop: true);
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
        // GEOMETRIC between the two authored ends, not straight-line. The
        // shape lives in GearScaling.CurveFraction and the reasoning with it:
        // a linear ramp cannot make the last floor feel wild, it can only be
        // uniformly steeper, so the curve accelerates instead.
        //
        // Both ENDS are untouched by that change — f(0) is 0 and f(maxTier)
        // is 1 — so a set's authored numbers still mean exactly what they say
        // and raising maxTier still stretches the curve rather than inflating
        // the tiers that already exist.
        //
        // FLOORED, and via Math.Floor rather than integer division. C#
        // integer division truncates toward zero, which would round a
        // descending stat the opposite way from an ascending one and put a
        // quiet asymmetry into every set that carries a penalty — steel's
        // negative speed is a live instance of it. (AbilityDerivation used to
        // carry a FloorDiv2 helper guarding the identical trap; Phase 2 of
        // the balance redesign deleted it along with the piecewise curve it
        // served — see that file's header. The trap itself is unchanged and
        // still worth guarding here.)
        public static int ValueAt(int atZero, int atMax, int tier, int maxTier)
        {
            return ValueAt(atZero, atMax, tier, maxTier, GearScaling.TierGrowth);
        }

        // The same interpolation, on an arbitrary growth rate. Balance
        // redesign Phase 3 (D3): weapons climb GearScaling.WeaponTierGrowth
        // (1.35) instead of armour's TierGrowth (1.25), and this is the one
        // place both curves are actually walked, so the two must not
        // silently share a rate again by one of them forgetting to pass its
        // own. Every existing caller (armour stats, weapon/armour
        // requirements, weapon grades) keeps calling the no-arg overload
        // above and stays on 1.25 — only WeaponEntryResolver's own
        // attackAtZero/attackAtMax interpolation passes WeaponTierGrowth.
        public static int ValueAt(int atZero, int atMax, int tier, int maxTier, double growth)
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
            double moved = span * GearScaling.CurveFraction(tier, maxTier, growth);
            return atZero + (int)Math.Floor(moved);
        }

        // One end of a derived piece: every ability score the style weights,
        // scaled by the slot and the global base.
        //
        // Weights arrive in hundredths, which is how the file authors them —
        // "strength 40" is 0.40 — so they are divided here rather than at the
        // parse, where dividing would throw away the precision that makes 0.85
        // and 0.90 different slots.
        private static AbilityScoreBlock DeriveScores(AbilityScoreBlock weights, EquipmentSlot slot,
                                                      int maxTier, bool atTop)
        {
            int Value(int hundredths)
            {
                if (hundredths == 0) return 0;
                double weight = hundredths / 100.0;
                return atTop
                    ? GearScaling.AtTopTier(slot, weight, maxTier)
                    : GearScaling.AtBaseTier(slot, weight);
            }

            return new AbilityScoreBlock(
                Value(weights.strength),
                Value(weights.dexterity),
                Value(weights.constitution),
                Value(weights.wisdom),
                Value(weights.intelligence),
                Value(weights.charisma));
        }

        // What the material demands, from the same weights that say what it
        // grants. No slot term -- see GearScaling.RequirementAtBaseTier.
        private static AbilityScoreBlock DeriveRequirements(AbilityScoreBlock weights, int maxTier, bool atTop)
        {
            int Value(int hundredths)
            {
                if (hundredths == 0) return 0;
                double weight = hundredths / 100.0;
                return atTop
                    ? GearScaling.RequirementAtTopTier(weight, maxTier)
                    : GearScaling.RequirementAtBaseTier(weight);
            }

            return new AbilityScoreBlock(
                Value(weights.strength),
                Value(weights.dexterity),
                Value(weights.constitution),
                Value(weights.wisdom),
                Value(weights.intelligence),
                Value(weights.charisma));
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

        // One end of a derived piece's combat stats: every budget stat the
        // set's statProfile weights, scaled by the slot and the 60% combat
        // share -- the StatBlock sibling of DeriveScores above. Phase 4 (D4).
        private static StatBlock DeriveCombatStats(StatBlock profilePercent, EquipmentSlot slot, int maxTier, bool atTop)
        {
            var result = StatBlock.Zero;
            foreach (StatType stat in ProfileStats)
            {
                int percent = profilePercent[stat];
                if (percent == 0)
                {
                    continue;
                }

                double weight = percent / 100.0;
                double unitCost = UnitCostFor(stat);
                int value = atTop
                    ? GearScaling.CombatStatAt(slot, weight, unitCost, maxTier)
                    : GearScaling.CombatStatAt(slot, weight, unitCost, 0);
                result = result + StatBlock.ForStat(stat, value);
            }

            return result;
        }

        // The five stats a statProfile may spend on -- everything StatType
        // has EXCEPT Attack, which gear stopped granting in Phase 3.
        private static readonly StatType[] ProfileStats =
        {
            StatType.MaxHealth, StatType.PhysicalDefense, StatType.MagicalDefense,
            StatType.Speed, StatType.ManaRegen,
        };

        private static bool IsProfileStat(StatType stat)
        {
            return Array.IndexOf(ProfileStats, stat) >= 0;
        }

        private static double UnitCostFor(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth: return GearScaling.HpPerBudgetPoint;
                case StatType.PhysicalDefense: return GearScaling.PhysicalDefensePerBudgetPoint;
                case StatType.MagicalDefense: return GearScaling.MagicalDefensePerBudgetPoint;
                case StatType.Speed: return GearScaling.SpeedPerBudgetPoint;
                case StatType.ManaRegen: return GearScaling.ManaRegenPerBudgetPoint;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, "Not a statProfile stat -- see IsProfileStat.");
            }
        }

        // "physicalDefense 35" -- a set's combat-stat budget, in percent of
        // the 60% combat share. Phase 4 (D4). A sibling to
        // AbilityScoreLineParser, but for the five budget stats rather than
        // the six ability scores, and REQUIRED to sum to 100: there is no
        // such thing as an unspent budget point here, on purpose -- an
        // author who wants a stat to get nothing writes nothing, or "0".
        private static bool TryParseStatProfile(string[] lines, string label, out StatBlock percentages, List<string> errors)
        {
            percentages = StatBlock.Zero;
            bool ok = true;
            int sum = 0;
            var seen = new HashSet<StatType>();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Trim().Split(new[] { ' ', '\t', ':', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    errors.Add($"{label}: entry '{line}' should read '<stat> <percent>', for example 'physicalDefense 35'.");
                    ok = false;
                    continue;
                }

                if (!Enum.TryParse<StatType>(parts[0], ignoreCase: true, out var stat) || !IsProfileStat(stat))
                {
                    errors.Add($"{label}: entry names '{parts[0]}', which is not one of the budget stats " +
                               $"(maxHealth, physicalDefense, magicalDefense, speed, manaRegen).");
                    ok = false;
                    continue;
                }

                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent))
                {
                    errors.Add($"{label}: entry '{line}' has '{parts[1]}' where a whole number should be.");
                    ok = false;
                    continue;
                }

                if (!seen.Add(stat))
                {
                    errors.Add($"{label}: names {stat} twice.");
                    ok = false;
                    continue;
                }

                percentages = percentages + StatBlock.ForStat(stat, percent);
                sum += percent;
            }

            if (ok && sum != 100)
            {
                errors.Add($"{label}: sums to {sum}, not 100 -- the whole point of a budget is that it is spent completely.");
                ok = false;
            }

            return ok;
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
