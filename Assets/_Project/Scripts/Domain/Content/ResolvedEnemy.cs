using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // A monster's ability, before the skill behind it has been looked up. See
    // ResolvedEnemy.Abilities for why the id survives this far.
    //
    // [Serializable] with public fields because ResolvedEnemy is what the
    // EnemyDefinition asset now stores, and Unity only serializes a struct it
    // has been told about. Immutable by convention, like everything else in
    // this folder -- the constructor is still the only thing that trims.
    [Serializable]
    public struct EnemyAbilityRef
    {
        public string SkillId;
        public float Weight;

        public EnemyAbilityRef(string skillId, float weight)
        {
            SkillId = (skillId ?? "").Trim();
            Weight = weight;
        }
    }

    // One scheduled sequence (RawEnemyScheduleEntry), validated: starts on
    // OnTurns (this monster's own acting turns, 1-based) and plays Skills in
    // order, one per acting turn. Ids, looked up by FightSession against the
    // kit's pool for the reason Abilities keeps ids.
    [Serializable]
    public sealed class EnemyScheduleEntry
    {
        public int[] OnTurns = Array.Empty<int>();
        public string[] Skills = Array.Empty<string>();

        public EnemyScheduleEntry()
        {
        }

        public EnemyScheduleEntry(IReadOnlyList<int> onTurns, IReadOnlyList<string> skills)
        {
            OnTurns = new int[onTurns?.Count ?? 0];
            for (int i = 0; i < OnTurns.Length; i++) OnTurns[i] = onTurns[i];
            Skills = new string[skills?.Count ?? 0];
            for (int i = 0; i < Skills.Length; i++) Skills[i] = (skills[i] ?? "").Trim();
        }
    }

    // A monster after every omitted field in its RawEnemyEntry has been
    // filled in and every provided field has been validated -- and the shape
    // EnemyDefinition now STORES rather than restates.
    //
    // WHY THIS IS A [Serializable] CLASS WITH PUBLIC FIELDS rather than a
    // readonly struct: the same measurement ResolvedSkill records one file
    // over. EnemyDefinition carried a second copy of 29 of these fields,
    // ContentBuilder copied them across one by one and FightEncounterAdapter
    // copied all 34 arguments back -- three field lists for one set of facts,
    // and a hand-written copy of that length is exactly the shape where a
    // dropped line is invisible (it happened twice on the skill side).
    // System.Serializable is BCL, not UnityEngine, so Domain stays engine-free.
    [Serializable]
    public sealed class ResolvedEnemy
    {
        public string Id = "";
        public string DisplayName = "";
        public StatBlock BaseStats;
        public int ExpReward;
        public int CurrencyReward;
        public bool IsBoss;

        // EVERY ELEMENT THIS MONSTER TAKES BADLY AND EVERY ONE IT SHRUGS OFF,
        // as the two authored lists rather than the packed ElementalAffinity.
        // The affinity itself is a readonly struct over two private int masks,
        // which no serializer can write; the lists are what was authored and
        // what EnemyDefinition already stored, so nothing is lost round-
        // tripping through them. Read the pair through Affinity below.
        public DamageType[] Weaknesses = Array.Empty<DamageType>();
        public DamageType[] Resistances = Array.Empty<DamageType>();

        public int SortOrder;

        // 0 means this enemy carries no stagger meter and cannot be broken.
        public int BreakShieldPoints;

        // See RawEnemyEntry.stageScale and .slotSpan. Both default to 1, so a
        // monster that says nothing stands exactly as it always has.
        public float StageScale = 1f;
        public int SlotSpan = 1;

        // See RawEnemyEntry.rollable. The initializer is load-bearing: an
        // asset built before this field existed reads TRUE, so no shipped
        // monster silently leaves the room pool.
        public bool Rollable = true;

        // See RawEnemyEntry.rallyPerRound. Both 0 means no rally.
        public int RallyAttackPercentPerStack;
        public int RallyMaxStacks;

        public bool HasRally => RallyAttackPercentPerStack > 0 && RallyMaxStacks > 0;

        // Resources-relative FOLDER holding this monster's stance
        // sprites (e.g. "Enemies/golem", which contains idle.png,
        // attack.png and so on). Empty for the many monsters with no art
        // yet — the fight stage falls back to a plain nameplate rather
        // than showing a broken sprite, same graceful-missing-art rule as
        // ResolvedCharacter.PortraitPath.
        public string SpritePath = "";

        // WHAT THIS MONSTER CAN DO, as skill ids and relative weights.
        //
        // IDS RATHER THAN RESOLVED SKILLS, deliberately. Enemies and skills are
        // two catalogues resolved independently and in no guaranteed order, so
        // holding a ResolvedSkill here would make one resolver depend on the
        // other having finished. The lookup happens where player kits are
        // already built from content -- FightEncounterAdapter -- which is the
        // one place that legitimately sees both.
        //
        // An ARRAY rather than IReadOnlyList because that is what serializes;
        // every consumer reads it as the interface, which an array satisfies.
        // Empty means this monster uses the legacy SkillName trio below.
        public EnemyAbilityRef[] Abilities = Array.Empty<EnemyAbilityRef>();

        // See RawEnemyEntry.schedule. Empty means every turn is the draw.
        public EnemyScheduleEntry[] Schedule = Array.Empty<EnemyScheduleEntry>();

        // The basic attack's own weight in that pool. See RawEnemyEntry.
        public float AttackWeight = 1f;

        // Empty SkillName means this monster has no second action at all.
        public string SkillName = "";
        public float SkillPower = 1.5f;
        public float SkillChance;

        // Which way SpritePath's art is drawn. The stage mirrors from this
        // rather than assuming a house convention — see StageFacing.
        public PrincesPalace.Domain.Stage.SpriteFacing Facing;

        // False means benched: the entry is still authored and validated,
        // but ContentBuilder builds no asset for it, so it never spawns.
        public bool Active = true;

        // A skill's VFX, shown over the TARGET when it lands — see
        // RawEnemyEntry's own comment. Empty path means no prop.
        // ONE VALUE, like a skill's. See SpellPresentation for the
        // measurement that collapsed the four fields that were here.
        public SpellPresentation Vfx = new SpellPresentation();

        // THE PAIR THAT REPLACES A NULLABLE, same trade ResolvedSkill makes:
        // StatusEffectType has a valid zero, so the FLAG is what says whether
        // anyone authored one. Read it through AppliesStatus below.
        //
        // StatusAuthored rather than HasStatus because HasStatus already means
        // something narrower here and has since v1 -- authored AND lasting at
        // least a turn.
        public StatusEffectType Status;
        public bool StatusAuthored;
        public int StatusMagnitude;
        public int StatusDuration;

        // See RawEnemyEntry.avoidsFrontSlot.
        public bool AvoidsFrontSlot;

        // See RawEnemyEntry.attackHoldsPosition.
        public bool AttackHoldsPosition;

        // See RawEnemyEntry.attackApproach. How the plain attack travels when it
        // is not holding position. Lunge is the default and the old behaviour.
        public PrincesPalace.Domain.Combat.Session.StageApproach AttackApproach =
            PrincesPalace.Domain.Combat.Session.StageApproach.Lunge;

        // See RawEnemyEntry.minFloor.
        public int MinFloor = 1;

        // The damage type this monster's own attacks and abilities carry —
        // see RawEnemyEntry.attackType. Defaults to Physical, same as an
        // unauthored player character's attackType (CharacterEntryResolver).
        // Enemies have no CombatantKit the way players' PlayerKit carries
        // one, so this is what FightSession.ActorAttackType/AttackTypeOf
        // fall through to for a combatant with no player kit registered —
        // without it, MagicalDefense was a dead stat against every enemy in
        // the game, since nothing an enemy did was ever typed.
        public DamageType AttackType = DamageType.Physical;

        // The packed pair every damage call site wants, COMPUTED ON EVERY READ.
        //
        // This was a memoised `_affinity`/`_affinityBuilt` pair, and the memo
        // was a write to a shared object from inside a fight. ResolvedEnemy
        // instances are the catalogue: ContentDatabase hands the SAME
        // EnemyDefinition.Data to every encounter, so the first packet of
        // damage of the first fight of the session mutated an object every
        // later fight also reads. Nothing observable went wrong -- the value is
        // a pure function of two arrays that never change -- but "nothing
        // observable went wrong" is exactly the claim a content-ownership test
        // has to be able to make about the WHOLE record, and it could not while
        // this field existed.
        //
        // THE OBVIOUS FIX DOES NOT WORK, which is worth writing down because it
        // is the one that gets tried. Seeding the cache in the constructors
        // covers the resolver's path and misses the one that matters: Unity
        // deserialises an asset by running the PARAMETERLESS constructor and
        // filling the fields afterwards, so at the moment that constructor runs
        // Weaknesses and Resistances are still empty and there is nothing to
        // seed from. Every ResolvedEnemy the game actually plays against
        // arrives that way.
        //
        // THE COST IS TWO SIX-BIT MASKS PER READ, over arrays of at most six
        // elements each, through an array overload of ElementalAffinity.Of that
        // allocates nothing. FightSession.AffinityOf asks on every damage
        // packet; that is a dozen array reads beside a damage calculation, and
        // buying immutability with it is the right trade.
        public ElementalAffinity Affinity => ElementalAffinity.Of(Weaknesses, Resistances);

        // The nullable reading of the Status/StatusAuthored pair, kept because
        // it is the shape every consumer already asks in.
        public StatusEffectType? AppliesStatus => StatusAuthored ? (StatusEffectType?)Status : null;

        // "Authored a skill" is a name, not a chance: an enemy with a named
        // skill and skillChance 0 still HAS one (a future rule could raise the
        // chance), while a blank name with a chance of 1 is nothing to use.
        public bool HasSkill => !string.IsNullOrWhiteSpace(SkillName);

        // Ported from v1's EnemyDefinition, which derived both the same way at
        // the same place. They live here rather than at the call site so the
        // enemy turn does not re-decide what "has a skill" means each time.
        public bool HasStatus => StatusAuthored && StatusDuration > 0;

        // For the serializer only. Every field carries its own initialiser so
        // an instance built this way is still safe to read before Unity fills
        // it in.
        public ResolvedEnemy()
        {
        }

        // THE SINGLE-ELEMENT CONVENIENCE, kept because it is how nearly every
        // fixture reads. NOT because the roster is authored through it -- the
        // production caller (EnemyEntryResolver) builds an ElementalAffinity
        // and uses the other constructor, so nothing shipped reaches this. It
        // is one line of delegation, not a second definition of anything: both
        // forms end up in the same two lists, so there is no shape here that
        // can drift out of step with the other.
        public ResolvedEnemy(string id, string displayName, StatBlock baseStats, int expReward, int currencyReward,
            bool isBoss, DamageType weakness, DamageType resistance, int sortOrder, string spritePath = "",
            PrincesPalace.Domain.Stage.SpriteFacing facing = PrincesPalace.Domain.Stage.SpriteFacing.Right,
            bool active = true, string skillName = "", float skillPower = 1.5f, float skillChance = 0f,
            int breakShieldPoints = 0, SpellPresentation presentation = null,
            IReadOnlyList<EnemyAbilityRef> abilities = null, float attackWeight = 1f,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0,
            int statusDuration = 0, bool avoidsFrontSlot = false, bool attackHoldsPosition = false,
            int minFloor = 1, float stageScale = 1f, int slotSpan = 1,
            PrincesPalace.Domain.Combat.Session.StageApproach attackApproach =
                PrincesPalace.Domain.Combat.Session.StageApproach.Lunge,
            DamageType attackType = DamageType.Physical)
            : this(id, displayName, baseStats, expReward, currencyReward, isBoss,
                   ElementalAffinity.Of(weakness, resistance), sortOrder, spritePath, facing, active,
                   skillName, skillPower, skillChance, breakShieldPoints, presentation, abilities,
                   attackWeight, appliesStatus, statusMagnitude, statusDuration, avoidsFrontSlot,
                   attackHoldsPosition, minFloor, stageScale, slotSpan, attackApproach, attackType)
        {
        }

        public ResolvedEnemy(string id, string displayName, StatBlock baseStats, int expReward, int currencyReward,
            bool isBoss, ElementalAffinity affinity, int sortOrder, string spritePath = "",
            PrincesPalace.Domain.Stage.SpriteFacing facing = PrincesPalace.Domain.Stage.SpriteFacing.Right,
            bool active = true, string skillName = "", float skillPower = 1.5f, float skillChance = 0f,
            int breakShieldPoints = 0, SpellPresentation presentation = null,
            IReadOnlyList<EnemyAbilityRef> abilities = null, float attackWeight = 1f,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0,
            int statusDuration = 0, bool avoidsFrontSlot = false, bool attackHoldsPosition = false,
            int minFloor = 1, float stageScale = 1f, int slotSpan = 1,
            PrincesPalace.Domain.Combat.Session.StageApproach attackApproach =
                PrincesPalace.Domain.Combat.Session.StageApproach.Lunge,
            DamageType attackType = DamageType.Physical)
        {
            // CLAMPED RATHER THAN TRUSTED, both of them. A zero or negative
            // scale is an invisible monster and a zero span is a room that
            // never fills, and neither is worth a crash or a mystery.
            StageScale = stageScale > 0f ? stageScale : 1f;
            SlotSpan = slotSpan > 0 ? slotSpan : 1;
            SkillName = skillName ?? string.Empty;
            SkillPower = skillPower;
            SkillChance = skillChance;
            Facing = facing;
            Active = active;
            Id = id ?? "";
            DisplayName = displayName ?? "";
            BaseStats = baseStats;
            ExpReward = expReward;
            CurrencyReward = currencyReward;
            IsBoss = isBoss;

            // Clamped up, not trusted: 0 from an unbanded entry means the
            // first floor, and a negative would let a filter of the form
            // `minFloor <= floor` pass everything forever.
            MinFloor = minFloor < 1 ? 1 : minFloor;
            SetAffinity(affinity);
            SortOrder = sortOrder;
            SpritePath = spritePath ?? "";
            BreakShieldPoints = breakShieldPoints;
            Vfx = (presentation ?? SpellPresentation.None).Copy();
            Abilities = ToArray(abilities);
            AttackWeight = attackWeight < 0f ? 0f : attackWeight;
            StatusAuthored = appliesStatus.HasValue;
            Status = appliesStatus ?? default;
            StatusMagnitude = statusMagnitude;
            StatusDuration = statusDuration;
            AvoidsFrontSlot = avoidsFrontSlot;
            AttackHoldsPosition = attackHoldsPosition;
            AttackApproach = attackApproach;
            AttackType = attackType;
        }

        private void SetAffinity(ElementalAffinity affinity)
        {
            var weaknesses = affinity.Weaknesses;
            var resistances = affinity.Resistances;
            Weaknesses = new DamageType[weaknesses.Count];
            for (int i = 0; i < weaknesses.Count; i++) Weaknesses[i] = weaknesses[i];
            Resistances = new DamageType[resistances.Count];
            for (int i = 0; i < resistances.Count; i++) Resistances[i] = resistances[i];

            // Nothing is cached from here on. The two arrays ARE the state; the
            // packed value is derived from them on demand. See the Affinity
            // property for why the cache that used to be seeded here is gone.
        }

        private static EnemyAbilityRef[] ToArray(IReadOnlyList<EnemyAbilityRef> abilities)
        {
            if (abilities == null || abilities.Count == 0) return Array.Empty<EnemyAbilityRef>();
            var copy = new EnemyAbilityRef[abilities.Count];
            for (int i = 0; i < abilities.Count; i++) copy[i] = abilities[i];
            return copy;
        }
    }
}
