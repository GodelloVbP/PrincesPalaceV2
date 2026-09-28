using System;
using System.Collections.Generic;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Events
{
    // The effects line under an event's result text (plan contract 11):
    // "+25 gold · Party healed 30%". Built from the effects that were
    // APPLIED, not from the ones authored, so the line cannot claim a spend
    // the purse did not see.
    //
    // A COUNTER IS NOT SHOWN. It is bookkeeping for a later outcome ("the
    // tenth toss"), and printing "+1 wishing_well_tosses" would put an id
    // in front of the player.
    public static class EventEffectSummary
    {
        // `itemName` resolves an item id to its display name; Domain cannot
        // see the item catalogue, so the caller hands the lookup in.
        public static string Describe(IReadOnlyList<EventEffect> effects, Func<string, string> itemName)
        {
            if (effects == null || effects.Count == 0) return "";

            var parts = new List<string>();
            foreach (var effect in effects)
            {
                string part = Describe(effect, itemName);
                if (!string.IsNullOrEmpty(part)) parts.Add(part);
            }

            return string.Join(UiStrings.EventEffectSeparator.Format(), parts);
        }

        private static string Describe(EventEffect effect, Func<string, string> itemName)
        {
            if (effect == null) return "";

            switch (effect.Kind)
            {
                case EventEffectKind.Gold:
                    if (effect.Amount > 0) return UiStrings.EventEffectGoldGain.Format(effect.Amount);
                    if (effect.Amount < 0) return UiStrings.EventEffectGoldSpend.Format(-effect.Amount);
                    return "";
                case EventEffectKind.HealPercent:
                    if (!effect.TargetsOneMember) return UiStrings.EventEffectHeal.Format(effect.Amount);
                    return effect.Amount >= 100
                        ? UiStrings.EventEffectHealMemberFull.Format(effect.CharacterDisplayName)
                        : UiStrings.EventEffectHealMember.Format(effect.CharacterDisplayName, effect.Amount);
                case EventEffectKind.DamagePercent:
                    return UiStrings.EventEffectDamage.Format(effect.Amount);
                case EventEffectKind.Exp:
                    return UiStrings.EventEffectExp.Format(effect.Amount);
                case EventEffectKind.Item:
                {
                    string name = itemName?.Invoke(effect.Item);
                    if (string.IsNullOrEmpty(name)) name = effect.Item;
                    return UiStrings.EventEffectItem.Format(effect.Amount, name);
                }
                case EventEffectKind.Counter:
                    return "";
                case EventEffectKind.Relic:
                    return UiStrings.EventEffectRelic.Format(
                        string.IsNullOrEmpty(effect.RelicDisplayName) ? effect.RelicId : effect.RelicDisplayName);
                case EventEffectKind.PrincesFavor:
                    return UiStrings.EventEffectPrincesFavor.Format(effect.Amount);
                case EventEffectKind.FillSpecialPool:
                    return UiStrings.EventEffectFillSpecialPool.Format();
                case EventEffectKind.TakeShelf:
                {
                    // Only the APPLIED record reaches here (ShelfLoss); the
                    // cards handed over are item lines of their own.
                    if (string.IsNullOrEmpty(effect.Item)) return UiStrings.EventEffectShelfNothingLost.Format();
                    string name = itemName?.Invoke(effect.Item);
                    return UiStrings.EventEffectShelfLost.Format(string.IsNullOrEmpty(name) ? effect.Item : name);
                }
                default:
                    return "";
            }
        }
    }
}
