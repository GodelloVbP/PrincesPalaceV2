using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // WHEN A RELIC GETS TO ACT, in one place.
    //
    // Every relic effect in the game reaches into combat at exactly one moment,
    // and until this file existed each one found its own: Dual Wield at line 186
    // of FightSession, Magical Shield at 565 of Skills, Bloodlust at 75 of
    // Riders, and the two counting relics at two more. Five effects, five
    // hand-picked lines across four files, and no list anywhere of what the
    // available moments even were.
    //
    // That is the actual cost of a relic. The mechanic is rarely hard -- "gain a
    // shield", "swing twice", "take another turn" are a few lines each -- and
    // the expensive part is reading a large combat flow to work out where it
    // hooks, and being sure the place chosen fires exactly once, after the thing
    // it reacts to, and on every path that reaches it.
    //
    // THE MOMENTS ARE NOW NAMED AND THERE ARE FIVE. A new relic picks one and
    // adds a branch; it does not go looking.
    //
    //     BeforeSwing   -- the actor is about to make a plain attack
    //     AfterSwing    -- a plain attack has landed and its damage is known
    //     BeforeCast    -- the actor is about to resolve a spell or skill
    //     AfterCast     -- a cast has finished resolving
    //     OnKill        -- the actor's action killed something
    //
    // FIVE AND NOT SIX. An OnTurnStart was written here and deleted before it
    // shipped: nothing called it, and a moment nobody reacts to is a
    // maintenance burden pretending to be an abstraction. The list grows when a
    // relic needs it to, which costs one method and one call -- far less than
    // the confidence lost by a surface that is half real.
    //
    // WHAT THIS IS NOT is a data-driven relic language. RelicEffect's own header
    // makes that argument and it is right: "strike twice", "halve the next hit"
    // and "take another turn" have no shared shape, and a JSON grammar able to
    // express all three would be a programming language with worse tooling. The
    // WHEN is shared. The WHAT is not, and stays C#.
    //
    // Numeric relics do not appear here at all -- a relic that is a number
    // applied to a stat is authored entirely in relics.json through
    // RelicModifier, and always was.
    public partial class FightSession
    {
        // ---- the swing -----------------------------------------------------------

        private void RelicsBeforeSwing(CombatantState actor)
        {
            // The Long Count. Advances its tally and decides whether this swing
            // is the counted one; see FightSession.Potency for why the decision
            // is made here rather than where the damage is finally added up.
            BeginAttackPotency(actor);
        }

        // Damage is passed because a relic reacting to a swing almost always
        // wants to know how big it was -- and because reading it back off the
        // target afterwards would be wrong the moment anything else touches
        // their health in between.
        private void RelicsAfterSwing(CombatantState actor, CombatantState target, int damage)
        {
            EndPotency();

            // The Salt Ledger: a swing pays down what is owed.
            if (HasRelic(actor, RelicEffect.SaltLedger)
                && ReduceCooldowns(actor, FightTuning.SaltLedgerTurns) > 0)
            {
                AppendMessage($"{actor.Name}'s Salt Ledger settles a debt - cooldowns tick down.");
            }
        }

        // ---- the cast ------------------------------------------------------------

        private void RelicsBeforeCast(CombatantState actor)
        {
            // The Charging Crystal, on the same terms as the Long Count above.
            BeginSpellPotency(actor);
        }

        // `skill` is null for the basic Skill action, which has no
        // ResolvedSkill behind it -- it is the global spell tier, not a
        // character's own kit entry. Relics that care which spell it was must
        // handle that; relics that only care THAT one happened need not.
        private void RelicsAfterCast(CombatantState actor, ResolvedSkill? skill, CombatantState target)
        {
            EndPotency();

            // Magical Shield: the next hit taken is halved.
            RaiseMagicalShield(actor);
        }

        // ---- the rest ------------------------------------------------------------

        // CALLED ON EVERY KILL, not only when a relic wants it. Bloodlust keeps
        // chain state that has to be RESET when the chain breaks, so a call
        // skipped because nobody was carrying the relic would leave a stale
        // count behind for whoever picks one up later in the fight.
        private void RelicsOnKill(CombatantState actor)
        {
            TryGrantBloodlust(actor);
        }

    }
}
