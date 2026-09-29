using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Bot
{
    // The four things a player turn can spend itself on, mirrored onto one
    // value a policy can hand back without touching FightSession itself.
    // BasicSpell removed (docs/PLAN_SHOP.md §4 Phase E) -- Skill now covers
    // every cast, since there is no second, nameless spell left to be a
    // fifth kind.
    public enum FightActionKind
    {
        Attack,
        Skill,
        Item,

        // Replaced HoldBack, which banked a turn for a later free one.
        // Carries a MoveDirection; the target field means nothing on it.
        Move
    }

    // One legal player command, already resolved against a target (or not,
    // for Move). Immutable so a policy can hold one across a Choose call
    // without a caller mutating it out from under it.
    //
    // THE ONLY PLACE bot code calls ExecuteAttack / CastSkill / UseConsumable
    // / Move -- see Apply below. A policy never touches
    // FightSession's commands directly, so every archetype goes through the
    // same legality and dispatch rules.
    public readonly struct FightAction
    {
        public readonly FightActionKind Kind;
        public readonly CombatantState Target;

        // Valid only when Kind == Move. Forward is toward rank 0.
        public readonly MoveDirection MoveDirection;

        // Valid only when Kind == Skill: the index into the actor's own
        // authored skill list, the same index ResolvedSkillOption.Index and
        // CastSkill(int, CombatantState) both use.
        public readonly int SkillIndex;

        // Valid only when Kind == Skill, and only for a skill that authored an
        // elements list -- null for every other cast, which is every cast but
        // the orb's. Carried on the action rather than decided at Apply time
        // because it is part of WHICH command this is: four elements against
        // one enemy are four different things a policy can weigh, exactly the
        // way four targets are.
        public readonly DamageType? Element;

        // Valid only when Kind == Skill and the skill's last pick is a seat
        // (SkillEffects.LastPickIsSeat -- Palace Passage): the seat, 0 = front,
        // that Target (the traveller) goes to, occupied or empty. -1 on every
        // other action. PLAN_BELLWETHER_KIT 3.6 -- one destination, not a
        // second target: an occupied seat names its occupant through the
        // board, so the action never has to.
        public readonly int DestinationSeat;

        // Valid only when Kind == Item.
        public readonly string ItemId;
        public readonly string ItemDisplayName;
        public readonly bool ItemRestoresMana;

        // WHICH COPY, for an item: the satchel stack's own instance, so the
        // session learns whether it is a fake and the run spends that exact
        // stack. Null on every other kind.
        public readonly ItemInstance ItemInstance;

        // Stands in for the real item's potency. SatchelStack (FightHudModel.cs)
        // carries an id, a display name, a count and whether the item restores
        // mana or health -- not how MUCH it restores; that number lives on the
        // Core-side item content FightBootstrap already reads when it calls
        // UseConsumable in the real game. CombatMath.Heal/RestoreMana both
        // clamp to the actor's max (see FightSession.Items.cs), so handing
        // UseConsumable a large sentinel amount reproduces "the potion always
        // tops the actor off" without Domain needing to know the item's real
        // number -- which is a fine proxy for what the bot is deciding
        // (WHICH item, not how much it heals for).
        public const int ItemAmountProxy = 999999;

        public FightAction(
            FightActionKind kind,
            CombatantState target = null,
            int skillIndex = -1,
            string itemId = null,
            string itemDisplayName = null,
            bool itemRestoresMana = false,
            MoveDirection moveDirection = MoveDirection.Forward,
            DamageType? element = null,
            ItemInstance itemInstance = null,
            int destinationSeat = -1)
        {
            DestinationSeat = destinationSeat < 0 ? -1 : destinationSeat;
            ItemInstance = itemInstance;
            Element = element;
            Kind = kind;
            Target = target;
            MoveDirection = moveDirection;
            SkillIndex = skillIndex;
            ItemId = itemId ?? "";
            ItemDisplayName = itemDisplayName ?? "";
            ItemRestoresMana = itemRestoresMana;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case FightActionKind.Attack: return $"Attack({Target?.Name})";
                case FightActionKind.Skill when DestinationSeat >= 0:
                    return $"Skill[{SkillIndex}]({Target?.Name}->seat {DestinationSeat})";
                case FightActionKind.Skill:
                    return Element.HasValue
                        ? $"Skill[{SkillIndex}]:{Element.Value}({Target?.Name})"
                        : $"Skill[{SkillIndex}]({Target?.Name})";
                case FightActionKind.Item: return $"Item({ItemDisplayName})";
                default: return $"Move({MoveDirection})";
            }
        }

        // Every legal command for `actor` right now, read entirely off the
        // session's own queries -- EligibleTargets/CanReachEnemy for who can be
        // hit, SkillOptionsFor filtered to Ready for what can be cast,
        // CanMove for where the actor can step, and the satchel handed in for
        // what can be drunk.
        //
        // What guarantees this list is never empty is Attack, not the pass
        // action. Move does not, because a rooted character cannot step at
        // all (a solo one can step into an empty seat since field seats,
        // PLAN_BELLWETHER_KIT 1.1, but not while rooted). On a live player
        // turn the opposing rank 0 always exists and is always
        // melee-reachable (Provoke aside, and Provoke is player-side -- a
        // provoked enemy is still reachable, it is only the enemy's own
        // choice of target that a taunt narrows).
        //
        // A rooted actor may not swing at all (plan 1.10), so Attack is not
        // unconditional either -- but a rooted actor with nothing left never
        // reaches a policy: FightSession.ResolveSkippedTurn forfeits its
        // turn inside AutoResolveEnemyTurns, before control is handed back, so
        // this list is still never asked for one. The guarantee is
        // "Attack, or the turn was already skipped".
        //
        // See FightInvariants' "no legal action" check, which is what would
        // catch this claim going wrong.
        public static IReadOnlyList<FightAction> LegalActions(
            FightSession session, CombatantState actor, IReadOnlyList<SatchelStack> satchel)
        {
            var actions = new List<FightAction>();
            if (session == null || actor == null) return actions;

            // ASKED OF THE SESSION, PER REACH. Every single-opponent command
            // carries its own Reach and the session answers which targets it
            // can land on -- a plain swing with Reach.Melee, an authored skill
            // with whatever it declared. Getting this wrong would offer Attack
            // on a target CanReachEnemy then refuses, or deny a ranged skill a
            // target the real menu allows. Same primitive
            // FightController.Input's own click handler gates on.
            // AND THE SWING HAS TO BE LEGAL FOR THE ACTOR, not merely aimed at
            // somebody reachable (plan 1.10, milestone D). Asked ONCE outside
            // the loop: the answer is about the actor's statuses and does not
            // change per target, and asking it per target would read as though
            // it might.
            bool canSwing = CombatActions.PlainAttackIsLegalFor(actor, out _);
            if (canSwing)
            {
                foreach (var target in session.EligibleTargets(actor, Reach.Melee))
                {
                    actions.Add(new FightAction(FightActionKind.Attack, target));
                }
            }

            foreach (var option in session.SkillOptionsFor(actor))
            {
                if (!option.Ready) continue;

                // ONE ACTION PER ENEMY ONLY FOR SkillTargeting.SingleEnemy --
                // one of the two targetings the resolve path actually reads
                // `target` for (FightSession.Skills.ResolveDamageSingle/
                // Provoke/...; SingleAlly is the other, handled below).
                // Self (HealSelf/Transform/Summon), AllEnemies
                // (DamageAll/Shatter) and Party (HealParty/BuffParty/
                // RestorePartyMana) all resolve off the actor and
                // ignore the target parameter outright (ResolveCharacterSkillInner
                // passes `actor` to BeginBeat/SkillResolution.Amount for every
                // one of them) -- looping this over every enemy would hand a
                // bot policy N identical copies of the same cast that
                // differ only in a Target field nothing downstream reads,
                // which is not "more options", it is the same option wearing
                // a different enemy's name. A Skill Choose() has to filter
                // "which candidates target enemy X" now leans on that Target
                // meaning something -- see DamagingTargetSelection.
                // ONE ACTION PER ELEMENT, on top of whatever target loop the
                // skill would have got anyway. A skill that offers no choice
                // yields the single null-element action it always did, so every
                // existing kit's legal list is unchanged entry for entry.
                //
                // A CAST WITH NO ELEMENT IS NOT OFFERED for a skill that asks
                // for one: FightSession refuses it, so a policy that picked it
                // would burn a turn on a refusal that spends nothing and never
                // advances the fight.
                var elements = ElementsOf(option.Skill);

                // ONE ACTION PER ELIGIBLE ALLY for SkillTargeting.SingleAlly,
                // exactly as SingleEnemy gets one per eligible enemy -- these
                // ARE different commands now, not one command wearing three
                // names: CastSkill reads `target` for a ward and for all
                // three gifts, so warding Bjorn and warding Odette are two
                // outcomes a policy can weigh. EligibleAllies is the same
                // list the party plates light up, so the bot's menu and the
                // player's cannot offer different squads.
                //
                // WHICH of them a thinking policy should choose is NOT
                // decided here: LegalActions states what is legal, and
                // AllyTargetSelection states what is preferred. That split is
                // what keeps RandomLegal genuinely uniform over the real menu
                // rather than over a menu somebody already narrowed for it.
                // A SEAT-DESTINATION CAST (Palace Passage) IS OFFERED BELOW,
                // not here: SeatDestinationActions lists one action per
                // traveller per seat through the session's own
                // CanCastToSeat (PLAN_BELLWETHER_KIT M6). WHEN a bot takes it
                // is TelegraphAnswer's rule; RandomLegal may take it any time,
                // and the scoring archetypes see a non-damaging skill.
                //
                // EVERY MULTI-PICK CAST IS SKIPPED HERE, NOT OFFERED-AND-
                // REFUSED: Milestone B shipped a skill the bot could pick and the session always refused, and
                // BalanceBotSmokeTests found it as a 60-command STALL. An
                // action the legal menu never contains cannot stall anything.
                if (SkillEffects.PicksRequired(option.Skill.Effect) > 1) continue;

                if (option.Skill.Targeting == SkillTargeting.SingleAlly)
                {
                    foreach (var ally in session.EligibleAllies(actor, option.Skill))
                    {
                        foreach (var element in elements)
                        {
                            actions.Add(new FightAction(FightActionKind.Skill, ally, option.Index, element: element));
                        }
                    }

                    continue;
                }

                if (option.Skill.Targeting != SkillTargeting.SingleEnemy)
                {
                    foreach (var element in elements)
                    {
                        actions.Add(new FightAction(FightActionKind.Skill, actor, option.Index, element: element));
                    }

                    continue;
                }

                foreach (var target in session.EligibleTargets(actor, option.Skill.Reach))
                {
                    foreach (var element in elements)
                    {
                        actions.Add(new FightAction(FightActionKind.Skill, target, option.Index, element: element));
                    }
                }
            }

            if (satchel != null)
            {
                foreach (var stack in satchel)
                {
                    if (stack.Count <= 0) continue;

                    // A POTION THIS ACTOR'S POOL REFUSES IS NOT A LEGAL
                    // ACTION, by the same rule the Move block below states:
                    // a row the menu would refuse never reaches a policy.
                    // FightHudModel.ItemRefusedBy is the predicate the greyed
                    // row and the session's own press both read, so a mana
                    // draught in front of a Fury holder is offered to nobody
                    // -- and cannot be picked into a command the session
                    // refuses, which spends no turn and would leave the run
                    // loop asking the same actor forever.
                    if (FightHudModel.ItemRefusedBy(stack.RestoresMana, actor.PrimaryPool)) continue;

                    actions.Add(new FightAction(
                        FightActionKind.Item, actor,
                        itemId: stack.ItemId, itemDisplayName: stack.DisplayName,
                        itemRestoresMana: stack.RestoresMana, itemInstance: stack.Instance));
                }
            }

            actions.AddRange(SeatDestinationActions(session, actor));

            // BOTH DIRECTIONS ASKED SEPARATELY -- CanMove is the same query
            // the verb row reads, so a row the menu would refuse never
            // reaches a policy either.
            if (session.CanMove(actor, MoveDirection.Forward))
            {
                actions.Add(new FightAction(FightActionKind.Move, moveDirection: MoveDirection.Forward));
            }

            if (session.CanMove(actor, MoveDirection.Back))
            {
                actions.Add(new FightAction(FightActionKind.Move, moveDirection: MoveDirection.Back));
            }

            return actions;
        }

        // EVERY LEGAL SEAT-DESTINATION CAST for `actor` right now: one action
        // per ready Palace Passage, per eligible traveller, per seat the
        // session would accept (occupied or empty, never the traveller's own).
        // Asked through FightSession.CanCastToSeat, the refusal list the cast
        // itself runs, so nothing here can be refused on Apply. Part of
        // LegalActions since M6; `actor` must be the session's Current, as
        // for every command (any other actor gets none).
        public static IReadOnlyList<FightAction> SeatDestinationActions(FightSession session, CombatantState actor)
        {
            var actions = new List<FightAction>();
            if (session == null || actor == null || !ReferenceEquals(session.Current, actor)) return actions;

            foreach (var option in session.SkillOptionsFor(actor))
            {
                if (!option.Ready || !SkillEffects.LastPickIsSeat(option.Skill.Effect)) continue;

                foreach (var traveller in session.EligibleAllies(actor, option.Skill))
                {
                    for (int seat = 0; seat < CombatEncounter.SeatsPerSide; seat++)
                    {
                        if (!session.CanCastToSeat(option.Index, traveller, seat)) continue;

                        actions.Add(new FightAction(FightActionKind.Skill, traveller, option.Index,
                            destinationSeat: seat));
                    }
                }
            }

            return actions;
        }

        // The elements a skill can be cast as, as a list a caller can always
        // loop over: exactly one entry -- null -- for a skill that offers no
        // choice, which keeps the two cases one loop instead of two branches.
        private static IReadOnlyList<DamageType?> ElementsOf(ResolvedSkill skill)
        {
            if (skill == null || !skill.HasElementChoice) return NoElement;

            var elements = new List<DamageType?>(skill.Elements.Length);
            foreach (var choice in skill.Elements)
            {
                if (choice != null) elements.Add(choice.Type);
            }

            return elements.Count == 0 ? NoElement : elements;
        }

        private static readonly DamageType?[] NoElement = { null };

        // THE SKILL AS THIS ACTION WOULD ACTUALLY CAST IT -- retyped when the
        // action names an element, the option's own skill otherwise. Every
        // policy that scores a cast through the session's preview goes through
        // here, so a scoring read and the cast that follows it describe the
        // same spell. Same seam FightController.Hud.AsChosen is on the view
        // side.
        public static ResolvedSkill CastAs(ResolvedSkill skill, FightAction action) =>
            action.Element.HasValue && skill != null && skill.Offers(action.Element.Value)
                ? skill.AsElement(action.Element.Value)
                : skill;

        // THE ONE PLACE A POLICY ASKS "what would this Skill action deal" --
        // 0 for a non-damaging skill, same as the three call sites this
        // replaces (GreedyAggressivePolicy.EstimateDamage, GreedyDefensivePolicy.
        // EstimateDamage, Lookahead2Policy.ScoreOf/NonProgressingSkillKey), all
        // of which hand-rolled option.Skill.Effect == DamageSingle ||
        // == DamageAll before asking PreviewSkillPower. ResolvedSkill.IsDamaging
        // is that same test, named once.
        //
        // CRITS AS EXPECTED VALUE (CritRules.ExpectedDamage): a party member's
        // chance x bonus rides on top of the session's preview, which itself
        // stays exact (it is also the HUD's POWER row, a promise about one
        // cast rather than an average). Pure arithmetic, no draw, so the bot
        // stays deterministic. An enemy's authored crit is exact and reaches a
        // policy through EnemyIntent.ExpectedDamage instead.
        //
        // THE FURY TIER THE CAST WOULD FIRE IS IN THE FIGURE. PreviewSkillPower
        // is the HUD's POWER row and quotes the base, tier-free number, so a
        // policy ranking Slam at x4 against Headsplitter's all-in against
        // Hack's two blows was comparing a x4 hit's base to the others' whole.
        public static int PreviewDamage(
            FightSession session, CombatantState actor, ResolvedSkillOption option, FightAction action)
        {
            if (!option.Skill.IsDamaging) return 0;

            var cast = CastAs(option.Skill, action);
            int power = session.PreviewSkillPower(actor, cast, action.Target);
            power = PoolTierResolution.ApplyDamageMultiplier(power, PoolTierResolution.Pick(actor.PrimaryPool, cast.PoolTiers));
            return CritRules.ExpectedDamage(power, actor, action.Target);
        }

        // The one place a policy asks "what would this Attack action deal" --
        // the raw swing (CombatMath.ComputeAttackDamage) with crits counted as
        // expected value, the same treatment PreviewDamage gives a cast. The
        // three policies used to each call ComputeAttackDamage directly; one
        // seam means none of them can forget the crit.
        public static int PreviewAttackDamage(CombatantState actor, CombatantState target) =>
            CritRules.ExpectedDamage(CombatMath.ComputeAttackDamage(actor, target), actor, target);

        // The last thing left on the menu, for a policy whose own scoring has
        // run out of opinions.
        //
        // Attack first, because that is what carries the never-empty
        // guarantee (see LegalActions above) -- and `legal[0]` rather than a
        // First(...) that throws if the guarantee ever changes shape again.
        // `legal.First(a => a.Kind == HoldBack)` would be an exception
        // waiting for the day Hold Back stops being unconditional -- and it
        // is not unconditional, so this uses Attack and `legal[0]` instead.
        public static FightAction LastResort(IReadOnlyList<FightAction> legal)
        {
            foreach (var action in legal)
            {
                if (action.Kind == FightActionKind.Attack) return action;
            }

            return legal[0];
        }

        // Issues the one session command this action names. Nothing else in
        // bot code may call ExecuteAttack/CastSkill/UseConsumable/Move
        // directly -- see this type's own header.
        public static void Apply(FightSession session, FightAction action)
        {
            if (session == null) return;

            switch (action.Kind)
            {
                case FightActionKind.Attack:
                    session.ExecuteAttack(action.Target);
                    break;
                case FightActionKind.Skill when action.DestinationSeat >= 0:
                    session.CastSkillToSeat(action.SkillIndex, action.Target, action.DestinationSeat);
                    break;
                case FightActionKind.Skill:
                    session.CastSkill(action.SkillIndex, action.Target, action.Element);
                    break;
                case FightActionKind.Item:
                    session.UseConsumable(action.ItemDisplayName, ItemAmountProxy, action.ItemRestoresMana,
                        action.ItemInstance?.IsFake == true);
                    break;
                case FightActionKind.Move:
                    session.Move(action.MoveDirection);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "unknown FightActionKind");
            }
        }
    }
}
