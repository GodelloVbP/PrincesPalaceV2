using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // Counting what happened, alongside resolving it.
    //
    // Recorded HERE rather than derived from the beats afterwards, for two
    // reasons the beat system makes unavoidable: a beat carries no damage type,
    // and an AOE beat deliberately reports its LARGEST single hit rather than
    // its total, so summing beats would under-count every volley in the game.
    //
    // The session is also the only layer that can attribute anything.
    // CombatMath.ApplyDamage takes a target and an amount and has never known
    // who swung, which is correct -- it is arithmetic. Only the session holds
    // both ends of a blow.
    public sealed partial class FightSession
    {
        // Never null, so nothing has to guard before counting.
        public CombatLedger Ledger { get; } = new CombatLedger();

        // The id a combatant is counted under.
        //
        // Player kits carry the save-side identity, which is the one that has
        // to match so a fight's totals can fold into a run's. Enemies fall back
        // to their resolved definition id, and anything unrecognised to its
        // display name -- a name is a poor key, but losing the row entirely
        // would be worse and this is the only path that can produce one.
        private string LedgerIdOf(CombatantState combatant)
        {
            if (combatant == null) return null;

            var kit = KitFor(combatant);
            if (kit != null) return kit.Id;

            var enemy = SourceFor(combatant);
            if (enemy?.Source != null && !string.IsNullOrEmpty(enemy.Source.Id)) return enemy.Source.Id;

            return combatant.Name;
        }

        // Applies damage AND counts it. Every in-session call site that used to
        // reach for CombatMath.ApplyDamage goes through here instead, so the
        // ledger cannot fall behind by someone adding a seventh damage path and
        // not knowing about this file.
        private CombatMath.DamageResult DealDamage(
            CombatantState actor, CombatantState target, int amount, DamageType type)
        {
            var result = CombatMath.ApplyDamageDetailed(target, amount);

            // What a shield ate is NOT what the target took. ApplyDamage's int
            // overload returns Absorbed despite its name, which is exactly the
            // confusion this split exists to avoid.
            int toHealth = amount - result.Absorbed;
            if (toHealth < 0) toHealth = 0;

            Ledger.Dealt(LedgerIdOf(actor), type, amount);
            Ledger.Took(LedgerIdOf(target), toHealth, result.Absorbed);

            return result;
        }

        // Damage with no one to blame: a poison tick, a detonation resolving
        // after its applier is already dead. Counted as taken, credited to
        // nobody -- inventing an attacker would put points in a column the
        // player would then not be able to account for.
        private void RecordUnattributedDamage(CombatantState target, int amount)
        {
            Ledger.Took(LedgerIdOf(target), amount);
        }

        // Heals report nothing, so the amount is measured rather than trusted:
        // CombatMath.Heal clamps at max health and a 200-point heal on a
        // character missing 30 restored 30.
        private void HealAndCount(CombatantState target, int amount)
        {
            if (target == null || amount <= 0) return;

            int before = target.CurrentHealth;
            CombatMath.Heal(target, amount);

            Ledger.Restored(LedgerIdOf(target), target.CurrentHealth - before);
        }

        private void RecordKill(CombatantState actor, CombatantState target)
        {
            Ledger.ScoredKill(LedgerIdOf(actor));
            Ledger.WentDown(LedgerIdOf(target));
        }

        // The damage type a combatant's ordinary swing carries. Only player
        // kits declare one; an enemy without one is swinging a weapon.
        private DamageType AttackTypeOf(CombatantState actor) =>
            KitFor(actor)?.AttackType ?? DamageType.Physical;
    }
}
