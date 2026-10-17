using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat
{
    // How several copies of one kind combine on one combatant.
    public enum EffectStacking
    {
        // The strongest affix copy, plus every other holder summed: two Vampiric
        // rolls do not stack, Vampiric plus Vampire Dentures does.
        BestAffixPlusOthers,

        // Every copy counts. The typed kinds, whose copies are told apart by
        // element and folded per element (resistance, elemental riders).
        Summed,

        // Present or absent; a second holder adds nothing. Magnitude is the
        // strongest copy's.
        Flag,
    }

    // Who may grant a kind. A content row is refused at build time for a kind
    // its holder may not carry.
    [Flags]
    public enum EffectHolders
    {
        None = 0,
        Relic = 1,
        Affix = 2,

        // The reward track's elemental terms (ContentDatabase.CharacterEffects).
        Track = 4,

        // An unlocked talent node (ContentDatabase.TalentEffects).
        Talent = 8,

        // A trait on a Legendary gear copy (modifiers.json `trait`): wearer-
        // scoped, any slot.
        Trait = 16,
    }

    // Exactly one holder per set entry; EffectHolders is the allowed-mask.
    public enum EffectHolder
    {
        Relic,
        Affix,
        Track,
        Talent,
        Trait,
    }

    // Which item an affix or trait of this kind may roll onto (ModifierTable).
    public enum AffixPool
    {
        None,

        // Riders a landed hit or the wielder's own aggression triggers.
        Weapon,

        // Protection and passive pools.
        Armour,
    }

    // Which element selector a row authors in `damageType`.
    public enum EffectTarget
    {
        None,
        Element,

        // An element, or "wearer": the element of the wearer's own
        // attackType, Physical if none (CharacterEffects resolves it).
        ElementOrWearer,

        // An element, or "magical" for every non-Physical type.
        ElementOrMagical,
    }

    // The stat a fold writes, percent/flat removed. Defence reaches both broad
    // Defenses.
    public enum EffectStat
    {
        None = 0,
        Attack,
        Defence,
        MaxHealth,
        MaxMana,
        Speed,
        ArmorPenetration,
        WeaknessBonus,
        Lifesteal,
        CritChance,
        CritDamage,
    }

    // One kind's facts.
    public readonly struct EffectInfo
    {
        public readonly EffectKind Kind;
        public readonly EffectStacking Stacking;
        public readonly EffectHolders Holders;

        // An affix's magnitude rides ModifierMagnitude's item-tier curve.
        public readonly bool TierScaled;
        public readonly AffixPool Pool;

        // False: a flag; authoring a magnitude is refused.
        public readonly bool UsesMagnitude;
        public readonly bool UsesThreshold;
        public readonly EffectTarget Target;

        // Set for a kind EffectFold folds into a stat.
        public readonly EffectStat Stat;
        public readonly bool Percent;

        // A talent kind that upgrades one named skill: the authoring node must
        // carry appliesToSkillId and the hook reads EffectSet.BestFor.
        public readonly bool SkillScoped;

        // A relic mechanic that still sums across holders (relic plus talent):
        // it names the relic like a Flag kind does but is not a Flag.
        private readonly bool _summedMechanic;

        // What a relic is named by: one per relic, one per draft.
        public bool IsMechanic => Stacking == EffectStacking.Flag || _summedMechanic;

        public EffectInfo(EffectKind kind, EffectStacking stacking, EffectHolders holders,
            bool tierScaled, AffixPool pool, bool usesMagnitude, bool usesThreshold,
            EffectTarget target, EffectStat stat, bool percent, bool skillScoped = false,
            bool summedMechanic = false)
        {
            Kind = kind;
            Stacking = stacking;
            Holders = holders;
            TierScaled = tierScaled;
            Pool = pool;
            UsesMagnitude = usesMagnitude;
            UsesThreshold = usesThreshold;
            Target = target;
            Stat = stat;
            Percent = percent;
            SkillScoped = skillScoped;
            _summedMechanic = summedMechanic;
        }

        public bool Allows(EffectHolder holder) => (Holders & ToMask(holder)) != 0;

        private static EffectHolders ToMask(EffectHolder holder)
        {
            switch (holder)
            {
                case EffectHolder.Relic: return EffectHolders.Relic;
                case EffectHolder.Affix: return EffectHolders.Affix;
                case EffectHolder.Track: return EffectHolders.Track;
                case EffectHolder.Talent: return EffectHolders.Talent;
                case EffectHolder.Trait: return EffectHolders.Trait;
                default: return EffectHolders.None;
            }
        }
    }

    // THE PER-KIND FACTS, one row each, so a new kind is one row here plus its
    // hook. EffectCatalogTests fails when a member has no row, and when a
    // member is named nowhere outside this file and EffectKind.cs.
    public static class EffectCatalog
    {
        private const EffectHolders RelicOnly = EffectHolders.Relic;
        private const EffectHolders AffixOnly = EffectHolders.Affix;
        private const EffectHolders RelicOrAffix = EffectHolders.Relic | EffectHolders.Affix;
        private const EffectHolders AffixOrTrack = EffectHolders.Affix | EffectHolders.Track;
        private const EffectHolders RelicOrTalent = EffectHolders.Relic | EffectHolders.Talent;

        private static readonly Dictionary<EffectKind, EffectInfo> Rows = Build();

        public static EffectInfo Of(EffectKind kind)
        {
            if (!Rows.TryGetValue(kind, out var info))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "EffectKind has no EffectCatalog row.");
            }

            return info;
        }

        public static bool Has(EffectKind kind) => Rows.ContainsKey(kind);

        public static IEnumerable<EffectInfo> All => Rows.Values;

        private static Dictionary<EffectKind, EffectInfo> Build()
        {
            var rows = new Dictionary<EffectKind, EffectInfo>();

            void Stat(EffectKind kind, EffectStat stat, bool percent, EffectHolders holders = RelicOnly,
                AffixPool pool = AffixPool.None)
            {
                rows.Add(kind, new EffectInfo(kind, EffectStacking.BestAffixPlusOthers, holders,
                    tierScaled: (holders & EffectHolders.Affix) != 0, pool, usesMagnitude: true, usesThreshold: false,
                    EffectTarget.None, stat, percent));
            }

            void Rider(EffectKind kind, AffixPool pool, EffectStacking stacking = EffectStacking.BestAffixPlusOthers,
                EffectHolders holders = AffixOnly, EffectTarget target = EffectTarget.None)
            {
                rows.Add(kind, new EffectInfo(kind, stacking, holders, tierScaled: true, pool,
                    usesMagnitude: true, usesThreshold: false, target, EffectStat.None, percent: false));
            }

            // A mechanic is a relic's; `traitPool` also makes it a gear trait
            // that rolls on that pool. A second holder of the same mechanic
            // adds nothing (Flag), whichever holders they are.
            void Mechanic(EffectKind kind, bool usesMagnitude, bool usesThreshold = false,
                AffixPool traitPool = AffixPool.None)
            {
                rows.Add(kind, new EffectInfo(kind, EffectStacking.Flag,
                    traitPool == AffixPool.None ? RelicOnly : EffectHolders.Relic | EffectHolders.Trait,
                    tierScaled: false, traitPool, usesMagnitude, usesThreshold, EffectTarget.None,
                    EffectStat.None, percent: false));
            }

            // A talent-only kind: the strongest owned node wins, because a
            // strand is one idea developed three times and owning T2 means
            // owning T1 (EffectSet.Magnitude).
            void Talent(EffectKind kind, bool usesMagnitude, bool usesThreshold, bool skillScoped)
            {
                rows.Add(kind, new EffectInfo(kind, EffectStacking.Flag, EffectHolders.Talent, tierScaled: false,
                    AffixPool.None, usesMagnitude, usesThreshold, EffectTarget.None, EffectStat.None,
                    percent: false, skillScoped));
            }

            Stat(EffectKind.AttackPercent, EffectStat.Attack, percent: true);
            Stat(EffectKind.DefensePercent, EffectStat.Defence, percent: true);
            Stat(EffectKind.MaxHealthPercent, EffectStat.MaxHealth, percent: true, RelicOrTalent);
            Stat(EffectKind.MaxManaPercent, EffectStat.MaxMana, percent: true);
            Stat(EffectKind.SpeedPercent, EffectStat.Speed, percent: true);
            Stat(EffectKind.AttackFlat, EffectStat.Attack, percent: false);
            Stat(EffectKind.DefenseFlat, EffectStat.Defence, percent: false);
            Stat(EffectKind.MaxHealthFlat, EffectStat.MaxHealth, percent: false);
            Stat(EffectKind.MaxManaFlat, EffectStat.MaxMana, percent: false, RelicOrAffix, AffixPool.Armour);
            Stat(EffectKind.SpeedFlat, EffectStat.Speed, percent: false);
            Stat(EffectKind.ArmorPenetrationFlat, EffectStat.ArmorPenetration, percent: false);
            Stat(EffectKind.WeaknessDamageBonusPercent, EffectStat.WeaknessBonus, percent: false);
            Stat(EffectKind.LifestealPercent, EffectStat.Lifesteal, percent: false, RelicOrAffix, AffixPool.Weapon);
            Stat(EffectKind.CritChanceFlat, EffectStat.CritChance, percent: false);
            Stat(EffectKind.CritDamageFlat, EffectStat.CritDamage, percent: false);

            // Summed, not best-of: each resistance copy is folded per element
            // into ResistanceByType, relic and affix alike (EffectFold).
            Rider(EffectKind.ResistanceFlat, AffixPool.Armour, EffectStacking.Summed, RelicOrAffix,
                EffectTarget.ElementOrMagical);

            // Every entry fires on its own, per element.
            Rider(EffectKind.ElementalDamageOnHitPercent, AffixPool.Weapon, EffectStacking.Summed, AffixOrTrack,
                EffectTarget.ElementOrWearer);
            Rider(EffectKind.ElementalDamagePercent, AffixPool.Weapon, EffectStacking.Summed, AffixOrTrack,
                EffectTarget.Element);

            Rider(EffectKind.FlatPhysicalDamageReduction, AffixPool.Armour);
            Rider(EffectKind.BreakShieldDepletionResistPercent, AffixPool.Armour);
            Rider(EffectKind.OnKillSplashPercent, AffixPool.Weapon, holders: EffectHolders.Affix | EffectHolders.Talent);
            Rider(EffectKind.PushBackOnHitChancePercent, AffixPool.Weapon);
            Rider(EffectKind.FlatManaRegenBonus, AffixPool.Armour);
            Rider(EffectKind.NextSkillManaDiscountPercent, AffixPool.Weapon);
            Rider(EffectKind.DodgeRating, AffixPool.Armour);
            Rider(EffectKind.ChilledOnHitChancePercent, AffixPool.Weapon);
            Rider(EffectKind.RootChancePercent, AffixPool.Weapon);

            // A flag affix: the conversion rate is FightTuning's, not authored.
            rows.Add(EffectKind.ManaToWardOnTurnStartPercent, new EffectInfo(
                EffectKind.ManaToWardOnTurnStartPercent, EffectStacking.Flag, AffixOnly, tierScaled: false,
                AffixPool.Armour, usesMagnitude: false, usesThreshold: false, EffectTarget.None,
                EffectStat.None, percent: false));

            Mechanic(EffectKind.DualWield, usesMagnitude: false, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.MagicalShield, usesMagnitude: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.Bloodlust, usesMagnitude: false);
            Mechanic(EffectKind.ChargingCrystal, usesMagnitude: true, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.BallerinasSlippers, usesMagnitude: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.TinFoilPipe, usesMagnitude: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.ToothedNecklace, usesMagnitude: true);
            Mechanic(EffectKind.BountyHunterContract, usesMagnitude: true);
            Mechanic(EffectKind.SwordInABox, usesMagnitude: false, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.LuckyDeck, usesMagnitude: false, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.DrownedLantern, usesMagnitude: true);
            Mechanic(EffectKind.FirstRune, usesMagnitude: false, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.SaltLedger, usesMagnitude: true);
            Mechanic(EffectKind.LongCount, usesMagnitude: true, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.MagicMarker, usesMagnitude: true, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.JarOfBearUrine, usesMagnitude: false);
            Mechanic(EffectKind.WorldEndersCrown, usesMagnitude: false, usesThreshold: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.CursedIdol, usesMagnitude: true);
            Mechanic(EffectKind.AmassingStar, usesMagnitude: true);
            Mechanic(EffectKind.RampagingBullsHorn, usesMagnitude: true);
            // The slow is re-read when a stack falls off on the TARGET's own
            // tick, where no holder is in reach, so its size stays
            // FightTuning.IceFingernailPercentPerStack.
            Mechanic(EffectKind.IceFingernail, usesMagnitude: false, traitPool: AffixPool.Weapon);
            Mechanic(EffectKind.LoadedDice, usesMagnitude: false);
            Mechanic(EffectKind.MonkeyKingsScepter, usesMagnitude: false);
            Mechanic(EffectKind.SparringSaber, usesMagnitude: true);
            Mechanic(EffectKind.SparringBuckler, usesMagnitude: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.EssenceSiphon, usesMagnitude: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.DisgruntledLackey, usesMagnitude: false);
            Mechanic(EffectKind.InconspicuousKey, usesMagnitude: false);
            Mechanic(EffectKind.DancersAnklet, usesMagnitude: false, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.BerserkersVest, usesMagnitude: true, traitPool: AffixPool.Armour);
            Mechanic(EffectKind.PhoenixEgg, usesMagnitude: true);
            Mechanic(EffectKind.Kinship, usesMagnitude: false);
            // Wrath's talent and the Bellwether's Bell add.
            rows.Add(EffectKind.TransformDurationBonus, new EffectInfo(EffectKind.TransformDurationBonus,
                EffectStacking.BestAffixPlusOthers, RelicOrTalent, tierScaled: false, AffixPool.None,
                usesMagnitude: true, usesThreshold: false, EffectTarget.None, EffectStat.None, percent: false,
                skillScoped: false, summedMechanic: true));
            Mechanic(EffectKind.TollOfTheFlock, usesMagnitude: true);


            Talent(EffectKind.WoolOnHitTaken, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WoolPerTurnBelowHealth, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.WoolWhenWardedAllyHit, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WoolPerStatusedEnemy, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.IgnoreDefensePercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShredDefenseOnHit, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ProvokedDamageReductionPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ProvokeHitsEveryEnemy, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WoolPerProvokedEnemy, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ExecuteDamageBonusPercent, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.ExtraAttackOnKill, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.TransformExtendOnKill, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.TransformHoldsBelowHealth, usesMagnitude: false, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.DefenseBonusPercentBelowHealth, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.DamageCapPercentBelowHealth, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.CheatDeathOncePerFight, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.HeadbuttCancelsIntent, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.TransformPushesEveryEnemy, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.TransformPermanentBelowHealth, usesMagnitude: false, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.WardReductionPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardIsFreeAction, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardAlsoAppliesRegen, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.WardHealsWhenSpent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardDamageBonusPerAlly, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardDamageBonusSelf, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardSelfBonusPersistsTurns, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardSpreadsToAllies, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardSpreadsToWholeParty, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.GiftManaPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.GiftAttackBonusPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.GiftAppliesImmediateTurn, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShatterDamagePercentOfAttack, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShatterSelfWardMultiplier, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShatterAppliesVulnerable, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.WardsNeverExpire, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.FuryEngineEinherjar, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.SlamCritRestoresFury, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.SlamCritChanceAtFury, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.SlamIgnoresDefenseAtFullFury, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.FuryOnKill, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.FirstIdleTurnFree, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.CritFuryBonusPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.MomentumTier, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.CritChanceBelowTargetHealth, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.KillFillsMomentum, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.CritChanceWhileTransformed, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.FuryOnKillWhileTransformed, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.BattleTranceTier, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.TwinRampage, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.FuryEngineJuggernaut, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.SkillCooldownTurns, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.HealthCurveRegen, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.DamagePerMissingHealth, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.SkillLifestealPercent, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.SkillDamageBonusBelowOwnHealth, usesMagnitude: true, usesThreshold: true, skillScoped: true);
            Talent(EffectKind.UnyieldingTier, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.DelayedDamagePercent, usesMagnitude: true, usesThreshold: true, skillScoped: false);
            Talent(EffectKind.HealReducesDelayedDamage, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShortfallPaidInHealthPermille, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.BloodPaidDamagePercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.CheatDeathFillsPrimary, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.FuryEngineSentinel, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.IronRetortPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.PlantedShieldBreakShards, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShieldBashShortWait, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ShieldwallCoversParty, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ThornsPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ReflectMagicPercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.SilenceCasterOnSpellHit, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.ReflectGrantsFury, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.SlowsAttacker, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.DisarmsOnBreak, usesMagnitude: false, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.SkillStatusTurns, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.SkillCleansesDebuffs, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.SkillSplashPercent, usesMagnitude: true, usesThreshold: false, skillScoped: true);
            Talent(EffectKind.PrimaryGainWhenBuffedAllyHit, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.TargetPreferencePercent, usesMagnitude: true, usesThreshold: false, skillScoped: false);
            Talent(EffectKind.PrimaryGainPerProvokedEnemy, usesMagnitude: true, usesThreshold: false, skillScoped: false);

            return rows;
        }
    }
}
