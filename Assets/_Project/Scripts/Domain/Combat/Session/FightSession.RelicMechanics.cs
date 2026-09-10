using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // Relic mechanics that don't reduce to a RelicModifier/ModifierEffectSet
    // numeric bonus -- each one is a genuinely unrelated piece of behavior,
    // wired through this session's own hooks (RelicsOnCombatBegin,
    // RelicsAfterSwing, RelicsOnEachKill, DealDamage) rather than any shared
    // table. See FightSession.Relics.cs's own header for why that split is a
    // decision, not an oversight: the WHEN is shared there, the WHAT is not,
    // and stays C#. Fourteen relics across two balance passes, previously
    // split across this file and a "BalanceRelics2" sibling by which patch
    // added them rather than by what they do -- merged here because "when"
    // is not a topic. Jar of Bear Urine, Magic Marker, World Ender's Crown,
    // Cursed Idol, Amassing Star, Rampaging Bull's Horn, Ice Fingernail,
    // Loaded Dice, Sparring Saber, Sparring Buckler, Dancer's Anklet,
    // Essence Siphon, Disgruntled Lackey, Inconspicuous Key, and Phoenix
    // Egg (its relic check lives in FightSession.Ledger.cs, next to the
    // damage funnel it intercepts; the hatch/absorb/tick mechanics are
    // here). Monkey King's Scepter's one line lives in FightSession.cs's
    // CanReach, next to the rule it bypasses. Pointy Nail on the End
    // of a Stick, Jo-Sun's Book of Anatomy, and Vampire Dentures have no
    // code here at all -- each is a pure RelicModifier relic, applied at
    // kit-build time exactly like every other numeric-only relic.
    //
    // Each mechanic here that has a reusable shape rides its own Domain
    // facility (Marks, Fear, FallingOffStacks, RunWideBonusDamagePercent,
    // CombatantState.ArmorPenetration, ConvergenceGate) rather than being
    // wired as one-off logic private to the relic -- see each facility's
    // own header for why, and for the other things it is meant to serve
    // besides the one relic that happens to be first through it.
    public partial class FightSession
    {
        // ---- jar of bear urine ----------------------------------------------------

        // Called once from Begin(), before the first GrantTurnStart -- every
        // enemy on the field is marked from the very first beat, whichever
        // side acts first.
        private void RelicsOnCombatBegin()
        {
            foreach (var kit in _playerKits)
            {
                var actor = kit.Key;
                if (!HasRelic(actor, RelicEffect.JarOfBearUrine)) continue;

                foreach (var enemy in _encounter.LivingEnemies)
                {
                    Marks.Apply(enemy, actor);
                }

                AppendMessage($"{actor.Name} uncorks the jar - every enemy reeks, and is marked.");
            }

            LoadedDiceOnCombatBegin();
        }

        // ---- magic marker -----------------------------------------------------------

        // Every spell that lands marks its target -- called from the same
        // three call sites the Drowned Lantern's own ApplyMark already uses
        // (FightSession.Skills.cs), through the SHARED Marks facility
        // rather than the Lantern's private HashSet. The two marks are
        // independent: a target can carry both at once, and each is
        // consumed by its own relic only.
        private void MagicMarkerApplyMark(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || !target.IsAlive) return;
            if (!HasRelic(actor, RelicEffect.MagicMarker)) return;

            Marks.Apply(target, actor);
        }

        // An ATTACK against a marked target consumes it and refunds 20% of
        // the actor's own missing primary resource. Hooked from
        // RelicsAfterSwing, the same funnel Dual Wield's second hit and
        // Sword in a Box's bonus attack both already pass through -- a
        // double attack can consume two independent marks (there is only
        // ever one mark on a given target at a time, so in practice this
        // means the SECOND swing finds nothing left to consume unless a
        // fresh cast re-marked the target in between).
        private void MagicMarkerConsumeOnAttack(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null) return;
            if (!HasRelic(actor, RelicEffect.MagicMarker)) return;
            if (!Marks.ConsumeMark(target)) return;

            int restored = RestorePrimaryResource(actor, FightTuning.MagicMarkerRestorePercent);
            if (restored > 0)
            {
                AppendMessage($"{actor.Name}'s mark ignites - {restored} restored!");
            }
        }

        // RESOURCE-AGNOSTIC: Shawn's Wool (Signature) if he has one, mana
        // otherwise -- "primary resource" reads as whichever pool the
        // actor's own kit actually spends to act, and every combatant has
        // at most one of the two. Returns how much was actually restored,
        // so a caller with nothing missing can skip its own message rather
        // than announcing a zero.
        private int RestorePrimaryResource(CombatantState actor, int percentOfMissing)
        {
            if (actor == null || percentOfMissing <= 0) return 0;

            if (actor.SignaturePool != null)
            {
                int missing = actor.SignaturePool.Max - actor.SignaturePool.Current;
                int restore = missing * percentOfMissing / 100;
                return restore > 0 ? actor.SignaturePool.Gain(restore) : 0;
            }

            var primary = actor.PrimaryPool;
            if (primary != null && primary.Max > 0)
            {
                int missing = primary.Max - primary.Current;
                int restore = missing * percentOfMissing / 100;
                if (restore <= 0) return 0;

                // Through RestoreMana rather than Gain, so a pool that
                // refuses mana effects refuses this too: the relic's own
                // wording is "restores mana", and a pool the satchel cannot
                // fill must not be fillable by a trinket either. Returns what
                // actually landed, which is 0 for such a pool -- and the
                // caller then says nothing rather than announcing a zero.
                return CombatMath.RestoreMana(actor, restore);
            }

            return 0;
        }

        // ---- world ender's crown -----------------------------------------------------

        // Who has already fired since the last time they were above the
        // threshold -- the "re-arms above 30%" half of the spec. A
        // session-scoped set rather than a field on CombatantState: this is
        // per-FIGHT bookkeeping about a per-fight relic, the same shape
        // _marked (FightSession.Relics.cs) already uses for the same
        // reason.
        private readonly System.Collections.Generic.HashSet<CombatantState> _crownFired =
            new System.Collections.Generic.HashSet<CombatantState>();

        // Called from DealDamage (FightSession.Ledger.cs) after EVERY hit
        // that changes a player's health -- the one funnel every damage
        // path already shares, so a crossing can never be missed because
        // one of several damage call sites forgot to check for it.
        private void WorldEndersCrownCheck(CombatantState target)
        {
            if (target == null || !target.IsPlayerSide || target.MaxHealth <= 0) return;
            if (!HasRelic(target, RelicEffect.WorldEndersCrown)) return;

            float fraction = (float)target.CurrentHealth / target.MaxHealth;

            if (fraction >= FightTuning.WorldEndersCrownHealthFraction)
            {
                // Back above the line -- re-arm for the next crossing.
                _crownFired.Remove(target);
                return;
            }

            if (!target.IsAlive || !_crownFired.Add(target)) return;

            foreach (var enemy in _encounter.LivingEnemies)
            {
                Fear.Apply(enemy, FightTuning.WorldEndersCrownFearTurns, target);
            }

            AppendMessage($"{target.Name}'s crown flares as they falter - every enemy recoils in fear!");
        }

        // ---- cursed idol --------------------------------------------------------------

        // Every hit the wearer lands on an enemy adds a stack (mechanic c),
        // called from DealDamage. AddStack no-ops past the cap on its own.
        private void CursedIdolOnHit(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null || target.IsPlayerSide) return;
            if (!HasRelic(actor, RelicEffect.CursedIdol)) return;

            FallingOffStacks.AddStack(target, FightTuning.CursedIdolStackKey,
                FightTuning.CursedIdolStackTurns, FightTuning.CursedIdolMaxStacks);
        }

        // The bonus itself, read inside DealDamage (FightSession.Ledger.cs)
        // rather than TotalDamage -- the stack lives on the TARGET and
        // DealDamage is the one call site that already has both actor and
        // target in hand where TotalDamage's own callers do not all carry
        // one uniformly. "Lowered resistance" is spent as bonus damage on
        // the SAME hit that is landing, the same "additive bonus folded
        // into the one funnel" shape NecklaceDamageBonus already uses.
        private int CursedIdolBonus(CombatantState actor, CombatantState target, int outcomeDamage)
        {
            if (actor == null || target == null || outcomeDamage <= 0) return 0;
            if (!HasRelic(actor, RelicEffect.CursedIdol)) return 0;

            int percent = FallingOffStacks.Magnitude(target, FightTuning.CursedIdolStackKey,
                FightTuning.CursedIdolPercentPerStack,
                FightTuning.CursedIdolPercentPerStack * FightTuning.CursedIdolMaxStacks);

            return percent <= 0 ? 0 : outcomeDamage * percent / 100;
        }

        // ---- amassing star ------------------------------------------------------------

        public int BonusDamagePercentEarned { get; private set; }

        // Called from RelicsOnEachKill (FightSession.Relics.cs), the ONE
        // per-body hook every other per-kill relic (the Bounty Hunter
        // Contract) already shares -- a splash that fells two enemies pays
        // twice, same as the bounty.
        private void AmassingStarOnKill(CombatantState actor, CombatantState victim)
        {
            if (actor == null || victim == null || !actor.IsPlayerSide) return;
            if (victim.IsSummon) return;
            if (!HasRelic(actor, RelicEffect.AmassingStar)) return;

            BonusDamagePercentEarned += FightTuning.AmassingStarPercentPerKill;
            AppendMessage($"{actor.Name}'s star grows brighter - the run itself hits harder now.");
        }

        // ---- rampaging bull's horn ------------------------------------------------------

        // Called from RelicsAfterCast (FightSession.Relics.cs) with the real
        // cast skill -- BasicSpell, the free action that never counted as a
        // convergence ability, is gone (docs/PLAN_SHOP.md §4 Phase E).
        private void RampagingBullsHornOnConvergence(CombatantState actor, ResolvedSkill skill)
        {
            if (actor == null || skill.Effect != SkillEffect.Transform) return;
            if (!HasRelic(actor, RelicEffect.RampagingBullsHorn)) return;

            // Protect is the existing "incoming damage reduced by Magnitude
            // percent, decays by turn count" status -- exactly this relic's
            // shape, so it needs no status of its own. Refresh-not-stack
            // (StatusEffects.Apply's own rule) means re-casting a
            // convergence ability while the reduction still stands never
            // compounds it.
            StatusEffects.Apply(actor.Statuses, StatusEffectType.Protect,
                FightTuning.BullsHornReductionPercent, FightTuning.BullsHornDurationTurns, actor);

            AppendMessage($"{actor.Name}'s horn lowers - the next blows land softer.");
        }

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

        // ---- the shared "an action moved someone on the field" event ----------------
        //
        // RENAMED FROM NotePositionChanged, and the rename is the fix. "A
        // position changed" was true of four different things -- a push down
        // the turn order, a pull to the front of it, Dancer's Anklet's own
        // automatic pull, and a Move -- and only the last of those is a
        // change of FIELD position, which is what the two Sparring relics
        // are about ("footwork", "pivots on a boot heel"). The three
        // turn-order sites no longer fire this: a relic about where you
        // stand should not pay out for where you stand in a QUEUE.
        //
        // `mover` is whoever changed places; `actingCharacter` is whoever's
        // action did it. Move fires it twice -- once for the character who
        // chose to move (mover == actingCharacter, which is what Sparring
        // Saber pays for) and once for the partner they displaced (which
        // Sparring Buckler pays the acting character for, and Saber does
        // not). Death compaction deliberately fires nothing: nobody chose
        // it, and it is not an action.
        //
        // Today Move is the only caller. A future shove/swap skill fires the
        // same note rather than growing a second one.
        private void NoteDeliberateMove(CombatantState mover, CombatantState actingCharacter)
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

            // Sparring Buckler: any action of the acting character's that
            // changes ANY position on the field, once per turn. "Ward" reuses this game's existing
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
        // "You may move one position" is granted automatically, toward the
        // FRONT of the TURN ORDER (PullToFront) -- tempo, not footwork. It
        // is deliberately NOT wired to the field-position Move the player
        // now has: the two are different axes, and a relic that quietly
        // dragged the wearer up the battle line every turn would fight the
        // formation the player is choosing. Once per turn, so three actions
        // in one turn cannot pull three times.
        //
        // FIRES NO SPARRING NOTE any more (see NoteDeliberateMove): this
        // moves nobody on the field.
        private void DancersAnkletReposition(CombatantState actor)
        {
            if (actor == null || !HasRelic(actor, RelicEffect.DancersAnklet)) return;
            if (!_locks.OncePerTurn(FightTuning.DancersAnkletLockKeyFor(LedgerIdOf(actor)))) return;
            if (!_encounter.PullToFront(actor)) return;

            AppendMessage($"{actor.Name} slips a step forward in the order.");
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
            DealDamage(holder, summoner, damage, AttackTypeOf(holder), KillCredit.Attacker);
            SetStance(summoner, summoner.IsAlive ? Stances.Hurt : Stances.Defeated);
            AppendMessage($"{holder.Name}'s grudge answers the summons - {summoner.Name} takes {damage}!");

            if (!summoner.IsAlive)
            {
                AppendMessage($"{summoner.Name} is defeated!");
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
            DealDamage(actor, target, damage, AttackTypeOf(victim), KillCredit.Attacker);
            SetStance(target, target.IsAlive ? Stances.Hurt : Stances.Defeated);
            AppendMessage($"{victim.Name} answers the key's call one last time - {target.Name} takes {damage}!");

            if (!target.IsAlive)
            {
                AppendMessage($"{target.Name} is defeated!");
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
                // to 0 here is what makes the ordinary defeat flow run for
                // the wearer exactly as it would for anyone else -- the kill
                // row and the rider flag from the DealDamage this call is
                // nested inside (SettleDeath sees a target that was alive on
                // the way in and is not on the way out), the message from
                // ApplyFinalDamage's own `!target.IsAlive` check above it. No
                // duplicate bookkeeping needed here.
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
        // World Ender's Crown, Cursed Idol and Phoenix Egg all react to a
        // raw damage event (DealDamage, FightSession.Ledger.cs) rather than
        // to a specific attack/cast path, so a test that wants to pin their
        // arithmetic without fighting a full swing's own variance/scaling
        // needs a way to fire that event directly -- the same reasoning
        // FightSession.Relics.LuckyDeckHealForTest and its siblings already
        // establish for Lucky Deck's own roll.
        //
        // KillCredit.Nobody, deliberately: this seam exists to fire the DAMAGE
        // event alone, and ARealKillGrantsTwoPercentRunWideDamage
        // (RelicMechanicsTests) pins that a relic keyed to kills is NOT reached
        // through it. A test that wants the kill funnel fights a real swing.
        public void DealDamageForTest(CombatantState actor, CombatantState target, int amount, DamageType type) =>
            DealDamage(actor, target, amount, type, KillCredit.Nobody);

        public void HealForTest(CombatantState target, int amount) => HealAndCount(target, amount);

        public void TickPhoenixEggForTest(CombatantState actor) => TickPhoenixEgg(actor);

        // Sparring Saber and Sparring Buckler only ever fire through
        // NoteDeliberateMove, whose one production caller is Move. A direct
        // seam lets each relic's own arithmetic be pinned without standing up
        // a three-member party and a legal move just to reach it.
        public void NoteDeliberateMoveForTest(CombatantState mover, CombatantState actingCharacter) =>
            NoteDeliberateMove(mover, actingCharacter);

        // Disgruntled Lackey fires from ResolveSummon, which this codebase
        // otherwise only reaches through an authored enemy ability's own AI
        // draw -- not something a hand-built EditMode fixture can trigger
        // without content. Calling it directly is the same "exercise the
        // real production method, skip only the unreachable entry point"
        // reasoning FightSession.Relics' own LuckyDeckHealForTest etc. use.
        public void ResolveSummonForTest(CombatantState actor, ResolvedSkill skill) => ResolveSummon(actor, skill);
    }
}
