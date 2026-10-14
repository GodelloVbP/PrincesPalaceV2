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
    // WHAT A REWARD PAGE CARD SAYS, as plain strings, so the Reckoning only
    // places them (docs/PLAN_ITEMS.md 1.2, "Card face"). Built from the same
    // readers the sheet and the bot use: GearEvaluator.LargestDelta swaps the
    // copy in on a throwaway recipient, ModifierEffectsForItem scales its
    // affixes at the copy's own tier.
    public sealed class RewardCardFace
    {
        // The name, honing suffix included, and the quality's frame colour.
        public string Name = "";
        public string Hex = "#FFFFFF";

        // "Rare  -  TIER 3" on gear; the rarity word on a relic.
        public string Meta = "";

        // "FOR SHAWN  -  Weapon 1". Empty on a relic.
        public string Recipient = "";

        // The one largest stat change against what the recipient wears in the
        // card's slot. Empty on a relic.
        public string Delta = "";

        // The strongest affix (gear), or the relic's description. An inert
        // copy says so here: it has no stat change to show.
        public string Feature = "";

        // An affix the worn copy has and this card lacks (gear), or the
        // members a relic adds nothing for.
        public string Loses = "";

        public bool Inert;

        public static RewardCardFace OfGear(SaveData save, PendingCard card)
        {
            var face = new RewardCardFace();
            var copy = card?.Copy;
            if (copy == null) return face;

            var recipient = save?.roster?.FirstOrDefault(c => c != null && c.definitionId == card.RecipientId);
            int plus = recipient == null ? 0 : ContentDatabase.HoningOf(recipient, card.Slot);
            var quality = ContentDatabase.QualityOf(copy);

            face.Name = RarityColors.NameOf(copy, plus);
            face.Hex = ItemQuality.HexColor(quality);
            face.Meta = UiStrings.ReckoningOfferMeta.Format(ItemQuality.DisplayName(quality), copy.Tier);
            face.Recipient = UiStrings.ReckoningForSlot.Format(
                UiStrings.ReckoningFor.Format(RewardApplier.DisplayNameFor(card.RecipientId)).ToUpperInvariant(),
                UiStrings.EquipmentSlotName(card.Slot).Format());
            face.Inert = card.Inert;
            face.Feature = card.Inert ? UiStrings.ReckoningInert.Format() : StrongestAffixLine(copy, recipient);

            // What the card would change is a question about an OPEN card: once
            // it is taken the recipient already wears it, and a delta against
            // itself would read "no stat change".
            if (card.Outcome == CardOutcome.Open)
            {
                face.Delta = DeltaText(recipient, card);
                face.Loses = LostAffix(recipient, card);
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
                Meta = row.RarityText,
                Feature = row.Description,
                Loses = card.AddsNothingFor == null || card.AddsNothingFor.Count == 0
                    ? ""
                    : UiStrings.ReckoningAddsNothing.Format(
                        string.Join(", ", card.AddsNothingFor.Select(RewardApplier.DisplayNameFor))),
            };
        }

        private static string DeltaText(Character recipient, PendingCard card)
        {
            if (recipient == null || card.Inert) return "";

            if (!GearEvaluator.LargestDelta(recipient, card.Copy, card.Slot, out int axis, out int delta))
            {
                return UiStrings.ReckoningNoChange.Format();
            }

            string signed = (delta > 0 ? "+" : "") + delta;
            switch (axis)
            {
                case 0: return UiStrings.ReckoningDeltaDamage.Format(signed);
                case 1: return UiStrings.ReckoningDeltaHealth.Format(signed);
                case 2: return UiStrings.ReckoningDeltaDefence.Format(signed);
                case 3: return UiStrings.ReckoningDeltaSpeed.Format(signed);
                default: return UiStrings.ReckoningDeltaMana.Format(signed);
            }
        }

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

        // The worn copy's affixes the card does not carry: the first one, by
        // name. Empty slot or nothing lost reads empty.
        private static string LostAffix(Character recipient, PendingCard card)
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
                string name = modifier == null || string.IsNullOrEmpty(modifier.Data.DisplayName) ? id : modifier.Data.DisplayName;
                return UiStrings.ReckoningLoses.Format(name);
            }

            return "";
        }
    }
}
