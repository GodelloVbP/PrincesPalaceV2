using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // The second balance pass's own relics -- Ice Fingernail, Loaded Dice,
    // Monkey King's Scepter, Sparring Saber, Sparring Buckler, Essence
    // Siphon, Disgruntled Lackey, Inconspicuous Key, Dancer's Anklet,
    // Berserker's Vest and Phoenix Egg. (Jo-Sun's Book of Anatomy and
    // Vampire Dentures have no code here at all -- both are pure
    // RelicModifier relics, wired at kit-build time exactly like Pointy
    // Nail on the End of a Stick before them; Monkey King's Scepter's own
    // one line lives in FightSession.cs's CanMeleeReach, next to the rule
    // it bypasses, rather than here.)
    public partial class FightSession
    {
        // ---- ice fingernail --------------------------------------------------------

        // Every landed attack stacks a slow on the target (FallingOffStacks),
        // capped at 4 stacks / 40%. Hooked from RelicsAfterSwing, the same
        // funnel Dual Wield's second hit and Sword in a Box's bonus attack
        // both reach -- each swing gets its own independent stack.
        private void IceFingernailOnAttack(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || !target.IsAlive) return;
            if (!HasRelic(actor, RelicEffect.IceFingernail)) return;

            FallingOffStacks.AddStack(target, FightTuning.IceFingernailStackKey,
                FightTuning.IceFingernailStackTurns, FightTuning.IceFingernailMaxStacks);

            RefreshIceFingernailSpeed(target);
        }

        // Recomputed from the current stack count rather than adjusted by a
        // delta -- the same revoke-then-regrant shape RefreshNecklaceSpeed
        // and RefreshChilledSpeed already use, and for the same reason: a
        // stack can fall off on its own turn-start tick
        // (FightSession.Riders.TickStatuses, via FallingOffStacks.TickAll)
        // with nothing else calling back in, so the malus has to be able to
        // shrink on its own from that same call site too.
        private void RefreshIceFingernailSpeed(CombatantState target)
        {
            if (target == null) return;

            RevokeSpeedBuff(target, RelicEffect.IceFingernail);

            int percent = FallingOffStacks.Magnitude(target, FightTuning.IceFingernailStackKey,
                FightTuning.IceFingernailPercentPerStack,
                FightTuning.IceFingernailPercentPerStack * FightTuning.IceFingernailMaxStacks);

            if (percent > 0)
            {
                GrantSpeedMalusPercent(target, RelicEffect.IceFingernail, percent, turns: 0);
            }
        }

        // ---- loaded dice -------------------------------------------------------------

        // Called once from RelicsOnCombatBegin. Relics are drafted for the
        // whole PARTY (FightEncounterAdapter's own header) -- every party
        // member's kit carries the identical relic list -- so this returns
        // the instant it fires rather than looping every holder the way Jar
        // of Bear Urine's idempotent re-marking can afford to: a second
        // stun would consume a second RNG draw and desync a seeded replay
        // for no gameplay reason, and stunning the same enemy twice reads
        // as a bug, not a bonus.
        private void LoadedDiceOnCombatBegin()
        {
            foreach (var kit in _playerKits)
            {
                var actor = kit.Key;
                if (!HasRelic(actor, RelicEffect.LoadedDice)) continue;

                int livingCount = 0;
                foreach (var e in _encounter.LivingEnemies) livingCount++;
                if (livingCount == 0) return;

                int index = _rng?.NextInt(0, livingCount) ?? 0;
                CombatantState target = null;
                int seen = 0;
                foreach (var e in _encounter.LivingEnemies)
                {
                    if (seen == index) { target = e; break; }
                    seen++;
                }

                StatusEffects.Apply(target.Statuses, StatusEffectType.Stun, 0, 1, actor);
                AppendMessage($"{actor.Name} palms the loaded dice - {target.Name} stumbles, stunned!");
                return;
            }
        }

        // ---- the shared "position changed" event ------------------------------------
        //
        // Fired from every place a combatant's spot in the turn order
        // actually moves: ApplyQueuePush's successful PushBack
        // (FightSession.Skills.cs), Gift: Haste's successful PullToFront
        // (FightSession.Talents.cs), and Dancer's Anklet's own automatic
        // pull below. `mover` is whoever's position changed; `actingCharacter`
        // is whoever's action caused it -- the same combatant for a
        // self-reposition, a different one for a shove or a pull.
        private void NotePositionChanged(CombatantState mover, CombatantState actingCharacter)
        {
            // Sparring Saber: altering YOUR OWN position.
            if (actingCharacter != null && ReferenceEquals(mover, actingCharacter)
                && HasRelic(actingCharacter, RelicEffect.SparringSaber))
            {
                int gained = GrantSpeedPercent(actingCharacter, RelicEffect.SparringSaber,
                    FightTuning.SparringSaberSpeedPercent, FightTuning.SparringSaberSpeedTurns);

                if (gained > 0)
                {
                    AppendMessage($"{actingCharacter.Name} pivots on a boot heel - faster for a moment.");
                }
            }

            // Sparring Buckler: casting an ability that alters ANY
            // position, once per turn. "Ward" reuses this game's existing
            // Shielded status (a percent reduction consumed on the next
            // hit, the same shape Magical Shield and Fleece Ward already
            // use) rather than a flat absorb pool -- there is no such pool
            // anywhere else in this vocabulary, and inventing one for a
            // single relic would be a second system claiming the word
            // "ward" already means something specific here.
            if (actingCharacter != null && HasRelic(actingCharacter, RelicEffect.SparringBuckler)
                && _locks.OncePerTurn(FightTuning.SparringBucklerLockKeyFor(LedgerIdOf(actingCharacter))))
            {
                StatusEffects.Apply(actingCharacter.Statuses, StatusEffectType.Shielded,
                    FightTuning.SparringBucklerWardPercent, FightTuning.MagicalShieldDurationTurns, actingCharacter);
                AppendMessage($"{actingCharacter.Name}'s buckler comes up - a ward from the footwork.");
            }
        }

        // ---- dancer's anklet ----------------------------------------------------------
        //
        // "You may move one position" has no player-facing input to grant
        // in this game (no reposition command exists), so this grants it
        // automatically -- toward the FRONT of the turn order (PullToFront),
        // the sensible default for a relic about earning tempo from your
        // own actions. Once per turn, so three actions in one turn cannot
        // pull three times.
        private void DancersAnkletReposition(CombatantState actor)
        {
            if (actor == null || !HasRelic(actor, RelicEffect.DancersAnklet)) return;
            if (!_locks.OncePerTurn(FightTuning.DancersAnkletLockKeyFor(LedgerIdOf(actor)))) return;
            if (!_encounter.PullToFront(actor)) return;

            AppendMessage($"{actor.Name} slips a step forward in the order.");
            NotePositionChanged(actor, actor);
        }

        // ---- essence siphon -------------------------------------------------------------

        // Called from RelicsOnEachKill, the same per-body hook Amassing
        // Star and the Bounty Hunter Contract already share.
        private void EssenceSiphonOnKill(CombatantState actor, CombatantState victim)
        {
            if (actor == null || victim == null || !actor.IsPlayerSide) return;
            if (victim.IsSummon) return;
            if (!HasRelic(actor, RelicEffect.EssenceSiphon)) return;

            int amount = actor.MaxHealth * FightTuning.EssenceSiphonHealPercent / 100;
            if (amount <= 0) return;

            HealAndCount(actor, amount);
            AppendMessage($"{actor.Name} draws {amount} HP from {victim.Name}'s fading essence.");
        }

        // ---- disgruntled lackey -----------------------------------------------------------
        //
        // Called from ResolveSummon (FightSession.Skills.cs) on a
        // successful enemy summon. Fires for the FIRST living party member
        // carrying the relic -- relics are drafted party-wide (see Loaded
        // Dice's own header for the same reasoning), and there is no
        // "the actor who cast this" on the player's side to anchor "your
        // max HP" to, since it is an ENEMY doing the summoning. The front
        // living party member is the closest honest reading of "you".
        private void DisgruntledLackeyOnEnemySummon(CombatantState summoner)
        {
            if (summoner == null || summoner.IsPlayerSide) return;

            CombatantState holder = null;
            foreach (var p in _encounter.LivingPlayerParty)
            {
                if (!HasRelic(p, RelicEffect.DisgruntledLackey)) continue;
                holder = p;
                break;
            }
            if (holder == null) return;

            int damage = System.Math.Max(1, holder.MaxHealth);
            DealDamage(holder, summoner, damage, AttackTypeOf(holder));
            SetStance(summoner, summoner.IsAlive ? Stances.Hurt : Stances.Defeated);
            AppendMessage($"{holder.Name}'s grudge answers the summons - {summoner.Name} takes {damage}!");

            if (!summoner.IsAlive)
            {
                AppendMessage($"{summoner.Name} is defeated!");
                _killedThisAction = true;
                RecordKill(holder, summoner);
            }
        }

        // ---- inconspicuous key -----------------------------------------------------------
        //
        // "They say hell and heaven have the same master key." The brief
        // asks for the fallen enemy to fight ON THE PLAYER'S SIDE -- this
        // combat model has no facility for that: CombatEncounter.PlayerParty
        // is a fixed list handed to its constructor (only the ENEMY side
        // can grow mid-fight, via TryAddEnemy, for a monster's own Roar-
        // style summon), and a corpse's CombatantState is still keyed into
        // _enemyKits, not _playerKits. Bolting a mid-fight player-roster
        // mutation on top of that for one relic risks exactly the kind of
        // "appears in both OpponentsOf and AlliesOf" desync a small relic
        // has no business introducing to the turn order.
        //
        // TAKEN INSTEAD: the "existing Summon effect path" the brief
        // explicitly allows falling back to, read as narrowly as the
        // engine supports it -- the fallen enemy gets ONE LAST BLOW against
        // one of its own former side, at its own Attack, before it is gone
        // for good. Once per combat. This is a real but smaller effect than
        // "revives as a standing ally"; report this reading rather than
        // calling it the same thing.
        private void InconspicuousKeyOnKill(CombatantState actor, CombatantState victim)
        {
            if (actor == null || victim == null || !actor.IsPlayerSide || victim.IsSummon) return;
            if (!HasRelic(actor, RelicEffect.InconspicuousKey)) return;
            if (!_locks.OncePerCombat(FightTuning.InconspicuousKeyLockKeyFor(LedgerIdOf(actor)))) return;

            var target = RandomLivingEnemy();
            if (target == null) return;

            int damage = System.Math.Max(1, victim.Attack);
            DealDamage(actor, target, damage, AttackTypeOf(victim));
            SetStance(target, target.IsAlive ? Stances.Hurt : Stances.Defeated);
            AppendMessage($"{victim.Name} answers the key's call one last time - {target.Name} takes {damage}!");

            if (!target.IsAlive)
            {
                AppendMessage($"{target.Name} is defeated!");
                _killedThisAction = true;
                RecordKill(actor, target);
            }
        }

        // ---- phoenix egg -----------------------------------------------------------------

        // The fatal blow that would have ended the fight instead turns the
        // wearer into a shell. Called from DealDamage BEFORE Cursed Idol's
        // bonus or CombatMath ever sees the figure -- the egg replaces the
        // blow outright rather than softening it. CurrentHealth is pinned
        // to 1 (never 0) for the WHOLE egg duration, which is what keeps
        // IsAlive true and the fight running while the shell absorbs
        // everything that follows through PhoenixEggAbsorb below.
        private CombatMath.DamageResult PhoenixEggHatch(
            CombatantState actor, CombatantState target, int amount, DamageType type)
        {
            target.CurrentHealth = 1;
            target.IsPhoenixEgg = true;
            target.EggHealth = target.MaxHealth;
            target.EggTurnsRemaining = FightTuning.PhoenixEggDurationTurns;

            Ledger.Dealt(LedgerIdOf(actor), type, amount);
            Ledger.Took(LedgerIdOf(target), 0, 0);

            AppendMessage($"{target.Name} would have fallen - instead they curl into an egg, " +
                           $"{target.EggHealth} HP inside the shell.");

            return new CombatMath.DamageResult(0, false);
        }

        // Every hit against an already-hatched egg lands on the SHELL'S OWN
        // pool, never on the wearer's real health. "The egg can be
        // attacked" -- this is what makes that true: an attacker still
        // rolls a normal mitigated swing against the wearer's real defenses
        // (DamagePipeline resolves before DealDamage is ever called), only
        // where the resulting figure is SPENT changes.
        private CombatMath.DamageResult PhoenixEggAbsorb(
            CombatantState actor, CombatantState target, int amount, DamageType type)
        {
            if (amount <= 0) return new CombatMath.DamageResult(0, false);

            int before = target.EggHealth;
            target.EggHealth = System.Math.Max(0, target.EggHealth - amount);
            int absorbed = before - target.EggHealth;

            Ledger.Dealt(LedgerIdOf(actor), type, amount);
            Ledger.Took(LedgerIdOf(target), absorbed, 0);

            if (target.EggHealth <= 0)
            {
                // The egg's own HP reached 0 -- the wearer dies outright.
                // CurrentHealth was pinned at 1 since the hatch; dropping it
                // to 0 here is what makes the caller's own `!target.IsAlive`
                // check (ApplyFinalDamage) run the ordinary defeat flow
                // (the message, RecordKill) exactly as it would for anyone
                // else -- no duplicate bookkeeping needed here.
                target.IsPhoenixEgg = false;
                target.CurrentHealth = 0;
                AppendMessage($"{target.Name}'s egg shatters. There is nothing left to hatch.");
            }

            return new CombatMath.DamageResult(0, false);
        }

        // The shell's own clock, ticked at the wearer's own turn start
        // (GrantTurnStart) beside every other duration. An egg that reached
        // 0 HP already cleared IsPhoenixEgg and CurrentHealth in
        // PhoenixEggAbsorb above, and a dead combatant's turn never comes
        // back around (CombatEncounter.SkipToNextLivingTurn), so this only
        // ever runs for a shell that is still standing.
        private void TickPhoenixEgg(CombatantState actor)
        {
            if (actor == null || !actor.IsPhoenixEgg) return;

            actor.EggTurnsRemaining--;
            if (actor.EggTurnsRemaining > 0) return;

            float fraction = actor.MaxHealth > 0 ? (float)actor.EggHealth / actor.MaxHealth : 0f;
            int revived = System.Math.Max(1, Rounding.AwayFromZero(actor.MaxHealth * fraction));

            actor.CurrentHealth = System.Math.Min(actor.MaxHealth, revived);
            actor.IsPhoenixEgg = false;
            actor.EggHealth = 0;

            AppendMessage($"{actor.Name} breaks free of the shell, reborn with {actor.CurrentHealth} HP.");
        }

        // An egg cannot act. Mirrors AutoResolveEnemyTurns' own shape: while
        // it is the player's turn and Current is a still-shelled egg, its
        // turn resolves itself (a message, then the schedule advances)
        // rather than waiting on an input this combatant cannot give. Called
        // from every place AutoResolveEnemyTurns already is (Begin,
        // AdvanceAfterAction) -- an egg's own turn can come up immediately
        // after an enemy's, and the loop has to keep clearing them the same
        // way enemy turns already are before control returns to a real
        // player action.
        private void AutoResolveEggTurns()
        {
            while (!_encounter.IsOver && _encounter.IsPlayerTurn
                   && _encounter.Current != null && _encounter.Current.IsPhoenixEgg)
            {
                var actor = _encounter.Current;
                AppendMessage($"{actor.Name} cannot act, sealed inside the shell.");

                _encounter.AdvanceTurn();
                GrantTurnStart();
                AutoResolveEnemyTurns();
            }
        }

        // ---- seams for tests -------------------------------------------------
        //
        // Same reasoning as FightSession.BalanceRelics' own DealDamageForTest/
        // HealForTest: Phoenix Egg reacts to the raw damage funnel rather
        // than to one swing, so a literal-pinned test needs to fire it
        // directly rather than fighting a real attack's own variance and
        // mitigation to land an exact fatal figure.
        public void TickPhoenixEggForTest(CombatantState actor) => TickPhoenixEgg(actor);

        // Sparring Saber only ever fires through NotePositionChanged, and
        // the one production caller that fires it with mover == acting
        // character (a genuine self-reposition) is Dancer's Anklet -- there
        // is no OTHER self-reposition action in the game yet. A direct seam
        // lets Sparring Saber's own arithmetic be pinned without also
        // drafting Dancer's Anklet just to reach it.
        public void NotePositionChangedForTest(CombatantState mover, CombatantState actingCharacter) =>
            NotePositionChanged(mover, actingCharacter);

        // Disgruntled Lackey fires from ResolveSummon, which this codebase
        // otherwise only reaches through an authored enemy ability's own AI
        // draw -- not something a hand-built EditMode fixture can trigger
        // without content. Calling it directly is the same "exercise the
        // real production method, skip only the unreachable entry point"
        // reasoning FightSession.Relics' own LuckyDeckHealForTest etc. use.
        public void ResolveSummonForTest(CombatantState actor, ResolvedSkill skill) => ResolveSummon(actor, skill);
    }
}
