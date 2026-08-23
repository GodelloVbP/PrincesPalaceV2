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

            // Ballerina's Slippers: warming up, to a ceiling, for the rest of
            // the fight. Silent once the cap is reached rather than repeating
            // a line every swing about a bonus that is no longer growing.
            if (HasRelic(actor, RelicEffect.BallerinasSlippers))
            {
                int gained = GrantSpeedPercent(actor, RelicEffect.BallerinasSlippers,
                    FightTuning.SlippersPercentPerSwing, turns: 0,
                    capPercent: FightTuning.SlippersCapPercent);

                if (gained > 0) AppendMessage($"{actor.Name} finds their footing - faster.");
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

            // Tin-Foil Pipe: quick until their next turn. Refreshes rather
            // than stacks -- see GrantSpeedPercent.
            if (HasRelic(actor, RelicEffect.TinFoilPipe)
                && GrantSpeedPercent(actor, RelicEffect.TinFoilPipe,
                    FightTuning.PipePercent, FightTuning.PipeTurns) > 0)
            {
                AppendMessage($"{actor.Name}'s pipe crackles - everything speeds up for a moment.");
            }
        }

        // ---- the one funnel every landed blow goes through -----------------------
        //
        // FOUR CALL SITES USED TO WRITE THIS EXPRESSION BY HAND: a plain
        // swing, a single-target skill, an all-enemies sweep, and the
        // Assassin's execute branch. All four read
        //
        //     outcome.Damage + PotencyBonus(baseAmount) + NecklaceDamageBonus(actor, baseAmount)
        //
        // which is not a coincidence, it is the same sentence copied four
        // times -- and copying it is exactly what let the sweep ship TWICE
        // without one of the two bonuses: the Charging Crystal's PotencyBonus
        // was wired into the single-target path and the plain swing and
        // forgotten on the sweep, and months later the necklace's
        // NecklaceDamageBonus would have repeated the identical gap if it had
        // been added the same way a second time.
        //
        // Every damage-modifying relic from here on adds its bonus INSIDE
        // this one function. A new one can be forgotten at a call site only if
        // every call site is rewritten to forget it, which is not a mistake
        // copy-paste makes by accident.
        //
        // `outcomeDamage` rather than `outcome.Damage` taken directly, because
        // the execute branch needs to stack these bonuses on top of a figure
        // ALREADY multiplied by the execute bonus, not on top of the pipeline's
        // raw output -- the two are different numbers on that one path.
        private int TotalDamage(CombatantState actor, int baseAmount, int outcomeDamage) =>
            outcomeDamage + PotencyBonus(baseAmount) + NecklaceDamageBonus(actor, baseAmount);

        // ---- the necklace --------------------------------------------------------
        //
        // NOT AN EVENT. Every other relic here reacts to something happening;
        // this one is a function of how hurt the wearer is right now, and it
        // has to be true continuously rather than at a moment.
        //
        // Which makes it two different problems wearing one name. The DAMAGE
        // half is asked at the instant a blow is computed and needs no state at
        // all -- a pure function of current health. The SPEED half cannot work
        // that way: speed is a stored number the turn order was told about, so
        // it has to be pushed when it changes rather than pulled when it is
        // read. Recomputed at turn start, which is the only moment the order
        // can act on it anyway.
        //
        // Full value at a quarter health, nothing at full, straight ramp
        // between. The floor is a QUARTER rather than zero because a bonus that
        // only pays at 1hp pays on the turn you die.
        private int NecklaceRampPercent(CombatantState actor, int maxPercent)
        {
            if (actor == null || actor.MaxHealth <= 0) return 0;
            if (!HasRelic(actor, RelicEffect.ToothedNecklace)) return 0;

            int healthPercent = actor.CurrentHealth * 100 / actor.MaxHealth;
            int floor = FightTuning.NecklaceFloorHealthPercent;

            if (healthPercent >= 100) return 0;
            if (healthPercent <= floor) return maxPercent;

            // How far down the ramp, from full health to the floor.
            int travelled = 100 - healthPercent;
            int total = 100 - floor;

            return maxPercent * travelled / total;
        }

        // The damage half, asked at the moment a blow is computed.
        private int NecklaceDamageBonus(CombatantState actor, int baseAmount)
        {
            if (baseAmount <= 0) return 0;

            int percent = NecklaceRampPercent(actor, FightTuning.NecklaceMaxDamagePercent);
            return percent <= 0 ? 0 : baseAmount * percent / 100;
        }

        // The speed half, pushed at turn start. Revoked and re-granted rather
        // than adjusted, because the ramp moves in both directions -- healing
        // has to give the speed back as surely as being hurt hands it over.
        private void RefreshNecklaceSpeed(CombatantState actor)
        {
            if (actor == null) return;

            int percent = NecklaceRampPercent(actor, FightTuning.NecklaceMaxSpeedPercent);
            int already = SpeedBonusFrom(actor, RelicEffect.ToothedNecklace);

            if (percent <= 0)
            {
                if (already > 0) RevokeSpeedBuff(actor, RelicEffect.ToothedNecklace);
                return;
            }

            RevokeSpeedBuff(actor, RelicEffect.ToothedNecklace);
            GrantSpeedPercent(actor, RelicEffect.ToothedNecklace, percent, turns: 0);
        }

        // ---- the kill ------------------------------------------------------------

        // WHAT A BODY IS WORTH, scaled by what it was.
        //
        // Measured off the enemy's authored expReward rather than a level,
        // because a CombatantState has no level -- experience is the number
        // content already uses to say how much a monster is worth, and a second
        // measure of the same thing would drift from it.
        //
        // Player-side only. Nothing pays a monster for killing you.
        private void PayBounty(CombatantState actor, CombatantState victim)
        {
            if (actor == null || victim == null || !actor.IsPlayerSide) return;
            if (!HasRelic(actor, RelicEffect.BountyHunterContract)) return;

            int worth = SourceFor(victim)?.Source.ExpReward ?? 0;
            int paid = worth * FightTuning.BountyPerLevel / 10;
            if (paid <= 0) paid = 1;

            BountyEarned += paid;
            AppendMessage($"{actor.Name} collects on {victim.Name} - {paid} gold.");
        }

        // Banked on the session and read out by whoever settles the fight, the
        // same way experience and currency already travel. A relic reaching for
        // the save directly is what put v1's relic logic in a controller.
        public int BountyEarned { get; private set; }

        // ---- the rest ------------------------------------------------------------

        // CALLED ON EVERY KILL, not only when a relic wants it. Bloodlust keeps
        // chain state that has to be RESET when the chain breaks, so a call
        // skipped because nobody was carrying the relic would leave a stale
        // count behind for whoever picks one up later in the fight.
        // ONCE PER ACTION, however many things that action killed.
        //
        // Bloodlust grants a turn, and a swing that fells two enemies should
        // not grant two. It also has to be called when NOTHING died -- see
        // below.
        private void RelicsOnKill(CombatantState actor)
        {
            TryGrantBloodlust(actor);
        }

        // ONCE PER BODY, which is a different moment and deliberately a second
        // method rather than a flag on the first.
        //
        // A bounty is paid for a corpse, so a splash that fells two pays twice.
        // Collapsing the two moments into one would force whichever relic was
        // written second to be wrong: Bloodlust would grant two turns, or the
        // Contract would pay for one of the two things it killed.
        private void RelicsOnEachKill(CombatantState actor, CombatantState victim)
        {
            PayBounty(actor, victim);
        }

    }
}
