using System;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One validated character skill — and the shape SkillDefinition now
    // STORES rather than restates.
    //
    // WHY THIS IS A [Serializable] CLASS WITH PUBLIC FIELDS rather than a
    // readonly struct. It used to be a struct, and SkillDefinition carried a
    // second copy of all 34 fields beside it, ContentBuilder copied them
    // across one by one, and FightEncounterAdapter copied them back. Adding
    // one field to skills.json therefore touched six files before a single
    // line of behaviour — and a hand-written copy of 34 lines is exactly the
    // kind where a missing line is invisible: `transform` was dropped on the
    // way out (Black Ram Mode granted nothing) and `bookOnly`/`bookTier` were
    // dropped after it. The same measurement SpellPresentation records, one
    // level up.
    //
    // A ScriptableObject can hold this directly, so SkillDefinition is now
    // `public ResolvedSkill data;` and the conversion is `definition.Data`.
    // Unity's serialiser needs writable public fields and a parameterless
    // constructor, so this is mutable by construction and treated as immutable
    // by convention — which is what SpellPresentation and every other DTO in
    // this folder already does.
    //
    // System.Serializable is BCL, not UnityEngine, so Domain stays engine-free.
    //
    // No mirror enums: SkillEffect and SkillTargeting live in Domain and are
    // used directly on both sides, the way DamageType and EquipmentSlot
    // already are. ResolvedItemKind exists only because ItemKind is a
    // Content-layer enum, which is a different situation.
    [Serializable]
    public sealed class ResolvedSkill
    {
        // Stable identifier written into save files -- a run names the
        // spells it has learned by this string. Never rename it once a save
        // exists.
        public string Id = "";
        public string DisplayName = "";
        public string Description = "";
        public string CharacterId = "";
        public int UnlockLevel;
        public SkillEffect Effect;
        public SkillTargeting Targeting;
        public int ManaCost;
        public int ResourceCost;
        public bool SpendsAllResource;
        public int Power;
        public int FlatAmount;
        public bool IgnoresDefense;

        // Empty for a skill that scales off Attack; non-empty for one that
        // deals fixed typed packets instead.
        public DamageInstance[] DamageInstances = Array.Empty<DamageInstance>();

        // HOW IT LOOKS, as one value. Six parallel fields lived here --
        // path, seconds, impact frame, from-caster, depart frame, sfx -- and
        // every one of them had to be threaded by hand through ten files that
        // do not care what a spell looks like. See SpellPresentation.
        public SpellPresentation Vfx = new SpellPresentation();

        public int SortOrder;

        // THE PAIR THAT REPLACES A NULLABLE. Unity does not serialize
        // StatusEffectType?, and appliesStatus has a valid zero (Poison), so
        // the FLAG is what says whether anyone meant it — the same trade
        // ResolvedEnemy's raw asset already made. AppliesStatus below is the
        // nullable reading every consumer still uses. Landed on whoever Effect
        // already resolves against — see RawSkillEntry.appliesStatus.
        public StatusEffectType Status;
        public bool HasStatus;
        public int StatusMagnitude;
        public int StatusDuration;

        // What a character needs, from base scores/talents/worn gear, before
        // this skill can be cast at all — see RequirementResolver and
        // FightController.Hud's own companion flag beside `affordable`.
        //
        // An unmet requirement shows GREYED AND UNCASTABLE rather than
        // hiding the button: a kit that changes shape between turns reads as
        // buttons appearing at random, the same argument AvailableSkillsFor
        // makes about affordability.
        public AbilityScoreBlock Requirements;

        // Which axis this skill's damage rides — see RawSkillEntry.
        // scalingAxis. Auto (the default) derives it from the caster's own
        // attackType at cast time; this only ever carries an explicit
        // override.
        public ScalingAxis ScalingAxis;

        // How many places later in the queue this skill knocks its target; 0
        // for everything that does not touch the turn order.
        public int QueuePushSlots;

        // What a Transform skill turns the caster into. Null for every other
        // skill — the resolver only ever fills this in for the one effect
        // that reads it, so a stray authored block is an error rather than a
        // silently ignored one.
        //
        // Read through IsAuthored rather than a null check alone: Unity's
        // serialiser writes an EMPTY grant rather than a null one, so a skill
        // that never authored a transform comes back off the asset with a
        // turns-0 block instead of nothing. EnterTransform already checks both.
        public TransformGrant Transform;

        // See RawSkillEntry.cooldownTurns. 0 for a skill with no cooldown.
        public int CooldownTurns;

        // See RawSkillEntry.playerSelectable.
        public bool PlayerSelectable = true;

        // See RawSkillEntry.shake. Clamped at construction rather than trusted,
        // because an authored 12 would otherwise throw the whole stage off
        // screen and the JSON has no schema to stop it.
        public float Shake;

        // See RawSkillEntry.approach. Hold is the long-standing behaviour and
        // the default, so a skill that says nothing stands still exactly as it
        // always has.
        public StageApproach Approach = StageApproach.Hold;

        // See RawSkillEntry.stance. Empty means the long-standing default:
        // whoever resolves this skill plays their "cast" pose.
        public string Stance = "";

        // See RawSkillEntry.summonEnemyId / summonCap. Null/0 for every
        // skill but a Summon effect's.
        public string SummonEnemyId = "";
        public int SummonCap;

        // WHERE THIS SKILL CAN BE AIMED. See RawSkillEntry.meleeReach /
        // reachSlots for the two authored spellings, and Reach for why the
        // KIND matters and not only the mask.
        //
        // REPLACED the old `bool MeleeReach` outright rather than sitting
        // beside it: two fields answering one question is exactly how a
        // caller ends up reading the one that has not been kept current.
        // Every reader (the click gate, plate dimming, the bot's legal menu,
        // the enemy pool) now asks FightSession.CanReach with this.
        //
        // Reach.Any is also default(Reach), so a skill deserialised from an
        // older asset reads as unrestricted rather than as nothing.
        public Reach Reach = Reach.Any;

        // See RawSkillEntry.bookOnly / bookTier.
        public bool BookOnly;
        public int BookTier;

        // The nullable reading of the Status/HasStatus pair, kept because it is
        // the shape both consumers (FightSession.Skills, FightSession.Enemies)
        // already ask in and the one the resolver hands in.
        public StatusEffectType? AppliesStatus => HasStatus ? (StatusEffectType?)Status : null;

        public bool HasFixedDamage => DamageInstances != null && DamageInstances.Length > 0;

        // Whether a cast spends the owner's signature resource at all. Either
        // half is enough: spendsAllResource with a zero floor still empties the
        // gauge. Content validation reads this rather than the two fields, so
        // "costs nothing at all" is asked once.
        public bool CostsResource => ResourceCost > 0 || SpendsAllResource;

        // Whether this skill deals damage at all -- the same gate
        // FightHudModel's SCALES/POWER rows already use (HasNoPreviewablePower/
        // ScalingLabelForSkill), repeated here rather than duplicated a third
        // time for the damage-type row those two rows sit beside.
        public bool IsDamaging => Effect == SkillEffect.DamageSingle || Effect == SkillEffect.DamageAll;

        // The damage type THIS SKILL authors directly -- only ever answerable
        // for a fixed-damage (multi-packet) spell, whose packets already
        // carry their own typed amount. A non-fixed damaging skill (the
        // common case: Power/FlatAmount scaled off the caster's own Attack)
        // has NO authored type of its own -- it rides whatever the caster's
        // kit/content declares as their attackType at cast time
        // (FightSession.ActorAttackType), and ResolvedSkill has no caster to
        // ask. Null here does not mean "no type" for that case; it means
        // "ask the caster" -- see FightHudModel.DamageTypeLabel, the one
        // place both a skill AND a caster are in hand together.
        public DamageType? FixedDamageType => HasFixedDamage ? DamageInstances[0].type : (DamageType?)null;

        // For the serialiser only. Every field carries its own initialiser so
        // an instance built this way is still safe to read before Unity fills
        // it in.
        public ResolvedSkill()
        {
        }

        public ResolvedSkill(string id, string displayName, string description, string characterId,
            int unlockLevel, SkillEffect effect, SkillTargeting targeting, int manaCost,
            int resourceCost, bool spendsAllResource, int power, int flatAmount, bool ignoresDefense,
            DamageInstance[] damageInstances, SpellPresentation presentation, int sortOrder,
            StatusEffectType? appliesStatus = null, int statusMagnitude = 0, int statusDuration = 0,
            AbilityScoreBlock requirements = default, ScalingAxis scalingAxis = ScalingAxis.Auto,
            int queuePushSlots = 0, TransformGrant transform = null, bool playerSelectable = true,
            int cooldownTurns = 0, string stance = "", string summonEnemyId = "", int summonCap = 0,
            StageApproach approach = StageApproach.Hold, float shake = 0f, Reach reach = default,
            bool bookOnly = false, int bookTier = 0)
        {
            BookOnly = bookOnly;
            BookTier = bookTier;
            Reach = reach;
            CooldownTurns = cooldownTurns < 0 ? 0 : cooldownTurns;
            PlayerSelectable = playerSelectable;
            QueuePushSlots = queuePushSlots;
            Transform = transform;
            DamageInstances = damageInstances ?? Array.Empty<DamageInstance>();
            // COPIED, not aliased. A skill in the catalogue and a beat in a
            // fight would otherwise hold the same mutable object. (The beat
            // copies again at its own boundary -- FightSession.Beats.
            // RecordSpellPresentation -- which is the copy that stops playback
            // editing the catalogue now that Resolve hands out `data` itself.)
            Vfx = (presentation ?? SpellPresentation.None).Copy();
            Id = id;
            DisplayName = displayName;
            Description = description;
            CharacterId = characterId;
            UnlockLevel = unlockLevel;
            Effect = effect;
            Targeting = targeting;
            ManaCost = manaCost;
            ResourceCost = resourceCost;
            SpendsAllResource = spendsAllResource;
            Power = power;
            FlatAmount = flatAmount;
            IgnoresDefense = ignoresDefense;
            SortOrder = sortOrder;
            HasStatus = appliesStatus.HasValue;
            Status = appliesStatus ?? default;
            StatusMagnitude = statusMagnitude;
            StatusDuration = statusDuration;
            Requirements = requirements;
            ScalingAxis = scalingAxis;
            Stance = stance ?? "";
            Approach = approach;
            Shake = shake < 0f ? 0f : (shake > 1f ? 1f : shake);
            SummonEnemyId = summonEnemyId ?? "";
            SummonCap = summonCap;
        }
    }
}
