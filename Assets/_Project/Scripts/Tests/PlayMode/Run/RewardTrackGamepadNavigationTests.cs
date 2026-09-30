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
    // The reward track's pad map, as the CHARACTER tab's REWARD TRACK section
    // (spec section 9: "as today, entered from the rail"), driven through the
    // REAL production dispatcher on the REAL Hub scene -- a scripted BaseInput
    // via inputOverride, yield return null, assert resulting state.
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

        // Same path RewardTrackClaimTests/RewardTrackLifecycleTests take: the
        // track is the dossier's REWARD TRACK section, inside a pane of the
        // system menu, inside the hub.
        //
        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). The dossier is
        // put back on LOADOUT first, so the track's OnEnable replays against
        // this test's character when its row is pressed. The squad edits land
        // on this test's own fresh save before the section shows.
        private IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SharedScene.Ensure("Hub");

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the hub has no CharacterDossierController");
            dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            NavSceneReuse.CloseHubModals();
            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            MenuMemory.Clear();
            yield return null;

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
                character.claimedTrackLevel = claimed;
                character.unspentStatPoints = 0;
            }

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            _menu.Open();
            _menu.Select(0);
            yield return null;

            var row = Find(TrackRow);
            Assert.IsNotNull(row, "the dossier's rail has no REWARD TRACK row");
            row.onClick.Invoke();

            // TWICE. Start() runs one frame after SetActive(true), not
            // synchronously -- .claude/rules/tests.md.
            yield return null;
            yield return null;

            // The section's own fly-in glides toward the CURRENT level when it
            // shows -- settled here, so this file's own dispatcher-driven
            // ScrollTo assertions are not racing it.
            yield return Settle(0.5f);
        }

        // The rail's REWARD TRACK row: the fourth section (CharacterSections).
        private const string TrackRow = "DossierSection3";

        private SystemMenuController _menu;

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

        // Right from the rail's REWARD TRACK row lands on the player's own
        // level's disc, the one the fly-in centred.
        [UnityTest]
        public IEnumerator RightFromTheTrackRow_EntersOnThePlayersOwnDisc()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            EventSystem.current.SetSelectedGameObject(Find(TrackRow).gameObject);
            yield return null;

            yield return Push(1f, 0f);

            Assert.AreSame(Find("TrackDot10").gameObject, Selected, "Right from the rail should enter on the current level's disc");
        }

        [UnityTest]
        public IEnumerator UpFromADisc_ReachesTheCollectButton_WhichClaimsExactlyOnce()
        {
            // owed > 0: earned level 12, paid through 10.
            yield return OpenTheTrack(level: 12, claimed: 10);

            var entryDot = Find($"TrackDot{RewardTrackLayout.FirstLevel}");
            var collectButton = Find("TrackCollectButton");
            Assert.IsTrue(collectButton.gameObject.activeSelf, "fixture: owed should be > 0 for this test to mean anything");

            EventSystem.current.SetSelectedGameObject(entryDot.gameObject);
            yield return null;

            // Up from ANY disc reaches COLLECT, the summary row's button --
            // proven from the very first disc, not a specially-chosen one.
            yield return Push(0f, 1f);
            Assert.AreSame(collectButton.gameObject, Selected, "Up from the rail should reach the collect button");

            var character = DossierPane.Shown();
            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(12, character.claimedTrackLevel,
                "Submit on the collect button should claim through the earned level, exactly once");
        }

        // With nothing owed there is no COLLECT: Up from a disc and Left from
        // the first disc leave the track for the rail row it was entered
        // from, and the ribbon is the floor.
        [UnityTest]
        public IEnumerator WithNothingOwed_TheTrackLeavesForItsRailRow_AndTheRibbonIsTheFloor()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            var ribbon = Find("TrackRibbonGrab");
            Assert.IsFalse(Find("TrackCollectButton").gameObject.activeSelf, "fixture: nothing should be owed");

            EventSystem.current.SetSelectedGameObject(Find("TrackDot10").gameObject);
            yield return null;

            yield return Push(0f, -1f);
            Assert.AreSame(ribbon.gameObject, Selected, "Down from the rail should reach the ribbon");

            yield return Push(0f, -1f);
            Assert.AreSame(ribbon.gameObject, Selected, "Down from the ribbon should go nowhere");

            EventSystem.current.SetSelectedGameObject(Find("TrackDot10").gameObject);
            yield return null;
            yield return Push(0f, 1f);
            Assert.AreSame(Find(TrackRow).gameObject, Selected, "Up from a disc with nothing owed should reach the rail row");

            EventSystem.current.SetSelectedGameObject(Find($"TrackDot{RewardTrackLayout.FirstLevel}").gameObject);
            yield return null;
            yield return Push(-1f, 0f);
            Assert.AreSame(Find(TrackRow).gameObject, Selected, "Left from the first disc should reach the rail row");
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
            Assert.AreEqual(10, DossierPane.Shown().claimedTrackLevel,
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

        // The track is a section, not a level: with nothing pending, B closes
        // the menu (plan C6).
        [UnityTest]
        public IEnumerator Cancel_OnTheTrack_ClosesTheMenu()
        {
            yield return OpenTheTrack(level: 10, claimed: 10);

            EventSystem.current.SetSelectedGameObject(Find("TrackDot10").gameObject);
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen, "B on the track should close the menu");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        }

        // THE CLASS OF THE BUG, not the instance: every control the track
        // owns is Explicit, and every link out of it goes to the dossier's
        // REWARD TRACK row or the selected tab -- never onto a control of a
        // section that is not showing.
        [UnityTest]
        public IEnumerator EveryTrackControl_IsExplicit_AndLeavesOnlyForItsRailRowOrTheTab()
        {
            foreach (var owed in new[] { false, true })
            {
                yield return OpenTheTrack(level: 12, claimed: owed ? 10 : 12);

                var panel = Rect("RewardTrackPanel");
                var row = Find(TrackRow).transform;
                var tab = Find("SystemTab" + _menu.SelectedTab).transform;

                foreach (var selectable in panel.GetComponentsInChildren<Selectable>(includeInactive: false))
                {
                    var nav = selectable.navigation;
                    Assert.AreEqual(UnityEngine.UI.Navigation.Mode.Explicit, nav.mode,
                        $"{selectable.name} navigates by screen position (owed: {owed})");

                    foreach (var target in new[] { nav.selectOnUp, nav.selectOnDown, nav.selectOnLeft, nav.selectOnRight })
                    {
                        if (target == null) continue;
                        Assert.IsTrue(target.transform.IsChildOf(panel) || target.transform == row || target.transform == tab,
                            $"{selectable.name} links to {target.name}, outside the track, its rail row and the tab (owed: {owed})");
                    }
                }
            }
        }
    }
}
