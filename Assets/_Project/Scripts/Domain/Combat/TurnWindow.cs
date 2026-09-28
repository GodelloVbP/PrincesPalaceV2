using System;

namespace PrincesPalace.Domain.Combat
{
    // A SPAN OF THE HOLDER'S OWN TURNS during which something is true --
    // Cursed Blood's heal conversion, Unstoppable, Unyielding's surge and its
    // cooldown (docs/PLAN_BJORN_CONSTELLATIONS.md Phase 4, 4b/4e).
    //
    // WHY NOT A STATUS. Each of these is a switch the engine reads at one
    // seam, not a badge with a magnitude, and a new StatusEffectType is a
    // content-hashed enum member (ContentInputHash) that also has to answer
    // DurationClock, the HUD slug table and the glossary. A window carries
    // none of that. When the Unity session wants a badge for one of these, it
    // can read IsOpen/TurnsRemaining off the combatant the way the HUD reads
    // any other per-combatant field.
    //
    // THE CLOCK IS THE AtTurnEnd FAMILY'S (StatusEffects.DurationClock): aged
    // at the END of the holder's own turn, and the turn it was opened on is
    // not counted when it was opened during that same turn. N = N of the
    // holder's turns fully covered, whichever side's turn opened it -- the
    // property a "for 2 turns" tooltip promises and the turn-START clock
    // (speed buffs) does not keep for a window opened on an enemy's turn.
    // FightSession ages every window in TickStatusesAtTurnEnd, beside the
    // statuses on that clock.
    //
    // RE-OPENING NEVER SHORTENS: Open takes the larger of what is left and
    // what is asked, the "refreshed, not stacked" rule StatusEffects.Apply
    // follows for a repeated status -- paying twice is not worse than once.
    public sealed class TurnWindow
    {
        public int TurnsRemaining { get; private set; }

        // Set when the window was opened (or refreshed) during the holder's
        // own turn: that turn's end is skipped once, like
        // FightSession._sparedAtWearersTurnEnd does for a status.
        private bool _spareNextTurnEnd;

        public bool IsOpen => TurnsRemaining > 0;

        public void Open(int turns, bool openedOnHoldersTurn)
        {
            if (turns <= 0) return;

            TurnsRemaining = Math.Max(TurnsRemaining, turns);
            _spareNextTurnEnd = openedOnHoldersTurn;
        }

        public void Close()
        {
            TurnsRemaining = 0;
            _spareNextTurnEnd = false;
        }

        // One of the holder's turns ended. True exactly when THIS call closed
        // the window, so the caller can say "wears off" once and tear down
        // whatever the window was holding up (Unyielding's speed grant).
        public bool AgeAtHoldersTurnEnd()
        {
            if (!IsOpen) return false;

            if (_spareNextTurnEnd)
            {
                _spareNextTurnEnd = false;
                return false;
            }

            TurnsRemaining--;
            return TurnsRemaining == 0;
        }
    }
}
