using System.Collections.Generic;

namespace PrincesPalace.Domain.Progression
{
    // What a character level costs, and what a gain of experience does to a
    // level/exp pair.
    //
    // IN DOMAIN, NOT ON Character, for the reason TalentSkeleton's header
    // gives for the same move: the EditMode assembly references Domain and
    // nothing else, so arithmetic that lives in Core can only be pinned from
    // PlayMode or duplicated into the test. `Character.ExpToNextLevel` and
    // `Character.AddExperience` still exist and are still what every caller
    // asks -- they delegate here, so the seam callers depend on has not
    // moved.
    //
    // AN AUTHORED TABLE, NOT A FORMULA, since progression v2 phase 2. The
    // geometric curve that used to live here (BaseCost 100, 90 permille a
    // level, cap 200) was fitted against an income that compounded at the
    // health rate, and the whole point of that fit was to outrun it. Two
    // things then changed at once: experience got its own, much flatter rate
    // (DifficultyCurve.ScaleExperience, 25 permille) and the cap came down
    // from 100 to 40. The model refitted the formula three ways against the
    // new income and every fit priced level 40 at four to five deep runs
    // while clearing a third of the track inside the first three short runs
    // -- because one exponent cannot describe a ladder whose early rungs are
    // meant to be minutes apart and whose late ones are meant to be runs
    // apart. See docs/handoffs/progression_v2/xp_model.md Part C for the
    // three fits and PLAN_PROGRESSION_V2.md §3 for the table that replaced
    // them.
    //
    // THE TABLE IS PASSED IN, NOT HELD. Domain cannot reach ContentDatabase,
    // and a static table installed once at load would be exactly the global
    // mutable state GlobalStateLintTests exists to keep out. Every method
    // here takes `costs`, the flat list the content resolver produces, and
    // `Character` -- the one Core-side seam -- is what fetches it.
    //
    // WHAT A SAVE WRITTEN BEFORE THIS SEES: nothing, because it is reset.
    // SaveData's version-6 migration puts every roster character back to
    // level 1 with 0 experience; the per-level costs moved far too much
    // (level 10 was 236 and is now 500; level 40 was 30,199 and is now
    // 10,000) for a carried level to mean the same thing on either side.
    public static class LevelCurve
    {
        // The first level a player PAYS for. A character begins at level 1,
        // so the table starts at 2 -- and `costs[0]` is that row.
        public const int FirstPaidLevel = 2;

        // WHAT AN ABSENT TABLE COSTS. Reached only with no built content --
        // an Editor session before the first ContentBuilder run, a test
        // holding ContentDatabase empty -- and it has to be a number, because
        // every caller asks for one.
        //
        // int.MaxValue rather than 0 or a guessed default, and the choice is
        // between two failure shapes rather than between right and wrong. A 0
        // makes `while (exp >= cost)` always true and spins forever. A guessed
        // default makes a character level up on numbers nobody authored, which
        // is this project's one forbidden answer: plausible and wrong. This
        // makes them stick at level 1, which is visibly broken and harms
        // nothing on the way.
        public const int NoTableCost = int.MaxValue;

        // The last level an authored table reaches. Derived from the table's
        // own length rather than read off RewardTrack, so a table and the cap
        // it was authored for cannot disagree here -- LevelCurveEntryResolver
        // is where they are checked against each other, once, at build time.
        public static int MaxLevel(IReadOnlyList<int> costs) =>
            costs == null || costs.Count == 0 ? RewardTrack.StartingLevel : FirstPaidLevel + costs.Count - 1;

        // What it costs to get from `level` to `level + 1`.
        //
        // AT AND PAST THE CAP this returns the LAST authored cost rather than
        // a sentinel, and AddExperience refuses to cross it -- so a level-40
        // character's experience bar reads against 10,000 and fills, instead
        // of reading against int.MaxValue and looking permanently empty. The
        // cap is enforced in one place (AddExperience), not two.
        public static int ExpToNextLevel(IReadOnlyList<int> costs, int level)
        {
            if (costs == null || costs.Count == 0) return NoTableCost;

            int index = level - FirstPaidLevel + 1;
            if (index < 0) index = 0;
            if (index >= costs.Count) index = costs.Count - 1;

            return costs[index];
        }

        // What reaching `level` costs in total, from level 1.
        //
        // long, because the cumulative figure is what a track is actually
        // priced against ("how long to 40", not "what does 23 cost") and a
        // caller summing it in an int is one cap change away from wrapping.
        public static long CumulativeCost(IReadOnlyList<int> costs, int level)
        {
            if (costs == null) return 0;

            long total = 0;
            for (int l = FirstPaidLevel; l <= level; l++)
            {
                int index = l - FirstPaidLevel;
                if (index < 0 || index >= costs.Count) break;
                total += costs[index];
            }

            return total;
        }

        // What `amount` more experience does to a level/exp pair.
        //
        // THE WHOLE LEVEL-UP LOOP, moved down from Character so it can be
        // pinned without a save, a scene or Unity -- CODE_STANDARDS §1's
        // "arithmetic to Domain, wrapper stays". Character.AddExperience is
        // now that wrapper and does nothing else.
        //
        // STOPS AT THE CAP, which is new. Nothing used to bound `level` at
        // all: a character could level past RewardTrack.MaxLevel purely
        // through income, and the track simply returned TrackEntry.None
        // forever after (RewardTrackDefinition.At). That was graceful with a
        // cap of 100 nobody reached; with a cap of 40 that a career reaches
        // on run 24 it would have every later run advertise levels that pay
        // nothing. Excess experience is KEPT rather than discarded -- it
        // costs nothing to carry, and throwing away a player's last fight
        // because they happened to be at the cap is a worse answer than a
        // number that stops mattering.
        public static LevelUp AddExperience(IReadOnlyList<int> costs, int level, int exp, int amount)
        {
            if (amount <= 0) return new LevelUp(level, exp, 0);

            int cap = MaxLevel(costs);
            int newExp = exp + amount;
            int newLevel = level;
            int gained = 0;

            while (newLevel < cap)
            {
                int cost = ExpToNextLevel(costs, newLevel);
                if (cost <= 0 || newExp < cost) break;

                newExp -= cost;
                newLevel++;
                gained++;
            }

            return new LevelUp(newLevel, newExp, gained);
        }

        // ---- HOW MANY FIGHTS THE NEXT LEVEL IS AWAY ------------------------
        //
        // The one number the reward track's focus card says that the rail
        // itself cannot: "about 6 fights to go". A cost table answers "how
        // much experience"; a player counts in fights.
        //
        // AN AVERAGE NORMAL FIGHT, not the fight they are about to have, and
        // the difference is the whole honesty of the estimate. §2 of
        // docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md measures a
        // room-0 normal fight at 29 experience -- the floor-1 pool's mean raw
        // 19.5 times the 1.5 enemies EncounterRoll fields -- against an elite
        // at 61 and a boss at 100. Counting in elites would flatter the
        // number and counting in the cheapest possible draw (a lone rat, 15)
        // would double it. The word the card says is "ABOUT".
        //
        // DEPTH IS THE CALLER'S, and it has exactly two answers: in a run,
        // the run's own step, because that is what the next fight will pay;
        // in the hub, 0, because the next fight is the first room of the next
        // descent. Nothing here guesses it -- a Domain function that reached
        // for RunManager would be reaching across two layers to save a
        // caller one argument.
        public const int AverageNormalFightExperience = 29;

        // ROUNDED UP, and clamped at 1 whenever anything at all is owed: a
        // remainder of 3 against a fight worth 29 is "one more fight", not
        // "zero fights" -- and zero is the answer reserved for a level that
        // is already paid for.
        public static int FightsToGo(int remainingExperience, int depthStep)
        {
            if (remainingExperience <= 0) return 0;

            int pay = Dungeon.DifficultyCurve.ScaleExperience(AverageNormalFightExperience, depthStep);

            // A pay of zero cannot happen through ScaleExperience (the curve
            // multiplies a positive constant by at least 1) but would divide
            // by zero if it ever did, so it is answered rather than trusted.
            if (pay <= 0) return 0;

            int fights = remainingExperience / pay;
            if (remainingExperience % pay != 0) fights++;

            return fights < 1 ? 1 : fights;
        }

        // The same question asked of a level/exp pair rather than of a
        // remainder, which is the form every caller actually holds. AT THE
        // CAP it answers 0: there is no next level to count toward, and a
        // number there would be counting fights toward nothing.
        public static int FightsToNextLevel(IReadOnlyList<int> costs, int level, int exp, int depthStep)
        {
            if (level >= MaxLevel(costs)) return 0;

            int cost = ExpToNextLevel(costs, level);
            if (cost == NoTableCost) return 0;

            return FightsToGo(cost - exp, depthStep);
        }

        // Where a gain left a character. A struct rather than three out
        // parameters because the three only ever travel together, and
        // Character copies all three back in one place.
        public readonly struct LevelUp
        {
            public readonly int Level;
            public readonly int Exp;
            public readonly int LevelsGained;

            public LevelUp(int level, int exp, int levelsGained)
            {
                Level = level;
                Exp = exp;
                LevelsGained = levelsGained;
            }
        }
    }
}
