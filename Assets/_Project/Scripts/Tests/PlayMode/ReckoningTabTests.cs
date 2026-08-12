using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The Reckoning's three tabs.
    //
    // ReckoningTests covers the payout page. This covers what was added around
    // it: the tab strip, the relics the descent is carrying, and the per-fight
    // combat tally -- the last of which is the ledger's third and final
    // consumer, after the run and the defeat screen.
    public class ReckoningTabTests
    {
        private string _root;
        private ReckoningController _reckoning;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-tabs-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
            RunManager.StartRun(4242);
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _reckoning.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private string TextOf(string name) => Named(name).GetComponent<TMP_Text>().text;

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        private IEnumerator OpenTheFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
        }

        // A reward whose ledger numbers are distinctive enough that finding
        // them on screen cannot be a coincidence.
        private static CombatReward Reward(string characterId, string displayName)
        {
            var reward = new CombatReward { GoldGained = 77 };
            reward.Characters.Add(new CharacterReward(characterId, displayName,
                levelBefore: 3, expBefore: 10, levelAfter: 3, expAfter: 60,
                expToNextAfter: 100, expGained: 50));

            reward.Ledger.Dealt(characterId, DamageType.Physical, 1234);
            reward.Ledger.Dealt(characterId, DamageType.Fire, 567);
            reward.Ledger.Took(characterId, 890);
            reward.Ledger.Restored(characterId, 345);
            reward.Ledger.ScoredKill(characterId);
            reward.Ledger.ScoredKill(characterId);

            return reward;
        }

        private IEnumerator ShowAReckoning(string characterId = "shawn")
        {
            yield return OpenTheFight();

            _reckoning.Show(Reward(characterId, "Shawn"), new List<ItemOffer>());
            yield return null;
        }

        // ---- the tabs -------------------------------------------------------------

        [UnityTest]
        public IEnumerator ItOpensOnTheSpoilsRatherThanWhereverYouLeftIt()
        {
            // Reopening on the last-read tab would bury the gold and the loot
            // behind a click, on the one screen whose job is showing them.
            yield return ShowAReckoning();

            Assert.IsTrue(Named("ReckoningSpoilsPage").activeSelf);
            Assert.IsFalse(Named("ReckoningRelicsPage").activeSelf);
            Assert.IsFalse(Named("ReckoningTallyPage").activeSelf);
        }

        [UnityTest]
        public IEnumerator EachTabShowsItsOwnPageAndHidesTheOthers()
        {
            yield return ShowAReckoning();

            var pages = new[] { "ReckoningSpoilsPage", "ReckoningRelicsPage", "ReckoningTallyPage" };

            for (int i = 0; i < pages.Length; i++)
            {
                Click($"ReckoningTab{i}");
                yield return null;

                for (int j = 0; j < pages.Length; j++)
                {
                    Assert.AreEqual(i == j, Named(pages[j]).activeSelf,
                        $"tab {i} left {pages[j]} in the wrong state");
                }
            }
        }

        [UnityTest]
        public IEnumerator ReopeningResetsBackToTheSpoils()
        {
            yield return ShowAReckoning();

            Click("ReckoningTab2");
            yield return null;
            Assert.IsTrue(Named("ReckoningTallyPage").activeSelf);

            _reckoning.Show(Reward("shawn", "Shawn"), new List<ItemOffer>());
            yield return null;

            Assert.IsTrue(Named("ReckoningSpoilsPage").activeSelf, "the tab did not reset on reopen");
        }

        // ---- the tally --------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheTallyCarriesThisFightsLedger()
        {
            // The ledger's THIRD consumer, after the run's totals and the
            // defeat screen. Until this existed the numbers a whole fight of
            // counting produced were visible only after you died.
            yield return ShowAReckoning();

            Click("ReckoningTab2");
            yield return null;

            Assert.IsTrue(Named("ReckoningTally0").activeSelf);

            string stats = TextOf("ReckoningTally0Stats");
            StringAssert.Contains("1801", stats, "total dealt (1234 + 567) is missing");
            StringAssert.Contains("1234", stats, "the physical split is missing");
            StringAssert.Contains("567", stats, "the elemental split is missing");
            StringAssert.Contains("890", stats, "damage taken is missing");
            StringAssert.Contains("345", stats, "healing is missing");
            StringAssert.Contains("2", TextOf("ReckoningTally0Kills"));
        }

        [UnityTest]
        public IEnumerator ACharacterWithNoLedgerLineReadsAsZeroes()
        {
            // A benched member is a true statement worth rendering; a missing
            // row is a bug the player has to guess at.
            var reward = new CombatReward();
            reward.Characters.Add(new CharacterReward("ghost", "Ghost",
                3, 0, 3, 0, 100, 0, isDowned: true));

            yield return OpenTheFight();
            _reckoning.Show(reward, new List<ItemOffer>());
            yield return null;

            Click("ReckoningTab2");
            yield return null;

            Assert.IsTrue(Named("ReckoningTally0").activeSelf);
            StringAssert.Contains("0", TextOf("ReckoningTally0Stats"));
            Assert.IsNotEmpty(TextOf("ReckoningTally0Name"));
        }

        // ---- the relics -------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRelicsTabShowsWhatTheDescentIsCarrying()
        {
            var relic = Content.ContentDatabase.Relics.First();
            RunManager.Run.relicIds = new List<string> { relic.id };

            yield return ShowAReckoning();

            Click("ReckoningTab1");
            yield return null;

            Assert.IsTrue(Named("ReckoningRelic0").activeSelf, "the run's relic is not listed");
            Assert.AreEqual(relic.displayName, TextOf("ReckoningRelic0Name"));
            Assert.IsFalse(Named("ReckoningRelicEmpty").activeSelf);
        }

        [UnityTest]
        public IEnumerator CarryingNothingSaysSoRatherThanShowingABlankPage()
        {
            RunManager.Run.relicIds = new List<string>();

            yield return ShowAReckoning();

            Click("ReckoningTab1");
            yield return null;

            Assert.IsTrue(Named("ReckoningRelicEmpty").activeSelf);
            Assert.IsFalse(Named("ReckoningRelic0").activeSelf);
        }

        [UnityTest]
        public IEnumerator ARelicIdNamingContentThatIsGoneStillGetsARow()
        {
            // The player IS carrying something. A silently shorter list would
            // be worse than an honest unknown.
            RunManager.Run.relicIds = new List<string> { "relic_that_was_deleted" };

            yield return ShowAReckoning();

            Click("ReckoningTab1");
            yield return null;

            Assert.IsTrue(Named("ReckoningRelic0").activeSelf);
            Assert.IsNotEmpty(TextOf("ReckoningRelic0Name"));
        }

        // ---- the animation ------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheGainCounterEndsOnTheRealNumber()
        {
            // It COUNTS UP during the bar fill, so the only thing worth pinning
            // is where it lands -- a counter that animated to the wrong total
            // would look right for most of its life.
            yield return ShowAReckoning();

            yield return new WaitForSecondsRealtime(1.4f);

            StringAssert.Contains("50", TextOf("ReckoningRow0Gain"));
        }

        [UnityTest]
        public IEnumerator TheFrameSettlesAtRestRatherThanStayingLifted()
        {
            // The lift is an entrance. A frame that never arrived would sit
            // permanently 46px low and nothing else would report it.
            yield return ShowAReckoning();

            yield return new WaitForSecondsRealtime(1.4f);

            var frame = Named("ReckoningFrame").GetComponent<RectTransform>();

            Assert.AreEqual(0f, frame.anchoredPosition.y, 0.01f, "the frame never finished lifting");
            Assert.AreEqual(1f, frame.localScale.x, 0.01f, "the frame never finished expanding");
        }

        [UnityTest]
        public IEnumerator TheGloomEndsFullyOnRatherThanPartWayThrough()
        {
            yield return ShowAReckoning();

            yield return new WaitForSecondsRealtime(1.4f);

            var dimmer = Named("ReckoningPanelDimmer").GetComponent<Image>();

            Assert.Greater(dimmer.color.a, 0.6f, "the gloom never finished fading up");
        }
    }
}
