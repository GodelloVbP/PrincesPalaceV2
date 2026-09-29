using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // CRITICAL HITS -- the whole rule in one place (PLAN_BJORN_CONSTELLATIONS
    // Phase 1).
    //
    // PARTY MEMBERS roll a crit on every damaging swing or cast: a percent
    // chance (CombatantState.CritChancePercent) through the fight's one
    // seeded generator (DamagePipeline.RollCrit -> RandomOps.RollPercent), so
    // the balance bot stays deterministic. A crit multiplies the OUTGOING
    // amount by CombatantState.CritDamagePercent / 100, before any defense --
    // the same side of the armour weapon scaling and the execute bonus sit on
    // (see CombatMath.ScaledAttack's header for why a bonus on the finished
    // figure would be worth less against exactly the armour it must beat).
    //
    // ENEMIES NEVER ROLL. Their chance is ignored outright (ChanceFor answers
    // 0 for anything not on the player side), so "no random spikes" holds. An
    // enemy ability -- or its plain swing -- can instead be AUTHORED to crit
    // (EnemyAbility.Crits): that crit is guaranteed, consumes no draw, and is
    // part of the telegraph (EnemyIntent.WillCrit / ExpectedDamage).
    //
    // HEALS NEVER CRIT. They never reach DamagePipeline, which is the only
    // place a crit is applied.
    public static class CritRules
    {
        // First values, owner-set. Tune here, nowhere else.
        public const int BaseChancePercent = 5;
        public const int BaseDamagePercent = 150;

        // A crit never deals LESS than the hit it replaced, whatever a
        // negative bonus says.
        public const int MinDamagePercent = 100;

        // The chance this attacker actually rolls at. 0 for enemies, always --
        // see this class' header. Momentum's live stacks (EinherjarSeams) ride
        // on top of the total here, the one place a chance is read, so the
        // roll, the preview and the bot's expected value all see them.
        public static int ChanceFor(CombatantState attacker) =>
            attacker == null || !attacker.IsPlayerSide
                ? 0
                : ClampChance(attacker.CritChancePercent + attacker.Momentum.CritChanceBonus);

        // Momentum T2's +5% crit damage per stack rides here likewise.
        public static int DamagePercentFor(CombatantState attacker) =>
            attacker == null
                ? BaseDamagePercent
                : Math.Max(MinDamagePercent, attacker.CritDamagePercent + attacker.Momentum.CritDamageBonus);

        // A crit landed: the outgoing amount times the attacker's crit damage.
        // Same rounding as every other multiplier in the damage path
        // (Rounding.AwayFromZero), floored at 1 like every other step.
        public static int Apply(int damage, CombatantState attacker)
        {
            if (damage <= 0) return damage;
            return Math.Max(1, Rounding.AwayFromZero(damage * DamagePercentFor(attacker) / 100f));
        }

        // THE EXPECTED VALUE of a party member's outgoing figure once crits are
        // counted: damage x (1 + chance x bonus). For the bot's scoring only --
        // a pure read, no draw. An enemy's chance is 0, so its figure comes
        // back unchanged; an enemy's AUTHORED crit is exact, not expected, and
        // is applied by the caller that knows about it (FightSession's intent
        // previews), not here.
        public static int ExpectedDamage(int damage, CombatantState attacker)
        {
            int chance = ChanceFor(attacker);
            if (damage <= 0 || chance <= 0) return damage;

            int bonus = DamagePercentFor(attacker) - 100;
            return Math.Max(1, Rounding.AwayFromZero(damage * (1f + chance * bonus / 10000f)));
        }

        private static int ClampChance(int chance) => chance < 0 ? 0 : chance > 100 ? 100 : chance;
    }
}
