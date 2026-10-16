using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Relics;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // WHAT A REWARD PAGE CARD SAYS, as plain values, so the Reckoning only
    // places them (docs/PLAN_REWARD_SCREENS.md C5). Built from the same
    // readers the sheet and the bot use: GearEvaluator.LargestDelta swaps the
    // copy in on a throwaway recipient, ModifierEffectsForItem scales its
    // affixes at the copy's own tier.
    public sealed class RewardCardFace
    {
        // The name WITHOUT the honing suffix (honing belongs to the wearer's
        // slot and is drawn on the slot band), and the quality's colour.
        public string Name = "";
        public string Hex = "#FFFFFF";

        // Plinth silhouette, 1 (flagstone) to 4 (gold cloth): the primary
        // quality channel. Gear: the item's quality. Relic: C5's mapping.
        public int PlinthStep = 1;

        // Slot band, gear only: the tier numeral and the slot's honing.
        public bool HasBand;
        public int BandTier;
        public int BandPlus;

        // The wearer (gear). Empty on a relic.
        public string WearerId = "";

        // The headline stat as glyph (or word) plus a signed number, and the
        // chevron's side. HasDelta is false for a relic, an inert card and a
        // card that is no longer open.
        public bool HasDelta;
        public string DeltaText = "";
        public int DeltaSign;

        // One rules line: the strongest affix (gear), or the relic's text.
        public string Feature = "";

        // What the wearer has in the card's slot now: the icon key, or null for
        // an empty slot or a card that is no longer open.
        public string GhostKey;

        // The worn copy has an affix the new one lacks; its name.
        public bool HasLost;
        public string LostName = "";

        public bool Inert;

        public static int PlinthStepOf(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return 1;
                case RelicRarity.Uncommon: return 2;
                case RelicRarity.Rare:
                case RelicRarity.UltraRare: return 3;
                default: return 4;
            }
        }

        public static int PlinthStepOf(Quality quality)
        {
            switch (quality)
            {
                case Quality.Uncommon: return 2;
                case Quality.Rare: return 3;
                case Quality.Legendary: return 4;
                default: return 1;
            }
        }

        public static RewardCardFace OfGear(SaveData save, PendingCard card)
        {
            var face = new RewardCardFace();
            var copy = card?.Copy;
            if (copy == null) return face;

            var recipient = save?.roster?.FirstOrDefault(c => c != null && c.definitionId == card.RecipientId);
            var quality = ContentDatabase.QualityOf(copy);

            face.Name = RarityColors.NameOf(copy);
            face.Hex = ItemQuality.HexColor(quality);
            face.PlinthStep = PlinthStepOf(quality);
            face.HasBand = true;
            face.BandTier = copy.Tier;
            face.BandPlus = recipient == null ? 0 : ContentDatabase.HoningOf(recipient, card.Slot);
            face.WearerId = card.RecipientId ?? "";
            face.Inert = card.Inert;
            face.Feature = card.Inert ? UiStrings.ReckoningInert.Format() : StrongestAffixLine(copy, recipient);

            // What the card would change is a question about an OPEN card: once
            // it is taken the recipient already wears it, and a delta against
            // itself would read "no stat change".
            if (card.Outcome == CardOutcome.Open)
            {
                SetDelta(face, recipient, card);
                face.LostName = LostAffixName(recipient, card);
                face.HasLost = face.LostName.Length > 0;
                if (recipient?.equipment != null && !recipient.equipment.IsEmpty(card.Slot))
                {
                    face.GhostKey = ItemIcons.KeyOf(recipient.equipment.GetInstance(card.Slot));
                }
            }

            return face;
        }

        public static RewardCardFace OfRelic(PendingCard card)
        {
            var row = RelicRows.Row(card.RelicId);
            return new RewardCardFace
            {
                Name = row.Name,
                Hex = "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(RarityColors.For(row.Rarity)),
                PlinthStep = PlinthStepOf(row.Rarity),
                Feature = row.Description,
            };
        }

        // The focus detail (C5): everything the card changes, for the shared
        // tooltip. Pure reads; nothing is equipped.
        public readonly struct Detail
        {
            public readonly string Title;
            public readonly string Body;

            public Detail(string title, string body)
            {
                Title = title;
                Body = body;
            }
        }

        public static Detail DetailOfGear(SaveData save, PendingCard card)
        {
            var copy = card?.Copy;
            if (copy == null) return new Detail("", "");

            var recipient = save?.roster?.FirstOrDefault(c => c != null && c.definitionId == card.RecipientId);
            var lines = new List<string>
            {
                UiStrings.ReckoningOfferMeta.Format(ItemQuality.DisplayName(ContentDatabase.QualityOf(copy)), copy.Tier),
                UiStrings.ReckoningForSlot.Format(
                    UiStrings.ReckoningFor.Format(RewardApplier.DisplayNameFor(card.RecipientId)).ToUpperInvariant(),
                    UiStrings.EquipmentSlotName(card.Slot).Format()),
            };

            if (card.Inert)
            {
                lines.Add(UiStrings.ReckoningInert.Format());
            }
            else if (recipient != null && card.Outcome == CardOutcome.Open)
            {
                var pool = ContentDatabase.PrimaryPoolOf(recipient.definitionId);
                var definition = ContentDatabase.GetCharacter(recipient.definitionId);
                bool signature = definition?.Data != null && definition.Data.HasSignatureResource;
                var rows = ItemCompareRows.Build(recipient, copy, ContentDatabase.EffectiveAbilityScores(recipient),
                    pool?.CapacityRule ?? PoolCapacityRule.WisdomDerived, signature).Rows;

                foreach (var r in rows)
                {
                    lines.Add(SheetStats.LabelFor(r.Stat).Format() + "  " + LoadoutCompare.DeltaText(
                        SheetStats.DisplayText(r.Stat, r.Now), SheetStats.DisplayText(r.Stat, r.Next), r.Delta));
                }

                // A weapon's power is not one of the sheet's derived stats, so its
                // swap has no row; the card's own headline says it instead.
                if (rows.Count == 0)
                {
                    string headline = OfGear(save, card).DeltaText;
                    lines.Add(headline.Length > 0 ? headline : UiStrings.ReckoningNoChange.Format());
                }
            }

            lines.AddRange(AllAffixLines(copy, recipient));
            string lost = card.Outcome == CardOutcome.Open ? LostAffixName(recipient, card) : "";
            if (lost.Length > 0) lines.Add(UiStrings.ReckoningLoses.Format(lost));

            return new Detail(RarityColors.NameOf(copy), string.Join("\n", lines));
        }

        // Relics: who gains, who already has the mechanic, who it never reaches
        // (C5's three portrait states, as text lines until the portraits row
        // has art).
        public static Detail DetailOfRelic(SaveData save, PendingCard card)
        {
            var row = RelicRows.Row(card.RelicId);
            var relic = ContentDatabase.GetRelic(card.RelicId)?.Data;
            var gains = new List<string>();
            var redundant = new List<string>();
            var unreached = new List<string>();

            foreach (var member in save?.ActiveSquad() ?? new List<Character>())
            {
                if (member == null) continue;

                string name = RewardApplier.DisplayNameFor(member.definitionId);
                if (relic == null || !relic.ReachesCharacter(member.definitionId)) unreached.Add(name);
                else if (card.AddsNothingFor != null && card.AddsNothingFor.Contains(member.definitionId)) redundant.Add(name);
                else gains.Add(name);
            }

            var lines = new List<string> { row.RarityText, row.Description };
            if (gains.Count > 0) lines.Add(UiStrings.ReckoningTipGains.Format(string.Join(", ", gains)));
            if (redundant.Count > 0) lines.Add(UiStrings.ReckoningAddsNothing.Format(string.Join(", ", redundant)));
            if (unreached.Count > 0) lines.Add(UiStrings.ReckoningTipUnreached.Format(string.Join(", ", unreached)));

            return new Detail(row.Name, string.Join("\n", lines));
        }

        private static void SetDelta(RewardCardFace face, Character recipient, PendingCard card)
        {
            if (recipient == null || card.Inert) return;

            face.HasDelta = true;
            if (!GearEvaluator.LargestDelta(recipient, card.Copy, card.Slot, out int axis, out int delta)) return;

            face.DeltaSign = delta > 0 ? 1 : delta < 0 ? -1 : 0;
            string signed = (delta > 0 ? "+" : "") + delta;
            switch (axis)
            {
                case 0: face.DeltaText = Glyphed("physical", signed); break;
                case 1: face.DeltaText = UiStrings.ReckoningDeltaHealth.Format(signed); break;
                case 2: face.DeltaText = UiStrings.ReckoningDeltaDefence.Format(signed); break;
                case 3: face.DeltaText = UiStrings.ReckoningDeltaSpeed.Format(signed); break;
                default: face.DeltaText = Glyphed("mana", signed); break;
            }
        }

        // The description icon set's inline sprite for a token, then the number.
        private static string Glyphed(string token, string signed) =>
            UiStrings.ReckoningDeltaGlyph.Format(
                DescriptionMarkup.Expand("{" + token + "}", DescriptionRenderCaps.Full), signed);

        // The affix with the largest scaled magnitude; the first rolled one on
        // a tie. Empty for a copy with none.
        private static string StrongestAffixLine(ItemInstance copy, Character viewer)
        {
            if (copy.ModifierIds == null || copy.ModifierIds.Count == 0) return "";

            var scaled = ContentDatabase.ModifierEffectsForItem(copy.Tier, copy.ModifierIds);
            if (scaled.Count == 0) return "";

            var strongest = scaled[0];
            foreach (var pair in scaled)
            {
                if (System.Math.Abs(pair.Effect.Magnitude) > System.Math.Abs(strongest.Effect.Magnitude)) strongest = pair;
            }

            var effects = scaled
                .Where(pair => pair.Modifier.id == strongest.Modifier.id)
                .Select(pair => new ModifierAffixLines.AffixEffect(
                    pair.Modifier.id, pair.Modifier.Data.DisplayName, pair.Effect))
                .ToList();

            var lines = ModifierAffixLines.LinePairs(effects, viewer == null ? null : ContentDatabase.PrimaryPoolOf(viewer.definitionId));
            return lines == null || lines.Count == 0 ? "" : lines[0].Line;
        }

        // Every affix of the copy, one line each, scaled at the copy's tier.
        private static List<string> AllAffixLines(ItemInstance copy, Character viewer)
        {
            var result = new List<string>();
            if (copy.ModifierIds == null || copy.ModifierIds.Count == 0) return result;

            var effects = ContentDatabase.ModifierEffectsForItem(copy.Tier, copy.ModifierIds)
                .Select(pair => new ModifierAffixLines.AffixEffect(
                    pair.Modifier.id, pair.Modifier.Data.DisplayName, pair.Effect))
                .ToList();
            var lines = ModifierAffixLines.LinePairs(effects, viewer == null ? null : ContentDatabase.PrimaryPoolOf(viewer.definitionId));
            if (lines != null) result.AddRange(lines.Select(l => l.Line));
            return result;
        }

        // The worn copy's affixes the card does not carry: the first one, by
        // name. Empty slot or nothing lost reads empty.
        private static string LostAffixName(Character recipient, PendingCard card)
        {
            var worn = recipient?.equipment == null || recipient.equipment.IsEmpty(card.Slot)
                ? null
                : recipient.equipment.GetModifierIds(card.Slot);
            if (worn == null || worn.Count == 0) return "";

            var kept = new HashSet<string>(card.Copy.ModifierIds ?? new List<string>());
            foreach (string id in worn)
            {
                if (kept.Contains(id)) continue;

                var modifier = ContentDatabase.GetModifier(id);
                return modifier == null || string.IsNullOrEmpty(modifier.Data.DisplayName) ? id : modifier.Data.DisplayName;
            }

            return "";
        }
    }
}
