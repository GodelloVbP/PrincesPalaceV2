using System.Collections.Generic;
using System.Linq;
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
    //
    // KEYED BY `object`, NOT JUST RelicEffect, since Phase D2 (item-modifier
    // plan). Every source here used to be a relic; Chilled is the first
    // that is not one — it is a STATUS (StatusEffectType.Chilled), and its
    // malus has to compose against the exact same TrueBaseSpeed every relic
    // grant already measures against, or it reopens the precise bug
    // TrueBaseSpeed's own comment documents (a second source reading a base
    // the first source already inflated). A second, PARALLEL dictionary
    // just for status-driven speed changes was considered and rejected for
    // that reason: two independent "true base" computations over the same
    // combatant's Speed is exactly the shape that bug came from, just
    // reintroduced one level up. Widening the key instead costs one type
    // change here (and a boxed-enum-equality fix at the one display call
    // site that compared sources with `==` — see FightHudModel.
    // BuffBadgesFor) and touches no other call site's source code: every
    // existing caller passes a RelicEffect literal, which boxes to `object`
    // implicitly, so GrantSpeedPercent/GrantSpeedMalusPercent's own
    // arithmetic and every relic that already uses them are unchanged.
    // Chilled needs no per-source sub-keying of its own even so — see
    // RefreshChilledSpeed below on why StatusEffectType.Chilled alone is
    // key enough.
    public partial class FightSession
    {
        private sealed class SpeedBuff
        {
            // The flat points handed over, so they can be handed back exactly.
            public int Granted;

            // Turns left, or -1 for "until this fight ends". The Slippers are
            // permanent within a fight; the Pipe lasts until the next turn.
            // Chilled's own entry is always -1 here — see RefreshChilledSpeed
            // for why its lifetime is owned by the status list instead.
            public int TurnsLeft;
        }

        private readonly Dictionary<CombatantState, Dictionary<object, SpeedBuff>> _speedBuffs =
            new Dictionary<CombatantState, Dictionary<object, SpeedBuff>>();

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
        private int TrueBaseSpeed(CombatantState actor, Dictionary<object, SpeedBuff> forActor)
        {
            int granted = 0;
            foreach (var buff in forActor.Values) granted += buff.Granted;
            return actor.Speed - granted;
        }

        // Percent of the combatant's speed AT THE MOMENT OF GRANTING, converted
        // to flat points immediately. Returns what was actually given, which is
        // 0 when the cap is already reached -- the caller uses that to decide
        // whether there is anything worth saying.
        private int GrantSpeedPercent(CombatantState actor, object source, int percent,
                                      int turns, int capPercent = 0)
        {
            if (actor == null || percent <= 0 || actor.Speed <= 0) return 0;

            // REFRESHED, NOT STACKED, when it is a timed buff. A second cast
            // before the first expires resets the clock rather than doubling
            // the speed -- the same rule StatusEffects.Apply follows, and for
            // the same reason: paying the cost twice should not be strictly
            // better than paying it once and waiting.
            //
            // HANDING THE PREVIOUS GRANT BACK FIRST is what makes that true,
            // and for eleven months it did not: only TurnsLeft was reset while
            // Granted and Speed both went on compounding, so two Tin-Foil Pipe
            // casts in one turn gave 20 base + 20% + 20% = 28 rather than the
            // 24 the relic's own card promises. Reachable, not theoretical --
            // Fleece Ward T3 grants a free action, so a second cast lands
            // before the turn start that would have ticked the first grant
            // away. Same revoke-then-regrant shape RefreshNecklaceSpeed and
            // RefreshChilledSpeed already use, and for the same reason: what
            // a refresh owes is "the magnitude one grant would give against
            // the current true base", which is exactly what recomputing from
            // scratch produces and what adjusting a delta only approximates.
            //
            // turns > 0 ONLY. A turns: 0 grant is not a timed buff and the
            // relics that use it mean what they do: Ballerina's Slippers
            // accumulate across a whole fight to their own cap, and the
            // Toothed Necklace already revokes explicitly before re-granting
            // because its ramp moves in both directions. Neither wants this.
            if (turns > 0) RevokeSpeedBuff(actor, source);

            if (!_speedBuffs.TryGetValue(actor, out var forActor))
            {
                forActor = new Dictionary<object, SpeedBuff>();
                _speedBuffs[actor] = forActor;
            }

            if (!forActor.TryGetValue(source, out var buff))
            {
                buff = new SpeedBuff();
                forActor[source] = buff;
            }

            // The TRUE base -- see TrueBaseSpeed. Every OTHER relic's grant is
            // subtracted too, not only this one's, or a second speed relic
            // reads a base that the first one already inflated. Read AFTER the
            // revoke above, so a refresh measures against a base this source is
            // no longer part of.
            int baseSpeed = TrueBaseSpeed(actor, forActor);
            int wanted = baseSpeed * percent / 100;
            if (wanted <= 0) wanted = 1;

            if (capPercent > 0)
            {
                int ceiling = baseSpeed * capPercent / 100;
                if (buff.Granted + wanted > ceiling) wanted = ceiling - buff.Granted;
            }

            // The clock. The refresh half of "refreshed, not stacked" is done
            // at the top of this method, where the previous grant is handed
            // back; by here `buff` is either brand new or an accumulating
            // turns: 0 entry.
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
        private int GrantSpeedMalusPercent(CombatantState actor, object source, int percent, int turns)
        {
            if (actor == null || percent <= 0 || actor.Speed <= 0) return 0;

            // HANDING THE PREVIOUS GRANT BACK FIRST, exactly as the buff twin
            // does and for the same reason. This line was missing here: the
            // "REFRESHED, NOT STACKED" comment below reset TurnsLeft while
            // Granted and Speed both went on compounding, which is the same
            // defect the buff side carried for eleven months and fixed with
            // this one line. `turns > 0` only, so a turns: 0 malus keeps
            // accumulating the way its two callers intend.
            if (turns > 0) RevokeSpeedBuff(actor, source);

            if (!_speedBuffs.TryGetValue(actor, out var forActor))
            {
                forActor = new Dictionary<object, SpeedBuff>();
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
            // The revoke at the top of this method is what makes that true of
            // the MAGNITUDE as well as the clock; this line alone only ever
            // moved the clock.
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
        private void RevokeSpeedBuff(CombatantState actor, object source)
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

            var sources = new List<object>(forActor.Keys);

            foreach (var source in sources)
            {
                var buff = forActor[source];

                // -1 is "for the rest of the fight" and never counts down.
                if (buff.TurnsLeft < 0) continue;

                buff.TurnsLeft--;
                if (buff.TurnsLeft <= 0) RevokeSpeedBuff(actor, source);
            }
        }

        // What a relic (or, since Phase D2, Chilled) has given this combatant
        // so far, for tests and for the cap. Zero for everyone carrying
        // nothing. `object` so a RelicEffect literal keeps compiling
        // unchanged at every existing call site (an implicit boxing
        // conversion, not a source change) while StatusEffectType.Chilled
        // can key the same dictionary -- see this file's own header.
        public int SpeedBonusFrom(CombatantState actor, object source) =>
            actor != null
            && _speedBuffs.TryGetValue(actor, out var forActor)
            && forActor.TryGetValue(source, out var buff)
                ? buff.Granted
                : 0;

        // EVERY speed buff/malus currently on a combatant, for a display that
        // cannot afford to hard-code which source to ask about the way
        // TagLineFor does. Empty rather than null for a combatant carrying
        // nothing, so a caller never needs its own null check on top of the
        // empty-collection one.
        //
        // Source is `object`, not RelicEffect, as of Phase D2 -- see this
        // file's own header. FightHudModel.BuffBadgesFor is the one caller
        // that used to compare Source with `==` against a RelicEffect; that
        // comparison had to move to Equals(...) because `==` between a
        // boxed value type and `object` is REFERENCE equality, not the
        // value equality two separately-boxed equal enums need -- a real
        // footgun this widening could otherwise have introduced silently.
        public IEnumerable<(object Source, int Granted, int TurnsLeft)> ActiveSpeedBuffs(CombatantState actor)
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

        // ---- Chilled (Phase D2, item-modifier plan) ---------------------------
        //
        // THE READ HOOK. Chilled's percent speed malus is granted/revoked
        // through the exact same GrantSpeedMalusPercent/RevokeSpeedBuff
        // arithmetic Lucky Deck's bespoke slow already used -- proven
        // correct (refresh-not-stack, exact reversal against the TRUE base)
        // before this pass ever touched it. The alternative the plan itself
        // named -- a status-aware read inside SpeedScale.TickRate -- was
        // rejected: that function's signature is read by TurnOrder.SetSpeed
        // and every caller of it, and its header says it was "tuned with
        // real care"; changing what it takes would ripple to all of them
        // for one new status. Reusing this file's bookkeeping needed no
        // signature change anywhere near SpeedScale or TurnOrder at all --
        // Speed stays a plain int the whole way through.
        //
        // NO PER-SOURCE KEYING NEEDED for Chilled specifically, unlike every
        // relic above: StatusEffects.Apply already guarantees at most ONE
        // Chilled entry per combatant (refresh-not-stack, the same rule
        // every status in this game follows), so StatusEffectType.Chilled
        // itself is a sufficient dictionary key -- there is never a second
        // "instance" of Chilled to distinguish it from.
        //
        // GRANTED WITH turns: 0 (-> TurnsLeft -1, "until revoked"), NOT the
        // status's own duration. Duration is already owned by
        // CombatantState.Statuses -- ActiveStatus.TurnsRemaining, ticked by
        // StatusEffects.Tick -- and letting THIS dictionary also count down
        // the same duration would be the identical double-bookkeeping
        // TrueBaseSpeed's own header warns against, one layer up: two
        // clocks for one status can drift. FightSession.Riders.TickStatuses
        // revokes the malus explicitly, the turn StatusEffects.Tick reports
        // Chilled as expired -- see that method's own comment.
        private int RefreshChilledSpeed(CombatantState target)
        {
            if (target == null) return 0;

            // Revoke-then-regrant rather than adjust-in-place, the same
            // shape RefreshNecklaceSpeed already uses and for the same
            // reason: Magnitude can only move on a refresh (Math.Max, never
            // down), so recomputing from scratch against the current true
            // base is simpler than reasoning about a delta.
            RevokeSpeedBuff(target, StatusEffectType.Chilled);

            var chilled = target.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Chilled);
            if (chilled == null) return 0;

            return GrantSpeedMalusPercent(target, StatusEffectType.Chilled, chilled.Magnitude, turns: 0);
        }

        // Applies (or refreshes) Chilled AND pushes its Speed effect through
        // in the same call -- StatusEffects.Apply on its own is pure Domain
        // and cannot touch Speed, so every caller that wants Chilled to
        // actually slow anyone goes through here rather than calling
        // StatusEffects.Apply directly and risking a forgotten follow-up.
        // Returns the (non-positive) amount of Speed actually taken, the
        // same convention GrantSpeedMalusPercent itself returns, so a
        // caller can gate a message on "did this actually do anything" the
        // same way LuckyDeckSlow always could.
        private int ApplyChilled(CombatantState target, int magnitude, int turns, CombatantState source)
        {
            if (target == null || magnitude <= 0) return 0;

            StatusEffects.Apply(target.Statuses, StatusEffectType.Chilled, magnitude, turns, source);
            return RefreshChilledSpeed(target);
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
        public bool GrantSpeedPercentForTest(CombatantState actor, object source, int percent,
                                             int turns, int capPercent = 0) =>
            GrantSpeedPercent(actor, source, percent, turns, capPercent) > 0;

        public void TickSpeedBuffsForTest(CombatantState actor) => TickSpeedBuffs(actor);

        // The malus twin's seam. It has no production caller passing turns > 0
        // today -- both of them pass 0 and revoke for themselves -- so this is
        // the only way to reach the timed-refresh rule the method states, and
        // a rule nothing can reach is a rule nothing can check.
        public int GrantSpeedMalusPercentForTest(CombatantState actor, object source, int percent, int turns) =>
            GrantSpeedMalusPercent(actor, source, percent, turns);

        // Chilled's own seam, for the same reason the buff seams above
        // exist: a one-status application's Speed effect is fully
        // observable synchronously (ApplyChilled already pushes it through
        // before returning), but tests still want the two production
        // methods directly rather than routing through a specific relic or
        // modifier proc that happens to call them.
        public int ApplyChilledForTest(CombatantState target, int magnitude, int turns, CombatantState source = null) =>
            ApplyChilled(target, magnitude, turns, source);
    }
}
