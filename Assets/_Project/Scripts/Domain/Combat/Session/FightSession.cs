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
        private readonly CombatEncounter _encounter;
        private readonly Dictionary<CombatantState, PlayerKit> _playerKits = new Dictionary<CombatantState, PlayerKit>();
        private readonly Dictionary<CombatantState, EnemyKit> _enemyKits = new Dictionary<CombatantState, EnemyKit>();
        private readonly SeededRandom _rng;
        private readonly int _initiativeSlots;

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
            int initiativeSlots = FightHudSpec.InitiativeSlots)
        {
            _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            _rng = rng;
            _initiativeSlots = initiativeSlots;
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
                weakness: enemyKit?.Weakness,
                resistance: enemyKit?.Resistance,
                varianceRange: DamageVarianceRange,
                rng: _rng,
                resolveWard: ResolveWard);

            DepleteBreakShield(target, outcome.Effectiveness);

            if (outcome.PoisonDetonation > 0)
            {
                AppendMessage($"The poison detonates! {target.Name} takes {outcome.PoisonDetonation} bonus damage!");
            }

            int damage = outcome.Damage + PotencyBonus(baseAmount);
            RelicsAfterSwing(actor, target, damage);

            AppendMessage($"{verbPhrase} for {damage} damage!{EffectivenessSuffix(outcome.Effectiveness)}");
            RecordActorVoice(actor);
            ApplyFinalDamage(actor, target, damage);

            return damage;
        }

        // The part that never differs once a damage figure and its own message
        // are already settled: apply it, show it on the beat, pose the target,
        // and flag a kill.
        private void ApplyFinalDamage(CombatantState actor, CombatantState target, int damage)
        {
            DealDamage(actor, target, damage, AttackTypeOf(actor));
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
                ExtendTransformOnKill(actor);
            }
        }

        private void DepleteBreakShield(CombatantState target, float effectiveness)
        {
            if (target?.BreakShield == null) return;

            if (target.BreakShield.Deplete(BreakShield.DepletionFor(effectiveness > 1f)))
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
