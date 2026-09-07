using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // A combatant's runtime state for the duration of one fight. Deliberately
    // plain and engine-free — the Core layer is responsible for building one
    // of these from a real Character/CharacterDefinition or EnemyDefinition
    // and reading the results back out afterward.
    public class CombatantState
    {
        public string Name;
        public bool IsPlayerSide;

        public int MaxHealth;
        public int CurrentHealth;
        public int MaxMana;
        public int CurrentMana;

        public int Attack;
        public int Speed;

        // Equipment-set stats. Zero for anyone wearing nothing, which is
        // every enemy and every unequipped character — so nothing changes
        // shape for a combatant that does not use them.
        //
        // Set after construction rather than taken as constructor arguments:
        // CombatantState is built in several places and from three different
        // sources, and a seven-argument constructor growing to ten is how the
        // wrong number ends up in the wrong slot.
        public int ManaRegen;

        // Mechanic (e), ARMOR PENETRATION: a flat amount subtracted from a
        // target's broad Defense (physical only -- see CombatMath.
        // BroadDefense) before mitigation, floored at 0 there. A derived
        // stat on the ATTACKER, the same "set once at kit-build time" shape
        // ManaRegen already uses -- gear/relics add to it via
        // RelicModifiers.Apply(RelicStat.ArmorPenetration, ...), same as
        // every other flat/percent stat on this class.
        public int ArmorPenetration;

        // The only two defensive stats — the old single generic `Defense`
        // field is gone, not renamed to either of these. Formerly
        // PhysicalResistance/MagicalResistance, renamed in place. See
        // DamagePipeline.AfterDefences for the one place either is read.
        public int PhysicalDefense;
        public int MagicalDefense;

        // Resistance to ONE element, on top of the two-way split above. Empty
        // for everything that has not been given any, which is why adding it
        // retuned nothing. See ResistanceByType.
        public ResistanceByType TypedResistance;

        // Null for everyone who has no signature resource, which today is
        // everyone except Shawn. A nullable reference rather than a
        // zero-capacity instance so "has one" is a single unambiguous check
        // and no combatant pays for a mechanic it does not use.
        public SignatureResource Signature;

        // Null for everyone without a stagger meter — every player character
        // and most enemies. Same nullable-reference reasoning as Signature,
        // and the same reason: a mechanic only some combatants carry should
        // cost the rest of them nothing, not even a zero-capacity instance.
        public BreakShield BreakShield;

        // Every rule this combatant's unlocked talents contribute to the
        // fight, flattened once at encounter build time — see
        // TalentEffectSet. Never null: the shared Empty instance is the
        // normal case (every enemy, and every character whose tree is still
        // the old stat-bump vocabulary), so no call site has to null-check
        // before asking a question.
        public TalentEffectSet Talents = TalentEffectSet.Empty;

        // Every rule this combatant's worn items' ROLLED MODIFIERS
        // contribute to the fight, flattened once at encounter build time —
        // see ModifierEffectSet, which mirrors Talents just above field for
        // field and reasoning for reasoning. Never null: the shared Empty
        // instance is the normal case for literally every combatant today,
        // more so than Talents — Phase A1 (EquipmentSlotEntry/InventoryEntry
        // modifierIds) landed the storage, but Phase A3 has not wired the
        // roll yet, so no equipped item's modifierIds is ever non-empty in a
        // real save. No call site has to null-check before asking a
        // question either way.
        public ModifierEffectSet ModifierEffects = ModifierEffectSet.Empty;

        // Runic's one-shot tempo rider: armed (to the discount PERCENT, not
        // just a bool -- the consuming call site needs the number, and
        // reading it back off ModifierEffects a second time would mean
        // re-deriving "which swing armed this" logic there too) the moment a
        // plain swing lands for a wearer of NextSkillManaDiscountPercent,
        // consumed and zeroed the next time mana is charged for ANY skill
        // cast (FightSession.ChargeSkillMana) -- whether that happens on the
        // very next action or three turns later. Zero is always "nothing
        // armed", the same "0 and absent mean the same thing" convention
        // every percent field in this vocabulary already uses.
        public int PendingManaDiscountPercent;

        // The timed transform this combatant is currently under, or null —
        // which is every combatant on nearly every turn. Nullable for the
        // same reason Signature and BreakShield are: "is transformed" should
        // be one unambiguous check, and nobody who never transforms should
        // carry the bookkeeping.
        public Transformation Transformation;

        // Extra attack percent from things CombatMath cannot see from here:
        // the Fragile Lamb's Weight of Wool (which counts warded party
        // members) and Gift: Fury (which is spent, not read).
        //
        // A field rather than a computation because ScaledAttack has the
        // attacker and nothing else — no party list, no encounter — and
        // handing it one would couple the damage formula to the whole fight.
        // The cost is that it can go stale, so there is exactly ONE thing
        // allowed to write it: FightController.RefreshAttackBonus, called
        // immediately before any damage this combatant deals. Anything else
        // setting this is a bug.
        public int BonusAttackPercent;

        // Turns left on Weight of Wool T3's grace period — the self-ward
        // damage bonus surviving the ward that earned it.
        //
        // On the combatant rather than on the status, because the whole point
        // is that it outlives the status: the ward is gone and the bonus is
        // not. Ticked down at the holder's turn start beside every other
        // duration.
        public int SelfWardGraceTurns;

        // Whether Last Stand's once-per-fight death save has already been
        // spent. Lives on the combatant rather than on the Transformation or
        // the talent set because it is per-FIGHT state: the talent set is
        // shared and immutable, and the transform comes and goes several
        // times in one fight while this must not reset with it.
        public bool CheatDeathSpent;

        // Always a real (possibly empty) list rather than nullable — unlike
        // Signature/BreakShield, which are each ONE mechanic a combatant
        // either has or does not, a combatant can pick up any number of
        // different statuses over a fight, so "empty list" is already the
        // correct zero state and there is no second state to distinguish it
        // from. Matches how every other per-combatant collection in this
        // codebase (inventory, unlockedTalentIds) is a real empty list, not
        // null.
        public readonly List<ActiveStatus> Statuses = new List<ActiveStatus>();

        // Mechanic (c), FALLING-OFF STACKS -- see FallingOffStacks' own
        // header. One list of remaining-turn counts per caller-owned key,
        // empty for every combatant carrying no stacking effect, which is
        // nearly everyone.
        public readonly Dictionary<string, List<int>> StackTimers = new Dictionary<string, List<int>>();

        // Whether ResolveSummon (a Roar-style skill) put this combatant on
        // the field rather than it starting the encounter there. Read by
        // Amassing Star (FightSession.BalanceRelics) -- "summons do not
        // count" toward its run-wide payout, since a summon is not a kill
        // the player fought for in the same sense the thing that cast it
        // was.
        public bool IsSummon;

        // Balance pass 2. Jo-Sun's Book of Anatomy: a flat percent added
        // MULTIPLICATIVELY to the super-effective (weakness) multiplier
        // only -- see CombatMath.EffectivenessMultiplier's own header.
        // Same "set once at kit-build time from a RelicModifier" shape
        // ArmorPenetration already uses.
        public int WeaknessMultiplierBonusPercent;

        // Vampire Dentures: the RELIC half of lifesteal, summed alongside
        // ModifierEffectType.LifestealPercent (gear's own half) inside
        // ApplyModifierOnHitRiders -- see that method's own comment. A
        // separate field rather than folded into ModifierEffects because
        // RelicModifiers.Apply only ever reads RelicModifier, the same
        // reason ArmorPenetration and WeaknessMultiplierBonusPercent are
        // each their own field rather than routed through ModifierEffects.
        public int RelicLifestealPercent;

        // Phoenix Egg. IsPhoenixEgg is false for literally everyone who has
        // not just cheated a fatal blow this way -- EggHealth/
        // EggTurnsRemaining are meaningless while it is false, the same
        // "flag first, numbers only matter behind it" shape BreakShield's
        // own IsBroken already uses. See FightSession.BalanceRelics2 for
        // the hatch/absorb/revive arithmetic.
        public bool IsPhoenixEgg;
        public int EggHealth;
        public int EggTurnsRemaining;

        // What this combatant's gear and spells RIDE, and the scores they
        // ride on.
        //
        // Scaling is a property of the action, but both actions a combatant
        // has are fixed for the fight — the weapon in their hand and the
        // spell their level grants — so they are resolved once here rather
        // than looked up per swing. That also keeps CombatMath's signatures
        // unchanged, which matters: the enemy attack path, the AOE path and
        // both player paths all funnel through the same two functions and any
        // one of them forgetting to pass a profile would silently drop
        // scaling for that route only.
        //
        // Both default to None and the scores to a neutral ten across the
        // board, so a combatant nobody has authored any of this for
        // multiplies by exactly 1.0 — see ScalingProfile. That default is why
        // every enemy in the game keeps hitting for what it always did.
        public AbilityScoreBlock AbilityScores = ScalingProfile.NeutralScores;

        // ScalingSet, not a bare ScalingProfile — a swing rides the main
        // hand alone, but a cast can ride the spell tier and both hands at
        // once (see ScalingSet's own comment on the off-hand rule). Every
        // existing single-profile assignment (the plain weapon.scaling
        // below, EffectiveSkillScaling) keeps compiling via ScalingSet's
        // implicit conversion.
        public ScalingSet WeaponScaling = ScalingSet.None;
        public ScalingSet SkillScaling = ScalingSet.None;

        public bool IsAlive => CurrentHealth > 0;

        public CombatantState(string name, bool isPlayerSide, int maxHealth, int maxMana, int attack, int speed)
        {
            Name = name;
            IsPlayerSide = isPlayerSide;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            MaxMana = maxMana;
            CurrentMana = maxMana;
            Attack = attack;
            Speed = speed;
        }
    }
}
