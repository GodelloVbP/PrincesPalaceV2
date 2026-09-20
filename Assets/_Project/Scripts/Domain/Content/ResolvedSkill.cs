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

        // See RawSkillEntry's own headers for what each of these four
        // means and why it is a field rather than a special case.
        public int ResourceSpendCap;
        public bool SpendsAllPrimary;
        public int PercentOfMaxHealthPerPoint;
        public bool FreeAction;

        // The two a Ward alone reads -- see RawSkillEntry's own headers.
        // WardTurns is already resolved here: 0 on the raw row becomes
        // FightTuning.DefaultWardTurns (one turn), so nothing downstream has
        // to know what an unauthored duration means.
        public int PercentOfCasterMaxHealth;
        public int WardTurns;

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

        // See RawSkillEntry.approachStance / windupStance. The two phases in
        // front of the strike Stance holds. Empty means unauthored, which is
        // every skill written before these existed and which plays exactly as
        // it always did — the precedence table is on CombatBeat.
        public string ApproachStance = "";
        public string WindupStance = "";

        // See RawSkillEntry.summonEnemyId / summonCap. Null/0 for every
        // skill but a Summon effect's.
        public string SummonEnemyId = "";
        public int SummonCap;

        // See RawSkillEntry.iconPath. Empty for every skill but a bookOnly
        // one today; ArtPathConvention.EditorBaked, so nothing here loads it
        // at runtime -- ScreenRegistry bakes it into an IconEntry[] the way
        // it already does for items and relics.
        public string IconPath = "";

        // ---- milestone B: consumption and health payment (plan §4) -------

        // Percent of the CASTER'S OWN max health paid as a cost, ceiling-
        // rounded, alongside mana (plan 1.2) -- Blackglass Spear's blood
        // price. 0, the default, is every skill that has ever existed.
        // Validated together with ManaCost/ResourceCost by
        // SkillResolution.CanAfford and paid directly by HealthCost.Pay,
        // never through the damage funnel -- see HealthCost's own header for
        // why.
        public int HealthCostPercent;

        // THE PAIR THAT REPLACES A NULLABLE, same convention Status/HasStatus
        // above already uses and for the identical reason: Unity's
        // serializer has no support for Nullable<T> at all, so a bare
        // `StatusEffectType? RequiresStatus` field looks correct in every
        // fixture built by hand in C# and comes back silently null off every
        // ScriptableObject the content build actually writes -- exactly the
        // failure that stays invisible until a real fight loads the asset. A
        // Reclaim/requires row with no live player-facing case would have
        // shipped with this bug undetected by the domain test suite, which
        // never serializes a ResolvedSkill through Unity at all.
        public StatusEffectType RequiresStatusType;
        public bool HasRequiresStatus;

        // A board-state refusal (plan 1.1/1.8): the TARGET must carry this
        // status or the cast is refused before anything is paid, the same
        // shape Shatter-with-no-wards already is. Null for every skill but
        // Ashen Reckoning, which requires Poison.
        public StatusEffectType? RequiresStatus => HasRequiresStatus ? (StatusEffectType?)RequiresStatusType : null;

        // Same nullable-avoidance pair as RequiresStatus above.
        public StatusEffectType ConsumesStatusType;
        public bool HasConsumesStatus;

        // The general status a landed hit spends (plan 1.8, 2.4) -- Crownfall
        // reads and consumes StatusEffectType.Marked through this, never the
        // Drowned Lantern's own private mark set. Null for every skill that
        // does not consume anything on landing.
        public StatusEffectType? ConsumesStatus => HasConsumesStatus ? (StatusEffectType?)ConsumesStatusType : null;

        // THE HEAVIER PACKET LIST a fixed-damage skill resolves with INSTEAD
        // OF DamageInstances, when the target is found to carry
        // ConsumesStatus at the moment of the read (plan 2.4) -- Crownfall's
        // 12 Arcane in place of its ordinary 7. Empty for every skill but
        // one authoring ConsumesStatus.
        public DamageInstance[] DamageInstancesIfConsumed = Array.Empty<DamageInstance>();

        // THE PREMIUM PERCENT a Reclaim effect's detonation is marked up by
        // (plan D2/1.6) -- Ashen Reckoning's 150. Meaningless on anything but
        // a Reclaim row, which the resolver requires to author it explicitly
        // (there is no sensible default for "how much bonus this spell's
        // whole damage IS").
        public int DetonationPercent;

        // THE ORDERED LIST OF TYPES a Reclaim effect's consumed total is
        // split across (plan 1.7) -- Ashen Reckoning's [Poison, Fire], with
        // the odd point going to whichever type is FIRST in this list. Empty
        // for anything but a Reclaim row.
        public DamageType[] DetonationSplit = Array.Empty<DamageType>();

        // WHERE THIS SKILL CAN BE AIMED. See RawSkillEntry.meleeReach /
        // reachSlots for the two authored spellings, and Reach for why the
        // KIND matters and not only the mask.
        //
        // REPLACED the old `bool MeleeReach` outright rather than sitting
        // beside it: two fields answering one question is exactly how a
        // caller ends up reading the one that has not been kept current.
        // Every reader (the click gate, plate dimming, the bot's legal menu,
        // the enemy pool) now asks FightSession.CanReachEnemy with this.
        //
        // Reach.Any is also default(Reach), so a skill deserialised from an
        // older asset reads as unrestricted rather than as nothing.
        public Reach Reach = Reach.Any;

        // See RawSkillEntry.bookOnly / bookTier.
        public bool BookOnly;
        public int BookTier;

        // WHAT THE PLAYER PICKS BETWEEN BEFORE TARGETING. Empty for every
        // skill that does not ask -- which is all of them but the orb. See
        // RawSkillEntry.elements for why a list of one is refused and why
        // this requires authored packets to retype.
        public ElementChoice[] Elements = Array.Empty<ElementChoice>();

        // See RawSkillEntry.poolTiers. Empty for every skill but a primary-
        // pool tier skill's -- Bjorn's Slam is the first and, per
        // SkillEntryResolver.TryResolvePoolTiers, ascending by Spend.
        public ResolvedPoolTier[] PoolTiers = Array.Empty<ResolvedPoolTier>();

        public bool HasPoolTiers => PoolTiers != null && PoolTiers.Length > 0;

        // A SKILL WHOSE DESIGN HASN'T BEEN WRITTEN YET (Shawn's fourth
        // ability, docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §7
        // phase 3 package 3). Never player-selectable and never drawn by a
        // monster's weighted pool -- SkillEntryResolver forces
        // PlayerSelectable false on a placeholder entry regardless of what
        // was authored, which is what actually keeps it out of
        // AvailableSkillsFor/LegalActions/FightHudModel; Placeholder itself
        // is read-model only, for a future dossier/track screen to show
        // "Undecided" plus PlaceholderNote.
        public bool Placeholder;

        // Required together with Placeholder -- says why this skill exists
        // undesigned, the way every other required-together pair in this
        // file (appliesStatus/statusMagnitude, summonEnemyId/summonCap) is
        // checked by the resolver rather than trusted.
        public string PlaceholderNote = "";

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
        public bool IsDamaging => SkillEffects.IsDamagePipeline(Effect);

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

        // Whether casting this asks the player for an element first. The one
        // question every menu depth, the bot's legal list and the session's
        // cast gate all ask, so none of them tests `Elements.Length` itself.
        public bool HasElementChoice => Elements != null && Elements.Length > 0;

        // Whether `element` is one of the choices this skill actually offers.
        // The session refuses a cast that says otherwise; the menu should
        // never have offered it.
        public bool Offers(DamageType element)
        {
            if (Elements == null) return false;
            foreach (var choice in Elements)
            {
                if (choice != null && choice.Type == element) return true;
            }

            return false;
        }

        // THIS SKILL WITH THE CHOICE MADE: every authored packet retyped to
        // `element`, and that element's own presentation in place of the
        // skill's if it authored one. Id, cost, cooldown, reach, requirements
        // and everything else are the skill's own, so a cast copy still spends
        // what the skill costs and comes back when the skill does.
        //
        // MemberwiseClone RATHER THAN A HAND-WRITTEN FIELD COPY. A 40-field
        // copy is the shape AUDIT #60 records twice over: the missing line is
        // invisible, and the next field authored on skills.json would have to
        // be remembered here as well. The clone carries every field this type
        // will ever have; the three lines below are the only ones a choice
        // changes.
        //
        // THE COPY OFFERS NO FURTHER CHOICE -- Elements is emptied. The choice
        // has been made, so `HasElementChoice` on the result is false, which is
        // what lets the cast go back through the ordinary CastSkill path
        // without a second element gate, and what makes the detail card's
        // damage-type row read "FIRE" rather than re-listing all four.
        // PHASE 3: THIS SKILL WITH THE REWARD TRACK'S COST/FLAT ADJUSTMENTS
        // APPLIED -- ContentDatabase.ApplyRewardTrackSkillDeltas' only
        // caller. MemberwiseClone, the same shape AsElement already uses and
        // for the same reason: the catalogue's own ResolvedSkill (definition.
        // Data) is shared by every fight that fields the character, so
        // adjusting it in place would leak one player's collected track into
        // every other combatant reading the same instance.
        //
        // FLOORED AT 1, NEVER BELOW -- "minimum 1" is the authored contract
        // for both SpellCostDelta and SkillCostDelta, and it only applies to
        // a cost that was already POSITIVE: a skill authored at 0 mana stays
        // free rather than being floored up to 1 by a discount it was never
        // charged in the first place.
        //
        // `powerDelta` (phase 4) is the one that ADDS rather than discounts,
        // alongside flatAmountDelta: Shawn's level 26 raises Tuck In's ward
        // from 2 per Wool to 3 by moving `power`, the per-point lever, not
        // the flat one. No floor on either -- a track only ever pays a
        // positive delta into them.
        public ResolvedSkill WithTrackDeltas(int manaCostDelta, int resourceCostDelta, int flatAmountDelta,
            int powerDelta = 0)
        {
            var copy = (ResolvedSkill)MemberwiseClone();

            if (manaCostDelta != 0 && copy.ManaCost > 0)
            {
                copy.ManaCost = Math.Max(1, copy.ManaCost - manaCostDelta);
            }

            if (resourceCostDelta != 0 && copy.ResourceCost > 0)
            {
                copy.ResourceCost = Math.Max(1, copy.ResourceCost - resourceCostDelta);
            }

            if (flatAmountDelta != 0)
            {
                copy.FlatAmount += flatAmountDelta;
            }

            if (powerDelta != 0)
            {
                copy.Power += powerDelta;
            }

            return copy;
        }

        public ResolvedSkill AsElement(DamageType element)
        {
            var copy = (ResolvedSkill)MemberwiseClone();

            var packets = DamageInstances ?? Array.Empty<DamageInstance>();
            var retyped = new DamageInstance[packets.Length];
            for (int i = 0; i < packets.Length; i++)
            {
                retyped[i] = new DamageInstance(element, packets[i].amount);
            }

            copy.DamageInstances = retyped;
            copy.Elements = Array.Empty<ElementChoice>();

            // A COPY EITHER WAY, never the catalogue's own object -- the same
            // rule the constructor states, and the reason a cast cannot edit
            // the content it was dealt from.
            // HasArt RATHER THAN A PATH TEST. An element's block says "use me
            // instead of the skill's" by drawing something, and a layered block
            // draws through `layers` with no `path` at all -- SpellLayerRules
            // refuses a block that authors both. Read for a path, prismatic_orb's
            // Water element authored five layers and got the skill's empty
            // presentation instead: the pilot drew nothing, silently.
            var chosen = Elements == null ? null : System.Array.Find(Elements, e => e != null && e.Type == element);
            copy.Vfx = (chosen != null && chosen.Vfx != null && chosen.Vfx.HasArt
                ? chosen.Vfx
                : Vfx ?? SpellPresentation.None).Copy();

            return copy;
        }

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
            bool bookOnly = false, int bookTier = 0, ElementChoice[] elements = null,
            // APPENDED rather than placed beside `stance`, where they belong
            // by meaning: every argument here is positional at its one call
            // site (SkillEntryResolver) and in a dozen test fixtures, so
            // inserting in the middle would silently repoint two strings.
            string approachStance = "", string windupStance = "",
            // APPENDED LAST, same reason. poolTiers is the newest field on
            // this type and the one least likely to be in hand at any
            // existing call site.
            ResolvedPoolTier[] poolTiers = null,
            // APPENDED LAST OF ALL (P3): placeholder/placeholderNote are the
            // newest fields on this type.
            bool placeholder = false, string placeholderNote = "",
            // APPENDED LAST OF ALL AGAIN (phase 4), same positional-argument
            // reason every block above gives.
            int resourceSpendCap = 0, bool spendsAllPrimary = false, int percentOfMaxHealthPerPoint = 0,
            bool freeAction = false,
            // APPENDED LAST OF ALL AGAIN (the shield model, phase 5), for the
            // same positional-argument reason every block above gives.
            int percentOfCasterMaxHealth = 0, int wardTurns = 0,
            // APPENDED LAST OF ALL AGAIN (spell book art), same reason.
            string iconPath = "",
            // APPENDED LAST OF ALL AGAIN (milestone B: consumption and
            // health payment), same positional-argument reason every block
            // above gives.
            int healthCostPercent = 0, StatusEffectType? requiresStatus = null,
            StatusEffectType? consumesStatus = null, DamageInstance[] damageInstancesIfConsumed = null,
            int detonationPercent = 0, DamageType[] detonationSplit = null)
        {
            IconPath = iconPath ?? "";
            HealthCostPercent = healthCostPercent;
            HasRequiresStatus = requiresStatus.HasValue;
            RequiresStatusType = requiresStatus ?? default;
            HasConsumesStatus = consumesStatus.HasValue;
            ConsumesStatusType = consumesStatus ?? default;
            DamageInstancesIfConsumed = damageInstancesIfConsumed ?? Array.Empty<DamageInstance>();
            DetonationPercent = detonationPercent;
            DetonationSplit = detonationSplit ?? Array.Empty<DamageType>();
            ResourceSpendCap = resourceSpendCap;
            SpendsAllPrimary = spendsAllPrimary;
            PercentOfMaxHealthPerPoint = percentOfMaxHealthPerPoint;
            FreeAction = freeAction;
            PercentOfCasterMaxHealth = percentOfCasterMaxHealth;
            WardTurns = wardTurns > 0 ? wardTurns : FightTuning.DefaultWardTurns;
            Placeholder = placeholder;
            PlaceholderNote = placeholderNote ?? "";
            Elements = elements ?? Array.Empty<ElementChoice>();
            PoolTiers = poolTiers ?? Array.Empty<ResolvedPoolTier>();
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
            ApproachStance = approachStance ?? "";
            WindupStance = windupStance ?? "";
            Approach = approach;
            Shake = shake < 0f ? 0f : (shake > 1f ? 1f : shake);
            SummonEnemyId = summonEnemyId ?? "";
            SummonCap = summonCap;
        }
    }

    // One element a skill offers, and how that element's cast looks.
    //
    // A CLASS BESIDE ResolvedSkill RATHER THAN A CONTENT-SIDE MIRROR. It is
    // [Serializable] for the same reason ResolvedSkill is -- SkillDefinition
    // stores the resolved record itself, so a mirror carrier here would be the
    // exact restatement AUDIT #60 deleted from TalentEffect and RelicModifier.
    // System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public sealed class ElementChoice
    {
        public DamageType Type;

        // Empty for every element that has not been drawn yet. When it carries
        // a path, ResolvedSkill.AsElement puts it in place of the skill's own
        // -- which is what makes four sheets a content edit rather than a
        // chain change (docs/archive/PLAN_PRISMATIC_ORB.md, draw policy).
        public SpellPresentation Vfx = new SpellPresentation();

        // For the serialiser only.
        public ElementChoice()
        {
        }

        public ElementChoice(DamageType type, SpellPresentation vfx = null)
        {
            Type = type;

            // COPIED, not aliased -- see ResolvedSkill's constructor for the
            // fight-edits-the-catalogue case this closes.
            Vfx = (vfx ?? SpellPresentation.None).Copy();
        }
    }

    // ONE RUNG OF A PRIMARY-POOL DAMAGE LADDER -- see RawSkillEntry.poolTiers
    // and PoolTierResolution.Pick, which is the only reader.
    //
    // CUE REUSES Combat.TransformHitCue RATHER THAN GROWING A SECOND SHAKE/
    // hitStopSeconds PAIR. A worn transform's contact effect and a fired pool
    // tier both answer the identical question -- "what does this blow's
    // landing add, as a floor, on top of whatever the damage already
    // earned" -- and FightSession.Beats.ApplyHitCueFloor applies both
    // through the one rule. `vfx` on the cue is never authored by a pool
    // tier today (RawPoolTier has no vfx block); it rides along unused
    // because the type is shared, not because a tier draws anything of its
    // own.
    [Serializable]
    public sealed class ResolvedPoolTier
    {
        public float Spend;
        public float DamageMultiplier;
        public Combat.TransformHitCue Cue = new Combat.TransformHitCue();

        // For the serializer only.
        public ResolvedPoolTier()
        {
        }

        // UNCLAMPED, exactly like a transform's own raw hit cue
        // (SkillEntryResolver.TryResolveTransform copies raw.transform.hit
        // straight across with no numeric range check either) -- the
        // hitStopSeconds ceiling is applied once, where it is consumed,
        // by FightSession.Beats.ApplyHitCueFloor.
        public ResolvedPoolTier(float spend, float damageMultiplier, float shake = 0f, float hitStopSeconds = 0f)
        {
            Spend = spend;
            DamageMultiplier = damageMultiplier;
            Cue = new Combat.TransformHitCue { shake = shake, hitStopSeconds = hitStopSeconds };
        }
    }
}
