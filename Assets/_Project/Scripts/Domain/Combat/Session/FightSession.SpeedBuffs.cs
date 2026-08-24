using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // SPEED THAT CHANGES DURING A FIGHT, and stays honest about it.
    //
    // Speed is not a status. It is the number that decides how often a
    // combatant acts -- the turn order is charge-based, so a combatant with
    // twice the speed genuinely takes twice the turns rather than merely going
    // first. That means a speed buff has to touch the real stat and the turn
    // order has to be told, or the number moves on the sheet and nothing about
    // the fight changes. CombatEncounter.RefreshSpeed exists for exactly that
    // reason and its own header says so.
    //
    // WHICH IS WHY THIS TRACKS WHAT IT GAVE. A percentage applied to a live
    // stat cannot be undone by applying the inverse -- +20% then -20% of the
    // new value does not return you to where you started, and a buff that
    // expires leaving the character permanently 4% faster is the kind of drift
    // nobody notices until a long fight ends with somebody twice their own
    // speed. So the bonus is stored as the FLAT amount actually handed over,
    // and taking it back subtracts precisely that.
    //
    // CAPPED IN THE SAME PLACE IT IS GRANTED. Ballerina's Slippers stack to a
    // ceiling; the ceiling has to be checked against what the relic has given
    // SO FAR, not against the combatant's current speed, or a character who is
    // fast for some other reason would hit the cap having gained nothing.
    public partial class FightSession
    {
        private sealed class SpeedBuff
        {
            // The flat points handed over, so they can be handed back exactly.
            public int Granted;

            // Turns left, or -1 for "until this fight ends". The Slippers are
            // permanent within a fight; the Pipe lasts until the next turn.
            public int TurnsLeft;
        }

        private readonly Dictionary<CombatantState, Dictionary<RelicEffect, SpeedBuff>> _speedBuffs =
            new Dictionary<CombatantState, Dictionary<RelicEffect, SpeedBuff>>();

        // THE TRUE BASE every grant measures against -- what this combatant's
        // Speed would be with NONE of the relic buffs and maluses in this
        // dictionary applied, from any source.
        //
        // FOUND AS A BUG, not designed in from the start. The first version
        // subtracted only the CURRENT source's own Granted, which stops a
        // relic compounding against ITSELF on a second grant, and does
        // nothing to stop it compounding against a DIFFERENT relic's grant
        // that landed first: Slippers +10% off 100 gives +10 (110), and Pipe's
        // +20% then read 110 as the base and gave +22 instead of the +20 a
        // true 20%-of-100 owes -- 132 instead of the 114 the two relics
        // together should produce. Proven with a throwaway test before this
        // was believed, the same way the potency funnel's compounding rule was
        // proven rather than assumed.
        //
        // Summing every source's Granted and subtracting it once is what makes
        // "against the true base" actually true regardless of how many speed
        // relics a character is carrying -- and past level 25 a character
        // carries more than one relic at all, so this is not an edge case.
        private int TrueBaseSpeed(CombatantState actor, Dictionary<RelicEffect, SpeedBuff> forActor)
        {
            int granted = 0;
            foreach (var buff in forActor.Values) granted += buff.Granted;
            return actor.Speed - granted;
        }

        // Percent of the combatant's speed AT THE MOMENT OF GRANTING, converted
        // to flat points immediately. Returns what was actually given, which is
        // 0 when the cap is already reached -- the caller uses that to decide
        // whether there is anything worth saying.
        private int GrantSpeedPercent(CombatantState actor, RelicEffect source, int percent,
                                      int turns, int capPercent = 0)
        {
            if (actor == null || percent <= 0 || actor.Speed <= 0) return 0;

            if (!_speedBuffs.TryGetValue(actor, out var forActor))
            {
                forActor = new Dictionary<RelicEffect, SpeedBuff>();
                _speedBuffs[actor] = forActor;
            }

            if (!forActor.TryGetValue(source, out var buff))
            {
                buff = new SpeedBuff();
                forActor[source] = buff;
            }

            // The TRUE base -- see TrueBaseSpeed. Every OTHER relic's grant is
            // subtracted too, not only this one's, or a second speed relic
            // reads a base that the first one already inflated.
            int baseSpeed = TrueBaseSpeed(actor, forActor);
            int wanted = baseSpeed * percent / 100;
            if (wanted <= 0) wanted = 1;

            if (capPercent > 0)
            {
                int ceiling = baseSpeed * capPercent / 100;
                if (buff.Granted + wanted > ceiling) wanted = ceiling - buff.Granted;
            }

            // REFRESHED, NOT STACKED, when it is a timed buff. A second cast
            // before the first expires resets the clock rather than doubling
            // the speed -- the same rule StatusEffects.Apply follows, and for
            // the same reason: paying the cost twice should not be strictly
            // better than paying it once and waiting.
            if (turns > 0) buff.TurnsLeft = turns;
            else buff.TurnsLeft = -1;

            if (wanted <= 0) return 0;

            buff.Granted += wanted;
            actor.Speed += wanted;
            _encounter.RefreshSpeed(actor);

            return wanted;
        }

        // THE MALUS TWIN, for a relic that SLOWS someone rather than
        // speeding them up -- Lucky Deck's one-turn slow, on an enemy.
        //
        // A SEPARATE METHOD rather than GrantSpeedPercent taught to accept a
        // negative percent, on purpose. That method's rounding rule
        // ("wanted <= 0 -> wanted = 1") exists specifically to guarantee a
        // BUFF is never rounded down to nothing, and its cap logic only makes
        // sense against a ceiling a positive grant is climbing toward. Neither
        // idea has an honest opposite for a malus, and bolting a sign flag
        // onto one function to cover both would make its rounding rule read
        // correctly for one sign and silently wrong for the other. Same
        // underlying SpeedBuff bookkeeping, own small function.
        //
        // Returns the (negative) amount actually taken, so a caller can tell
        // whether anything happened -- 0 for a combatant with no Speed to
        // take from.
        private int GrantSpeedMalusPercent(CombatantState actor, RelicEffect source, int percent, int turns)
        {
            if (actor == null || percent <= 0 || actor.Speed <= 0) return 0;

            if (!_speedBuffs.TryGetValue(actor, out var forActor))
            {
                forActor = new Dictionary<RelicEffect, SpeedBuff>();
                _speedBuffs[actor] = forActor;
            }

            if (!forActor.TryGetValue(source, out var buff))
            {
                buff = new SpeedBuff();
                forActor[source] = buff;
            }

            // The TRUE base -- see TrueBaseSpeed. Only relic-granted buffs and
            // maluses ever populate this dictionary, and today a malus and a
            // buff never land on the same combatant (Lucky Deck's slow only
            // ever targets an enemy, the buffs only ever the player who cast
            // them), but the fix is the same one the buff side needed and
            // costs nothing to apply here too.
            int baseSpeed = TrueBaseSpeed(actor, forActor);
            int wanted = -(baseSpeed * percent / 100);
            if (wanted >= 0) wanted = -1;

            // REFRESHED, NOT STACKED. A second slow landing before the first
            // wears off resets the one-turn clock rather than compounding the
            // malus -- the same rule the buff side follows, and the same
            // reason: two slows should not be worse than one applied twice.
            buff.TurnsLeft = turns > 0 ? turns : -1;

            // Never past the point of taking a combatant to 0 -- RevokeSpeedBuff
            // already floors at 1, and a malus larger than what remains of the
            // BASE would otherwise ask for more than there is to give back.
            if (buff.Granted + wanted < -baseSpeed) wanted = -baseSpeed - buff.Granted;
            if (wanted >= 0) return 0;

            buff.Granted += wanted;
            actor.Speed += wanted;
            if (actor.Speed < 1) actor.Speed = 1;
            _encounter.RefreshSpeed(actor);

            return wanted;
        }

        // Hands back exactly what was given and forgets the buff.
        private void RevokeSpeedBuff(CombatantState actor, RelicEffect source)
        {
            if (actor == null || !_speedBuffs.TryGetValue(actor, out var forActor)) return;
            if (!forActor.TryGetValue(source, out var buff)) return;

            actor.Speed -= buff.Granted;
            if (actor.Speed < 1) actor.Speed = 1;

            forActor.Remove(source);
            _encounter.RefreshSpeed(actor);
        }

        // Ticked with every other duration, at the actor's turn start.
        private void TickSpeedBuffs(CombatantState actor)
        {
            if (actor == null || !_speedBuffs.TryGetValue(actor, out var forActor)) return;

            var sources = new List<RelicEffect>(forActor.Keys);

            foreach (var source in sources)
            {
                var buff = forActor[source];

                // -1 is "for the rest of the fight" and never counts down.
                if (buff.TurnsLeft < 0) continue;

                buff.TurnsLeft--;
                if (buff.TurnsLeft <= 0) RevokeSpeedBuff(actor, source);
            }
        }

        // What a relic has given this combatant so far, for tests and for the
        // cap. Zero for everyone carrying nothing.
        public int SpeedBonusFrom(CombatantState actor, RelicEffect source) =>
            actor != null
            && _speedBuffs.TryGetValue(actor, out var forActor)
            && forActor.TryGetValue(source, out var buff)
                ? buff.Granted
                : 0;

        // EVERY speed buff/malus currently on a combatant, for a display that
        // cannot afford to hard-code which RelicEffect to ask about the way
        // TagLineFor does. Empty rather than null for a combatant carrying
        // nothing, so a caller never needs its own null check on top of the
        // empty-collection one.
        public IEnumerable<(RelicEffect Source, int Granted, int TurnsLeft)> ActiveSpeedBuffs(CombatantState actor)
        {
            if (actor == null || !_speedBuffs.TryGetValue(actor, out var forActor))
            {
                yield break;
            }

            foreach (var pair in forActor)
            {
                yield return (pair.Key, pair.Value.Granted, pair.Value.TurnsLeft);
            }
        }

        // ---- seams for tests -------------------------------------------------
        //
        // A one-turn speed buff's ENTIRE observable lifecycle sits inside one
        // synchronous public call. CastSkill grants it, and by the time that
        // call returns, AdvanceAfterAction has already run GrantTurnStart for
        // whoever is up next -- which, for a buff granted with turns: 1, is the
        // exact tick that revokes it. Control only ever returns to a caller once
        // it is the player's OWN next turn, so there is no externally observable
        // moment "just after casting, before the tick" through the public
        // round-trip API -- not for any speed ratio, not for any encounter
        // shape with one enemy. Confirmed by instrumenting the resolve loop
        // rather than assumed.
        //
        // These call the same two private methods everything else in this file
        // uses, so what they exercise IS the production arithmetic -- the seam
        // only removes the round trip that makes the result unobservable. Same
        // pattern FightController uses for PlaySpellVfxForTest and
        // SlotForTest, for the same reason: some things are only checkable with
        // the turn-resolution loop held open.
        public bool GrantSpeedPercentForTest(CombatantState actor, RelicEffect source, int percent,
                                             int turns, int capPercent = 0) =>
            GrantSpeedPercent(actor, source, percent, turns, capPercent) > 0;

        public void TickSpeedBuffsForTest(CombatantState actor) => TickSpeedBuffs(actor);
    }
}
