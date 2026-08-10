using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // What the monsters do, and what they told the player they were going to
    // do. Ported from v1's FightController.Turns.cs.
    //
    // In v1 this ran only inside a loaded Gameplay scene, so the whole of it --
    // intent commitment, the skip paths, taunt redirection, the status rider --
    // was covered by whatever a PlayMode fight happened to walk through. It is
    // ordinary branching logic over ordinary data and belongs here.
    public sealed partial class FightSession
    {
        // The word shown when a monster is telegraphing nothing special. Not a
        // UiString: it is compared against, and it is a sentinel before it is
        // a label.
        public const string IntentAttack = "Attack";

        private readonly Dictionary<CombatantState, string> _intents = new Dictionary<CombatantState, string>();

        // Called whenever it becomes the player's turn, so every living enemy
        // has a declared action for the player to read and plan around.
        public void PrepareEnemyIntents()
        {
            foreach (var enemy in _encounter.LivingEnemies)
            {
                // Already committed; do not re-roll behind the player's back.
                if (_intents.ContainsKey(enemy)) continue;

                var source = SourceFor(enemy)?.Source;
                bool useSkill = source.HasValue
                                && source.Value.HasSkill
                                && (_rng?.NextFloat() ?? 1f) < source.Value.SkillChance;

                _intents[enemy] = useSkill ? source.Value.SkillName : IntentAttack;
            }
        }

        public string IntentFor(CombatantState enemy) =>
            enemy != null && _intents.TryGetValue(enemy, out var intent) ? intent : null;

        // The nameplate's third line, or "" for the overwhelming majority of
        // turns. Two deliberate restrictions, both ported intact:
        //
        // Only SKILLS are telegraphed. "Intent: Attack" on every enemy every
        // turn is noise the player learns to stop reading, which would bury the
        // one line that actually changes a decision. A blank nameplate means
        // "nothing special is coming".
        //
        // Only while the PLAYER's turn is the one being SHOWN. A round resolves
        // in full and is then played back, so by the time an enemy's blow is
        // animating, _intents already holds its commitment for the NEXT turn --
        // the current one was consumed and removed before playback started.
        // Rendering it mid-playback would telegraph the wrong turn entirely.
        //
        // `isPlayerTurn` is passed in rather than read off the encounter for
        // exactly that reason: the caller knows which turn is on SCREEN, and
        // this method must not answer for the one that has already resolved.
        public string TelegraphSuffix(CombatantState enemy, bool isPlayerTurn)
        {
            if (!isPlayerTurn) return "";

            string intent = IntentFor(enemy);
            return string.IsNullOrEmpty(intent) || intent == IntentAttack ? "" : "\n" + intent + "!";
        }

        // Resolves every enemy turn between now and the player's next one, in
        // full, synchronously -- the whole round lands before the view draws a
        // single frame of it, exactly as v1 did.
        public void AutoResolveEnemyTurns()
        {
            while (!_encounter.IsOver && !_encounter.IsPlayerTurn)
            {
                var enemy = _encounter.Current;

                // A status tick (Poison) can kill the very combatant whose turn
                // it was ticking for, inside GrantTurnStart -- before this loop
                // ever gets to resolve an action for them. CombatEncounter only
                // re-checks who is alive inside AdvanceTurn, so without this a
                // dead enemy would still swing once more on the strength of
                // having been alive when the schedule picked it.
                if (!enemy.IsAlive)
                {
                    if (!StepToNextTurn()) break;
                    continue;
                }

                if (ResolveSkippedTurn(enemy))
                {
                    if (!StepToNextTurn()) break;
                    continue;
                }

                ResolveEnemyAction(enemy);

                if (!StepToNextTurn()) break;
            }
        }

        // Hands the turn on and opens the next one. False means the fight ended
        // and the caller should stop.
        //
        // Pairing AdvanceTurn with GrantTurnStart is not optional and missing it
        // here was a real bug in v1: AdvanceAfterAction only grants to whoever is
        // Current the FIRST time it advances, normally an enemy. When this loop
        // then advances PAST that enemy -- to a second enemy, or back to the
        // player -- nothing granted THEM a turn start at all. Wool's GainPerTurn
        // survived unnoticed because GainOnAttack dwarfs it in every test; mana
        // regen has no such cover and would have fired once per fight, ever.
        private bool StepToNextTurn()
        {
            if (_encounter.IsOver)
            {
                // A fight can end on a monster's swing or on a poison tick at
                // turn start, and neither of those passes through the riders --
                // so without this a defeat would never settle.
                ResolveVictory();
                ResolveOutcome();
                return false;
            }

            _encounter.AdvanceTurn();
            GrantTurnStart();
            return !_encounter.IsOver;
        }

        // Two independent reasons a turn is skipped outright rather than
        // resolving a weakened version of it -- a broken stagger meter and a
        // Stun status -- reported together, since either or both can be true at
        // once and the player should see why.
        //
        // Reset and ConsumeStun both happen HERE, the instant the skip is
        // actually spent, rather than inside Deplete/Tick or on a timer, so
        // either one always costs exactly one turn however it was reached.
        private bool ResolveSkippedTurn(CombatantState enemy)
        {
            bool isBroken = enemy.BreakShield != null && enemy.BreakShield.IsBroken;
            bool isStunned = StatusEffects.HasStun(enemy.Statuses);
            if (!isBroken && !isStunned) return false;

            BeginBeat(enemy, enemy);

            if (isBroken) enemy.BreakShield.Reset();
            if (isStunned) StatusEffects.ConsumeStun(enemy.Statuses);

            // A skipped turn still spends the commitment: the monster declared
            // it and then could not deliver, so re-rolling next turn is honest.
            _intents.Remove(enemy);

            AppendMessage(isBroken && isStunned
                ? $"{enemy.Name} is stunned AND still reeling - it cannot act!"
                : isBroken
                    ? $"{enemy.Name} is still reeling and cannot act!"
                    : $"{enemy.Name} is stunned and cannot act!");

            CommitBeat();
            return true;
        }

        private void ResolveEnemyAction(CombatantState enemy)
        {
            // A taunt overrides the AI's own pick entirely. Consumed further
            // down, once the swing has actually happened -- Provoke buys ONE
            // redirected turn, and spending it on a turn the enemy never got to
            // take (it died to a poison tick first) would be a rule the player
            // cannot see working.
            var forced = ForcedTargetFor(enemy);
            var target = forced ?? PickRandomLivingPlayerTarget();
            if (target == null) return;

            BeginBeat(enemy, target);

            // Said AFTER the beat opens, unlike v1, which announced the
            // redirection first and so retro-attached it to the PREVIOUS beat
            // -- the taunt line showed a beat early, alongside the blow that
            // applied the taunt rather than the turn it redirected. Same rule
            // as everywhere else: a line belongs to the moment it describes.
            if (forced != null)
            {
                AppendMessage($"{enemy.Name} can see nothing but {forced.Name}.");
            }

            // Honour whatever was telegraphed. Falling back to a plain attack
            // only when nothing was declared (an enemy that joined mid-round,
            // say) rather than re-rolling, which would make the shown intent a
            // lie after the fact.
            var kit = SourceFor(enemy);
            bool hasSource = kit != null;
            var source = hasSource ? kit.Source : default(ResolvedEnemy);
            string intent = IntentFor(enemy) ?? IntentAttack;
            bool usingSkill = hasSource && source.HasSkill && intent == source.SkillName;
            _intents.Remove(enemy);

            // A skill already holds position through its cast stance. A monster
            // whose PLAIN attack art is itself a stationary pose needs the same
            // treatment, or the view still lunges it toward the target -- and,
            // for a back-row target, climbs the stage -- while the art shows it
            // rooted to the spot. The golem is the case this exists for: its
            // "attack" stance is a byte-for-byte alias of its "cast" stance, a
            // ground-slam with earth spikes rather than a forward strike, and
            // without this the lunge made it read as flying.
            if (!usingSkill && hasSource && source.AttackHoldsPosition)
            {
                HoldActorPosition();
            }

            int damage = CombatMath.ComputeAttackDamage(enemy, target);
            if (usingSkill)
            {
                damage = System.Math.Max(1, Rounding.AwayFromZero(damage * source.SkillPower));
            }

            // The player's armour, on the one path where it matters most: this
            // is the only damage the player ever takes. No enemy authors an
            // attack type, so the funnel reads their claws as untyped and
            // physical resistance stops them, which is the sensible reading of
            // "hits you with whatever it has".
            damage = DamagePipeline.AfterDefences(
                damage, enemy, target,
                attackType: null, weakness: null, resistance: null,
                varianceRange: DamageVarianceRange,
                rng: _rng,
                resolveWard: ResolveWard).Damage;

            // Provoke T2: a goaded enemy swings wide. Applied to the PAIR rather
            // than through the target's own DamageTakenMultiplier, so it blunts
            // the blow aimed at the taunter and nothing else.
            float goaded = StatusEffects.ProvokedDamageMultiplier(enemy, target);
            if (goaded < 1f)
            {
                damage = System.Math.Max(1, Rounding.AwayFromZero(damage * goaded));
            }

            SetStance(enemy, usingSkill ? Stances.Cast : Stances.Attack);

            // A skill's own VFX, recorded the instant the pose is decided --
            // same resolved-now, played-back-later split every other beat field
            // uses. Not gated on the monster actually having art: an unauthored
            // vfxPath just leaves HasSpellAnimation false, the same graceful
            // posture as everywhere else.
            if (usingSkill)
            {
                RecordSpellPresentation(source.VfxPath, source.VfxSeconds, source.VfxImpactFrame, source.SfxPath);
            }

            var landed = CombatMath.ApplyDamageDetailed(target, damage);

            // A fleece thickens in a hard winter: being ground down is itself a
            // way to build. Granted per HIT rather than per point, so a swarm of
            // weak attackers is not a better generator than one real threat.
            GrantSignatureForHitTaken(target);

            if (landed.Absorbed > 0)
            {
                AppendMessage($"{target.Name}'s {target.Signature.DisplayName} soaks {landed.Absorbed} of it.");
            }

            // Last Stand T3. Said out loud because it is otherwise invisible: a
            // combatant left on 1 HP looks exactly like a combatant that was
            // only ever going to be left on 1 HP, and a once-per-fight save the
            // player does not notice spending is one they will spend again
            // expecting it to work.
            if (landed.CheatedDeath)
            {
                AppendMessage($"{target.Name} should be down. {target.Name} refuses.");
            }

            // A taunt is spent by the turn it redirected, not by a countdown.
            StatusEffects.ConsumeProvoke(enemy.Statuses);

            // NOT skill-gated, unlike the VFX above -- a monster whose whole
            // gimmick is that its claws bleed applies it on every landed hit,
            // plain attack or skill either one.
            if (hasSource && source.HasStatus && target.IsAlive)
            {
                StatusEffects.Apply(target.Statuses, source.AppliesStatus.Value, source.StatusMagnitude, source.StatusDuration);
                AppendMessage($"{target.Name} is afflicted with {source.AppliesStatus.Value}!");
            }

            RecordBeatAmount(damage);
            RecordTargetVoice(target);

            // The party member on the receiving end poses too, so a hit taken is
            // visible on the stage and not only in the log.
            SetStance(target, target.IsAlive ? Stances.Hurt : Stances.Defeated);

            AppendMessage(usingSkill
                ? $"{enemy.Name} uses {source.SkillName} on {target.Name} for {damage} damage!"
                : $"{enemy.Name} attacks {target.Name} for {damage} damage!");

            if (!target.IsAlive)
            {
                AppendMessage($"{target.Name} is defeated!");
            }

            CommitBeat();
        }

        private static CombatantState ForcedTargetFor(CombatantState enemy)
        {
            var provoker = StatusEffects.ProvokedBy(enemy);
            return provoker != null && provoker.IsAlive ? provoker : null;
        }

        private CombatantState PickRandomLivingPlayerTarget()
        {
            var living = _encounter.LivingPlayerParty.ToList();
            if (living.Count == 0) return null;
            return _rng == null ? living[0] : living[_rng.NextInt(0, living.Count)];
        }

        // The character's authored baseline plus whatever their talents add on
        // top. Both exist on purpose: the authored field is the character's
        // floor and the talent is the engine above it, which is why a character
        // built around the engine can author 0 and still generate.
        //
        // The Fragile Lamb's engine is NOT here -- it pays inside ResolveWard,
        // at the moment a ward actually meets a hit. Paying it here as well was
        // the first shape and it was wrong twice over: by the time damage has
        // landed the ward has usually been spent, so the evidence is gone, and a
        // poison tick is not "a hit", does not consume a ward, and must not pay
        // for one.
        private void GrantSignatureForHitTaken(CombatantState victim)
        {
            if (victim == null) return;

            GrantSignature(victim,
                (victim.Signature?.GainOnDamageTaken ?? 0)
                + victim.Talents.Best(TalentEffectType.WoolOnHitTaken));
        }

        // Adds to a combatant's signature resource if it has one. A no-op for
        // everyone else, so call sites never have to ask.
        private static void GrantSignature(CombatantState combatant, int amount)
        {
            if (amount > 0)
            {
                combatant?.Signature?.Gain(amount);
            }
        }
    }
}
