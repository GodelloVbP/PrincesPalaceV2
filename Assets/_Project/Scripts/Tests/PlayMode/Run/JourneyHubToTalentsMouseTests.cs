using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 6's mouse-only
    // regression. See JourneyHubToTalentsTests' own header for why the
    // refused case is PrerequisiteMissing rather than the brief's own
    // NotAuthored (a content fact, not a stable one to pin), and why the two
    // outcomes are two separate reconstructed journeys rather than one
    // chained session.
    //
    // TalentBackButton, clicked directly, replaces PressCancel here -- unlike
    // segment 3/9's "open the menu" transition, this one has a REAL clickable
    // control standing in for Cancel (TalentScreen's own Back button, the
    // same handler HandleCancel/Cancel reaches), so this file uses it rather
    // than falling back to the ESC key.
    public class JourneyHubToTalentsMouseTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-talents-mouse-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string TalentRefusalText() =>
            Node("TalentDetailRefusal")?.GetComponent<TMP_Text>()?.text ?? "";

        private IEnumerator ReachTalentsFromTheHub()
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            // No Grid walk needed -- a mouse click reaches TalentsBuilding
            // directly, the same real Button the pad's three-press walk
            // (gate -> CharacterSheetBuilding -> PrincipalityBuilding ->
            // TalentsBuilding) eventually lands Submit on.
            yield return Click(Node("TalentsBuilding")); // Navigation.Go(Talents), a REAL scene load
            yield return WaitForScene("Talents", 5f, "clicking TalentsBuilding should load the Talents scene");
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private IEnumerator ClickBackToTheHub()
        {
            yield return Click(Node("TalentBackButton"));
            yield return WaitForScene("Hub", 5f, "clicking Back should take the same path Cancel does, back to the Hub");
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
        }

        [UnityTest]
        public IEnumerator ATier1Orb_BeforeItsRootIsUnlocked_IsRefused_BackReturnsToTheHub_MouseOnly()
        {
            yield return ReachTalentsFromTheHub();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();

            var talents = Object.FindAnyObjectByType<TalentController>(FindObjectsInactive.Include);
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");
            talents.Refresh();
            yield return null;

            yield return Click(Node("Orb0_2")); // OnOrbPressed -> selects this orb, reveals the detail panel
            yield return Click(Node("InvestButton")); // Kindle() -> TalentOps.Kindle refuses: PrerequisiteMissing

            Assert.AreEqual(0, character.unlockedTalentIds.Count,
                "a tier-1 orb clicked before its root is unlocked should not have kindled anything");
            Assert.IsNotEmpty(TalentRefusalText(),
                "the refusal should be painted, not a silent dead press on an unreachable-looking orb");

            yield return ClickBackToTheHub();
        }

        [UnityTest]
        public IEnumerator TheRootOrb_AlwaysEligible_KindlesExactlyOnce_BackReturnsToTheHub_MouseOnly()
        {
            yield return ReachTalentsFromTheHub();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();

            var talents = Object.FindAnyObjectByType<TalentController>(FindObjectsInactive.Include);
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");
            talents.Refresh();
            yield return null;

            yield return Click(Node("Orb0_0")); // OnOrbPressed on the root
            yield return Click(Node("InvestButton")); // Kindle() -> TalentOps.Kindle succeeds: the root is free and parentless

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "clicking InvestButton should kindle the root exactly once");

            yield return ClickBackToTheHub();
        }
    }
}
