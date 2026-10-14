using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Bot
{
    // A thresholds-on-HP path rule: which of the rooms on offer a policy will
    // walk into at this party HP. Narrow() removes what the party is too hurt
    // for and orders what is left by strength, so the policy's own room-type
    // order then picks the first of its kind. When every room breaks a gate the
    // least costly ones stay, so a forced room is still taken.
    public sealed class PathRule
    {
        // A pocket is skipped below this fraction of party HP.
        public readonly float PocketMinHp;

        // A strength-3 fight is skipped below this fraction.
        public readonly float Strength3MinHp;

        // An anchor is skipped below this fraction. Above 1 means never.
        public readonly float AnchorMinHp;

        // True lists higher strength first (more prize), false lower first.
        public readonly bool PrefersStrong;

        public PathRule(float pocketMinHp, float strength3MinHp, float anchorMinHp, bool prefersStrong)
        {
            PocketMinHp = pocketMinHp;
            Strength3MinHp = strength3MinHp;
            AnchorMinHp = anchorMinHp;
            PrefersStrong = prefersStrong;
        }

        // What a node costs against the rule: 0 is allowed. A strength-3 fight
        // or an anchor outweighs a pocket, so when every room on offer breaks
        // a gate the party takes the pocket before the heavy fight.
        private const int PocketCost = 1;
        private const int HeavyCost = 2;

        public IReadOnlyList<RealmNode> Narrow(IReadOnlyList<RealmNode> choices, RunView view)
        {
            var map = view.Map;
            if (map == null || choices.Count < 2) return choices;

            float hp = view.PartyHpFraction;
            int least = choices.Min(n => map.Has(n) ? Cost(map.Of(n), hp) : 0);
            return choices
                .Where(n => (map.Has(n) ? Cost(map.Of(n), hp) : 0) == least)
                .OrderBy(n => map.Has(n) ? (PrefersStrong ? -map.Of(n).Strength : map.Of(n).Strength) : 0)
                .ToList();
        }

        private int Cost(MapNodeView node, float hp)
        {
            // A pocket that opens on a Rest is worth entering hurt.
            int cost = 0;
            if (node.InPocket && node.Type != RoomType.Rest && hp < PocketMinHp) cost += PocketCost;
            if (node.IsAnchor ? hp < AnchorMinHp : node.Strength >= 3 && hp < Strength3MinHp) cost += HeavyCost;
            return cost;
        }
    }
}
