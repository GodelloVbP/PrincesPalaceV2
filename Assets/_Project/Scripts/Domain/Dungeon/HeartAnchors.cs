using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Dungeon
{
    // The living anchors at the heart (docs/PLAN_RUN_MAP.md section 6): the
    // order they stand in, how many stand in front of the boss, the share of
    // their HP they bring, and the order their chips are paid in. Pure: Core
    // builds the list from the realm map, the save and content, and the boss
    // fight and its settlement read it back from here.
    public static class HeartAnchors
    {
        // An anchor joins the boss fight at this share of its elite max HP.
        // A tuning literal for the P5 bot report (boss-fight win rate by
        // anchors left alive), never shown to the player.
        public const int HealthPermille = 500;

        public readonly struct Anchor
        {
            public readonly int NodeId;

            // Where on the realm the anchor stood; with NodeId, its map order.
            public readonly int Offset;

            public readonly string EnemyId;

            // Section 2's threat: scaled max HP x attack, as fielded at the
            // heart (NodeStrength.Weight of its one body).
            public readonly double Threat;

            // The chip it pays if it dies at the heart.
            public readonly PrizeKind Prize;

            public Anchor(int nodeId, int offset, string enemyId, double threat, PrizeKind prize)
            {
                NodeId = nodeId;
                Offset = offset;
                EnemyId = enemyId;
                Threat = threat;
                Prize = prize;
            }
        }

        // Map order: the walk's offset, then node id within one offset.
        public static int CompareMapOrder(Anchor a, Anchor b)
        {
            int byOffset = a.Offset.CompareTo(b.Offset);
            return byOffset != 0 ? byOffset : a.NodeId.CompareTo(b.NodeId);
        }

        // Formation order: strongest threat first, ties by map order. The
        // first FrontCount stand in front of the boss in this order (index 0
        // is rank 0); the rest queue in it.
        public static List<Anchor> Formation(IEnumerable<Anchor> living)
        {
            var list = living?.ToList() ?? new List<Anchor>();
            list.Sort((a, b) =>
            {
                int byThreat = b.Threat.CompareTo(a.Threat);
                return byThreat != 0 ? byThreat : CompareMapOrder(a, b);
            });
            return list;
        }

        // How many anchors stand on the field: the stage's slots less the
        // boss's own, never below zero.
        public static int FrontCount(int anchors, int stageSlots, int bossSlots = 1) =>
            Math.Max(0, Math.Min(anchors, stageSlots - bossSlots));

        // An anchor's max HP at the heart, never below 1.
        public static int HealthAtHeart(int maxHealth) =>
            Math.Max(1, (int)((long)maxHealth * HealthPermille / 1000));

        // Anchors that died at the heart, in the order the boss settlement
        // pays their chips: map order, before the boss's own pages merge
        // (PLAN_ITEMS.md 1.2). An anchor with no chip still appears: it is
        // still slain.
        public static List<Anchor> SettlementOrder(IEnumerable<Anchor> fallen)
        {
            var list = fallen?.ToList() ?? new List<Anchor>();
            list.Sort(CompareMapOrder);
            return list;
        }
    }
}
