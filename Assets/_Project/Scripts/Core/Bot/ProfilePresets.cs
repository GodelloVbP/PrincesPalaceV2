using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace
{
    // THE SAVE A BATCH PLAYS FROM, and the answer to "which profile were these
    // numbers measured on".
    //
    // docs/PLAN_BALANCE_BOT.md F5: a run's power is the profile (level, track
    // rewards, stat points, talents) plus whatever it picks up in-run, and
    // everything in the first list survives death while nothing in the second
    // does. A depth median with no profile attached to it is therefore not a
    // number at all, which is why the batch names one and this builds it.
    //
    // Deliberately built rather than loaded. A preset assembled from a file
    // somebody hand-edited would drift from the game's own idea of a save the
    // moment SaveData.Reconcile gained a step, so these go through exactly the
    // doors the game uses -- SaveData.CreateNew via SaveSystem.Load's
    // missing-file path, then Character.ClaimTrackRewards, Character.Invest and
    // TalentOps.Kindle.
    //
    // BUILT BY AN ARCHETYPE, not by a house rule. Points and talents used to be
    // spent by a fixed round-robin, on the argument that any other rule is a
    // BUILD and a build is a strategy the batch would be measuring instead of
    // the game. That argument does not survive contact with the thing the batch
    // is FOR: the archetype gap. A GreedyDefensive that fights defensively and
    // then spends its levels the same way GreedyAggressive does is not a
    // defensive player, and the gap between them is measured with half the
    // difference sanded off. The archetype is the variable; it now varies here
    // too, and the neutral spread is available as a fifth policy if a future
    // phase wants that comparison.
    public static class ProfilePresets
    {
        public const string Fresh = "Fresh";
        public const string Mid = "Mid";
        public const string Late = "Late";

        // Tuning inputs, not laws -- plan §5 assumes 20 and 60 and says so.
        // Named constants rather than literals because the batch prints them
        // and the report is read against them.
        public const int MidLevel = 20;
        public const int LateLevel = 60;

        public static readonly IReadOnlyList<string> All = new[] { Fresh, Mid, Late };

        public static bool IsKnown(string profile) =>
            profile == Fresh || profile == Mid || profile == Late;

        public static int LevelFor(string profile)
        {
            switch (profile)
            {
                case Mid: return MidLevel;
                case Late: return LateLevel;
                default: return 1;
            }
        }

        // ---- the ember budget -------------------------------------------------------

        // HOW MANY EMBERS A PROFILE HOLDS, and the assumption is worth stating
        // in full because it is the shortest chapter in the game's economy.
        //
        // Embers are paid ONLY at RunSettlement, one per boss THIS CHARACTER'S
        // save had never killed before (EmberPayout.PerUniqueBoss), and the
        // payout is recorded against save.defeatedBossIds in the same pass. So
        // the lifetime supply is not a rate at all -- it is a fixed number,
        // equal to the count of distinct live boss definitions in the content,
        // and EmberPayout's own header says so ("the total embers obtainable in
        // the game is FIXED at the number of unique bosses").
        //
        // THE RULE, then: Fresh has killed nothing and holds none. Mid and Late
        // hold the lifetime maximum -- a character at level 20 has run enough
        // descents to have met every boss the content has at least once, and
        // there is nothing further to earn afterwards, which is why Mid and
        // Late come out identical here rather than Late being richer.
        //
        // Counted off the content rather than hardcoded, so adding a boss moves
        // the budget with no edit here. What it will NOT move is the shape of
        // the finding: the content ships three live bosses against a tree whose
        // single path costs 45 and a per-character cap of 30
        // (ContentDatabase.EmberSpendCap). The talent tree is, today,
        // approximately unreachable, and the batch is about to say so.
        public static int EmbersFor(string profile)
        {
            if (LevelFor(profile) <= 1) return 0;

            int bosses = ContentDatabase.Enemies.Count(e => e != null && e.Data.IsBoss);

            // NOT CLAMPED TO EmberSpendCap ANY MORE, and the deletion is the
            // point rather than a tidy-up. This clamped the GRANT because
            // nothing clamped the SPEND: TalentPage.Evaluate asked the wallet
            // alone, so a preset handed 31 embers would have kindled 31 embers'
            // worth of tree and quietly reported a build the game cannot
            // produce. The cap is now a refusal on the same path the player
            // uses (TalentPage.Refusal.BudgetSpent), so the honest grant is the
            // whole payout and the gate stops the spend at 30 -- one rule, in
            // one place, instead of a second copy of it living here.
            //
            // A save CAN hold more than the cap, and always could: EmberPayout
            // pays one per unique boss with no reference to the cap, and the
            // wallet is deliberately shared and uncapped. What is capped is how
            // much of it any ONE character may commit.
            return EmberPayout.EmbersFor(bosses);
        }

        // ---- building one --------------------------------------------------------------

        // Builds the named profile onto whatever slot is current, and returns
        // it. THE CALLER OWNS THE SAVE ROOT: this is called under a throwaway
        // SaveSystem.RootOverride with SaveSlotManager.Forget() already done,
        // so CurrentSave takes SaveSystem.Load's missing-file branch and comes
        // back as SaveData.CreateNew() -- a new profile exactly as the game
        // makes one, Shawn fielded, starting stock granted. That IS the Fresh
        // preset; Mid and Late are that plus a level, its track rewards, its
        // stat points and whatever the ember budget buys.
        public static SaveData Build(string profile, IRunPolicy policy, SeededRandom rng)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null) return null;

            // Belt and braces. CreateNew already reconciles, but a slot that
            // somehow held a file would have come through Migrate instead, and
            // the two paths must leave the same shape.
            save.Reconcile();

            int level = LevelFor(profile);
            if (level > 1)
            {
                int embers = EmbersFor(profile);

                // THE FIELDED SQUAD ONLY, not the whole roster. The roster
                // holds every authored character; ActiveSquad is who actually
                // walks into the fight, and levelling a bench nobody fields
                // would put a number in the report that no run ever felt.
                foreach (var character in save.ActiveSquad())
                {
                    if (character == null) continue;

                    character.level = level;

                    // NOT AUTOMATIC, and that is deliberate in the game:
                    // RewardApplier leaves the track unclaimed so the player
                    // collects it themselves. A bot that skipped this would
                    // play a level 60 character with a level 1 character's
                    // stat points, max health and signature resource -- which
                    // is not "level 60" by any reading a balance report could
                    // use. The claim moves claimedTrackLevel, and that
                    // watermark is what every read site on the track sums
                    // against, so this one call is what makes the whole
                    // statline the level it says.
                    character.ClaimTrackRewards(RewardTracks.For(character), character.level);

                    SpendEveryPoint(character, policy, rng);

                    // AFTER the points, deliberately. A talent's value is
                    // measured against the character it lands on, and half the
                    // tree's stat blocks are worth more or less depending on
                    // what is already there -- spending in the other order
                    // would price every orb against a level-1 statline.
                    character.embers += embers;
                    BuyTalents(character, policy, rng);
                }
            }

            SaveSlotManager.SaveCurrent();
            return save;
        }

        // ---- spending the levels ---------------------------------------------------------

        // ONE POINT AT A TIME, asked of the archetype each time.
        //
        // Re-asked per point rather than solved once and applied N times,
        // because the answer legitimately moves: AbilityDerivation is linear,
        // but the WEAPON scaling that carries Strength and Intelligence into
        // damage is not the same shape as the health a Constitution point buys,
        // so the best next point at 0 spent is not always the best next point
        // at 40 spent. It is also what a player does.
        //
        // Character.Invest is the real door: it refuses when there is nothing
        // unspent, so the loop terminates on the character's own accounting
        // rather than on a count computed here that could disagree with it.
        private static void SpendEveryPoint(Character character, IRunPolicy policy, SeededRandom rng)
        {
            if (character == null) return;

            int guard = 0;

            // The guard is not the terminating condition -- Invest returning
            // false is. It is here because this runs thousands of times per
            // batch inside a headless process with no way to interrupt it, and
            // an accounting bug that made Invest succeed forever would hang
            // the whole run rather than fail one.
            while (character.unspentStatPoints > 0 && guard++ < 100000)
            {
                var options = GearEvaluator.StatOptionsFor(character);
                int index = policy?.ChooseStat(options, PresetView(character), rng) ?? -1;

                // A policy that declines, or names something off the end of the
                // list, still has to leave a terminating loop -- so an
                // out-of-range answer falls back to the first score rather than
                // spinning on an unspent point forever.
                var score = index >= 0 && index < options.Count
                    ? (AbilityScore)options[index].Score
                    : AbilityScores.All[0];

                if (!character.Invest(score)) break;
            }
        }

        // ---- spending the embers ----------------------------------------------------------

        // GREEDILY ALONG THE TREE, one orb at a time, from whatever is
        // affordable and unlocked right now.
        //
        // The frontier is TalentPage.Frontier per path -- the screen's own
        // "what can be pressed" -- filtered to what the wallet can pay for, and
        // each option carries what kindling it would actually derive
        // (GearEvaluator.DeltaForTalent) so the archetype ranks effects rather
        // than names.
        //
        // ONE PURCHASE PER LOOP, re-derived each time, because kindling opens
        // the next tier: solving the whole spend up front would buy a tier-1
        // orb three times and never reach the strand above it.
        private static void BuyTalents(Character character, IRunPolicy policy, SeededRandom rng)
        {
            if (character == null || policy == null || character.embers <= 0) return;

            var tree = TalentOps.BuildTree(character);

            // Bounded by the ember budget, which every purchase either spends
            // from or (for the two free landmark orbs) leaves alone -- so the
            // budget alone cannot bound the loop. EmberSpendCap is the honest
            // ceiling on how many orbs one character may ever hold.
            int guard = 0;

            // NOT `embers > 0`. Two orbs in every path -- the convergence and
            // the capstone -- cost nothing at all, and their price is a gate on
            // what is already spent instead (see ContentDatabase.OrbCost). A
            // wallet-empty loop condition would walk away from a free capstone
            // the character had already paid for in full.
            while (guard++ <= ContentDatabase.EmberSpendCap * 2)
            {
                var options = new List<TalentOption>();
                var coords = new List<(int Path, int Slot)>();

                var unlocked = new HashSet<string>(character.unlockedTalentIds);

                // RE-DERIVED EACH LOOP like the frontier itself, because each
                // purchase spends some of it. This is what stops the preset at
                // the lifetime cap now that EmbersFor no longer clamps the
                // grant -- the same refusal the screen's button reads.
                int budget = ContentDatabase.EmbersLeftFor(character);

                for (int path = 0; path < TalentPage.PathCount; path++)
                {
                    foreach (int slot in TalentPage.Frontier(tree, path, unlocked, budget))
                    {
                        var here = tree.At(path, slot);

                        // Frontier deliberately ignores the wallet -- it is
                        // about SHAPE (see its own header) -- so affordability
                        // is checked here, through the same CanInvest the
                        // screen's button reads rather than a second cost
                        // comparison that could disagree with it.
                        if (!TalentPage.CanInvest(tree, path, slot, unlocked,
                                                  character.embers, budget)) continue;

                        options.Add(new TalentOption(
                            here.Id, here.Name, here.Cost,
                            GearEvaluator.DeltaForTalent(character, here.Id)));
                        coords.Add((path, slot));
                    }
                }

                if (options.Count == 0) break;

                int index = policy.ChooseTalent(options, PresetView(character), rng);
                if (index < 0 || index >= options.Count) break;

                var (chosenPath, chosenSlot) = coords[index];
                if (!TalentOps.Kindle(character, tree, chosenPath, chosenSlot)) break;
            }
        }

        // A view with no run behind it. Every field a policy could read here is
        // genuinely unknown -- there is no descent yet -- so this says "full
        // health, step zero, floor one, nothing held" rather than reaching for
        // RunManager, which at preset-build time has no run at all.
        private static RunView PresetView(Character character) =>
            new RunView(1f, 0, 1, 0, null);
    }
}
