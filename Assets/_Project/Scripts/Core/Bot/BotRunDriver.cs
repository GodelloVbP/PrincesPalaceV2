using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // ONE WHOLE RUN, PLAYED HEADLESS, THROUGH THE GAME'S OWN DOORS.
    //
    // Every rule this loop obeys comes out of RunOrchestrator and RunManager --
    // arrival, the fight build, the settlement, the loot roll, the leg advance.
    // Nothing here decides anything about the game; it decides only the ORDER
    // in which it asks, and that order is copied from the screens
    // (RelicDraftController.Commit, MapController.Walk, FightBootstrap,
    // FightController.Input, ReckoningController) rather than invented. That is
    // the whole bargain of docs/PLAN_BALANCE_BOT.md F2: one rulebook, two
    // callers, and a bot that measures the game instead of a copy of it.
    public static class BotRunDriver
    {
        // ---- the archetype registry ---------------------------------------------

        // FORWARDED, not restated. Domain.Bot.Archetypes is the one registry;
        // this used to be a second switch beside it, and a second switch is a
        // second thing that can disagree -- it already had, the moment Phase 6
        // added GreedyDefensive and Lookahead2 to Domain and left the batch
        // runner and the smoke test unable to name them.
        //
        // Kept as a forward rather than deleted because three callers reach the
        // registry through Core (the batch runner's -botArchetypes, the smoke
        // test, the report's archetypeGap) and Core is the layer they are
        // allowed to see. A single object implements BOTH interfaces per
        // archetype, which is the shape every policy in Domain already has -- a
        // fight brain and a map brain that disagreed about what the archetype
        // is would make the whole comparison meaningless.
        public static IReadOnlyList<string> Archetypes => Domain.Bot.Archetypes.Names;

        private static object PolicyFor(string archetype) => Domain.Bot.Archetypes.Create(archetype);

        // ---- the seeded streams -------------------------------------------------

        // LOCAL STREAM IDS, NOT RngStreams CONSTANTS, and deliberately so.
        //
        // RngStreams' own header calls its numbers "a serialized format, not
        // constants that can be tidied" -- every run ever seeded is numbered by
        // them. Adding Bot streams there would be safe today (new numbers only)
        // but it puts the bot's needs inside the file that decides what a
        // PLAYER's map looks like, and the next person to add one has to reason
        // about a consumer that is not the game. These are the bot's own, they
        // never touch a saved run, and RngStreams.Derive is a pure hash of
        // (seed, stream, a, b) with no ordering to preserve -- so an id nothing
        // else uses is simply a stream nothing else draws from.
        //
        // Started at 101 to stay clearly out of the 1..4 the game uses.
        private const uint NodeStream = 101;
        private const uint FightStream = 102;
        private const uint OfferStream = 103;
        private const uint RelicStream = 104;

        // The three between-fight streams. Separate from each other and from
        // the four above for the reason the four are separate: a stream is
        // consumed a different number of times depending on what the policy
        // does, and sharing one would make an extra tie-break in the equip pass
        // shift the map choice two rooms later. Started where the block above
        // ends rather than reusing a gap, so a stream id is never ambiguous
        // about which decision it seeded.
        private const uint PresetStream = 105;
        private const uint EquipStream = 106;
        private const uint LevelStream = 107;

        // The shop's own, opened per CHOICE rather than per visit: a policy
        // that draws a different number of times on its second call would
        // otherwise shift what its third call sees. Position is (step,
        // choiceIndex), so the same seed asked the same question at the same
        // point in the same visit always gets the same tie-break.
        private const uint ShopStream = 108;
        private const uint SpellAssignmentStream = 109;

        // ONE PER RUN WITHIN A CAREER -- derives that run's own seed from the
        // career's single top-level seed and the run's index, so a career
        // replays deterministically without the caller having to hand in N
        // seeds by hand. See PlayCareer.
        private const uint CareerRunStream = 110;

        // ---- what one run hands back --------------------------------------------

        // One relic-draft round, which RunTrace has no field for.
        //
        // Not added to RunTrace.cs: docs/BOT_SUMMARY_SCHEMA.md pins that file's
        // shape as the traces.jsonl contract, and relicPickRate /
        // decisionPressure.relics are computed IN THE RUNNER (same reasoning as
        // party max HP -- see the schema's own note). This rides alongside the
        // trace instead.
        public sealed class RelicRound
        {
            public List<string> OfferIds = new List<string>();

            // Index into OfferIds; -1 for "took nothing", which is a legal
            // answer the draft screen allows and so the bot must be able to
            // give.
            public int PickedIndex = -1;
        }

        // The trace plus everything the summary needs that the trace
        // deliberately does not carry.
        //
        // The plan asks PlayRun for "RunTrace + List<InvariantHit>"; this is
        // that, with the in-process-only side channel the schema requires
        // rather than a widened RunTrace. Every extra field here is a number
        // that is only knowable while the live save and session still exist,
        // which is exactly the schema's stated reason for computing metrics in
        // the runner.
        public sealed class BotRunResult
        {
            public RunTrace Trace = new RunTrace();
            public List<InvariantHit> Hits = new List<InvariantHit>();

            // Index-aligned with Trace.Fights: the party's TOTAL max HP for
            // that fight, read live off the encounter. doomedShare and
            // swingShare are fractions of this and it is not a trace field.
            public List<int> PartyMaxHpPerFight = new List<int>();

            // What the run was holding when it ended. Captured BEFORE the
            // settlement, because RunManager.EndRun clears the stockpile and
            // replaces the snapshot -- reading either afterwards reports zero
            // for every dead run, which is every run.
            public List<string> RelicIdsAtEnd = new List<string>();
            public List<string> TalentIdsAtEnd = new List<string>();
            public int ConsumablesLeftAtEnd;

            public List<RelicRound> RelicRounds = new List<RelicRound>();

            // Milliseconds of wall clock, for the batch's own budget.
            public double ElapsedMs;
        }

        // ---- career mode -----------------------------------------------------------

        // ONE ROW PER LIFE IN A CAREER. Deliberately separate from BotRunResult
        // rather than a widened copy of it: a career row's own reason to exist
        // is the pair no single-life run has anything to compare against --
        // per-character level/exp AFTER this run, so the summary can show a
        // gains-per-run table without re-deriving cross-run state from a trace
        // that (correctly) knows nothing about any run but its own.
        public sealed class CareerRunSummary
        {
            public int RunIndex;

            // The run's own DeathStep, or DepthCapSteps when Capped -- the
            // single number a "run -> deepest step" table wants, so a caller
            // does not have to re-apply that same ternary at every read site.
            public int DeepestStep;
            public bool Capped;

            // Keyed by CharacterDefinition.id (Character.definitionId), read
            // off save.ActiveSquad() the instant this run ends -- the fielded
            // squad only, matching RunTrace.LevelAtDeath's own scope, but per
            // character here because a career's whole point is showing HOW
            // level/exp move run over run, and a squad-wide sum degrading three
            // characters into one number is exactly the loss PLAN_BALANCE_BOT.md
            // F5's own per-character correction (2026-09-11) was about.
            public Dictionary<string, int> LevelByCharacter = new Dictionary<string, int>();
            public Dictionary<string, int> ExpByCharacter = new Dictionary<string, int>();

            public BotRunResult Result = new BotRunResult();
        }

        public sealed class BotCareerResult
        {
            public List<CareerRunSummary> Runs = new List<CareerRunSummary>();

            // Milliseconds of wall clock for the WHOLE career, mirroring
            // BotRunResult.ElapsedMs's own units.
            public double ElapsedMs;
        }

        // ---- probes: forcing one event, one relic, one build (M8a) ----------------

        // WHAT A TUNING BATCH PINS DOWN that a plain batch leaves to the dice
        // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md M8a). Every field empty/off is
        // exactly a plain run. tools/bot.ps1's -ForceEvent / -ForceEventFloor /
        // -EventChoice / -NoTransform / -GrantRelic / -GrantTalent / -GrantBook /
        // -NoTelegraphAnswer.
        public sealed class BotProbe
        {
            // ForceEventId with ForceEventFloor 0: every Event room opens this
            // event instead of its roll. With a floor n > 0: ONCE per run, at
            // the first room on floor n that is not a fight or a shop -- the
            // node choice prefers an Event node, then any other non-fight,
            // non-shop node -- and that room is the event and nothing else
            // (no rest heal, no stash). A run that dies before floor n, or
            // whose floor n offers only fights and shops, never sees it.
            public string ForceEventId = "";
            public int ForceEventFloor;

            // Case-insensitive substring of a choice's text. On any event page
            // that has a visible, enabled choice containing it, that choice is
            // taken; everywhere else the first available one, as always.
            public string EventChoiceText = "";

            // Transforms off the menu entirely (TransformUse.Never). Off, a
            // ready Transform is cast whenever none is worn, and the resource
            // it costs is saved for it until then (WhenReady).
            public bool NoTransform;

            // Added to the run's relics after the draft.
            public List<string> GrantRelicIds = new List<string>();

            // Kindled on their owner (and every prerequisite, transitively)
            // after the profile is built, free: "-GrantTalent
            // sheep_ram_converge" is a Black Ram build.
            public List<string> GrantTalentIds = new List<string>();

            // Spell books taught and slotted when the run opens, free: each
            // on the character its skill names (characterId), else the first
            // fielded character with a free slot. "-GrantBook palace_passage"
            // is Shawn with the Passage (PLAN_BELLWETHER_KIT M6).
            public List<string> GrantBookIds = new List<string>();

            // The telegraph answer off (FightRunner answerTelegraphs false):
            // the archetype alone decides, as before M6.
            public bool NoTelegraphAnswer;

            public bool IsEmpty =>
                string.IsNullOrEmpty(ForceEventId) && string.IsNullOrEmpty(EventChoiceText) && !NoTransform
                && GrantRelicIds.Count == 0 && GrantTalentIds.Count == 0 && GrantBookIds.Count == 0
                && !NoTelegraphAnswer;
        }

        // The probe of the run in flight: set by PlayRun for its own duration
        // and put back in a finally, so a career (which never sets one) and
        // every later run see none.
        private static BotProbe _probe;
        private static bool _forcedEventDone;

        // Shawn's kit id. The Bell and the flock are his (plan 1.4).
        private const string ShawnKitId = "sheep";

        // ---- the run -------------------------------------------------------------

        // THE SAVE STAYS IN RAM, unless somebody deliberately turns that off.
        //
        // Measured (see BotPhaseTimers' first phase table): the game persists
        // 28 times per run and at ~5.7ms a write that was 91% of a batch's
        // wall clock. None of those writes influences what a run does -- they
        // exist so a player can quit mid-descent -- so the bot keeps the slot
        // in a dictionary instead. It is a FLAG rather than a hardcode because
        // the only way to know the optimisation is safe is to play the same
        // seed both ways and compare RunTrace.Hash(), which is exactly what
        // BalanceBotSmokeTests does with it.
        public static bool InMemorySaves = true;

        // ONE THROWAWAY ROOT FOR THE PROCESS, not one directory per run.
        //
        // Only reached when InMemorySaves is off: with it on nothing is ever
        // written here at all, and the root exists purely so a stray writer
        // that ignores InMemory still cannot reach the player's real
        // persistentDataPath.
        private static string _sharedRoot;

        private static string SharedRoot()
        {
            if (_sharedRoot == null)
            {
                _sharedRoot = Path.Combine(Path.GetTempPath(), "pp-bot-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_sharedRoot);
            }

            return _sharedRoot;
        }

        // `shopNodes` is a BATCH-LEVEL switch, not an archetype's opinion --
        // see ShopNodePreference. Defaulted rather than made a static like
        // InMemorySaves because it changes what a run DOES, and a
        // process-global that changes results is a thing a caller forgets to
        // reset; the two modes are meant to be run as two batches over the
        // same seeds and compared.
        public static BotRunResult PlayRun(ulong seed, string archetype, string profile, int depthCapSteps,
            ShopNodeMode shopNodes = ShopNodeMode.WhenOffered, BotProbe probe = null)
        {
            var result = new BotRunResult();
            result.Trace.Seed = seed;
            result.Trace.Archetype = archetype;
            result.Trace.Profile = profile;

            var started = DateTime.UtcNow;

            // With the save in RAM the root is shared and never written to; on
            // disk it has to be per-run, because isolation between runs IS the
            // fresh directory -- run N+1's Forget()+CurrentSave would otherwise
            // load run N's save_slot_0.json instead of taking Load's
            // missing-file branch.
            bool inMemory = InMemorySaves;
            string root = inMemory
                ? SharedRoot()
                : Path.Combine(Path.GetTempPath(), "pp-bot-" + Guid.NewGuid().ToString("N"));

            try
            {
                // THE SAME HARNESS FightSettlementTests USES, for the same
                // reasons: a save root nothing else can see (so a batch cannot
                // touch the player's own save, and two runs cannot leak state
                // through one), a Navigation that goes nowhere (Go() would try
                // to load a scene from a process that has none open), and a
                // RunManager whose cached map is dropped between runs.
                OpenHarness(root, inMemory);

                _probe = probe;
                _forcedEventDone = false;
                PlayOneRun(seed, archetype, profile, depthCapSteps, shopNodes, result);
            }
            catch (Exception e)
            {
                // ANY EXCEPTION, ANYWHERE, IS A FINDING AND NOT A CRASH
                // (plan §3). Caught at the run level so one bad seed costs one
                // row in the bug section rather than the rest of the batch --
                // and recorded WITH the stack, because a repro that says only
                // "something threw" is not a repro.
                result.Hits.Add(new InvariantHit("Exception", e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace));
            }
            finally
            {
                _probe = null;
                _forcedEventDone = false;

                // The SHARED root outlives the run by design; only a per-run
                // one is this run's to delete -- CloseHarness itself checks
                // inMemory before deleting.
                CloseHarness(root, inMemory);
            }

            result.ElapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;
            return result;
        }

        // ---- shared harness open/close, used by both PlayRun and PlayCareer ------

        private static void OpenHarness(string root, bool inMemory)
        {
            using (BotPhaseTimers.Measure(BotPhase.HarnessSetup))
            {
                if (!inMemory) Directory.CreateDirectory(root);

                SaveSystem.InMemory = inMemory;
                SaveSystem.ClearMemory();
                SaveSystem.RootOverride = root;
                SaveSlotManager.CurrentSlot = 0;
                SaveSlotManager.Forget();
                RunManager.ResetForTests();
                RoomResolver.Reset();
                Navigation.LoadOverride = _ => { };
            }
        }

        private static void CloseHarness(string root, bool inMemory)
        {
            using (BotPhaseTimers.Measure(BotPhase.HarnessTeardown))
            {
                Navigation.Reset();
                SaveSystem.RootOverride = null;
                SaveSystem.InMemory = false;
                SaveSystem.ClearMemory();
                SaveSlotManager.Forget();
                RunManager.ResetForTests();
                RoomResolver.Reset();

                try
                {
                    if (!inMemory && Directory.Exists(root)) Directory.Delete(root, recursive: true);
                }
                catch (IOException)
                {
                    // A temp directory that will not delete is litter, not a
                    // finding -- it must not turn a clean run into a bug row.
                }
            }
        }

        // ---- career mode -----------------------------------------------------------

        // ONE SAVE, MANY DESCENTS -- the career the real game already has
        // (die, or walk out of a capped descent, and the next gate press
        // starts a new run on the SAME character) and that PlayRun above
        // never exercised: every call wiped the save with SaveSystem.
        // ClearMemory() and rebuilt the profile from scratch, so a batch
        // could only ever measure a single first life. This plays
        // `runsInCareer` of them back to back against ONE save.
        //
        // ProfilePresets.Build RUNS ONCE, for run 0 -- level, track claim,
        // stat points and talents for the STARTING preset, same as PlayRun.
        // Every later run reuses that SAME SaveData and goes through
        // RunOrchestrator.StartRun / RunManager.EndRun exactly the way the
        // real game moves from one descent's end to the next one's start,
        // rather than a bespoke "carry everything" copy this method would
        // otherwise have to invent and keep in sync by hand.
        //
        // THAT IS ALSO WHY GEAR DOES NOT SURVIVE A CAREER RUN BOUNDARY, even
        // though a first reading of "keep level, exp, stat points, embers,
        // talents, gear" could be taken to mean it should: RunManager.EndRun
        // clears every roster character's equipment and the whole stockpile
        // on EVERY completed run, win or lose (its own header explains why --
        // inventory and loot are run-scoped, same as a relic). "Run-scoped
        // state resets as a real new run does" is the rule this method is
        // built to MATCH, not override, so it does not special-case gear to
        // survive a boundary nothing else survives either. Level, exp,
        // claimedTrackLevel, stat points, embers and talents all live on
        // Character/SaveData and are untouched by EndRun, so those five carry
        // over exactly as asked, through the save object staying alive across
        // the whole loop rather than through any code added here.
        //
        // DETERMINISM: each life's own seed is derived from the career's one
        // top-level seed and its run index (RngStreams.Derive, CareerRunStream)
        // rather than handed in per call, so a career replays byte-identically
        // from the single seed alone -- the same guarantee PlayRun's own
        // determinism check already makes for a single life, extended over
        // the whole sequence (see BalanceBotSmokeTests' career case).
        public static BotCareerResult PlayCareer(ulong seed, string archetype, string profile,
            int runsInCareer, int depthCapSteps, ShopNodeMode shopNodes = ShopNodeMode.WhenOffered)
        {
            var careerResult = new BotCareerResult();
            var started = DateTime.UtcNow;

            bool inMemory = InMemorySaves;
            string root = inMemory
                ? SharedRoot()
                : Path.Combine(Path.GetTempPath(), "pp-bot-" + Guid.NewGuid().ToString("N"));

            try
            {
                OpenHarness(root, inMemory);

                // Just to fail fast on a bad archetype/profile before doing
                // any work -- the per-run instance built inside the loop
                // below is the one every run actually plays with.
                var probePolicy = PolicyFor(archetype);
                if (!(probePolicy is IFightPolicy) || !(probePolicy is IRunPolicy))
                {
                    careerResult.Runs.Add(FailedCareerRow(new InvariantHit(
                        "UnknownArchetype", $"no policy named '{archetype}'")));
                    return careerResult;
                }

                if (!ProfilePresets.IsKnown(profile))
                {
                    careerResult.Runs.Add(FailedCareerRow(new InvariantHit(
                        "UnknownProfile", $"no preset named '{profile}'")));
                    return careerResult;
                }

                // probePolicy IS THE ONE-OFF INSTANCE THE PRESET BUILD USES:
                // Build only reads ChooseStat/ChooseTalent, which score off
                // static weights and take their RNG as a parameter (see the
                // loop below), so probePolicy's guard dictionary never gets
                // touched here and there is nothing to reset before the
                // per-run instances start.
                SaveData save;
                using (BotPhaseTimers.Measure(BotPhase.PresetBuild))
                {
                    save = ProfilePresets.Build(profile, (IRunPolicy)probePolicy, StreamFor(seed, PresetStream, 0, 0));
                }

                if (save == null)
                {
                    careerResult.Runs.Add(FailedCareerRow(new InvariantHit("NoSave", "the profile built no save")));
                    return careerResult;
                }

                for (int i = 0; i < runsInCareer; i++)
                {
                    // ONE POLICY INSTANCE PER RUN, not one for the whole
                    // career: GreedyAggressivePolicy._repeatGuard and
                    // GreedyDefensivePolicy._enemyHpAtLastCheck are
                    // CombatantState-keyed dictionaries the policy itself
                    // documents as "one instance per run" (their own
                    // headers) precisely so they reset at a run boundary the
                    // way a fresh opponent's HP history should. RNG is not
                    // at risk from this -- every Choose*/ChooseNode/etc. call
                    // takes its SeededRandom as a parameter derived from
                    // (seed, stream, step, node), never stored on the
                    // policy, so rebuilding here changes nothing about which
                    // random numbers a run draws.
                    var policy = PolicyFor(archetype);
                    var fightPolicy = policy as IFightPolicy;
                    var runPolicy = policy as IRunPolicy;

                    // BELT AND BRACES, PER THE BRIEF: every won fight already
                    // claims the track and spends every stat point
                    // (CollectLevelUps, called from PlayTheFight), so this is
                    // a no-op idempotent call on every run whose last fight
                    // was a win it already settled through -- exactly the
                    // "idempotent against its own watermark" guarantee
                    // ClaimTrackRewards already makes. It exists so a node
                    // earned right up to the previous life's last moment is
                    // guaranteed active before the next life's first choice,
                    // even if some future change to the settlement order
                    // ever left one uncollected.
                    using (BotPhaseTimers.Measure(BotPhase.LevelUp))
                    {
                        CollectLevelUps(seed, save, runPolicy, step: -(i + 1));
                    }

                    ulong runSeed = RngStreams.Derive(seed, CareerRunStream, i, 0);

                    var runResult = new BotRunResult();
                    runResult.Trace.Seed = runSeed;
                    runResult.Trace.Archetype = archetype;
                    runResult.Trace.Profile = profile;

                    RunOrchestrator.StartRun(runSeed);
                    if (!RunManager.HasRun)
                    {
                        runResult.Hits.Add(new InvariantHit("NoRun", "StartRun left no run standing"));
                    }
                    else
                    {
                        PlayDescent(runSeed, save, fightPolicy, runPolicy, depthCapSteps, shopNodes, runResult);

                        // A CAPPED RUN NEVER DIES, so nothing else ever calls
                        // EndRun for it -- SettleFight's own EndRun call only
                        // fires on a loss. Settle it explicitly here, the way
                        // a player choosing to leave a capped descent and
                        // press the gate again would, so the NEXT life's
                        // StartRun opens onto a real "no run standing" hub
                        // state instead of overwriting a run that is still,
                        // technically, open.
                        if (RunManager.HasRun) RunManager.EndRun();
                    }

                    var row = new CareerRunSummary
                    {
                        RunIndex = i,
                        DeepestStep = runResult.Trace.Capped ? depthCapSteps : runResult.Trace.DeathStep,
                        Capped = runResult.Trace.Capped,
                        Result = runResult,
                    };

                    foreach (var character in save.ActiveSquad())
                    {
                        if (character == null) continue;
                        row.LevelByCharacter[character.definitionId] = character.level;
                        row.ExpByCharacter[character.definitionId] = character.exp;
                    }

                    careerResult.Runs.Add(row);
                }
            }
            catch (Exception e)
            {
                // SAME REASONING AS PlayRun's OWN CATCH: one bad career costs
                // one row, with the stack, rather than the rest of the batch.
                careerResult.Runs.Add(FailedCareerRow(new InvariantHit(
                    "Exception", e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace)));
            }
            finally
            {
                // Litter, not a finding -- same as PlayRun's own catch;
                // CloseHarness carries that reasoning now.
                CloseHarness(root, inMemory);
            }

            careerResult.ElapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;
            return careerResult;
        }

        private static CareerRunSummary FailedCareerRow(InvariantHit hit)
        {
            var row = new CareerRunSummary();
            row.Result.Hits.Add(hit);
            return row;
        }

        private static void PlayOneRun(
            ulong seed, string archetype, string profile, int depthCapSteps, ShopNodeMode shopNodes,
            BotRunResult result)
        {
            var policy = PolicyFor(archetype);
            var fightPolicy = policy as IFightPolicy;
            var runPolicy = policy as IRunPolicy;
            if (fightPolicy == null || runPolicy == null)
            {
                result.Hits.Add(new InvariantHit("UnknownArchetype", $"no policy named '{archetype}'"));
                return;
            }

            if (!ProfilePresets.IsKnown(profile))
            {
                result.Hits.Add(new InvariantHit("UnknownProfile", $"no preset named '{profile}'"));
                return;
            }

            SaveData save;
            using (BotPhaseTimers.Measure(BotPhase.PresetBuild))
            {
                // THE ARCHETYPE BUILDS ITS OWN CHARACTER. Where the level's
                // points go and what the embers buy are decisions with the same
                // standing as which room to walk into, and a preset that made
                // them by house rule was sanding half the archetype gap off
                // before the run started -- see ProfilePresets' own header.
                save = ProfilePresets.Build(profile, runPolicy, StreamFor(seed, PresetStream, 0, 0));
            }

            if (save == null)
            {
                result.Hits.Add(new InvariantHit("NoSave", "the profile built no save"));
                return;
            }

            GrantTalents(save, result);

            RunOrchestrator.StartRun(seed);
            if (!RunManager.HasRun)
            {
                result.Hits.Add(new InvariantHit("NoRun", "StartRun left no run standing"));
                return;
            }

            PlayDescent(seed, save, fightPolicy, runPolicy, depthCapSteps, shopNodes, result);
        }

        // ONE DESCENT, on a save and a run StartRun has already opened -- the
        // draft, the equip pass, and the room-by-room loop that used to be
        // PlayOneRun's own tail, unchanged. Split out so PlayCareer (below)
        // can call this once per life against the SAME save across many
        // calls instead of duplicating the loop or forcing a fresh profile
        // build every time PlayOneRun's shape used to require.
        private static void PlayDescent(
            ulong seed, SaveData save, IFightPolicy fightPolicy, IRunPolicy runPolicy,
            int depthCapSteps, ShopNodeMode shopNodes, BotRunResult result)
        {
            using (BotPhaseTimers.Measure(BotPhase.RelicDraft))
            {
                Draft(seed, runPolicy, result);
            }

            GrantRelics(result);
            GrantBooks(save, result);

            // DRESS BEFORE WALKING IN. A player opens the character sheet on
            // the way out of the hub and wears the best of what they own; the
            // bot did not, so every batch before this measured a Late profile
            // fighting in a level-1 starting kit.
            //
            // AFTER the draft rather than before it AND after -- the plan asks
            // for a pass at both points, and the draft touches relics, never
            // gear or the bag, so the earlier of the two could only ever find
            // exactly what this one finds. One pass, at the later point, is the
            // same answer for half the work.
            result.Trace.EquippedAtStart.AddRange(EquipPassTraced(seed, save, runPolicy, step: -1, node: 0));

            // The last state seen while the run was still alive. EndRun
            // replaces the snapshot and strips the stockpile, so anything read
            // after the fatal settlement reports the empty shape rather than
            // what the party actually died holding.
            CaptureWhatTheRunHolds(save, result);

            // A ceiling on ROOMS, above the depth cap on STEPS, so a run that
            // somehow stops advancing its step (an arrival refused forever, a
            // leg that will not advance) still terminates. It is a bug when it
            // fires, and it is recorded as one.
            int roomBudget = Math.Max(64, depthCapSteps * 8);
            int rooms = 0;

            while (RunManager.HasRun)
            {
                if (RunManager.Run.step >= depthCapSteps)
                {
                    result.Trace.Capped = true;
                    break;
                }

                if (++rooms > roomBudget)
                {
                    result.Hits.Add(new InvariantHit("RunDidNotTerminate",
                        $"{rooms} rooms visited without dying or reaching the depth cap at step {RunManager.Run.step}"));
                    break;
                }

                var choices = RunManager.Choices();
                if (choices.Count == 0)
                {
                    // LegIsOver() IS "Choices() is empty" -- one is defined as
                    // the other on RunManager, so the plan's "Choices() empty
                    // while LegIsOver() is false" cannot fire here and asserting
                    // it would be theatre. The check with teeth is the one
                    // after a won fight (StuckAfterWin, below): the settlement
                    // is supposed to have advanced the leg already, so reaching
                    // this branch at all means it did not.
                    RunManager.AdvanceLeg();
                    continue;
                }

                // THE SHOP PREFERENCE WRAPS THE POLICY, IT DOES NOT REPLACE
                // IT. WhenOffered answers the node itself only when a Shop is
                // on the table and the archetype would not have rested;
                // Never narrows the list and lets the archetype choose out of
                // what is left. Everything else is the archetype's own rule,
                // unchanged (ShopNodePreference).
                var view = ViewOf(save);
                var offered = ShopNodePreference.ChoicesFor(shopNodes, choices);
                bool forceHere = ForcedEventDueOnThisFloor();
                var node = (forceHere ? ForcedEventNode(offered) : null)
                           ?? ShopNodePreference.PreferredNode(
                               shopNodes, offered, view, runPolicy.RestBelowPartyHpFraction)
                           ?? runPolicy.ChooseNode(offered, view, StreamFor(seed, NodeStream, RunManager.Run.step, rooms));

                if (node == null)
                {
                    result.Hits.Add(new InvariantHit("NoNodeChosen", $"the policy chose nothing from {choices.Count} options"));
                    break;
                }

                var roomTrace = new RoomTrace
                {
                    Step = RunManager.Run.step,
                    Floor = RunManager.Run.floor,
                    NodeId = node.Id,
                    RoomType = node.Type.ToString(),
                    PickedIndex = -1,
                };

                bool isFight = RunOrchestrator.IsFight(node.Type);

                // BEFORE ArriveAt, so a treasure room's stash is not already
                // counted -- this is what the party WALKED IN WITH.
                roomTrace.GoldOnArrival = RunManager.Run.gold;

                RunOrchestrator.Arrival arrival;
                using (BotPhaseTimers.Measure(BotPhase.RoomResolve))
                {
                    arrival = forceHere && CanHostForcedEvent(node)
                        ? ArriveAtForcedEvent(node, roomTrace, result)
                        : RunOrchestrator.ArriveAt(node);
                }

                // Every-Event-room mode (-ForceEventFloor 0): the roll is
                // replaced where it stood.
                if (arrival == RunOrchestrator.Arrival.Event && !roomTrace.EventForced
                    && !string.IsNullOrEmpty(_probe?.ForceEventId) && _probe.ForceEventFloor <= 0)
                {
                    if (RunManager.Run.eventId == _probe.ForceEventId
                        || RunOrchestrator.OpenEventForDebug(_probe.ForceEventId))
                    {
                        roomTrace.EventForced = true;
                    }
                    else
                    {
                        result.Hits.Add(new InvariantHit("ForcedEventMissing",
                            $"-ForceEvent '{_probe.ForceEventId}' is not in the built content"));
                    }
                }

                // Read before the visit: the event's Leave clears it.
                if (arrival == RunOrchestrator.Arrival.Event) roomTrace.EventId = RunManager.Run.eventId ?? "";

                if (arrival == RunOrchestrator.Arrival.Refused)
                {
                    result.Hits.Add(new InvariantHit("ArrivalRefused",
                        $"node {node.Id} ({node.Type}) was offered by Choices() and refused by MoveTo"));
                    break;
                }

                // MoveTo writes the step (and can cross a leg boundary into a
                // new floor), so both are only right once arrival has happened.
                roomTrace.Step = RunManager.Run.step;
                roomTrace.Floor = RunManager.Run.floor;

                // THE isFight SPLIT IS NO LONGER TWO-WAY. A shop is the
                // second room that does not resolve itself on arrival, so
                // "not a fight" no longer means "already done".
                if (arrival == RunOrchestrator.Arrival.Shop)
                {
                    using (BotPhaseTimers.Measure(BotPhase.Shop))
                    {
                        VisitShop(seed, save, runPolicy, roomTrace);
                    }
                    using (BotPhaseTimers.Measure(BotPhase.SpellAssign))
                    {
                        ResolvePendingSpellAssignments(seed, save, runPolicy, roomTrace);
                    }
                    result.Trace.Rooms.Add(roomTrace);
                    CaptureWhatTheRunHolds(save, result);
                    continue;
                }

                // THE THIRD ROOM THAT DOES NOT RESOLVE ITSELF. An event
                // left open would leave the node uncleared for the rest of
                // the leg, so the visit ends in LeaveEvent whatever happens.
                if (arrival == RunOrchestrator.Arrival.Event)
                {
                    bool survived;
                    using (BotPhaseTimers.Measure(BotPhase.RoomResolve))
                    {
                        survived = VisitEvent(seed, save, node, fightPolicy, runPolicy, roomTrace, result);
                    }
                    result.Trace.Rooms.Add(roomTrace);
                    if (!survived) return;
                    CaptureWhatTheRunHolds(save, result);
                    continue;
                }

                if (!isFight)
                {
                    result.Trace.Rooms.Add(roomTrace);
                    CaptureWhatTheRunHolds(save, result);
                    continue;
                }

                bool alive = PlayTheFight(seed, save, node, fightPolicy, runPolicy, roomTrace, result);

                // A won fight can drop a book (VictoryRewards.RollSpellDrop,
                // RunOrchestrator.SettleFight) -- resolved here whether or
                // not the party survives the room, same reason a dead run's
                // empty pool below is simply a no-op rather than a case to
                // special-case.
                using (BotPhaseTimers.Measure(BotPhase.SpellAssign))
                {
                    ResolvePendingSpellAssignments(seed, save, runPolicy, roomTrace);
                }
                result.Trace.Rooms.Add(roomTrace);

                if (!alive) return;
            }
        }

        // ---- the event room --------------------------------------------------------

        // A ceiling on choices in one event, because an authored "do it
        // again" loop (a choice whose outcome goes back to its own page) is
        // legal content and "first available choice" can walk it forever.
        // Past this the bot leaves and records it -- a finding about the
        // content or the policy, not a hang. Far above any visit the demo
        // event can make (its first choice is Leave).
        private const int MaxEventChoices = 32;

        // WHAT THE BOT DOES IN AN EVENT: THE FIRST AVAILABLE CHOICE UNTIL IT
        // LEAVES (plan contract 13). "Available" is EventChoiceGate's answer,
        // the one the panel greys with, so the bot can never pick what a
        // player could not. Deliberately no policy: an archetype's opinion on
        // events is a later question, and taking the first open choice is the
        // floor every event must survive.
        //
        // The visit ALWAYS ends with the room cleared, or with the run over in
        // an `endRun` event fight. Anything else is a hit.
        //
        // AN EVENT FIGHT IS PLAYED WHERE IT STARTS (plan M2): a pick that
        // leaves a request pending hands straight to PlayTheFight, the room
        // fight's own routine, which builds through the same
        // CurrentEncounterRequest seam the screen does. Back from it the loop
        // reads the event again, now on the fight's result. Returns false
        // when the run ended in the fight.
        private static bool VisitEvent(ulong seed, SaveData save, DescentNode node,
            IFightPolicy fightPolicy, IRunPolicy runPolicy, RoomTrace roomTrace, BotRunResult result)
        {
            for (int picks = 0; picks < MaxEventChoices; picks++)
            {
                if (RunOrchestrator.EventFightPending)
                {
                    bool alive = PlayTheFight(seed, save, node, fightPolicy, runPolicy, roomTrace, result);
                    using (BotPhaseTimers.Measure(BotPhase.SpellAssign))
                    {
                        ResolvePendingSpellAssignments(seed, save, runPolicy, roomTrace);
                    }
                    if (!alive) return false;
                    continue;
                }

                // AN EVENT'S SHELF (plan 3.4): the shop's own buying loop on
                // the stock in front, then back to the event. The shelf's
                // page offers Walk on first, so a first-available bot leaves
                // after one visit rather than looking again forever.
                if (RunOrchestrator.EventShelfPending)
                {
                    ShopAtTheStockInFront(seed, save, runPolicy, roomTrace);
                    RunOrchestrator.LeaveShelf();
                    continue;
                }

                var view = RunOrchestrator.CurrentEvent();
                if (view == null) break;

                if (view.Concluded)
                {
                    RunOrchestrator.LeaveEvent();
                    break;
                }

                var open = PreferredChoice(view);
                if (open == null)
                {
                    // The build refuses a page with no unconditional choice,
                    // so this is content the resolver should have stopped.
                    result.Hits.Add(new InvariantHit("EventTrapped",
                        $"event '{view.EventId}' page '{view.PageId}' offered no available choice"));
                    RunOrchestrator.LeaveEvent();
                    break;
                }

                var outcome = RunOrchestrator.ChooseEventOption(open.Index, view.PageId);
                if (!outcome.WasApplied)
                {
                    result.Hits.Add(new InvariantHit("EventChoiceRefused",
                        $"event '{view.EventId}' refused choice {open.Index} ({outcome.Reason}) that the gate offered"));
                    RunOrchestrator.LeaveEvent();
                    break;
                }

                if (outcome.Closed) break;
            }

            if (RunOrchestrator.EventIsOpen)
            {
                result.Hits.Add(new InvariantHit("EventChoiceCap",
                    $"still in an event after {MaxEventChoices} choices; left it"));
                RunOrchestrator.LeaveEvent();
            }

            if (!RunManager.Run.clearedNodeIds.Contains(node.Id))
            {
                result.Hits.Add(new InvariantHit("EventRoomNotCleared",
                    $"node {node.Id} was still uncleared after its event closed"));
            }

            return true;
        }

        // -EventChoice's text when an open choice on this page contains it,
        // else the first available one.
        private static EventChoiceView PreferredChoice(EventView view)
        {
            string wanted = _probe?.EventChoiceText;
            if (!string.IsNullOrEmpty(wanted))
            {
                var match = view.Choices.FirstOrDefault(c => c.Visible && c.Enabled && c.Text != null
                    && c.Text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null) return match;
            }

            return view.Choices.FirstOrDefault(c => c.Visible && c.Enabled);
        }

        // ---- the forced event (BotProbe) ---------------------------------------------

        private static bool ForcedEventDueOnThisFloor() =>
            _probe != null && !string.IsNullOrEmpty(_probe.ForceEventId) && _probe.ForceEventFloor > 0
            && !_forcedEventDone && RunManager.Run.floor == _probe.ForceEventFloor;

        private static bool CanHostForcedEvent(DescentNode node) =>
            node != null && !RunOrchestrator.IsFight(node.Type) && node.Type != RoomType.Shop;

        // An Event node first, then any other node that can host the event;
        // null when this column is all fights and shops (the policy chooses,
        // and the next column is asked again).
        private static DescentNode ForcedEventNode(IReadOnlyList<DescentNode> offered) =>
            offered.FirstOrDefault(n => n.Type == RoomType.Event)
            ?? offered.FirstOrDefault(CanHostForcedEvent);

        // ArriveAt's event branch with the forced id in place of the roll, on
        // any non-fight, non-shop node: the room IS the event, so a Rest or a
        // Treasure node pays nothing of its own here.
        private static RunOrchestrator.Arrival ArriveAtForcedEvent(DescentNode node, RoomTrace roomTrace, BotRunResult result)
        {
            if (!RunManager.MoveTo(node.Id)) return RunOrchestrator.Arrival.Refused;

            RoomResolver.Reset();
            _forcedEventDone = true;

            if (!RunOrchestrator.OpenEventForDebug(_probe.ForceEventId))
            {
                result.Hits.Add(new InvariantHit("ForcedEventMissing",
                    $"-ForceEvent '{_probe.ForceEventId}' is not in the built content"));
                RoomResolver.Resolve(RunManager.Run, node.Type);
                RunManager.ClearCurrentRoom();
                return RunOrchestrator.Arrival.Resolved;
            }

            roomTrace.EventForced = true;
            return RunOrchestrator.Arrival.Event;
        }

        private static void GrantRelics(BotRunResult result)
        {
            if (_probe == null || _probe.GrantRelicIds.Count == 0) return;

            var run = RunManager.Run;
            run.relicIds ??= new List<string>();
            foreach (string id in _probe.GrantRelicIds)
            {
                if (ContentDatabase.GetRelic(id) == null)
                {
                    result.Hits.Add(new InvariantHit("GrantedRelicMissing", $"-GrantRelic '{id}' is not in the built content"));
                    continue;
                }

                if (!run.relicIds.Contains(id)) run.relicIds.Add(id);
            }

            SaveSlotManager.SaveCurrent();
        }

        // Each book bought for nothing and learned into a free slot, the two
        // steps a player takes (shop, then the dossier) through the same
        // RunOrchestrator.LearnSpell the bot's own assignment pass uses.
        private static void GrantBooks(SaveData save, BotRunResult result)
        {
            if (_probe == null || _probe.GrantBookIds.Count == 0) return;

            var run = RunManager.Run;
            run.unassignedSpellBooks ??= new List<string>();
            foreach (string id in _probe.GrantBookIds)
            {
                var skill = ContentDatabase.GetSkill(id);
                if (skill == null)
                {
                    result.Hits.Add(new InvariantHit("GrantedBookMissing", $"-GrantBook '{id}' is not in the built content"));
                    continue;
                }

                string owner = skill.Data.CharacterId;
                var learner = save.ActiveSquad().FirstOrDefault(c =>
                    c != null && (string.IsNullOrEmpty(owner) || c.definitionId == owner)
                    && RunOrchestrator.CanLearn(c.definitionId) >= 0);
                if (learner == null)
                {
                    result.Hits.Add(new InvariantHit("GrantedBookUnlearned",
                        $"-GrantBook '{id}': no fielded character {owner} has a free slot"));
                    continue;
                }

                run.unassignedSpellBooks.Add(id);
                var learned = RunOrchestrator.LearnSpell(learner.definitionId, id,
                    RunOrchestrator.CanLearn(learner.definitionId));
                if (!learned.Applied)
                {
                    run.unassignedSpellBooks.Remove(id);
                    result.Hits.Add(new InvariantHit("GrantedBookUnlearned", $"-GrantBook '{id}': {learned.Outcome}"));
                }
            }

            SaveSlotManager.SaveCurrent();
        }

        // Each talent and its prerequisites, transitively, kindled on the
        // character it belongs to -- no embers spent, no cap asked. A shared
        // talent (no owner) goes to every fielded character.
        private static void GrantTalents(SaveData save, BotRunResult result)
        {
            if (_probe == null || _probe.GrantTalentIds.Count == 0) return;

            var pending = new Stack<string>(_probe.GrantTalentIds);
            var seen = new HashSet<string>();
            while (pending.Count > 0)
            {
                string id = pending.Pop();
                if (!seen.Add(id)) continue;

                var talent = ContentDatabase.GetTalent(id);
                if (talent == null)
                {
                    result.Hits.Add(new InvariantHit("GrantedTalentMissing", $"-GrantTalent '{id}' is not in the built content"));
                    continue;
                }

                foreach (var character in save.ActiveSquad())
                {
                    if (character == null) continue;
                    if (!talent.Data.IsShared && talent.Data.CharacterId != character.definitionId) continue;
                    if (!character.unlockedTalentIds.Contains(id)) character.unlockedTalentIds.Add(id);
                }

                foreach (string before in talent.Data.Prerequisites) pending.Push(before);
            }

            SaveSlotManager.SaveCurrent();
        }

        // ---- the shop ------------------------------------------------------------

        // HOW MANY DECISIONS ONE VISIT MAY MAKE.
        //
        // A policy that never says leave is a hang, and there is no timeout
        // under BotRunDriver to catch one (docs/PLAN_SHOP.md 2g). The bound
        // itself is DERIVED from the shelf it bounds -- see
        // ShopStock.MaxChoicesPerVisit, which is where the arithmetic and the
        // one judgement in it (sell headroom) are written down.
        //
        // It used to be a hand-typed 12, justified against a six-card shelf:
        // "six cards plus three rerolls is nine, so hitting it is a finding
        // rather than a truncation". The shelf became ten cards on 2026-09-03
        // and the justification did not follow it, so ten buys plus three
        // rerolls plus the Leave -- fourteen, nothing unusual about it -- was
        // being truncated and the truncation recorded as a finding. Measured
        // over a 200-run batch that was 20.4% of GreedyDefensive's visits and
        // 31.4% of Lookahead2's.
        private const int MaxShopChoices = ShopStock.MaxChoicesPerVisit;

        // WHAT THE BOT DOES IN A SHOP: ASK THE POLICY UNTIL IT LEAVES.
        //
        // The view is rebuilt before EVERY call, not once per visit. A
        // purchase moves the purse and marks a card sold; a sale renumbers
        // the bag, because InventoryOps.TryRemoveAt drops an emptied stack
        // out of the list. A policy handed a stale view would be buying
        // against gold it had already spent and selling at indices that had
        // moved.
        //
        // THE VISIT ENDS ON ANY OF FOUR THINGS: leave, a refusal, the cap, or
        // an empty shelf. A refusal ends it because an archetype is allowed
        // to ask for something it cannot have but asking twice in a row is a
        // loop -- and the refusal is on the trace, so a policy that keeps
        // doing it is visible rather than merely slow.
        //
        // The shelf is read BEFORE LeaveShop, which clears it.
        private static void VisitShop(ulong seed, SaveData save, IRunPolicy runPolicy, RoomTrace roomTrace)
        {
            var run = RunManager.Run;

            roomTrace.PurchasesBySection = new int[ShopStock.SectionCount];
            roomTrace.RerollsBySection = new int[ShopStock.SectionCount];

            ShopAtTheStockInFront(seed, save, runPolicy, roomTrace);

            RunOrchestrator.LeaveShop();

            roomTrace.GoldOnLeave = run.gold;
            roomTrace.GoldSpent = roomTrace.GoldOnArrival - roomTrace.GoldOnLeave;
        }

        // THE BUYING LOOP, SHARED by the room shop (VisitShop) and an event's
        // merchant shelf (VisitEvent): it reads whatever stock is in front
        // (RunOrchestrator.CurrentShopStock) and buys through the same
        // orchestrator calls the screen uses. Leaving is the caller's, since
        // the two leave differently.
        private static void ShopAtTheStockInFront(ulong seed, SaveData save, IRunPolicy runPolicy, RoomTrace roomTrace)
        {
            var run = RunManager.Run;

            roomTrace.PurchasesBySection ??= new int[ShopStock.SectionCount];
            roomTrace.RerollsBySection ??= new int[ShopStock.SectionCount];

            int step = run.step;

            // ONE SCORE CACHE FOR THE WHOLE VISIT, not one ScoreOffer call per
            // (choice, shelf card) and per (choice, bag row). ScoreOffer's
            // result depends only on (itemId, plus) against the squad's
            // CURRENTLY WORN gear, and nothing in this loop ever changes what
            // is worn -- EquipPass only runs on the way out of a fight, never
            // mid-shop -- so the same card and the same bag stack scored the
            // same thing on choice 1 score the same thing on choice 12. Before
            // this cache, a visit with a dozen choices against a bag that had
            // grown to twenty-odd items over the run was rescoring the whole
            // shelf AND the whole bag from scratch on every single choice --
            // the measured cost of that was half the batch's wall clock
            // (BotPhase.Shop, ~85ms/visit against ~5ms for everything else a
            // visit does). A fresh dictionary per visit, not per batch: a
            // purchase or a level-up between VISITS can change what is worn,
            // and a stale score carried across visits would be wrong rather
            // than merely repeated.
            var scoreCache = new Dictionary<(string, int), float>();

            for (int choiceIndex = 0; choiceIndex < MaxShopChoices; choiceIndex++)
            {
                var shopView = ShopViewOf(save, runPolicy, scoreCache);
                if (shopView.Cards.Count == 0) break;

                var choice = runPolicy.ChooseShop(
                    shopView, ViewOf(save), StreamFor(seed, ShopStream, step, choiceIndex));

                if (choice.Kind == ShopChoiceKind.Leave)
                {
                    roomTrace.ShopChoices.Add(new ShopChoiceTrace { Kind = "Leave", Outcome = "Leave" });
                    break;
                }

                var result = Apply(choice);
                roomTrace.ShopChoices.Add(new ShopChoiceTrace
                {
                    Kind = choice.Kind.ToString(),
                    Section = choice.Section,
                    Index = choice.Index,
                    GoldDelta = result.GoldDelta,
                    Outcome = result.Outcome.ToString(),
                    Refusal = result.Reason.ToString(),
                });

                if (!result.Applied) break;

                if (choice.Kind == ShopChoiceKind.Reroll &&
                    choice.Section >= 0 && choice.Section < ShopStock.SectionCount)
                {
                    roomTrace.RerollsBySection[choice.Section]++;
                }

                // RECORDED, NOT SILENTLY TRUNCATED. A visit that ran out of
                // budget was cut off mid-decision, and a report that could
                // not tell that from a policy choosing to leave would count
                // the cut-off visit as a completed one.
                //
                // ASKED, NOT ASSUMED. This used to fire on the last allowed
                // choice, which marks Capped whenever the budget ran out --
                // including the case where the policy was about to say Leave
                // anyway and the visit was over. That made the rate an upper
                // bound with no way to read the true one off the trace. One
                // extra ChooseShop settles it: the answer is thrown away, the
                // stream it draws from is keyed by (seed, step, choiceIndex)
                // like every other, and it only happens on a visit that
                // reached the ceiling -- which, with the ceiling now derived
                // from the shelf, is close to never.
                if (choiceIndex == MaxShopChoices - 1)
                {
                    roomTrace.ShopChoices.Add(WouldHaveLeftAnyway(seed, save, runPolicy, scoreCache, step)
                        ? new ShopChoiceTrace { Kind = "Leave", Outcome = "Leave" }
                        : new ShopChoiceTrace { Kind = "Capped", Outcome = "Capped" });
                }
            }

            // COUNTED OFF THE SHELF, not off the choice list: `sold` is the
            // shelf's own record of what left it, so a purchase the
            // orchestrator applied and the trace mis-attributed cannot make
            // the two disagree.
            foreach (var entry in RunOrchestrator.CurrentShopStock)
            {
                if (entry == null || entry.noOffer) continue;

                roomTrace.ShopOffers.Add(new ShopOfferTrace
                {
                    Kind = entry.kind.ToString(),
                    ContentId = entry.contentId,
                    Price = entry.price,
                    Sold = entry.sold,
                });

                // A merchant's consumable section sits outside the room
                // shop's three; its purchases are on ShopOffers above.
                if (entry.sold && entry.section >= 0 && entry.section < roomTrace.PurchasesBySection.Length)
                {
                    roomTrace.PurchasesBySection[entry.section]++;
                }
            }
        }

        // WAS THE VISIT OVER, OR ONLY CUT OFF? The difference is the whole
        // value of the Capped marker, and the loop above cannot see it: a
        // budget that runs out on the same choice the policy would have
        // followed with Leave is not a truncation of anything.
        //
        // The answer is thrown away. Nothing here mutates the run -- the view
        // is rebuilt read-only and no choice is applied -- so a visit that
        // ends here ends exactly as it would have, and the only thing the
        // extra call buys is an honest label on the trace.
        private static bool WouldHaveLeftAnyway(
            ulong seed, SaveData save, IRunPolicy runPolicy,
            Dictionary<(string, int), float> scoreCache, int step)
        {
            var shopView = ShopViewOf(save, runPolicy, scoreCache);
            if (shopView.Cards.Count == 0) return true;

            var choice = runPolicy.ChooseShop(
                shopView, ViewOf(save), StreamFor(seed, ShopStream, step, MaxShopChoices));

            return choice.Kind == ShopChoiceKind.Leave;
        }

        // EVERY PENDING BOOK, RESOLVED THE SAME TURN (docs/PLAN_SHOP.md
        // §1g/§2g). Called after every room that can grow
        // run.unassignedSpellBooks -- a shop visit's purchases and a won
        // fight's drop both land there, and the bot has no dossier to walk
        // to separately, so it asks ChooseSpellAssignment for whatever is
        // still unplaced right where a human player's next visit to the
        // dossier would. The asymmetry is real and deliberate: a human's two
        // acts (buy, then place) can be separated by any number of rooms,
        // so the bot's own numbers should be read as "assigned as soon as
        // possible," not as a measurement of how promptly a player would
        // actually open the dossier.
        private static void ResolvePendingSpellAssignments(ulong seed, SaveData save, IRunPolicy runPolicy,
            RoomTrace roomTrace)
        {
            var run = RunManager.Run;
            if (run?.unassignedSpellBooks == null || run.unassignedSpellBooks.Count == 0)
            {
                if (roomTrace != null)
                {
                    roomTrace.LearnedSpellCountAfterRoom = run?.learnedSpells?.Count ?? 0;
                    roomTrace.UnassignedSpellBookCountAfterRoom = 0;
                }

                return;
            }

            // A snapshot, not a live read of run.unassignedSpellBooks -- the
            // loop below mutates that list through LearnSpell, and walking a
            // list while removing from it under a different name is the
            // usual way that goes wrong.
            var pending = new List<string>(run.unassignedSpellBooks);

            for (int i = 0; i < pending.Count; i++)
            {
                string skillId = pending[i];
                var view = SpellAssignmentViewOf(save, skillId);

                // Three real coordinates (step, node, which pending book),
                // not two packed into one -- the same F9 reasoning the real
                // game's own RngStreams.Derive third argument exists for,
                // even though this stream (109+) is bot-only and never
                // persisted: a packed coordinate is a bug waiting for the
                // day either half grows past its assumed bound, and the fix
                // costs nothing here either.
                var rng = new SeededRandom(RngStreams.Derive(seed, SpellAssignmentStream, run.step, run.currentNodeId, i));
                var choice = runPolicy.ChooseSpellAssignment(view, ViewOf(save), rng);

                var assignmentTrace = new SpellAssignmentTrace { SkillId = skillId };

                if (choice.Assign)
                {
                    var result = RunOrchestrator.LearnSpell(choice.CharacterId, skillId, choice.Slot);
                    assignmentTrace.Assigned = result.Applied;
                    assignmentTrace.CharacterId = choice.CharacterId;
                    assignmentTrace.Slot = choice.Slot;
                    assignmentTrace.Outcome = result.Outcome.ToString();
                }
                else
                {
                    assignmentTrace.Outcome = "Skip";
                }

                roomTrace?.SpellAssignments.Add(assignmentTrace);
            }

            if (roomTrace != null)
            {
                run = RunManager.Run;
                roomTrace.LearnedSpellCountAfterRoom = run?.learnedSpells?.Count ?? 0;
                roomTrace.UnassignedSpellBookCountAfterRoom = run?.unassignedSpellBooks?.Count ?? 0;
            }
        }

        // ONE PENDING BOOK, FLATTENED FOR THE POLICY -- every fielded
        // character, whether they already know it, and their lowest free
        // slot (-1 if all three are full). Mirrors ShopViewOf's own bargain:
        // the policy sees exactly what CanLearn/AlreadyKnows already answer,
        // never the raw learnedSpells list.
        private static SpellAssignmentView SpellAssignmentViewOf(SaveData save, string skillId)
        {
            var run = RunManager.Run;
            var squad = save?.ActiveSquad() ?? new List<Character>();
            var learned = run?.learnedSpells ?? new List<LearnedSpellEntry>();

            var candidates = new List<SpellCandidateView>();
            foreach (var character in squad)
            {
                if (character == null) continue;

                bool alreadyKnows = learned.Exists(e =>
                    e != null && e.characterId == character.definitionId && e.skillId == skillId);
                int freeSlot = RunOrchestrator.CanLearn(character.definitionId);

                candidates.Add(new SpellCandidateView(character.definitionId, alreadyKnows, freeSlot));
            }

            return new SpellAssignmentView(skillId, candidates);
        }

        // The one place a ShopChoice becomes a mutation. A switch rather than
        // four call sites, so "which orchestrator method does this kind mean"
        // is answered once.
        private static ShopResult Apply(ShopChoice choice)
        {
            switch (choice.Kind)
            {
                case ShopChoiceKind.BuyGear: return RunOrchestrator.BuyGear(choice.Index);
                case ShopChoiceKind.BuyConsumable: return RunOrchestrator.BuyConsumable(choice.Index);
                case ShopChoiceKind.BuyRelic: return RunOrchestrator.BuyRelic(choice.Index);
                case ShopChoiceKind.BuyBook: return RunOrchestrator.BuyBook(choice.Index);
                case ShopChoiceKind.Sell: return RunOrchestrator.Sell(choice.Index, choice.Quantity);
                case ShopChoiceKind.Reroll: return RunOrchestrator.RerollSection(choice.Section);
                default: return ShopResult.Refused(ShopRefusal.BadIndex);
            }
        }

        // THE SHELF AND THE BAG, FLATTENED, WITH CORE'S SCORES ATTACHED.
        //
        // Same bargain ViewOf makes and the same reason RunView.OfferScores
        // exists: Domain cannot resolve an item id into what wearing it would
        // do, so the score is computed HERE, by the same GearEvaluator the
        // reward offer uses, against the archetype's own weights -- and a
        // policy sees a number rather than a content database.
        private static ShopView ShopViewOf(
            SaveData save, IRunPolicy runPolicy, Dictionary<(string, int), float> scoreCache)
        {
            var run = RunManager.Run;
            var stock = RunOrchestrator.CurrentShopStock;
            if (run == null || stock.Count == 0)
            {
                return new ShopView(Array.Empty<ShopCardView>(), Array.Empty<int>(),
                    Array.Empty<int>(), Array.Empty<ShopBagRow>(), run?.gold ?? 0);
            }

            var weights = runPolicy.Gear;
            var cards = new List<ShopCardView>(stock.Count);

            foreach (var entry in stock)
            {
                if (entry == null) continue;

                // SCORED ONLY FOR GEAR, and only for a card still on sale:
                // ScoreOffer walks the paperdoll for every fielded character,
                // and paying that for a NO OFFER placeholder or a card
                // already bought buys nothing.
                float score = entry.kind == ShopEntryKind.Gear && !entry.noOffer && !entry.sold
                    ? ScoreCached(save, entry.contentId, entry.plus, weights, scoreCache)
                    : 0f;

                cards.Add(new ShopCardView(entry.section, entry.index, entry.kind, entry.contentId,
                    entry.price, entry.sold, entry.noOffer, run.gold >= entry.price, score));
            }

            // A MERCHANT SHELF neither rerolls nor buys from the bag (plan
            // 3.4), and says so in the view rather than by refusing: no
            // reroll anyone can afford, no bag rows to sell. A policy then
            // never asks for either, and its visit ends on its own Leave.
            bool merchant = RunOrchestrator.ShelfInFrontIsMerchant;

            var rerollPrices = new int[ShopStock.SectionCount];
            var rerollsUsed = new int[ShopStock.SectionCount];
            for (int section = 0; section < ShopStock.SectionCount; section++)
            {
                rerollPrices[section] = merchant ? int.MaxValue : RunOrchestrator.RerollPriceFor(section);
                rerollsUsed[section] = run.shopRerollsUsed != null && section < run.shopRerollsUsed.Length
                    ? run.shopRerollsUsed[section]
                    : 0;
            }

            var bag = merchant ? new List<ShopBagRow>() : BagRowsOf(save, weights, scoreCache);
            return new ShopView(cards, rerollPrices, rerollsUsed, bag, run.gold);
        }

        // itemId+plus is the whole of what GearEvaluator.ScoreOffer reads
        // besides `save` (worn gear, constant across a visit) and `weights`
        // (the archetype, constant for the run) -- see the cache's own note
        // at its one call site in VisitShop.
        private static float ScoreCached(
            SaveData save, string itemId, int plus, GearWeights weights,
            Dictionary<(string, int), float> cache)
        {
            var key = (itemId, plus);
            if (cache.TryGetValue(key, out float cached)) return cached;

            float score = GearEvaluator.ScoreOffer(save, itemId, plus, weights);
            cache[key] = score;
            return score;
        }

        private static List<ShopBagRow> BagRowsOf(
            SaveData save, GearWeights weights, Dictionary<(string, int), float> scoreCache)
        {
            var rows = new List<ShopBagRow>();
            var bag = save?.stockpiledItems;
            if (bag == null) return rows;

            var seenIds = new HashSet<string>();

            for (int i = 0; i < bag.Count; i++)
            {
                var entry = bag[i];
                if (entry == null || entry.count <= 0) continue;

                var definition = ContentDatabase.GetItem(entry.itemId);
                bool consumable = definition != null && definition.kind == ItemKind.Consumable;

                // DUPLICATE MEANS "an earlier row already holds this id",
                // which is the only duplicate a bag can show: two stacks of
                // the same item exist precisely because their plus, affixes
                // or rift tier differ, so the ids match and the copies do not.
                bool duplicate = !seenIds.Add(entry.itemId);

                float score = consumable
                    ? 0f
                    : ScoreCached(save, entry.itemId, entry.plus, weights, scoreCache);

                rows.Add(new ShopBagRow(i, entry.itemId, entry.count,
                    RunOrchestrator.SellPriceOf(entry), score, duplicate, consumable));
            }

            return rows;
        }

        // ---- the relic draft -----------------------------------------------------

        // ROUND ORDER COPIED FROM RelicDraftController.Commit, not reinvented:
        // roll, ask, TakeRelic (null for "nothing"), and only if something was
        // taken AND the track has earned another round, roll again and persist
        // BEFORE the next round -- which is what makes a mid-draft reload come
        // back to the round that was on screen. Declining ends the draft, and
        // so does an empty offer.
        private static void Draft(ulong seed, IRunPolicy runPolicy, BotRunResult result)
        {
            if (!RunOrchestrator.NeedsRelicDraft()) return;

            ulong draftSeed = RunManager.Run.runSeed;
            int round = 0;

            while (round++ < 16)
            {
                var offer = RunOrchestrator.RelicDraftOffer(draftSeed).ToList();
                if (offer.Count == 0) break;

                var trace = new RelicRound();
                trace.OfferIds.AddRange(offer.Select(o => o.Id));

                int index = runPolicy.ChooseRelic(offer, ViewOf(SaveSlotManager.CurrentSave),
                    StreamFor(seed, RelicStream, round, 0));

                bool took = index >= 0 && index < offer.Count;
                trace.PickedIndex = took ? index : -1;
                result.RelicRounds.Add(trace);

                RunOrchestrator.TakeRelic(took ? offer[index].Id : null);

                if (!took || !RunOrchestrator.DraftHasAnotherRound()) break;

                RunOrchestrator.PersistDraft();
            }

            RunOrchestrator.FinishDraft();
        }

        // ---- one fight room ------------------------------------------------------

        // Returns false when the run ended here.
        private static bool PlayTheFight(
            ulong seed, SaveData save, DescentNode node,
            IFightPolicy fightPolicy, IRunPolicy runPolicy,
            RoomTrace roomTrace, BotRunResult result)
        {
            var run = RunManager.Run;
            int step = run.step;

            // Read BEFORE the build and the settlement, which clears an event
            // fight's request: what a loss does and whether the fight pays
            // are the request's, not the room's.
            var request = RunOrchestrator.CurrentEncounterRequest();

            FightEncounterAdapter.BuiltFight built;
            using (BotPhaseTimers.Measure(BotPhase.BuildFight))
            {
                built = RunOrchestrator.BuildFight();
            }

            if (built == null)
            {
                result.Hits.Add(new InvariantHit("NoFightBuilt",
                    $"step {step} node {node.Id} ({node.Type}) built no fight; the run cannot continue"));
                return false;
            }

            var session = built.Session;
            var fightTrace = new FightTrace
            {
                Step = step,
                Floor = run.floor,
                RoomType = node.Type.ToString(),
                PartyHpIn = session.Encounter.PlayerParty.Sum(c => c.CurrentHealth),
            };
            fightTrace.EnemyIds.AddRange(session.Encounter.Enemies
                .Select(e => session.SourceFor(e)?.Source.Id ?? "")
                .Where(id => !string.IsNullOrEmpty(id)));

            int partyMaxHp = session.Encounter.PlayerParty.Sum(c => c.MaxHealth);

            // Shawn's own numbers (M8a): the Bell is his fight and the flock
            // is his relic. Read before the fight so a Transform's temporary
            // health is not in the "in" figure.
            fightTrace.EventId = request.IsEventFight ? request.EventId ?? "" : "";
            var shawn = session.Encounter.PlayerParty.FirstOrDefault(c => session.KitFor(c)?.Id == ShawnKitId);
            if (shawn != null)
            {
                fightTrace.ShawnHpPercentIn = HpPercent(shawn);
                fightTrace.ShawnSpeed = shawn.Speed;
            }

            // What the run is worth before the fight pays it, for the plan's
            // "gold, exp or level decreasing across a won fight" check.
            int goldBefore = run.gold;
            int levelBefore = save.ActiveSquad().Sum(c => c?.level ?? 0);
            int expBefore = save.ActiveSquad().Sum(c => c?.exp ?? 0);

            var fightRng = StreamFor(seed, FightStream, step, node.Id);

            // RunOrchestrator.SpendConsumable is the save-side half of drinking
            // a potion, which FightBootstrap.OnItemUsed does for the screen.
            // Without it FightRunner decrements only its local copy and the
            // bot's bag never empties.
            using (BotPhaseTimers.Measure(BotPhase.FightPlay))
            {
                result.Hits.AddRange(FightRunner.Play(
                    session, fightPolicy, RunOrchestrator.BuildSatchel(), fightRng, fightTrace,
                    RunOrchestrator.SpendConsumable,
                    _probe != null && _probe.NoTransform ? TransformUse.Never : TransformUse.WhenReady,
                    answerTelegraphs: _probe == null || !_probe.NoTelegraphAnswer));
            }

            fightTrace.EndReason = session.EndReason.ToString();
            fightTrace.Rounds = session.Round;
            if (shawn != null)
            {
                fightTrace.ShawnHpPercentOut = shawn.IsAlive ? HpPercent(shawn) : 0;
                fightTrace.ShawnTransformed = fightTrace.TransformedActors.Contains(shawn.Name);
                string shawnLedger = session.KitFor(shawn)?.Id;
                if (!string.IsNullOrEmpty(shawnLedger) && session.Ledger.Has(shawnLedger))
                {
                    fightTrace.ShawnDamageDealt = session.Ledger.For(shawnLedger).TotalDealt;
                }
            }
            fightTrace.FlockDamage = session.TollOfTheFlockDamageDealt;

            bool won = session.PlayerWon;
            fightTrace.Won = won;
            fightTrace.PartyHpOut = won ? session.Encounter.PlayerParty.Sum(c => c.CurrentHealth) : 0;

            // Off the fight's own ledger rather than re-summed from the turn
            // traces: the ledger is what the game itself reports, and a second
            // accounting here could disagree with the Reckoning's tally.
            var fieldedIds = session.Encounter.PlayerParty
                .Select(c => session.KitFor(c)?.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            foreach (var id in fieldedIds)
            {
                if (!session.Ledger.Has(id)) continue;
                var line = session.Ledger.For(id);
                fightTrace.DamageDealt += line.TotalDealt;
                fightTrace.DamageTaken += line.DamageTaken;
            }

            if (won && session.Payout.HasValue)
            {
                fightTrace.PayoutGold = session.Payout.Value.Gold;
                fightTrace.PayoutExp = session.Payout.Value.Experience;
            }

            result.Trace.Fights.Add(fightTrace);
            result.PartyMaxHpPerFight.Add(partyMaxHp);

            // CAPTURED BEFORE THE SETTLEMENT. A loss runs EndRun, which strips
            // the stockpile and replaces the snapshot; reading relics or
            // potions afterwards would report an empty bag for every dead run.
            CaptureWhatTheRunHolds(save, result);

            if (!won && request.EndsRunOnLoss)
            {
                result.Trace.DeathStep = step;
                result.Trace.DeathCause = DescribeTheDeath(fightTrace);
            }

            using (BotPhaseTimers.Measure(BotPhase.SettleFight))
            {
                RunOrchestrator.SettleFight(session, won);
            }

            if (!won)
            {
                if (request.EndsRunOnLoss)
                {
                    if (RunManager.HasRun)
                    {
                        result.Hits.Add(new InvariantHit("RunSurvivedALoss",
                            $"the party lost at step {step} and the run is still standing"));
                    }
                    return false;
                }

                // A `wake` event fight: the run goes on, and nothing was paid.
                if (!RunManager.HasRun)
                {
                    result.Hits.Add(new InvariantHit("WakeLossEndedTheRun",
                        $"event fight '{request.EventFight.Id}' was lost at step {step} under onLoss wake, " +
                        "and the run ended"));
                    return false;
                }

                CaptureWhatTheRunHolds(save, result);
                return true;
            }

            CheckRewardsDidNotGoBackwards(save, goldBefore, levelBefore, expBefore, step, result);

            // AFTER the settlement, because the settlement is what applies the
            // experience (RewardApplier, inside SettleFight) -- claiming before
            // it would collect the track through the level the party had
            // walking in and leave this fight's own level-up owed until the
            // next one.
            using (BotPhaseTimers.Measure(BotPhase.LevelUp))
            {
                CollectLevelUps(seed, save, runPolicy, step);
            }

            // A room fight clears its room and may open the next leg; an event
            // fight does neither (its event's Leave does), so this check is a
            // room's.
            if (!request.IsEventFight && RunManager.HasRun && RunManager.Choices().Count == 0)
            {
                result.Hits.Add(new InvariantHit("StuckAfterWin",
                    $"step {step} node {node.Id} was won and settled, and there is nowhere left to walk; " +
                    "SettleFight is supposed to have advanced the leg"));
            }

            // `pays: false` opens no Reckoning, so there is nothing to take.
            if (request.Pays)
            {
                using (BotPhaseTimers.Measure(BotPhase.Offers))
                {
                    Offer(seed, save, node, runPolicy, session, roomTrace, result);
                }
            }

            CaptureWhatTheRunHolds(save, result);
            return true;
        }

        // Whole percent of max, the Transform's temporary health excluded by
        // the clamp (it can push CurrentHealth over MaxHealth).
        private static int HpPercent(Domain.Combat.CombatantState c) =>
            c == null || c.MaxHealth <= 0 ? 0 : Math.Min(100, (int)Math.Round(100.0 * c.CurrentHealth / c.MaxHealth));

        // ---- wearing what the run has picked up -----------------------------------

        // ONE EQUIP PASS: every fielded character, every slot, the best legal
        // thing in the bag.
        //
        // The rules are GearEvaluator's, which are the character sheet's; this
        // only decides WHEN to ask, and the answer is the two moments a player
        // would -- on the way out of the hub, and after taking a reward. Not
        // after every room: nothing else puts anything in the bag, so a pass
        // anywhere else would be a scan that can only find what the last one
        // already took.
        //
        // NOTHING IS EVER UNEQUIPPED, and RunManager.EndRun clearing both gear
        // and bag is why -- see GearEvaluator.EquipBestGear's own note.
        private static List<GearEvaluator.Equipped> EquipPass(
            ulong seed, SaveData save, IRunPolicy runPolicy, int step, int node)
        {
            using (BotPhaseTimers.Measure(BotPhase.Equip))
            {
                var worn = GearEvaluator.EquipBestGear(save, runPolicy, StreamFor(seed, EquipStream, step, node));
                if (worn.Count > 0) SaveSlotManager.SaveCurrent();
                return worn;
            }
        }

        private static List<EquipTrace> EquipPassTraced(
            ulong seed, SaveData save, IRunPolicy runPolicy, int step, int node)
        {
            return EquipPass(seed, save, runPolicy, step, node)
                .Select(e => new EquipTrace
                {
                    CharacterId = e.CharacterId,
                    ItemId = e.ItemId,
                    Slot = e.Slot,
                    Plus = e.Plus,
                })
                .ToList();
        }

        // ---- collecting a level-up ---------------------------------------------------

        // WHAT A PLAYER DOES AFTER A WON FIGHT, which the bot did none of.
        //
        // RewardApplier deliberately stopped claiming the reward track (its own
        // header says so at length: collection is something the player does
        // now, from the reward-track screen). So a bot that only settled the
        // fight levelled up and then never collected the stat points, max
        // health and everything else those levels owed -- for a Late profile
        // mid-run that is dozens of unclaimed nodes, and the batch reported it
        // as the game being hard.
        //
        // ClaimTrackRewards is idempotent against its own watermark, so calling
        // it after every won fight costs nothing on the fights that crossed no
        // level. It moves the watermark, and stat points are the only grant
        // that is the WHOLE payment: they are handed over here because
        // the bot spends them below, and every other reward on the track --
        // max health, wool, elemental damage, the respec, the second life, a
        // book spell -- is summed live off claimedTrackLevel at its own read
        // site. So a bot that skipped this would not merely miss the points,
        // it would field a level-60 character with a level-1 everything.
        private static void CollectLevelUps(
            ulong seed, SaveData save, IRunPolicy runPolicy, int step)
        {
            if (save == null) return;

            bool changed = false;

            foreach (var character in save.ActiveSquad())
            {
                if (character == null) continue;

                if (ClaimKeepingTheCarriedFraction(character)) changed = true;

                int guard = 0;
                while (character.unspentStatPoints > 0 && guard++ < 1000)
                {
                    var options = GearEvaluator.StatOptionsFor(character);
                    int index = runPolicy.ChooseStat(
                        options, ViewOf(save), StreamFor(seed, LevelStream, step, guard));

                    var score = index >= 0 && index < options.Count
                        ? (Domain.Stats.AbilityScore)options[index].Score
                        : Domain.Stats.AbilityScores.All[0];

                    if (!character.Invest(score)) break;
                    changed = true;
                }
            }

            if (changed) SaveSlotManager.SaveCurrent();
        }

        // THE PAIR, IN THE ORDER RewardTrackController.Input.Claim TAKES IT.
        //
        // maxBefore is a photograph taken BEFORE the claim moves the watermark:
        // EffectiveStats sums the track's MaxHealth entries at levels <=
        // claimedTrackLevel, so reading it any later makes the scale a no-op.
        // Claim's own header says this at length; what it could not say is that
        // the bot is the second door onto the same claim and went through it
        // without the photograph. Every max-health node the bot collected left
        // the run holding the old absolute -- a permanently empty tail on the
        // bar, one per node, compounding across a descent, and the batch
        // reported the resulting weakness as the game being hard.
        //
        // A PRIVATE HELPER, NOT A SHARED ONE. "Move a maximum, keep the
        // fraction" has four callers now (EquipmentOps, TalentOps, the reward
        // track screen, and this) and wants to be one Core helper --
        // TalentOps.RescalingCarriedHealth is already exactly that shape. It is
        // not extracted here because TalentOps is being changed in the same
        // pass by another hand, and a cross-file extraction under two editors
        // at once is how a pair ends up applied zero times instead of four. A
        // later pass should fold all four into it.
        private static bool ClaimKeepingTheCarriedFraction(Character character)
        {
            if (character == null) return false;

            int maxBefore = ContentDatabase.EffectiveStats(character).maxHealth;

            if (!character.ClaimTrackRewards(RewardTracks.For(character), character.level)) return false;

            RunEncounter.ScaleCarriedHealth(character, maxBefore);
            return true;
        }

        // Test-only door onto the claim above, named ...ForTest per house
        // convention (FightBeatPlayer.WireStageForTest, CharacterVoice.
        // VoiceKeysForTest) because Core's InternalsVisibleTo names the Editor
        // assembly and nothing else. PlayRun is the only other way in and it
        // plays a whole descent, which cannot pin a literal at one claim.
        public static bool ClaimTrackForTest(Character character) =>
            ClaimKeepingTheCarriedFraction(character);

        // ---- the post-fight offer ------------------------------------------------

        private static void Offer(
            ulong seed, SaveData save, DescentNode node, IRunPolicy runPolicy,
            FightSession session, RoomTrace roomTrace, BotRunResult result)
        {
            // SEEDED, where the screen's is not. FightController.Input rolls
            // this from UnityEngine.Random on purpose, so a reroll cannot shift
            // the fight a replay would produce -- and RunOrchestrator.RollOffers
            // takes the randomness as a parameter precisely so the bot can hand
            // in a reproducible stream instead. Derived from (seed, step, node)
            // per plan §5; the bot never rerolls in v1, so there is no reroll
            // index in the key.
            var offerRng = StreamFor(seed, OfferStream, roomTrace.Step, node.Id);

            List<ItemOffer> offers;
            try
            {
                offers = RunOrchestrator.RollOffers(session, bound => offerRng.NextInt(0, bound));
            }
            catch (Exception e)
            {
                result.Hits.Add(new InvariantHit("Exception",
                    "rolling the offer: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace));
                return;
            }

            if (offers == null || offers.Count == 0)
            {
                result.Hits.Add(new InvariantHit("EmptyOffer",
                    $"the fight at step {roomTrace.Step} node {node.Id} was won and offered nothing"));
                return;
            }

            roomTrace.OfferItemIds.AddRange(offers.Select(o => o.ItemId));

            // WHAT DECIDED THIS OFFER, for the shop-balance batch
            // (docs/PLAN_BALANCE_BOT.md §7 extension): the exact axes
            // RarityTable/LootLadder rolled, and the favor/encounter-class
            // that fed them. session.EncounterClass is the SAME ranking
            // RunOrchestrator.RollOffers reads its own `encounter` from
            // (Boss > Elite > Normal, FightSession.EncounterClass) -- one
            // property now, not two copies of the same ternary that can
            // drift apart the way this one already had (it used to check
            // IsEliteFight only, so a boss room's trace read Normal/Elite
            // even after RollOffers itself was fixed to check Boss first).
            roomTrace.Favor = ItemOfferRoll.CurrentSquadFavor();
            roomTrace.EncounterClass = session.EncounterClass.ToString();
            roomTrace.Offers.AddRange(offers.Select(o => new OfferEntry
            {
                ItemId = o.ItemId,
                Tier = o.Tier,
                Plus = o.Plus,
                RiftTier = (int)o.RiftTier,
                ModifierCount = o.Modifiers?.Count ?? 0,
            }));

            // WHAT EACH ONE IS ACTUALLY WORTH, priced by this archetype's own
            // weights against the squad's current loadout. Without it
            // ChooseOffer ranks on tier-then-plus, which is blind to the one
            // thing that decides a pick: a tier-3 helm is worse than a tier-2
            // sword to a character with an empty hand, and worthless to one
            // already wearing a better helm.
            var scores = GearEvaluator.ScoreOffers(save, offers, runPolicy.Gear);

            int index = runPolicy.ChooseOffer(offers, ViewOf(save).WithOfferScores(scores), offerRng);
            if (index < 0 || index >= offers.Count) return;

            var taken = offers[index];
            RunOrchestrator.TakeOffer(save, taken);
            roomTrace.PickedIndex = index;

            // AND THEN PUT IT ON, if it beats what is worn. TakeOffer already
            // auto-equips into an EMPTY slot -- that is the game's own rule for
            // a reward that fills a hole -- but a slot already holding
            // something is deliberately left to the player, and this is the bot
            // being that player.
            roomTrace.Equipped.AddRange(
                EquipPassTraced(seed, save, runPolicy, roomTrace.Step, node.Id));

            // "A taken offer not landing in stockpiledItems" (plan §3), with the
            // one legitimate exception spelled out rather than assumed:
            // TakeOffer auto-equips a piece into an EMPTY slot, which takes it
            // straight back out of the bag. Checking the bag alone would fire
            // on the majority of early picks, which is how a bug row stops
            // being read.
            bool inTheBag = save.stockpiledItems.Any(e => e != null && e.itemId == taken.ItemId && e.count > 0);
            bool worn = save.roster.Any(c => c?.equipment != null && c.equipment.EquippedItemIds().Contains(taken.ItemId));

            if (!inTheBag && !worn)
            {
                result.Hits.Add(new InvariantHit("OfferPickNotStockpiled",
                    $"took '{taken.ItemId}' at step {roomTrace.Step} and it is neither in the stockpile nor worn"));
            }
        }

        // ---- run-level invariants -------------------------------------------------

        private static void CheckRewardsDidNotGoBackwards(
            SaveData save, int goldBefore, int levelBefore, int expBefore, int step, BotRunResult result)
        {
            var run = RunManager.Run;
            if (run == null) return;

            if (run.gold < goldBefore)
            {
                result.Hits.Add(new InvariantHit("RewardsDecreased",
                    $"run gold fell from {goldBefore} to {run.gold} across the won fight at step {step}"));
            }

            int levelAfter = save.ActiveSquad().Sum(c => c?.level ?? 0);
            int expAfter = save.ActiveSquad().Sum(c => c?.exp ?? 0);

            if (levelAfter < levelBefore)
            {
                result.Hits.Add(new InvariantHit("RewardsDecreased",
                    $"squad level total fell from {levelBefore} to {levelAfter} across the won fight at step {step}"));
            }

            // EXP ONLY WHEN THE LEVEL DID NOT MOVE. AddExperience spends the
            // bar on the way up, so a level-up legitimately leaves less exp
            // showing than went in -- asserting on the raw number would fire on
            // every levelling fight, which is the opposite of a bug.
            if (levelAfter == levelBefore && expAfter < expBefore)
            {
                result.Hits.Add(new InvariantHit("RewardsDecreased",
                    $"squad exp fell from {expBefore} to {expAfter} at an unchanged level across the won fight at step {step}"));
            }
        }

        // ---- reading the live state ------------------------------------------------

        private static void CaptureWhatTheRunHolds(SaveData save, BotRunResult result)
        {
            var run = RunManager.Run;

            result.RelicIdsAtEnd = new List<string>(run?.relicIds ?? new List<string>());

            result.TalentIdsAtEnd = save?.ActiveSquad()
                .Where(c => c?.unlockedTalentIds != null)
                .SelectMany(c => c.unlockedTalentIds)
                .Distinct()
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            // WHAT THE PARTY IS WEARING, captured on the same schedule and for
            // the same reason as the relics beside it: EndRun clears the
            // paperdoll, so the last live reading is the only one there will
            // ever be. Onto the trace rather than the result, because a loadout
            // is a decision the run made -- see RunTrace.WornAtDeath.
            result.Trace.WornAtDeath = save?.ActiveSquad()
                .Where(c => c?.equipment != null)
                .SelectMany(c => c.equipment.EquippedItemIds())
                .Where(id => !string.IsNullOrEmpty(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            result.Trace.LevelAtDeath = save?.ActiveSquad().Sum(c => c?.level ?? 0) ?? 0;

            result.ConsumablesLeftAtEnd = save?.stockpiledItems
                .Where(e => e != null && e.count > 0)
                .Where(e => ContentDatabase.GetItem(e.itemId)?.kind == ItemKind.Consumable)
                .Sum(e => e.count) ?? 0;
        }

        // What an IRunPolicy is allowed to see. Deliberately the same handful
        // of numbers a player has on the map screen and nothing more -- a
        // policy that could read the whole save would be measuring an
        // omniscience no archetype is supposed to have.
        private static RunView ViewOf(SaveData save)
        {
            var run = RunManager.Run;
            if (run == null || save == null) return new RunView(1f, 0, 1, 0, null);

            int max = 0;
            int current = 0;

            foreach (var character in save.ActiveSquad())
            {
                if (character == null) continue;

                int characterMax = ContentDatabase.EffectiveStats(character).maxHealth;
                max += characterMax;

                var entry = run.currentHealth?.FirstOrDefault(e => e != null && e.characterId == character.definitionId);

                // NO ENTRY MEANS UNTOUCHED, which is the same reading
                // RunEncounter.Roster.StartingHealth documents: the run only
                // ever records who is HURT, so a missing row is full health
                // rather than zero. Reading it as zero would tell every policy
                // the party was dead on the first step.
                current += entry != null ? Math.Min(entry.hp, characterMax) : characterMax;
            }

            float fraction = max > 0 ? (float)current / max : 1f;
            return new RunView(fraction, run.step, run.floor, run.gold, run.relicIds);
        }

        private static string DescribeTheDeath(FightTrace fight)
        {
            string enemies = fight.EnemyIds.Count > 0 ? string.Join(", ", fight.EnemyIds) : "nothing";
            return $"killed by {enemies} in a {fight.RoomType} room on floor {fight.Floor}";
        }

        private static SeededRandom StreamFor(ulong seed, uint stream, int a, int b) =>
            new SeededRandom(RngStreams.Derive(seed, stream, a, b));
    }
}
