using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
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

        // ONE ranking, computed once, for every caller that needs to know
        // what kind of fight this was -- Boss beats Elite beats Normal.
        // RunOrchestrator.RollOffers and Core/Bot/BotRunDriver's own trace
        // used to each carry their own copy of this ranking; BotRunDriver's
        // fell out of sync with RollOffers' own fix (Boss checked first) and
        // silently kept reporting a boss room's offer as Elite/Normal in the
        // balance-bot trace. One property, read by both, cannot drift apart
        // again the same way.
        public EncounterClass EncounterClass =>
            IsBossFight ? EncounterClass.Boss : IsEliteFight ? EncounterClass.Elite : EncounterClass.Normal;

        // How wide the damage roll is. A parameter rather than v1's mutable
        // static: tests set it to 0 so a prediction made through CombatMath
        // directly still matches what the session deals.
        public float DamageVarianceRange { get; set; } = DamagePipeline.DefaultVarianceRange;

        // Mechanic (d), RUN-WIDE STATS. Set by the caller (FightEncounterAdapter)
        // from RunSnapshot.bonusDamagePercent BEFORE Begin() -- a settable
        // property rather than a constructor parameter, the same shape
        // DamageVarianceRange already uses, so every existing constructor
        // call site keeps compiling. Read by every player-side damage
        // instance through FightSession.Relics.TotalDamage. 0 for a run
        // that has not banked any yet, which is every run's first fight.
        public int RunWideBonusDamagePercent { get; set; }

        // Mechanic (f), LOCKS. See CombatLocks' own header -- a per-fight
        // once-per-turn/once-per-combat gate, reset at the turn boundary
        // inside GrantTurnStart (FightSession.Riders.cs).
        private readonly CombatLocks _locks = new CombatLocks();

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

        // CAN THIS ACTOR, WITH THIS REACH, LAND A SINGLE-OPPONENT ACTION ON
        // THIS TARGET? The primitive. Everything positional in the game --
        // the click gate, plate dimming, row affordability, the enemy AI's
        // own pool, the bot's legal menu -- is a convenience over this one
        // method, so there is exactly one place the rule is stated.
        //
        // DEFINED ONLY FOR SINGLE-OPPONENT ACTIONS: the plain attack, and a
        // skill whose targeting is SingleEnemy. Self, Party, AllEnemies and
        // Summon keep their existing applicability rules (AlliesOf /
        // OpponentsOf, the summon cap) and must never ask this -- an ally
        // handed in here answers false, which is the answer that surfaces
        // the caller bug rather than hiding it.
        //
        // NO ALLOCATION. It is asked once per enemy plate per repaint and
        // once per ability per AI draw; EligibleTargets below is the filter
        // for callers who genuinely want a list.
        //
        // RENAMED FROM CanReach when single-ally targeting landed. The old
        // name had been unambiguous only while nothing on the caster's own
        // side could be pointed at; with an ally picker on the screen,
        // "CanReach(ally)" answering false reads as a bug rather than as the
        // category refusal it is. CanReachAlly below is the same question
        // asked of the other side, and the two share ReachAllows.
        //
        // Evaluation order is fixed and each step is load-bearing:
        public bool CanReachEnemy(CombatantState actor, Reach reach, CombatantState target)
        {
            // CATEGORY, ahead of everything. A single-opponent question asked
            // about an ally. Checked here rather than in ReachAllows because
            // it is the one step the ally side inverts rather than shares.
            if (actor == null || target == null) return false;
            if (actor.IsPlayerSide == target.IsPlayerSide) return false;

            return ReachAllows(actor, reach, target);
        }

        // CAN THIS ACTOR AIM A SINGLE-ALLY ACTION AT THIS COMBATANT?
        //
        // NO REACH ARGUMENT, and that is the rule rather than an omission:
        // a rank mask and the front-rank rule are both about fighting PAST
        // somebody, and nothing on the caster's own side is in the way of
        // his own squad (docs/handoffs/archive/battle_ui/README.md:230 --
        // "no unreachable state exists for allies"). A skill that authored
        // reachSlots or meleeReach is refused at content-build time for any
        // targeting but SingleEnemy, so there is no authored mask here for
        // this to be quietly ignoring.
        //
        // WHAT the effect will accept is a separate question and belongs to
        // AllyTargeting, asked by EligibleAllies below -- this one is only
        // "is there a living squadmate there".
        public bool CanReachAlly(CombatantState actor, CombatantState target)
        {
            if (actor == null || target == null) return false;
            if (actor.IsPlayerSide != target.IsPlayerSide) return false;

            // Same -1-for-dead-and-for-absent reading LivingRankOf gives on
            // the enemy side; the rank itself is not used.
            return _encounter.LivingRankOf(target) >= 0;
        }

        // The side-blind core: everything CanReachEnemy checks once the two
        // combatants are known to be on opposite sides. Private because the
        // side check is never optional -- a caller reaching this directly
        // would be asking a question neither public method has a meaning for.
        private bool ReachAllows(CombatantState actor, Reach reach, CombatantState target)
        {
            // 1. VALIDATE. LivingRankOf answers -1 for dead and for "not in
            //    this encounter" alike, which is the same "there is nothing
            //    there to hit" in both cases.
            int rank = _encounter.LivingRankOf(target);
            if (rank < 0) return false;

            // 3a. PROVOKE WINS, for every ReachKind. A taunt is the one thing
            //     allowed to override where an action can go, in both
            //     directions: it makes the provoker reachable from anywhere
            //     AND makes everyone else unreachable. Applied before the
            //     scepter because a taunt narrows and the scepter widens, and
            //     the narrowing is the promise the player paid a turn for.
            var forced = ForcedTargetFor(actor);
            if (forced != null) return ReferenceEquals(target, forced);

            // 3b. MONKEY KING'S SCEPTER lifts the FRONT-RANK RULE only.
            //     Kind == Melee, never ExplicitRanks: the relic's promise is
            //     about striking past a bodyguard, not about ignoring what a
            //     skill says about where it may be aimed. See Reach's header.
            if (reach.Kind == ReachKind.Melee && actor.IsPlayerSide
                && HasRelic(actor, RelicEffect.MonkeyKingsScepter))
            {
                return true;
            }

            // 4. THE RANK MASK.
            return reach.Allows(rank);
        }

        // The filter over CanReachEnemy: every opponent this actor could aim
        // a single-opponent action at right now, in list order. Same
        // single-opponent-only contract as CanReachEnemy above -- an
        // AllEnemies or Party skill has no business here.
        public IReadOnlyList<CombatantState> EligibleTargets(CombatantState actor, Reach reach)
        {
            var eligible = new List<CombatantState>();
            if (actor == null) return eligible;

            foreach (var opponent in _encounter.OpponentsOf(actor))
            {
                if (CanReachEnemy(actor, reach, opponent)) eligible.Add(opponent);
            }

            return eligible;
        }

        // THE ALLY-SIDE SIBLING of EligibleTargets: every combatant on this
        // actor's side that this skill may be aimed at right now, in party
        // order (which since the positions pass IS the field formation, so
        // the list reads front to rear).
        //
        // TAKES THE SKILL, not a Reach, because the ally side's variation is
        // the EFFECT rather than the geometry -- a ward may land on the
        // caster and a gift may not, and AllyTargeting owns that rule for
        // every reader. THE CASTER'S OWN SIDE INCLUDES THE CASTER, so the
        // player's own plate is in this list whenever the effect accepts it.
        //
        // Answers an empty list for any targeting but SingleAlly: a Party or
        // Self skill has no pick to make, and handing a caller a plausible
        // list for one would invite a second, wrong ally picker.
        public IReadOnlyList<CombatantState> EligibleAllies(CombatantState actor, ResolvedSkill skill)
        {
            var eligible = new List<CombatantState>();
            if (actor == null || skill == null) return eligible;
            if (skill.Targeting != SkillTargeting.SingleAlly) return eligible;

            foreach (var ally in _encounter.AlliesOf(actor))
            {
                if (!CanReachAlly(actor, ally)) continue;
                if (!AllyTargeting.Accepts(skill.Effect, actor, ally)) continue;

                // THE BOARD-STATE HALF, asked by the session because
                // AllyTargeting has no schedule to read (its own header on
                // where the split falls). An advance aimed at the ally who is
                // already next is refused at cast (plan 1.9 rule 5), so
                // offering their plate would be lighting a target the command
                // then turns down -- the exact disagreement between "what the
                // UI offers" and "what the session accepts" this whole
                // function exists to prevent.
                if (skill.Effect == SkillEffect.Hasten && !CanAdvanceInOrder(ally)) continue;

                eligible.Add(ally);
            }

            return eligible;
        }

        // IS THERE ANYWHERE EARLIER FOR THIS COMBATANT TO GO?
        //
        // ONE RULE, TWO READERS -- the ally rack's plates (through
        // EligibleAllies above) and CanResolveSkill's own refusal, which
        // re-asks at commit because the schedule can move between a pick and
        // a press. Stated once here so the two can never answer differently.
        //
        // POSITION 1, NOT 0. Index 0 of the forecast is the action happening
        // right now (TurnOrder.Project's own header), so 1 is the earliest
        // position any advance can reach and an actor already standing in it
        // has nothing to buy. A combatant the forecast does not contain at
        // all answers -1 and is refused for the same reason: there is no
        // destination to compute.
        public bool CanAdvanceInOrder(CombatantState combatant)
        {
            int position = _encounter.ForecastPositionOf(combatant);
            return position > 1;
        }

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
            _encounter.LivingEnemies.Count(e => SourceFor(e)?.Source?.Id == enemyId);

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
                    // healthCostPercent JOINS mana/resource here too (plan
                    // 1.2, milestone B) -- omitting it left Blackglass
                    // Spear reading as affordable on a menu row (and to
                    // every bot policy that trusts this flag) even when the
                    // health cost would be refused outright at cast time,
                    // the exact "legal-looking, actually refused" shape
                    // that stalls a greedy policy without a repeat guard.
                    SkillResolution.CanAfford(actor, s.ManaCost, s.ResourceCost, s.HealthCostPercent),
                    CooldownRemaining(actor, s.Id)))
                .Where(o => actor.AbilityScores.Meets(RequirementCurve.Apply(o.Skill.Requirements)))
                .ToList();
        }

        // Same question SkillOptionsFor's own filter answers -- does the
        // actor have ANYTHING to show in the Skill branch -- without paying
        // for the list it builds. OnVerbPressed only ever asks "is this
        // empty", never for a skill out of it, so it does not need
        // affordability, cooldowns or a ResolvedSkillOption per entry: just
        // the one requirement gate SkillOptionsFor's Where clause already
        // states.
        public bool HasAnySkillOption(CombatantState actor)
        {
            var kit = KitFor(actor);
            if (kit == null) return false;

            foreach (var skill in kit.Skills)
            {
                if (actor.AbilityScores.Meets(RequirementCurve.Apply(skill.Requirements))) return true;
            }
            return false;
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

            RelicsOnCombatBegin();
            GrantTurnStart();
            AutoResolveEnemyTurns();
            AutoResolveEggTurns();

            if (!_encounter.IsOver && _encounter.IsPlayerTurn)
            {
                PrepareEnemyIntents();
            }
        }

        // A plain attack. Resolves the swing, records its beat, and hands the
        // turn on -- enemy replies included.
        //
        // Returns false for a refusal that changed nothing: no beat, no
        // mana, no cooldown, no turn. The click gate and the bot's legal
        // menu both filter this out ahead of time, so a false here means
        // something called the command directly.
        public bool ExecuteAttack(CombatantState target)
        {
            var actor = Current;
            if (actor == null || target == null) return false;

            // THE REACH CHECK COMES FIRST, before anything is spent and
            // before a beat exists to be thrown away. A plain swing is
            // SingleEnemy with Reach.Melee, always -- the front-rank rule
            // stated as a value rather than as a special case.
            if (!CanReachEnemy(actor, Reach.Melee, target))
            {
                AppendMessage($"{target.Name} is out of reach.");
                return false;
            }

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
            GrantSignature(actor, actor.SignaturePool?.GainOnAttack ?? 0);

            // THE PRIMARY POOL IS NOT PAID HERE, and the omission is the
            // point. It used to be, right on this line, which quietly gave a
            // rage bar Wool's rule: a Slam that hit for 40 built nothing, and
            // a swing that MISSED built the full amount, since this site fires
            // whether or not the blow landed. Its grant moved to the damage
            // funnel (ApplyAndCountDamage), where "the owner dealt damage" is
            // a fact rather than an assumption -- see
            // GrantPrimaryOnDamagingAction for why the two pools are allowed
            // to disagree about what an attack is.

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
            return true;
        }

        // ---- move ----------------------------------------------------------
        //
        // Trade field places with the nearest living ally in one direction,
        // and END THE TURN doing it. This replaced Hold Back, which banked an
        // action for a later free turn: a turn spent on tempo, for a turn
        // spent on position. The tank swap it buys is only worth a turn
        // because enemy melee now concentrates on rank 0 the same way the
        // player's always has.
        //
        // ENEMIES NEVER MOVE. There is no AI branch for it and no intent that
        // can telegraph one; the monsters' side of the front-rank rule is
        // enforced entirely by what they are allowed to target.

        // The ONE legality query -- the verb row, the bot's legal menu and
        // Move itself all read this, so a row can never offer a move the
        // command then refuses.
        public bool CanMove(CombatantState actor, MoveDirection direction)
        {
            if (!TryFindMovePartner(actor, direction, out _, out int partnerIndex)) return false;
            if (StatusEffects.HasRooted(actor.Statuses)) return false;
            return !StatusEffects.HasRooted(_encounter.PlayerParty[partnerIndex].Statuses);
        }

        // Returns false for a refusal that spent nothing -- checked BEFORE
        // BeginBeat, so a refused move costs no turn and leaves no beat.
        public bool Move(MoveDirection direction)
        {
            var actor = Current;
            if (actor == null) return false;

            // ROOTED IS ONE RULE, READ OFF BOTH SIDES OF THE SWAP. Rooted
            // means "cannot change field position", and a swap changes two --
            // so a rooted partner refuses the move just as a rooted actor
            // does, and says the same sentence about whichever one it is.
            if (StatusEffects.HasRooted(actor.Statuses))
            {
                AppendMessage($"{actor.Name} is rooted.");
                return false;
            }

            if (!TryFindMovePartner(actor, direction, out int actorIndex, out int partnerIndex))
            {
                AppendMessage($"{actor.Name} has nowhere to move {WordFor(direction)}.");
                return false;
            }

            var other = _encounter.PlayerParty[partnerIndex];
            if (StatusEffects.HasRooted(other.Statuses))
            {
                AppendMessage($"{other.Name} is rooted.");
                return false;
            }

            BeginBeat(actor, null);
            SetStance(actor, Stances.Idle);

            _encounter.SwapPartySlots(actorIndex, partnerIndex);
            AppendMessage($"{actor.Name} steps {WordFor(direction)}, trading places with {other.Name}.");

            // BOTH figures moved, and the note is fired for both -- Sparring
            // Buckler pays the acting character for an action that changed
            // ANY position, Sparring Saber pays only the one who chose to
            // move (mover == actingCharacter). See NoteDeliberateMove.
            NoteDeliberateMove(actor, actor);
            NoteDeliberateMove(other, actor);

            CommitBeat();
            AdvanceAfterAction();
            return true;
        }

        // Who this actor would trade places with, and where both of them
        // stand in the party list. CORPSES ARE STEPPED OVER: the swap is with
        // the nearest LIVING ally in that direction, so a dead front-ranker
        // does not wall the character behind it in.
        //
        // Returns list INDICES, not living ranks -- SwapPartySlots reorders
        // the list, and the list is the thing ranks are computed from.
        private bool TryFindMovePartner(CombatantState actor, MoveDirection direction,
                                        out int actorIndex, out int partnerIndex)
        {
            actorIndex = -1;
            partnerIndex = -1;

            // Not the player's side means no move: the enemy AI has no Move
            // and this is also what stops an enemy turn reaching it.
            if (actor == null || !actor.IsPlayerSide || !actor.IsAlive) return false;

            var party = _encounter.PlayerParty;
            for (int i = 0; i < party.Count; i++)
            {
                if (ReferenceEquals(party[i], actor)) { actorIndex = i; break; }
            }

            if (actorIndex < 0) return false;

            int step = direction == MoveDirection.Forward ? -1 : 1;
            for (int i = actorIndex + step; i >= 0 && i < party.Count; i += step)
            {
                if (!party[i].IsAlive) continue;
                partnerIndex = i;
                return true;
            }

            return false;
        }

        private static string WordFor(MoveDirection direction) =>
            direction == MoveDirection.Forward ? "forward" : "back";

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
                resolveWard: ResolveWard,
                resolveDetonation: ResolveDetonation);

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
            // cast -- see CastSkill, which never calls this)
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
        // and pay out whatever a kill owes on top of the ledger's own row.
        // The row and the rider flag are DealDamage's, not this method's --
        // see SettleDeath (FightSession.Ledger.cs) for why they moved.
        private void ApplyFinalDamage(CombatantState actor, CombatantState target, int damage)
        {
            // Read once and reused below for the modifier riders -- see
            // ApplyModifierOnHitRiders' own header on why a rider needs to
            // know what element THIS hit already was.
            var hitType = AttackTypeOf(actor);
            DealDamage(actor, target, damage, hitType, KillCredit.Attacker);
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

            // Vampire Dentures' own lifesteal (RelicLifestealPercent) is a
            // SEPARATE field from gear's ModifierEffects -- see
            // CombatantState.RelicLifestealPercent's own comment -- so an
            // actor carrying no gear-derived on-hit rider at all (the
            // common case for a fresh relic with no equipment involved)
            // must not short-circuit out of this method before the
            // lifesteal block below ever reads it.
            if (effects.IsEmpty && actor.RelicLifestealPercent <= 0) return;

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
                        DealDamage(actor, target, matchingBonus, hitType, KillCredit.Attacker);
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
                // ResolveAttackSwing/CastSkill/etc. long before
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
                    dodgeAlreadyResolved: true,
                    resolveDetonation: ResolveDetonation);

                int elementalDamage = elementalOutcome.Damage;
                if (elementalDamage <= 0) continue;

                DealDamage(actor, target, elementalDamage, effect.Against.Value, KillCredit.Attacker);
                AppendMessage($"{target.Name} takes {elementalDamage} bonus {effect.Against.Value} damage!");

                if (!target.IsAlive) break;
            }

            // Vampiric: heal the wielder for a percent of what they just dealt.
            // `damage` here is already the final, post-mitigation landed-hit
            // figure -- NOT CombatMath.Scale, same reasoning and the same
            // Rounding.AwayFromZero(value * pct / 100f) convention as the
            // elemental rider just above.
            // Vampire Dentures' own half summed on top of gear's --
            // see CombatantState.RelicLifestealPercent's own comment.
            int lifestealPercent = effects.Best(ModifierEffectType.LifestealPercent) + actor.RelicLifestealPercent;
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
            // target -- through ApplyStatusTo, the one status seam, the same
            // way the chill above reaches ApplyChilled. `actor` is the Source
            // so a rooted enemy's forfeited-turn message and any future "who
            // rooted you" query can attribute it, matching Poison/Provoked/
            // every other sourced status in this file.
            int rootChance = effects.Best(ModifierEffectType.RootChancePercent);
            if (rootChance > 0 && target.IsAlive && RollPercent(rootChance))
            {
                ApplyStatusTo(target, StatusEffectType.Rooted,
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

        // THE POISON COMBO'S DAMAGE HALF, handed to DamagePipeline beside
        // ResolveWard. StatusCombos owns the RULE (which hits detonate, and
        // what the remaining ticks are worth); this owns the consequence,
        // because dealing damage is something only a session can do properly.
        //
        // Through DealDamage rather than CombatMath.ApplyDamage, which is the
        // point: the bonus lands on the ledger, and a target it fells is
        // settled like any other kill (SettleDeath -- the rider flag and the
        // kill row together). It used to go straight to CombatMath from
        // inside the pipeline, so it did neither.
        //
        // Typed as Poison on the ledger, not as whatever set it off: the
        // damage IS the poison's remaining ticks arriving at once.
        //
        // CREDIT FOLLOWS THE SIDE, not the caller. A player's Nature swing
        // detonating a monster is that player's kill. An enemy's Poison swing
        // detonating a party member credits nobody -- the same answer the
        // enemy swing itself already gives, and for the reason written at
        // that call site: an enemy turn resolves INSIDE AdvanceAfterAction,
        // after _killedThisAction has been read and reset, so a flag raised
        // here would survive to hand the PLAYER a Trample the monster earned.
        // See AUDIT.md #63.
        private int ResolveDetonation(CombatantState attacker, CombatantState target, DamageType incomingType)
        {
            int bonus = StatusCombos.SpendPoisonIfMatched(target, incomingType);
            if (bonus <= 0) return 0;

            DealDamage(attacker, target, bonus, DamageType.Poison,
                attacker != null && attacker.IsPlayerSide ? KillCredit.Attacker : KillCredit.Nobody);

            return bonus;
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
