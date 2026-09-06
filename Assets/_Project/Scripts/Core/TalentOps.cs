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
        // when TalentPage refuses it -- unauthored, already taken, a
        // prerequisite or gate unmet, or not enough embers.
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

            if (!TalentPage.CanInvest(tree, path, slot, unlocked, character.embers)) return false;

            var taken = tree.At(path, slot);
            character.unlockedTalentIds.Add(taken.Id);
            character.embers -= taken.Cost;
            return true;
        }
    }
}
