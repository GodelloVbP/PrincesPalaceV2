using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // One temporary change to a stat that the fight has applied: "+3 Speed
    // from Stampede". A delta and its source, never a resulting number, so the
    // menu decides what the base is (the sheet's own figure) and this only says
    // what the fight has done to it.
    public readonly struct FightStatModifier
    {
        public readonly SheetStat Stat;
        public readonly int Delta;
        public readonly string Source;

        public FightStatModifier(SheetStat stat, int delta, string source)
        {
            Stat = stat;
            Delta = delta;
            Source = source ?? "";
        }
    }

    // What the fight is SHOWING about one party member, as plain values the
    // system menu can read without seeing a FightSession (plan C4). Health and
    // pools are the numbers the HUD has painted, not the session's lead: the
    // session records a round before its beats play, so reading it would
    // reveal a hit the player has not yet watched land.
    public sealed class FightMemberFacts
    {
        public static readonly IReadOnlyList<FightStatModifier> NoModifiers = new FightStatModifier[0];

        // The save-side character id (the definition id the menu keys on).
        public readonly string CharacterId;

        public readonly int Health;
        public readonly int MaxHealth;

        // The primary pool, under its own name ("Fury", "Mana").
        public readonly string PoolName;
        public readonly int Pool;
        public readonly int MaxPool;

        // Null signature name means the character has none; the numbers are
        // then zero and mean nothing.
        public readonly string SignatureName;
        public readonly int Signature;
        public readonly int MaxSignature;

        public readonly IReadOnlyList<FightStatModifier> Modifiers;

        public FightMemberFacts(string characterId, int health, int maxHealth,
                                string poolName, int pool, int maxPool,
                                string signatureName, int signature, int maxSignature,
                                IReadOnlyList<FightStatModifier> modifiers)
        {
            CharacterId = characterId ?? "";
            Health = health;
            MaxHealth = maxHealth;
            PoolName = poolName ?? "";
            Pool = pool;
            MaxPool = maxPool;
            SignatureName = signatureName;
            Signature = signature;
            MaxSignature = maxSignature;
            Modifiers = modifiers ?? NoModifiers;
        }

        public bool HasSignature => SignatureName != null;

        // The sum of every modifier on `stat`; zero when the fight has not
        // touched it.
        public int DeltaFor(SheetStat stat)
        {
            int total = 0;
            foreach (var modifier in Modifiers)
            {
                if (modifier.Stat == stat) total += modifier.Delta;
            }

            return total;
        }

        // The sources behind that sum, each named once, in the order the fight
        // applied them: "Stampede, Chilled".
        public string SourcesFor(SheetStat stat)
        {
            var seen = new List<string>();
            foreach (var modifier in Modifiers)
            {
                if (modifier.Stat != stat || modifier.Delta == 0) continue;
                if (!seen.Contains(modifier.Source)) seen.Add(modifier.Source);
            }

            return string.Join(", ", seen);
        }

        // Whether the fight has moved this stat at all. Two modifiers that
        // cancel still read as a change worth naming.
        public bool Modifies(SheetStat stat)
        {
            foreach (var modifier in Modifiers)
            {
                if (modifier.Stat == stat && modifier.Delta != 0) return true;
            }

            return false;
        }
    }
}
