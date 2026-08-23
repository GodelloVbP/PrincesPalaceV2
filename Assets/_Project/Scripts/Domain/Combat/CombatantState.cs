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
        public int Defense;
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
        public int PhysicalResistance;
        public int MagicalResistance;

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

        // Brave/Default's banked pool. Zero for everyone who has never
        // Defaulted, which is everyone at the start of every fight — no
        // opt-in needed, unlike Signature/BreakShield, because spending a
        // real turn to bank one is already its own cost; there is no reason
        // to gate who is allowed to pay it.
        public int BankedActions;

        // Always a real (possibly empty) list rather than nullable — unlike
        // Signature/BreakShield, which are each ONE mechanic a combatant
        // either has or does not, a combatant can pick up any number of
        // different statuses over a fight, so "empty list" is already the
        // correct zero state and there is no second state to distinguish it
        // from. Matches how every other per-combatant collection in this
        // codebase (inventory, unlockedTalentIds) is a real empty list, not
        // null.
        public readonly List<ActiveStatus> Statuses = new List<ActiveStatus>();

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

        public CombatantState(string name, bool isPlayerSide, int maxHealth, int maxMana, int attack, int defense, int speed)
        {
            Name = name;
            IsPlayerSide = isPlayerSide;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            MaxMana = maxMana;
            CurrentMana = maxMana;
            Attack = attack;
            Defense = defense;
            Speed = speed;
        }
    }
}
