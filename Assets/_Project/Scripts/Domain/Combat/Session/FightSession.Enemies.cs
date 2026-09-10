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

        // PHASE D3 FIX: what BuildIntent telegraphs for a ROOTED enemy that
        // drew -1 because Rooted zeroed the only entry its pool had, rather
        // than the "attack" fallback below. Same sentinel-before-label
        // status as IntentAttack -- IntentTooltip compares against it to
        // phrase the honest sentence instead of the generic "will {label}
        // {who}" template, which would otherwise read as nonsense ("will
        // Forfeits Turn itself").
        public const string IntentForfeit = "Forfeits Turn";

        private readonly Dictionary<CombatantState, EnemyIntent> _intents = new Dictionary<CombatantState, EnemyIntent>();

        // NULL IN EVERY FIGHT THE GAME BUILDS. Set only by tools/preview.ps1's
        // route through FightBootstrap, where the point is to SEE a mob's kit
        // rather than to play against it -- see EnemyShowcase's own header for
        // why weighted randomness is the wrong tool for looking at new content.
        // A property rather than a constructor argument because the session is
        // built by FightEncounterAdapter, which has no business knowing that a
        // preview exists.
        public EnemyShowcase Showcase { get; set; }

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
                var pool = EffectivePoolFor(enemy, kit?.Abilities);

                // THE ROLL STILL HAPPENS EITHER WAY, and it has to: the draw's
                // position in the RNG stream is what keeps a seeded run
                // reproducible, and a preview that skipped the draw would give
                // the fight a different shape from the one being previewed --
                // different targets, different variance, a different fight.
                // Only which entry is taken changes.
                float roll = _rng?.NextFloat() ?? 0f;
                int chosen = Showcase != null
                    ? Showcase.Next(enemy, pool)
                    : EnemyAbilityDraw.Pick(pool, roll);

                var ability = pool != null && chosen >= 0 && chosen < pool.Count
                    ? pool[chosen]
                    : (EnemyAbility?)null;

                var target = PickIntentTarget(enemy, ability);
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
        //
        // PHASE D3: also where Rooted excludes the plain-attack entry. A
        // rooted enemy loses its melee option and must draw from whatever
        // skills remain -- the exact mirror of the front-rank rule gating the
        // PLAYER's own plain attack, just read off the ACTOR instead of the
        // target. `IsPlainSwing` (not
        // `IsLegacyAttack`) is the right predicate here: it is the same
        // "ranged" line ResolveEnemyAction already draws for `usingSkill`
        // (`!chosen.Value.IsPlainSwing`), which also treats a legacy scaled
        // attack (authored SkillPower != 1, no real ResolvedSkill) as a
        // skill rather than a melee swing. Zeroed with LegacyAttack, not
        // EnemyAbility.Of, to keep HasSkill/Power intact for that entry --
        // only its Weight moves.
        //
        // AND WHERE NOTHING IS IN REACH. A SingleEnemy ability with no
        // eligible target is zero-weighted FOR THIS DRAW ONLY, the same
        // "capped this turn does not mean gone" treatment the summon cap
        // already gets. ONLY SingleEnemy: every other targeting category
        // keeps its existing rule, because none of them has a reach question
        // to ask (see FightSession.CanReach's own header). And because
        // EligibleTargets applies the Provoke override, a taunt from the back
        // rank keeps a front-only ability perfectly legal -- forced onto the
        // provoker, wherever they are standing.
        private IReadOnlyList<EnemyAbility> EffectivePoolFor(CombatantState enemy, IReadOnlyList<EnemyAbility> abilities)
        {
            if (abilities == null) return null;

            bool rooted = StatusEffects.HasRooted(enemy.Statuses);
            List<EnemyAbility> effective = null;

            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];

                if (ability.HasSkill && ability.Skill.Effect == SkillEffect.Summon)
                {
                    int living = LivingCountOf(ability.Skill.SummonEnemyId);
                    if (living >= ability.Skill.SummonCap)
                    {
                        effective ??= new List<EnemyAbility>(abilities);
                        effective[i] = EnemyAbility.Of(ability.Skill, 0f);
                        continue;
                    }
                }

                var reach = SingleOpponentReachOf(ability);
                if (reach.HasValue && EligibleTargets(enemy, reach.Value).Count == 0)
                {
                    effective ??= new List<EnemyAbility>(abilities);
                    effective[i] = ability.HasSkill
                        ? EnemyAbility.Of(ability.Skill, 0f)
                        : EnemyAbility.LegacyAttack(ability.Label, ability.Power, 0f);
                    continue;
                }

                if (rooted && ability.IsPlainSwing)
                {
                    effective ??= new List<EnemyAbility>(abilities);
                    effective[i] = EnemyAbility.LegacyAttack(ability.Label, ability.Power, 0f);
                }
            }

            // NOT abilities.ToList(). The copy was defensive against nothing:
            // every consumer takes IReadOnlyList and none of them mutates, so
            // the only thing it bought was a fresh List per enemy per intent
            // draw -- which is the allocation the null-until-needed build
            // directly above exists to avoid.
            return effective ?? abilities;
        }

        // THE REACH QUESTION FOR ONE DRAWN ABILITY, or null when the ability
        // is not a single-opponent action and therefore has none.
        //
        // Null is not "unrestricted" -- it is "this question does not apply",
        // which is a different thing and the reason this returns Reach? and
        // not Reach. A DamageAll or a self-heal has no position to be aimed
        // at, so nothing about it may be gated on reach.
        //
        // A plain swing, and a legacy scaled attack, are both SingleEnemy
        // with Reach.Melee: the monsters' side of the front-rank rule, stated
        // as a value rather than as a special case, exactly the way
        // ExecuteAttack states the player's.
        private static Reach? SingleOpponentReachOf(EnemyAbility? ability)
        {
            if (!ability.HasValue) return Reach.Melee;

            var value = ability.Value;
            if (!value.HasSkill) return Reach.Melee;

            return value.Skill.Targeting == SkillTargeting.SingleEnemy
                ? value.Skill.Reach
                : (Reach?)null;
        }

        // WHO THE TELEGRAPH PROMISES, and the ONE draw a preparation makes
        // per enemy.
        //
        // The draw happens FIRST, UNCONDITIONALLY, and over the LIVING PARTY
        // COUNT -- before and regardless of Provoke, of what was drawn, and
        // of how many targets are actually in reach. That is the whole point:
        // SeededRandom.NextInt consumes exactly one NextUlong whatever the
        // bound (SeededRandom.cs:37-46), so keeping one draw of the same
        // range in the same position keeps the stream byte-identical to what
        // it was before reach existed. A draw made only when it was needed
        // would make the rest of a seeded run depend on who happened to be
        // standing where.
        //
        // Only WHICH entry it lands on changes: forced ?? eligible[draw %
        // eligible.Count].
        private CombatantState PickIntentTarget(CombatantState enemy, EnemyAbility? ability)
        {
            var living = _encounter.LivingPlayerParty.ToList();
            if (living.Count == 0) return null;

            int draw = _rng == null ? 0 : _rng.NextInt(0, living.Count);

            // A taunt is honoured at TELEGRAPH time, not only at resolution:
            // the icon promising someone the provoker has already pulled off
            // the monster is a promise the fight is not going to keep.
            var forced = ForcedTargetFor(enemy);
            if (forced != null) return forced;

            var reach = SingleOpponentReachOf(ability);
            var candidates = reach.HasValue
                ? EligibleTargets(enemy, reach.Value)
                : (IReadOnlyList<CombatantState>)living;

            // Empty only when a Showcase forced an ability the field cannot
            // satisfy -- EffectivePoolFor has already zero-weighted anything
            // a real draw could land on with nothing in reach. Naming a
            // living member anyway keeps the telegraph from going blank; the
            // resolution-time re-check below is what actually decides.
            if (candidates.Count == 0) candidates = living;

            return candidates[draw % candidates.Count];
        }

        // Whether a ROOTED enemy has any legal skill left to draw from, once
        // its plain attack is excluded by EffectivePoolFor above. Recomputes
        // the same effective pool rather than caching one from intent time --
        // Rooted (and a Summon cap) can change between when an intent was
        // committed and when the turn actually resolves, and ResolveSkippedTurn
        // asks this question fresh, the same way it re-checks HasStun fresh
        // rather than trusting whatever the telegraph assumed.
        //
        // Pick(pool, 0f) rather than a hand-rolled "any weight > 0" scan: it
        // is the exact function the real draw uses, so "no legal option"
        // means precisely what the draw itself would find, and passing a
        // literal 0f (not _rng.NextFloat()) costs nothing from the seeded
        // stream -- this is a query, not a commitment.
        private bool RootedEnemyHasNoLegalAction(CombatantState enemy)
        {
            var kit = SourceFor(enemy);
            var pool = EffectivePoolFor(enemy, kit?.Abilities);
            return EnemyAbilityDraw.Pick(pool, 0f) < 0;
        }

        private EnemyIntent BuildIntent(CombatantState enemy, CombatantState target,
                                        IReadOnlyList<EnemyAbility> pool, int chosen)
        {
            var source = SourceFor(enemy)?.Source;

            // PHASE D3b FIX: a STUNNED enemy forfeits UNCONDITIONALLY --
            // ResolveSkippedTurn's isStunned check (see its own header) never
            // consults the pool at all, unlike isRootedHelpless just below it,
            // which only fires when RootedEnemyHasNoLegalAction says the pool
            // is genuinely empty. A stunned enemy that drew a perfectly legal,
            // fully-affordable skill still forfeits, so this cannot be folded
            // into the "nothing to draw from" branch below the way Rooted's
            // check is -- it has to run BEFORE `pool`/`chosen` are consulted
            // at all, and unconditionally, not gated on them being empty.
            //
            // Before this, a stunned monster with a real skill in its pool
            // was telegraphed that skill's full preview here (see
            // EnemyIntentTests.AStunnedEnemyWithARealSkill_TelegraphsAForfeit_NotAFakeSkill)
            // and then silently did nothing when AutoResolveEnemyTurns
            // actually reached it -- the player planned around an attack
            // that was never coming, the same class of bug Rooted had.
            if (StatusEffects.HasStun(enemy.Statuses))
            {
                return new EnemyIntent(IntentForfeit, EnemyIntentKind.Skill, target, 0,
                    scope: EnemyIntentScope.Self);
            }

            // Nothing to draw from: a monster that joined mid-round, or one
            // whose whole pool was weighted out. It swings -- EXCEPT when the
            // -1 is because Rooted zeroed the only entry the pool had and
            // nothing else was authored to replace it. That case is not a
            // content mistake, it is the enemy genuinely having no legal
            // action, and ResolveSkippedTurn (see its own header) already
            // forfeits the turn for exactly this reason when it resolves.
            //
            // RootedEnemyHasNoLegalAction is called here rather than
            // re-derived: it is the SAME check ResolveSkippedTurn asks at
            // resolution time, over the SAME recomputed pool (Pick's result
            // does not depend on which roll is passed once the pool sums to
            // zero -- see EnemyAbilityDraw.Pick's own comment), so the
            // telegraph and the resolution can never disagree about whether
            // this enemy is helpless. Before this, BuildIntent had no such
            // check at all: a Rooted enemy with an empty pool was telegraphed
            // a full-power "Attack" here and then silently had its turn
            // forfeited when AutoResolveEnemyTurns actually reached it -- the
            // player planned around damage that was never coming.
            if (pool == null || chosen < 0 || chosen >= pool.Count)
            {
                if (StatusEffects.HasRooted(enemy.Statuses) && RootedEnemyHasNoLegalAction(enemy))
                {
                    return new EnemyIntent(IntentForfeit, EnemyIntentKind.Skill, target, 0,
                        scope: EnemyIntentScope.Self);
                }

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
                        source?.AppliesStatus, source != null && source.HasStatus);

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
                varianceRange: 0f, rng: null, resolveWard: null,
                ignoresDefense: skill.IgnoresDefense).Damage;
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
                                         ResolvedEnemy source, float power)
        {
            if (enemy == null || target == null) return 0;

            int damage = CombatMath.ComputeAttackDamage(enemy, target);
            if (useSkill && source != null)
            {
                damage = System.Math.Max(1, Rounding.AwayFromZero(damage * power));
            }

            // The enemy's own authored attackType, same as the real swing
            // now reads (see ActorAttackType's comment) -- a monster with
            // none stays Physical via the null fallback, exactly as before.
            return DamagePipeline.AfterDefences(
                damage, enemy, target,
                attackType: source?.AttackType, affinity: ElementalAffinity.Neutral,
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
            // A fight can end on a monster's swing or on a poison tick at turn
            // start, and neither of those passes through the riders -- so
            // without this a defeat would never settle.
            //
            // ASKED ON BOTH EXITS, because the two endings this names arrive at
            // opposite ends of the method and only the first was ever caught: a
            // swing is already settled by the time the loop calls back in, but
            // the turn start is the LAST thing that happens here, and the death
            // it causes is exactly what makes this method return false and the
            // loop stop. There is no next pass to catch it on.
            //
            // AdvanceAfterAction re-checks IsOver after the loop and covered it
            // for any fight driven by a player command, which is why this held
            // for so long. It does not cover the two callers that drive
            // AutoResolveEnemyTurns themselves -- Begin, and
            // FightController.RescueAStalledEnemyTurn, the path a spent second
            // life leaves the fight sitting on. Both settled nothing: no payout,
            // not even the zeroed one a loss owes, and no "The party falls."
            //
            // Both calls are idempotent (_victoryResolved, _payoutResolved), so
            // asking twice costs a comparison.
            if (SettleIfOver()) return false;

            _encounter.AdvanceTurn();
            GrantTurnStart();
            return !SettleIfOver();
        }

        // True when the fight is over, having settled it.
        private bool SettleIfOver()
        {
            if (!_encounter.IsOver) return false;

            ResolveVictory();
            ResolveOutcome();
            return true;
        }

        // Three independent reasons a turn is skipped outright rather than
        // resolving a weakened version of it -- a broken stagger meter, a
        // Stun status, and (PHASE D3) a Rooted enemy with no legal skill left
        // to cast -- reported together, since more than one can be true at
        // once and the player should see why.
        //
        // The Rooted check is gated behind HasRooted first and short-circuits
        // on isStunned: RootedEnemyHasNoLegalAction recomputes a pool, which
        // is wasted work when the turn is already forfeit for a cheaper
        // reason, and Stun's own message already covers "cannot act" without
        // needing to know why the pool would have been empty too.
        //
        // ALSO GATED ON `!enemy.IsPlayerSide` -- ResolveSkippedTurn runs for
        // BOTH sides (a stunned PLAYER's turn is skipped through this exact
        // path too, per AutoResolveEnemyTurns' own header), but Rooted's pool
        // machinery (SourceFor/EffectivePoolFor) only ever tracks an ENEMY's
        // ability kit. SourceFor(player) is always null, which would make
        // RootedEnemyHasNoLegalAction read as "helpless" unconditionally --
        // forfeiting a player's ENTIRE turn, not merely disabling their
        // plain-attack option, the moment anything ever applied Rooted to
        // one. Nothing today does (see StatusEffectType.Rooted's own
        // comment), but this guard is what keeps that true by construction
        // rather than by accident.
        //
        // Reset and ConsumeStun both happen HERE, the instant the skip is
        // actually spent, rather than inside Deplete/Tick or on a timer, so
        // either one always costs exactly one turn however it was reached.
        // Rooted is NOT consumed here -- it decays by turn count like Chilled
        // (StatusEffects.Tick's generic countdown), not spent like Stun, so a
        // forfeited turn does not erase turns of Rooted still owed.
        private bool ResolveSkippedTurn(CombatantState enemy)
        {
            bool isBroken = enemy.BreakShield != null && enemy.BreakShield.IsBroken;
            bool isStunned = StatusEffects.HasStun(enemy.Statuses);
            bool isRootedHelpless = !isStunned && !enemy.IsPlayerSide && StatusEffects.HasRooted(enemy.Statuses)
                                     && RootedEnemyHasNoLegalAction(enemy);
            if (!isBroken && !isStunned && !isRootedHelpless) return false;

            if (isBroken) enemy.BreakShield.Reset();
            if (isStunned) StatusEffects.ConsumeStun(enemy.Statuses);

            ForfeitTurn(enemy, isBroken && isStunned
                ? $"{enemy.Name} is stunned AND still reeling - it cannot act!"
                : isBroken
                    ? $"{enemy.Name} is still reeling and cannot act!"
                    : isStunned
                        ? $"{enemy.Name} is stunned and cannot act!"
                        : $"{enemy.Name} is rooted with nothing to cast - it cannot act!");

            return true;
        }

        // A TURN THAT RESOLVES TO NOTHING, in the one shape every such turn
        // takes: a beat of its own carrying the sentence that explains it, and
        // the commitment dropped.
        //
        // DROPPING THE INTENT IS THE LOAD-BEARING HALF. A monster that
        // declared something it could not deliver has spent that declaration
        // -- PrepareEnemyIntents skips an enemy that still holds one, so an
        // intent left standing after a forfeit is re-honoured next turn
        // against the same field that already refused it, and the monster
        // forfeits again, and again. Re-drawing is what lets EffectivePoolFor
        // weight the impossible entry out and find the enemy something legal.
        //
        // A BEAT, not a silent return: playback is beat-driven, so a turn with
        // no beat passes with nothing on screen at all -- the player sees
        // their own action and then, apparently, their own action again.
        private void ForfeitTurn(CombatantState combatant, string message)
        {
            BeginBeat(combatant, combatant);
            _intents.Remove(combatant);
            AppendMessage(message);
            CommitBeat();
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
            // WHAT WAS COMMITTED, resolved BY INDEX before the target is,
            // because the target has to be re-checked against the committed
            // ability's own reach and cannot be until that ability is known.
            // Pure lookups, no draw, so this block moving above the target
            // selection does not move anything in the RNG stream.
            //
            // BY INDEX, not by comparing the label back to a name. That
            // comparison was sound while a monster had exactly one skill and
            // its name was therefore unique. A weighted pool can hold two
            // abilities that share a display name, and matching on text would
            // resolve the wrong one while the telegraph looked correct -- the
            // precise failure a telegraph exists to prevent.
            var enemyKit = SourceFor(enemy);
            var committed = IntentDetailFor(enemy);
            var committedPool = enemyKit?.Abilities;
            var committedAbility = committed.HasValue && committedPool != null
                                   && committed.Value.AbilityIndex >= 0
                                   && committed.Value.AbilityIndex < committedPool.Count
                ? committedPool[committed.Value.AbilityIndex]
                : (EnemyAbility?)null;

            var forced = ForcedTargetFor(enemy);
            var promised = committed?.Target;

            // THE PROMISE IS RE-VALIDATED ON TWO COUNTS, not one. It always
            // had to survive the target dying; it now also has to survive the
            // target MOVING -- the player's whole reason to spend a turn on
            // Move is to change who the telegraphed blow can land on, and a
            // promise honoured against a formation that no longer exists
            // would make Move do nothing.
            //
            // THE RE-PICK DRAWS NOTHING. It is the first eligible target in
            // list order, deliberately: a second draw here would consume from
            // the seeded stream on a branch whose frequency depends on how the
            // player is playing, which is the one thing a reproducible run
            // cannot have.
            if (promised != null
                && (!promised.IsAlive || !CanReachWithAbility(enemy, committedAbility, promised)))
            {
                promised = FirstEligibleFor(enemy, committedAbility);

                // AND WHEN THE RE-PICK FINDS NOBODY, THE TURN IS FORFEIT --
                // it does not fall through to the pick below. That fall-through
                // broke the two rules this whole block exists to keep. It drew
                // (PickIntentTarget always rolls), on a branch whose frequency
                // is decided by how the player is playing, which is exactly the
                // dependency the "re-picks draw nothing" rule forbids. And
                // PickIntentTarget's own last-resort widening to the living
                // party -- written for the preview showcase, where an ability
                // is forced onto a field that cannot satisfy it -- then handed
                // back a target the committed ability's mask had just refused,
                // so a back-rank-only blow landed on rank 0 the moment its
                // promised target died or stepped away. The mask is the rule;
                // an ability with nothing left in reach lands on nobody.
                if (promised == null)
                {
                    ForfeitTurn(enemy, $"{enemy.Name} has nothing in reach - it cannot act!");
                    return;
                }
            }

            // THE THIRD PATH, and it is not a re-pick: an enemy that has no
            // committed intent at all. A monster fast enough to swing during
            // Begin() (intents are prepared after the opening enemy turns) and
            // one that joined mid-round both arrive here, and both used to
            // reach straight past the front rank through a uniform random
            // pick. It draws ONE value, exactly as that pick did, so the
            // stream keeps its shape -- it just draws over what is actually
            // in reach.
            var target = forced ?? promised ?? PickIntentTarget(enemy, committedAbility);
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
            //
            // THE KIT EXISTING IS NOT THE SOURCE EXISTING, and this read
            // `kit != null` while all three uses below dereference `source`.
            // EnemyKit's own constructor is deliberately null-source-tolerant
            // ("a kit built without a source still fields a plain attack
            // rather than throwing"), so the one shape it promises to
            // survive -- a summon handed a kit with no resolved record -- was
            // the one shape that threw here on that combatant's first turn.
            var kit = enemyKit;
            var source = kit?.Source;
            bool hasSource = source != null;

            var pool = committedPool;
            var chosen = committedAbility;

            // PHASE D3: a plain-attack commitment made BEFORE this enemy was
            // rooted is no longer legal by the time it resolves -- and this
            // is the ORDINARY case, not a corner one. Sylvan's Root lands as
            // an on-hit rider on a swing the PLAYER just took, which happens
            // DURING the player's own turn, immediately ahead of
            // AutoResolveEnemyTurns resolving this exact enemy's already-
            // telegraphed reply -- the intent for THIS turn was drawn back
            // when PrepareEnemyIntents last ran, before the hit that rooted
            // it. ResolveSkippedTurn's own Rooted check already recomputes
            // fresh (RootedEnemyHasNoLegalAction never reads the committed
            // intent), so the no-legal-skill forfeit is unaffected by this
            // staleness -- only the "has a legal skill, but the stale
            // commitment says plain swing" gap needs handling here.
            //
            // Re-drawing (not merely refusing) is safe specifically for a
            // PLAIN SWING: TelegraphSuffix never shows one ("only SKILLS are
            // telegraphed" -- see its own header), so nothing was promised
            // to the player for this turn, and honouring a promise that was
            // never shown is not a promise worth keeping. A committed SKILL
            // is never touched here, matching every other path in this
            // method that honours the telegraph outright.
            if (chosen.HasValue && chosen.Value.IsPlainSwing && StatusEffects.HasRooted(enemy.Statuses))
            {
                var effective = EffectivePoolFor(enemy, pool);
                int redraw = EnemyAbilityDraw.Pick(effective, _rng?.NextFloat() ?? 0f);
                chosen = effective != null && redraw >= 0 && redraw < effective.Count
                    ? effective[redraw]
                    : (EnemyAbility?)null;
            }

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
            // is the only damage the player ever takes. An enemy with no
            // authored attackType still reads as untyped Physical here (the
            // null fallback) — one WITH one now actually meets the matching
            // MagicalDefense instead, which used to be a dead stat against
            // every monster in the game. See ActorAttackType's own comment.
            var outcome = DamagePipeline.AfterDefences(
                damage, enemy, target,
                attackType: ActorAttackType(enemy), affinity: ElementalAffinity.Neutral,
                varianceRange: DamageVarianceRange,
                rng: _rng,
                resolveWard: ResolveWard,
                resolveDetonation: ResolveDetonation);

            // The enemy's own pose (and cast VFX) is recorded regardless of
            // whether the blow connects -- the monster still visibly swings
            // or casts, it is the PLAYER who evades the result. Moved ahead
            // of the goaded multiplier/miss check for that reason (it does
            // not depend on either).
            SetStance(enemy, usingSkill ? Stances.Cast : Stances.Attack);
            if (usingSkill)
            {
                RecordSpellPresentation(source.Vfx);
            }

            // Swift: the player dodged. No damage, no signature grant, no
            // absorbed/CheatedDeath message, no applied status, no Hurt pose
            // -- every one of those is a rider on a landed hit. A taunt is
            // still spent: the enemy DID act on it, the target simply
            // evaded the result, which is not the same thing as the enemy
            // never having gone for the taunter at all.
            if (outcome.IsMiss)
            {
                RecordMiss();
                StatusEffects.ConsumeProvoke(enemy.Statuses);
                AppendMessage(usingSkill
                    ? $"{enemy.Name} uses {source.SkillName} on {target.Name}, but it misses!"
                    : $"{enemy.Name} attacks {target.Name}, but it misses!");
                CommitBeat();
                return;
            }

            damage = outcome.Damage;

            // Provoke T2: a goaded enemy swings wide. Applied to the PAIR rather
            // than through the target's own DamageTakenMultiplier, so it blunts
            // the blow aimed at the taunter and nothing else.
            float goaded = StatusEffects.ProvokedDamageMultiplier(enemy, target);
            if (goaded < 1f)
            {
                damage = System.Math.Max(1, Rounding.AwayFromZero(damage * goaded));
            }

            // Through the ledger's funnel, like every other damage path.
            //
            // This one was missed on the first pass: the enemy swing does its
            // own ApplyDamageDetailed rather than going through
            // ApplyFinalDamage, so "damage taken" read zero for the entire
            // party while every other column was correct. An untyped monster
            // swing counts as Physical, which is what AttackTypeOf already
            // answers for anything without a player kit.
            // KillCredit.Nobody, which is what this path has always done
            // rather than what it obviously should do: a monster felling a
            // party member records no kill row today. Raising the flag here
            // would be worse than the gap -- an enemy turn resolves INSIDE
            // AdvanceAfterAction, after that method has already read and reset
            // _killedThisAction, so the flag would survive to the player's
            // next action and hand them a Trample the enemy earned. Crediting
            // the ledger without the flag is a real question and a balance
            // one; it is not a refactor's to answer. See AUDIT.md #63.
            var landed = DealDamage(enemy, target, damage, AttackTypeOf(enemy), KillCredit.Nobody);

            // A fleece thickens in a hard winter: being ground down is itself a
            // way to build. Granted per HIT rather than per point, so a swarm of
            // weak attackers is not a better generator than one real threat.
            GrantSignatureForHitTaken(target);

            if (landed.Absorbed > 0)
            {
                AppendMessage($"{target.Name}'s {target.SignaturePool.DisplayName} soaks {landed.Absorbed} of it.");
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

        // The committed ability's own reach question, asked of one target.
        // True for an ability that has no such question (see
        // SingleOpponentReachOf) -- a DamageAll does not stop being legal
        // because somebody moved.
        private bool CanReachWithAbility(CombatantState enemy, EnemyAbility? ability, CombatantState target)
        {
            var reach = SingleOpponentReachOf(ability);
            return !reach.HasValue || CanReach(enemy, reach.Value, target);
        }

        // The re-pick: first in LIST ORDER, no draw. See ResolveEnemyAction's
        // own comment on why this must not roll.
        private CombatantState FirstEligibleFor(CombatantState enemy, EnemyAbility? ability)
        {
            var reach = SingleOpponentReachOf(ability);
            if (!reach.HasValue) return _encounter.LivingPlayerParty.FirstOrDefault();

            foreach (var candidate in _encounter.LivingPlayerParty)
            {
                if (CanReach(enemy, reach.Value, candidate)) return candidate;
            }

            return null;
        }

        private static CombatantState ForcedTargetFor(CombatantState enemy)
        {
            var provoker = StatusEffects.ProvokedBy(enemy);
            return provoker != null && provoker.IsAlive ? provoker : null;
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
                (victim.SignaturePool?.GainOnDamageTaken ?? 0)
                + victim.Talents.Best(TalentEffectType.WoolOnHitTaken));

            // The primary pool hears the same hit, on the same terms -- per
            // HIT, not per point. Zero for mana; a rage bar that fills by
            // being ground down authors a number instead of needing a hook.
            //
            // The TALENT term is deliberately NOT summed in here: WoolOnHit-
            // Taken is Shawn's tree feeding Shawn's fleece, and a talent that
            // says "wool" must not quietly pay a different resource.
            GrantPrimary(victim, victim.PrimaryPool?.GainOnDamageTaken ?? 0);
        }

        // Adds to a combatant's signature resource if it has one. A no-op for
        // everyone else, so call sites never have to ask.
        private static void GrantSignature(CombatantState combatant, int amount)
        {
            if (amount > 0)
            {
                combatant?.SignaturePool?.Gain(amount);
            }
        }

        // The same for the primary pool, which every combatant has. Kept as
        // its own method rather than a two-pool loop so a call site names
        // which pool it is paying and cannot pay the wrong one by omission.
        private static void GrantPrimary(CombatantState combatant, int amount)
        {
            if (amount > 0)
            {
                combatant?.PrimaryPool?.Gain(amount);
            }
        }
    }
}
