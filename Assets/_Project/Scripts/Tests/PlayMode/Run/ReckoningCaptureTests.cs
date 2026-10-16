using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Captures the Reckoning's CHOICE phase, which no static capture can reach.
    //
    // ScreenshotTool renders Edit Mode and only knows top-level registered
    // panels; the Reckoning is a sub-panel inside the fight that starts
    // inactive and is driven by Show(). AUDIT #43 is the record of the tool
    // reporting success for this exact panel while never capturing it.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // ReckoningCaptureTests.
    public class ReckoningCaptureTests
    {
        private string _root;
        private ReckoningController _reckoning;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shot-" + System.Guid.NewGuid().ToString("N"));
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

        private static CombatReward Reward()
        {
            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn",
                1, 0, 1, 79, 100, 79, expToNextBefore: 100));
            return reward;
        }

        // Real pages, rolled the way a won fight rolls them: one normal gear
        // chip is three cards for the squad's recipients.
        private static IReadOnlyList<PendingPage> Offers() => ReckoningPageFixtures.OneGearPage();

        [UnityTest]
        public IEnumerator CaptureTheChoicePhase()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            _reckoning.Show(Reward(), Offers());

            // REAL SECONDS, not a frame count. Every animation on this screen
            // runs on Time.unscaledDeltaTime, which reports wall-clock and is
            // NOT affected by Time.captureFramerate -- and a PlayMode frame in
            // batchmode costs well under a millisecond. "Wait 240 frames" was
            // therefore about a tenth of a second against a 0.34s wipe, and the
            // capture came out with the frame still clipped to a third of its
            // width, which reads as a broken panel rather than as an
            // unfinished animation.
            yield return new WaitForSecondsRealtime(2.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Reckoning_choice.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[ReckoningCapture] wrote {path}");
        }

        // A boss and two anchors settled together: the gear page with five
        // cards and three choices, a card taken, then the relic page.
        [UnityTest]
        public IEnumerator CaptureTheBossSettlementPages()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            _reckoning.Show(Reward(), pages);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shoot("Reckoning_boss_gear.png");

            Press("ReckoningOffer1");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("Reckoning_boss_gear_taken.png");

            Press("ReckoningLeaveButton");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("Reckoning_boss_gear_left.png");

            Press("ReckoningLeaveButton");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shoot("Reckoning_boss_relic.png");
        }

        private void Press(string name) =>
            _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == name).onClick.Invoke();

        private IEnumerator Shoot(string file)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, file);
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[ReckoningCapture] wrote {path}");
            yield return null;
        }

        // ---- docs/PLAN_REWARD_SCREENS.md R0 fixtures ----------------------------
        //
        // One file name per state, fixed across phases, so a before/after pair
        // from any later phase lines up. R2 (card face, cache opening, ghosts),
        // R4 (reveal) and R5 (board, outcome board) change what each of these
        // shows; the data they set up does not change. States the screen cannot
        // yet draw (the cache, ghost cards, the board) seed the data that will
        // produce them and capture today's rendering.
        private const string NoGraphics =
            "No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests";

        private IEnumerator OpenFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");
        }

        // The run is started here rather than inside Settle so a fixture can
        // set run state (legendary pity) before the page rolls.
        private static void StartFixtureRun()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"\[RealmOpening\]"));
            RunManager.StartRun(ReckoningPageFixtures.Seed);
        }

        private static CombatReward PartyReward()
        {
            var reward = Reward();
            reward.Characters.Add(new CharacterReward("odette", "Odette", 1, 0, 1, 40, 100, 40, expToNextBefore: 100));
            reward.Characters.Add(new CharacterReward("bjorn", "Bjorn", 1, 0, 1, 60, 100, 60, expToNextBefore: 100));
            return reward;
        }

        // A normal fight's page: three cards, none above the ordinary ladder.
        [UnityTest]
        public IEnumerator CaptureTheNoCachePage()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            StartFixtureRun();
            _reckoning.Show(Reward(), ReckoningPageFixtures.OneGearPage());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shoot("Reckoning_nocache_page.png");
        }

        // Pity one page short of its cap forces a Legendary on the boss gear
        // page, the top-tier gear a cache will hold.
        [UnityTest]
        public IEnumerator CaptureTheCachePageWithATopTierUpgrade()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            StartFixtureRun();
            RunManager.Run.legendaryPity = GearPage.LegendaryPityPages - 1;
            _reckoning.Show(Reward(), ReckoningPageFixtures.BossAndTwoAnchors());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shoot("Reckoning_cache_toptier_page.png");
        }

        // The boss gear page is the five-card row. A ghost is the item the
        // wearer already wears in the card's slot (docs/PLAN_REWARD_SCREENS.md
        // C5), so cards 0-3 get their wearer+slot filled and card 4's slot is
        // emptied: the row shows ghost present and ghost absent. No card is
        // marked inert; the inert case is left to the live-upgrade fixtures R1/R2
        // add.
        [UnityTest]
        public IEnumerator CaptureTheFiveCardRowWithGhosts()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            StartFixtureRun();
            var pages = ReckoningPageFixtures.BossAndTwoAnchors();
            var cards = pages[0].Cards;
            Assert.AreEqual(5, cards.Count, "the boss gear page is no longer five cards");

            // A second, ordinary page supplies the worn items (any item that
            // fits the slot); rolled after the boss page, which it must not
            // disturb, and discarded from the run's pending pages after.
            var wornSource = ReckoningPageFixtures.Settle(
                new List<EncounterClass> { EncounterClass.Normal, EncounterClass.Normal, EncounterClass.Normal },
                nodeId: 3)[0].Cards;
            RunManager.Run.pendingPages = pages.ToList();

            var save = SaveSlotManager.CurrentSave;
            for (int i = 0; i < cards.Count; i++)
            {
                var who = save.roster.First(c => c != null && c.definitionId == cards[i].RecipientId);
                if (i == cards.Count - 1)
                {
                    EquipmentOps.Unequip(MenuContext.Descent, save, who, cards[i].Slot);
                    Assert.IsNull(who.equipment.GetInstance(cards[i].Slot), "the no-ghost slot is not empty");
                    continue;
                }

                var worn = wornSource.FirstOrDefault(c => c.IsGear && c.RecipientId == cards[i].RecipientId &&
                                                          c.Slot == cards[i].Slot) ?? cards[i];
                var definition = ContentDatabase.DefinitionOf(worn.Copy);
                InventoryOps.Add(save.stockpiledItems, worn.Copy, 1);
                Assert.IsTrue(EquipmentOps.Equip(MenuContext.Descent, save, who, worn.Copy, definition.equipSlot,
                    definition.IsEquippable, cards[i].Slot), $"could not wear card {i}'s slot");
            }

            _reckoning.Show(Reward(), pages);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shoot("Reckoning_five_ghosts_page.png");
        }

        // The full party after one pick: today's spoils/tally pages stand in
        // for the board.
        [UnityTest]
        public IEnumerator CaptureTheBoard()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            StartFixtureRun();
            _reckoning.Show(PartyReward(), ReckoningPageFixtures.OneGearPage());
            yield return new WaitForSecondsRealtime(1f);
            Press("ReckoningOffer0");
            Press("ReckoningLeaveButton");
            yield return new WaitForSecondsRealtime(3f);
            yield return Shoot("Reckoning_board.png");
        }

        // A run fight that paid nothing: the party fell, no offers.
        [UnityTest]
        public IEnumerator CaptureTheOutcomeBoard()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            var tally = PartyReward().Characters
                .Select(c => new TallyMember(c.CharacterId, c.DisplayName)).ToList();
            _reckoning.Show(ReckoningRequest.Outcome(
                FightEnding.Fell, new List<string>(), new List<string>(), tally, new CombatLedger()));
            yield return new WaitForSecondsRealtime(3f);
            yield return Shoot("Reckoning_outcome_board.png");
        }

        // The summary phase's Continue plate against the spoils and tally
        // rows, at 16:9 and 4:3. The canvas renders at 1 unit per pixel, so the
        // 4:3 shot is the same canvas cropped to 1440x1080.
        [UnityTest]
        public IEnumerator CaptureTheSummaryPages()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            var reward = Reward();
            reward.Characters.Add(new CharacterReward("odette", "Odette", 1, 0, 1, 40, 100, 40, expToNextBefore: 100));
            reward.Characters.Add(new CharacterReward("bjorn", "Bjorn", 1, 0, 1, 60, 100, 60, expToNextBefore: 100));

            _reckoning.Show(reward, Offers());
            yield return new WaitForSecondsRealtime(1f);

            var offer = _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "ReckoningOffer0");
            offer.onClick.Invoke();

            // The page waits to be moved on; the same button carries on.
            _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "ReckoningLeaveButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(3f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);
            Directory.CreateDirectory(OutputDir);

            var tabs = _reckoning.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true);
            foreach (var page in new[] { (tab: "ReckoningTab0", label: "spoils"), (tab: "ReckoningTab2", label: "tally") })
            {
                tabs.First(b => b.name == page.tab).onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.5f);

                CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, $"Reckoning_summary_{page.label}_16x9.png"), 1920, 1080);
                CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, $"Reckoning_summary_{page.label}_4x3.png"), 1440, 1080);
            }
        }
    }
}
