using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
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

        // Name -> policy, in one place, because three separate things need the
        // same mapping (the batch runner's -botArchetypes, the smoke test, and
        // the report's archetypeGap) and a fourth spelling of it is a fourth
        // thing that can disagree.
        //
        // A single object implements BOTH interfaces per archetype, which is
        // the shape RandomLegalPolicy/GreedyAggressivePolicy already have -- a
        // fight brain and a map brain that disagreed about what the archetype
        // is would make the whole comparison meaningless.
        public static readonly IReadOnlyList<string> Archetypes =
            new[] { "RandomLegal", "GreedyAggressive" };

        private static object PolicyFor(string archetype)
        {
            switch (archetype)
            {
                case "RandomLegal": return new RandomLegalPolicy();
                case "GreedyAggressive": return new GreedyAggressivePolicy();
                default: return null;
            }
        }

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

        // ---- the run -------------------------------------------------------------

        public static BotRunResult PlayRun(ulong seed, string archetype, string profile, int depthCapSteps)
        {
            var result = new BotRunResult();
            result.Trace.Seed = seed;
            result.Trace.Archetype = archetype;
            result.Trace.Profile = profile;

            var started = DateTime.UtcNow;
            string root = Path.Combine(Path.GetTempPath(), "pp-bot-" + Guid.NewGuid().ToString("N"));

            try
            {
                // THE SAME HARNESS FightSettlementTests USES, for the same
                // reasons: a save root nothing else can see (so a batch cannot
                // touch the player's own save, and two runs cannot leak state
                // through one), a Navigation that goes nowhere (Go() would try
                // to load a scene from a process that has none open), and a
                // RunManager whose cached map is dropped between runs.
                Directory.CreateDirectory(root);
                SaveSystem.RootOverride = root;
                SaveSlotManager.CurrentSlot = 0;
                SaveSlotManager.Forget();
                RunManager.ResetForTests();
                RoomResolver.Reset();
                Navigation.LoadOverride = _ => { };

                PlayOneRun(seed, archetype, profile, depthCapSteps, result);
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
                Navigation.Reset();
                SaveSystem.RootOverride = null;
                SaveSlotManager.Forget();
                RunManager.ResetForTests();
                RoomResolver.Reset();

                try
                {
                    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
                }
                catch (IOException)
                {
                    // A temp directory that will not delete is litter, not a
                    // finding -- it must not turn a clean run into a bug row.
                }
            }

            result.ElapsedMs = (DateTime.UtcNow - started).TotalMilliseconds;
            return result;
        }

        private static void PlayOneRun(
            ulong seed, string archetype, string profile, int depthCapSteps, BotRunResult result)
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

            var save = ProfilePresets.Build(profile);
            if (save == null)
            {
                result.Hits.Add(new InvariantHit("NoSave", "the profile built no save"));
                return;
            }

            RunOrchestrator.StartRun(seed);
            if (!RunManager.HasRun)
            {
                result.Hits.Add(new InvariantHit("NoRun", "StartRun left no run standing"));
                return;
            }

            Draft(seed, runPolicy, result);

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

                var node = runPolicy.ChooseNode(choices, ViewOf(save), StreamFor(seed, NodeStream, RunManager.Run.step, rooms));
                if (node == null)
                {
                    result.Hits.Add(new InvariantHit("NoNodeChosen", $"the policy chose nothing from {choices.Count} options"));
                    break;
                }

                var roomTrace = new RoomTrace
                {
                    Step = RunManager.Run.step,
                    NodeId = node.Id,
                    RoomType = node.Type.ToString(),
                    PickedIndex = -1,
                };

                bool isFight = RunOrchestrator.IsFight(node.Type);
                var arrival = RunOrchestrator.ArriveAt(node);

                if (arrival == RunOrchestrator.Arrival.Refused)
                {
                    result.Hits.Add(new InvariantHit("ArrivalRefused",
                        $"node {node.Id} ({node.Type}) was offered by Choices() and refused by MoveTo"));
                    break;
                }

                // MoveTo writes the step, so the room's own step is only right
                // once arrival has happened.
                roomTrace.Step = RunManager.Run.step;

                if (!isFight)
                {
                    result.Trace.Rooms.Add(roomTrace);
                    CaptureWhatTheRunHolds(save, result);
                    continue;
                }

                bool alive = PlayTheFight(seed, save, node, fightPolicy, runPolicy, roomTrace, result);
                result.Trace.Rooms.Add(roomTrace);

                if (!alive) return;
            }
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

            var built = RunOrchestrator.BuildFight();
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
            result.Hits.AddRange(FightRunner.Play(
                session, fightPolicy, RunOrchestrator.BuildSatchel(), fightRng, fightTrace,
                RunOrchestrator.SpendConsumable));

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

            if (!won)
            {
                result.Trace.DeathStep = step;
                result.Trace.DeathCause = DescribeTheDeath(fightTrace);
            }

            RunOrchestrator.SettleFight(session, won);

            if (!won)
            {
                if (RunManager.HasRun)
                {
                    result.Hits.Add(new InvariantHit("RunSurvivedALoss",
                        $"the party lost at step {step} and the run is still standing"));
                }
                return false;
            }

            CheckRewardsDidNotGoBackwards(save, goldBefore, levelBefore, expBefore, step, result);

            if (RunManager.HasRun && RunManager.Choices().Count == 0)
            {
                result.Hits.Add(new InvariantHit("StuckAfterWin",
                    $"step {step} node {node.Id} was won and settled, and there is nowhere left to walk; " +
                    "SettleFight is supposed to have advanced the leg"));
            }

            Offer(seed, save, node, runPolicy, session, roomTrace, result);
            CaptureWhatTheRunHolds(save, result);
            return true;
        }

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

            int index = runPolicy.ChooseOffer(offers, ViewOf(save), offerRng);
            if (index < 0 || index >= offers.Count) return;

            var taken = offers[index];
            RunOrchestrator.TakeOffer(save, taken);
            roomTrace.PickedIndex = index;

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
