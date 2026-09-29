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

        public int Attack;
        public int Speed;

        // THE RESOURCE THIS COMBATANT'S SKILLS SPEND. Mana for everyone
        // shipped today; a character can trade it for something else by
        // naming a different pools.json row (RawCharacterEntry.primaryPoolId).
        //
        // NEVER NULL, and that is the difference between this slot and
        // SignaturePool below: a signature resource is a mechanic only some
        // combatants have, while EVERY combatant pays for something. A
        // nullable primary pool would put a `?.` in front of every cost check
        // in the game and make "cannot afford" and "has no resource at all"
        // indistinguishable at the call site.
        public ResourcePool PrimaryPool;

        // THE THREE FIELDS THIS REPLACED, kept as read-only views onto the
        // pool so the ~40 places that only ASK a question -- the bot's
        // missing-mana sum, the item log line, the relic that reads how much
        // is missing, the HUD -- did not all have to be retyped to say the
        // same thing. They are truthful rather than merely compiling: while
        // every shipped primary pool is mana, "CurrentMana" and
        // "PrimaryPool.Current" are the same number under two names.
        //
        // They will stop being truthful the moment a character's primary pool
        // is not mana (phase E), which is why they are getters and not
        // fields: a reader keeps working and a WRITER does not compile, so
        // the ~25 sites that used to assign mana had to say which pool they
        // meant. Phase F renames the readers and deletes these.
        public int MaxMana => PrimaryPool != null ? PrimaryPool.Max : 0;
        public int CurrentMana => PrimaryPool != null ? PrimaryPool.Current : 0;
        public int ManaRegen => PrimaryPool != null ? PrimaryPool.GainPerTurn : 0;

        // Mechanic (e), ARMOR PENETRATION: a flat amount subtracted from a
        // target's broad Defense (physical only -- see CombatMath.
        // BroadDefense) before mitigation, floored at 0 there. A derived
        // stat on the ATTACKER, set once at kit-build time and never
        // recomputed -- gear/relics add to it via
        // RelicModifiers.Apply(RelicStat.ArmorPenetration, ...), same as
        // every other flat/percent stat on this class.
        //
        // Set after construction rather than taken as a constructor
        // argument: CombatantState is built in several places and from three
        // different sources, and a six-argument constructor growing to ten is
        // how the wrong number ends up in the wrong slot.
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

        // CRITICAL HITS -- see CritRules for the whole rule. Totals, not
        // bonuses: a party member's kit build may write the totals its
        // effective stats give here. Set after construction for the same
        // reason ArmorPenetration is.
        //
        // The constructor starts a PLAYER-SIDE combatant at the baseline 5%
        // (CritRules.BaseChancePercent) and an enemy at 0; only party members
        // ever roll it (CritRules.ChanceFor). The damage defaults to the
        // baseline 150%, so an enemy's AUTHORED crit lands at the rule's own
        // figure.
        public int CritChancePercent;
        public int CritDamagePercent = CritRules.BaseDamagePercent;

        // THE SECOND, PRIVATE POOL: null for everyone who has no signature
        // resource, which today is everyone except Shawn. A nullable
        // reference rather than a zero-capacity instance so "has one" is a
        // single unambiguous check and no combatant pays for a mechanic it
        // does not use.
        //
        // The same TYPE as PrimaryPool and a different SLOT. Two named
        // fields rather than a keyed list because nothing indexes a pool by
        // id, and because a skill's two cost fields (manaCost, resourceCost)
        // then name their pool at the call site instead of through a lookup
        // that can miss.
        public ResourcePool SignaturePool;

        // Null for everyone without a stagger meter — every player character
        // and most enemies. Same nullable-reference reasoning as Signature,
        // and the same reason: a mechanic only some combatants carry should
        // cost the rest of them nothing, not even a zero-capacity instance.
        public BreakShield BreakShield;

        // Cumulative physical armour permanently removed during this fight.
        // The live PhysicalDefense field is still the authority for damage;
        // this counter exists so inspect/status UI can explain why it changed.
        public int PermanentPhysicalDefenseShred;

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

        // THE PHASE 4 ENGINE SEAMS (docs/PLAN_BJORN_CONSTELLATIONS.md, "Phase
        // 4 engine seams"). Each is off for everyone until the Juggernaut
        // wiring sets it, and each is read at exactly one seam in the session.
        //
        // Cursed Blood (4b): open the window and every heal converts. Read by
        // FightSession.HealAndCount. See HealConversion.
        public readonly HealConversion HealConversion = new HealConversion();

        // Ignore Pain (4c): null = no delayed damage. Read by
        // FightSession.LandPacket (defer), TickStatuses (pay) and HealAndCount
        // (T3's heal-reduces-pool). See DelayedDamagePool.
        public DelayedDamagePool DelayedDamage;

        // Blood Price (4d): thousandths of max health paid per point of
        // primary-pool shortfall; 0 = off, 5 = Blood Price T1. Read by
        // SkillResolution.CanAfford and FightSession.CastCore. See BloodPrice.
        public int ShortfallHealthPermille;

        // Unstoppable and Unyielding (4e). Read at FightSession.RecordStatus,
        // the application seam every status reaches. See CrowdControlGuard.
        public readonly CrowdControlGuard CrowdControl = new CrowdControlGuard();

        // The Sentinel's planted shield and Shieldwall (4a). Nothing placed
        // and every reactive hook off until the Sentinel wiring touches it.
        // Read at FightSession.ResolveWard. See PlantedShield.
        public readonly PlantedShield PlantedShield = new PlantedShield();

        // PHASE 2, THE ROOT FURY ENGINES. None = the primary pool's authored
        // flat gains pay, exactly as before; any other kind replaces them for
        // this combatant. Read at FightSession.NoteDamageForPools and
        // TickPrimaryPool. See FuryEngine.
        public readonly FuryEngine FuryEngine = new FuryEngine();

        // The Einherjar tree's seams (EinherjarSeams.cs). Momentum is off
        // until Enabled; the other two are null = off.
        public readonly Momentum Momentum = new Momentum();
        public BattleTrance BattleTrance;
        public TwinRampageRule TwinRampage;

        // WHAT ONE SWING CARRIES, set by the session around a cast that has a
        // context the funnel cannot see (a Fury tier that fired) and put back
        // to 0 when the cast resolves. Read where the crit chance and the
        // armour are worked out (CritRules.ChanceFor, CombatMath.TotalDefense),
        // so the roll, the preview and the defence step all agree.
        public int CastCritChanceBonus;
        public int CastIgnoreDefensePercent;

        // Whether the last blow this combatant's swing decided was a crit.
        // Written by DamagePipeline where the crit is decided (only when the
        // roll is real, never on a preview), read by the Fury engine when the
        // pools hear that same blow.
        public bool LastHitWasCrit;

        // PER-HOLDER SKILL COOLDOWNS, by skill id: when present (> 0) it
        // replaces the skill's authored cooldownTurns for this combatant only
        // -- Second Wind granted by the Juggernaut root carries 4 where the
        // row itself has none. Read by SkillCooldowns.TurnsFor.
        public readonly Dictionary<string, int> CooldownOverrides = new Dictionary<string, int>();

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

        // THE POOL-AWARE CONSTRUCTOR, and the one every real fight now builds
        // through. The pool arrives already resolved -- capacity chain run,
        // start rule applied -- because the chain reads talents, relics, the
        // reward track and gear, none of which Domain can see. See
        // ContentDatabase.BuildPrimaryPool.
        public CombatantState(string name, bool isPlayerSide, int maxHealth, ResourcePool primaryPool, int attack, int speed)
        {
            Name = name;
            IsPlayerSide = isPlayerSide;
            MaxHealth = maxHealth;

            // THE PARTY BASELINE, here rather than at kit build, so every
            // player-side combatant crits at the rule's own 5% however it was
            // built. A kit build that knows the character's stats may overwrite
            // it with their stat-driven total (see CritRules). Enemies stay at 0 (and
            // are ignored by CritRules.ChanceFor regardless).
            CritChancePercent = isPlayerSide ? CritRules.BaseChancePercent : 0;
            CurrentHealth = maxHealth;
            PrimaryPool = primaryPool ?? DefaultManaPool(0);
            Attack = attack;
            Speed = speed;
        }

        // COMPATIBILITY OVERLOAD -- 254 call sites, nearly all of them test
        // fixtures, hand an int where a fight now hands a pool. Kept rather
        // than fixed up in this phase for one reason: a rename and a rewiring
        // are two things to review, and 254 mechanical edits in the same
        // commit would bury the twelve that are not mechanical.
        //
        // REMOVE AFTER PHASE F, when `manaCost` becomes `primaryCost` and
        // every fixture has to say which pool it means anyway.
        //
        // The pool it builds is mana-shaped by construction, not by lookup:
        // Domain cannot reach ContentDatabase, and a fixture asking for "30
        // mana" is asking for the default pool rather than for whatever
        // pools.json currently says. Full at the start, no decay, no
        // triggered gain -- exactly what the two int fields it replaced meant.
        public CombatantState(string name, bool isPlayerSide, int maxHealth, int maxMana, int attack, int speed)
            : this(name, isPlayerSide, maxHealth, DefaultManaPool(maxMana), attack, speed)
        {
        }

        // The id/name/tag a hand-built mana pool wears. Duplicated from the
        // `mana` row's three strings and NOT from its capacity, which is the
        // duplication that mattered and is gone: capacity arrives as an
        // argument here, and every real fight reads it off the row.
        private static ResourcePool DefaultManaPool(int maxMana)
        {
            var pool = new ResourcePool("mana", "Mana", maxMana, 0, 0, 0) { ShortTag = "MP" };
            pool.Gain(maxMana);
            return pool;
        }
    }
}
