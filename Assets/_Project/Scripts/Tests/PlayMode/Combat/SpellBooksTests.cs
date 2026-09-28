using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

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

        private static readonly string[] ExpansionBookIds =
        {
            "gilded_aegis", "winters_rebuke", "vipers_bite", "crownfall",
            "ashen_reckoning", "blackglass_spear", "borrowed_moment", "gale_scythe",
            "palace_passage", "velvet_shackles", "censer_of_embers", "thorn_tithe",
            "court_of_whispers",
        };

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

        [Test]
        public void NoneOfTheThirteenIsOfferedToAFuryCharacter()
        {
            RunManager.StartRun(4242UL);

            foreach (string id in ExpansionBookIds)
            {
                GiveOneUnassignedCopy(id);
                Assert.AreEqual(ShopOutcome.Refused, RunOrchestrator.LearnSpell("bear", id).Outcome, id);
                CollectionAssert.DoesNotContain(
                    ContentDatabase.AvailableSkillsFor(new Character { definitionId = "bear", level = 99 })
                        .Select(skill => skill.id).ToList(), id, id);
            }
        }

        [Test]
        public void AllThirteenSurviveASaveAndReloadOneAtATime()
        {
            foreach (string id in ExpansionBookIds)
            {
                RunManager.StartRun(4242UL);
                RunManager.Run.learnedSpells.Clear();
                RunManager.Run.unassignedSpellBooks.Clear();
                GiveOneUnassignedCopy(id);
                Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.LearnSpell(CharacterId, id).Outcome, id);
                Assert.IsTrue(SaveSlotManager.SaveCurrent(), id);

                var loaded = SaveSystem.Load(0);

                Assert.That(loaded.activeRun.learnedSpells,
                    Has.Some.Matches<LearnedSpellEntry>(entry => entry.characterId == CharacterId && entry.skillId == id), id);
            }
        }

        // ---- who can hold a book at all (plan P6) -------------------------------
        //
        // A FIXTURE POOL, KEPT NOW THAT `fury` SHIPS. It states the predicate's
        // own switches as literals, so the rule is pinned against a row this
        // file controls rather than against whatever pools.json currently
        // says -- which is what keeps the shipped-content assertion below an
        // assertion about CONTENT rather than a second reading of the same
        // fact. Same split PartyFormationCaptureTests draws for the HUD meter.
        private static ResolvedPool BookRefusingFixture() =>
            new ResolvedPool(
                "fury_fixture", "Fury", "FURY",
                PoolCapacityRule.Fixed, 100,
                0, 15, 10,
                10, PoolDecayTrigger.Damage,
                PoolStartRule.Zero, 0,
                "#FF8A3A", "#8E3A12", "#FFD2B0",
                pulse: true, allowsSpellBooks: false, restoredByManaEffects: false, absorbsDamage: false,
                sortOrder: 99);

        [Test]
        public void ManaReadsBooksAndAPoolThatSaysOtherwiseDoesNot()
        {
            var mana = ContentDatabase.PrimaryPoolFor(ContentDatabase.ManaPoolId)?.Data;
            Assert.IsNotNull(mana, "the shipped catalogue has no mana row, so this asserts nothing");
            Assert.IsTrue(SpellBooks.CanHold(mana), "mana's row authors allowsSpellBooks true");

            Assert.IsFalse(SpellBooks.CanHold(BookRefusingFixture()),
                "a row that authors allowsSpellBooks false must refuse");

            // Graceful degradation, the house posture: a pool that has gone
            // missing from a save's catalogue plays as mana, and mana reads
            // books. The alternative -- refusing -- would strip a live run's
            // learned spells the first time a content edit renamed a row.
            Assert.IsTrue(SpellBooks.CanHold(null), "a missing pool degrades to the shipped answer");
        }

        // THE ADAPTER IN FRONT OF THE PREDICATE, pinned against the real
        // roster: every gate in phase C asks a character id, not a pool.
        //
        // BY ID, NOT BY LOOP, since phase E. The loop this replaced said "all
        // three" and would have kept passing had Bjorn's row been the one
        // that stayed on mana; naming who answers what is the assertion.
        [Test]
        public void TheTwoManaHoldersCarryBooksAndTheFuryHolderDoesNot()
        {
            var roster = ContentDatabase.Characters.Select(c => c.id).ToList();
            CollectionAssert.IsSupersetOf(roster, new[] { "sheep", "bear", "owl" },
                "the catalogue loaded nothing recognisable, so this agrees with itself");

            Assert.IsTrue(ContentDatabase.CanHoldSpellBooks("sheep"), "Shawn holds mana, and mana reads books");
            Assert.IsTrue(ContentDatabase.CanHoldSpellBooks("owl"), "Odette holds mana, and mana reads books");

            Assert.IsFalse(ContentDatabase.CanHoldSpellBooks("bear"),
                "Bjorn's primaryPoolId is 'fury', whose row authors allowsSpellBooks false");

            Assert.IsTrue(ContentDatabase.CanHoldSpellBooks("nobody_by_that_name"),
                "an unknown character resolves to the mana fallback, not to a refusal");
        }

        // GATE 1 (plan P6). CanLearn is the single door in front of every
        // learn and replace path -- LearnSpell and ReplaceSpell both consult
        // it -- so -1 here is what makes the other five refusals unreachable
        // rather than merely unlikely.
        [Test]
        public void CanLearnRefusesTheFuryHolderOutrightRatherThanForWantOfASlot()
        {
            RunManager.StartRun(4242UL);

            Assert.AreEqual(-1, RunOrchestrator.CanLearn("bear"),
                "Bjorn has three empty slots and still cannot learn: the refusal is the pool, not the slots");
            Assert.AreEqual(0, RunOrchestrator.CanLearn(CharacterId),
                "and it is per character -- Shawn's own first slot is still free");

            GiveOneUnassignedCopy();
            var result = RunOrchestrator.LearnSpell("bear", SkillId);
            Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, SkillId,
                "a refused learn must leave the book on the pile rather than consuming it");
            Assert.IsEmpty(RunManager.Run.learnedSpells);
        }

        // GATE 2, and the threshold is the whole point: ALL, not ANY. One
        // book-less character in the squad must not take a book off the shelf
        // for the two who can read it.
        [Test]
        public void ABookStaysOnTheShelfWhileAnybodyFieldedCanReadIt()
        {
            RunManager.StartRun(4242UL);

            var squad = SaveSlotManager.CurrentSave.ActiveSquadIds();
            CollectionAssert.Contains(squad, "bear",
                "this fixture is only worth anything with the book-less character actually fielded");
            Assert.IsTrue(squad.Any(id => ContentDatabase.CanHoldSpellBooks(id)),
                "and only worth anything with somebody beside him who can read one");

            CollectionAssert.Contains(
                RunOrchestrator.ShopBookCandidatesForTest().Select(c => c.SkillId).ToList(), SkillId,
                "Bjorn cannot read it and it is still stock -- Shawn and Odette can");
        }

        // THE OTHER SIDE OF THAT THRESHOLD. A squad of nobody-can-read is the
        // case where the card really is dead, and the shelf has to drop it or
        // it stocks a purchase that can never be placed.
        [Test]
        public void ABookLeavesTheShelfOnlyWhenEveryFieldedCharacterRefusesIt()
        {
            RunManager.StartRun(4242UL);

            var save = SaveSlotManager.CurrentSave;
            Assert.IsNotNull(save.ActiveSquad().FirstOrDefault(c => c != null && c.definitionId == "bear"),
                "no Bjorn in the squad, so there is no all-refuse case to build");

            // A ONE-MEMBER SQUAD OF THE ONE WHO CANNOT READ, built through the
            // save's own selection rather than by inventing a party the game
            // cannot produce -- a solo Bjorn is an ordinary fielding, and it is
            // the only shipped way to reach `squad.All(cannot read)`.
            save.selectedCharacterIds = new List<string> { "bear" };

            CollectionAssert.AreEqual(new[] { "bear" }, save.ActiveSquadIds(),
                "the bench did not take, so this is still testing a mixed squad");

            CollectionAssert.DoesNotContain(
                RunOrchestrator.ShopBookCandidatesForTest().Select(c => c.SkillId).ToList(), SkillId,
                "a card nobody fielded could act on is a dead card holding a live card's slot");
        }

        // PROGRESSION V2 PHASE 4: a book a character's own TRACK handed over
        // is one they already know, so it leaves the shelf the same way a
        // bought copy does.
        //
        // Three book spells are now track rewards -- Cinderfault to Shawn at
        // 20, Frost Flare and Lightning Bolt to Odette at 3 and 10 -- and
        // until this the shelf only ever asked run.learnedSpells. A squad who
        // had collected all three could still be sold copies of them, which
        // is precisely the dead card §2d already refuses for a bought book.
        //
        // A SOLO ODETTE, because the threshold is ALL: with Shawn fielded
        // beside her the book is correctly still stock for him, and the two
        // halves of the rule would hide each other. Solo is an ordinary
        // fielding (ABookLeavesTheShelfOnlyWhenEveryFieldedCharacterRefusesIt
        // builds a solo Bjorn the same way).
        //
        // CLAIMED, NOT REACHED: the level below the node still sells the
        // book, which is the assertion that makes this about collection
        // rather than about the character id.
        [Test]
        public void ABookTheTrackHasAlreadyHandedOverLeavesTheShelf()
        {
            RunManager.StartRun(4242UL);

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl" };
            CollectionAssert.AreEqual(new[] { "owl" }, save.ActiveSquadIds(),
                "the bench did not take, so this is still testing a mixed squad");

            var odette = save.roster.Single(c => c.definitionId == "owl");
            int frostFlareLevel = RewardTracks.For(odette).UnlockLevel(Domain.Progression.TrackReward.UnlockSkill);
            Assert.Greater(frostFlareLevel, 0, "Odette's track no longer unlocks a skill at all");
            Assert.AreEqual("frost_flare", RewardTracks.For(odette).At(frostFlareLevel).SkillId,
                "her first UnlockSkill node is not Frost Flare any more -- repin this fixture");

            odette.claimedTrackLevel = frostFlareLevel - 1;
            CollectionAssert.Contains(
                RunOrchestrator.ShopBookCandidatesForTest().Select(c => c.SkillId).ToList(), "frost_flare",
                "the node has not been collected, so the book is still worth selling");

            odette.claimedTrackLevel = frostFlareLevel;
            CollectionAssert.DoesNotContain(
                RunOrchestrator.ShopBookCandidatesForTest().Select(c => c.SkillId).ToList(), "frost_flare",
                "she has collected the node that teaches it, so the book is a card she cannot act on");
        }

        // GATE 4. Reconcile runs on LOAD, so a save written before a
        // character's pool changed reaches a fight through routes that never
        // opened a save file (the tooling party, a preview) -- this is what
        // makes the difference between "the entry is gone" and "the entry is
        // gone AND could not have cast anything anyway".
        [Test]
        public void AStaleLearnedBookNeverReachesTheFuryHoldersKit()
        {
            RunManager.StartRun(4242UL);

            // Written straight into the run, past LearnSpell's refusal, which
            // is exactly the shape a save from before the pool changed has.
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = "bear", skillId = SkillId, slot = 0 });

            var bjorn = new Character { definitionId = "bear", level = 9 };
            CollectionAssert.DoesNotContain(
                ContentDatabase.AvailableSkillsFor(bjorn).Select(s => s.id).ToList(), SkillId,
                "the learned-this-run route is gated on the pool, so a stale entry grants nothing");

            // THE CONTROL: the same stale-looking entry on a mana holder IS
            // honoured, so this is not passing because the route is broken.
            var shawn = new Character { definitionId = CharacterId, level = 9 };
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = CharacterId, skillId = SkillId, slot = 0 });
            CollectionAssert.Contains(
                ContentDatabase.AvailableSkillsFor(shawn).Select(s => s.id).ToList(), SkillId);
        }

        // AND THE PRUNE ITSELF. Loud rather than quiet: the other arms of
        // Reconcile drop content that no longer exists, which the player can
        // see for themselves; this one drops a spell off a character who
        // still has three empty-looking slots.
        [Test]
        public void ReconcileReturnsTheFuryHoldersBookToTheUnplacedPile()
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = "bear", skillId = SkillId, slot = 1 });

            LogAssert.Expect(LogType.Warning, new Regex("bear.*can no longer carry spell books.*" + SkillId));

            SaveSlotManager.CurrentSave.Reconcile();

            Assert.IsEmpty(RunManager.Run.learnedSpells,
                "the entry must not survive on a character who cannot hold it");
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, SkillId,
                "and the book is returned, not destroyed -- somebody else can still place it");
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
            Assert.IsTrue(skill.Data.BookOnly, "the five spells are learned-only, never levelled into");
            Assert.Greater(skill.Data.BookTier, 0, "and still carry the tier that prices/rolls them in the shop");
            Assert.AreEqual(int.MaxValue, skill.Data.UnlockLevel,
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

        // ---- a book belongs to whoever learned it (plan Step 4, E4) -----------

        // ODETTE CASTS SHAWN'S SPELL, because it was never Shawn's -- it was
        // whoever paid for the book's.
        //
        // Every one of the six book spells carries characterId "sheep",
        // because Shawn is who they were first written for. The shop has never
        // read that field (AvailableBookOptions offers any bookTier > 0 skill
        // not already known by every fielded character), so Odette could buy
        // Frost Flare, be charged, watch it land in one of her three slots --
        // and then find it absent from her kit in the fight, because
        // AvailableSkillsFor still asked whether the skill was hers by
        // AUTHORSHIP. A purchase with no effect, and nothing anywhere said so.
        private const string OtherCharacterId = "owl";

        [Test]
        public void ALearnedBookReachesTheCharacterWhoLearnedItWhoeverItWasAuthoredFor()
        {
            RunManager.StartRun(4242UL);

            var skill = ContentDatabase.GetSkill(SkillId);
            Assert.IsNotNull(skill);
            Assert.AreNotEqual(OtherCharacterId, skill.Data.CharacterId,
                "this test is only worth anything if the book is authored against SOMEBODY ELSE");

            var odette = new Character { definitionId = OtherCharacterId, level = 9 };

            CollectionAssert.DoesNotContain(
                ContentDatabase.AvailableSkillsFor(odette).Select(x => x.id).ToList(), SkillId,
                "nothing should grant a book before it is learned");

            GiveOneUnassignedCopy();
            var result = RunOrchestrator.LearnSpell(OtherCharacterId, SkillId);
            Assert.AreEqual(ShopOutcome.Ok, result.Outcome,
                "the shop already lets anyone buy this book; learning it must agree");

            CollectionAssert.Contains(
                ContentDatabase.AvailableSkillsFor(odette).Select(x => x.id).ToList(), SkillId,
                "the character who learned the book is the character who has it");
        }

        // THE OTHER HALF, and the half that would make the change wrong if it
        // failed: a book is learned by ONE character, not by the party.
        [Test]
        public void ASquadMateWhoDidNotLearnItDoesNotSeeIt()
        {
            RunManager.StartRun(4242UL);

            GiveOneUnassignedCopy();
            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.LearnSpell(OtherCharacterId, SkillId).Outcome);

            var shawn = new Character { definitionId = CharacterId, level = 9 };

            CollectionAssert.DoesNotContain(
                ContentDatabase.AvailableSkillsFor(shawn).Select(x => x.id).ToList(), SkillId,
                "Odette's book is Odette's -- even though this skill is authored against Shawn, " +
                "which is exactly the case that would pass for the wrong reason if the check were dropped " +
                "rather than moved");
        }

        // AND IT ACTUALLY RESOLVES. Availability is a list; a cast is mana
        // leaving the pool and health leaving a monster. A skill that appears
        // on the kit and does nothing when pressed would satisfy both tests
        // above and be exactly as broken as the bug they describe.
        [Test]
        public void TheLearnerCanActuallyCastIt()
        {
            RunManager.StartRun(4242UL);

            GiveOneUnassignedCopy();
            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.LearnSpell(OtherCharacterId, SkillId).Outcome);

            var odette = new Character { definitionId = OtherCharacterId, level = 9 };
            var available = ContentDatabase.AvailableSkillsFor(odette);
            int row = available.ToList().FindIndex(x => x.id == SkillId);
            Assert.GreaterOrEqual(row, 0, "the learned book should be on her kit");

            var skills = available.Select(FightEncounterAdapter.Resolve).ToList();
            var caster = new CombatantState("Odette", true, 300, 99, 40, 10);
            var foe = new CombatantState("Target", false, 5000, 10, 8, 4);

            var session = new FightSession(
                new CombatEncounter(new[] { caster }, new[] { foe }),
                new List<PlayerKit>
                {
                    new PlayerKit(OtherCharacterId, CharacterRole.Support, skills, null, null, level: 9),
                },
                new List<EnemyKit>
                {
                    new EnemyKit(new ResolvedEnemy("dummy", "Target", new StatBlock(), 1, 1, false,
                        DamageType.Physical, DamageType.Physical, 0), false),
                },
                new SeededRandom(11));

            session.Begin();

            int manaBefore = caster.CurrentMana;
            int healthBefore = foe.CurrentHealth;

            var option = session.SkillOptionsFor(caster).FirstOrDefault(o => o.Skill.Id == SkillId);
            Assert.IsNotNull(option.Skill, "the session's own option list should carry the learned book");
            Assert.IsTrue(option.Affordable, "99 mana is enough for any authored book");

            session.CastSkill(option.Index, foe);

            Assert.Less(caster.CurrentMana, manaBefore, "casting it should cost mana");
            Assert.Less(foe.CurrentHealth, healthBefore, "and the effect should resolve on the target");
        }

        // THE SHOP'S END OF THE SAME PROMISE, pinned rather than assumed. It
        // was already true -- AvailableBookOptions never read the skill's
        // characterId -- and it is precisely the half that made the other half
        // a bug rather than a design: a shop that sells to anyone paired with
        // a kit that only serves the author is a purchase with no effect.
        [Test]
        public void TheShopOffersABookToWhoeverIsBuying()
        {
            RunManager.StartRun(4242UL);

            var offered = RunOrchestrator.ShopBookCandidatesForTest().Select(c => c.SkillId).ToList();
            CollectionAssert.Contains(offered, SkillId,
                "a book authored against Shawn is stock for whoever walks in");

            // And it stays stock while only SOME of the squad know it -- the
            // pool only drops a book everyone fielded already has.
            GiveOneUnassignedCopy();
            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.LearnSpell(OtherCharacterId, SkillId).Outcome);

            CollectionAssert.Contains(
                RunOrchestrator.ShopBookCandidatesForTest().Select(c => c.SkillId).ToList(), SkillId,
                "one buyer knowing it does not take it off the shelf for the rest");
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
            // A second, genuinely different book (AUDIT #150: not
            // static_fleece).
            GiveOneUnassignedCopy("frost_flare");

            SaveSlotManager.CurrentSave.Reconcile();

            Assert.AreEqual(1, RunManager.Run.learnedSpells.Count);
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, "frost_flare");
        }
    }
}
