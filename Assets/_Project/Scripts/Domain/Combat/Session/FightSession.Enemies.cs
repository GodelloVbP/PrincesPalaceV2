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

        private readonly Dictionary<CombatantState, EnemyIntent> _intents = new Dictionary<CombatantState, EnemyIntent>();

        // Called whenever it becomes the player's turn, so every living enemy
        // has a declared action for the player to read and plan around.
        //
        // The TARGET is committed here too, which is a deliberate change of
        // when the draw happens: it used to be rolled at resolution time, so
        // nothing could be shown about who was in danger. Rolling it here is
        // what lets the icon say "and it is coming for Shawn". The draw COUNT
        // per enemy turn is unchanged; only its position in the stream moved,
        // so seeded runs stay reproducible but do not reproduce the same fights
        // they did before this.
        public void PrepareEnemyIntents()
        {
            foreach (var enemy in _encounter.LivingEnemies)
            {
                // Already committed; do not re-roll behind the player's back.
                if (_intents.ContainsKey(enemy)) continue;

                // ONE DRAW, from the monster's whole pool.
                //
                // This used to be a coin flip against SkillChance, which is the
                // same draw and the same position in the stream -- so a seeded
                // run keeps its shape -- but it now chooses among everything the
                // monster can do rather than between its two hardcoded options.
                var kit = SourceFor(enemy);
                var pool = EffectivePoolFor(kit?.Abilities);
                int chosen = EnemyAbilityDraw.Pick(pool, _rng?.NextFloat() ?? 0f);

                var target = PickRandomLivingPlayerTarget();
                _intents[enemy] = BuildIntent(enemy, target, pool, chosen);
            }
        }

        // The pool a draw actually considers, with any Summon ability
        // already at its cap weighted to zero for THIS draw. The authored
        // weight on the kit itself is untouched — capped this turn does not
        // mean gone, only skipped over in favour of whatever else the
        // monster can do, exactly the way EnemyAbilityDraw already treats
        // any other zero-weight entry.
        //
        // SAME LENGTH, SAME ORDER as the source pool, always -- the index a
        // draw commits to is looked back up against kit.Abilities directly
        // at resolution time (see ResolveEnemyAction), so this can adjust
        // weights but can never reshuffle or drop an entry.
        private IReadOnlyList<EnemyAbility> EffectivePoolFor(IReadOnlyList<EnemyAbility> abilities)
        {
            if (abilities == null) return null;

            List<EnemyAbility> effective = null;
            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                if (!ability.HasSkill || ability.Skill.Effect != SkillEffect.Summon) continue;

                int living = LivingCountOf(ability.Skill.SummonEnemyId);
                if (living < ability.Skill.SummonCap) continue;

                effective ??= new List<EnemyAbility>(abilities);
                effective[i] = EnemyAbility.Of(ability.Skill, 0f);
            }

            // NOT abilities.ToList(). The copy was defensive against nothing:
            // every consumer takes IReadOnlyList and none of them mutates, so
            // the only thing it bought was a fresh List per enemy per intent
            // draw -- which is the allocation the null-until-needed build
            // directly above exists to avoid.
            return effective ?? abilities;
        }

        private EnemyIntent BuildIntent(CombatantState enemy, CombatantState target,
                                        IReadOnlyList<EnemyAbility> pool, int chosen)
        {
            var source = SourceFor(enemy)?.Source;

            // Nothing to draw from: a monster that joined mid-round, or one
            // whose whole pool was weighted out. It swings.
            if (pool == null || chosen < 0 || chosen >= pool.Count)
            {
                return new EnemyIntent(IntentAttack, EnemyIntentKind.Attack, target,
                    PreviewDamage(enemy, target, false, source, 1f));
            }

            var ability = pool[chosen];

            // THE LEGACY SCALED ATTACK, including the plain swing that sits in
            // every pool. Its magnitude is the basic attack times its own power.
            if (ability.IsLegacyAttack)
            {
                bool isPlainSwing = ability.IsPlainSwing;
                var kind = isPlainSwing
                    ? EnemyIntentKind.Attack
                    : EnemyIntentIcons.KindFor(true,
                        source?.AppliesStatus, source.HasValue && source.Value.HasStatus);

                return new EnemyIntent(ability.Label, kind, target,
                    PreviewDamage(enemy, target, !isPlainSwing, source, ability.Power), chosen);
            }

            // A REAL SKILL. Its own effect decides the icon, the scope and
            // whether the number is a wound or a mend -- see EnemyIntentIcons.
            var skill = ability.Skill;
            var effect = skill.Effect;

            return new EnemyIntent(
                ability.Label,
                EnemyIntentIcons.KindFor(effect, skill.AppliesStatus, skill.AppliesStatus.HasValue),
                target,
                PreviewSkill(enemy, target, skill),
                chosen,
                EnemyIntentIcons.ScopeFor(effect),
                EnemyIntentIcons.HealsFor(effect));
        }

        // What a monster's SKILL would land for.
        //
        // THE SAME CALL ResolveDamageSingle MAKES, argument for argument, and
        // that is not tidiness. The first version left out castType and
        // ScalingAxis -- which decide whether the figure scales off Attack or
        // off Spell -- and promised 370 for a blow that landed 300. A telegraph
        // overstating by a fifth is worse than none: the player budgets a heal
        // they did not need, or declines a trade they could have won.
        //
        // The three things it deliberately does NOT share are the three that
        // would change the fight it is previewing: variance and the ward would
        // consume a draw and spend a shield the player still holds, and both
        // are why the tooltip says "about".
        private int PreviewSkill(CombatantState enemy, CombatantState target, ResolvedSkill skill)
        {
            if (enemy == null) return 0;

            bool heals = EnemyIntentIcons.HealsFor(skill.Effect);
            var against = heals ? enemy : target;
            if (against == null) return 0;

            var castType = ActorAttackType(enemy) ?? DamageType.Physical;

            int raw = SkillResolution.Amount(skill.Effect, enemy, against,
                skill.Power, skill.FlatAmount, 0, skill.IgnoresDefense, castType, skill.ScalingAxis);

            if (heals) return raw;

            return DamagePipeline.AfterDefences(
                raw, enemy, against,
                attackType: castType,
                affinity: AffinityOf(against),
                varianceRange: 0f, rng: null, resolveWard: null).Damage;
        }

        // What the blow would land for, with NOTHING that mutates and NOTHING
        // that draws from the run's generator.
        //
        // Variance, the ward and the Provoke multiplier are all deliberately
        // left out: the first two would consume a draw and spend a shield the
        // player still has, and the third depends on a taunt that may not exist
        // yet when the icon is drawn. The number is therefore a centre, not a
        // promise, and the tooltip says "about" for that reason.
        private static int PreviewDamage(CombatantState enemy, CombatantState target, bool useSkill,
                                         ResolvedEnemy? source, float power)
        {
            if (enemy == null || target == null) return 0;

            int damage = CombatMath.ComputeAttackDamage(enemy, target);
            if (useSkill && source.HasValue)
            {
                damage = System.Math.Max(1, Rounding.AwayFromZero(damage * power));
            }

            return DamagePipeline.AfterDefences(
                damage, enemy, target,
                attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: 0f, rng: null, resolveWard: null).Damage;
        }

        public string IntentFor(CombatantState enemy) =>
            enemy != null && _intents.TryGetValue(enemy, out var intent) ? intent.Label : null;

        // The whole commitment, for the icon above the monster's head.
        public EnemyIntent? IntentDetailFor(CombatantState enemy) =>
            enemy != null && _intents.TryGetValue(enemy, out var intent) ? intent : (EnemyIntent?)null;

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

        // Resolves every enemy turn between now and the player's next REAL one,
        // in full, synchronously -- the whole round lands before the view draws
        // a single frame of it, exactly as v1 did.
        //
        // "Real" is doing work in that sentence since Grapple: a stunned PLAYER
        // turn is a turn nobody can act on either, and belongs on the same
        // auto-skip path an enemy's already took -- not stopped on and handed
        // to a player with no legal action to take. ResolveSkippedTurn already
        // does not care which side its combatant is on; it only ever checked a
        // broken stagger meter and a Stun status, and a player combatant is
        // never built with a BreakShield (see FightEncounterAdapter's two
        // ToCombatant overloads), so the broken half of that check is simply
        // always false for them -- Stun is the only one that can ever fire
        // here, which is exactly what Grapple needs.
        public void AutoResolveEnemyTurns()
        {
            while (!_encounter.IsOver)
            {
                var current = _encounter.Current;

                // A status tick (Poison) can kill the very combatant whose turn
                // it was ticking for, inside GrantTurnStart -- before this loop
                // ever gets to resolve an action for them. CombatEncounter only
                // re-checks who is alive inside AdvanceTurn, so without this a
                // dead combatant would still swing once more on the strength of
                // having been alive when the schedule picked it.
                if (!current.IsAlive)
                {
                    if (!StepToNextTurn()) break;
                    continue;
                }

                if (ResolveSkippedTurn(current))
                {
                    if (!StepToNextTurn()) break;
                    continue;
                }

                // A genuine, unskipped PLAYER turn is where this loop stops and
                // hands control back -- the one case that was always this
                // method's whole reason to return early.
                if (current.IsPlayerSide) break;

                ResolveEnemyAction(current);

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
            // Precedence, and each step is load-bearing:
            //
            //   a taunt   -- the player OVERRODE the declared plan, which is the
            //                one thing allowed to change a telegraphed target
            //   committed -- what the icon promised, honoured
            //   a re-pick -- only if the promised target died in the meantime,
            //                which is the player having removed it
            var forced = ForcedTargetFor(enemy);
            var promised = IntentDetailFor(enemy)?.Target;
            if (promised != null && !promised.IsAlive) promised = null;

            var target = forced ?? promised ?? PickRandomLivingPlayerTarget();
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

            // BY INDEX, not by comparing the label back to a name.
            //
            // That comparison was sound while a monster had exactly one skill
            // and its name was therefore unique. A weighted pool can hold two
            // abilities that share a display name, and matching on text would
            // resolve the wrong one while the telegraph looked correct -- the
            // precise failure a telegraph exists to prevent.
            var committed = IntentDetailFor(enemy);
            var pool = kit?.Abilities;
            var chosen = committed.HasValue && pool != null
                         && committed.Value.AbilityIndex >= 0
                         && committed.Value.AbilityIndex < pool.Count
                ? pool[committed.Value.AbilityIndex]
                : (EnemyAbility?)null;

            _intents.Remove(enemy);

            // A REAL SKILL RUNS THE SKILL PATH -- the same one a player's cast
            // goes through, which is what makes the whole SkillEffect
            // vocabulary available to monsters rather than a second
            // implementation of half of it.
            if (chosen.HasValue && chosen.Value.HasSkill)
            {
                // NO SetStance HERE, and there used to be one. It read
                // SetStance(enemy, StanceFor(skill)) and did nothing at all:
                // SetStance writes into _recordingBeat, the previous beat was
                // committed several lines ago, and the beat this cast belongs
                // to is not opened until BeginBeat inside ResolveCharacterSkill
                // -- which then poses the caster itself. A skill that named its
                // own stance was therefore posed "cast" regardless, silently.
                // The pose now happens where the beat exists; see
                // ResolveDamageSingle.
                ResolveCharacterSkill(enemy, chosen.Value.Skill, target, 0);

                // AND COMMITTED, which it was not.
                //
                // This branch returned straight out, past the CommitBeat at the
                // bottom of this method, and ResolveCharacterSkill does not
                // close its own beat -- the player's two wrappers (UseSkill,
                // CastSkill) each commit after calling it. So a monster casting
                // a REAL skill opened a beat and abandoned it: the next
                // BeginBeat overwrote _recordingBeat and the whole thing went
                // in the bin.
                //
                // Nothing about the fight looked wrong, which is why it lasted.
                // Damage, statuses and kills are model state and had already
                // landed; what the discarded beat was carrying was the entire
                // PRESENTATION -- the spell's frames, the caster's pose, the
                // target's flinch, the floating number and the log line. Every
                // monster with an authored skill has been casting invisibly:
                // the Bog Witch's Mud Burst, the Golem's Boulder Slam, the
                // Warden's Overhead Slam and Grapple. Reported from play as
                // "the Bog Witch doesn't use the mud blast animation", which is
                // exactly what it looks like from the outside.
                //
                // The legacy scaled-attack path below never had the problem --
                // it falls through to the same CommitBeat every plain swing
                // uses -- which is why enemy VFX worked at all and made this
                // read as a content-wiring question rather than a dropped beat.
                CommitBeat();
                return;
            }

            bool usingSkill = chosen.HasValue && !chosen.Value.IsPlainSwing;
            float skillPower = chosen?.Power ?? 1f;

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
            // AND THE PLAIN ATTACK'S OWN APPROACH, when it is not rooted. The
            // beat opened on the Lunge default (BeginBeat, isCast:false), which
            // is what every plain swing has always used; a monster that authored
            // a charge -- the beetle, so its rush actually reaches the party and
            // bumps them -- says so here. Hold wins outright above, so this only
            // fires when the attack moves at all, and it changes nothing for the
            // roster that left attackApproach blank (parsed to Lunge).
            else if (!usingSkill && hasSource && source.AttackApproach != StageApproach.Lunge)
            {
                ApproachAs(source.AttackApproach);
            }

            int damage = CombatMath.ComputeAttackDamage(enemy, target);
            if (usingSkill)
            {
                damage = System.Math.Max(1, Rounding.AwayFromZero(damage * skillPower));
            }

            // The player's armour, on the one path where it matters most: this
            // is the only damage the player ever takes. No enemy authors an
            // attack type, so the funnel reads their claws as untyped and
            // physical resistance stops them, which is the sensible reading of
            // "hits you with whatever it has".
            damage = DamagePipeline.AfterDefences(
                damage, enemy, target,
                attackType: null, affinity: ElementalAffinity.Neutral,
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
                RecordSpellPresentation(source.Vfx);
            }

            // Through the ledger's funnel, like every other damage path.
            //
            // This one was missed on the first pass: the enemy swing does its
            // own ApplyDamageDetailed rather than going through
            // ApplyFinalDamage, so "damage taken" read zero for the entire
            // party while every other column was correct. An untyped monster
            // swing counts as Physical, which is what AttackTypeOf already
            // answers for anything without a player kit.
            var landed = DealDamage(enemy, target, damage, AttackTypeOf(enemy));

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
