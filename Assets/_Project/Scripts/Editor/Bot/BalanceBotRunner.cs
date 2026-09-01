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
    // THE BATCH: seeds x archetypes x profiles, played headless, summarised.
    //
    // Everything in summary.json is computed HERE, in-process, while the live
    // save and the finished traces are both still in hand -- that is the split
    // docs/BOT_SUMMARY_SCHEMA.md states and the reason it states it: doomed and
    // swing are fractions of the party's MAX hp, which is not a trace field and
    // must not become one (it moves with level, gear and relics, and recording
    // it per turn would bloat every trace for a number this side already has).
    // tools/bot_report.py renders and diffs summary.json; it never recomputes a
    // metric from traces.jsonl, which is archival.
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

        [MenuItem("Tools/Balance Bot/Run a small batch (20 seeds, both archetypes, Fresh)")]
        public static void RunASmallBatchFromTheMenu()
        {
            string outDir = Path.Combine(Path.GetTempPath(), "pp-bot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            var options = new Options
            {
                Runs = 20,
                FirstSeed = DefaultSeed,
                Archetypes = BotRunDriver.Archetypes.ToList(),
                Profiles = new List<string> { ProfilePresets.Fresh },
                DepthCap = DefaultDepthCap,
                OutDir = outDir,
                CommitSha = "",
            };

            RunBatch(options);
            Debug.Log($"[BalanceBot] Wrote {outDir}. Open summary.json, or run tools/bot_report.py over it.");
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
            };

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

        private sealed class Played
        {
            public ulong Seed;
            public string Archetype;
            public string Profile;
            public BotRunDriver.BotRunResult Result;
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
            var played = new List<Played>();

            // NO BOM. Encoding.UTF8 writes one, and Python's json.load refuses
            // a leading BOM outright ("Unexpected UTF-8 BOM") -- which broke
            // tools/bot_report.py on the very first batch this wrote. Both
            // files go out through the same encoding for the same reason.
            using (var traces = new StreamWriter(tracesPath, append: false, NoBom))
            {
                foreach (string profile in o.Profiles)
                {
                    foreach (string archetype in o.Archetypes)
                    {
                        for (int i = 0; i < o.Runs; i++)
                        {
                            ulong seed = o.FirstSeed + (ulong)i;
                            var entry = PlayOnePairSafely(seed, archetype, profile, o.DepthCap);
                            played.Add(entry);

                            using (BotPhaseTimers.Measure(BotPhase.TraceJson))
                            {
                                traces.WriteLine(TraceJson(entry.Result.Trace));
                            }
                        }

                        Debug.Log($"[BalanceBot] {profile}/{archetype}: {o.Runs} runs done.");
                    }
                }
            }

            double elapsed = (DateTime.UtcNow - startedAt).TotalSeconds;

            string summary;
            using (BotPhaseTimers.Measure(BotPhase.Summary))
            {
                summary = SummaryJson(o, played, startedAt, elapsed);
            }

            File.WriteAllText(Path.Combine(o.OutDir, "summary.json"), summary, NoBom);

            Debug.Log(PlainText(o, played, elapsed));

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
        private static Played PlayOnePairSafely(ulong seed, string archetype, string profile, int depthCap)
        {
            var entry = new Played { Seed = seed, Archetype = archetype, Profile = profile, HashMatched = true };

            try
            {
                entry.Result = BotRunDriver.PlayRun(seed, archetype, profile, depthCap);
                _runPlays++;

                RunTrace again;
                using (BotPhaseTimers.Measure(BotPhase.Replay))
                {
                    again = BotRunDriver.PlayRun(seed, archetype, profile, depthCap).Trace;
                }

                _runPlays++;
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

        // ---- summary.json -------------------------------------------------------------

        private static string SummaryJson(Options o, List<Played> played, DateTime startedAt, double elapsed)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");

            sb.Append("\"batch\":{");
            sb.Append("\"timestamp\":").Append(Str(startedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))).Append(',');
            sb.Append("\"commitSha\":").Append(Str(o.CommitSha)).Append(',');
            sb.Append("\"runsPerCell\":").Append(o.Runs).Append(',');
            sb.Append("\"firstSeed\":").Append(o.FirstSeed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"archetypes\":").Append(StrList(o.Archetypes)).Append(',');
            sb.Append("\"profiles\":").Append(StrList(o.Profiles)).Append(',');
            sb.Append("\"depthCapSteps\":").Append(o.DepthCap).Append(',');
            sb.Append("\"elapsedSeconds\":").Append(Num(elapsed));
            sb.Append("},\n");

            sb.Append("\"cells\":[");
            bool first = true;
            foreach (string profile in o.Profiles)
            {
                foreach (string archetype in o.Archetypes)
                {
                    var cell = played.Where(p => p.Profile == profile && p.Archetype == archetype).ToList();
                    if (cell.Count == 0) continue;

                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('\n').Append(CellJson(o, archetype, profile, cell));
                }
            }
            sb.Append("],\n");

            sb.Append("\"decisionPressure\":").Append(DecisionPressureJson(o, played)).Append(",\n");
            sb.Append("\"coverage\":").Append(CoverageJson(played)).Append(",\n");
            sb.Append("\"archetypeGap\":").Append(ArchetypeGapJson(o, played)).Append(",\n");
            sb.Append("\"bugs\":").Append(BugsJson(played)).Append(",\n");

            var mismatches = played.Where(p => !p.HashMatched).ToList();
            sb.Append("\"determinism\":{\"checked\":").Append(played.Count).Append(",\"mismatches\":[");
            for (int i = 0; i < mismatches.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"seed\":").Append(mismatches[i].Seed.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"archetype\":").Append(Str(mismatches[i].Archetype))
                  .Append(",\"profile\":").Append(Str(mismatches[i].Profile)).Append('}');
            }
            sb.Append("]}\n");

            sb.Append("}\n");
            return sb.ToString();
        }

        // Depth of one run: the step it died on, or the cap if it never did.
        // A capped run "did not die, but it did stop there" (schema).
        private static int DepthOf(Played p, int cap) => p.Result.Trace.Capped ? cap : p.Result.Trace.DeathStep;

        private static string CellJson(Options o, string archetype, string profile, List<Played> cell)
        {
            var depths = cell.Select(p => DepthOf(p, o.DepthCap)).OrderBy(d => d).ToList();
            var sb = new StringBuilder();

            sb.Append("{\"archetype\":").Append(Str(archetype));
            sb.Append(",\"profile\":").Append(Str(profile));
            sb.Append(",\"runs\":").Append(cell.Count);

            sb.Append(",\"depth\":{\"median\":").Append(Num(Percentile(depths, 0.5)))
              .Append(",\"p10\":").Append(Num(Percentile(depths, 0.10)))
              .Append(",\"p90\":").Append(Num(Percentile(depths, 0.90)))
              .Append(",\"mean\":").Append(Num(depths.Count == 0 ? 0 : depths.Average()))
              .Append(",\"cappedShare\":").Append(Num(Share(cell.Count(p => p.Result.Trace.Capped), cell.Count)))
              .Append('}');

            sb.Append(",\"deathCauses\":").Append(DeathCausesJson(cell));
            sb.Append(",\"doomedShare\":").Append(Num(DoomedShare(cell)));
            sb.Append(",\"swingShare\":").Append(Num(SwingShare(cell)));

            var fights = cell.SelectMany(p => p.Result.Trace.Fights).ToList();

            sb.Append(",\"fightLength\":{\"byFloor\":")
              .Append(NumMap(fights.GroupBy(f => f.Floor.ToString(CultureInfo.InvariantCulture))
                                   .ToDictionary(g => g.Key, g => g.Average(f => (double)f.Turns))))
              .Append(",\"byRoomType\":")
              .Append(NumMap(fights.GroupBy(f => f.RoomType)
                                   .ToDictionary(g => g.Key, g => g.Average(f => (double)f.Turns))))
              .Append('}');

            sb.Append(",\"turnOneShare\":").Append(Num(Share(fights.Count(f => f.Turns == 1), fights.Count)));

            sb.Append(",\"steamrollByFloor\":")
              .Append(NumMap(fights.GroupBy(f => f.Floor.ToString(CultureInfo.InvariantCulture))
                                   .ToDictionary(g => g.Key, g => Share(g.Count(f => f.DamageTaken == 0), g.Count()))));

            sb.Append(",\"consumableUseShare\":")
              .Append(Num(Share(fights.Count(f => f.TurnTraces.Any(t => t.Action != null && t.Action.StartsWith("Item:"))), fights.Count)));

            sb.Append(",\"potionsWastedMean\":")
              .Append(Num(cell.Count == 0 ? 0 : cell.Average(p => (double)p.Result.ConsumablesLeftAtEnd)));

            sb.Append(",\"buildDiversity\":").Append(BuildDiversityJson(o, cell));
            sb.Append(",\"itemPickRate\":").Append(NumMap(ItemPickRate(cell)));
            sb.Append(",\"relicPickRate\":").Append(NumMap(RelicPickRate(cell)));
            sb.Append('}');
            return sb.ToString();
        }

        // ---- the cell's harder metrics -------------------------------------------------

        private static string DeathCausesJson(List<Played> cell)
        {
            // Keyed on (enemyId, roomType, floor); a multi-enemy fight
            // contributes one row per enemy standing in it, because the trace
            // does not record which one landed the killing blow and inventing
            // an answer would be worse than saying "these were present".
            var counts = new Dictionary<string, int>();
            var parts = new Dictionary<string, Tuple<string, string, int>>();

            foreach (var p in cell)
            {
                var trace = p.Result.Trace;
                if (trace.Capped) continue;

                var fight = trace.Fights.FirstOrDefault(f => f.Step == trace.DeathStep);
                if (fight == null) continue;

                foreach (string enemyId in fight.EnemyIds.Distinct())
                {
                    string key = enemyId + "|" + fight.RoomType + "|" + fight.Floor;
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                    parts[key] = Tuple.Create(enemyId, fight.RoomType, fight.Floor);
                }
            }

            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var kv in counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!first) sb.Append(',');
                first = false;
                var part = parts[kv.Key];
                sb.Append("{\"enemyId\":").Append(Str(part.Item1))
                  .Append(",\"roomType\":").Append(Str(part.Item2))
                  .Append(",\"floor\":").Append(part.Item3)
                  .Append(",\"count\":").Append(kv.Value).Append('}');
            }
            return sb.Append(']').ToString();
        }

        // "Never above half health at the end of any of the last three fights
        // before death" -- a run that was never going to make it, as distinct
        // from one that died to a spike. Max HP off BotRunResult's per-fight
        // capture, which is read live off the encounter; it is not a trace
        // field and the schema says explicitly not to make it one.
        private static double DoomedShare(List<Played> cell)
        {
            int denominator = 0;
            int doomed = 0;

            foreach (var p in cell)
            {
                var trace = p.Result.Trace;
                if (trace.Capped || trace.Fights.Count < 1) continue;

                denominator++;

                int from = Math.Max(0, trace.Fights.Count - 3);
                bool everHealthy = false;

                for (int i = from; i < trace.Fights.Count; i++)
                {
                    int max = i < p.Result.PartyMaxHpPerFight.Count ? p.Result.PartyMaxHpPerFight[i] : 0;
                    if (max <= 0) continue;
                    if ((double)trace.Fights[i].PartyHpOut / max > 0.5) { everHealthy = true; break; }
                }

                if (!everHealthy) doomed++;
            }

            return Share(doomed, denominator);
        }

        // How often a single command moves the party's health bar by more than
        // a quarter of its maximum. Too few is a slog; too many is a coin flip.
        private static double SwingShare(List<Played> cell)
        {
            int turns = 0;
            int swings = 0;

            foreach (var p in cell)
            {
                var trace = p.Result.Trace;
                for (int i = 0; i < trace.Fights.Count; i++)
                {
                    var fight = trace.Fights[i];
                    int max = i < p.Result.PartyMaxHpPerFight.Count ? p.Result.PartyMaxHpPerFight[i] : 0;
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

            return Share(swings, turns);
        }

        // Distinct relic/talent sets among the DEEPEST tenth of the cell's
        // runs. Ranked on the same depth figure the cell's distribution uses
        // (a capped run counts as the cap) rather than the raw DeathStep,
        // which reads 0 for a capped run and would sort the strongest runs to
        // the bottom.
        private static string BuildDiversityJson(Options o, List<Played> cell)
        {
            int take = Math.Max(1, (int)Math.Ceiling(cell.Count * 0.10));
            var deepest = cell.OrderByDescending(p => DepthOf(p, o.DepthCap)).Take(take).ToList();

            int relicSets = deepest
                .Select(p => string.Join("+", p.Result.RelicIdsAtEnd.OrderBy(id => id, StringComparer.Ordinal)))
                .Distinct().Count();

            int talentSets = deepest
                .Select(p => string.Join("+", p.Result.TalentIdsAtEnd.OrderBy(id => id, StringComparer.Ordinal)))
                .Distinct().Count();

            return $"{{\"distinctRelicSets\":{relicSets},\"distinctTalentSets\":{talentSets}}}";
        }

        private static Dictionary<string, double> ItemPickRate(List<Played> cell)
        {
            var offered = new Dictionary<string, int>();
            var picked = new Dictionary<string, int>();

            foreach (var room in cell.SelectMany(p => p.Result.Trace.Rooms))
            {
                for (int i = 0; i < room.OfferItemIds.Count; i++)
                {
                    Bump(offered, room.OfferItemIds[i]);
                    if (i == room.PickedIndex) Bump(picked, room.OfferItemIds[i]);
                }
            }

            return offered.ToDictionary(kv => kv.Key, kv => Share(picked.TryGetValue(kv.Key, out int n) ? n : 0, kv.Value));
        }

        private static Dictionary<string, double> RelicPickRate(List<Played> cell)
        {
            var offered = new Dictionary<string, int>();
            var picked = new Dictionary<string, int>();

            foreach (var round in cell.SelectMany(p => p.Result.RelicRounds))
            {
                for (int i = 0; i < round.OfferIds.Count; i++)
                {
                    Bump(offered, round.OfferIds[i]);
                    if (i == round.PickedIndex) Bump(picked, round.OfferIds[i]);
                }
            }

            return offered.ToDictionary(kv => kv.Key, kv => Share(picked.TryGetValue(kv.Key, out int n) ? n : 0, kv.Value));
        }

        // ---- batch-wide -------------------------------------------------------------------

        // "A choice every archetype makes the same way is dead." Denominator is
        // positions reached by two or more archetypes on the same seed and
        // profile -- a position only one archetype ever sees (because an
        // earlier disagreement diverged the run) counts neither way.
        private static string DecisionPressureJson(Options o, List<Played> played)
        {
            if (o.Archetypes.Count < 2)
            {
                // With one archetype there is nobody to disagree with, and a
                // zero would read as "every choice is dead" rather than "not
                // measured".
                return "{\"nodes\":null,\"offers\":null,\"relics\":null}";
            }

            var nodes = new Dictionary<string, List<string>>();
            var offers = new Dictionary<string, List<string>>();
            var relics = new Dictionary<string, List<string>>();

            foreach (var p in played)
            {
                string run = p.Seed + "|" + p.Profile;

                foreach (var room in p.Result.Trace.Rooms)
                {
                    Collect(nodes, run + "|" + room.Step, room.NodeId.ToString(CultureInfo.InvariantCulture));

                    if (room.OfferItemIds.Count > 0)
                    {
                        Collect(offers, run + "|" + room.Step + "|" + room.NodeId,
                            room.PickedIndex.ToString(CultureInfo.InvariantCulture));
                    }
                }

                Collect(relics, run, string.Join(">", p.Result.RelicRounds.Select(r =>
                    r.PickedIndex >= 0 && r.PickedIndex < r.OfferIds.Count ? r.OfferIds[r.PickedIndex] : "-")));
            }

            return "{\"nodes\":" + Num(Disagreement(nodes))
                 + ",\"offers\":" + Num(Disagreement(offers))
                 + ",\"relics\":" + Num(Disagreement(relics)) + "}";
        }

        private static double Disagreement(Dictionary<string, List<string>> positions)
        {
            int reached = 0;
            int differed = 0;

            foreach (var kv in positions)
            {
                if (kv.Value.Count < 2) continue;
                reached++;
                if (kv.Value.Distinct().Count() > 1) differed++;
            }

            return Share(differed, reached);
        }

        // What the batch never touched, differenced against the content
        // database rather than against itself -- a relic that exists and was
        // never drawn is the finding; a relic that is silently absent from the
        // traces is not one anybody can see.
        private static string CoverageJson(List<Played> played)
        {
            var seenEnemies = new HashSet<string>(played.SelectMany(p => p.Result.Trace.Fights).SelectMany(f => f.EnemyIds));

            var offeredRelics = new HashSet<string>(played.SelectMany(p => p.Result.RelicRounds).SelectMany(r => r.OfferIds));
            var pickedRelics = new HashSet<string>(played.SelectMany(p => p.Result.RelicRounds)
                .Where(r => r.PickedIndex >= 0 && r.PickedIndex < r.OfferIds.Count)
                .Select(r => r.OfferIds[r.PickedIndex]));

            var offeredItems = new HashSet<string>(played.SelectMany(p => p.Result.Trace.Rooms).SelectMany(r => r.OfferItemIds));
            var pickedItems = new HashSet<string>(played.SelectMany(p => p.Result.Trace.Rooms)
                .Where(r => r.PickedIndex >= 0 && r.PickedIndex < r.OfferItemIds.Count)
                .Select(r => r.OfferItemIds[r.PickedIndex]));

            var usedSkills = new HashSet<string>(played
                .SelectMany(p => p.Result.Trace.Fights).SelectMany(f => f.TurnTraces)
                .Where(t => t.Action != null && t.Action.StartsWith("Skill:"))
                .Select(t => t.Action.Substring("Skill:".Length)));

            var allEnemies = ContentDatabase.Enemies.Where(e => e != null).Select(e => e.id).ToList();
            var allRelics = ContentDatabase.Relics.Where(r => r != null).Select(r => r.id).ToList();

            // OFFERABLE, not every item. ContentDatabase.Items holds equipment
            // and consumables the offer table cannot draw at all, and listing
            // those as "never offered" every batch is noise that buries the
            // one line that matters.
            var allItems = ContentDatabase.Offerable.Where(i => i != null).Select(i => i.id).ToList();
            var allSkills = ContentDatabase.Skills.Where(s => s != null).Select(s => s.id).ToList();

            return "{\"enemiesNeverSeen\":" + StrList(allEnemies.Where(id => !seenEnemies.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList())
                 + ",\"relicsNeverOffered\":" + StrList(allRelics.Where(id => !offeredRelics.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList())
                 + ",\"relicsNeverPicked\":" + StrList(allRelics.Where(id => !pickedRelics.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList())
                 + ",\"itemsNeverOffered\":" + StrList(allItems.Where(id => !offeredItems.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList())
                 + ",\"itemsNeverPicked\":" + StrList(allItems.Where(id => !pickedItems.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList())
                 + ",\"skillsNeverUsed\":" + StrList(allSkills.Where(id => !usedSkills.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList())
                 + "}";
        }

        private static string ArchetypeGapJson(Options o, List<Played> played)
        {
            var sb = new StringBuilder("[");
            bool first = true;

            foreach (string profile in o.Profiles)
            {
                if (!first) sb.Append(',');
                first = false;

                sb.Append("{\"profile\":").Append(Str(profile)).Append(",\"archetypes\":[");
                bool innerFirst = true;

                foreach (string archetype in o.Archetypes)
                {
                    var cell = played.Where(p => p.Profile == profile && p.Archetype == archetype).ToList();
                    if (cell.Count == 0) continue;

                    if (!innerFirst) sb.Append(',');
                    innerFirst = false;

                    var depths = cell.Select(p => DepthOf(p, o.DepthCap)).OrderBy(d => d).ToList();
                    sb.Append("{\"archetype\":").Append(Str(archetype))
                      .Append(",\"medianDepth\":").Append(Num(Percentile(depths, 0.5))).Append('}');
                }

                sb.Append("]}");
            }

            return sb.Append(']').ToString();
        }

        // One row per hit, never deduplicated -- a run that trips the same
        // invariant twice is two rows, because twice is the finding.
        //
        // step/nodeId are the run's LAST recorded room rather than the exact
        // position of the hit: InvariantHit carries a name and a detail and
        // nothing else, and widening it to carry a position would put run
        // bookkeeping inside a Domain type whose only job is to say what went
        // wrong. The detail line names the step in its own text where the
        // driver knows it, and lastActions is what a repro actually needs.
        private static string BugsJson(List<Played> played)
        {
            var sb = new StringBuilder("[");
            bool first = true;

            foreach (var p in played)
            {
                if (p.Result.Hits.Count == 0) continue;

                var lastRoom = p.Result.Trace.Rooms.LastOrDefault();
                var lastActions = p.Result.Trace.Fights
                    .SelectMany(f => f.TurnTraces)
                    .Select(t => t.Action)
                    .Reverse().Take(10).Reverse()
                    .ToList();

                foreach (var hit in p.Result.Hits)
                {
                    if (!first) sb.Append(',');
                    first = false;

                    sb.Append("{\"invariant\":").Append(Str(hit.Name))
                      .Append(",\"seed\":").Append(p.Seed.ToString(CultureInfo.InvariantCulture))
                      .Append(",\"archetype\":").Append(Str(p.Archetype))
                      .Append(",\"profile\":").Append(Str(p.Profile))
                      .Append(",\"step\":").Append(lastRoom?.Step ?? 0)
                      .Append(",\"nodeId\":").Append(lastRoom == null ? "null" : lastRoom.NodeId.ToString(CultureInfo.InvariantCulture))
                      .Append(",\"detail\":").Append(Str(hit.Detail))
                      .Append(",\"lastActions\":").Append(StrList(lastActions))
                      .Append(",\"stack\":").Append(hit.Name == "Exception" ? Str(hit.Detail) : "null")
                      .Append('}');
                }
            }

            return sb.Append(']').ToString();
        }

        // ---- the log's own summary ------------------------------------------------------

        // The same numbers as summary.json, in the shape somebody reads off a
        // terminal without opening a browser. Deliberately short: depth per
        // cell, the bug count by name, and determinism. Everything else is what
        // the HTML is for.
        private static string PlainText(Options o, List<Played> played, double elapsed)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== BALANCE BOT ===");
            sb.AppendLine($"{o.Runs} runs/cell x {o.Archetypes.Count} archetypes x {o.Profiles.Count} profiles, " +
                          $"seeds {o.FirstSeed}..{o.FirstSeed + (ulong)o.Runs - 1}, depth cap {o.DepthCap}, " +
                          $"{elapsed:F1}s");
            sb.AppendLine();
            sb.AppendLine("profile/archetype              runs  median  p10  p90  capped  doomed  swing");

            foreach (string profile in o.Profiles)
            {
                foreach (string archetype in o.Archetypes)
                {
                    var cell = played.Where(p => p.Profile == profile && p.Archetype == archetype).ToList();
                    if (cell.Count == 0) continue;

                    var depths = cell.Select(p => DepthOf(p, o.DepthCap)).OrderBy(d => d).ToList();
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0,-30}{1,5}{2,8:F1}{3,5:F0}{4,5:F0}{5,8:P0}{6,8:P0}{7,7:P0}",
                        profile + "/" + archetype, cell.Count,
                        Percentile(depths, 0.5), Percentile(depths, 0.10), Percentile(depths, 0.90),
                        Share(cell.Count(p => p.Result.Trace.Capped), cell.Count),
                        DoomedShare(cell), SwingShare(cell)));
                }
            }

            var byName = played.SelectMany(p => p.Result.Hits.Select(h => new { p.Seed, p.Archetype, p.Profile, h.Name }))
                .GroupBy(x => x.Name)
                .OrderByDescending(g => g.Count())
                .ToList();

            sb.AppendLine();
            sb.AppendLine($"bugs: {byName.Sum(g => g.Count())} hits across {byName.Count} invariant(s)");
            foreach (var group in byName)
            {
                var one = group.First();
                sb.AppendLine($"  {group.Key} x{group.Count()}  (e.g. seed {one.Seed} {one.Profile}/{one.Archetype})");
            }

            var mismatches = played.Where(p => !p.HashMatched).ToList();
            sb.AppendLine();
            sb.AppendLine(mismatches.Count == 0
                ? $"determinism: {played.Count} runs replayed, 0 mismatches"
                : $"determinism: {played.Count} runs replayed, {mismatches.Count} MISMATCHES " +
                  $"(e.g. seed {mismatches[0].Seed} {mismatches[0].Profile}/{mismatches[0].Archetype})");

            return sb.ToString();
        }

        // ---- the small shared bits ---------------------------------------------------------

        private static void Bump(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
        }

        private static void Collect(Dictionary<string, List<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<string>();
                map[key] = list;
            }
            list.Add(value);
        }

        private static double Share(int part, int whole) => whole <= 0 ? 0 : (double)part / whole;

        // Nearest-rank on an already-sorted list, which is what a depth
        // distribution wants: every value it reports is a step somebody
        // actually reached rather than an interpolation between two.
        private static double Percentile(List<int> sorted, double q)
        {
            if (sorted.Count == 0) return 0;
            int index = (int)Math.Ceiling(q * sorted.Count) - 1;
            return sorted[Math.Min(sorted.Count - 1, Math.Max(0, index))];
        }

        private static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string NumMap(Dictionary<string, double> map)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in map.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Str(kv.Key)).Append(':').Append(Num(kv.Value));
            }
            return sb.Append('}').ToString();
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
