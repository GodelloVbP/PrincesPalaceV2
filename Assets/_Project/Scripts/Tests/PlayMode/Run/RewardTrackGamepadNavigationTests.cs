using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Step C's behavioural gate (docs/GAMEPAD_NAVIGATION_PLAN.md phase 2):
    // the ribbon as a Rail group, driven through the REAL production
    // dispatcher on the REAL Hub scene -- a scripted BaseInput via
    // inputOverride, yield return null, assert resulting state. Same shape
    // as SystemMenuGamepadNavigationTests, and the same setup
    // RewardTrackClaimTests/RewardTrackLifecycleTests already use to reach
    // this panel (a child of the dossier, inside the system menu).
    public class RewardTrackGamepadNavigationTests
    {
        private string _root;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-track-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            // Every cue on this screen runs off Time.unscaledDeltaTime, so
            // this collapses the fly-in/glide waits to well under a frame --
            // matching RewardTrackClaimTests/RewardTrackLifecycleTests.
            RewardTrackController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            RewardTrackController.SpeedMultiplier = 1f;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Same path RewardTrackClaimTests/RewardTrackLifecycleTests take:
        // the panel is an inactive child of the dossier, inside a pane of
        // the system menu, inside the hub.
        private IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
                character.claimedTrackLevel = claimed;
                character.unspentStatPoints = 0;
            }

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            menu.Open();
            menu.Select(0);
            yield return null;

            var row = menu.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name == "DossierTrackRow");
            Assert.IsNotNull(row, "the dossier has no reward-track row");
            row.onClick.Invoke();

            // TWICE. Start() runs one frame after SetActive(true), not
            // synchronously -- docs/CODE_STANDARDS.md section 5.
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            // The panel's own fly-in glides toward the CURRENT level on
            // open (RewardTrackController's Motion half) -- settled here,
            // same technique RewardTrackLifecycleTests uses, so this file's
            // own dispatcher-driven ScrollTo assertions are not racing it.
            yield return Settle(0.5f);
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private static IEnumerator Settle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static RectTransform Rect(string name) =>
            Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.name == name);

        private static Button Find(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .FirstOrDefault(b => b.name == name);

        [UnityTest]
        public IEnumerator MovingAlongTheRail_ThroughTheDispatcher_ScrollsToTheSelectedNode()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            int firstLevel = RewardTrackLayout.FirstLevel;
            var entryDot = Find($"TrackDot{firstLevel}");
            var nextDot = Find($"TrackDot{firstLevel + 1}");
            Assert.IsNotNull(entryDot, "the track has no first dot");
            Assert.IsNotNull(nextDot, "the track has no second dot");

            EventSystem.current.SetSelectedGameObject(entryDot.gameObject);
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();

            Assert.AreSame(nextDot.gameObject, EventSystem.current.currentSelectedGameObject,
                "one Right press on the rail should move selection to the next dot");

            var content = Rect("TrackContent");
            var viewport = Rect("TrackViewport");
            Assert.IsNotNull(content, "the reward track has no content rect");
            Assert.IsNotNull(viewport, "the reward track has no viewport rect");

            float expected = RewardTrackLayout.ScrollFor(firstLevel + 1, viewport.rect.width);
            Assert.AreEqual(expected, content.anchoredPosition.x, 1f,
                "selecting the next dot should have scrolled the rail to it via ScrollTo, the same call a " +
                "mouse hover already makes -- selecting a node is a distinct action from revealing it, but " +
                "one must still drive the other");
        }

        [UnityTest]
        public IEnumerator ReachingAndSubmittingTheCollectButton_ClaimsExactlyOnce()
        {
            // owed > 0: earned level 12, paid through 10.
            yield return OpenTheTrack(level: 12, claimed: 10);

            var entryDot = Find($"TrackDot{RewardTrackLayout.FirstLevel}");
            Assert.IsNotNull(entryDot, "the track has no first dot");

            var collectButton = Find("TrackCollectButton");
            Assert.IsNotNull(collectButton, "the track has no collect button");
            Assert.IsTrue(collectButton.gameObject.activeSelf, "fixture: owed should be > 0 for this test to mean anything");

            EventSystem.current.SetSelectedGameObject(entryDot.gameObject);
            yield return null;

            // Down from ANY dot reaches the collect button (plan section
            // 7/9a: a required action reachable from the rail) -- proven
            // from the very first dot, not a specially-chosen one.
            _input.Vertical = -1f;
            yield return DriveFrame();

            Assert.AreSame(collectButton.gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from the rail should reach the collect button");

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First();
            int before = character.claimedTrackLevel;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(12, character.claimedTrackLevel,
                "Submit on the collect button should claim through the earned level, exactly once");
            Assert.AreNotEqual(before, character.claimedTrackLevel, "the claim should actually have happened");
        }
    }
}
