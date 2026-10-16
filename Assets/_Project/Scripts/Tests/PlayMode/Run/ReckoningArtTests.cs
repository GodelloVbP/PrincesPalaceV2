using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE PROCESSED ART ON THE REAL SCREEN (docs/PLAN_REWARD_SCREENS.md
    // section 5, phase R7): the cache by the page's class, the chain on an
    // inert card with the words moved to the tooltip, and the glyphs on the
    // whetstone, the door and the delta line. Sprite names are the file
    // stems, pinned as literals.
    public class ReckoningArtTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-art-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = true;
            ReckoningController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        private IEnumerator Show(IReadOnlyList<PendingPage> pages)
        {
            yield return SharedScene.Ensure("Fight");

            _input = NavSceneReuse.TakeOverInput();
            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
            if (_reckoning.gameObject.activeSelf) _reckoning.gameObject.SetActive(false);
            NavSceneReuse.ForgetFocusMemory();

            _reckoning.Show(Reward(), pages);
            yield return null;
            yield return null;
            yield return ReckoningRevealWait.UntilChoosable(_reckoning);
        }

        private static T Part<T>(string name) where T : Component =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(c => c.name == name && c.gameObject.scene.IsValid());

        // ---- the cache -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator AGearPageShowsTheBundleOfItsClass(
            [Values(EncounterClass.Normal, EncounterClass.Elite, EncounterClass.Boss)] EncounterClass cls)
        {
            var pages = ReckoningPageFixtures.Settle(new List<EncounterClass> { cls });
            yield return Show(pages);

            string expected = cls == EncounterClass.Boss ? "reward_cache_large"
                : cls == EncounterClass.Elite ? "reward_cache_medium"
                : "reward_cache_small";
            Assert.AreEqual(expected, Part<Image>("ReckoningCacheArt").sprite.name);
        }

        // Relic chips come from elites and bosses only.
        [UnityTest]
        public IEnumerator ARelicPageShowsTheTinOfItsClass(
            [Values(EncounterClass.Elite, EncounterClass.Boss)] EncounterClass cls)
        {
            var pages = ReckoningPageFixtures.Settle(new List<EncounterClass>(), new List<EncounterClass> { cls });
            Assert.AreEqual(RewardPageKind.Relic, pages[0].Kind, "fixture: the relic page is first");
            yield return Show(pages);

            string expected = cls == EncounterClass.Boss ? "reward_relic_large"
                : "reward_relic_medium";
            Assert.AreEqual(expected, Part<Image>("ReckoningCacheArt").sprite.name);
        }

        // ---- the chain -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnInertCardWearsTheChain_AndItsFaceCarriesNoWords_TheTooltipSaysWhy()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            var inert = pages[0].Cards[0];
            inert.Inert = true;
            yield return Show(pages);

            var chain = Part<Image>("ReckoningOffer0Chain");
            Assert.AreEqual("reward_chain", chain.sprite.name);
            Assert.AreEqual(Color.white, chain.color, "the chain lies over the card, untinted");
            Assert.AreEqual("", Part<TMP_Text>("ReckoningOffer0Feature").text, "no inert note on the face");

            // The first card already holds the screen's own focus, which opens
            // no detail; move off it and back.
            EventSystemFocus("ReckoningLeaveButton");
            yield return null;
            EventSystemFocus("ReckoningOffer0");
            yield return null;
            yield return null;
            StringAssert.Contains(PrincesPalace.Domain.UiKit.UiStrings.ReckoningInert.Format(),
                Part<TMP_Text>("ReckoningCardTipBody").text, "the words are the tooltip's");
        }

        [UnityTest]
        public IEnumerator AnOrdinaryCardHasNoChain()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            yield return Show(pages);

            Assert.AreEqual(0f, Part<Image>("ReckoningOffer0Chain").color.a, 0.001f);
        }

        // ---- the glyphs ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWhetstoneIsAGlyphAlone_AndTheDoorLeadsItsFaceWithThePayment()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            yield return Show(pages);

            Assert.AreEqual("glyph_whetstone", Part<Image>("ReckoningHone0Glyph").sprite.name);
            Assert.IsFalse(Part<TMP_Text>("ReckoningHone0Count").gameObject.activeSelf, "no number beside a lone whetstone");
            Assert.AreEqual("glyph_door", Part<Image>("ReckoningLeaveDoorGlyph").sprite.name);
            Assert.AreEqual("glyph_whetstone", Part<Image>("ReckoningLeaveHoneGlyph").sprite.name);
            Assert.AreEqual("1", Part<TMP_Text>("ReckoningLeaveHoneCount").text, "one unused choice, one whetstone");

            var door = Part<Image>("ReckoningLeaveDoorGlyph").rectTransform.anchoredPosition.x;
            var whetstone = Part<Image>("ReckoningLeaveHoneGlyph").rectTransform.anchoredPosition.x;
            Assert.Less(door, whetstone, "the door comes first");
        }

        [UnityTest]
        public IEnumerator TheDeltaLineLeadsWithTheStatGlyph()
        {
            var pages = ReckoningPageFixtures.OneGearPage();
            yield return Show(pages);

            var glyph = Part<Image>("ReckoningOffer0DeltaGlyph");
            Assert.IsTrue(glyph.enabled);
            var face = RewardCardFace.OfGear(SaveSlotManager.CurrentSave, pages[0].Cards[0]);
            Assert.IsTrue(face.HasDelta, "fixture: the card changes a stat");
            Assert.AreEqual(face.DeltaGlyphId, glyph.sprite.name, "the glyph the face names is the one drawn");
            StringAssert.StartsWith("glyph_", glyph.sprite.name);
        }

        private static void EventSystemFocus(string name) =>
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Part<Button>(name).gameObject);
    }
}
