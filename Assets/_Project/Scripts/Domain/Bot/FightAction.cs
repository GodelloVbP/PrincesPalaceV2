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

        // Valid only when Kind == Item.
        public readonly string ItemId;
        public readonly string ItemDisplayName;
        public readonly bool ItemRestoresMana;

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
            DamageType? element = null)
        {
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
                case FightActionKind.Skill:
                    return Element.HasValue
                        ? $"Skill[{SkillIndex}]:{Element.Value}({Target?.Name})"
                        : $"Skill[{SkillIndex}]({Target?.Name})";
                case FightActionKind.Item: return $"Item({ItemDisplayName})";
                default: return $"Move({MoveDirection})";
            }
        }

        // Every legal command for `actor` right now, read entirely off the
        // session's own queries -- EligibleTargets/CanReach for who can be
        // hit, SkillOptionsFor filtered to Ready for what can be cast,
        // CanMove for where the actor can step, and the satchel handed in for
        // what can be drunk.
        //
        // WHAT GUARANTEES THIS LIST IS NEVER EMPTY IS NOW ATTACK, not the
        // pass action. Hold Back used to be unconditionally legal and carried
        // the guarantee on its own; Move does not, because a solo party has
        // nowhere to step and a rooted character cannot step at all. The
        // guarantee moved to Attack: on a live player turn the opposing rank
        // 0 always exists and is always melee-reachable (Provoke aside, and
        // Provoke is player-side -- a provoked ENEMY is still reachable, it
        // is only the enemy's own choice of target that a taunt narrows).
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
            // on a target CanReach then refuses, or deny a ranged skill a
            // target the real menu allows. Same primitive
            // FightController.Input's own click handler gates on.
            foreach (var target in session.EligibleTargets(actor, Reach.Melee))
            {
                actions.Add(new FightAction(FightActionKind.Attack, target));
            }

            foreach (var option in session.SkillOptionsFor(actor))
            {
                if (!option.Ready) continue;

                // ONE ACTION PER ENEMY ONLY FOR SkillTargeting.SingleEnemy --
                // the only targeting the resolve path actually reads `target`
                // for (FightSession.Skills.ResolveDamageSingle/Provoke/...).
                // Self (HealSelf/Ward/Transform/Summon), AllEnemies
                // (DamageAll/Shatter) and Party (HealParty/BuffParty/
                // RestorePartyMana/GiftHaste) all resolve off the actor and
                // ignore the target parameter outright (ResolveCharacterSkillInner
                // passes `actor` to BeginBeat/SkillResolution.Amount for every
                // one of them) -- looping this over every enemy used to hand
                // a bot policy N identical copies of the same cast that
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
                        itemRestoresMana: stack.RestoresMana));
                }
            }

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
        public static int PreviewDamage(
            FightSession session, CombatantState actor, ResolvedSkillOption option, FightAction action) =>
            option.Skill.IsDamaging
                ? session.PreviewSkillPower(actor, CastAs(option.Skill, action))
                : 0;

        // THE LAST THING LEFT ON THE MENU, for a policy whose own scoring has
        // run out of opinions.
        //
        // Attack first, because that is what carries the never-empty
        // guarantee (see LegalActions above) -- and `legal[0]` rather than a
        // First(...) that throws if the guarantee ever changes shape again.
        // The two greedy policies both used to end on
        // `legal.First(a => a.Kind == HoldBack)`, which was an exception
        // waiting for the day Hold Back stopped being unconditional. It has.
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
                case FightActionKind.Skill:
                    session.CastSkill(action.SkillIndex, action.Target, action.Element);
                    break;
                case FightActionKind.Item:
                    session.UseConsumable(action.ItemDisplayName, ItemAmountProxy, action.ItemRestoresMana);
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
