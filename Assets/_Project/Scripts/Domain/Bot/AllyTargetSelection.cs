using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Bot
{
    // WHICH ally a thinking policy aims a SingleAlly cast at.
    //
    // WHERE THIS CAME FROM. Until AUDIT #147 these were engine rules:
    // FightSession.Talents.GiftRecipient picked "the ally missing the most
    // mana who can actually take it" for Gift: Mana and "the first living
    // ally" for Fury and Haste, and ApplyWard put the ward on the caster and
    // the Flock's share on the first living non-caster. The owner's call was
    // that deciding for the PLAYER is "just stupid" -- so the player now
    // picks, and the rules did not stop being good ones, they stopped being
    // the engine's. They live here, where the only caller left that must
    // choose with no hand on the mouse is.
    //
    // NARROWING, NOT CHOOSING. FightAction.LegalActions emits one action per
    // eligible ally, which is what makes the bot's menu the same menu the
    // player sees -- RandomLegal draws uniformly over that and is meant to.
    // A thinking policy runs its legal list through Narrow first, which
    // collapses each SingleAlly skill's N actions to the one this would have
    // chosen, so the policy's own scoring never has to grow an opinion about
    // squadmates it was never written to weigh.
    public static class AllyTargetSelection
    {
        // Every SingleAlly action for a given skill index reduced to one --
        // the preferred recipient. Anything else in `legal` passes through
        // untouched and in order, so a policy that ranks by list position
        // (several do) sees the same shape it always did.
        //
        // Returns `legal` itself when there was nothing to narrow, which is
        // every turn of every fight with no ally-facing skill in the kit.
        public static IReadOnlyList<FightAction> Narrow(
            FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal)
        {
            if (session == null || actor == null || legal == null) return legal;

            var kit = session.KitFor(actor);
            if (kit == null) return legal;

            // The preferred target per skill index, computed once and only
            // for the indices that actually appear -- a kit of eight skills
            // with one gift in it must not pay for eight lookups.
            Dictionary<int, CombatantState> preferred = null;

            foreach (var action in legal)
            {
                var skill = SingleAllySkillOf(kit, action);
                if (skill == null) continue;

                preferred = preferred ?? new Dictionary<int, CombatantState>();
                if (preferred.ContainsKey(action.SkillIndex)) continue;

                preferred[action.SkillIndex] =
                    Preferred(skill.Effect, actor, session.EligibleAllies(actor, skill));
            }

            if (preferred == null) return legal;

            var narrowed = new List<FightAction>(legal.Count);
            foreach (var action in legal)
            {
                var skill = SingleAllySkillOf(kit, action);
                if (skill != null
                    && preferred.TryGetValue(action.SkillIndex, out var wanted)
                    && !ReferenceEquals(action.Target, wanted))
                {
                    continue;
                }

                narrowed.Add(action);
            }

            return narrowed;
        }

        // WHO THE THREE MOVED RULES NAME, given the candidates the session
        // already filtered (AllyTargeting decides who is ELIGIBLE; this
        // decides who is BEST among them, and must never widen that list).
        //
        // Null for an empty list, which a caller reaches only for a skill
        // LegalActions therefore emitted no action for at all.
        public static CombatantState Preferred(
            SkillEffect effect, CombatantState actor, IReadOnlyList<CombatantState> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            switch (effect)
            {
                // "EMPTIEST FIRST", not "first that qualifies": a gift is a
                // fixed percentage of the recipient's own maximum, so handing
                // it to whoever is missing the most is the only reading under
                // which the number the caster spent wool for is the number
                // that lands. LINQ's OrderByDescending is stable, so a tie
                // falls back to party order.
                case SkillEffect.GiftMana:
                    return candidates.OrderByDescending(a => a.MaxMana - a.CurrentMana).First();

                // HIS OWN BACK FIRST. The ward was the caster's own for the
                // whole of the talent's life and the Lamb is the frailest
                // thing on the field; a bot that started wrapping somebody
                // else by default would be a behaviour change smuggled in
                // under a targeting change. An ally is taken only when the
                // caster is somehow not a candidate for his own ward.
                case SkillEffect.Ward:
                    return candidates.FirstOrDefault(a => ReferenceEquals(a, actor)) ?? candidates[0];

                // Gift: Fury and Gift: Haste say nothing about a resource --
                // one applies Empowered, the other moves a turn up the order
                // -- so every living ally is a valid recipient and the first
                // one is as good a pick as any. That was the engine's answer
                // and it is kept verbatim.
                default:
                    return candidates[0];
            }
        }

        // The authored skill behind one legal action, but ONLY when that
        // skill is SingleAlly -- null for an Attack, an Item, a Move, and for
        // every cast whose target the resolve path does not read.
        private static ResolvedSkill SingleAllySkillOf(PlayerKit kit, FightAction action)
        {
            if (action.Kind != FightActionKind.Skill) return null;
            if (action.SkillIndex < 0 || action.SkillIndex >= kit.Skills.Count) return null;

            var skill = kit.Skills[action.SkillIndex];
            return skill != null && skill.Targeting == SkillTargeting.SingleAlly ? skill : null;
        }
    }
}
