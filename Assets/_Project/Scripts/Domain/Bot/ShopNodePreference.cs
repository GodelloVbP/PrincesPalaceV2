using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Bot
{
    // WHETHER THE BATCH TAKES SHOPS AT ALL.
    //
    // The four archetypes' own ChooseNode rules were written before a shop
    // was a room worth entering, and none of them mentions one: a Shop node
    // falls through every case and is reached only when it is the sole
    // choice. So a batch run against the archetypes as written measures a
    // shop that is almost never visited, which answers neither "what does a
    // visit cost" nor "what does it buy" (docs/PLAN_SHOP.md §7.1 point 1).
    //
    // Rather than teach four policies about shops -- which would make the
    // comparison a comparison of four new heuristics -- the preference wraps
    // ChooseNode from OUTSIDE, at the driver, and is a batch-level switch.
    // Two batches over the same seeds, one in each mode, are then paired runs
    // that differ in exactly one decision.
    //
    // MATCHED BY CONSTRUCTION, not by care. BotRunDriver opens the node
    // stream at (seed, NodeStream, step, roomIndex) -- a position, not a
    // running generator -- so a mode that consumes a different number of
    // draws at one node cannot shift what any later node draws. The two
    // batches diverge only where the map choice itself differs.
    public enum ShopNodeMode
    {
        // Take a Shop node whenever one is offered (and the party is not hurt
        // enough that the archetype would have rested).
        WhenOffered,

        // Never even see one: Shop nodes are filtered out of the choice list
        // before the policy is asked. The fight-only baseline.
        Never,
    }

    public static class ShopNodePreference
    {
        public static ShopNodeMode Parse(string raw)
        {
            return string.Equals((raw ?? "").Trim(), "Never", System.StringComparison.OrdinalIgnoreCase)
                ? ShopNodeMode.Never
                : ShopNodeMode.WhenOffered;
        }

        public static string Name(ShopNodeMode mode) =>
            mode == ShopNodeMode.Never ? "Never" : "WhenOffered";

        // The choice list the policy is actually shown.
        //
        // Only `Never` narrows it, and it refuses to narrow it to nothing: a
        // column that offered only shops would otherwise leave the run with
        // no legal move, which is a hang dressed as a baseline. In that case
        // the shop is taken, and the pairing for that seed is honestly
        // broken rather than silently.
        public static IReadOnlyList<DescentNode> ChoicesFor(
            ShopNodeMode mode, IReadOnlyList<DescentNode> choices)
        {
            if (mode != ShopNodeMode.Never || choices == null) return choices;

            var withoutShops = choices.Where(n => n != null && n.Type != RoomType.Shop).ToList();
            return withoutShops.Count > 0 ? (IReadOnlyList<DescentNode>)withoutShops : choices;
        }

        // The node to take instead of asking, or null for "ask the policy".
        //
        // The rest carve-out is the archetype's own threshold rather than a
        // number invented here: GreedyDefensive tops off below 75% and
        // GreedyAggressive below 50%, and a wrapper that walked a 40%-health
        // party past the Rest node into a shop would be measuring a shop
        // visit that the archetype would never have made. RandomLegal's
        // threshold is 0 -- it has no rest rule -- so nothing carves out.
        public static DescentNode PreferredNode(
            ShopNodeMode mode, IReadOnlyList<DescentNode> choices, RunView view, float restBelowHpFraction)
        {
            if (mode != ShopNodeMode.WhenOffered || choices == null) return null;

            var shop = choices.FirstOrDefault(n => n != null && n.Type == RoomType.Shop);
            if (shop == null) return null;

            if (view.PartyHpFraction < restBelowHpFraction &&
                choices.Any(n => n != null && n.Type == RoomType.Rest))
            {
                return null;
            }

            return shop;
        }
    }
}
