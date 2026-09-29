using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat
{
    // MAY THIS ACTOR TAKE THIS ACTION AT ALL -- one predicate, read by every
    // site that has to agree about the answer (plan 1.10).
    //
    // THE RULE, as a trigger -> condition -> outcome:
    //
    //   an actor asks whether an action is legal
    //   -> if the actor is Rooted and the action is a physical move
    //   -> refuse, and say why. Otherwise the existing rules decide.
    //
    // FOUR CALLERS, and the reason they cannot each hold their own copy is
    // that three of them run at different MOMENTS of the same turn:
    //
    //   1. the player's menu (FightSession.SkillOptionsFor, and the verb row's
    //      plain attack) -- what may be offered;
    //   2. the enemy's draw (FightSession.Enemies.EffectivePoolFor) -- what may
    //      be chosen;
    //   3. the committed telegraph, re-validated at resolution
    //      (FightSession.Enemies.ResolveEnemyAction) -- an intent drawn before
    //      the root landed is voided rather than honoured;
    //   4. execution (FightSession.CastSkill and FightSession.ExecuteAttack) --
    //      the last word, so a command issued past the menu still refuses.
    //
    // A disagreement between any two of them is a bug the player sees as
    // either a greyed row that fires anyway or a telegraphed blow that never
    // lands, which is exactly the class of bug the Rooted plain-swing rule
    // already collected twice before this predicate existed.
    //
    // THIS IS NOT A REACH QUESTION. Reach asks where a blow may land and is
    // about the TARGET's rank; this asks whether the actor may move its body
    // and is about the ACTOR's statuses. They are checked side by side and
    // neither subsumes the other -- a rooted actor with a reachable target is
    // still refused, and an unrooted one with nobody in reach is still refused.
    //
    // NO RNG, EVER. Every caller here is asking, not committing --
    // RootedEnemyHasNoLegalAction leans on that to recompute an effective pool
    // at resolution time without spending a draw from the seeded stream.
    public static class CombatActions
    {
        // A PLAIN ATTACK IS PHYSICAL BY CONSTRUCTION. It has no content row to
        // author a classification on, and there is no swing that is not a
        // swing -- stated as a named constant rather than a bare `true` at the
        // three call sites so the claim has one place to be read and argued
        // with.
        public const bool PlainAttackIsPhysicalMove = true;

        // A null skill reads as not physical: nothing authored cannot be a
        // move. The plain attack does NOT come through here -- it has no
        // ResolvedSkill, and PlainAttackIsPhysicalMove is its answer.
        public static bool IsPhysicalMove(ResolvedSkill skill) => skill != null && skill.PhysicalMove;

        // THE PREDICATE. `refusal` is the player-facing sentence and is null
        // when the answer is yes.
        //
        // TAKES THE CLASSIFICATION, NOT THE ACTION. The three action shapes in
        // this game -- an authored skill, an enemy ability, a plain swing --
        // have three different types and one classification between them, so
        // the overloads below convert and this body holds the rule once.
        public static bool IsLegalFor(CombatantState actor, bool physicalMove, out string refusal)
        {
            refusal = null;
            if (actor == null) return true;

            // ROOTED MEANS "CANNOT MOVE TO ACT", which is what it has always
            // meant for the formation swap (FightSession.CanMove) and for the
            // monsters' plain swing (EffectivePoolFor). Velvet Shackles does
            // not introduce a second restriction status; it makes the existing
            // one's meaning total across every action, which is why every
            // Rooted source -- Sylvan's root modifier included -- now denies a
            // physical kit rather than only a bare swing. See plan 2.10.
            if (physicalMove && StatusEffects.HasRooted(actor.Statuses))
            {
                refusal = $"{actor.Name} is rooted and cannot strike, charge or move - only cast.";
                return false;
            }

            // SILENCED MEANS "CANNOT CAST", the mirror of Rooted above: a
            // move is not a spell, so a silenced actor may still strike. Any
            // action that is not a physical move is a cast here, which is the
            // same line the Spellbreaker's reflect draws ("magic").
            if (!physicalMove && actor.Suppression.IsSilenced)
            {
                refusal = $"{actor.Name} is silenced and cannot cast - only strike.";
                return false;
            }

            return true;
        }

        public static bool IsLegalFor(CombatantState actor, ResolvedSkill skill, out string refusal)
        {
            // A FORM CAN FORBID WHOLE KINDS OF CAST (Berserk: no shouts, no
            // shields). Stated on the form, asked here, so the menu, the
            // detail card, the bot's menu and the cast refusal all read one
            // answer.
            var form = actor?.Transformation;
            if (skill != null && form != null && form.Forbids(skill.Effect))
            {
                refusal = $"{actor.Name} cannot use {skill.DisplayName} while {form.DisplayName}.";
                return false;
            }

            // THE PLANTED SHIELD'S TWO BOARD-STATE RULES, asked here so the
            // menu, the bot and the cast's refusal read one answer: it cannot
            // be planted while one stands or the re-place wait runs, and a
            // Shield Bash has nothing to strike with unless one stands.
            if (skill != null && actor != null)
            {
                if (skill.Effect == SkillEffect.PlantShield && !actor.PlantedShield.CanPlace)
                {
                    refusal = actor.PlantedShield.IsPlaced
                        ? $"{actor.Name}'s shield is already planted."
                        : $"{actor.Name} cannot plant the shield again yet.";
                    return false;
                }

                if (skill.PlantedShieldBashPercent > 0 && !actor.PlantedShield.IsPlaced)
                {
                    refusal = $"{actor.Name} has no planted shield to {skill.DisplayName}.";
                    return false;
                }
            }

            return IsLegalFor(actor, IsPhysicalMove(skill), out refusal);
        }

        // The plain attack's own arm, so no caller has to remember which
        // constant the swing takes.
        public static bool PlainAttackIsLegalFor(CombatantState actor, out string refusal) =>
            IsLegalFor(actor, PlainAttackIsPhysicalMove, out refusal);
    }
}
