using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;

namespace PrincesPalace.Editor.Bot
{
    // ONE SHARD OF A BATCH: seeds x archetypes x profiles, played headless.
    //
    // This writes FACTS, not aggregates. Four files land in -botOut:
    // traces.jsonl (archival, one full RunTrace per line), runs.jsonl (one
    // flat row per run -- everything summary.json's metrics are computed
    // from), content.json (the id lists coverage is differenced against) and
    // batch.json (what this shard was asked to do). tools/bot_merge.py reads
    // all of a batch's shards and writes the one summary.json;
    // tools/bot_report.py renders and diffs that.
    //
    // summary.json used to be computed here, and docs/BOT_SUMMARY_SCHEMA.md
    // had a good reason: doomedShare and swingShare are fractions of the
    // party's MAX hp, which is not a trace field and must not become one. That
    // reason survives intact -- it is why runs.jsonl carries partyMaxHp per
    // fight and a pre-reduced swing count, both read live off the encounter.
    // What did not survive is computing the BATCH's numbers here, because a
    // batch is now N Unity processes and a median cannot be merged from N
    // medians.
    //
    // NOTHING HERE IS A GATE. A batch reports; the author decides (plan §6).
    // The exit code says whether the BATCH ran, not whether the game is
    // balanced -- a run that trips an invariant is a row in bugs[], not a
    // failure.
    public static class BalanceBotRunner
    {
        private static readonly UTF8Encoding NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private const int DefaultRuns = 200;
        private const ulong DefaultSeed = 1;
        private const int DefaultDepthCap = 40;
        private const double DefaultReplayShare = 0.1;

        [MenuItem("Tools/Balance Bot/Run a small batch (20 seeds, both archetypes, Fresh)")]
        public static void RunASmallBatchFromTheMenu()
        {
            string outDir = Path.Combine(Path.GetTempPath(), "pp-bot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var options = new Options
            {
                Runs = 20,
                FirstSeed = DefaultSeed,
                Archetypes = BotRunDriver.Archetypes.ToList(),
                Shard = "1/1",
                Profiles = new List<string> { ProfilePresets.Fresh },
                DepthCap = DefaultDepthCap,
                OutDir = outDir,
                CommitSha = "",
            };

            RunBatch(options);
            Debug.Log($"[BalanceBot] Wrote {outDir}. Run tools/bot_merge.py over it for summary.json, " +
                      "then tools/bot_report.py for the HTML.");
            EditorUtility.RevealInFinder(outDir);
        }

        // The -executeMethod entry tools/bot.ps1 launches.
        public static void RunFromCommandLine()
        {
            int exit = 0;

            try
            {
                var options = ReadOptions();
                RunBatch(options);
            }
            catch (Exception e)
            {
                // A throw that escapes RunBatch is the batch machinery failing,
                // not the game -- a game fault is a bugs[] row and never
                // reaches here. So this one IS an exit code.
                Debug.LogError("[BalanceBot] The batch itself failed: " + e);
                exit = 1;
            }

            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        // ---- the arguments ---------------------------------------------------------

        private sealed class Options
        {
            public int Runs = DefaultRuns;
            public ulong FirstSeed = DefaultSeed;
            public List<string> Archetypes = new List<string>();
            public List<string> Profiles = new List<string>();
            public int DepthCap = DefaultDepthCap;
            public string OutDir = "";
            public string CommitSha = "";

            // SAMPLED, NOT EVERY RUN. Replaying every seed doubles the batch
            // for a check whose answer is a property of the machinery rather
            // than of a seed -- if anything unseeded leaks into the loop it
            // leaks into every run, so one run in ten finds it just as surely
            // as ten in ten and costs a tenth as much. 1.0 restores the old
            // behaviour; 0 turns the check off entirely, which is a thing a
            // throughput measurement wants and a report never does.
            public double ReplayShare = DefaultReplayShare;

            // Off makes the bot write save_slot_0.json to a throwaway
            // directory the way it used to, which is how the equivalence of
            // the in-memory mode is demonstrated rather than asserted.
            public bool InMemorySaves = true;

            // "2/4" -- which of how many. Provenance only: the shard's seed
            // range and output directory are decided by tools/bot.ps1 and
            // arrive as -botSeed/-botRuns/-botOut, so nothing here branches on
            // this. It goes into batch.json so a shard directory found on its
            // own still says what it was part of.
            public string Shard = "1/1";
        }

        private static Options ReadOptions()
        {
            var args = Environment.GetCommandLineArgs();
            var o = new Options
            {
                Runs = ArgInt(args, "-botRuns", DefaultRuns),
                FirstSeed = ArgULong(args, "-botSeed", DefaultSeed),
                DepthCap = ArgInt(args, "-botDepthCap", DefaultDepthCap),
                OutDir = Arg(args, "-botOut", Path.Combine(Path.GetTempPath(), "pp-bot-out")),
                CommitSha = Arg(args, "-botCommit", ""),
                ReplayShare = ArgDouble(args, "-botReplayShare", DefaultReplayShare),
                InMemorySaves = ArgInt(args, "-botInMemorySaves", 1) != 0,
                Shard = Arg(args, "-botShard", "1/1"),
            };

            if (o.ReplayShare < 0) o.ReplayShare = 0;
            if (o.ReplayShare > 1) o.ReplayShare = 1;

            o.Archetypes = SplitList(Arg(args, "-botArchetypes", string.Join(",", BotRunDriver.Archetypes)))
                .Where(a => BotRunDriver.Archetypes.Contains(a))
                .ToList();

            o.Profiles = SplitList(Arg(args, "-botProfiles", ProfilePresets.Fresh))
                .Where(ProfilePresets.IsKnown)
                .ToList();

            // A misspelled archetype silently running the default set would be
            // a batch whose header lies about what it measured, so it refuses.
            if (o.Archetypes.Count == 0) throw new ArgumentException("-botArchetypes named nothing known");
            if (o.Profiles.Count == 0) throw new ArgumentException("-botProfiles named nothing known");
            if (o.Runs <= 0) throw new ArgumentException("-botRuns must be positive");

            return o;
        }

        private static List<string> SplitList(string raw) =>
            (raw ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        private static string Arg(string[] args, string name, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return fallback;
        }

        private static int ArgInt(string[] args, string name, int fallback) =>
            int.TryParse(Arg(args, name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        private static double ArgDouble(string[] args, string name, double fallback) =>
            double.TryParse(Arg(args, name, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;

        private static ulong ArgULong(string[] args, string name, ulong fallback) =>
            ulong.TryParse(Arg(args, name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v) ? v : fallback;

        // ---- one run's worth of bookkeeping -----------------------------------------

        // A completed run plus the determinism verdict for it. Kept together
        // because every metric below is over the FIRST play of a pair -- the
        // second exists only to compare hashes, and folding its trace into the
        // numbers would double-count every fight.
        // How many whole runs were actually PLAYED, replays included. Reset
        // with the batch; only the phase report reads it.
        private static int _runPlays;

        // WHAT THE QUIETENED LOG WOULD HAVE SAID.
        //
        // The run loop drops Debug.unityLogger.filterLogType to Error for its
        // duration -- in batchmode every Debug.Log walks the managed stack to
        // build a trace and then writes a line to disk, and a batch running
        // thousands of runs pays that for output nobody reads. Warnings and
        // infos are disposable here; an ERROR is not, and swallowing one would
        // turn a report into a lie. So errors and exceptions still flow
        // (filterLogType = Error passes exactly those) and land in bugs[]
        // against the run that produced them -- which is strictly MORE than the
        // old behaviour gave, where they went to the log file and appeared in
        // no report at all.
        private static readonly List<InvariantHit> LoggedThisRun = new List<InvariantHit>();

        private sealed class Played
        {
            public ulong Seed;
            public string Archetype;
            public string Profile;
            public BotRunDriver.BotRunResult Result;

            // A run nobody replayed is neither a match nor a mismatch, and
            // counting it as a match would inflate determinism.checked into a
            // claim the batch never actually made.
            public bool Replayed;
            public bool HashMatched;
        }

        // ---- the batch --------------------------------------------------------------

        private static void RunBatch(Options o)
        {
            var startedAt = DateTime.UtcNow;
            BotPhaseTimers.ResetBatch();
            _runPlays = 0;
            Directory.CreateDirectory(o.OutDir);

            string tracesPath = Path.Combine(o.OutDir, "traces.jsonl");
            string runsPath = Path.Combine(o.OutDir, "runs.jsonl");
            var played = new List<Played>();
            var progress = new List<string>();

            BotRunDriver.InMemorySaves = o.InMemorySaves;

            var previousFilter = Debug.unityLogger.filterLogType;
            Application.LogCallback capture = (condition, stack, type) =>
                LoggedThisRun.Add(new InvariantHit("LoggedError",
                    type + ": " + condition + (string.IsNullOrEmpty(stack) ? "" : "\n" + stack)));

            // NO BOM. Encoding.UTF8 writes one, and Python's json.load refuses
            // a leading BOM outright ("Unexpected UTF-8 BOM") -- which broke
            // tools/bot_report.py on the very first batch this wrote. Both
            // files go out through the same encoding for the same reason.
            using (var traces = new StreamWriter(tracesPath, append: false, NoBom))
            using (var runs = new StreamWriter(runsPath, append: false, NoBom))
            try
            {
                Debug.unityLogger.filterLogType = LogType.Error;
                Application.logMessageReceived += capture;

                foreach (string profile in o.Profiles)
                {
                    foreach (string archetype in o.Archetypes)
                    {
                        for (int i = 0; i < o.Runs; i++)
                        {
                            ulong seed = o.FirstSeed + (ulong)i;

                            // EVERY Nth RUN, not a random tenth: a sampled
                            // check whose sample moves between batches makes
                            // "this batch found no mismatch" unreproducible,
                            // which is the one property a determinism check
                            // cannot afford to lack.
                            bool replay = o.ReplayShare >= 1.0
                                || (o.ReplayShare > 0 && i % Math.Max(1, (int)Math.Round(1.0 / o.ReplayShare)) == 0);

                            var entry = PlayOnePairSafely(seed, archetype, profile, o.DepthCap, replay);
                            played.Add(entry);

                            // WRITTEN AS THEY FINISH, not held and dumped at
                            // the end, so a batch that is killed halfway
                            // leaves nearly all of its completed runs on disk
                            // rather than none -- which is what the schema's
                            // "partial batches" section is for and what makes
                            // a 10,000-run batch worth starting. NEARLY,
                            // deliberately: the StreamWriter still buffers, so
                            // a kill loses the last few KB. AutoFlush would
                            // close that gap and cost a syscall per run for a
                            // handful of rows nobody was going to read anyway.
                            using (BotPhaseTimers.Measure(BotPhase.TraceJson))
                            {
                                traces.WriteLine(TraceJson(entry.Result.Trace));
                                runs.WriteLine(RunRowJson(entry));
                            }
                        }

                        // Held rather than logged: the quiet window is still
                        // open here and would eat it. A progress line per cell
                        // is what a watched batch is steered by, so it comes
                        // back out below rather than being dropped.
                        progress.Add($"[BalanceBot] {profile}/{archetype}: {o.Runs} runs done.");
                    }
                }
            }
            finally
            {
                Application.logMessageReceived -= capture;
                Debug.unityLogger.filterLogType = previousFilter;
            }

            foreach (string line in progress) Debug.Log(line);

            double elapsed = (DateTime.UtcNow - startedAt).TotalSeconds;

            using (BotPhaseTimers.Measure(BotPhase.Summary))
            {
                File.WriteAllText(Path.Combine(o.OutDir, "content.json"), ContentJson(), NoBom);
                File.WriteAllText(Path.Combine(o.OutDir, "batch.json"), BatchJson(o, startedAt, elapsed), NoBom);
            }

            Debug.Log($"[BalanceBot] shard {o.Shard}: {played.Count} runs in {elapsed:F1}s -> {o.OutDir}. " +
                      "summary.json and the plain-text table come from tools/bot_merge.py.");

            // RUN-PLAYS, not runs: every metric is over the first play of a
            // pair and the second exists only to compare hashes, but the clock
            // pays for both -- so the throughput number has to count both or it
            // reports a rate the machine never actually achieved.
            Debug.Log(BotPhaseTimers.Report(_runPlays));
        }

        // TWICE, BACK TO BACK, ALWAYS -- the schema's determinism section. A
        // fight is milliseconds, so doubling the run count is cheap next to one
        // Unity boot, and non-determinism is the one defect that makes every
        // other number in the report unreproducible.
        //
        // Wrapped so a throw that somehow escapes BotRunDriver's own run-level
        // catch still costs one run rather than the batch.
        private static Played PlayOnePairSafely(
            ulong seed, string archetype, string profile, int depthCap, bool replay)
        {
            var entry = new Played { Seed = seed, Archetype = archetype, Profile = profile, HashMatched = true };
            LoggedThisRun.Clear();

            try
            {
                entry.Result = BotRunDriver.PlayRun(seed, archetype, profile, depthCap);
                _runPlays++;

                if (!replay) return entry;

                RunTrace again;
                using (BotPhaseTimers.Measure(BotPhase.Replay))
                {
                    again = BotRunDriver.PlayRun(seed, archetype, profile, depthCap).Trace;
                }

                _runPlays++;
                entry.Replayed = true;
                entry.HashMatched = entry.Result.Trace.Hash() == again.Hash();
            }
            catch (Exception e)
            {
                entry.Result = new BotRunDriver.BotRunResult();
                entry.Result.Trace.Seed = seed;
                entry.Result.Trace.Archetype = archetype;
                entry.Result.Trace.Profile = profile;
                entry.Result.Hits.Add(new InvariantHit("Exception",
                    "outside the run loop: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace));
            }
            finally
            {
                // Attributed to the run that was in flight when it fired. The
                // log callback cannot write straight onto entry.Result -- that
                // object does not exist until PlayRun returns -- so it parks
                // them in the list above and this is where they are claimed.
                // In a finally because the un-replayed path returns early.
                if (LoggedThisRun.Count > 0 && entry.Result != null)
                {
                    entry.Result.Hits.AddRange(LoggedThisRun);
                }

                LoggedThisRun.Clear();
            }

            return entry;
        }

        // ---- traces.jsonl -----------------------------------------------------------

        // Hand-written rather than JsonUtility. JsonUtility cannot express the
        // string->number maps summary.json is full of, so one serializer had to
        // exist here anyway; using it for both keeps the two files' escaping
        // and number formatting identical, and it is what guarantees the ulong
        // Seed is written as a full-precision integer rather than through a
        // double (the schema calls that out explicitly).
        private static string TraceJson(RunTrace t)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"Seed\":").Append(t.Seed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"Archetype\":").Append(Str(t.Archetype)).Append(',');
            sb.Append("\"Profile\":").Append(Str(t.Profile)).Append(',');

            sb.Append("\"Fights\":[");
            for (int i = 0; i < t.Fights.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var f = t.Fights[i];
                sb.Append('{');
                sb.Append("\"Step\":").Append(f.Step).Append(',');
                sb.Append("\"Floor\":").Append(f.Floor).Append(',');
                sb.Append("\"RoomType\":").Append(Str(f.RoomType)).Append(',');
                sb.Append("\"EnemyIds\":").Append(StrList(f.EnemyIds)).Append(',');
                sb.Append("\"Turns\":").Append(f.Turns).Append(',');
                sb.Append("\"PartyHpIn\":").Append(f.PartyHpIn).Append(',');
                sb.Append("\"PartyHpOut\":").Append(f.PartyHpOut).Append(',');
                sb.Append("\"DamageDealt\":").Append(f.DamageDealt).Append(',');
                sb.Append("\"DamageTaken\":").Append(f.DamageTaken).Append(',');
                sb.Append("\"Won\":").Append(f.Won ? "true" : "false").Append(',');
                sb.Append("\"PayoutGold\":").Append(f.PayoutGold).Append(',');
                sb.Append("\"PayoutExp\":").Append(f.PayoutExp).Append(',');
                sb.Append("\"TurnTraces\":[");
                for (int j = 0; j < f.TurnTraces.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    var turn = f.TurnTraces[j];
                    sb.Append('{');
                    sb.Append("\"ActorId\":").Append(Str(turn.ActorId)).Append(',');
                    sb.Append("\"Action\":").Append(Str(turn.Action)).Append(',');
                    sb.Append("\"TargetId\":").Append(Str(turn.TargetId)).Append(',');
                    sb.Append("\"PartyHpAfter\":").Append(turn.PartyHpAfter).Append(',');
                    sb.Append("\"EnemyHpAfter\":").Append(turn.EnemyHpAfter);
                    sb.Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("],");

            sb.Append("\"Rooms\":[");
            for (int i = 0; i < t.Rooms.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = t.Rooms[i];
                sb.Append('{');
                sb.Append("\"Step\":").Append(r.Step).Append(',');
                sb.Append("\"NodeId\":").Append(r.NodeId).Append(',');
                sb.Append("\"RoomType\":").Append(Str(r.RoomType)).Append(',');
                sb.Append("\"OfferItemIds\":").Append(StrList(r.OfferItemIds)).Append(',');
                sb.Append("\"PickedIndex\":").Append(r.PickedIndex);
                sb.Append('}');
            }
            sb.Append("],");

            sb.Append("\"DeathStep\":").Append(t.DeathStep).Append(',');
            sb.Append("\"DeathCause\":").Append(Str(t.DeathCause)).Append(',');
            sb.Append("\"Capped\":").Append(t.Capped ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        // ---- runs.jsonl, content.json, batch.json ------------------------------------

        // ONE FLAT ROW PER RUN, and no aggregate anywhere in this file.
        //
        // summary.json used to be computed HERE, in-process, and
        // docs/BOT_SUMMARY_SCHEMA.md gave a good reason: doomedShare and
        // swingShare are fractions of the party's MAX hp, which is not a trace
        // field and must not become one. That reason still holds -- but it is
        // a reason to compute the RUN'S OWN numbers here, not the batch's.
        //
        // Sharding is what forced the split. A batch now runs as N Unity
        // processes over disjoint seed ranges, and a median, a decision-
        // pressure denominator or a coverage list cannot be merged from N
        // summaries: the median of medians is not the median, and "this relic
        // was never offered" is only true if it was never offered in ANY
        // shard. So each shard writes the facts and one merger
        // (tools/bot_merge.py) computes every aggregate exactly once, over all
        // of them. Two implementations of doomedShare -- one here for the
        // single-shard case and one there -- is precisely the thing that
        // drifts, so there is only the one, and it is the Python.
        //
        // What travels is everything an aggregate needs and nothing it does
        // not: party max HP per fight (the schema's own example of a number
        // only this side knows), the swing count already reduced against it,
        // and otherwise the same ids and counters traces.jsonl carries.
        private static string RunRowJson(Played p)
        {
            var t = p.Result.Trace;
            var sb = new StringBuilder();

            sb.Append('{');
            sb.Append("\"seed\":").Append(p.Seed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"archetype\":").Append(Str(p.Archetype)).Append(',');
            sb.Append("\"profile\":").Append(Str(p.Profile)).Append(',');
            sb.Append("\"capped\":").Append(t.Capped ? "true" : "false").Append(',');
            sb.Append("\"deathStep\":").Append(t.DeathStep).Append(',');
            sb.Append("\"consumablesLeft\":").Append(p.Result.ConsumablesLeftAtEnd).Append(',');
            sb.Append("\"replayed\":").Append(p.Replayed ? "true" : "false").Append(',');
            sb.Append("\"hashMatched\":").Append(p.HashMatched ? "true" : "false").Append(',');

            // ALREADY REDUCED, because the alternative is shipping every
            // TurnTrace's PartyHpAfter and the live max HP beside it to
            // Python -- which is traces.jsonl plus a column, for a number that
            // is two counters. swingShare is a pooled share (swings / turns
            // over the whole cell), so summing the two counters across runs
            // gives the identical answer to computing it run by run.
            CountSwings(p.Result, out int swings, out int swingTurns);
            sb.Append("\"swingTurns\":").Append(swings).Append(',');
            sb.Append("\"swingDenomTurns\":").Append(swingTurns).Append(',');

            sb.Append("\"relicIds\":").Append(StrList(p.Result.RelicIdsAtEnd)).Append(',');
            sb.Append("\"talentIds\":").Append(StrList(p.Result.TalentIdsAtEnd)).Append(',');

            sb.Append("\"fights\":[");
            for (int i = 0; i < t.Fights.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var f = t.Fights[i];

                sb.Append("{\"step\":").Append(f.Step);
                sb.Append(",\"floor\":").Append(f.Floor);
                sb.Append(",\"roomType\":").Append(Str(f.RoomType));
                sb.Append(",\"enemyIds\":").Append(StrList(f.EnemyIds));
                sb.Append(",\"turns\":").Append(f.Turns);
                sb.Append(",\"damageTaken\":").Append(f.DamageTaken);
                sb.Append(",\"partyHpOut\":").Append(f.PartyHpOut);

                // THE NUMBER THE SCHEMA SAYS ONLY THIS SIDE KNOWS. Read live
                // off the encounter when the fight was built; doomedShare is a
                // fraction of it and it is deliberately not a trace field.
                sb.Append(",\"partyMaxHp\":")
                  .Append(i < p.Result.PartyMaxHpPerFight.Count ? p.Result.PartyMaxHpPerFight[i] : 0);

                sb.Append(",\"usedItem\":")
                  .Append(f.TurnTraces.Any(x => x.Action != null && x.Action.StartsWith("Item:")) ? "true" : "false");
                sb.Append('}');
            }
            sb.Append("],");

            sb.Append("\"rooms\":[");
            for (int i = 0; i < t.Rooms.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var r = t.Rooms[i];
                sb.Append("{\"step\":").Append(r.Step);
                sb.Append(",\"nodeId\":").Append(r.NodeId);
                sb.Append(",\"offerItemIds\":").Append(StrList(r.OfferItemIds));
                sb.Append(",\"pickedIndex\":").Append(r.PickedIndex).Append('}');
            }
            sb.Append("],");

            sb.Append("\"relicRounds\":[");
            for (int i = 0; i < p.Result.RelicRounds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var round = p.Result.RelicRounds[i];
                sb.Append("{\"offerIds\":").Append(StrList(round.OfferIds));
                sb.Append(",\"pickedIndex\":").Append(round.PickedIndex).Append('}');
            }
            sb.Append("],");

            var skillsUsed = t.Fights
                .SelectMany(f => f.TurnTraces)
                .Where(x => x.Action != null && x.Action.StartsWith("Skill:"))
                .Select(x => x.Action.Substring("Skill:".Length))
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            sb.Append("\"skillsUsed\":").Append(StrList(skillsUsed)).Append(',');

            // step/nodeId are the run's LAST recorded room, and lastActions the
            // last ten commands -- the same approximation the schema already
            // documents, kept here because both need the turn traces, which do
            // not travel in this file.
            var lastRoom = t.Rooms.LastOrDefault();
            var lastActions = t.Fights
                .SelectMany(f => f.TurnTraces)
                .Select(x => x.Action)
                .Reverse().Take(10).Reverse()
                .ToList();

            sb.Append("\"bugs\":[");
            for (int i = 0; i < p.Result.Hits.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var hit = p.Result.Hits[i];
                sb.Append("{\"invariant\":").Append(Str(hit.Name));
                sb.Append(",\"step\":").Append(lastRoom?.Step ?? 0);
                sb.Append(",\"nodeId\":").Append(lastRoom == null ? "null" : lastRoom.NodeId.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"detail\":").Append(Str(hit.Detail));
                sb.Append(",\"lastActions\":").Append(StrList(lastActions));
                sb.Append(",\"stack\":").Append(hit.Name == "Exception" ? Str(hit.Detail) : "null");
                sb.Append('}');
            }
            sb.Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        // How often a single command moved the party's health bar by more than
        // a quarter of its maximum, and how many commands were eligible to.
        //
        // Fights whose max HP never got recorded are excluded from BOTH
        // counters rather than counted as non-swings, which is what the
        // in-process version did -- a zero denominator is honest about not
        // knowing; a padded one is not.
        private static void CountSwings(BotRunDriver.BotRunResult result, out int swings, out int turns)
        {
            swings = 0;
            turns = 0;

            for (int i = 0; i < result.Trace.Fights.Count; i++)
            {
                var fight = result.Trace.Fights[i];
                int max = i < result.PartyMaxHpPerFight.Count ? result.PartyMaxHpPerFight[i] : 0;
                if (max <= 0) continue;

                int before = fight.PartyHpIn;
                foreach (var turn in fight.TurnTraces)
                {
                    turns++;
                    if (Math.Abs(turn.PartyHpAfter - before) / (double)max > 0.25) swings++;
                    before = turn.PartyHpAfter;
                }
            }
        }

        // THE LISTS COVERAGE IS DIFFERENCED AGAINST.
        //
        // Written per shard rather than left for the merger to look up,
        // because the merger is stdlib Python with no Unity behind it and
        // ContentDatabase is the only authority on what exists. Identical
        // across shards of one batch (same commit, same content); the merger
        // unions them anyway, so a mismatched shard degrades to "the union of
        // what each shard believed" rather than to a wrong answer.
        private static string ContentJson()
        {
            var enemies = ContentDatabase.Enemies.Where(e => e != null).ToList();

            // AN ENEMY ABILITY IS A skills.json ENTRY, owned by an enemy
            // through RawEnemyAbility.skillId -- monsters and characters are
            // authored in the same file because the rules are identical. So
            // ContentDatabase.Skills has always contained both, and
            // coverage.skillsNeverUsed has always listed every enemy ability
            // as "never used" (FightRunner traces the PLAYER's commands, so an
            // enemy's skill can never appear there). That is not a finding, it
            // is noise, and it was burying the player-side gaps that are.
            // This list is what lets the report split the two apart.
            var enemyAbilities = enemies
                .Where(e => e.abilities != null)
                .SelectMany(e => e.abilities)
                .Where(a => a != null && !string.IsNullOrEmpty(a.skillId))
                .Select(a => a.skillId)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("\"enemyIds\":").Append(StrList(enemies.Select(e => e.id).ToList())).Append(",\n");
            sb.Append("\"relicIds\":").Append(StrList(ContentDatabase.Relics.Where(r => r != null).Select(r => r.id).ToList())).Append(",\n");

            // OFFERABLE, not every item. ContentDatabase.Items holds equipment
            // and consumables the offer table cannot draw at all, and listing
            // those as "never offered" every batch is noise that buries the
            // one line that matters.
            sb.Append("\"offerableItemIds\":").Append(StrList(ContentDatabase.Offerable.Where(i => i != null).Select(i => i.id).ToList())).Append(",\n");
            sb.Append("\"skillIds\":").Append(StrList(ContentDatabase.Skills.Where(s => s != null).Select(s => s.id).ToList())).Append(",\n");
            sb.Append("\"enemyAbilityIds\":").Append(StrList(enemyAbilities)).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        // What this shard was asked to do. The merger reconciles these into
        // summary.json's "batch" block -- runsPerCell sums across shards,
        // firstSeed is the lowest, and elapsedSeconds is the WALL of the
        // slowest shard rather than the sum, because the shards ran at once.
        private static string BatchJson(Options o, DateTime startedAt, double elapsed)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("\"timestamp\":").Append(Str(startedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))).Append(",\n");
            sb.Append("\"commitSha\":").Append(Str(o.CommitSha)).Append(",\n");
            sb.Append("\"shard\":").Append(Str(o.Shard)).Append(",\n");
            sb.Append("\"runsPerCell\":").Append(o.Runs).Append(",\n");
            sb.Append("\"firstSeed\":").Append(o.FirstSeed.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("\"archetypes\":").Append(StrList(o.Archetypes)).Append(",\n");
            sb.Append("\"profiles\":").Append(StrList(o.Profiles)).Append(",\n");
            sb.Append("\"depthCapSteps\":").Append(o.DepthCap).Append(",\n");
            sb.Append("\"replayShare\":").Append(Num(o.ReplayShare)).Append(",\n");
            sb.Append("\"elapsedSeconds\":").Append(Num(elapsed)).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string StrList(IReadOnlyList<string> items)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Str(items[i]));
            }
            return sb.Append(']').ToString();
        }

        private static string Str(string raw)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in raw ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
