using System.Collections.Generic;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Combat.Session
{
    // The temporary changes a fight has made to a party member's stats, as the
    // system menu shows them: "Speed 10 -> 13 (Stampede)" (plan C4).
    //
    // ONLY WHAT THE FIGHT ADDED. A relic's permanent bonus is part of the
    // character's run, and belongs to the RUN tab; the sheet's own figure stays
    // the base this reads against. What is listed here is what ends with the
    // fight, or sooner: speed buffs and maluses (relics, Chilled), a form's
    // grants, and armour shredded away.
    //
    // Read off the same session state the status badges are drawn from
    // (FightHudModel.StatusRowsFor), so the menu cannot name a change the HUD
    // has no badge for.
    public static class FightModifiers
    {
        public static IReadOnlyList<FightStatModifier> For(FightSession session, CombatantState member)
        {
            var modifiers = new List<FightStatModifier>();
            if (member == null) return modifiers;

            if (session != null)
            {
                var kit = session.KitFor(member);
                foreach (var buff in session.ActiveSpeedBuffs(member))
                {
                    if (buff.Granted == 0) continue;
                    modifiers.Add(new FightStatModifier(
                        SheetStat.Speed, buff.Granted, FightHudModel.SpeedSourceName(kit, buff.Source)));
                }
            }

            // A form. Its defence is CONVERTED into attack, so the attack it
            // reports already includes those points, and the two defences show
            // the loss.
            var form = member.Transformation;
            if (form != null)
            {
                if (form.AttackBonus != 0) modifiers.Add(new FightStatModifier(SheetStat.Attack, form.AttackBonus, form.DisplayName));
                if (form.SpeedBonus != 0) modifiers.Add(new FightStatModifier(SheetStat.Speed, form.SpeedBonus, form.DisplayName));
                if (form.PhysicalDefenseConverted != 0)
                {
                    modifiers.Add(new FightStatModifier(SheetStat.PhysicalDefense, -form.PhysicalDefenseConverted, form.DisplayName));
                }

                if (form.MagicalDefenseConverted != 0)
                {
                    modifiers.Add(new FightStatModifier(SheetStat.MagicalDefense, -form.MagicalDefenseConverted, form.DisplayName));
                }
            }

            if (member.PermanentPhysicalDefenseShred > 0)
            {
                modifiers.Add(new FightStatModifier(SheetStat.PhysicalDefense, -member.PermanentPhysicalDefenseShred, UiStrings.FightModifierArmourShred.Format()));
            }

            return modifiers;
        }
    }
}
