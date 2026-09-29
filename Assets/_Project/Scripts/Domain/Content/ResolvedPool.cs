using System;

namespace PrincesPalace.Domain.Content
{
    // A validated resource pool -- and the shape PoolDefinition STORES rather
    // than restates, the way ResolvedCharacter is stored by
    // CharacterDefinition. Every string has been parsed to its enum and every
    // number checked, so the Editor-side glue has no decisions left to make.
    //
    // [Serializable] class with public fields, for the reason ResolvedSkill
    // records. System.Serializable is BCL, so Domain stays engine-free.
    //
    // NOTHING READS THIS YET. Phase A of the pool work is the catalogue only:
    // the type, its resolver, its asset and the one shipped `mana` row.
    // CombatantState, the HUD, the spell-book gates and the mana-restore
    // family are phases B-F.
    [Serializable]
    public sealed class ResolvedPool
    {
        public string Id = "";
        public string DisplayName = "";

        // What the combat meter's tag reads, e.g. "MP".
        public string ShortTag = "";

        // Whether Capacity is a base every Max Mana source adds to, or the
        // whole number from every source. See PoolCapacityRule.
        public PoolCapacityRule CapacityRule = PoolCapacityRule.WisdomDerived;
        public int Capacity;

        public int GainPerTurn;
        public int GainOnAttack;
        public int GainOnDamageTaken;

        // What an idle turn costs, and what stops a turn being idle.
        public int DecayPerIdleTurn;
        public PoolDecayTrigger DecayUnless = PoolDecayTrigger.Damage;

        public PoolStartRule StartRule = PoolStartRule.Full;
        public int StartValue;

        // Meter colours, as the '#RRGGBB'/'#RRGGBBAA' tokens Domain/UiKit
        // speaks. The rim and shade the meter also needs are derived from
        // DeepHex rather than authored -- see RawPoolEntry.deepHex.
        public string BrightHex = "";
        public string DeepHex = "";
        public string TextHex = "";

        public bool Pulse;
        public bool AllowsSpellBooks = true;
        public bool RestoredByManaEffects = true;
        public bool AbsorbsDamage;

        public int SortOrder;

        // A talent's Fury engine fills this pool, so the pool is dead until a
        // character has taken an engine root (Party.RequiredChoices).
        public bool EngineFed;

        // For the serializer only.
        public ResolvedPool()
        {
        }

        public ResolvedPool(
            string id, string displayName, string shortTag,
            PoolCapacityRule capacityRule, int capacity,
            int gainPerTurn, int gainOnAttack, int gainOnDamageTaken,
            int decayPerIdleTurn, PoolDecayTrigger decayUnless,
            PoolStartRule startRule, int startValue,
            string brightHex, string deepHex, string textHex,
            bool pulse, bool allowsSpellBooks, bool restoredByManaEffects, bool absorbsDamage,
            int sortOrder, bool engineFed = false)
        {
            Id = id ?? "";
            DisplayName = displayName ?? "";
            ShortTag = shortTag ?? "";
            CapacityRule = capacityRule;
            Capacity = capacity;
            GainPerTurn = gainPerTurn;
            GainOnAttack = gainOnAttack;
            GainOnDamageTaken = gainOnDamageTaken;
            DecayPerIdleTurn = decayPerIdleTurn;
            DecayUnless = decayUnless;
            StartRule = startRule;
            StartValue = startValue;
            BrightHex = brightHex ?? "";
            DeepHex = deepHex ?? "";
            TextHex = textHex ?? "";
            Pulse = pulse;
            AllowsSpellBooks = allowsSpellBooks;
            RestoredByManaEffects = restoredByManaEffects;
            AbsorbsDamage = absorbsDamage;
            SortOrder = sortOrder;
            EngineFed = engineFed;
        }
    }
}
