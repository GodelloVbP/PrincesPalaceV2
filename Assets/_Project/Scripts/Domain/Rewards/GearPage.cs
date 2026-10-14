using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Rewards
{
    // One fielded member as the gear page sees them: who, and what each slot
    // holds now (null = empty).
    public sealed class PageMember
    {
        public readonly string Id;
        private readonly ItemInstance[] _worn = new ItemInstance[EquipmentSlots.All.Length];

        public PageMember(string id, IEnumerable<KeyValuePair<EquipmentSlot, ItemInstance>> worn = null)
        {
            Id = id ?? "";
            if (worn == null) return;
            foreach (var pair in worn)
            {
                _worn[(int)pair.Key] = pair.Value;
            }
        }

        public ItemInstance Worn(EquipmentSlot slot) => _worn[(int)slot];
        public bool IsEmpty(EquipmentSlot slot) => _worn[(int)slot] == null;

        public int EmptySlots
        {
            get
            {
                int empty = 0;
                foreach (var copy in _worn) if (copy == null) empty++;
                return empty;
            }
        }

        public int SummedTier
        {
            get
            {
                int sum = 0;
                foreach (var copy in _worn) if (copy != null) sum += copy.Tier;
                return sum;
            }
        }
    }

    // What the pipeline asks of content. Core answers from ContentDatabase;
    // a test answers with literals.
    public interface IGearCatalog
    {
        // Bases that can fill this slot, in content order. Weapon1 and
        // Weapon2 answer the same list.
        IReadOnlyList<string> BasesFor(EquipmentSlot slot);

        // Whether this base at this tier would be live (its requirements
        // hold) worn in `slot` on the member's current loadout.
        bool Fits(string memberId, string baseId, int tier, EquipmentSlot slot);

        // Affix ids the slot draws from: the weapon pool for a weapon slot,
        // the armour pool otherwise (the necklace included).
        IReadOnlyList<string> AffixPool(EquipmentSlot slot);
    }

    public sealed class GearPageRequest
    {
        public ResolvedRewardSource Source;

        // k: how many Gear chips merged into this page. k + Source.ExtraCards
        // cards are shown and k choices granted.
        public int Chips = 1;

        public int DepthStep;
        public int Favor;

        // The settled node's strength 1-3 against the party; 0 = none.
        public int Strength;

        // Fielded at settlement, fallen or not, in squad order.
        public IReadOnlyList<PageMember> Members;

        // Gear pages in a row with no Legendary card shown, before this one.
        public int LegendaryPity;

        // The previous gear page's best card was Common.
        public bool LastPageBestCommon;
    }

    public sealed class GearCard
    {
        public string OwnerId;

        // The slot the card fills on its owner (a weapon card already
        // resolved to Weapon1 or Weapon2).
        public EquipmentSlot Slot;

        public ItemInstance Copy;

        // The quality the ladder and floors landed on. The copy's own
        // quality (ItemQuality.Of) is what the face shows; the two differ
        // only for a Legendary roll before traits exist, which falls back
        // to Rare with a third affix.
        public Quality Rolled;

        // No base for the slot fits the owner's loadout: shown "inert until".
        public bool Inert;

        // The step-2 weapon card.
        public bool GuaranteedWeapon;
    }

    public sealed class GearPageRoll
    {
        public readonly List<GearCard> Cards = new List<GearCard>();

        // Index of the best card (highest quality, then tier, then first);
        // -1 on an empty page.
        public int BestIndex = -1;

        // The counters the run carries to the next gear page.
        public int LegendaryPity;
        public bool BestWasCommon;
    }

    // THE GEAR PAGE PIPELINE (docs/PLAN_ITEMS.md 1.2 steps 1-8): owners,
    // weapon, slots, then per card tier, base and quality, then the floors,
    // then affixes. Pure: content comes in through IGearCatalog and
    // randomness through `nextIndex` (upper-bound exclusive), so a seed
    // pins every card.
    //
    // DRAW ORDER, which a seeded page depends on: one slot draw per card
    // whose owner already wears a Weapon1 and is not the weapon card; then
    // per card in order: tier (LootLadder.MaxRungs draws), base (one draw),
    // quality (rungs above the floor); then per card its affixes.
    public static class GearPage
    {
        // The seventh gear page in a row without a Legendary shown makes its
        // best card Legendary.
        public const int LegendaryPityPages = 7;

        // Quality's own Favor dial and cap (1.2 step 6).
        public const float QualityFavorPerPoint = LootLadder.FavorPerPoint;
        public const float QualityMaxStep = LootLadder.MaxStep;

        // What squad Favor adds to this source's two ladders, in whole points
        // of per-rung chance, after each dial's cap: the page header's Favor
        // line. A Favor of 20 on a normal page reads tier +12, quality +12.
        public static void FavorEffect(ResolvedRewardSource source, int favor, out int tierPoints, out int qualityPoints)
        {
            tierPoints = qualityPoints = 0;
            if (source == null) return;

            tierPoints = Points(source.TierStep,
                LootLadder.StepChance(source.TierStep, favor, RarityTable.TierFavorPerPoint, RarityTable.TierMaxStep));
            qualityPoints = Points(source.LadderStep,
                LootLadder.StepChance(source.LadderStep, favor, QualityFavorPerPoint, QualityMaxStep));
        }

        private static int Points(float bare, float withFavor) => Math.Max(0, (int)Math.Round((withFavor - bare) * 100.0));

        // Weight of an empty slot against a filled one in the slot draw.
        public const int EmptySlotWeight = 3;
        public const int FilledSlotWeight = 1;

        public static GearPageRoll Roll(GearPageRequest request, IGearCatalog catalog, Func<int, int> nextIndex)
        {
            var roll = new GearPageRoll();
            var source = request?.Source;
            if (source == null || catalog == null || nextIndex == null || request.Members == null)
            {
                return roll;
            }

            int cardCount = Math.Max(0, request.Chips) + source.ExtraCards;
            var owners = Owners(request.Members, cardCount);
            if (owners.Count == 0)
            {
                roll.LegendaryPity = request.LegendaryPity;
                roll.BestWasCommon = request.LastPageBestCommon;
                return roll;
            }

            bool hasWeapons = catalog.BasesFor(EquipmentSlot.Weapon1).Count > 0;

            // Step 2: the weapon card.
            int weaponCard = source.WeaponGuarantee && hasWeapons ? WeaponCardIndex(owners) : -1;

            // Step 3: slots.
            var slots = new EquipmentSlot[owners.Count];
            for (int i = 0; i < owners.Count; i++)
            {
                var owner = owners[i];
                if (i == weaponCard || (hasWeapons && owner.IsEmpty(EquipmentSlot.Weapon1)))
                {
                    slots[i] = WeaponTarget(owner);
                }
                else
                {
                    var drawn = DrawSlot(owner, catalog, nextIndex);
                    slots[i] = EquipmentSlots.IsWeaponSlot(drawn) ? WeaponTarget(owner) : drawn;
                }
            }

            // Steps 4-6, per card: tier, base, quality.
            var usedBases = new HashSet<string>(StringComparer.Ordinal);
            var tiers = new int[owners.Count];
            var bases = new string[owners.Count];
            var inert = new bool[owners.Count];
            var qualities = new Quality[owners.Count];
            for (int i = 0; i < owners.Count; i++)
            {
                tiers[i] = RarityTable.RollTier(source, request.DepthStep, request.Favor, nextIndex);
                bases[i] = PickBase(owners[i].Id, slots[i], tiers[i], catalog, usedBases, nextIndex, out inert[i]);
                if (bases[i] != null) usedBases.Add(bases[i]);
                qualities[i] = RollQuality(source, request.Favor, nextIndex);
            }

            // Step 7: floors.
            if (weaponCard >= 0 && qualities[weaponCard] < source.WeaponFloor)
            {
                qualities[weaponCard] = source.WeaponFloor;
            }

            int best = BestIndex(qualities, tiers);
            var floor = BestCardFloor(request.Strength, request.LastPageBestCommon);
            if (qualities[best] < floor) qualities[best] = floor;

            bool anyLegendary = Array.IndexOf(qualities, Quality.Legendary) >= 0;
            if (!anyLegendary && request.LegendaryPity >= LegendaryPityPages - 1)
            {
                qualities[best] = Quality.Legendary;
                anyLegendary = true;
            }

            // Step 8: affixes. Traits land in I2c; until then a Legendary
            // takes a third affix instead (the plan's "none left" rule).
            for (int i = 0; i < owners.Count; i++)
            {
                if (bases[i] == null) continue;

                var affixes = ModifierTable.PickModifiers(catalog.AffixPool(slots[i]), AffixCount(qualities[i]), nextIndex);
                roll.Cards.Add(new GearCard
                {
                    OwnerId = owners[i].Id,
                    Slot = slots[i],
                    Copy = ItemInstance.Of(bases[i], tiers[i], affixes),
                    Rolled = qualities[i],
                    Inert = inert[i],
                    GuaranteedWeapon = i == weaponCard,
                });
            }

            roll.BestIndex = BestIndexOf(roll.Cards);
            roll.LegendaryPity = anyLegendary ? 0 : request.LegendaryPity + 1;
            roll.BestWasCommon = roll.BestIndex >= 0 && roll.Cards[roll.BestIndex].Rolled == Quality.Common;
            return roll;
        }

        // Step 1. Members in squad order, cycling until `cards` owners are
        // named. With fewer than three members the cycle starts with the one
        // who has the most empty slots (ties: lowest summed tier, then squad
        // order).
        public static List<PageMember> Owners(IReadOnlyList<PageMember> members, int cards)
        {
            var owners = new List<PageMember>();
            var present = new List<PageMember>();
            if (members != null)
            {
                foreach (var m in members) if (m != null) present.Add(m);
            }

            if (present.Count == 0 || cards <= 0) return owners;

            int start = 0;
            if (present.Count < 3)
            {
                for (int i = 1; i < present.Count; i++)
                {
                    var a = present[i];
                    var b = present[start];
                    if (a.EmptySlots > b.EmptySlots ||
                        (a.EmptySlots == b.EmptySlots && a.SummedTier < b.SummedTier))
                    {
                        start = i;
                    }
                }
            }

            for (int i = 0; i < cards; i++)
            {
                owners.Add(present[(start + i) % present.Count]);
            }

            return owners;
        }

        // Step 2: the first card whose owner has an empty Weapon1, else the
        // first card whose owner wears the lowest-tier weapon.
        public static int WeaponCardIndex(IReadOnlyList<PageMember> owners)
        {
            int best = -1;
            int bestTier = int.MaxValue;
            for (int i = 0; i < owners.Count; i++)
            {
                if (owners[i].IsEmpty(EquipmentSlot.Weapon1)) return i;

                int tier = LowestWeaponTier(owners[i]);
                if (tier < bestTier)
                {
                    bestTier = tier;
                    best = i;
                }
            }

            return best;
        }

        // A weapon card fills Weapon1 if empty, else Weapon2 if empty, else
        // replaces the lower-tier weapon (a tie replaces Weapon2, the off
        // hand).
        public static EquipmentSlot WeaponTarget(PageMember owner)
        {
            if (owner.IsEmpty(EquipmentSlot.Weapon1)) return EquipmentSlot.Weapon1;
            if (owner.IsEmpty(EquipmentSlot.Weapon2)) return EquipmentSlot.Weapon2;
            return owner.Worn(EquipmentSlot.Weapon1).Tier < owner.Worn(EquipmentSlot.Weapon2).Tier
                ? EquipmentSlot.Weapon1
                : EquipmentSlot.Weapon2;
        }

        // Step 3's weighted draw over every slot with bases: 3 an empty slot,
        // 1 a filled one; Weapon2 only once Weapon1 is filled.
        public static EquipmentSlot DrawSlot(PageMember owner, IGearCatalog catalog, Func<int, int> nextIndex)
        {
            var slots = new List<EquipmentSlot>();
            var weights = new List<int>();
            int total = 0;
            foreach (var slot in EquipmentSlots.All)
            {
                if (catalog.BasesFor(slot).Count == 0) continue;
                if (slot == EquipmentSlot.Weapon2 && owner.IsEmpty(EquipmentSlot.Weapon1)) continue;

                int w = owner.IsEmpty(slot) ? EmptySlotWeight : FilledSlotWeight;
                slots.Add(slot);
                weights.Add(w);
                total += w;
            }

            if (total == 0) return EquipmentSlot.Weapon1;

            int roll = Clamp(nextIndex(total), total);
            for (int i = 0; i < slots.Count; i++)
            {
                if (roll < weights[i]) return slots[i];
                roll -= weights[i];
            }

            return slots[slots.Count - 1];
        }

        // Step 4: uniform over the slot's bases that fit the owner at this
        // tier, distinct across the page. None fit: uniform over the rest,
        // shown inert. Always one draw.
        private static string PickBase(string ownerId, EquipmentSlot slot, int tier, IGearCatalog catalog,
            HashSet<string> used, Func<int, int> nextIndex, out bool inert)
        {
            var all = catalog.BasesFor(slot);
            var unused = new List<string>();
            var fitting = new List<string>();
            foreach (var id in all)
            {
                if (used.Contains(id)) continue;
                unused.Add(id);
                if (catalog.Fits(ownerId, id, tier, slot)) fitting.Add(id);
            }

            inert = fitting.Count == 0;
            var pool = fitting.Count > 0 ? fitting : unused.Count > 0 ? unused : new List<string>(all);
            if (pool.Count == 0)
            {
                inert = false;
                return null;
            }

            return pool[Clamp(nextIndex(pool.Count), pool.Count)];
        }

        // Step 6: the source's floor, then LootLadder.Climb over the rungs
        // left above it at ladderStep plus Favor.
        public static Quality RollQuality(ResolvedRewardSource source, int favor, Func<int, int> nextIndex)
        {
            int floor = (int)source.QualityFloor;
            int rungs = (int)Quality.Legendary - floor;
            float step = LootLadder.StepChance(source.LadderStep, favor, QualityFavorPerPoint, QualityMaxStep);
            return (Quality)(floor + LootLadder.Climb(step, rungs, nextIndex));
        }

        // Step 7's best-card floor: strength 2 Uncommon, 3 Rare; after a page
        // whose best card was Common, Uncommon.
        public static Quality BestCardFloor(int strength, bool lastPageBestCommon)
        {
            var floor = Quality.Common;
            if (strength >= 3) floor = Quality.Rare;
            else if (strength == 2) floor = Quality.Uncommon;
            if (lastPageBestCommon && floor < Quality.Uncommon) floor = Quality.Uncommon;
            return floor;
        }

        // Step 8 counts. Legendary is 2 + a trait; with no trait drawn
        // (always, until I2c) it is a third affix.
        public static int AffixCount(Quality quality)
        {
            switch (quality)
            {
                case Quality.Uncommon: return 1;
                case Quality.Rare: return 2;
                case Quality.Legendary: return 3;
                default: return 0;
            }
        }

        private static int LowestWeaponTier(PageMember owner)
        {
            int tier = int.MaxValue;
            var w1 = owner.Worn(EquipmentSlot.Weapon1);
            var w2 = owner.Worn(EquipmentSlot.Weapon2);
            if (w1 != null) tier = Math.Min(tier, w1.Tier);
            if (w2 != null) tier = Math.Min(tier, w2.Tier);
            return tier;
        }

        private static int BestIndex(Quality[] qualities, int[] tiers)
        {
            int best = 0;
            for (int i = 1; i < qualities.Length; i++)
            {
                if (qualities[i] > qualities[best] ||
                    (qualities[i] == qualities[best] && tiers[i] > tiers[best]))
                {
                    best = i;
                }
            }

            return best;
        }

        private static int BestIndexOf(List<GearCard> cards)
        {
            int best = -1;
            for (int i = 0; i < cards.Count; i++)
            {
                if (best < 0 || cards[i].Rolled > cards[best].Rolled ||
                    (cards[i].Rolled == cards[best].Rolled && cards[i].Copy.Tier > cards[best].Copy.Tier))
                {
                    best = i;
                }
            }

            return best;
        }

        private static int Clamp(int roll, int bound) => roll < 0 ? 0 : roll >= bound ? bound - 1 : roll;
    }
}
