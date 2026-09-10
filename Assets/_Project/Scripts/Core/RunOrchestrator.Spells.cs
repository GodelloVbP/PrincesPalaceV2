using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // ASSIGNING A BOUGHT OR DROPPED BOOK TO A SLOT (docs/PLAN_SHOP.md §1d,
    // §1g). Called by CharacterDossierController, not the shop -- a shop
    // purchase only ever appends to run.unassignedSpellBooks
    // (RunOrchestrator.Shop.cs's BuyBook); this file is what moves an entry
    // out of that pool and into a character's three slots.
    //
    // SAME THREE-PART SHAPE AS EVERY SHOP MUTATION (§2f): validate touching
    // nothing, apply unable to fail, persist exactly once. The duplicate
    // refusal (a character already knowing this skillId) is decided in step
    // 1 on both entry points, same as a purchase's purse check.
    public static partial class RunOrchestrator
    {
        // The lowest free slot index for `characterId`, or -1 if all
        // SpellBooks.MaxSpellSlots are occupied. Does not know or care which
        // skillId is being considered -- that is LearnSpell/ReplaceSpell's
        // question, once this one has answered "is there room".
        public static int CanLearn(string characterId)
        {
            // NO POOL, NO SLOTS (plan P6, gate 1). A character whose primary
            // pool refuses spell books has nowhere to put one, which is the
            // same answer this method already gives a character with three
            // full slots -- so every caller that already handles -1 handles
            // this without a second question. That is the whole reason the
            // refusal lives HERE and not at five call sites: LearnSpell's
            // lowest-free-slot overload, the shop card's "all slots full"
            // line and the bot's SpellAssignmentView all read CanLearn, and
            // none of them had to learn a new word.
            if (!ContentDatabase.CanHoldSpellBooks(characterId)) return -1;

            var run = RunManager.Run;
            var used = OccupiedSlots(run, characterId);

            for (int slot = 0; slot < SpellBooks.MaxSpellSlots; slot++)
            {
                if (!used.Contains(slot)) return slot;
            }

            return -1;
        }

        // Places one unassigned copy of `skillId` into `characterId`'s
        // lowest free slot. Refuses if the character already knows it, if
        // there is no unassigned copy, or if there is no free slot -- the
        // caller is expected to have checked CanLearn first and to offer
        // ReplaceSpell instead when it returns -1, so NoFreeSlot reaching a
        // player is a caller bug, not a normal refusal.
        public static ShopResult LearnSpell(string characterId, string skillId) =>
            LearnSpell(characterId, skillId, CanLearn(characterId));

        // Same mutation, an EXPLICIT target slot instead of "the lowest
        // free one" -- for a caller whose UI shows fixed, numbered slots
        // (the dossier's three chips): a press on the slot the player
        // actually looked at and clicked has to land there, not wherever
        // CanLearn's own tie-break happens to prefer. Refuses NoFreeSlot if
        // the named slot is already occupied -- that is what ReplaceSpell
        // is for.
        public static ShopResult LearnSpell(string characterId, string skillId, int slot)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            if (run == null) return ShopResult.Refused(ShopRefusal.NoShop);
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(skillId))
                return ShopResult.Refused(ShopRefusal.BadIndex);
            if (slot < 0 || slot >= SpellBooks.MaxSpellSlots) return ShopResult.Refused(ShopRefusal.NoFreeSlot);

            // THE SAME REFUSAL CanLearn GIVES, restated because this overload
            // does not go through it -- a caller who names the slot has
            // already decided there is one. The dossier hides the slot block
            // entirely for such a character, so nothing can press this today;
            // it is here so that a caller who does not paint a screen (a bot
            // policy, a later tool) cannot walk past the gate by naming an
            // index.
            if (!ContentDatabase.CanHoldSpellBooks(characterId)) return ShopResult.Refused(ShopRefusal.NoFreeSlot);

            run.unassignedSpellBooks ??= new List<string>();
            if (!run.unassignedSpellBooks.Contains(skillId)) return ShopResult.Refused(ShopRefusal.NotOwned);

            run.learnedSpells ??= new List<LearnedSpellEntry>();
            if (AlreadyKnows(run, characterId, skillId)) return ShopResult.Refused(ShopRefusal.AlreadyKnown);
            if (OccupiedSlots(run, characterId).Contains(slot)) return ShopResult.Refused(ShopRefusal.NoFreeSlot);

            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY.
            run.unassignedSpellBooks.Remove(skillId);
            run.learnedSpells.Add(new LearnedSpellEntry { characterId = characterId, skillId = skillId, slot = slot });

            // 3. PERSIST.
            return Persisted(0);
        }

        // Overwrites `characterId`'s existing `slot` with `skillId`. The
        // book that WAS in that slot returns to run.unassignedSpellBooks
        // rather than being destroyed (§7.1 point 5 -- the original design's
        // REPLACING/struck-through-name language is withdrawn with it): a
        // full character never has to be told what they are giving up
        // forever, only which slot they are handing this one instead.
        public static ShopResult ReplaceSpell(string characterId, string skillId, int slot)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            if (run == null) return ShopResult.Refused(ShopRefusal.NoShop);
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(skillId))
                return ShopResult.Refused(ShopRefusal.BadIndex);
            if (slot < 0 || slot >= SpellBooks.MaxSpellSlots) return ShopResult.Refused(ShopRefusal.BadIndex);

            // Same gate as LearnSpell's explicit overload above, same reason.
            // A character who cannot hold a book cannot hold one by trading a
            // book they should never have had either.
            if (!ContentDatabase.CanHoldSpellBooks(characterId)) return ShopResult.Refused(ShopRefusal.NoFreeSlot);

            run.unassignedSpellBooks ??= new List<string>();
            if (!run.unassignedSpellBooks.Contains(skillId)) return ShopResult.Refused(ShopRefusal.NotOwned);

            run.learnedSpells ??= new List<LearnedSpellEntry>();
            if (AlreadyKnows(run, characterId, skillId)) return ShopResult.Refused(ShopRefusal.AlreadyKnown);

            var existing = run.learnedSpells
                .FirstOrDefault(e => e != null && e.characterId == characterId && e.slot == slot);
            if (existing == null) return ShopResult.Refused(ShopRefusal.BadIndex);

            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY. The displaced skillId is captured before the slot is
            // overwritten, and the removal-from-pool happens before the pool
            // gains the displaced entry back.
            //
            // THE ORDERING IS RIGHT AND THE REASON IT USED TO GIVE IS NOT. It
            // claimed the ordering stopped "a duplicate copy of the SAME
            // skillId (buy two, learn one, replace with the other)" being
            // confused for the one just displaced. That case cannot get here:
            // AlreadyKnows returns AlreadyKnown above, so replacing slot N
            // (holding X) with a second copy of X is refused before the
            // ordering matters. The ordering survives as the plainer property
            // -- remove what is being spent, then bank what came back -- which
            // is the same posture every other mutation in this file takes.
            //
            // And the removal is by VALUE: Remove(skillId) takes the first
            // matching entry, not a particular one. Two copies of one book are
            // interchangeable, so which one goes carries no information.
            string displaced = existing.skillId;
            run.unassignedSpellBooks.Remove(skillId);
            existing.skillId = skillId;
            run.unassignedSpellBooks.Add(displaced);

            // 3. PERSIST.
            return Persisted(0);
        }

        private static bool AlreadyKnows(RunSnapshot run, string characterId, string skillId) =>
            run.learnedSpells != null
            && run.learnedSpells.Exists(e => e != null && e.characterId == characterId && e.skillId == skillId);

        private static HashSet<int> OccupiedSlots(RunSnapshot run, string characterId)
        {
            var slots = new HashSet<int>();
            if (run?.learnedSpells == null) return slots;

            foreach (var entry in run.learnedSpells)
            {
                if (entry != null && entry.characterId == characterId) slots.Add(entry.slot);
            }

            return slots;
        }
    }
}
