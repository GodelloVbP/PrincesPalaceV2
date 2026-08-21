using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.PlayModeTests
{
    // DOES KINDLING AN ORB ACTUALLY DO ANYTHING?
    //
    // It did not. The screen minted its own ids -- "sheep.p0.s0" -- and wrote
    // them into unlockedTalentIds, while every consumer of that list matches
    // against the ids in talents.json ("sheep_ram_root"). Nothing translated
    // between them, so 294 authored talents were unreachable: an ember was
    // spent, an orb lit, the save was written, and no effect ever applied.
    //
    // Nothing in the suite could see it. The screen tests check the tree, the
    // page tests check the refusal rules against ids they supply themselves,
    // and the combat tests build a CombatantState with talents handed to them
    // directly. The gap was exactly between the two halves, which is where
    // nobody was looking.
    public class TalentInvestmentTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-talent-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE ID THE SAVE HOLDS HAS TO BE ONE THE CONTENT KNOWS. Everything
        // else in this file follows from that.
        [UnityTest]
        public IEnumerator KindlingWritesAnIdTheContentActuallyHas()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var talents = Object.FindAnyObjectByType<TalentController>();
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");

            var save = SaveSlotManager.CurrentSave;
            var character = save.ActiveSquad().FirstOrDefault(c => c != null);
            Assert.IsNotNull(character, "the fixture has no character");

            character.embers = 99;
            int before = character.unlockedTalentIds.Count;

            // Driven through the BUTTONS, because PlayMode has no access to
            // Core's internals by design -- pick the root of path 0, which is
            // reachable with nothing else invested, then kindle it.
            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            Assert.AreEqual(before + 1, character.unlockedTalentIds.Count,
                "kindling the root of a path did not record anything");

            string taken = character.unlockedTalentIds.Last();
            Assert.IsNotNull(ContentDatabase.GetTalent(taken),
                $"the save now holds '{taken}', which no authored talent has as its id - so nothing " +
                "that reads unlockedTalentIds will ever match it and the ember bought nothing");
        }

        // And the effect reaches the character, which is the thing the player
        // is actually paying for.
        [UnityTest]
        public IEnumerator AKindledTalentChangesTheCharacterItWasSpentOn()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var talents = Object.FindAnyObjectByType<TalentController>();
            var character = SaveSlotManager.CurrentSave.ActiveSquad().FirstOrDefault(c => c != null);
            character.embers = 99;

            int spentBefore = ContentDatabase.SpentBy(character);
            var effectsBefore = ContentDatabase.TalentEffects(character);
            var scoresBefore = ContentDatabase.EffectiveAbilityScores(character);
            var statsBefore = ContentDatabase.EffectiveStats(character);

            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            // The root is FREE -- ContentDatabase prices the root, the
            // convergence and the capstone at 0, because reaching them is the
            // price. So spend is measured after taking the orb ABOVE it, which
            // is the first one that costs anything.
            Press(talents, "Orb0_1");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            int spentAfter = ContentDatabase.SpentBy(character);
            var effectsAfter = ContentDatabase.TalentEffects(character);
            var scoresAfter = ContentDatabase.EffectiveAbilityScores(character);
            var statsAfter = ContentDatabase.EffectiveStats(character);

            Assert.Greater(spentAfter, spentBefore,
                "ContentDatabase does not count the orbs as spent, so the save and the content still " +
                "disagree about what has been taken");

            // ANY of the three, not a particular one.
            //
            // A talent's effects land in three different places -- the stat
            // block, the ability scores, and the TalentEffectSet combat reads
            // per swing -- and which one depends entirely on what was authored.
            // The sheep's own root grants wool on being hit, which is a combat
            // effect and moves no stat at all, so asserting on stats alone
            // failed for a talent that was working perfectly.
            //
            // What is being guarded against is a talent that reaches NONE of
            // them, which is what the whole id mismatch caused.
            bool reached = !ReferenceEquals(effectsBefore, effectsAfter)
                           || Sum(statsBefore) != Sum(statsAfter)
                           || scoresBefore.strength != scoresAfter.strength
                           || scoresBefore.dexterity != scoresAfter.dexterity
                           || scoresBefore.constitution != scoresAfter.constitution
                           || scoresBefore.wisdom != scoresAfter.wisdom
                           || scoresBefore.intelligence != scoresAfter.intelligence
                           || scoresBefore.charisma != scoresAfter.charisma;

            Assert.IsTrue(reached,
                "the talents were recorded and counted as spent, but reached neither the stat block, " +
                "the ability scores nor the combat effect set - so the player paid for nothing");
        }

        // COSTS COME FROM THE CONTENT NOW, and they are not flat -- which is
        // the whole reason the screen could not keep charging a constant 1.
        //
        // I had this backwards when I wrote the seam and the content put me
        // right: the root, the convergence and the capstone are FREE, because
        // reaching them is the price. What costs is the climb between them,
        // by strand tier.
        [Test]
        public void TheClimbCostsAndTheLandmarksDoNot()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = save.ActiveSquad().FirstOrDefault(c => c != null);
            Assert.IsNotNull(character);

            var all = ContentDatabase.TalentsFor(character);
            CollectionAssert.IsNotEmpty(all, "the character has no talents at all");

            foreach (var talent in all)
            {
                int cost = ContentDatabase.OrbCost(talent);
                string kind = TalentSkeleton.Kind[talent.row];
                bool landmark = kind == "merge" || kind == "cap" || TalentSkeleton.Depth[talent.row] == 0;

                if (landmark)
                {
                    Assert.AreEqual(0, cost, $"'{talent.id}' is a {kind} at depth " +
                        $"{TalentSkeleton.Depth[talent.row]} and should be free");
                }
                else
                {
                    Assert.Greater(cost, 0, $"'{talent.id}' is a chain orb and should cost something");
                }
            }

            // Non-vacuity: a tree of nothing but free landmarks would satisfy
            // the loop above without ever charging for anything.
            Assert.Greater(all.Select(ContentDatabase.OrbCost).Max(), 1,
                "nothing in this tree costs more than one ember, so cost is effectively flat after all");
        }

        // ---- the respec, level 20 of the reward track ------------------------------

        private static IEnumerator OpenTheTree()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Button ButtonNamed(TalentController talents, string name) =>
            talents.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == name);

        [UnityTest]
        public IEnumerator TheRespecIsHiddenUntilTheTrackGrantsIt()
        {
            yield return OpenTheTree();
            var talents = Object.FindAnyObjectByType<TalentController>();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.level = 19;
            talents.Refresh();
            yield return null;

            Assert.IsFalse(ButtonNamed(talents, "TalentRespecButton").gameObject.activeSelf,
                "a level-19 character is shown a respec they have not earned");

            character.level = 20;
            talents.Refresh();
            yield return null;

            Assert.IsTrue(ButtonNamed(talents, "TalentRespecButton").gameObject.activeSelf,
                "level 20 granted the respec and the button is still hidden");
        }

        [UnityTest]
        public IEnumerator RespeccingGivesBackTheEmbersTheOrbsCost()
        {
            yield return OpenTheTree();
            var talents = Object.FindAnyObjectByType<TalentController>();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.level = 20;
            character.embers = 99;
            talents.Refresh();
            yield return null;

            // The ROOT is free -- slot 0 of each path is that path's wool
            // generation rule, and OrbCost prices it at nothing. So kindle one
            // step past it, or this test refunds zero and passes for the wrong
            // reason.
            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            Press(talents, "Orb0_1");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            int spent = 99 - character.embers;
            Assert.Greater(spent, 0, "the fixture kindled nothing that cost anything");

            Press(talents, "TalentRespecButton");
            yield return null;

            Assert.AreEqual(99, character.embers, "the respec did not give back what the orbs cost");
            Assert.IsEmpty(character.unlockedTalentIds, "the respec left an orb kindled");
        }

        // THE TRAP THIS CODEBASE NAMED BEFORE A RESPEC EXISTED.
        //
        // Character.unlockedSkillIds' own comment says the two lists are
        // separate precisely so "a respec (which only ever refunds
        // unlockedTalentIds)" cannot "strip an event-taught spell it never
        // granted". Those skills are handed over by an Event room's mage and
        // cost nothing, so taking them back would remove something the player
        // never spent and cannot re-buy.
        [UnityTest]
        public IEnumerator ARespecDoesNotTakeBackWhatWasNeverBought()
        {
            yield return OpenTheTree();
            var talents = Object.FindAnyObjectByType<TalentController>();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.level = 20;
            character.embers = 99;
            character.unlockedSkillIds.Add("taught_by_the_mage");
            talents.Refresh();
            yield return null;

            Press(talents, "Orb0_0");
            yield return null;
            Press(talents, "InvestButton");
            yield return null;

            Press(talents, "TalentRespecButton");
            yield return null;

            CollectionAssert.Contains(character.unlockedSkillIds, "taught_by_the_mage",
                "the respec stripped a skill the player was given rather than bought");
        }

        // Stat points are refunded too, and that half cannot be reached through
        // the UI at all: nothing in the game spends a stat point (AUDIT #53,
        // Character.Invest has no production caller). Set directly here, which
        // is the one honest way to cover a path production cannot yet walk --
        // and the reason it is covered now is that a respec quietly forgetting
        // half its job is a bug nobody would look for once a "+" appears.
        [UnityTest]
        public IEnumerator RespeccingGivesBackPlacedStatPoints()
        {
            yield return OpenTheTree();
            var talents = Object.FindAnyObjectByType<TalentController>();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.level = 20;
            character.unspentStatPoints = 3;
            Assert.IsTrue(character.Invest(PrincesPalace.Domain.Stats.AbilityScore.Constitution));
            Assert.IsTrue(character.Invest(PrincesPalace.Domain.Stats.AbilityScore.Strength));

            Assert.AreEqual(1, character.unspentStatPoints, "the fixture did not place two points");
            talents.Refresh();
            yield return null;

            Press(talents, "TalentRespecButton");
            yield return null;

            Assert.AreEqual(3, character.unspentStatPoints, "the placed points did not come back");
            Assert.AreEqual(0, character.InvestedPointTotal, "the invested block was not cleared");
        }

        [UnityTest]
        public IEnumerator ARespecWithNothingToGiveBackIsNotPressable()
        {
            yield return OpenTheTree();
            var talents = Object.FindAnyObjectByType<TalentController>();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.level = 20;
            character.unlockedTalentIds.Clear();
            character.investedAbilityScores = default;
            talents.Refresh();
            yield return null;

            Assert.IsFalse(ButtonNamed(talents, "TalentRespecButton").interactable,
                "a respec with nothing to refund is pressable, so pressing it does nothing visible");
        }

        private static void Press(TalentController talents, string name)
        {
            var button = talents.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == name);

            Assert.IsNotNull(button, $"the talent screen has no '{name}'");
            Assert.IsTrue(button.gameObject.activeInHierarchy, $"'{name}' is not on screen");
            button.onClick.Invoke();
        }

        private static int Sum(PrincesPalace.Domain.Stats.StatBlock s) =>
            s.maxHealth + s.attack + s.defense + s.speed + s.manaRegen
            + s.physicalResistance + s.magicalResistance;
    }
}
