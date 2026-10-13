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

        // The reward track's elemental terms (ContentDatabase.Effects).
        Track = 4,
    }

    // Exactly one holder per set entry; EffectHolders is the allowed-mask.
    public enum EffectHolder
    {
        Relic,
        Affix,
        Track,
    }

    // Which item an affix of this kind may roll onto (ModifierTable).
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

        // An affix's magnitude rides ModifierMagnitude's tier x rift curve.
        public readonly bool TierScaled;
        public readonly AffixPool Pool;

        // False: a flag; authoring a magnitude is refused.
        public readonly bool UsesMagnitude;
        public readonly bool UsesThreshold;
        public readonly EffectTarget Target;

        // Set for a kind EffectFold folds into a stat.
        public readonly EffectStat Stat;
        public readonly bool Percent;

        public EffectInfo(EffectKind kind, EffectStacking stacking, EffectHolders holders,
            bool tierScaled, AffixPool pool, bool usesMagnitude, bool usesThreshold,
            EffectTarget target, EffectStat stat, bool percent)
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
        }

        public bool Allows(EffectHolder holder) => (Holders & ToMask(holder)) != 0;

        private static EffectHolders ToMask(EffectHolder holder)
        {
            switch (holder)
            {
                case EffectHolder.Relic: return EffectHolders.Relic;
                case EffectHolder.Affix: return EffectHolders.Affix;
                case EffectHolder.Track: return EffectHolders.Track;
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

            void Mechanic(EffectKind kind, bool usesMagnitude, bool usesThreshold = false)
            {
                rows.Add(kind, new EffectInfo(kind, EffectStacking.Flag, RelicOnly, tierScaled: false,
                    AffixPool.None, usesMagnitude, usesThreshold, EffectTarget.None, EffectStat.None, percent: false));
            }

            Stat(EffectKind.AttackPercent, EffectStat.Attack, percent: true);
            Stat(EffectKind.DefensePercent, EffectStat.Defence, percent: true);
            Stat(EffectKind.MaxHealthPercent, EffectStat.MaxHealth, percent: true);
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
                EffectTarget.Element);
            Rider(EffectKind.ElementalDamagePercent, AffixPool.Weapon, EffectStacking.Summed, AffixOrTrack,
                EffectTarget.Element);

            Rider(EffectKind.FlatPhysicalDamageReduction, AffixPool.Armour);
            Rider(EffectKind.BreakShieldDepletionResistPercent, AffixPool.Armour);
            Rider(EffectKind.OnKillSplashPercent, AffixPool.Weapon);
            Rider(EffectKind.PushBackOnHitChancePercent, AffixPool.Weapon);
            Rider(EffectKind.FlatManaRegenBonus, AffixPool.Armour);
            Rider(EffectKind.NextSkillManaDiscountPercent, AffixPool.Weapon);
            Rider(EffectKind.FortunateFavorBonusFlat, AffixPool.Armour);
            Rider(EffectKind.DodgeRating, AffixPool.Armour);
            Rider(EffectKind.ChilledOnHitChancePercent, AffixPool.Weapon);
            Rider(EffectKind.RootChancePercent, AffixPool.Weapon);

            // A flag affix: the conversion rate is FightTuning's, not authored.
            rows.Add(EffectKind.ManaToWardOnTurnStartPercent, new EffectInfo(
                EffectKind.ManaToWardOnTurnStartPercent, EffectStacking.Flag, AffixOnly, tierScaled: false,
                AffixPool.Armour, usesMagnitude: false, usesThreshold: false, EffectTarget.None,
                EffectStat.None, percent: false));

            Mechanic(EffectKind.DualWield, usesMagnitude: false);
            Mechanic(EffectKind.MagicalShield, usesMagnitude: true);
            Mechanic(EffectKind.Bloodlust, usesMagnitude: false);
            Mechanic(EffectKind.ChargingCrystal, usesMagnitude: true);
            Mechanic(EffectKind.BallerinasSlippers, usesMagnitude: true);
            Mechanic(EffectKind.TinFoilPipe, usesMagnitude: true);
            Mechanic(EffectKind.ToothedNecklace, usesMagnitude: true);
            Mechanic(EffectKind.BountyHunterContract, usesMagnitude: true);
            Mechanic(EffectKind.SwordInABox, usesMagnitude: false);
            Mechanic(EffectKind.LuckyDeck, usesMagnitude: false);
            Mechanic(EffectKind.DrownedLantern, usesMagnitude: true);
            Mechanic(EffectKind.FirstRune, usesMagnitude: false);
            Mechanic(EffectKind.SaltLedger, usesMagnitude: true);
            Mechanic(EffectKind.LongCount, usesMagnitude: true);
            Mechanic(EffectKind.MagicMarker, usesMagnitude: true);
            Mechanic(EffectKind.JarOfBearUrine, usesMagnitude: false);
            Mechanic(EffectKind.WorldEndersCrown, usesMagnitude: false, usesThreshold: true);
            Mechanic(EffectKind.CursedIdol, usesMagnitude: true);
            Mechanic(EffectKind.AmassingStar, usesMagnitude: true);
            Mechanic(EffectKind.RampagingBullsHorn, usesMagnitude: true);
            // The slow is re-read when a stack falls off on the TARGET's own
            // tick, where no holder is in reach, so its size stays
            // FightTuning.IceFingernailPercentPerStack.
            Mechanic(EffectKind.IceFingernail, usesMagnitude: false);
            Mechanic(EffectKind.LoadedDice, usesMagnitude: false);
            Mechanic(EffectKind.MonkeyKingsScepter, usesMagnitude: false);
            Mechanic(EffectKind.SparringSaber, usesMagnitude: true);
            Mechanic(EffectKind.SparringBuckler, usesMagnitude: true);
            Mechanic(EffectKind.EssenceSiphon, usesMagnitude: true);
            Mechanic(EffectKind.DisgruntledLackey, usesMagnitude: false);
            Mechanic(EffectKind.InconspicuousKey, usesMagnitude: false);
            Mechanic(EffectKind.DancersAnklet, usesMagnitude: false);
            Mechanic(EffectKind.BerserkersVest, usesMagnitude: true);
            Mechanic(EffectKind.PhoenixEgg, usesMagnitude: true);
            Mechanic(EffectKind.Kinship, usesMagnitude: false);
            Mechanic(EffectKind.BellwethersBell, usesMagnitude: true);
            Mechanic(EffectKind.TollOfTheFlock, usesMagnitude: true);

            return rows;
        }
    }
}
