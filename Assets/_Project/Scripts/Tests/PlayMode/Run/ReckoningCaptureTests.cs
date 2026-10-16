using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Stats;
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
            yield return ShootPage("Reckoning_choice.png");
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
            yield return ShootPage("Reckoning_boss_gear.png");

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

        // Character ids are the content's (sheep, owl, bear), which is what
        // the portraits and the ledger key on.
        // The whole party after a fight that paid: rings part-way round (one
        // crossing a level), deeds in every column, a potion and a book held.
        private static CombatReward PartyReward()
        {
            var reward = new CombatReward { GoldGained = 46, GoldBefore = 240 };
            reward.Characters.Add(new CharacterReward("sheep", "Shawn", 1, 20, 1, 70, 100, 50, slotIndex: 0, expToNextBefore: 100));
            reward.Characters.Add(new CharacterReward("owl", "Odette", 2, 85, 3, 30, 140, 85, slotIndex: 1, expToNextBefore: 120));
            reward.Characters.Add(new CharacterReward("bear", "Bjorn", 1, 5, 1, 45, 100, 40, slotIndex: 2, expToNextBefore: 100));
            reward.Ledger.Dealt("sheep", DamageType.Physical, 412);
            reward.Ledger.Took("sheep", 96);
            reward.Ledger.ScoredKill("sheep");
            reward.Ledger.Dealt("owl", DamageType.Fire, 288);
            reward.Ledger.Restored("owl", 54);
            reward.Ledger.Took("owl", 31);
            reward.Ledger.ScoredKill("owl");
            reward.Ledger.ScoredKill("owl");
            reward.Ledger.Dealt("bear", DamageType.Physical, 530);
            reward.Ledger.Took("bear", 140);
            reward.Ledger.ScoredKill("bear");
            reward.Prizes.Add(new PaidPrize(PrizeKind.Consumable, PrizeKind.Consumable, 1, new[] { "health_potion" }));
            reward.Prizes.Add(new PaidPrize(PrizeKind.Book, PrizeKind.Book, 0, new[] { "test_book" }));
            reward.Prizes.Add(new PaidPrize(PrizeKind.None, PrizeKind.Gold, 46));
            return reward;
        }

        // A PAGE SHOT SHOWS THE ROW AS IT IS, not the screen's focus: no
        // marker on the first card and no detail over the others, so the row
        // itself can be judged (the ghost gate). Rendered in the same frame
        // the focus is cleared: on the next one NavigationInputModule's
        // reselection rule puts the pad's focus back on the entry card, which
        // is right for play and wrong only for this picture. The marker is
        // hidden by hand because it only follows the selection in LateUpdate.
        //
        // The reveal is settled first through the screen's own silent settle,
        // so no card is caught face-down however slow the pace.
        private IEnumerator ShootPage(string file)
        {
            yield return SettleTheReveal();
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            foreach (var marker in Object.FindObjectsByType<FocusMarker>(FindObjectsInactive.Exclude))
            {
                var image = marker.GetComponent<UnityEngine.UI.Image>();
                if (image != null) image.enabled = false;
            }

            yield return Shoot(file);
        }

        [UnityTest]
        public IEnumerator CaptureTheNoCachePage()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            _reckoning.Show(Reward(), ReckoningPageFixtures.NoCachePage());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ShootPage("Reckoning_nocache_page.png");
        }

        [UnityTest]
        public IEnumerator CaptureTheCachePageWithATopTierUpgrade()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            _reckoning.Show(Reward(), ReckoningPageFixtures.CacheTopTierPages());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ShootPage("Reckoning_cache_toptier_page.png");
        }

        [UnityTest]
        public IEnumerator CaptureTheFiveCardRowWithGhosts()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            _reckoning.Show(Reward(), ReckoningPageFixtures.FiveGhostsPages());
            yield return new WaitForSecondsRealtime(2.5f);
            yield return ShootPage("Reckoning_five_ghosts_page.png");
        }

        // The full party after one pick: the board (C8).
        [UnityTest]
        public IEnumerator CaptureTheBoard()
        {
            if (!CanvasCapture.IsSupported) Assert.Ignore(NoGraphics);

            yield return OpenFight();
            ReckoningPageFixtures.StartRun();
            _reckoning.Show(PartyReward(), ReckoningPageFixtures.OneGearPage());
            yield return TakeFirstAndLeave();
            yield return Shoot("Reckoning_board.png");
        }

        // Settles the reveal (a press during it only skips it), takes the first
        // card, leaves the page and settles the sweep and the board.
        private IEnumerator TakeFirstAndLeave()
        {
            yield return SettleTheReveal();
            Press("ReckoningOffer0");
            Press("ReckoningLeaveButton");
            yield return null;
            _reckoning.SettleBeat();
            yield return null;
            yield return null;
        }

        // The screen's own silent settle, then the guard it leaves after a
        // skip (a press inside it is dropped), so the next press lands.
        private IEnumerator SettleTheReveal()
        {
            yield return null;
            _reckoning.SettleBeat();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!_reckoning.AcceptsChoices && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_reckoning.AcceptsChoices, "the reveal never released its guard");
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
                FightEnding.Fell, new List<string>(), new List<string>(), tally, PartyReward().Ledger));
            yield return null;
            _reckoning.SettleBeat();
            yield return null;
            yield return Shoot("Reckoning_outcome_board.png");
        }

        // The board's Continue plate against the haul and the portraits, at
        // 16:9 and 4:3. The canvas renders at 1 unit per pixel, so the
        // 4:3 shot is the same canvas cropped to 1440x1080.
        [UnityTest]
        public IEnumerator CaptureTheSummaryPages()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ReckoningCaptureTests");
            }

            yield return OpenFight();
            ReckoningPageFixtures.StartRun();
            _reckoning.Show(PartyReward(), ReckoningPageFixtures.OneGearPage());
            yield return TakeFirstAndLeave();

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);
            Directory.CreateDirectory(OutputDir);

            CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, "Reckoning_summary_board_16x9.png"), 1920, 1080);
            CanvasCapture.RenderToFile(canvas, Path.Combine(OutputDir, "Reckoning_summary_board_4x3.png"), 1440, 1080);
        }
    }
}
