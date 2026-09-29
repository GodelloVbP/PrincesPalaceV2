using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Bot
{
    // THE BOT'S VALUATION OF THE SKILLS THAT DEAL NO DAMAGE OF THEIR OWN
    // (docs/PLAN_BJORN_CONSTELLATIONS.md Phase 4 intro and section 8 finding
    // 2): a policy that ranks actions by previewed damage scores Unbroken,
    // Cursed Blood and Second Wind at nothing, so a Juggernaut run would never
    // press them and the balance pass would measure a tree with its three
    // survival buttons welded shut.
    //
    // ONE PRE-PASS, like FightRunner's Transform handling, so every archetype
    // gets the same habit rather than three policies each growing its own
    // opinion of a heal: when a ready utility skill is worth casting NOW by
    // the rules below, play it; otherwise the policy decides as before. The
    // rules are plain habits, named for what they read, and read live state
    // only (health, statuses, the window flags), never a skill id -- the
    // skills are identified by their SkillEffect and shape.
    //
    //   Unbroken     -- the reset button: at or below UnbrokenBelowHealth of
    //                   max health, when no Unstoppable window is already
    //                   running.
    //   Cursed Blood -- the nuke: when the heals about to land on him over its
    //                   window (Regen statuses and Thick Blood's curve regen),
    //                   capped by his missing health, dealt to every enemy,
    //                   add up to at least CursedBloodMinWorth of his max
    //                   health. Turns Unbroken's regen into damage.
    //   Second Wind  -- a heal that spends all his Fury: at or below
    //                   SecondWindBelowHealth, or, under Cursed Blood's window
    //                   (where it converts to damage), whenever a real share
    //                   of his health is missing.
    //
    // The Sentinel's three, same shape (identified by effect and fields):
    //
    //   Plant the Shield -- whenever it is legal: a shield that is not down is
    //                       soaking nothing, and the legal menu already holds
    //                       the wait and the shield-already-down refusals.
    //   Hold the Line    -- a party buff (BuffParty of Fortified) when nobody
    //                       in the party carries it yet.
    //   Bellow           -- a Provoke, at an enemy that is not already
    //                       provoked, when an ally other than the caster is
    //                       below BellowAllyBelowHealth of his health (the
    //                       moment a redirect is worth a turn), or when the
    //                       cast pays him Fury.
    public static class UtilitySkills
    {
        // Habits, not balance numbers: the same rough shape GreedyDefensive's
        // own "hurt" line has, which is the point at which a player would
        // reach for a reset.
        public const float UnbrokenBelowHealth = 0.35f;
        public const float SecondWindBelowHealth = 0.35f;

        // Cursed Blood is worth its Fury once its window would convert at
        // least this share of his max health per enemy.
        public const float CursedBloodMinWorth = 0.2f;

        // Second Wind under Cursed Blood needs this much health missing to be
        // worth a whole Fury bar as a nuke.
        public const float SecondWindNukeMissing = 0.25f;

        // Bellow is worth a turn once an ally is this hurt.
        public const float BellowAllyBelowHealth = 0.6f;

        // The command to play instead of asking the policy, or null.
        public static FightAction? Choose(
            FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal)
        {
            if (session == null || actor == null || legal == null || actor.MaxHealth <= 0) return null;
            if (!session.Encounter.LivingEnemies.Any()) return null;

            FightAction? unbroken = null;
            FightAction? cursed = null;
            FightAction? secondWind = null;
            FightAction? plant = null;
            FightAction? hold = null;
            FightAction? bellow = null;

            foreach (var action in legal)
            {
                if (action.Kind != FightActionKind.Skill) continue;

                var option = session.SkillOptionsFor(actor).FirstOrDefault(o => o.Index == action.SkillIndex);
                var skill = option.Skill;
                if (skill == null) continue;

                if (skill.Effect == SkillEffect.Unbroken) unbroken = unbroken ?? action;
                else if (skill.Effect == SkillEffect.CursedBlood) cursed = cursed ?? action;
                else if (IsAllInHeal(skill)) secondWind = secondWind ?? action;
                else if (skill.Effect == SkillEffect.PlantShield) plant = plant ?? action;
                else if (IsPartyFortify(skill)) hold = hold ?? action;
                else if (skill.Effect == SkillEffect.Provoke && !bellow.HasValue && IsUnprovoked(action.Target)
                         && BellowWorthIt(session, actor)) bellow = action;
            }

            if (unbroken.HasValue && UnbrokenWorthIt(actor)) return unbroken;
            if (cursed.HasValue && CursedBloodWorthIt(actor)) return cursed;
            if (secondWind.HasValue && SecondWindWorthIt(actor)) return secondWind;
            if (plant.HasValue) return plant;
            if (hold.HasValue && !AnyAllyFortified(session, actor)) return hold;
            if (bellow.HasValue) return bellow;

            return null;
        }

        private static bool IsPartyFortify(ResolvedSkill skill) =>
            skill.Effect == SkillEffect.BuffParty && skill.AppliesStatus == StatusEffectType.Fortified;

        private static bool IsUnprovoked(CombatantState enemy) =>
            enemy != null && !enemy.Statuses.Any(s => s.Type == StatusEffectType.Provoked);

        private static bool AnyAllyFortified(FightSession session, CombatantState actor) =>
            session.Encounter.AlliesOf(actor).Any(a => a.IsAlive
                && a.Statuses.Any(s => s.Type == StatusEffectType.Fortified));

        // The redirect is worth a turn when an ally other than the caster is
        // hurt, or when the cast itself pays him Fury.
        private static bool BellowWorthIt(FightSession session, CombatantState actor)
        {
            if (actor.Talents.Best(TalentEffectType.PrimaryGainPerProvokedEnemy) > 0) return true;

            return session.Encounter.AlliesOf(actor).Any(a => a.IsAlive && !ReferenceEquals(a, actor)
                && a.MaxHealth > 0 && (float)a.CurrentHealth / a.MaxHealth < BellowAllyBelowHealth);
        }

        // A HealSelf that spends the whole Fury bar (Second Wind's shape).
        private static bool IsAllInHeal(ResolvedSkill skill) =>
            skill.Effect == SkillEffect.HealSelf && skill.SpendsAllPrimary;

        private static float HealthFraction(CombatantState actor) =>
            actor.MaxHealth <= 0 ? 1f : (float)actor.CurrentHealth / actor.MaxHealth;

        public static bool UnbrokenWorthIt(CombatantState actor) =>
            HealthFraction(actor) <= UnbrokenBelowHealth && !actor.CrowdControl.Unstoppable.IsOpen;

        public static bool SecondWindWorthIt(CombatantState actor)
        {
            if (actor.HealConversion.IsActive)
            {
                return actor.MaxHealth - actor.CurrentHealth >= SecondWindNukeMissing * actor.MaxHealth;
            }

            return HealthFraction(actor) <= SecondWindBelowHealth;
        }

        // What his window would convert, per enemy: the heals he will receive
        // over CursedBloodWindowTurns of his own turns, at today's figures,
        // capped by what is missing (the effective heal, as the mechanic
        // converts it).
        public const int CursedBloodWindowTurns = 2;

        public static bool CursedBloodWorthIt(CombatantState actor)
        {
            if (actor.HealConversion.IsActive) return false;

            int missing = actor.MaxHealth - actor.CurrentHealth;
            if (missing <= 0) return false;

            int perTurn = 0;
            foreach (var status in actor.Statuses)
            {
                if (status.Type == StatusEffectType.Regen && status.Magnitude > 0) perTurn += status.Magnitude;
            }

            int curve = actor.Talents.Best(TalentEffectType.HealthCurveRegen);
            if (curve > 0)
            {
                perTurn += HealthCurveRegen.AmountFor(actor.CurrentHealth, actor.MaxHealth,
                    actor.Talents.Threshold(TalentEffectType.HealthCurveRegen), curve);
            }

            int converted = System.Math.Min(missing, perTurn * CursedBloodWindowTurns);
            return converted >= CursedBloodMinWorth * actor.MaxHealth;
        }
    }
}
