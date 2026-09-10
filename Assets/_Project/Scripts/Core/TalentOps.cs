using PrincesPalace.Content;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace
{
    // KINDLING AN ORB, WITHOUT A SCREEN AROUND IT.
    //
    // The rule was inside TalentController: build the character's tree out of
    // content, ask TalentPage.CanInvest, then write the id and subtract the
    // cost. Three of those four steps are the game's, and only the fourth --
    // the beat, the repaint, the save write -- is the screen's. Nothing that is
    // not a MonoBehaviour could reach any of it, which is why the balance bot
    // played every profile with an empty talent tree and reported
    // buildDiversity.distinctTalentSets = 1 for every cell.
    //
    // Same extraction, same bargain, as RunOrchestrator (see its header): the
    // bodies move down, the screen becomes a caller, and the bot is the second
    // caller rather than a second rulebook. Pinned first by
    // TalentInvestmentTests, which drives the real screen and asserts the id
    // written is one talents.json actually has.
    //
    // DOES NOT PERSIST, for the same reason EquipmentOps does not: the screen
    // writes immediately (a talent tree that loses a kindled orb to a crash is
    // the least forgivable thing it could do), and the bot's preset build
    // writes once at the end of the whole spend.
    public static class TalentOps
    {
        // THE CHARACTER'S OWN TREE, resolved from content.
        //
        // column -> path and row -> slot, which is what the content has always
        // meant by those fields: TalentEntryResolver refuses a row outside
        // 0..20, and the skeleton is 21 slots.
        //
        // Walks every talent the character has and costs each one, so it is
        // work to cache rather than repeat -- the screen holds it against the
        // character id it was built for; the bot builds it once per preset.
        public static TalentTree BuildTree(Character character)
        {
            var tree = new TalentTree();
            if (character == null) return tree;

            foreach (var talent in ContentDatabase.TalentsFor(character))
            {
                if (talent == null) continue;

                tree.Set(talent.Data.Column, talent.Data.Row, new TalentSlot(
                    talent.id,
                    talent.Data.DisplayName,
                    talent.Data.Description,
                    ContentDatabase.OrbCost(talent),
                    talent.Data.MinSpent));
            }

            return tree;
        }

        // Spends this character's embers on one orb. False, changing nothing,
        // when TalentPage refuses it -- unauthored, already taken, an
        // allegiance sworn elsewhere, a prerequisite or gate unmet, the
        // lifetime budget spent, or not enough embers.
        //
        // THE CONTENT'S OWN ID, which is the entire point of this seam.
        // Everything that reads unlockedTalentIds -- the effective stats, the
        // ability scores, the combat effect set -- matches against the ids in
        // talents.json, and this used to write one the screen had invented.
        //
        // `unlocked`/`embers` are read off the character rather than passed in,
        // unlike TalentPage.CanInvest's own parameters: TalentPage is Domain
        // and cannot see a Character at all, while this is the Core half whose
        // whole job is knowing where those two numbers are kept. Embers are
        // THIS CHARACTER'S, never a shared pool -- reading a pool here is what
        // let a character you never fielded be kindled out of someone else's
        // earnings.
        public static bool Kindle(Character character, TalentTree tree, int path, int slot)
        {
            if (character == null || tree == null) return false;

            var unlocked = new System.Collections.Generic.HashSet<string>(
                character.unlockedTalentIds ?? new System.Collections.Generic.List<string>());

            // TWO NUMBERS, NOT ONE. The wallet is what this character holds;
            // the budget is what they may still commit against
            // ContentDatabase.EmberSpendCap, and until this passed the second
            // one the cap was enforced nowhere on the player's path -- only in
            // the balance bot's preset builder, which clamped its own grant.
            // Both are read off the character here for the same reason the
            // wallet already was: TalentPage is Domain and cannot see a
            // Character or a catalogue, and this is the Core half whose whole
            // job is knowing where those numbers are kept.
            if (!TalentPage.CanInvest(tree, path, slot, unlocked,
                                      character.embers,
                                      ContentDatabase.EmbersLeftFor(character))) return false;

            var taken = tree.At(path, slot);

            return RescalingCarriedHealth(character, () =>
            {
                character.unlockedTalentIds.Add(taken.Id);
                character.embers -= taken.Cost;
                return true;
            });
        }

        // GIVING BACK EVERY CHOICE, with the same half Character.Respec cannot
        // reach on its own.
        //
        // Character is Data. It cannot call ContentDatabase or SaveSlotManager
        // -- its own ClaimTrackRewards comment states that discipline outright
        // ("a convenience overload that resolved its own track would make this
        // the one method on Character reaching into a content lookup on its own
        // initiative") -- and ScaleCarriedHealth needs both. So the Core half
        // lives HERE, exactly the shape EquipmentOps already has, rather than
        // as a fourth copy of the measure/rescale pair at the call site.
        //
        // A respec clears unlockedTalentIds AND investedAbilityScores, and both
        // move max health: talents through TalentDefinition.StatBonus, invested
        // Constitution at 20 health a point through AbilityDerivation. Neither
        // was rescaled.
        public static RespecRefund Respec(Character character, int embersSpent)
        {
            if (character == null) return new RespecRefund(0, 0);

            var refund = new RespecRefund(0, 0);
            RescalingCarriedHealth(character, () =>
            {
                refund = character.Respec(embersSpent);
                return true;
            });

            return refund;
        }

        // THE PAIR, ONCE. Measure the maximum, make the change, rescale what
        // the run is carrying against the maximum that just moved.
        //
        // RunEncounter.ScaleCarriedHealth's header states the rule in the
        // widest possible terms -- "the run stores current health as an
        // ABSOLUTE number per character, and the maximum it is a fraction of is
        // computed from the character -- so anything that changes the maximum
        // silently changes the fraction" -- and until this, only gear obeyed
        // it. Kindling an orb and confirming a respec both moved the maximum
        // and left the run holding the old absolute: kindle a +20 max health
        // talent at 50 of 100 and the run carries 50 against 120, growing the
        // permanently-empty tail that header describes.
        //
        // A LAMBDA RATHER THAN THREE LINES AT EACH SITE, for the reason
        // EquipmentOps gives for existing at all: that pair "had three copies
        // ... which is what three copies of a rule look like just before one of
        // them is edited alone". Two more copies here would be five.
        //
        // The change reports whether it happened, and a refusal skips the
        // rescale: a Kindle that CanInvest turned down moved nothing, and
        // rescaling against an unchanged maximum is a no-op that still costs an
        // EffectiveStats resolve.
        private static bool RescalingCarriedHealth(Character character, System.Func<bool> change)
        {
            int maxBefore = ContentDatabase.EffectiveStats(character).maxHealth;

            if (!change()) return false;

            RunEncounter.ScaleCarriedHealth(character, maxBefore);
            return true;
        }
    }
}
