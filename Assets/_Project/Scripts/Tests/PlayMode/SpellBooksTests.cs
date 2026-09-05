using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // LEARNING A BOOK INTO A SLOT (docs/PLAN_SHOP.md §1d, §1g), and what
    // Reconcile does when content moves under a saved run.
    //
    // PlayMode, same reason ShopMutationTests is: every one of these reads
    // ContentDatabase and writes a save file, both Core.
    public class SpellBooksTests
    {
        private const string CharacterId = "sheep";

        // Book-eligible from Phase A onward (bookTier authored, unlockLevel
        // still kept -- see skills.json). Any of the five would do; this one
        // is the lowest tier and therefore cheapest to reason about.
        private const string SkillId = "mud_burst";

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-spellbooks-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RunOrchestrator.RefuseShopMutationForTest = null;
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            RunOrchestrator.RefuseShopMutationForTest = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static void GiveOneUnassignedCopy(string skillId = SkillId)
        {
            RunManager.Run.unassignedSpellBooks ??= new List<string>();
            RunManager.Run.unassignedSpellBooks.Add(skillId);
        }

        // ---- content: bookOnly stays inert, bookTier does not -------------------

        [Test]
        public void TheFiveSpellsCarryABookTierAndAreUnreachableByLevelling()
        {
            // Phase E (docs/PLAN_SHOP.md Gate 4) flipped bookOnly on the five
            // spells and stripped their unlockLevel -- this is now the live,
            // shipped state, not a staging snapshot. SkillEntryResolver reads
            // an absent unlockLevel on a bookOnly skill as int.MaxValue (see
            // that file's own comment for why: NOT defaulting to 1, which
            // would have handed every book-only spell out for free at the
            // level route the book gate exists to close).
            var skill = ContentDatabase.GetSkill(SkillId);
            Assert.IsNotNull(skill, "mud_burst should exist in the built content");
            Assert.IsTrue(skill.data.BookOnly, "the five spells are learned-only, never levelled into");
            Assert.Greater(skill.data.BookTier, 0, "and still carry the tier that prices/rolls them in the shop");
            Assert.AreEqual(int.MaxValue, skill.data.UnlockLevel,
                "no level should ever grant a book-only skill for free");
        }

        [Test]
        public void LearningABookIsTheOnlyWayToReachIt()
        {
            // The payoff of the staged flip: before Gate 4 this same shape
            // (learn, then diff AvailableSkillsFor) was a deliberate no-op,
            // because the level route already granted mud_burst regardless.
            // Now bookOnly is true, so the level route grants nothing for
            // this skill and AvailableSkillsFor's fourth route (gated on
            // bookOnly) is the only way in.
            RunManager.StartRun(4242UL);
            var character = new Character { definitionId = CharacterId, level = 9 };

            var before = ContentDatabase.AvailableSkillsFor(character).Select(s => s.id).ToList();
            CollectionAssert.DoesNotContain(before, SkillId,
                "a book-only skill must not be granted by levelling, even at max level");

            GiveOneUnassignedCopy();
            RunOrchestrator.LearnSpell(CharacterId, SkillId);

            var after = ContentDatabase.AvailableSkillsFor(character).Select(s => s.id).ToList();
            CollectionAssert.Contains(after, SkillId, "learning the book is what grants it");
        }

        // ---- CanLearn -------------------------------------------------------

        [Test]
        public void CanLearnReturnsTheLowestFreeSlot()
        {
            RunManager.StartRun(4242UL);
            Assert.AreEqual(0, RunOrchestrator.CanLearn(CharacterId));

            RunManager.Run.learnedSpells.Add(new LearnedSpellEntry { characterId = CharacterId, skillId = "a", slot = 0 });
            Assert.AreEqual(1, RunOrchestrator.CanLearn(CharacterId));

            RunManager.Run.learnedSpells.Add(new LearnedSpellEntry { characterId = CharacterId, skillId = "b", slot = 2 });
            Assert.AreEqual(1, RunOrchestrator.CanLearn(CharacterId));
        }

        [Test]
        public void CanLearnReturnsMinusOneWhenAllThreeSlotsAreFull()
        {
            RunManager.StartRun(4242UL);
            for (int slot = 0; slot < SpellBooks.MaxSpellSlots; slot++)
            {
                RunManager.Run.learnedSpells.Add(
                    new LearnedSpellEntry { characterId = CharacterId, skillId = "skill" + slot, slot = slot });
            }

            Assert.AreEqual(-1, RunOrchestrator.CanLearn(CharacterId));
        }

        [Test]
        public void CanLearnIsPerCharacter()
        {
            RunManager.StartRun(4242UL);
            for (int slot = 0; slot < SpellBooks.MaxSpellSlots; slot++)
            {
                RunManager.Run.learnedSpells.Add(
                    new LearnedSpellEntry { characterId = "other", skillId = "skill" + slot, slot = slot });
            }

            Assert.AreEqual(0, RunOrchestrator.CanLearn(CharacterId), "a full OTHER character leaves this one untouched");
        }

        // ---- LearnSpell -------------------------------------------------------

        [Test]
        public void LearnSpellMovesOneCopyFromThePoolIntoTheLowestFreeSlot()
        {
            RunManager.StartRun(4242UL);
            GiveOneUnassignedCopy();

            var result = RunOrchestrator.LearnSpell(CharacterId, SkillId);

            Assert.IsTrue(result.Applied);
            var run = RunManager.Run;
            CollectionAssert.DoesNotContain(run.unassignedSpellBooks, SkillId);
            Assert.AreEqual(1, run.learnedSpells.Count);
            Assert.AreEqual(CharacterId, run.learnedSpells[0].characterId);
            Assert.AreEqual(SkillId, run.learnedSpells[0].skillId);
            Assert.AreEqual(0, run.learnedSpells[0].slot);
        }

        [Test]
        public void LearningTwoCopiesForTwoCharactersIsLegal()
        {
            RunManager.StartRun(4242UL);
            GiveOneUnassignedCopy();
            GiveOneUnassignedCopy();

            var first = RunOrchestrator.LearnSpell(CharacterId, SkillId);
            var second = RunOrchestrator.LearnSpell("other", SkillId);

            Assert.IsTrue(first.Applied);
            Assert.IsTrue(second.Applied);
            Assert.AreEqual(2, RunManager.Run.learnedSpells.Count);
        }

        [Test]
        public void LearnSpellRefusesANotOwnedBook()
        {
            RunManager.StartRun(4242UL);

            var result = RunOrchestrator.LearnSpell(CharacterId, SkillId);

            Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
            Assert.AreEqual(ShopRefusal.NotOwned, result.Reason);
            Assert.IsEmpty(RunManager.Run.learnedSpells);
        }

        [Test]
        public void LearnSpellRefusesADuplicate()
        {
            RunManager.StartRun(4242UL);
            GiveOneUnassignedCopy();
            RunOrchestrator.LearnSpell(CharacterId, SkillId);

            GiveOneUnassignedCopy();
            var result = RunOrchestrator.LearnSpell(CharacterId, SkillId);

            Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
            Assert.AreEqual(ShopRefusal.AlreadyKnown, result.Reason);
            // The pre-state is intact: the second copy is still unassigned,
            // and the character still has exactly one slot filled.
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, SkillId);
            Assert.AreEqual(1, RunManager.Run.learnedSpells.Count);
        }

        [Test]
        public void LearnSpellRefusesWithNoFreeSlot()
        {
            RunManager.StartRun(4242UL);
            for (int slot = 0; slot < SpellBooks.MaxSpellSlots; slot++)
            {
                RunManager.Run.learnedSpells.Add(
                    new LearnedSpellEntry { characterId = CharacterId, skillId = "skill" + slot, slot = slot });
            }

            GiveOneUnassignedCopy();
            var result = RunOrchestrator.LearnSpell(CharacterId, SkillId);

            Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
            Assert.AreEqual(ShopRefusal.NoFreeSlot, result.Reason);
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, SkillId);
        }

        // ---- ReplaceSpell: return to pool, not destruction ---------------------

        [Test]
        public void ReplaceSpellReturnsTheDisplacedBookToThePool()
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = CharacterId, skillId = "old_spell", slot = 1 });
            GiveOneUnassignedCopy();

            var result = RunOrchestrator.ReplaceSpell(CharacterId, SkillId, 1);

            Assert.IsTrue(result.Applied);
            var run = RunManager.Run;
            Assert.AreEqual(SkillId, run.learnedSpells.Single(e => e.characterId == CharacterId && e.slot == 1).skillId);
            // The old book is not destroyed (§7.1 point 5) -- it comes back
            // to the pool, where a different character (or a later
            // replacement) can still place it.
            CollectionAssert.Contains(run.unassignedSpellBooks, "old_spell");
            CollectionAssert.DoesNotContain(run.unassignedSpellBooks, SkillId);
        }

        [Test]
        public void ReplaceSpellRefusesAnEmptySlot()
        {
            RunManager.StartRun(4242UL);
            GiveOneUnassignedCopy();

            var result = RunOrchestrator.ReplaceSpell(CharacterId, SkillId, 1);

            Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
            Assert.AreEqual(ShopRefusal.BadIndex, result.Reason);
        }

        [Test]
        public void ReplaceSpellRefusesADuplicate()
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = CharacterId, skillId = SkillId, slot = 0 });
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = CharacterId, skillId = "other_spell", slot = 1 });
            GiveOneUnassignedCopy();

            // Trying to replace slot 1 with a spell already sitting in slot 0.
            var result = RunOrchestrator.ReplaceSpell(CharacterId, SkillId, 1);

            Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
            Assert.AreEqual(ShopRefusal.AlreadyKnown, result.Reason);
            Assert.AreEqual("other_spell", RunManager.Run.learnedSpells.Single(e => e.slot == 1).skillId);
        }

        // ---- the roll ---------------------------------------------------------

        [Test]
        public void VictoryDropsFoldIntoUnassignedSpellBooksOnAWin()
        {
            // A high-probability boss fight with only one candidate is
            // deterministic enough to assert on directly, rather than
            // fighting a real encounter through FightBootstrap -- what is
            // under test is the fold into unassignedSpellBooks, not combat.
            var rng = new SeededRandom(1);
            string dropped = null;
            for (int i = 0; i < 2000 && dropped == null; i++)
            {
                dropped = VictoryRewards.RollSpellDrop(
                    new List<string> { SkillId }, isEliteFight: false, isBossFight: true, rng);
            }

            Assert.AreEqual(SkillId, dropped, "a boss fight rolls often enough at 0.35 that 2000 draws should land one");
        }

        [Test]
        public void TheSpellDropRollIsDeterministicByPosition()
        {
            var a = RngStreams.Open(4242UL, RngStreams.SpellDrop, 5, 3);
            var b = RngStreams.Open(4242UL, RngStreams.SpellDrop, 5, 3);

            var ids = new List<string> { "a", "b", "c" };
            var first = VictoryRewards.RollSpellDrop(ids, false, true, a);
            var second = VictoryRewards.RollSpellDrop(ids, false, true, b);

            Assert.AreEqual(first, second);
        }

        // ---- Reconcile ----------------------------------------------------------

        [Test]
        public void ReconcileDropsALearnedSpellThatIsNoLongerBookEligible()
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = CharacterId, skillId = "not_a_real_skill_id", slot = 0 });
            RunManager.Run.unassignedSpellBooks.Add("also_not_real");

            SaveSlotManager.CurrentSave.Reconcile();

            Assert.IsEmpty(RunManager.Run.learnedSpells);
            Assert.IsEmpty(RunManager.Run.unassignedSpellBooks);
        }

        [Test]
        public void ReconcileKeepsAGenuineBookEligibleEntry()
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = CharacterId, skillId = SkillId, slot = 0 });
            GiveOneUnassignedCopy("static_fleece");

            SaveSlotManager.CurrentSave.Reconcile();

            Assert.AreEqual(1, RunManager.Run.learnedSpells.Count);
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, "static_fleece");
        }
    }
}
