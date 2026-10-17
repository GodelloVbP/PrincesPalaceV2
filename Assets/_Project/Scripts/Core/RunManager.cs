using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace
{
    // The descent, between scenes.
    //
    // Everything it holds already existed: RealmMap and its generator are
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
        // A seed, a saved profile and a generator reproduce a realm exactly; a
        // serialised map is a second representation that can drift from the
        // generator that made it, and would have to be migrated every time the
        // generator changed. Cached here because regenerating on every query
        // would be wasteful, keyed by every input the generator takes.
        private static RealmMap _map;
        private static ulong _mapSeed;
        private static int _mapStartStep = -1;

        // WHICH SLOT the seed belongs to. runSeed/legStartStep alone cannot tell two
        // slots apart -- a fresh run in slot B can roll the same seed and
        // step-0 leg a run already cached from slot A, and without this the
        // cache would hand slot B slot A's map. SaveSlotManager.CurrentSlot
        // already drops ITS OWN cache the moment the slot changes (its
        // setter, see SaveSlotManager.cs); this mirrors that by joining the
        // slot into the key here rather than hooking a second place to clear
        // on the same event.
        private static int _mapSlot = -1;

        // THE PROFILE the realm was generated from, by reference. A reloaded
        // save brings a new profile object and so one regeneration; anything
        // that replaces the saved profile (RealmOpening.Open) does the same.
        // restBeforeBoss lives inside it (RealmProfile.restBeforeBoss).
        private static RealmProfile _mapProfile;

        public static bool HasRun => Save != null && Save.activeRun != null && Save.activeRun.hasRun;

        // THE ONE DEFINITION OF "a descent is under way", forwarded from the
        // snapshot that owns it (RunSnapshot.DescentIsUnderWay).
        //
        // Distinct from HasRun, which is true from the moment the relic draft
        // is rolled -- in the hub, before anything has been walked. Anything
        // asking "is the party down there" asks this; anything asking "is
        // there a snapshot to read" asks HasRun.
        public static bool DescentIsUnderWay => Run?.DescentIsUnderWay ?? false;

        // Settles a run found in a slot the player has just opened.
        //
        // A RUN DOES NOT SURVIVE THE PROCESS. Leaving a descent, or anything
        // that does not continue the run, kills the run: every deliberate exit
        // calls EndRun (the map's abandon button, defeat, the hub's title
        // button, the main menu's quit). A crash, an alt-F4 and a power cut call
        // nothing, so a run found in a save being opened belonged to a session
        // that is over. It is SETTLED rather than discarded -- EndRun pays out
        // what the run earned, so a player whose game crashed keeps the embers
        // their bosses paid for. Deliberately not a resume: the run-scoped
        // prunes in SaveData.Reconcile are unreachable, because a run is
        // settled before Reconcile's output can be read.
        //
        // Called by SaveSlotManager.EnterSlot, the one place a slot is chosen
        // (the slot list and the main menu's Continue alike), AFTER
        // CurrentSlot is set, which is what reaches every slot rather than only
        // slot 0. There is no boot check: a BeforeSceneLoad hook would run with
        // CurrentSlot still at its default 0 and could only ever see slot 0.
        // Not hung off SaveSlotManager.CurrentSlot's setter either: a plain
        // accessor used freely by tests and the label refresh must not
        // silently end runs and write the disk.
        public static void SettleOnOpening()
        {
            if (HasRun) EndRun();
        }

        public static RunSnapshot Run => Save?.activeRun;

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // Regenerates (or returns) the current realm's map. A snapshot whose
        // realm was never opened (one built in memory rather than through
        // StartRun) is opened here, unsaved; the next write keeps it.
        public static RealmMap Map
        {
            get
            {
                var run = Run;
                if (run == null || !run.hasRun) return null;

                if (run.realmProfile == null || run.realmOpenedAt != run.legStartStep)
                {
                    Remember(run, RealmOpening.Open(Save, run));
                }
                else if (_map == null || _mapSeed != run.runSeed || _mapStartStep != run.legStartStep
                         || !ReferenceEquals(_mapProfile, run.realmProfile) || _mapSlot != SaveSlotManager.CurrentSlot)
                {
                    Remember(run, RealmOpening.MapOf(run));
                }

                return _map;
            }
        }

        private static void Remember(RunSnapshot run, RealmMap map)
        {
            _map = map;
            _mapSeed = run.runSeed;
            _mapStartStep = run.legStartStep;
            _mapProfile = run.realmProfile;
            _mapSlot = SaveSlotManager.CurrentSlot;
        }

        public static RealmNode CurrentNode
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
            Remember(save.activeRun, RealmOpening.Open(save, save.activeRun));

            // Entry is not a room to fight -- it is where the party is standing
            // before choosing. Set explicitly rather than left at -1 so every
            // later query has a real node to reason from.
            var entry = Map?.Entry;
            if (entry != null) save.activeRun.currentNodeId = entry.Id;

            Persist();
        }

        // Ends the run AND closes its books, in that order, in one call.
        //
        // Settling here rather than leaving it to each caller means every
        // caller -- a defeat and an abandon from the map alike -- pays out
        // every ember its bosses earned and credits every room it cleared,
        // rather than the run being silently discarded unpaid.
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
            // the draft is the same thing the next time that slot is opened,
            // through SettleOnOpening.
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

            // AND THE PACK: stockpiledItems is "the single live inventory
            // for now" -- FightBootstrap's own words -- and it is what the
            // pack draws, what the fight satchel is built from, and what the
            // Reckoning drops loot into. RunSnapshot.inventory is vestigial:
            // one stats readout reads it and nothing ever puts loot there.
            // Clearing only RunSnapshot would leave the run's actual haul
            // sitting on the PROFILE, surviving every death and abandon.
            save.stockpiledItems?.Clear();

            save.activeRun = new RunSnapshot { hasRun = false };
            Forget();
            Persist();
            return settlement;
        }

        // ---- moving through it ----------------------------------------------------

        // Where the party may go next. Empty at the boss, which is what ends a
        // realm rather than a special case anywhere else.
        public static IReadOnlyList<RealmNode> Choices()
        {
            var map = Map;
            var current = CurrentNode;
            if (map == null || current == null) return new List<RealmNode>();

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

            // Read before the party moves: walking past a pocket is a fact of
            // the edge, not of the node arrived at.
            bool skipsPocket = map.PocketSkippedBy(current.Id, nodeId) != null;

            run.currentNodeId = nodeId;

            // ABSOLUTE, and Offset is not. A node's Offset is its place on the
            // realm's spine, 0..8 (pocket rooms take their rejoin offset), and
            // `step` is the run's absolute difficulty clock: AdvanceLeg writes
            // legStartStep into it, the curve compounds per step, and RunDepth
            // reads it (docs/PLAN_RUN_MAP.md section 3).
            run.step = run.legStartStep + target.Offset;

            // Rooms, not steps: a pocket's rooms share their rejoin step.
            run.roomsWalked++;

            // THE SKIP HEAL (plan section 3): every living member, once per
            // pocket, at the moment of passing it. Before the write below, so
            // the heal and the move land together.
            if (skipsPocket) RunEncounter.HealLivingPartyByPercent(run, RealmMap.SkipHealPercent);

            // AND THE WATERMARK MOVES WITH IT, here, because this is the one
            // place a step advances.
            //
            // deepestStep is "the furthest step reached ... kept separately so
            // the summary can say how deep they got rather than where they
            // happened to die", so every arrival that advances a step writes
            // it here -- treasure, shop, rest and event rooms never reach
            // RunLedger.RecordRoom, whose only caller is a settled fight.
            //
            // RecordRoom's raise stays as the belt to this brace: it costs one
            // comparison and covers a fight that settles at a step this method
            // did not write.
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

        // The realm is over when there is nowhere left to go: the party stands
        // on the heart, whose boss is beaten. A REALM IS NOT A RUN -- this
        // means "open the next realm", never "the run is over".
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

            run.legStartStep += RealmMap.SpineLength;
            run.step = run.legStartStep;

            // The floor field was written once, as 1, when the run began, and
            // never again -- so every lintel, every reward line and every enemy
            // band that read it saw floor 1 for the whole descent. Derived from
            // the leg boundary rather than incremented here, so the two cannot
            // drift apart the next time one of them moves.
            run.floor = RunDepth.FloorFor(run.legStartStep);
            run.currentNodeId = -1;
            run.clearedNodeIds.Clear();
            run.slainAnchorIds ??= new List<int>();
            run.slainAnchorIds.Clear();

            // THE PARTY CATCHES ITS BREATH BETWEEN LEGS.
            //
            // Health carries from room to room and nothing else restores it:
            // rest rooms are weight 6 of 90, so a seven-room leg expects 0.47 of
            // one and 62% of legs contain none. Without this the party crosses
            // into the next leg on whatever the elite left them, against enemies
            // at 1.95x health and 1.58x attack.
            //
            // A realm is the natural place: it always ends on a boss, so this
            // reads as the beat after a set piece rather than as a free heal in
            // the middle of one. The reward track's level 30 is untouched -- it
            // places a rest on the step BEFORE a boss, and this fires after.
            RunEncounter.HealPartyToFull(run);

            // Opened AFTER the heal: strength weighs the party standing at the
            // door of the new realm.
            Forget();
            Remember(run, RealmOpening.Open(Save, run));

            var entry = Map?.Entry;
            if (entry != null) run.currentNodeId = entry.Id;

            // THE SECOND WRITE OF THIS LEG, and deliberately not avoided.
            // HealPartyToFull persists on its own because a rest room reaches
            // it through RoomResolver, which writes nothing -- the rule across
            // this seam is that the mutator owns the write (RunEncounter.
            // HealPartyToFull says so at length). This Persist is for the new
            // leg and the new entry node, which the heal knows nothing about.
            // Two full writes at roughly 5.7ms is the price of callers not
            // having to know whether their callee already wrote.
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
        // Crediting `goldEarned` only in RunLedger.RecordRoom (a settled
        // FIGHT) would miss the other two gold sources: treasure rooms pay
        // 15-30 through here (RoomResolver), and a shop sale pays straight
        // onto run.gold -- and Run statistics shows held and earned side by
        // side precisely because "held and earned are different numbers as
        // soon as anything is spent, and a shop exists" (RunStatRows), a
        // framing that only holds while earned >= held.
        //
        // HELD AND EARNED MOVE TOGETHER HERE and diverge only through spending,
        // which is the whole distinction: spending touches run.gold and never
        // this method.
        public static void BankPayout(int gold)
        {
            if (CreditPayout(gold)) Persist();
        }

        // BankPayout's rule without its write, for a caller that applies
        // several changes and persists ONCE at the end -- an event choice
        // (RunOrchestrator.ChooseEventOption, plan contract 10). One home for
        // "held and earned move together"; the write is the only difference.
        // Returns whether anything moved.
        internal static bool CreditPayout(int gold)
        {
            var run = Run;
            if (run == null || gold <= 0) return false;

            run.gold += gold;
            run.goldEarned += gold;
            return true;
        }

        // ---- persistence -------------------------------------------------------------

        private static void Persist()
        {
            if (Save != null) SaveSlotManager.SaveCurrent();
        }

        // Drops the cached map so the next query rebuilds it. Called whenever
        // the seed or the realm changes.
        private static void Forget()
        {
            _map = null;
            _mapSeed = 0;
            _mapStartStep = -1;
            _mapProfile = null;
            _mapSlot = -1;
        }

        // For tests, which run many runs in one process.
        public static void ResetForTests() => Forget();
    }
}
