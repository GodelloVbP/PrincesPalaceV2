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

        // Applies damage, counts it, AND settles the death it may have caused.
        // Every in-session call site that used to reach for
        // CombatMath.ApplyDamage goes through here instead, so the ledger
        // cannot fall behind by someone adding a seventh damage path and not
        // knowing about this file -- and since 2026-09-06 the kill bookkeeping
        // rides along, so that seventh path cannot drop half of it either.
        //
        // `credit` has no default on purpose: a caller must say whether its
        // damage scores a kill. See KillCredit for what the two answers cost.
        private CombatMath.DamageResult DealDamage(
            CombatantState actor, CombatantState target, int amount, DamageType type, KillCredit credit)
        {
            // Measured ACROSS the call rather than read after it, so one body
            // can only be settled once. ApplyFinalDamage's elemental and
            // matching-type riders deal their own figures through here after
            // the main hit may already have felled the target, and a second
            // kill row for the same corpse would out-count the fight.
            bool wasAlive = target != null && target.IsAlive;

            var result = ApplyAndCountDamage(actor, target, amount, type);

            if (wasAlive && target != null && !target.IsAlive)
            {
                SettleDeath(actor, target, credit);
            }

            return result;
        }

        // The arithmetic and the counting, with nothing to say about who scored
        // what. Split out of DealDamage only so the alive-before/dead-after
        // check above can wrap every path through it, the two Phoenix Egg
        // early-returns included -- an egg whose own pool reaches zero kills
        // its wearer from inside this method and must settle like any other
        // death.
        private CombatMath.DamageResult ApplyAndCountDamage(
            CombatantState actor, CombatantState target, int amount, DamageType type)
        {
            // THE POOLS HEAR THE BLOW FIRST, BEFORE ANY EXIT CAN SKIP THEM.
            //
            // This block used to sit at the bottom, beside the ledger rows,
            // which meant the two Phoenix Egg early returns below jumped
            // straight over it: every hit on a shell, and the fatal blow a
            // hatch replaces, paid the victim's pool nothing and left their
            // turn reading idle. Hoisted to the top so the claim this method's
            // own comment makes -- one funnel, and no exit from it that the
            // pools do not hear -- is true of every RETURN as well as of every
            // caller.
            //
            // Read on the raw amount, ahead of Cursed Idol's bonus: whether
            // this was a damaging action is a fact about the blow that was
            // thrown, not about a relic that made it bigger.
            NoteDamageForPools(actor, target, amount);

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
                && _locks.OncePerCombat(target, FightTuning.PhoenixEggLockKey))
            {
                return PhoenixEggHatch(actor, target, amount, type);
            }

            // Cursed Idol's own bonus -- see FightSession.BalanceRelics.
            // CursedIdolBonus's own header for why it lands here rather
            // than inside TotalDamage.
            amount += CursedIdolBonus(actor, target, amount);

            // Read BEFORE the blow lands, because it is the only moment the
            // answer exists: ApplyDamageDetailed clamps the target at zero and
            // reports what it dealt, not what there was to deal it to. See the
            // toHealth clamp below.
            int healthBefore = target == null ? 0 : target.CurrentHealth;

            var result = CombatMath.ApplyDamageDetailed(target, amount);

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

            // Berserker's Vest: getting hit shortens every one of the
            // wearer's own active cooldowns by 1, once per turn. Gated on
            // toHealth (POST-absorb), not the raw amount -- a hit a shield
            // or ward ate in full never reached the wearer at all, and
            // "getting hit" is a claim about health lost, not about a swing
            // having been thrown. Read after toHealth is computed for
            // exactly that reason (it used to gate on `amount > 0`, before
            // absorption was even known, so a fully-absorbed hit still
            // shaved a cooldown for a blow that landed on the shield).
            if (target != null && target.IsPlayerSide && toHealth > 0
                && HasRelic(target, RelicEffect.BerserkersVest)
                && _locks.OncePerTurn(target, FightTuning.BerserkersVestLockKey))
            {
                int shortened = ReduceCooldowns(target, FightTuning.BerserkersVestCooldownReduction);
                if (shortened > 0)
                {
                    AppendMessage($"{target.Name}'s vest bristles at the blow - cooldowns tick down.");
                }
            }

            // NO OVERKILL IN DAMAGE TAKEN (AUDIT #124a, owner's call
            // 2026-09-11). CombatLedger's own header says this column is "what
            // reached this combatant's HEALTH", and a 500-point blow on a
            // 10-HP target reached ten of it. Clamped to what there was to
            // reach, so the column says what it is named after.
            //
            // ONLY THE TAKEN SIDE. Ledger.Dealt on the line below still books
            // the whole swing: "damage dealt" is a claim about the blow that
            // was thrown, and these two columns are allowed to disagree for
            // the same reason Absorbed is handed over separately from toHealth.
            //
            // BELOW THE VEST GATE ON PURPOSE, not folded into the clamp above
            // it. Berserker's Vest asks "did this blow reach my health", which
            // is the post-absorb figure it already reads; whether a rider that
            // arrives after the body fell should fire at all is the other half
            // of #124 and is still an open owner's call. This line changes the
            // ledger and nothing else.
            int booked = target != null && toHealth > healthBefore ? healthBefore : toHealth;

            Ledger.Dealt(LedgerIdOf(actor), type, amount);
            Ledger.Took(LedgerIdOf(target), booked, result.Absorbed);

            return result;
        }

        // THE DAMAGE SEAM FOR THE POOLS, and it is here for the same reason
        // the ledger rows are: this is the ONE funnel every point of damage in
        // the session passes through, so a seventh damage path cannot open
        // without the pools hearing about it. Both ends of the blow count --
        // dealing and taking are each "not an idle turn" (the plan's attack
        // point 5); a turn spent on Provoke, an item or a Move is idle and
        // decays.
        //
        // ITS OWN METHOD rather than a block inside ApplyAndCountDamage,
        // because there are two callers and the second is the one that proved
        // a block was the wrong shape: a poison tick applies its own damage
        // inside StatusEffects.Tick and only comes past here afterwards to be
        // counted (RecordUnattributedDamage). While the pool bookkeeping lived
        // inside the funnel's body, poison was damage the pools could not hear
        // -- so a poisoned Bjorn standing still lost 20 health AND 10 Fury a
        // turn, which is precisely backwards for a bar that fills by being
        // ground down.
        //
        // `actor` may be null and that is the unattributed case, not an error:
        // NotePoolActivity and GrantPrimaryOnDamagingAction are both no-ops on
        // one.
        private void NoteDamageForPools(CombatantState actor, CombatantState target, int amount)
        {
            if (amount <= 0) return;

            NotePoolActivity(actor, PoolActivity.Damage);
            NotePoolActivity(target, PoolActivity.Damage);

            // THE PRIMARY POOL'S gainOnAttack. The owner's spec for Fury is
            // "gains when he deals damage"; phase B wired it at the Attack
            // VERB instead, which is where Wool's narrower rule lives, so a
            // Slam that hit for 40 built nothing. Here it fires off any
            // damaging action -- verb or skill -- and off none of the things
            // that reach no damage: a miss returns before this funnel, a heal
            // never enters it.
            //
            // Fired BEFORE CommitBeat, so the Vitals snapshot the HUD replays
            // is the one taken after the gain: the meter moves on the beat
            // that shows the hit, not on the next one.
            GrantPrimaryOnDamagingAction(actor);

            // AND gainOnDamageTaken, at the same seam and for the same reason.
            // It used to sit at the enemy's plain-swing verb beside Wool's,
            // which meant a monster's SKILL, an AOE, a rider and every
            // player-side blow paid the victim nothing. See
            // GrantPrimaryOnDamageTaken for why this one carries no per-turn
            // lock where its gainOnAttack twin does.
            GrantPrimaryOnDamageTaken(target);

            // AND THE SIGNATURE POOL'S, which was left behind at that same verb
            // when the primary pool's half moved here. It had the identical
            // problem for the identical reason -- "He gains wool from being
            // hit" heard a monster's fist and nothing else -- and the ATTACK
            // side of Wool's deliberately narrow rule is untouched by moving
            // it: that one is gainOnAttack, and it still lives at the verb.
            GrantSignatureForHitTaken(target);
        }

        // Damage with no one to blame: a poison tick, a detonation resolving
        // after its applier is already dead. Counted as taken, credited to
        // nobody -- inventing an attacker would put points in a column the
        // player would then not be able to account for.
        //
        // THE POOLS ARE TOLD HERE TOO, which is the whole reason this is not
        // just a Ledger.Took call. A status tick is damage its victim took by
        // every account that matters to a resource pool: it pays
        // gainOnDamageTaken, and it makes the turn it landed in not an idle
        // one. Credit is the only thing missing from it, and credit is a
        // question about the ATTACKER -- passing null says there isn't one
        // rather than skipping the bookkeeping the victim is owed.
        // `absorbed` is the part a signature pool ate before health, kept apart
        // from `amount` for the same reason the funnel's own Ledger.Took call
        // keeps them apart: a point a pool ate was never taken by health, and
        // one number for both would double-count every absorbed point. The
        // pools are told the RAW total, which is what the funnel does too --
        // whether the turn was idle is a fact about the blow, not about how
        // much of it got through.
        private void RecordUnattributedDamage(CombatantState target, int amount, int absorbed = 0)
        {
            Ledger.Took(LedgerIdOf(target), amount, absorbed);
            NoteDamageForPools(null, target, amount + absorbed);
        }

        // Heals report nothing, so the amount is measured rather than trusted:
        // CombatMath.Heal clamps at max health and a 200-point heal on a
        // character missing 30 restored 30.
        //
        // RETURNS THAT MEASUREMENT, because it already had it and every caller
        // that says a number out loud needs it. It was void until 2026-09-11
        // and the delta was computed here, handed to the ledger and thrown
        // away -- so the ledger booked 0 for a heal on a full bar in the same
        // call whose log line said 40 and whose beat floated a green +40. The
        // potion path (FightSession.Items.cs) states the rule the callers now
        // all follow: "the number the player sees is the number that
        // happened".
        private int HealAndCount(CombatantState target, int amount)
        {
            if (target == null || amount <= 0) return 0;

            int before = target.CurrentHealth;
            CombatMath.Heal(target, amount);

            // World Ender's Crown re-arms on the way back UP too -- a
            // health change that climbs back above 30% has to clear the
            // "already fired" flag exactly like the damage path does, or a
            // healed party would never fear the field again for the rest of
            // the fight.
            WorldEndersCrownCheck(target);

            int landed = target.CurrentHealth - before;
            Ledger.Restored(LedgerIdOf(target), landed);
            return landed;
        }

        // THE ONE PLACE A DEATH IS SETTLED, and the reason the pairing it
        // performs can no longer be half-typed.
        //
        // Two things have to happen together when a combatant goes down: the
        // rider block's own flag (read once in AdvanceAfterAction, which is
        // what makes Trample and Bloodlust eligible for the kill) and the
        // ledger's kill row. Until 2026-09-06 both were typed by hand at five
        // separate call sites after five separate DealDamage calls, and one of
        // them -- SplashOntoNeighbours -- had already lost the flag half
        // without anything noticing. Nothing can lose it now: DealDamage is
        // the only damage funnel in the session and it calls this itself.
        //
        // Anything that has to happen once per BODY rather than once per
        // action belongs here rather than at whichever call site happened to
        // be written most recently -- see FightSession.Relics.RelicsOnEachKill
        // for what already learned that lesson once, on the sweep.
        private void SettleDeath(CombatantState actor, CombatantState target, KillCredit credit)
        {
            if (target == null || target.IsAlive) return;

            // THE BODY WENT DOWN, WHOEVER IS TO BLAME -- and that is a
            // question about the VICTIM, so it is settled above the credit
            // guard rather than behind it.
            //
            // CombatLedger splits ScoredKill(actorId) from WentDown(targetId)
            // deliberately: they are questions about two different combatants
            // and only the first is about credit. Until 2026-09-11 the guard
            // below withheld both, which meant "times downed" could not report
            // the one event it exists to report -- every monster swing reaches
            // here with Nobody (FightSession.Enemies.cs), so a party member
            // dying to a monster wrote nothing, and RunStatsController's row
            // and the achievements reading it were structurally always zero.
            Ledger.WentDown(LedgerIdOf(target));

            // Credited to nobody: no kill row, and no rider eligibility. The
            // poison tick in TickStatuses is the one caller that asks for
            // this, and it asks in writing -- an omission there would be
            // indistinguishable from the bug this method exists to kill.
            //
            // The enemy path asks for it too, and its own reason
            // (FightSession.Enemies.cs) is entirely about the RIDER FLAG: an
            // enemy turn resolves inside AdvanceAfterAction, after that method
            // has read and reset _killedThisAction, so a flag raised here would
            // survive to the player's next action and hand them a Trample the
            // enemy earned. That argument covers everything still below this
            // line and never covered the row above it.
            if (credit == KillCredit.Nobody) return;

            _killedThisAction = true;
            Ledger.ScoredKill(LedgerIdOf(actor));
            RelicsOnEachKill(actor, target);
        }

        // The damage type a combatant's ordinary swing carries. Player kits
        // declare one directly; an enemy falls through to its own authored
        // attackType (see ActorAttackType's own comment, FightSession.
        // Skills.cs) and anything with neither is swinging a weapon.
        private DamageType AttackTypeOf(CombatantState actor) =>
            KitFor(actor)?.AttackType ?? SourceFor(actor)?.Source?.AttackType ?? DamageType.Physical;
    }
}
