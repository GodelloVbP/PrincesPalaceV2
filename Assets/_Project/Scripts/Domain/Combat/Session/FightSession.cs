using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // A fight, resolved. Engine-free.
    //
    // This is the ~34% of v1's FightController that was never really about
    // Unity: the damage funnel, enemy decisions, turn riders, skill dispatch,
    // combat-time talent rules and reward assembly. Moving it here converts the
    // bulk of the fight's test mass from scene-loading PlayMode tests into
    // sub-second EditMode ones, and gives four things their first cheap
    // coverage at all.
    //
    // COMMANDS ARE STATELESS. The session is told what to do and resolves it
    // synchronously, including every enemy reply, exactly as v1 did in a single
    // frame -- a large number of assertions act and then check the result on
    // the very next line. What it deliberately does NOT hold is the pending
    // selection ("Attack is armed, waiting for a target"): that is what is
    // highlighted, which is view state, and it stays in Core's menu state
    // machine where v1 kept it. A session that also tracked it would invite a
    // desync class v1 never had.
    //
    // Partials mirror v1's file layout so the interleaved beat-recording sites
    // port shape-for-shape with their comments.
    public sealed partial class FightSession
    {
        // Builds the state and kit for a summoned enemy, by content id — a
        // Roar's whole reason to exist. Domain cannot look ContentDatabase
        // up itself (see FightEncounterAdapter's own header on why that
        // boundary exists), so the one call site that CAN — the adapter,
        // building the fight in the first place — hands down a closure over
        // its own content-reading code instead. False means the id named
        // nothing real; the caller treats that exactly like "no room left".
        public delegate bool SummonFactory(string enemyId, out CombatantState state, out EnemyKit kit);

        private readonly CombatEncounter _encounter;
        private readonly Dictionary<CombatantState, PlayerKit> _playerKits = new Dictionary<CombatantState, PlayerKit>();
        private readonly Dictionary<CombatantState, EnemyKit> _enemyKits = new Dictionary<CombatantState, EnemyKit>();
        private readonly SeededRandom _rng;
        private readonly int _initiativeSlots;
        private readonly SummonFactory _summonFactory;
        private readonly int _stageSlotsPerSide;

        public bool IsBossFight { get; }
        public bool IsEliteFight { get; }

        // How wide the damage roll is. A parameter rather than v1's mutable
        // static: tests set it to 0 so a prediction made through CombatMath
        // directly still matches what the session deals.
        public float DamageVarianceRange { get; set; } = DamagePipeline.DefaultVarianceRange;

        public FightSession(
            CombatEncounter encounter,
            IReadOnlyList<PlayerKit> players,
            IReadOnlyList<EnemyKit> enemies,
            SeededRandom rng,
            bool isBossFight = false,
            bool isEliteFight = false,
            int initiativeSlots = FightHudSpec.InitiativeSlots,
            SummonFactory summonFactory = null,
            int stageSlotsPerSide = FightHudSpec.StageSlotsPerSide)
        {
            _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            _rng = rng;
            _initiativeSlots = initiativeSlots;
            _summonFactory = summonFactory;
            _stageSlotsPerSide = stageSlotsPerSide;
            IsBossFight = isBossFight;
            IsEliteFight = isEliteFight;

            // Kits are matched to live state positionally, in the order the
            // encounter was built with. The encounter owns turn ORDER; these
            // lists are the roster it was built from.
            var party = encounter.PlayerParty.ToList();
            for (int i = 0; i < party.Count && players != null && i < players.Count; i++)
            {
                _playerKits[party[i]] = players[i];
            }

            var foes = encounter.Enemies.ToList();
            for (int i = 0; i < foes.Count && enemies != null && i < enemies.Count; i++)
            {
                _enemyKits[foes[i]] = enemies[i];
            }
        }

        // ---- queries -------------------------------------------------------

        public CombatantState Current => _encounter.Current;
        public bool IsPlayerTurn => _encounter.IsPlayerTurn;
        public bool IsOver => _encounter.IsOver;
        public bool PlayerWon => _encounter.PlayerWon;
        public CombatEncounter Encounter => _encounter;

        public bool CanMeleeReach(CombatantState target) => _encounter.CanMeleeReach(target);

        public PlayerKit KitFor(CombatantState combatant) =>
            combatant != null && _playerKits.TryGetValue(combatant, out var kit) ? kit : null;

        public EnemyKit SourceFor(CombatantState combatant) =>
            combatant != null && _enemyKits.TryGetValue(combatant, out var kit) ? kit : null;

        // What a blow landing on this combatant is up against, elementally.
        //
        // ONE helper rather than `SourceFor(x)?.Affinity ?? Neutral` repeated at
        // eight damage call sites: the fallback is the whole rule for the player
        // side of a fight (no definition, no affinity, everything lands at face
        // value), and a rule spelled out eight times is a rule seven of them can
        // drift away from.
        private ElementalAffinity AffinityOf(CombatantState combatant) =>
            SourceFor(combatant)?.Affinity ?? ElementalAffinity.Neutral;

        // How many of one enemy id are still standing on the monsters' side.
        //
        // The summon cap is checked TWICE on purpose -- once when the intent is
        // drawn so the boss does not wind up for a call that fizzles, and again
        // when it resolves because the field can fill in between (see
        // ResolveSummon). That double check is the design; the double
        // EXPRESSION was not, and the two copies were free to drift into
        // disagreeing about what "still standing" counts as.
        private int LivingCountOf(string enemyId) =>
            _encounter.LivingEnemies.Count(e => SourceFor(e)?.Source.Id == enemyId);

        public IReadOnlyList<ResolvedSkillOption> SkillOptionsFor(CombatantState actor)
        {
            var kit = KitFor(actor);
            if (kit == null) return Array.Empty<ResolvedSkillOption>();

            // ONLY WHAT THE ACTOR CAN ACTUALLY USE. A skill whose ability
            // requirements are unmet was listed greyed with a LOCKED prefix,
            // and a menu of things you cannot pick is a menu you have to read
            // past every turn.
            //
            // Filtered HERE rather than in the row builder, because the option
            // carries its own Index into kit.Skills and the dispatcher reads
            // that -- filtering the rows alone would leave row position and
            // skill index disagreeing, which casts a different skill than the
            // one that was pressed.
            return kit.Skills
                .Select((s, i) => new ResolvedSkillOption(i, s,
                    SkillResolution.CanAfford(actor, s.ManaCost, s.ResourceCost),
                    CooldownRemaining(actor, s.Id)))
                .Where(o => actor.AbilityScores.Meets(RequirementCurve.Apply(o.Skill.Requirements)))
                .ToList();
        }

        // ---- commands ------------------------------------------------------

        // Opens the fight: the first actor gets its turn start, any monsters
        // faster than the whole party take their opening swings, and the
        // survivors declare what they intend next.
        //
        // A method rather than constructor work, deliberately. Construction
        // that resolves combat would mean a fight cannot be built without also
        // being started -- and the adapter needs the built session in hand to
        // append its opening lines ("The bog witch blocks your path!") BEFORE
        // any beat exists, which is the one window where AppendMessage's
        // immediate path is the right one.
        // IDEMPOTENT, so the door the player comes through and the doors the
        // tests come through cannot double-grant between them. GrantTurnStart
        // is the half that would show it: called twice, turn one pays its regen
        // and its status ticks twice, which is a balance change disguised as a
        // wiring detail.
        private bool _begun;

        public void Begin()
        {
            if (_begun) return;
            _begun = true;

            GrantTurnStart();
            AutoResolveEnemyTurns();

            if (!_encounter.IsOver && _encounter.IsPlayerTurn)
            {
                PrepareEnemyIntents();
            }
        }

        // A plain attack. Resolves the swing, records its beat, and hands the
        // turn on -- enemy replies included.
        public void ExecuteAttack(CombatantState target)
        {
            var actor = Current;
            if (actor == null || target == null) return;

            // Any ordinary action may cash in a banked turn afterwards. Hold
            // Back is the exception and says so itself.
            _actionCanBrave = true;
            BeginBeat(actor, target);
            SetStance(actor, Stances.Attack);

            // Weight of Wool counts warded party members and Gift: Fury is spent
            // by the swing, and CombatMath can see neither from inside its own
            // scaling. Summed onto the actor immediately before the figure is
            // computed, which is the only arrangement that cannot go stale.
            RefreshAttackBonus(actor, spendingGift: true);

            ResolveAttackSwing(actor, target, $"{actor.Name} attacks {target.Name}");

            // A basic attack is the best way to build a signature resource, and
            // ONLY a basic attack. That is what makes swinging anyway a
            // deliberate choice rather than a wasted turn on a character whose
            // attack barely dents an enemy's defence -- and it is why Skill does
            // not grant it, and why Dual Wield's second swing does not grant it
            // again either.
            GrantSignature(actor, actor.Signature?.GainOnAttack ?? 0);

            // Dual Wield: a second full swing at the SAME target, only on a plain
            // attack (the relic's wording is "whenever you attack", not "whenever
            // you act") and only while there is still something standing to hit
            // again.
            //
            // Played as its OWN beat rather than folded into this one: a single
            // beat plays exactly one lunge/hit/recoil cycle no matter how much
            // activity happens inside it, so two swings sharing a beat rendered
            // as one action with the second swing's number silently overwriting
            // the first's (RecordBeatAmount assigns, it does not accumulate).
            if (target.IsAlive && HasRelic(actor, RelicEffect.DualWield))
            {
                CommitBeat();
                BeginBeat(actor, target);
                SetStance(actor, Stances.Attack);
                ResolveAttackSwing(actor, target, $"{actor.Name}'s Dual Wield strikes {target.Name} again");
            }

            // Anything the action recorded is closed off before the riders run,
            // so an extra turn earned by a kill retro-attaches to the blow that
            // earned it rather than opening a beat of its own.
            CommitBeat();
            AdvanceAfterAction();
        }

        // Give up this turn to bank one, up to the cap. The action that earns
        // a banked point must NOT be able to spend it on itself, which is why
        // this is the one command that leaves _actionCanBrave false.
        public void HoldBack()
        {
            var actor = Current;
            if (actor == null) return;

            _actionCanBrave = false;
            BeginBeat(actor, null);
            SetStance(actor, Stances.Idle);

            if (actor.BankedActions < FightTuning.MaxBankedActions)
            {
                actor.BankedActions++;
                AppendMessage($"{actor.Name} holds back, banking an action.");
            }
            else
            {
                AppendMessage($"{actor.Name} cannot bank any more actions.");
            }

            CommitBeat();
            AdvanceAfterAction();
        }

        private int ResolveAttackSwing(CombatantState actor, CombatantState target, string verbPhrase)
        {
            var enemyKit = SourceFor(target);
            var actorKit = KitFor(actor);

            RelicsBeforeSwing(actor);

            // The base, held so a counted swing measures its bonus against the
            // blow itself rather than against what armour and elements make of
            // it. See FightSession.Potency.
            int baseAmount = CombatMath.ComputeAttackDamage(actor, target);

            var outcome = DamagePipeline.AfterDefences(
                baseAmount,
                actor, target,
                attackType: actorKit?.AttackType,
                affinity: enemyKit?.Affinity ?? ElementalAffinity.Neutral,
                varianceRange: DamageVarianceRange,
                rng: _rng,
                resolveWard: ResolveWard);

            // Swift: the swing missed outright. Everything below this line
            // is a rider on a LANDED hit -- BreakShield depletion, the mark
            // bonus, Lucky Deck's own splash (via RelicsAfterSwing), the
            // message that names a number, the actor's hit grunt, and every
            // rider inside ApplyFinalDamage (elemental procs, lifesteal,
            // Hardened's push, splash/kill-splash, status application) --
            // so a miss returns here, before any of it, rather than trying
            // to gate each one individually. See DamagePipeline's own
            // header for why the roll already happened inside AfterDefences
            // rather than needing a second check here.
            if (outcome.IsMiss)
            {
                RecordMiss();
                AppendMessage($"{target.Name} dodges {actor.Name}'s attack!");
                return 0;
            }

            DepleteBreakShield(target, outcome.Effectiveness);

            if (outcome.PoisonDetonation > 0)
            {
                AppendMessage($"The poison detonates! {target.Name} takes {outcome.PoisonDetonation} bonus damage!");
            }

            // MarkBonus stays OUTSIDE the shared funnel deliberately -- it is
            // consumed by an ATTACK specifically, and TotalDamage is every
            // path a hit can land through. See FightSession.Relics.MarkBonus.
            int damage = TotalDamage(actor, baseAmount, outcome.Damage) + MarkBonus(actor, target, baseAmount);
            RelicsAfterSwing(actor, target, damage);

            AppendMessage($"{verbPhrase} for {damage} damage!{EffectivenessSuffix(outcome.Effectiveness)}");
            RecordActorVoice(actor);
            ApplyFinalDamage(actor, target, damage);

            // Runic's tempo rider: a PLAIN swing (this method, never a skill
            // cast -- see ExecuteSkillInner/CastSkill, which never call this)
            // arms a one-shot mana discount for the actor's NEXT skill.
            // Armed here rather than inside ApplyFinalDamage because
            // ApplyFinalDamage is shared with both skill paths and has no
            // way to tell "this damage came from a plain swing" apart from
            // "this damage came from a skill" -- the one thing only the
            // plain-swing call site actually knows.
            ArmRunicManaDiscount(actor);

            return damage;
        }

        // See ResolveAttackSwing's own call site for why this is armed only
        // from a plain swing. 0 when the actor carries no
        // NextSkillManaDiscountPercent modifier -- ChargeSkillMana already
        // treats 0 as "nothing armed", the same convention every other
        // percent field here uses.
        private void ArmRunicManaDiscount(CombatantState actor)
        {
            if (actor == null) return;
            actor.PendingManaDiscountPercent = actor.ModifierEffects.Best(ModifierEffectType.NextSkillManaDiscountPercent);
        }

        // The part that never differs once a damage figure and its own message
        // are already settled: apply it, show it on the beat, pose the target,
        // and flag a kill.
        private void ApplyFinalDamage(CombatantState actor, CombatantState target, int damage)
        {
            // Read once and reused below for the modifier riders -- see
            // ApplyModifierOnHitRiders' own header on why a rider needs to
            // know what element THIS hit already was.
            var hitType = AttackTypeOf(actor);
            DealDamage(actor, target, damage, hitType);
            RecordBeatAmount(damage);
            SetStance(target, target.IsAlive ? Stances.Hurt : Stances.Defeated);

            // Sharp Horns T3 leaves its mark on a target that SURVIVED. Not on
            // a corpse: shredding the armour of something already dead is a log
            // line about nothing.
            ApplyDefenseShred(actor, target);

            // Black Ram Mode splashes on every landed hit, kill or no kill --
            // unlike Trample's kill splash just below, which is a different rule
            // from a different strand and can legitimately fire on the same blow.
            ApplyTransformSplash(actor, target, damage);

            // Every item-modifier on-hit rider (elemental damage, lifesteal,
            // chance-to-push) -- see the method's own header for why these
            // three share one block rather than three separate call sites.
            ApplyModifierOnHitRiders(actor, target, damage, hitType);

            if (!target.IsAlive)
            {
                AppendMessage($"{target.Name} is defeated!");
                _killedThisAction = true;
                RecordKill(actor, target);

                // Everything the Black Ram gets FOR a kill, in the one place
                // that knows a kill just happened on the player's action.
                // Splash first: it can itself kill a neighbour, and the messages
                // read better as "he went down, and so did the one next to him"
                // than the reverse.
                ApplyKillSplash(actor, target);
                ApplyModifierKillSplash(actor, target);
                ExtendTransformOnKill(actor);
            }
        }

        // Everything an item modifier grants ON A LANDED HIT, from EITHER a
        // plain swing or a skill cast -- both funnel through ApplyFinalDamage,
        // which is exactly why this lives here rather than duplicated at both
        // call sites. Fires for the ACTOR's own modifiers only: a target's
        // gear does not react to being hit (that is what
        // FlatPhysicalDamageReduction/BreakShieldDepletionResistPercent are
        // for, both read defensively inside the damage pipeline instead).
        //
        // ONE METHOD for three riders rather than three, because all three
        // read the same actor.ModifierEffects bag off the same landed hit and
        // none of them can fail the others -- a target already reduced to 0
        // HP by the elemental tick still gets lifestolen-from and can still be
        // pushed (harmlessly; PushBack on a combatant about to be removed
        // from the order is a no-op the queue already tolerates).
        private void ApplyModifierOnHitRiders(CombatantState actor, CombatantState target, int damage, DamageType hitType)
        {
            if (actor == null || target == null || damage <= 0) return;

            var effects = actor.ModifierEffects;
            if (effects.IsEmpty) return;

            // Elemental on-hit damage -- the player-side mirror of the
            // Poison-on-hit template at FightSession.Enemies.cs:548. One
            // extra damage INSTANCE per landed hit, typed by whichever
            // element the modifier names, dealt through the same DealDamage
            // funnel every other hit uses so it counts on the ledger.
            foreach (var effect in effects.All)
            {
                if (effect.Type != ModifierEffectType.ElementalDamageOnHitPercent || !effect.Against.HasValue)
                {
                    continue;
                }

                // BUG (found investigating "Astral only added 4 damage to a
                // 117-damage arcane spell"): when the affix's own element IS
                // the element the hit already was -- an Astral (Arcane) affix
                // riding an ARCANE spell, not a Fiery (Fire) affix riding a
                // Physical sword -- the +% describes how much bigger THAT hit
                // itself should have been, not a second, foreign-element proc.
                // The proc math below is `Magnitude% of Attack`, which is
                // correct for a foreign-element rider (a small extra tick
                // alongside the swing) but floors a same-element bonus to a
                // few points regardless of how large the hit it is supposed
                // to be boosting was -- 41% of an Attack of 10 is 4, whether
                // the swing it rode in on hit for 12 or the spell it rode in
                // on hit for 117. Applied straight to `damage`, the hit's own
                // already-mitigated landed figure, the same convention
                // Vampiric's lifesteal just below already uses for
                // percent-of-this-hit math -- not re-run through
                // DamagePipeline.AfterDefences, because it is the SAME
                // element as `damage` already paid its one mitigation pass
                // as; re-mitigating it a second time would tax it twice.
                if (effect.Against.Value == hitType)
                {
                    int matchingBonus = Rounding.AwayFromZero(damage * effect.Magnitude / 100f);
                    if (matchingBonus > 0)
                    {
                        DealDamage(actor, target, matchingBonus, hitType);
                        AppendMessage($"{target.Name} takes {matchingBonus} bonus {hitType} damage!");
                        if (!target.IsAlive) break;
                    }

                    continue;
                }

                // NOT CombatMath.Scale -- see this method's own header note
                // above and CombatMath.Scale's own header: the x5 DamageScale
                // it applies was deleted from the mitigated-combat path on
                // 2026-08-26 and this rider is part of that path (it is bonus
                // damage riding a landed hit, not one of the handful of
                // deliberately-still-x5 splash sites CombatMath.Scale's own
                // header names). "X% of Attack" uses the same
                // Rounding.AwayFromZero(value * pct / 100f) convention every
                // other percent-of-a-computed-figure call in this file uses
                // (see DepleteBreakShield's resistPercent a few methods
                // down), floored at 1 the same way ScaledAttack's own raw
                // Attack term is.
                int elementalRaw = Math.Max(1, Rounding.AwayFromZero(actor.Attack * effect.Magnitude / 100f));

                // Routed through DamagePipeline.AfterDefences' TYPED overload
                // -- the SAME funnel a spell's own damageInstances resolve
                // through (FightSession.Skills.ResolveDamageInstances) --
                // rather than straight through DealDamage. This is bonus
                // damage of a DIFFERENT type than the swing that triggered it
                // (Fire riding a Physical sword, say), so it owes its own
                // typed-resistance/defense check the way any other typed hit
                // does; it is absent from DamagePipeline's own documented
                // exemption list (Trample/Explosive/Shatter/Lucky Deck
                // splash), which is exactly why skipping mitigation here was
                // a bug and not a design choice.
                //
                // dodgeAlreadyResolved: true -- NOT a second dodge roll. This
                // rider only ever runs from ApplyFinalDamage, which only runs
                // once the parent swing already landed (a miss returns out of
                // ResolveAttackSwing/ExecuteSkillInner/etc. long before
                // ApplyFinalDamage is reached). The blade already connected;
                // "the elemental charge on the blade separately whiffs" is
                // not a distinct event this combat model has a concept for,
                // the same reasoning ResolveDamageInstances' own multi-packet
                // spell already established for a second authored packet.
                var elementalOutcome = DamagePipeline.AfterDefences(
                    elementalRaw, effect.Against.Value, target,
                    affinity: AffinityOf(target),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    attacker: actor,
                    dodgeAlreadyResolved: true);

                int elementalDamage = elementalOutcome.Damage;
                if (elementalDamage <= 0) continue;

                DealDamage(actor, target, elementalDamage, effect.Against.Value);
                AppendMessage($"{target.Name} takes {elementalDamage} bonus {effect.Against.Value} damage!");

                if (!target.IsAlive) break;
            }

            // Vampiric: heal the wielder for a percent of what they just dealt.
            // `damage` here is already the final, post-mitigation landed-hit
            // figure -- NOT CombatMath.Scale, same reasoning and the same
            // Rounding.AwayFromZero(value * pct / 100f) convention as the
            // elemental rider just above.
            int lifestealPercent = effects.Best(ModifierEffectType.LifestealPercent);
            if (lifestealPercent > 0)
            {
                int healed = Rounding.AwayFromZero(damage * lifestealPercent / 100f);
                if (healed > 0)
                {
                    CombatMath.Heal(actor, healed);
                    AppendMessage($"{actor.Name} drains {healed} health from the blow.");
                }
            }

            // Hardened's push: a CHANCE, not a guarantee, to knock the target
            // back in turn order -- reuses CombatEncounter.PushBack, the same
            // pass-through the Black Ram's Headbutt already drives.
            int pushChance = effects.Best(ModifierEffectType.PushBackOnHitChancePercent);
            if (pushChance > 0 && target.IsAlive && RollPercent(pushChance))
            {
                if (_encounter.PushBack(target, FightTuning.ModifierPushBackSlots))
                {
                    AppendMessage($"{target.Name} is knocked off balance!");
                }
            }

            // Frosty's chill: a CHANCE, on this landed hit, to slow the
            // target -- reuses ApplyChilled (FightSession.SpeedBuffs.cs),
            // the exact call Lucky Deck's own migrated slow now goes
            // through. See ModifierEffectType.ChilledOnHitChancePercent for
            // why the chance is authored per-modifier but the chill's own
            // speed-reduction magnitude and duration are fixed constants.
            int chillChance = effects.Best(ModifierEffectType.ChilledOnHitChancePercent);
            if (chillChance > 0 && target.IsAlive && RollPercent(chillChance))
            {
                int lost = ApplyChilled(target, FightTuning.ChilledOnHitSpeedPercent,
                    FightTuning.ChilledOnHitTurns, actor);

                if (lost < 0)
                {
                    AppendMessage($"{target.Name} is chilled to the bone - slower now!");
                }
            }

            // Sylvan's root: a CHANCE, on this landed hit, to root the
            // target -- StatusEffectType.Rooted via StatusEffects.Apply
            // directly, the same status-list entry point ApplyChilled itself
            // sits on top of. No speed bookkeeping to mirror here (Chilled's
            // whole complication), so this needs no ApplyRooted wrapper --
            // Apply is already the correct, complete call. `actor` is the
            // Source so a rooted enemy's forfeited-turn message and any
            // future "who rooted you" query can attribute it, matching
            // Poison/Provoked/every other sourced status in this file.
            int rootChance = effects.Best(ModifierEffectType.RootChancePercent);
            if (rootChance > 0 && target.IsAlive && RollPercent(rootChance))
            {
                StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted,
                    magnitude: 0, turns: FightTuning.RootOnHitTurns, source: actor);
                AppendMessage($"{target.Name} is rooted in place!");
            }
        }

        // A single shared percent roll for on-hit modifier riders -- kept as
        // one method rather than inlined at each call site so every chance
        // effect in this vocabulary rolls the SAME way (>= this session's own
        // _rng, never a second independent random source).
        private bool RollPercent(int percentChance)
        {
            return RandomOps.RollPercent(_rng, percentChance);
        }

        private void DepleteBreakShield(CombatantState target, float effectiveness)
        {
            if (target?.BreakShield == null) return;

            int amount = BreakShield.DepletionFor(effectiveness > 1f);

            // Stalwart's shield resistance: shrinks the amount BEFORE
            // BreakShield.Deplete ever sees it, rather than reducing Deplete's
            // own return -- Deplete's bool answers "did THIS hit break it",
            // and shrinking the input is what actually changes that answer
            // rather than merely lying about it after the fact.
            int resistPercent = target.ModifierEffects.Best(ModifierEffectType.BreakShieldDepletionResistPercent);
            if (resistPercent > 0)
            {
                amount = Rounding.AwayFromZero(amount * (100 - Math.Min(100, resistPercent)) / 100f);
            }

            if (target.BreakShield.Deplete(amount))
            {
                AppendMessage($"{target.Name}'s guard breaks!");
            }
        }

        private static string EffectivenessSuffix(float multiplier)
        {
            if (multiplier > 1f) return " It's super effective!";
            if (multiplier < 1f) return " It's not very effective...";
            return "";
        }
    }

    // A skill and whether this actor can currently pay for it. Returned rather
    // than having the view ask twice.
    public readonly struct ResolvedSkillOption
    {
        public readonly int Index;
        public readonly Content.ResolvedSkill Skill;
        public readonly bool Affordable;

        // Turns until it comes back, 0 when it is ready. Carried BESIDE
        // Affordable rather than folded into it: "you cannot pay for this" and
        // "you cannot do this yet" want different words on the row, and a
        // single bool would make the menu say the wrong one.
        public readonly int CooldownRemaining;

        public bool Ready => Affordable && CooldownRemaining <= 0;

        public ResolvedSkillOption(int index, Content.ResolvedSkill skill, bool affordable,
                                   int cooldownRemaining = 0)
        {
            Index = index;
            Skill = skill;
            Affordable = affordable;
            CooldownRemaining = cooldownRemaining;
        }
    }
}
