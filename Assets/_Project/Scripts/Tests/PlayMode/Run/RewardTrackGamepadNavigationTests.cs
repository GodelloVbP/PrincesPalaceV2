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
            NavSceneReuse.AfterTest(_input);
            RewardTrackController.SpeedMultiplier = 1f;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Same path RewardTrackClaimTests/RewardTrackLifecycleTests take:
        // the panel is an inactive child of the dossier, inside a pane of
        // the system menu, inside the hub.
        //
        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). The track panel
        // is shut through its own close button FIRST -- shutting only the
        // menu around it would leave it activeSelf, and its OnEnable would
        // then replay against the previous test's character on the next
        // open -- and then the menu. The squad edits below land on this
        // test's own fresh save either way, before the panel opens and reads
        // them.
        private IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SharedScene.Ensure("Hub");

            var close = Find("TrackCloseButton");
            if (close != null && close.gameObject.activeInHierarchy) close.onClick.Invoke();
            NavSceneReuse.CloseHubModals();
            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
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

        // One discrete stick press: pushed for a frame, then released for a
        // frame so the dispatcher's move gate re-arms before the next one.
        private IEnumerator Push(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        private static GameObject Selected => EventSystem.current.currentSelectedGameObject;

        private static TMPro.TMP_Text CardLevel() =>
            Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.name == "TrackCardLevel");

        [UnityTest]
        public IEnumerator DownFromTheRail_ReachesTheRibbon_ThenTheCollectButton_WhichClaimsExactlyOnce()
        {
            // owed > 0: earned level 12, paid through 10.
            yield return OpenTheTrack(level: 12, claimed: 10);

            var entryDot = Find($"TrackDot{RewardTrackLayout.FirstLevel}");
            var ribbon = Find("TrackRibbonGrab");
            var collectButton = Find("TrackCollectButton");
            Assert.IsNotNull(entryDot, "the track has no first dot");
            Assert.IsNotNull(ribbon, "the track has no ribbon");
            Assert.IsNotNull(collectButton, "the track has no collect button");
            Assert.IsTrue(collectButton.gameObject.activeSelf, "fixture: owed should be > 0 for this test to mean anything");

            EventSystem.current.SetSelectedGameObject(entryDot.gameObject);
            yield return null;

            // Down from ANY dot reaches the ribbon -- proven from the very
            // first dot, not a specially-chosen one -- and Down again the
            // footer, in the order the three rows are drawn.
            yield return Push(0f, -1f);
            Assert.AreSame(ribbon.gameObject, Selected, "Down from the rail should reach the ribbon slider");

            yield return Push(0f, -1f);
            Assert.AreSame(collectButton.gameObject, Selected, "Down from the ribbon should reach the collect button");

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First();
            int before = character.claimedTrackLevel;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(12, character.claimedTrackLevel,
                "Submit on the collect button should claim through the earned level, exactly once");
            Assert.AreNotEqual(before, character.claimedTrackLevel, "the claim should actually have happened");
        }

        [UnityTest]
        public IEnumerator WithNothingOwed_TheRibbonLeadsToClose_AndCloseLeadsBackUp()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            var ribbon = Find("TrackRibbonGrab");
            var close = Find("TrackCloseButton");
            Assert.IsFalse(Find("TrackCollectButton").gameObject.activeSelf, "fixture: nothing should be owed");

            EventSystem.current.SetSelectedGameObject(ribbon.gameObject);
            yield return null;

            yield return Push(0f, -1f);
            Assert.AreSame(close.gameObject, Selected, "with no collect button, Down from the ribbon should reach CLOSE");

            // CLOSE is the panel's corner: nothing further left or down.
            yield return Push(-1f, 0f);
            Assert.AreSame(close.gameObject, Selected, "Left from a lone CLOSE should go nowhere, not onto the dossier underneath");
            yield return Push(0f, -1f);
            Assert.AreSame(close.gameObject, Selected, "Down from CLOSE should go nowhere");

            yield return Push(0f, 1f);
            Assert.AreSame(ribbon.gameObject, Selected, "Up from CLOSE should return to the ribbon");
        }

        [UnityTest]
        public IEnumerator LeftRightOnTheRibbon_ScrubsTheRail_AndUpLandsOnTheDiscInView()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            var ribbon = Find("TrackRibbonGrab");
            var content = Rect("TrackContent");
            var viewport = Rect("TrackViewport");

            EventSystem.current.SetSelectedGameObject(ribbon.gameObject);
            yield return null;

            float before = content.anchoredPosition.x;

            yield return Push(1f, 0f);
            yield return Settle(0.3f);

            Assert.AreSame(ribbon.gameObject, Selected, "Right on the ribbon adjusts it; it must not move focus away");
            Assert.Less(content.anchoredPosition.x, before - RewardTrackLayout.NodePitch,
                "Right on the ribbon should scrub the rail forward by more than a disc");

            int inView = RewardTrackLayout.LevelAtCentre(content.anchoredPosition.x);

            yield return Push(0f, 1f);
            Assert.AreSame(Find($"TrackDot{inView}").gameObject, Selected,
                "Up from a scrubbed ribbon should land on the disc now in the middle of the window, not the one scrolled away");
            Assert.AreEqual(RewardTrackLayout.ScrollFor(inView, viewport.rect.width), content.anchoredPosition.x, 1f);
        }

        [UnityTest]
        public IEnumerator SubmitOnALockedDisc_PutsItOnTheCard_AndClaimsNothing()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            const int Locked = 20;
            EventSystem.current.SetSelectedGameObject(Find($"TrackDot{Locked}").gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return Settle(0.4f);

            Assert.IsNotNull(CardLevel(), "the track has no card level label");
            Assert.AreEqual(Locked.ToString(), CardLevel().text,
                "A on a reward not yet reached should show that reward on the card");
            Assert.AreEqual(10, SaveSlotManager.CurrentSave.ActiveSquad().First().claimedTrackLevel,
                "a locked disc must not claim anything");
        }

        [UnityTest]
        public IEnumerator MovingAlongTheRail_TheCardFollowsTheSelection()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            EventSystem.current.SetSelectedGameObject(Find("TrackDot30").gameObject);
            yield return null;
            yield return Push(1f, 0f);
            yield return Settle(0.4f);

            Assert.AreEqual("31", CardLevel().text, "the card should describe the disc the pad is on");
        }

        [UnityTest]
        public IEnumerator Cancel_ClosesTheTrackOnly_AndHandsFocusBackToItsRow()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            var panel = Rect("RewardTrackPanel");
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsTrue(panel.gameObject.activeInHierarchy, "fixture: the track should be open");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(panel.gameObject.activeInHierarchy, "B should close the reward track");
            Assert.IsTrue(menu.IsOpen, "B on the track should close the track, not the whole menu");
            Assert.AreEqual("DossierTrackRow", Selected != null ? Selected.name : null,
                "closing the track should hand focus back to the row that opened it");
        }

        // THE CLASS OF THE BUG, not the instance: every control this panel
        // owns is Explicit, and every link it has stays inside the panel.
        // Automatic navigation on one of them is how the pad walked onto
        // dossier rows drawn underneath -- a focus marker and an A press on
        // something the player could not see.
        [UnityTest]
        public IEnumerator EveryTrackControl_IsExplicit_AndNeverLinksOutsideThePanel()
        {
            foreach (var owed in new[] { false, true })
            {
                yield return OpenTheTrack(level: 12, claimed: owed ? 10 : 12);

                var panel = Rect("RewardTrackPanel");
                foreach (var selectable in panel.GetComponentsInChildren<Selectable>(includeInactive: false))
                {
                    var nav = selectable.navigation;
                    Assert.AreEqual(UnityEngine.UI.Navigation.Mode.Explicit, nav.mode,
                        $"{selectable.name} navigates by screen position and can walk onto the dossier underneath (owed: {owed})");

                    foreach (var target in new[] { nav.selectOnUp, nav.selectOnDown, nav.selectOnLeft, nav.selectOnRight })
                    {
                        if (target == null) continue;
                        Assert.IsTrue(target.transform.IsChildOf(panel),
                            $"{selectable.name} links to {target.name}, which is outside the track panel (owed: {owed})");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ThePanelGround_TakesThePointer_SoNothingUnderneathCanBeHovered()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            var fill = Object.FindObjectsByType<Image>(FindObjectsInactive.Include)
                .FirstOrDefault(i => i.name == "RewardTrackPanelFill");
            Assert.IsNotNull(fill, "the track has no panel ground");
            Assert.IsTrue(fill.raycastTarget,
                "the ground must block raycasts, or hovering and clicking empty track space reaches the dossier behind it");
        }
    }
}
