using System.Collections.Generic;

namespace PrincesPalace.Domain.Rewards
{
    // What one character earned from a fight, captured BEFORE and AFTER.
    //
    // Both halves, because the rewards screen draws an experience bar and a
    // bar needs somewhere to start. Experience is applied the instant a fight
    // resolves (that is what makes it safe against quitting mid-screen), so
    // by the time anything is drawn the before-state is already gone unless
    // it was written down here.
    //
    // The same reason FightController snapshots vitals for beat playback:
    // display always lags simulation, and the fix is to carry the past
    // forward rather than to slow the simulation down.
    public readonly struct CharacterReward
    {
        public readonly string CharacterId;
        public readonly string DisplayName;

        // Which PARTY SLOT this row is, 0-based.
        //
        // The Reckoning used to iterate a Dictionary's Values. Insertion
        // order happens to hold for a small never-removed-from dictionary,
        // but it is not a contract, and nothing guaranteed that party slot 1
        // was Reckoning row 1 — so the two screens a player reads back to
        // back could disagree about who is who. Slot order is the contract
        // now, and it is carried here rather than inferred from list
        // position so a caller that filters or reorders cannot silently
        // break it.
        public readonly int SlotIndex;

        // True for a squad member who was not fielded — at 0 HP, so
        // BuildEncounter skipped them.
        //
        // They get a row anyway, greyed and with nothing gained. Skipping
        // them made the party silently change size on the results screen,
        // which is exactly the confusion this pass exists to remove: three
        // characters went in, two came back, and nothing said why.
        public readonly bool IsDowned;

        public readonly int LevelBefore;
        public readonly int ExpBefore;
        public readonly int LevelAfter;
        public readonly int ExpAfter;

        // What the bar fills toward at the END level, which is not the same
        // as at the start level once a level-up happened mid-fight.
        public readonly int ExpToNextAfter;

        // What the bar filled toward at the START level.
        //
        // Needed only by the level-up sweep, and it cannot be derived from the
        // fields above: after a level-up ExpToNextAfter describes the NEW
        // level's larger requirement, so measuring ExpBefore against it would
        // put the sweep's starting point somewhere the bar never was.
        public readonly int ExpToNextBefore;

        public readonly int ExpGained;

        public bool LevelledUp => LevelAfter > LevelBefore;

        public int LevelsGained => LevelAfter > LevelBefore ? LevelAfter - LevelBefore : 0;

        // Where the bar STOOD when the fight began, on the start level's own
        // scale. BarFillBefore01 deliberately answers 0 after a level-up
        // (the level reset the bar, so every point now showing was earned
        // here) -- which is right for the resting display and useless as the
        // first frame of a sweep that has to start where the player left it.
        public float SweepStart01()
        {
            if (!LevelledUp) return BarFillBefore01();
            return Clamp01(ExpToNextBefore <= 0 ? 0f : ExpBefore / (float)ExpToNextBefore);
        }

        public CharacterReward(string characterId, string displayName,
            int levelBefore, int expBefore, int levelAfter, int expAfter, int expToNextAfter, int expGained,
            int slotIndex = 0, bool isDowned = false, int expToNextBefore = 0)
        {
            ExpToNextBefore = expToNextBefore;
            CharacterId = characterId;
            DisplayName = displayName;
            SlotIndex = slotIndex;
            IsDowned = isDowned;
            LevelBefore = levelBefore;
            ExpBefore = expBefore;
            LevelAfter = levelAfter;
            ExpAfter = expAfter;
            ExpToNextAfter = expToNextAfter;
            ExpGained = expGained;
        }

        // How full the bar sits after the fight, 0..1. Guards a
        // non-positive requirement rather than dividing by it — a broken
        // level curve should not take the screen down with it.
        public float BarFill01()
        {
            return Clamp01(ExpToNextAfter <= 0 ? 1f : ExpAfter / (float)ExpToNextAfter);
        }

        // Where the bar stood BEFORE this fight, on the same scale as
        // BarFill01. The screen draws up to here in the resting colour and
        // the remainder in a bright one, so the player can see at a glance
        // what this particular fight was worth rather than only where they
        // ended up.
        //
        // Zero after a level-up, and that is not a fudge: the level reset the
        // bar, so every point now showing genuinely was earned in this fight.
        // Carrying the old level's progress across would draw a segment that
        // does not exist on the new level's scale.
        public float BarFillBefore01()
        {
            if (LevelledUp)
            {
                return 0f;
            }

            return Clamp01(ExpToNextAfter <= 0 ? 1f : ExpBefore / (float)ExpToNextAfter);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    // Everything one fight paid out. Built by FightController at the moment
    // of victory and read by the rewards screen.
    public sealed class CombatReward
    {
        public readonly List<CharacterReward> Characters = new List<CharacterReward>();

        public int GoldGained;
        public int TotalGold;

        // The item the player picked from the three offered, or empty if the
        // fight offered none (or the screen was skipped).
        public string ChosenItemId = "";
        public string ChosenItemName = "";

        public bool HasItem => !string.IsNullOrEmpty(ChosenItemId);

        // What everyone actually DID in the fight that just ended.
        //
        // THIS fight's counters, not the run's: the Reckoning is a post-fight
        // screen and "what did I just do" is the question it is answering. The
        // run's running totals belong to the defeat screen, which is the one
        // describing a whole descent.
        //
        // Never null, so the tally tab needs no guard.
        public Combat.CombatLedger Ledger = new Combat.CombatLedger();
    }
}
