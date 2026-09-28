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
    //
    // THE LINE HAS A LENGTH BUDGET, MaxLength characters. On the dialogue
    // stage it shares one box with the result text, and a line-cap result
    // leaves room for one line of effects under it
    // (DialogueStageTextFitTests measures a MaxLength line there). A robbed
    // caravan hands over a whole shelf, one "+1 <name>" per card, which ran
    // to three lines. So a line over budget folds its item grants into one
    // count ("+5 items") where the first grant stood; every other part --
    // gold, XP, heals, relics, the scuffle's loss -- stays word for word,
    // because those are what the player cannot read anywhere else. A line
    // that is over budget with no grants to fold is left as it is: nothing
    // in it is safe to drop.
    public static class EventEffectSummary
    {
        public const int MaxLength = 72;

        // `itemName` resolves an item id to its display name; Domain cannot
        // see the item catalogue, so the caller hands the lookup in.
        public static string Describe(IReadOnlyList<EventEffect> effects, Func<string, string> itemName)
        {
            if (effects == null || effects.Count == 0) return "";

            var parts = new List<string>();
            var isGrant = new List<bool>();
            int grantParts = 0;
            int grantedCount = 0;
            foreach (var effect in effects)
            {
                string part = Describe(effect, itemName);
                if (string.IsNullOrEmpty(part)) continue;

                bool grant = effect.Kind == EventEffectKind.Item;
                parts.Add(part);
                isGrant.Add(grant);
                if (grant)
                {
                    grantParts++;
                    grantedCount += effect.Amount;
                }
            }

            string separator = UiStrings.EventEffectSeparator.Format();
            string line = string.Join(separator, parts);
            if (line.Length <= MaxLength || grantParts == 0) return line;

            var folded = new List<string>();
            bool countPlaced = false;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!isGrant[i])
                {
                    folded.Add(parts[i]);
                    continue;
                }
                if (countPlaced) continue;
                countPlaced = true;
                folded.Add(grantedCount == 1
                    ? UiStrings.EventEffectItemsOne.Format()
                    : UiStrings.EventEffectItemsCount.Format(grantedCount));
            }

            return string.Join(separator, folded);
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
                    // exp with a character reaches that member alone
                    // (RunOrchestrator's Exp case), so the line names them.
                    if (!effect.TargetsOneMember) return UiStrings.EventEffectExp.Format(effect.Amount);
                    return UiStrings.EventEffectExpMember.Format(
                        string.IsNullOrEmpty(effect.CharacterDisplayName) ? effect.CharacterId : effect.CharacterDisplayName,
                        effect.Amount);
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
