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

        // Every fight's gold, and Treasure's, before ScaleReward, in
        // thousandths (docs/PLAN_ITEMS.md 1.6). 1000 pays today's figure; the
        // balance pass sets it against the 120 +/- 20 step-0 gold a realm target.
        public const int GoldIncomePermille = 1000;

        // `gold` after the income cut, rounded half away from zero like the
        // elite multiplier before it.
        public static int Income(int gold) =>
            Rounding.AwayFromZero(gold * (GoldIncomePermille / 1000f));

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
        public static Payout For(IEnumerable<EnemyKit> defeated, bool isElite, int depthStep) =>
            ForEnemies(defeated?.Where(kit => kit != null).Select(kit => kit.Source), isElite, depthStep);

        // The same sum from the enemies themselves, so the map can quote a
        // fight's base payout before any kit exists. One formula, two callers.
        public static Payout ForEnemies(IEnumerable<Content.ResolvedEnemy> defeated, bool isElite, int depthStep)
        {
            if (defeated == null) return new Payout(0, 0);

            float multiplier = isElite ? EliteRewardMultiplier : 1f;

            int rawExp = 0;
            int rawGold = 0;
            foreach (var enemy in defeated)
            {
                if (enemy == null) continue;
                rawExp += enemy.ExpReward;
                rawGold += enemy.CurrencyReward;
            }

            // TWO CURVES, ONE ROOM. The elite multiplier and the rounding are
            // shared; the depth rate is not. Experience rides
            // ScaleExperience (25 permille a step) and gold rides ScaleReward
            // (the health rate, 75) -- see DifficultyCurve's own headers for
            // why the two were separated, and note the ORDER is unchanged:
            // elite first, then round, then the income cut, then depth, which is what
            // FightRewardsTests.TheEliteMultiplierIsAppliedBeforeTheDepthCurve
            // pins.
            return new Payout(
                DifficultyCurve.ScaleExperience(Rounding.AwayFromZero(rawExp * multiplier), depthStep),
                DifficultyCurve.ScaleReward(Income(Rounding.AwayFromZero(rawGold * multiplier)), depthStep));
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

        // The Book chip's draw (docs/PLAN_ITEMS.md 1.1): one book from the
        // pool the caller built (already minus books waiting unassigned or
        // known to every fielded member), uniform, no chance gate. Domain does
        // not reach ContentDatabase, so the pool is passed in. Null for an
        // empty pool.
        public static string DrawSpell(IReadOnlyList<string> bookSkillIds, SeededRandom rng)
        {
            if (bookSkillIds == null || bookSkillIds.Count == 0 || rng == null) return null;
            return bookSkillIds[rng.NextInt(0, bookSkillIds.Count)];
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
        // receives the full amount. A pre-existing design decision, made
        // visible here rather than changed.
        //
        // A DOWNED CHARACTER NOW EARNS HALF, ROUNDED UP, where they used to
        // earn nothing. Progression v2's contract 4
        // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §1): "downed at
        // a victory pays half, rounded up; never zero". The reasoning is the
        // one the plan's knockout trajectory measures -- a player who is
        // losing fights is the player who most needs the levels that would
        // let them stop losing, and zero pay compounds a bad run into a bad
        // career. Half still costs them something real: the plan's
        // downed-at-every-boss career reaches level 30 nine runs later than
        // the standing one.
        //
        // ROUNDED UP rather than down, so the floor is 1 and not 0 -- "never
        // zero" is the half of the contract that matters at room 0, where a
        // rat-only fight pays 15 and a rounded-down half of a 1-experience
        // payout would be the old rule wearing a new name.
        //
        // WHAT isDowned ACTUALLY MEANS is decided by the caller, and it is
        // narrower than the plan's words: RewardApplier reads it as "not in
        // the fielded list", and the fielded list is
        // RunOrchestrator.FieldedIds -- every member of the encounter's
        // PlayerParty. A squad member at 0 health when the encounter was BUILT
        // is left out and is therefore downed; a member who falls during the
        // fight is still in PlayerParty and collects in full. So "revived
        // before victory counts as standing" holds trivially, and so does
        // rather more than that. Reported to the owner rather than changed
        // here, because widening it is a gameplay decision and not this
        // commit's.
        public static int ExperienceFor(Payout payout, bool isDowned)
        {
            if (!isDowned) return payout.Experience;

            int experience = payout.Experience;
            return experience <= 0 ? 0 : (experience + 1) / 2;
        }
    }
}
