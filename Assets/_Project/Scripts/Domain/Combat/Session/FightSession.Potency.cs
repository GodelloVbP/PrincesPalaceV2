using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // "EVERY NTH ACTION HITS HARDER", and what "harder" is measured against.
    //
    // THE RULE, because it is the part that is easy to get wrong and impossible
    // to notice once it is:
    //
    //     The bonus is a percentage of the action's OWN BASE, added AFTER
    //     everything that multiplies.
    //
    // A spell whose base is 50, doubled by something else, and then charged at
    // +50% deals 125 -- not 150. The doubling is worth 50 and the crystal is
    // worth 25, and the crystal is worth 25 whatever else is happening. Applied
    // the other way round, at the front of the pipeline, it would compound with
    // every multiplier it meets and be worth 25 against a resistant target and
    // 150 against a weak one wearing Vulnerable. A relic whose value swings by
    // six times depending on circumstances nobody is thinking about at the
    // moment they pick it up is not a choice, it is a lottery ticket.
    //
    // The same reasoning is why it is not applied to the POST-defence figure
    // either: armour would then scale the bonus, and a relic that is worth less
    // against armoured things is a second hidden interaction on top of the one
    // armour already has.
    //
    // COUNTED PER FIGHT, per character. A counter carried between fights makes
    // the first cast of a fight arbitrarily charged or not depending on how the
    // previous one happened to end -- which the player cannot see, cannot plan
    // around, and would experience as the relic being unreliable.
    //
    // Which needs no clearing: FightEncounterAdapter builds one FightSession per
    // encounter and Begin() refuses a second call, so these tallies are born
    // empty and die with the fight. A Reset method was written here first and
    // deleted -- it had no caller, and a dead one would read as evidence that
    // sessions ARE reused.
    public partial class FightSession
    {
        private readonly Dictionary<CombatantState, int> _spellsCast =
            new Dictionary<CombatantState, int>();

        private readonly Dictionary<CombatantState, int> _attacksMade =
            new Dictionary<CombatantState, int>();

        // The action being resolved right now, and how much of its own base it
        // owes on top. Zero for the overwhelming majority of actions.
        //
        // DECIDED ONCE PER ACTION, not once per target. A spell that hits three
        // enemies is ONE cast: it advances the counter once, and if that cast is
        // the charged one then every target it lands on takes the bonus. Asking
        // per target would make a sweep advance the counter three times and turn
        // "every fourth cast" into something nobody could predict.
        private int _potencyPercent;

        // Opens a cast. Returns nothing -- the answer is held rather than
        // returned because it has to survive across the several damage figures a
        // single cast can produce.
        private void BeginSpellPotency(CombatantState actor)
        {
            _potencyPercent = PotencyFor(actor, _spellsCast, RelicEffect.ChargingCrystal,
                FightTuning.ChargingCrystalEvery, FightTuning.ChargingCrystalPercent);
        }

        private void BeginAttackPotency(CombatantState actor)
        {
            _potencyPercent = PotencyFor(actor, _attacksMade, RelicEffect.LongCount,
                FightTuning.LongCountEvery, FightTuning.LongCountPercent);
        }

        // THE COUNTER ADVANCES WHETHER OR NOT THE RELIC IS HELD.
        //
        // It would be cheaper to skip counting for a character carrying neither
        // relic, and it would be wrong in one visible way: picking the crystal
        // up mid-fight would start its count from whatever the tally happened to
        // be, so the relic's first charged cast would arrive after a number of
        // casts the player has no way to know. Counting always means "every
        // fourth cast OF THIS FIGHT" is true from the moment it is equipped.
        private int PotencyFor(CombatantState actor, Dictionary<CombatantState, int> tally,
                               RelicEffect effect, int every, int percent)
        {
            if (actor == null) return 0;

            tally.TryGetValue(actor, out int count);
            count++;
            tally[actor] = count;

            if (every <= 0 || count % every != 0) return 0;

            return HasRelic(actor, effect) ? percent : 0;
        }

        // Closes an action. Called on every path out, so a cast that resolves
        // without dealing damage cannot leave the charge armed for whatever
        // happens next.
        private void EndPotency() => _potencyPercent = 0;

        // What the current action owes on top, computed from ITS OWN BASE.
        //
        // `baseAmount` is the figure the action produces before anything
        // outside it has a say -- SkillResolution.Amount's return, or a plain
        // swing's computed damage. Deliberately not the post-pipeline number.
        private int PotencyBonus(int baseAmount)
        {
            if (_potencyPercent <= 0 || baseAmount <= 0) return 0;

            return baseAmount * _potencyPercent / 100;
        }

    }
}
