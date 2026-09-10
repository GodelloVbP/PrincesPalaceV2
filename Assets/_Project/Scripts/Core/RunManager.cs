using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Domain.Dungeon;

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

        // THE ONE DEFINITION OF "a descent is under way", forwarded from the
        // snapshot that owns it (RunSnapshot.DescentIsUnderWay).
        //
        // Distinct from HasRun, which is true from the moment the relic draft
        // is rolled -- in the hub, before anything has been walked. Anything
        // asking "is the party down there" asks this; anything asking "is
        // there a snapshot to read" asks HasRun.
        public static bool DescentIsUnderWay => Run?.DescentIsUnderWay ?? false;

        // A RUN DOES NOT SURVIVE THE PROCESS.
        //
        // "Leaving a descent, or anything that does not continue the run, kills
        // the run -- no exceptions." Every deliberate exit calls EndRun on its
        // way out: the map's abandon button, defeat, the hub's title button and
        // the main menu's quit. A crash, an alt-F4 and a power cut call
        // nothing, and those are the exceptions the rule says do not exist.
        //
        // So a run found in the save at startup belonged to a session that is
        // over, and it is SETTLED rather than discarded -- EndRun is what pays
        // out what the run earned, and a player whose game crashed should keep
        // the embers their bosses paid for even though the descent is gone.
        //
        // Deliberately not a resume. Resuming is the other reading of the same
        // rule and it is not the one the author asked for.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SettleAnyRunAPreviousSessionLeftBehind()
        {
            // Before any scene, so nothing can read a run that is about to be
            // settled and act on it -- the same self-bootstrapping shape
            // GameSettings uses.
            //
            // THIS ONLY EVER SEES SLOT 0, and that is not a fault in it -- it
            // is what "before any scene" means. SaveSlotManager.CurrentSlot
            // starts at 0 and the player has not chosen yet, so a run left in
            // any other slot was invisible here and survived the process,
            // flatly against the rule this method exists to enforce. Quitting
            // mid-fight in slot 5 and coming back put you straight back into
            // the same fight, every time.
            //
            // SettleOnOpening below is the other half: the moment a slot is
            // actually opened is the moment its contents can be seen at all.
            if (HasRun) EndRun();
        }

        // Settles a run found in a slot the player has just opened.
        //
        // Same rule as the boot check and the same reasoning -- a run in a save
        // being opened for the first time this session belonged to a session
        // that is over, so it is SETTLED rather than discarded and the embers
        // its bosses paid for are kept. It is separate only because the boot
        // check cannot know which slot the player will pick.
        //
        // Called by SaveSlotController, which is the one place a slot is
        // chosen. Deliberately NOT hung off SaveSlotManager.CurrentSlot's
        // setter: that is a plain accessor used freely by tests and by the
        // label refresh, and a setter that silently ends runs and writes the
        // disk is exactly the kind of surprise its own header warns about.
        public static void SettleOnOpening()
        {
            if (HasRun) EndRun();
        }

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
                    _map = DescentMapGenerator.GenerateLegFor(run.runSeed, run.legStartStep,
                        restBeforeBoss: run.restBeforeBoss);
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
            // NOTHING TO END IS NOT THE SAME AS A RUN THAT EARNED NOTHING,
            // and everything below this line treats them alike.
            //
            // Five buttons call this and only two of them stand on a descent
            // (the map's abandon, the defeat screen). The main menu's Exit,
            // the hub's Main Menu button and both of ExitsController's call it
            // flat -- and the hub between runs is precisely where a player
            // equips what they just bought.
            //
            // RunSettlement guards on `run == null`, which never fires:
            // activeRun is always non-null, which is the whole reason hasRun
            // is an in-band flag (see RunSnapshot's header). So the settlement
            // ran against an empty snapshot, counted a lifetimeRunsEnded that
            // never happened, and the two clears below stripped every roster
            // character's equipment and the whole pack.
            //
            // HERE rather than at each caller: five call sites is five chances
            // to forget, and "gear does not survive a run" is meaningless
            // where there is no run for it to not survive.
            if (!HasRun) return new RunSettlement.Result();

            var save = Save;
            if (save == null) return new RunSettlement.Result();

            // AND A RUN THAT NEVER LEFT THE HUB IS THE SAME SHAPE OF NOTHING.
            //
            // The guard above asks HasRun, and HasRun is not the question. A
            // run exists from the moment the descent gate rolls the relic
            // draft, and that draft is offered IN THE HUB -- so pressing the
            // gate, seeing the draft, and then leaving to the title without
            // entering a single room reached everything below this line with
            // hasRun true and nothing walked. Every roster character's
            // equipment cleared, the whole stockpile cleared, and one more
            // lifetimeRunsEnded, for a descent that did not happen. Alt-F4 at
            // the draft is the same thing on the next boot, through
            // SettleAnyRunAPreviousSessionLeftBehind.
            //
            // DISCARDED RATHER THAN SETTLED, and that costs nothing: an
            // unwalked run has no rooms, no bosses, no ledger and no gold, so
            // Settle would fold zeroes and increment a counter that means "a
            // descent ended". The run itself still ends -- "leaving a descent,
            // or anything that does not continue the run, kills the run" holds
            // whether or not the run was worth closing books on.
            if (!save.activeRun.DescentIsUnderWay)
            {
                save.activeRun = new RunSnapshot { hasRun = false };
                Forget();
                Persist();
                return new RunSettlement.Result();
            }

            var settlement = RunSettlement.Settle(save, save.activeRun);

            // THE GEAR GOES WITH THE RUN, like the inventory always has.
            //
            // Inventory lives on RunSnapshot, so replacing the snapshot below
            // has always discarded it. Equipment lives on the CHARACTER, which
            // is profile-scoped -- so wearing an item was the one way to carry a
            // run's loot out of it, and a player who died fully kitted kept the
            // kit while a player who died holding the same items lost them.
            //
            // AFTER Settle, not before: settlement reads the run's own ledger
            // and pays out of it, and clearing gear first would be a change to
            // what a run is worth rather than to what survives it.
            //
            // Every roster character, not just the fielded squad. A benched
            // character cannot have picked anything up this run, so their slots
            // are already empty and clearing them costs nothing -- and "gear
            // does not survive a run" said with an exception is a rule somebody
            // has to remember.
            foreach (var character in save.roster ?? new List<Character>())
            {
                character?.equipment?.Clear();
            }

            // AND THE PACK, which is the part I got wrong the first time.
            //
            // This used to say inventory was already handled because it lives on
            // RunSnapshot, which the line below replaces. It does not.
            // stockpiledItems is "the single live inventory for now" --
            // FightBootstrap's own words -- and it is what the pack draws, what
            // the fight satchel is built from, and what the Reckoning drops loot
            // into. RunSnapshot.inventory is vestigial: one stats readout reads
            // it and nothing ever puts loot there.
            //
            // So the run's actual haul sat on the PROFILE and survived every
            // death and every abandon, which is what "my inventory in save slot
            // 5 is still not cleared" was.
            save.stockpiledItems?.Clear();

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

            // ABSOLUTE, and Depth is not.
            //
            // A node's Depth is its column WITHIN ITS LEG, 0..8 -- the
            // generator says so itself by passing `startStep + d` to
            // ForcedTypeAt while storing the bare `d`. But `step` is the run's
            // absolute counter: AdvanceLeg writes legStartStep into it, the
            // difficulty curve compounds per step, and RunDepth reads it.
            //
            // Writing the bare depth here made the two disagree the moment a
            // run left its first leg, and only then -- on leg 1 legStartStep is
            // 0 and the two are identical, which is why this survived. On leg 2
            // a save reads step 1 against legStartStep 8: the party is nine
            // rooms deep and the curve is scaling them for the second.
            run.step = run.legStartStep + target.Depth;

            // AND THE WATERMARK MOVES WITH IT, here, because this is the one
            // place a step advances.
            //
            // deepestStep's own header says it is "the furthest step reached ...
            // kept separately so the summary can say how deep they got rather
            // than where they happened to die". It could not say that: the only
            // writer was RunLedger.RecordRoom, whose only caller is a settled
            // FIGHT. Every non-fight arrival -- treasure, shop, rest, event --
            // goes ArriveAt -> RoomResolver.Resolve -> ClearCurrentRoom and
            // never touched it. So clearing the leg-1 boss at step 8 and then
            // walking a treasure, a shop and an event left the map header
            // reading 11 (UiStrings.MapDepth off run.step) while Run statistics
            // read 8, and abandoning there folded 8 into lifetimeDeepestStep.
            //
            // RecordRoom's raise is deliberately left in place as the belt to
            // this brace: it costs one comparison, and a fight that somehow
            // settles at a step this method did not write is exactly the case
            // worth keeping covered.
            if (run.step > run.deepestStep) run.deepestStep = run.step;

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
        // cadence forces there -- an elite mid-leg (step 4 within it), a boss
        // at the end (step 8). Running out of choices therefore means "start
        // the next leg", not "the run is over", and conflating the two would
        // have ended every descent at the first elite.
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

            // The floor field was written once, as 1, when the run began, and
            // never again -- so every lintel, every reward line and every enemy
            // band that read it saw floor 1 for the whole descent. Derived from
            // the leg boundary rather than incremented here, so the two cannot
            // drift apart the next time one of them moves.
            run.floor = RunDepth.FloorFor(run.legStartStep);
            run.currentNodeId = -1;
            run.clearedNodeIds.Clear();

            // THE PARTY CATCHES ITS BREATH BETWEEN LEGS.
            //
            // Reported as "floor 1 after the first elite it already becomes so
            // difficult", and the numbers say it is attrition rather than
            // scaling. Health carries from room to room and NOTHING restored
            // it: rest rooms are weight 6 of 90, so a seven-room leg expects
            // 0.47 of one and 62% of legs contain none at all. Levelling does
            // not heal, advancing a leg did not heal, and the shop that would
            // sell a potion is not built. So the party crossed into leg 2 on
            // whatever the elite left them, against enemies at 1.95x health and
            // 1.58x attack, and it never recovered.
            //
            // The elite itself was not the spike, which is worth saying because
            // it is where the report points: an elite room fields two enemies
            // where a normal room fields one or two. At the time this was
            // written, StatBlock.ScaledForElite -- the multiplier its own
            // comment said made elites "completely clap you" in playtesting --
            // HAD NO CALLER (AUDIT #54); FightEncounterAdapter.ToCombatant now
            // wires it in for elite encounters (Phase 5B), so an elite's own
            // stats are the deliberate spike they were always meant to be, and
            // what came after the elite was the separate, unrelated problem
            // this heal fixes.
            //
            // A leg is the natural place: it now always ends on a boss (with a
            // forced elite mid-leg), so this reads as the beat after a set
            // piece rather than as a free heal in the middle of one. The
            // reward track's level 30 is untouched -- it forces a rest on the
            // step BEFORE a boss, and this fires after.
            RunEncounter.HealPartyToFull(run);

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
        //
        // AND IT IS THE ONE PLACE A RUN EARNS GOLD, so it is the one place that
        // records having earned it.
        //
        // `goldEarned` used to be credited in RunLedger.RecordRoom, which only
        // a settled FIGHT reaches. Treasure rooms pay 15-30 through here
        // (RoomResolver) and a shop sale paid straight onto run.gold, so
        // neither counted -- and Run statistics shows held and earned side by
        // side precisely because "held and earned are different numbers as soon
        // as anything is spent, and a shop exists" (RunStatRows). That framing
        // only holds while earned >= held; with treasure uncounted, a run could
        // read 22 gold held against 0 earned.
        //
        // HELD AND EARNED MOVE TOGETHER HERE and diverge only through spending,
        // which is the whole distinction: spending touches run.gold and never
        // this method.
        public static void BankPayout(int gold)
        {
            var run = Run;
            if (run == null || gold <= 0) return;

            run.gold += gold;
            run.goldEarned += gold;
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
