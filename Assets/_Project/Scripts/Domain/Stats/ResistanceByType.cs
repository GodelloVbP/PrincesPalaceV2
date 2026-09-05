using System;

namespace PrincesPalace.Domain.Stats
{
    // RESISTANCE TO ONE SPECIFIC KIND OF HARM, on top of the two-way split.
    //
    // The game already resists by type, and the split is two ways: physical,
    // and everything else. CombatMath.TotalDefense sums the broad answer
    // (physical or magical) with this struct's typed one, and the total
    // reduces damage on a softening curve (R/(R+100)), so 100 resistance is
    // exactly half.
    //
    // Two ways is enough to say "armoured" or "warded" and not enough to say
    // anything else. A cloak against fire and a mask against poison are the
    // same item under a two-way split -- both are "magical resistance" -- so
    // there is no such thing as a good answer to a specific threat, only more
    // or less of the one generic answer.
    //
    // ADDITIVE ON TOP, not a replacement. physicalResistance and
    // magicalResistance stay exactly what they are, gear keeps rolling them,
    // the sheet keeps showing them, and this is what a combatant has BESIDES
    // that against one element. A combatant with nothing authored here resists
    // precisely as they did, which is why this could go in without retuning a
    // single existing item.
    //
    // A STRUCT WITH ONE FIELD PER TYPE rather than a dictionary. There are
    // eleven damage types (was five when this struct was written) and there
    // will not be fifty; a dictionary would allocate per combatant, serialise
    // awkwardly, and buy only the ability to ask about a type that does not
    // exist.
    [Serializable]
    public struct ResistanceByType
    {
        public int Physical;
        public int Fire;
        public int Ice;
        public int Nature;
        public int Poison;

        // ADDED FOR THE ITEM-MODIFIER PLAN'S PHASE C: Astral (Arcane) typed
        // resistance is one of the six elemental-family modifiers, and until
        // this field existed `For`/`With` silently dropped anything rolled
        // against Arcane -- the struct's own header used to read "Arcane
        // exists in the enum and nothing deals it yet" as the justification
        // for having no field, which stopped being true the moment a real,
        // droppable modifier could grant resistance to it. Zero for every
        // relic and every modifier authored before this field existed, same
        // as every other stat this codebase adds fields to.
        public int Arcane;

        // FIVE MORE FIELDS, added the moment DamageType grew five more
        // members -- same "zero for every relic and modifier authored
        // before this field existed" grandfathering the Arcane field's own
        // comment above already argues for.
        public int Earth;
        public int Water;
        public int Wind;
        public int Lightning;
        public int Void;

        public int For(DamageType type)
        {
            switch (type)
            {
                case DamageType.Physical: return Physical;
                case DamageType.Fire: return Fire;
                case DamageType.Ice: return Ice;
                case DamageType.Nature: return Nature;
                case DamageType.Poison: return Poison;
                case DamageType.Arcane: return Arcane;
                case DamageType.Earth: return Earth;
                case DamageType.Water: return Water;
                case DamageType.Wind: return Wind;
                case DamageType.Lightning: return Lightning;
                case DamageType.Void: return Void;
                default: return 0;
            }
        }

        public ResistanceByType With(DamageType type, int amount)
        {
            var copy = this;
            switch (type)
            {
                case DamageType.Physical: copy.Physical += amount; break;
                case DamageType.Fire: copy.Fire += amount; break;
                case DamageType.Ice: copy.Ice += amount; break;
                case DamageType.Nature: copy.Nature += amount; break;
                case DamageType.Poison: copy.Poison += amount; break;
                case DamageType.Arcane: copy.Arcane += amount; break;
                case DamageType.Earth: copy.Earth += amount; break;
                case DamageType.Water: copy.Water += amount; break;
                case DamageType.Wind: copy.Wind += amount; break;
                case DamageType.Lightning: copy.Lightning += amount; break;
                case DamageType.Void: copy.Void += amount; break;
            }

            return copy;
        }

        // EVERY TYPE THAT IS NOT PHYSICAL, which is what a player means by
        // "magic resistance" and what a buckler like Darrow's is sold as.
        //
        // A helper rather than a sixth field, because it is not a sixth kind of
        // harm: nothing in the game deals damage of a type called magic. A
        // relic offering it is offering four things at once, and saying that
        // once here beats saying it in every relic that offers it.
        //
        // NOW TEN, not five -- Earth/Water/Wind/Lightning/Void join
        // Fire/Ice/Nature/Poison/Arcane the moment each has its own field
        // to add to. Same rule as before: every non-Physical type is
        // "magical", no exceptions carved out per element.
        public ResistanceByType WithMagical(int amount)
        {
            var copy = this;
            copy.Fire += amount;
            copy.Ice += amount;
            copy.Nature += amount;
            copy.Poison += amount;
            copy.Arcane += amount;
            copy.Earth += amount;
            copy.Water += amount;
            copy.Wind += amount;
            copy.Lightning += amount;
            copy.Void += amount;
            return copy;
        }

        public bool IsEmpty =>
            Physical == 0 && Fire == 0 && Ice == 0 && Nature == 0 && Poison == 0 && Arcane == 0 &&
            Earth == 0 && Water == 0 && Wind == 0 && Lightning == 0 && Void == 0;
    }
}
