using PrincesPalace.Domain.Content;
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
            // Phoenix Egg, already hatched: every further hit eats the
            // egg's OWN pool instead of the wearer's health -- see
            // PhoenixEggAbsorb's own header. Checked first and returns
            // outright: nothing below (Cursed Idol's bonus, the crown's
            // crossing-check) applies to a shell.
            if (target != null && target.IsPhoenixEgg)
            {
                return PhoenixEggAbsorb(actor, target, amount, type);
            }

            // Phoenix Egg, about to hatch: this hit would otherwise be
            // fatal. Intercepted BEFORE Cursed Idol's bonus/CombatMath ever
            // sees it -- the egg replaces the blow entirely rather than
            // softening it. Once per combat (CombatLocks).
            if (target != null && target.IsPlayerSide && amount > 0 && target.CurrentHealth > 0
                && amount >= target.CurrentHealth
                && HasRelic(target, RelicEffect.PhoenixEgg)
                && _locks.OncePerCombat(FightTuning.PhoenixEggLockKeyFor(LedgerIdOf(target))))
            {
                return PhoenixEggHatch(actor, target, amount, type);
            }

            // Cursed Idol's own bonus -- see FightSession.BalanceRelics.
            // CursedIdolBonus's own header for why it lands here rather
            // than inside TotalDamage.
            amount += CursedIdolBonus(actor, target, amount);

            var result = CombatMath.ApplyDamageDetailed(target, amount);

            // Berserker's Vest: getting hit shortens every one of the
            // wearer's own active cooldowns by 1, once per turn. Read
            // BEFORE the crown/idol riders below -- it cares only that
            // damage actually reached this combatant's health, not about
            // anything either of those two mechanics does afterward.
            if (target != null && target.IsPlayerSide && amount > 0
                && HasRelic(target, RelicEffect.BerserkersVest)
                && _locks.OncePerTurn(FightTuning.BerserkersVestLockKeyFor(LedgerIdOf(target))))
            {
                int shortened = ReduceCooldowns(target, FightTuning.BerserkersVestCooldownReduction);
                if (shortened > 0)
                {
                    AppendMessage($"{target.Name}'s vest bristles at the blow - cooldowns tick down.");
                }
            }

            // Both mechanic (c)'s stack and mechanic (b)'s crossing-check
            // read what THIS call just produced -- CursedIdolOnHit stacks
            // on the target being hit, WorldEndersCrownCheck reads that
            // same target's own new health fraction.
            CursedIdolOnHit(actor, target);
            WorldEndersCrownCheck(target);

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

            // World Ender's Crown re-arms on the way back UP too -- a
            // health change that climbs back above 30% has to clear the
            // "already fired" flag exactly like the damage path does, or a
            // healed party would never fear the field again for the rest of
            // the fight.
            WorldEndersCrownCheck(target);

            Ledger.Restored(LedgerIdOf(target), target.CurrentHealth - before);
        }

        // THE ONE PLACE A KILL IS RECORDED, from all four call sites: a plain
        // swing, an all-enemies cast, Shatter's chain, and a transform's splash.
        // Anything that has to happen once per body rather than once per action
        // belongs HERE rather than at whichever of the four happened to be
        // written most recently -- see FightSession.Relics.RelicsOnEachKill for
        // what already learned that lesson once, on the sweep.
        private void RecordKill(CombatantState actor, CombatantState target)
        {
            Ledger.ScoredKill(LedgerIdOf(actor));
            Ledger.WentDown(LedgerIdOf(target));
            RelicsOnEachKill(actor, target);
        }

        // The damage type a combatant's ordinary swing carries. Player kits
        // declare one directly; an enemy falls through to its own authored
        // attackType (see ActorAttackType's own comment, FightSession.
        // Skills.cs) and anything with neither is swinging a weapon.
        private DamageType AttackTypeOf(CombatantState actor) =>
            KitFor(actor)?.AttackType ?? SourceFor(actor)?.Source.AttackType ?? DamageType.Physical;
    }
}
