using System;

namespace PrincesPalace.Domain.Combat
{
    // WHICH LIVING RANKS A SINGLE-OPPONENT ACTION CAN LAND ON.
    //
    // Three kinds, and the difference between the last two is the whole
    // reason this is not just a bitmask:
    //
    //   Any            -- no positional restriction at all. Every skill
    //                     authored before positions mattered, and every
    //                     bolt/curse/thrown thing authored after.
    //   Melee          -- the front-rank rule: a hand striking through a
    //                     monster's own bodyguard. Monkey King's Scepter
    //                     lifts THIS and only this.
    //   ExplicitRanks  -- an authored list of ranks. A front-only authored
    //                     restriction (FromContent([1])) carries the same
    //                     mask as Melee and is deliberately NOT Melee: the
    //                     scepter does not lift it, because the scepter's
    //                     promise is about reaching past a bodyguard, not
    //                     about ignoring whatever a spell says about where
    //                     it can be aimed. The KIND decides, not the mask.
    public enum ReachKind
    {
        Any = 0,
        Melee = 1,
        ExplicitRanks = 2,
    }

    // TWO CONVENTIONS MEET HERE AND NOWHERE ELSE. Content authors positions
    // 1-based, because that is how a designer counts a battle line; every
    // line of C# in this codebase counts living ranks from 0, because that
    // is what an index is. `FromContent` is the resolver's door and does the
    // -1; `Ranks` is everyone else's and does not. A test is written in the
    // convention of whichever door it came through.
    //
    // A MUTABLE STRUCT, WHICH THE PLAN'S SKETCH WROTE AS `readonly struct`.
    // Unity's serialiser skips readonly fields outright, and ResolvedSkill --
    // which carries one of these -- is stored INSIDE a ScriptableObject
    // (SkillDefinition.data). A readonly Reach would round-trip through the
    // generated content tree as default(Reach), which reads as Reach.Any:
    // every melee and rank-restricted skill would silently lose its
    // restriction at runtime while every EditMode test still passed, since
    // those build a Reach in memory and never go through the asset. Same
    // "mutable by construction, immutable by convention" bargain ResolvedSkill
    // itself already documents and for exactly the same reason.
    //
    // System.Serializable is BCL, not UnityEngine, so Domain stays engine-free.
    [Serializable]
    public struct Reach : IEquatable<Reach>
    {
        // Read as a rule, not as data: a mask alone cannot tell an authored
        // front-only restriction from the melee rule. See ReachKind.
        public ReachKind Kind;

        // Bit i set means living rank i is allowed. Meaningless when Kind is
        // Any (which allows everything) and always {0} for Melee.
        public int RankMask;

        // Ranks beyond this cannot exist: a side never holds more than
        // FightHudSpec.StageSlotsPerSide combatants (CombatEncounter.
        // TryAddEnemy refuses past it), so every living rank has a stage slot
        // and an authored slot above this is a build refusal rather than a
        // rule nothing can ever satisfy.
        //
        // DERIVED, NOT RESTATED. This was `= 3` sitting directly under a
        // comment naming the constant it was copying, with nothing that would
        // fire when the two disagreed -- and the disagreement is silent in
        // both directions. Raise the stage to four slots and SkillEntryResolver
        // (which reads its authored 1..N bound off MaxRanks, and is the only
        // place that bound is stated) starts refusing a reachSlots of 4 as
        // "a restriction nothing could ever satisfy" while the fourth slot is
        // right there on screen; lower it to two and Allows goes on granting a
        // rank the stage cannot draw, which is the invisible-and-still-swinging
        // failure FightHudSpec's own header exists to prevent.
        //
        // Both constants are in the same assembly (PrincesPalace.Domain, which
        // is noEngineReferences and so cannot be bringing UI in through the
        // back door), and Session is a child namespace of this one, so the
        // reference resolves with no using and costs nothing. Domain.Content
        // and Domain.Dungeon already read the same constant this way.
        //
        // FightHudSpec is the direction of the derivation on purpose: it says
        // what combat may CREATE and the stage having that many slots is the
        // consequence. Reach is downstream of both.
        public const int MaxRanks = Session.FightHudSpec.StageSlotsPerSide;

        public Reach(ReachKind kind, int rankMask)
        {
            Kind = kind;
            RankMask = rankMask;
        }

        public static Reach Any => new Reach(ReachKind.Any, 0);

        public static Reach Melee => new Reach(ReachKind.Melee, 1);

        // ZERO-BASED. Everything inside the engine.
        public static Reach Ranks(params int[] zeroBased) =>
            new Reach(ReachKind.ExplicitRanks, MaskOf(zeroBased, 0));

        // ONE-BASED, and the resolver is its only caller. See this type's
        // own header for why the conversion lives in exactly one place.
        public static Reach FromContent(int[] oneBased) =>
            new Reach(ReachKind.ExplicitRanks, MaskOf(oneBased, -1));

        // A rank below zero is "dead, or not on this field at all" -- the
        // answer CombatEncounter.LivingRankOf gives for both -- and nothing
        // reaches it. Answered here rather than at every call site so the
        // sentinel has one reading.
        public bool Allows(int rank)
        {
            if (rank < 0) return false;
            if (Kind == ReachKind.Any) return true;
            if (rank >= MaxRanks) return false;
            return (RankMask & (1 << rank)) != 0;
        }

        private static int MaskOf(int[] slots, int offset)
        {
            int mask = 0;
            if (slots == null) return mask;

            foreach (int slot in slots)
            {
                int rank = slot + offset;
                if (rank < 0 || rank >= MaxRanks) continue;
                mask |= 1 << rank;
            }

            return mask;
        }

        public bool Equals(Reach other) => Kind == other.Kind && RankMask == other.RankMask;

        public override bool Equals(object obj) => obj is Reach other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ RankMask;

        public static bool operator ==(Reach a, Reach b) => a.Equals(b);

        public static bool operator !=(Reach a, Reach b) => !a.Equals(b);

        public override string ToString()
        {
            if (Kind == ReachKind.Any) return "Any";
            if (Kind == ReachKind.Melee) return "Melee";

            var ranks = new System.Collections.Generic.List<string>();
            for (int i = 0; i < MaxRanks; i++)
            {
                if ((RankMask & (1 << i)) != 0) ranks.Add(i.ToString());
            }

            return "Ranks(" + string.Join(",", ranks) + ")";
        }
    }

    // WHICH WAY A MOVE GOES, along the party's own list order. Forward is
    // toward rank 0 -- the front of the line, where the enemy melee lands.
    public enum MoveDirection
    {
        Forward,
        Back,
    }
}
