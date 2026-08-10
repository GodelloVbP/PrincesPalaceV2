using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // What clearing a room pays out.
    //
    // Ported from the arithmetic half of v1's FightController.ResolveVictory,
    // which computed this inline between a GameplayManager lookup and a save
    // write -- so the two scaling rules below, both of which decide whether the
    // whole descent is worth making, could only be checked by playing a fight.
    //
    // What deliberately did NOT come across is the persistence: applying
    // experience to a save-side Character, adding gold to the run, and writing
    // the save. Those are Core's, and they stay Core's. This answers "how much",
    // and the caller decides where to put it.
    public static class VictoryRewards
    {
        // Elites fight scaled up, so their reward scales the same way --
        // otherwise a genuinely harder fight would pay out exactly the same as
        // an ordinary one. The same figure the elite stat scaling uses, kept
        // beside it in intent rather than in code because they answer two
        // different questions that happen to share a number today.
        public const float EliteRewardMultiplier = 1.56f;

        // How often an ordinary enemy leaves a consumable behind.
        public const float ItemDropChance = 0.3f;

        // A fight's total experience and gold, before anything is spent.
        public readonly struct Payout
        {
            public readonly int Experience;
            public readonly int Gold;

            public Payout(int experience, int gold)
            {
                Experience = experience;
                Gold = gold;
            }
        }

        // Sums the room and applies both multipliers.
        //
        // DEPTH scales it as well as elite status, and both halves matter:
        // without the depth curve a step-40 fight would cost five times the
        // effort of a step-1 one and pay exactly the same, so the optimal way to
        // play an endless dungeon would be never to descend.
        public static Payout For(IEnumerable<EnemyKit> defeated, bool isElite, int depthStep)
        {
            if (defeated == null) return new Payout(0, 0);

            float multiplier = isElite ? EliteRewardMultiplier : 1f;

            int rawExp = 0;
            int rawGold = 0;
            foreach (var kit in defeated)
            {
                if (kit == null) continue;
                rawExp += kit.Source.ExpReward;
                rawGold += kit.Source.CurrencyReward;
            }

            return new Payout(
                DifficultyCurve.ScaleReward(Rounding.AwayFromZero(rawExp * multiplier), depthStep),
                DifficultyCurve.ScaleReward(Rounding.AwayFromZero(rawGold * multiplier), depthStep));
        }

        // Which enemies dropped something, and what.
        //
        // Returns the (enemy, item id) pairs rather than mutating an inventory,
        // so the roll is testable without a run in hand. One roll per enemy, in
        // roster order, which is what makes a seeded run reproduce its own loot.
        public static IReadOnlyList<Drop> RollConsumableDrops(
            IEnumerable<EnemyKit> defeated, IReadOnlyList<string> consumableIds, SeededRandom rng)
        {
            var drops = new List<Drop>();
            if (defeated == null || consumableIds == null || consumableIds.Count == 0 || rng == null)
            {
                return drops;
            }

            foreach (var kit in defeated)
            {
                if (kit == null) continue;
                if (rng.NextFloat() >= ItemDropChance) continue;

                drops.Add(new Drop(kit.Source.DisplayName, consumableIds[rng.NextInt(0, consumableIds.Count)]));
            }

            return drops;
        }

        public readonly struct Drop
        {
            public readonly string FromEnemyName;
            public readonly string ItemId;

            public Drop(string fromEnemyName, string itemId)
            {
                FromEnemyName = fromEnemyName;
                ItemId = itemId;
            }
        }

        // Whether this fight guarantees a weapon.
        //
        // Elite AND boss rooms both do. Boss alone used to make weapons
        // effectively unequippable, because the boss was the last room and
        // clearing it ended the run before the weapon could be used. Clearing a
        // boss now descends to the next floor, so boss loot is usable -- but
        // elite rooms keep their drop regardless: a player who never reaches a
        // boss should still find gear.
        public static bool GuaranteesWeapon(bool isBossFight, bool isEliteFight) =>
            isBossFight || isEliteFight;

        // Experience is NOT split across the party -- every FIELDED character
        // receives the full amount, and a character who was not fielded receives
        // none. A pre-existing design decision, made visible here rather than
        // changed.
        public static int ExperienceFor(Payout payout, bool isDowned) =>
            isDowned ? 0 : payout.Experience;
    }
}
