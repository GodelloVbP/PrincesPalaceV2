using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace
{
    // The descent, between scenes.
    //
    // Everything it holds already existed: DescentMap and its generator are
    // Domain, RunSnapshot is the save shape, DifficultyCurve scales the rooms.
    // What was missing is the thing that DRIVES them -- start a run, know which
    // room you are in, bank what a fight paid, move on.
    //
    // STATIC, not a MonoBehaviour. A run outlives every scene it passes through
    // (hub -> fight -> hub -> fight), and the alternative is a DontDestroyOnLoad
    // singleton whose lifetime nothing can test. The state lives in the SAVE, so
    // this is a thin accessor over SaveSystem rather than a second copy of the
    // run that could disagree with what is on disk.
    public static class RunManager
    {
        // The map is REGENERATED from the seed rather than serialised.
        //
        // A seed and a generator reproduce a map exactly; a serialised map is a
        // second representation that can drift from the generator that made it,
        // and would have to be migrated every time the generator changed. Cached
        // here because regenerating on every query would be wasteful, keyed by
        // the seed so a different run cannot read a stale one.
        private static DescentMap _map;
        private static ulong _mapSeed;
        private static int _mapStartStep = -1;

        public static bool HasRun => Save != null && Save.activeRun != null && Save.activeRun.hasRun;

        public static RunSnapshot Run => Save?.activeRun;

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // Regenerates (or returns) the current leg's map.
        public static DescentMap Map
        {
            get
            {
                var run = Run;
                if (run == null || !run.hasRun) return null;

                if (_map == null || _mapSeed != run.runSeed || _mapStartStep != run.legStartStep)
                {
                    _map = DescentMapGenerator.GenerateLeg(new SeededRandom(run.runSeed), run.legStartStep);
                    _mapSeed = run.runSeed;
                    _mapStartStep = run.legStartStep;
                }

                return _map;
            }
        }

        public static DescentNode CurrentNode
        {
            get
            {
                var map = Map;
                var run = Run;
                if (map == null || run == null) return null;

                return run.currentNodeId < 0 ? map.Entry : map.Node(run.currentNodeId);
            }
        }

        // ---- starting and ending ------------------------------------------------

        // A fresh seed for a new descent.
        //
        // Derived from the clock rather than from Random, so two runs started in
        // the same session cannot collide -- and stated here rather than at the
        // call site, so every entry point seeds the same way.
        public static ulong NewSeed() => (ulong)System.DateTime.UtcNow.Ticks;

        public static void StartRun(ulong seed)
        {
            var save = Save;
            if (save == null) return;

            // The last run's last room has nothing to say about this one. Only
            // entering a fight cleared this before, so a descent abandoned in a
            // treasure room opened its replacement still announcing the stash
            // from the run before it -- credited to a room the player had not
            // walked into yet.
            RoomResolver.Reset();

            save.activeRun = new RunSnapshot
            {
                hasRun = true,
                runSeed = seed,
                legStartStep = 0,
                step = 0,
                floor = 1,
                currentNodeId = -1,
            };

            Forget();

            // Entry is not a room to fight -- it is where the party is standing
            // before choosing. Set explicitly rather than left at -1 so every
            // later query has a real node to reason from.
            var entry = Map?.Entry;
            if (entry != null) save.activeRun.currentNodeId = entry.Id;

            Persist();
        }

        // Ends the run AND closes its books, in that order, in one call.
        //
        // Settling used to be the caller's job and there are two callers -- a
        // defeat and an abandon from the map. Only the defeat did it, so
        // walking away from a descent silently threw away every ember the
        // bosses in it had earned and every room it had cleared. Nothing
        // reported that, because discarding a snapshot looks exactly the same
        // whether or not anybody read it first.
        //
        // Folded in here so the ordering cannot be got wrong again: the
        // settlement is returned for whoever wants to describe it, and ignoring
        // the return value costs nothing.
        public static RunSettlement.Result EndRun()
        {
            var save = Save;
            if (save == null) return new RunSettlement.Result();

            var settlement = RunSettlement.Settle(save, save.activeRun);

            save.activeRun = new RunSnapshot { hasRun = false };
            Forget();
            Persist();
            return settlement;
        }

        // ---- moving through it ----------------------------------------------------

        // Where the party may go next. Empty at the boss, which is what ends a
        // leg rather than a special case anywhere else.
        public static IReadOnlyList<DescentNode> Choices()
        {
            var map = Map;
            var current = CurrentNode;
            if (map == null || current == null) return new List<DescentNode>();

            return map.ReachableFrom(current.Id)
                .Select(map.Node)
                .Where(n => n != null)
                .ToList();
        }

        // Moves to a node. Refuses anything not actually reachable rather than
        // trusting the caller -- the map is the authority on adjacency, and a UI
        // that offered a wrong choice would otherwise teleport the party.
        public static bool MoveTo(int nodeId)
        {
            var map = Map;
            var run = Run;
            var current = CurrentNode;
            if (map == null || run == null || current == null) return false;
            if (!map.CanMove(current.Id, nodeId)) return false;

            var target = map.Node(nodeId);
            if (target == null) return false;

            run.currentNodeId = nodeId;
            run.step = target.Depth;
            Persist();
            return true;
        }

        // Marks the room done. Idempotent: a room cleared twice is a room
        // cleared once, and the list is what the map screen greys out.
        public static void ClearCurrentRoom()
        {
            var run = Run;
            var current = CurrentNode;
            if (run == null || current == null) return;

            if (!run.clearedNodeIds.Contains(current.Id))
            {
                run.clearedNodeIds.Add(current.Id);
            }

            Persist();
        }

        // The leg is over when there is nowhere left to go.
        //
        // A LEG IS NOT A RUN. A leg is eight steps and ends in whatever the
        // curve forces there -- an elite at step 8, a boss at 16. Running out of
        // choices therefore means "start the next leg", not "the run is over",
        // and conflating the two would have ended every descent at the first
        // elite.
        public static bool LegIsOver() => Choices().Count == 0;

        // Opens the next leg, carrying the run's own seed forward.
        //
        // The SEED IS UNCHANGED and only the start step moves, which is what
        // makes a run's whole descent reproducible from one number -- a fresh
        // seed per leg would make resume a different dungeon below the join.
        public static void AdvanceLeg()
        {
            var run = Run;
            if (run == null || !run.hasRun) return;

            run.legStartStep += DescentMapGenerator.DefaultLegLength;
            run.step = run.legStartStep;
            run.currentNodeId = -1;
            run.clearedNodeIds.Clear();

            Forget();

            var entry = Map?.Entry;
            if (entry != null) run.currentNodeId = entry.Id;

            Persist();
        }

        // ---- what a fight pays ------------------------------------------------------

        // Banks a payout into the run.
        //
        // GOLD ONLY here; experience belongs to the characters and is applied
        // where they live. Kept separate because a run's gold is spent inside
        // the run and lost with it, while experience survives -- conflating them
        // is how a defeat would silently keep half its reward.
        public static void BankPayout(int gold)
        {
            var run = Run;
            if (run == null || gold <= 0) return;

            run.gold += gold;
            Persist();
        }

        // ---- persistence -------------------------------------------------------------

        private static void Persist()
        {
            if (Save != null) SaveSlotManager.SaveCurrent();
        }

        // Drops the cached map so the next query rebuilds it. Called whenever
        // the seed or the leg changes -- a cache keyed by two fields still has
        // to be invalidated when either moves.
        private static void Forget()
        {
            _map = null;
            _mapSeed = 0;
            _mapStartStep = -1;
        }

        // For tests, which run many runs in one process.
        public static void ResetForTests() => Forget();
    }
}
