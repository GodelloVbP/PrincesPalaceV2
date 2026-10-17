using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // SAVE 8 -> 9: a run saved on the old nine-column map, moved onto the
    // realm map of the same seed and leg (docs/PLAN_RUN_MAP.md section 8,
    // "SaveData v8 -> v9"). Old node ids mean nothing on the new map, so the
    // party is placed by OFFSET, the one coordinate both maps share:
    //
    // - o = step - legStartStep, clamped to 0..8 (saves from before the
    //   absolute-step fix can read below 0).
    // - A resolved room below the heart: the party stands on the spine node
    //   at o. Anything else -- an open fight, event or shop, or the boss room
    //   -- stands at o - 1 (at least 0), so the room at o is the next choice
    //   and the boss is never skipped.
    // - Every spine room up to the party is walked. Pockets behind it are
    //   walked past with no skip heal, and their anchors (and fork anchors
    //   behind it) are slain, paying nothing, so none joins the boss unearned.
    // - Open-room state and promised events (keyed on old node ids) are
    //   cleared; HP, gold and leg buffs are kept.
    //
    // The entry (offset 0) is never marked cleared: it is not a room, and a
    // cleared entry would read as a descent under way when nothing was walked.
    public static class RealmMigration
    {
        public static void ToRealmMap(SaveData save, RunSnapshot run)
        {
            if (run == null || !run.hasRun) return;

            int offset = Math.Max(0, Math.Min(RealmMap.SpineLength, run.step - run.legStartStep));
            bool resolved = run.clearedNodeIds != null && run.clearedNodeIds.Contains(run.currentNodeId);
            int placedOffset = resolved && offset < RealmMap.SpineLength ? offset : Math.Max(offset - 1, 0);

            var map = RealmOpening.Open(save, run);
            var placed = WalkedNodeAt(map, placedOffset);

            run.clearedNodeIds = new List<int>();
            for (int k = 1; k <= placedOffset; k++) run.clearedNodeIds.Add(WalkedNodeAt(map, k).Id);

            run.slainAnchorIds = map.Nodes
                .Where(n => n.IsAnchor && n.Offset <= placedOffset)
                .Select(n => n.Id)
                .ToList();

            run.currentNodeId = placed.Id;
            run.step = run.legStartStep + placed.Offset;
            run.roomsWalked = run.step;
            if (run.step > run.deepestStep) run.deepestStep = run.step;

            run.nodeEvents = new List<NodeEventEntry>();
            run.eventId = "";
            run.eventNodeId = -1;
            run.eventPageId = "";
            run.eventResult = "";
            run.eventResultEffects = new List<EventEffect>();
            run.pendingFight = "";
            run.pendingShelf = "";
            run.shopStock = new List<ShopStockEntry>();
            run.shopNodeId = -1;
        }

        // The node a route walked at `offset`: the entry, the spine room, or
        // of a fork the sibling that is not an anchor (an anchor on the walked
        // node would be an anchor fought and unpaid).
        private static RealmNode WalkedNodeAt(RealmMap map, int offset)
        {
            if (offset <= 0) return map.Entry;
            return map.SpineAt(offset).OrderBy(n => n.IsAnchor).ThenBy(n => n.Id).First();
        }
    }
}
