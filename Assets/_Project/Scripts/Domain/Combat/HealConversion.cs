using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // CURSED BLOOD'S RULE (docs/PLAN_BJORN_CONSTELLATIONS.md section 4,
    // Phase 4 row 4b): while the window is open, every heal that would land on
    // the holder restores NOTHING and is dealt instead as damage of `Type` to
    // every living enemy.
    //
    // THE EFFECTIVE HEAL, NOT THE RAW ONE -- capped by the holder's missing
    // health at that moment (EffectiveAmount). A raw-heal conversion would
    // pay most at full health, the opposite of a low-health tree. Since the
    // holder cannot be healed while it runs, the missing health stays put and
    // every heal in the window converts in full up to that cap.
    //
    // APPLIED IN ONE PLACE: FightSession.HealAndCount (FightSession.Ledger),
    // the funnel every in-fight heal passes through -- skills, potions,
    // relics, Regen ticks, lifesteal and a breaking ward's Mending Fleece
    // heal. It runs FIRST in that funnel, before Ignore Pain's pool
    // (DelayedDamagePool.ReduceByHeal) can take any of it: a converted heal
    // clears no delayed damage (plan section 6, review finding 7).
    //
    // NOT HEALS, so never converted: a revive (Phoenix Egg, the between-fight
    // restore), a transformation's temporary health, a health COST refunded
    // by nothing. Those write CurrentHealth for their own reasons and say so.
    public sealed class HealConversion
    {
        // Void: exists in DamageType and is rarely resisted (plan section 4).
        // A field rather than a constant so a later variant can convert to
        // something else without a second mechanic; nothing sets it today.
        public DamageType Type = DamageType.Void;

        public readonly TurnWindow Window = new TurnWindow();

        public bool IsActive => Window.IsOpen;

        // The part of `amount` that would actually have restored health: the
        // heal clamped to what is missing. 0 for a full-health or dead holder
        // (a corpse is not healed, and converting a heal aimed at one would
        // be damage from nothing).
        public static int EffectiveAmount(CombatantState target, int amount)
        {
            if (target == null || amount <= 0 || !target.IsAlive) return 0;

            int missing = target.MaxHealth - target.CurrentHealth;
            return Math.Max(0, Math.Min(amount, missing));
        }
    }
}
