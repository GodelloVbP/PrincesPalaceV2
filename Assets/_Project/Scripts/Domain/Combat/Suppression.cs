using System;

namespace PrincesPalace.Domain.Combat
{
    // WHAT A COMBATANT HAS BEEN STRIPPED OF, as two windows the engine reads
    // at one seam each (docs/PLAN_BJORN_CONSTELLATIONS.md Phase 4, row 4f):
    //
    //   Silence -- cannot cast. Read by CombatActions.IsLegalFor, the one
    //              predicate the menu, the enemy draw, the committed intent
    //              and execution all ask, as the mirror of Rooted: a rooted
    //              actor may only cast, a silenced one may only strike.
    //   Disarm  -- -Percent attack. Read by FightSession.AttackBonusFor
    //              beside every other attack bonus.
    //
    // WHY NOT STATUSES. StatusEffectType is content-hashed (ContentInputHash),
    // and a status also has to answer DurationClock, the HUD slug table and the
    // glossary. These follow the CrowdControlGuard precedent instead: state on
    // the combatant, a TurnWindow for the span, a switch the engine reads.
    // When the Unity session wants a badge, it reads IsSilenced / IsDisarmed
    // the way the HUD reads any other per-combatant field.
    //
    // NOT CROWD CONTROL. CrowdControl.IsCrowdControl is the fixed list Stun,
    // Feared, Rooted, Chilled, so Unstoppable and Unyielding do not see these:
    // the plan's Unbroken text says "crowd control" and lists no denial of
    // casting or attack. Chilled (Thornwall's slow) is on that list and goes
    // through ApplyStatusTo like every other status.
    //
    // Everyone carries one, empty, and for them nothing changes.
    public sealed class Suppression
    {
        // Silence lasts 1 turn (Spellbreaker T2). The same enemy cannot be
        // silenced again until SilenceCooldown, counted from the moment the
        // silence landed, has run out.
        public readonly TurnWindow Silence = new TurnWindow();
        public readonly TurnWindow SilenceCooldown = new TurnWindow();

        public readonly TurnWindow Disarm = new TurnWindow();

        // Percent of attack removed while Disarm is open (30, Thornwall T3).
        public int DisarmPercent { get; private set; }

        public bool IsSilenced => Silence.IsOpen;
        public bool IsDisarmed => Disarm.IsOpen && DisarmPercent > 0;

        // The attack bonus this contributes, as a signed percent; 0 when off.
        public int AttackPercentDelta => IsDisarmed ? -DisarmPercent : 0;

        // False when the cooldown refuses. The cooldown starts when the
        // silence lands and runs on the silenced enemy's own turns, so a
        // 3-turn cooldown covers the silenced turn and the two after it.
        public bool TrySilence(int turns, int cooldownTurns, bool onHoldersTurn)
        {
            if (turns <= 0 || SilenceCooldown.IsOpen) return false;

            Silence.Open(turns, onHoldersTurn);
            SilenceCooldown.Open(Math.Max(turns, cooldownTurns), onHoldersTurn);
            return true;
        }

        // Re-disarming refreshes the span and never weakens the stronger
        // figure, the rule TurnWindow.Open gives a span.
        public void ApplyDisarm(int percent, int turns, bool onHoldersTurn)
        {
            if (percent <= 0 || turns <= 0) return;

            if (!Disarm.IsOpen) DisarmPercent = 0;
            DisarmPercent = Math.Max(DisarmPercent, percent);
            Disarm.Open(turns, onHoldersTurn);
        }

        // One of the holder's turns ended. Reports which windows just closed
        // so the session can say so once.
        public (bool silenceEnded, bool disarmEnded) AgeAtHoldersTurnEnd()
        {
            bool silenceEnded = Silence.AgeAtHoldersTurnEnd();
            SilenceCooldown.AgeAtHoldersTurnEnd();

            bool disarmEnded = Disarm.AgeAtHoldersTurnEnd();
            if (disarmEnded) DisarmPercent = 0;

            return (silenceEnded, disarmEnded);
        }
    }
}
