using System;
using System.Globalization;
using System.Text;

namespace PrincesPalace
{
    // WHERE A BATCH'S WALL CLOCK ACTUALLY GOES.
    //
    // The first attempt at making the bot faster guessed at the hot phase and
    // guessed wrong: the obvious suspect (playing the fight) was a minority of
    // the time, and the two real costs were the throwaway save DIRECTORY per
    // run and JsonUtility serialising the whole save on every arrival. Neither
    // is visible without measuring, so measuring is not an optional extra here
    // -- it is the only thing that keeps "an optimisation" from being a
    // superstition.
    //
    // ALWAYS ON rather than behind a flag. One Stopwatch.GetTimestamp pair per
    // phase entry is a handful of nanoseconds against phases measured in tens
    // of microseconds, so the flag would only buy the risk of the numbers
    // being unavailable exactly when somebody wants them. Measured cost of the
    // instrumentation itself: below the run-to-run noise of a batch (see the
    // header of tools/bot.ps1).
    //
    // Static, and reset per batch by the caller. This is a measuring
    // instrument, not state the game reads -- nothing here changes what a run
    // does, and a run's RunTrace.Hash() is identical with it on or off.
    public enum BotPhase
    {
        // Per run, outside the game: making the throwaway save root, resetting
        // the statics, tearing it back down.
        HarnessSetup = 0,
        HarnessTeardown = 1,

        // ProfilePresets.Build -- CreateNew, levelling, stat spend.
        PresetBuild = 2,

        // The relic draft, whole.
        RelicDraft = 3,

        // The three halves of a fight room.
        BuildFight = 4,
        FightPlay = 5,
        SettleFight = 6,

        // RollOffers + ChooseOffer + TakeOffer, and NESTED INSIDE this one is
        // Equip: TakeOffer's own equip pass happens mid-Offer, before the
        // "did it land in the bag or on the body" check that closes the
        // phase out, so splitting Equip's time back out would mean the same
        // "minus a subtotal" threading PersistSerialize/PersistWrite already
        // avoid below. Read Equip's row as "of the time in Offers, this much
        // was equipping", not as extra time on top of it.
        Offers = 7,

        // ArriveAt for a non-fight room (RoomResolver.Resolve).
        RoomResolve = 8,

        // NESTED INSIDE the phases above, and deliberately so: a save write is
        // triggered from inside TakeOffer, SettleFight and the draft, and
        // pulling it out of their totals would mean threading a "minus
        // persistence" subtotal through every one of them. Read these two as
        // "of the time above, this much was the save", not as extra time.
        PersistSerialize = 9,
        PersistWrite = 10,

        // The batch's own bookkeeping.
        TraceJson = 11,
        Summary = 12,

        // The determinism replay: a second whole play of the same seed.
        // Everything it costs is ALSO counted in the phases above (it goes
        // through the same code), so this is the one line that says what
        // sampling the replay would buy.
        Replay = 13,

        // THE TWO THINGS A PLAYER DOES BETWEEN FIGHTS, and the two the bot did
        // none of before: wearing the best of what it owns, and collecting the
        // level-ups it has earned. Given their own rows because both run a
        // clone-and-re-resolve per candidate (see GearEvaluator) and that is
        // the one new cost in this phase worth being able to see grow.
        //
        // NESTED INSIDE Offers when it fires from a taken reward (see the
        // note on Offers above) -- but ALSO measured on its own outside any
        // phase at all, for the pre-run equip pass before the first room, so
        // Equip's row is not simply "Offers minus this" the way
        // PersistSerialize/PersistWrite are for their hosts.
        Equip = 14,
        LevelUp = 15,

        // A WHOLE SHOP VISIT (up to MaxShopChoices rounds of ShopViewOf +
        // ChooseShop + Apply) and the spell-book placement that follows a
        // shop or a fight. Added after these two ran with no timer around
        // them at all and the batch that found the 2026-09 shop-visit
        // regression (VisitShop rescoring every shelf card and every bag row
        // from scratch on every one of a visit's choices) had to be read as
        // "half of WALL is unaccounted for" before the culprit could even be
        // named.
        Shop = 16,
        SpellAssign = 17,
    }

    public static class BotPhaseTimers
    {
        // DERIVED, not hand-counted. A hand-counted 18 agreed with BotPhase
        // only until the next member was added to it without also touching
        // this line -- Add() below indexes straight into Elapsed/Calls by
        // (int)phase, so a phase at or past a stale count throws
        // IndexOutOfRangeException the first time anything measures it, and
        // Report (further down) silently omits any phase past the count
        // instead of throwing at all. Walking the enum itself is what makes
        // "add a BotPhase member" the only edit a new phase needs.
        private static readonly int PhaseCount = Enum.GetValues(typeof(BotPhase)).Length;

        // Test-only door to PhaseCount, named ...ForTest per house convention
        // (FightBeatPlayer.WireStageForTest, FightController.
        // StageShakesForTest) since Core's InternalsVisibleTo names only the
        // Editor assembly and a PlayMode test sits outside that grant.
        public static int PhaseCountForTest => PhaseCount;

        private static readonly long[] Elapsed = new long[PhaseCount];
        private static readonly long[] Calls = new long[PhaseCount];

        private static long _batchStart;

        public static void ResetBatch()
        {
            Array.Clear(Elapsed, 0, PhaseCount);
            Array.Clear(Calls, 0, PhaseCount);
            _batchStart = System.Diagnostics.Stopwatch.GetTimestamp();
        }

        public static void Add(BotPhase phase, long ticks)
        {
            int i = (int)phase;
            Elapsed[i] += ticks;
            Calls[i]++;
        }

        // A struct, used through `using`, so there is no allocation per phase
        // entry -- a class here would be one garbage object per fight per run,
        // which for a 10k-run batch is millions of them and would itself show
        // up in the numbers it is supposed to be reporting.
        public struct Scope : IDisposable
        {
            private readonly BotPhase _phase;
            private readonly long _start;

            internal Scope(BotPhase phase)
            {
                _phase = phase;
                _start = System.Diagnostics.Stopwatch.GetTimestamp();
            }

            public void Dispose() => Add(_phase, System.Diagnostics.Stopwatch.GetTimestamp() - _start);
        }

        public static Scope Measure(BotPhase phase) => new Scope(phase);

        public static double MillisecondsOf(BotPhase phase) =>
            Elapsed[(int)phase] * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        public static long CallsOf(BotPhase phase) => Calls[(int)phase];

        // The table the batch prints. Deliberately plain text with a fixed
        // column layout: it is read off a terminal or out of a log tail, and
        // the one thing it has to make obvious is which row is big.
        public static string Report(int runPlays)
        {
            double wall = (System.Diagnostics.Stopwatch.GetTimestamp() - _batchStart)
                          * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== PHASE TIMES ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-18}{1,12}{2,12}{3,12}{4,9}", "phase", "total ms", "calls", "us/call", "% wall"));

            for (int i = 0; i < PhaseCount; i++)
            {
                var phase = (BotPhase)i;
                double ms = MillisecondsOf(phase);
                long calls = Calls[i];
                double perCall = calls == 0 ? 0 : ms * 1000.0 / calls;
                double share = wall <= 0 ? 0 : ms / wall;

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-18}{1,12:F1}{2,12}{3,12:F1}{4,9:P1}", phase, ms, calls, perCall, share));
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-18}{1,12:F1}", "WALL", wall));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "run-plays {0}, {1:F1} ms per run-play, {2:F0} run-plays/minute",
                runPlays,
                runPlays <= 0 ? 0 : wall / runPlays,
                wall <= 0 ? 0 : runPlays * 60000.0 / wall));

            // PersistSerialize/PersistWrite are inside the rows above, so the
            // column would otherwise sum past 100% with no explanation.
            sb.AppendLine("(Persist* and Replay are nested inside the rows above, not additional to them.)");
            return sb.ToString();
        }
    }
}
