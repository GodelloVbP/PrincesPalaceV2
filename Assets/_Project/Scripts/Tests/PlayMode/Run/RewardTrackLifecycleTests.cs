using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The reward track's rail while it is still moving, and across a close.
    //
    // THE PANEL IS CLOSED AND REOPENED CONSTANTLY -- it is a child of the
    // dossier, inside a pane of the system menu -- and it is the screen that
    // pays out stat points, spells and second lives. Those two facts together
    // are what make its motion worth testing: every animation here writes the
    // same content rect, the claim underneath is money, and OnDisable's
    // StopAllCoroutines is the only thing standing between "closed mid-glide"
    // and "reopened still travelling toward a level the player has left".
    //
    // RewardTrackClaimTests pins the PAYOUT through this panel. What it does
    // not do is interrupt anything: it presses once, waits, and reads the save.
    //
    // scenarios A15, B2, C9, D4 (docs/hunt/SCENARIOS.md).
    public class RewardTrackLifecycleTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            // BEFORE the scene loads: the hub reads the save on the way up, and
            // an override applied after would test against the developer's own
            // save file and then write to it.
            _root = Path.Combine(Path.GetTempPath(), "pp-track-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            // Every cue on this screen runs off Time.unscaledDeltaTime (see
            // RewardTrackController.Motion's own SpeedMultiplier comment), so
            // this collapses the waits below to well under a frame.
            RewardTrackController.SpeedMultiplier = 40f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- A15: a second node pressed while the first glide is live -------------

        // ONE GLIDE, AND IT ENDS ON THE NODE THAT WAS PRESSED LAST. CancelGlide
        // is what makes that true: every start goes through it, so the rail
        // never has two coroutines lerping the same anchoredPosition from two
        // different `from` values -- which would not look like a stutter, it
        // would look like the rail settling somewhere neither node is.
        [UnityTest]
        public IEnumerator ASecondNodePressedMidGlideEndsTheRailOnTheSecondNode()
        {
            yield return OpenTheTrack(level: 30, claimed: 30);

            var content = Rect("TrackContent");
            var viewport = Rect("TrackViewport");
            Assert.IsNotNull(content, "the reward track has no content rect");
            Assert.IsNotNull(viewport, "the reward track has no viewport");

            // 38 and 10, far apart on a forty-level rail -- the pair used to
            // be 90 and 50, which the progression v2 cap removed.
            Press("TrackDot38");
            yield return null;

            float midGlide = content.anchoredPosition.x;

            Press("TrackDot10");
            yield return Settle(0.25f);

            float expected = RewardTrackLayout.ScrollFor(10, viewport.rect.width);

            Assert.AreEqual(expected, content.anchoredPosition.x, 1f,
                "the rail did not end on the node pressed last - two glides were running against the " +
                "same rect and it settled wherever the survivor stopped");
            Assert.AreNotEqual(midGlide, content.anchoredPosition.x,
                "fixture: the rail never moved after the second press");

            // NOTHING PAID. Level 10 is already collected and level 38 has
            // not been reached, so neither press has anything to claim and a
            // glide that also claimed would be visible here.
            Assert.AreEqual(30, SquadFixture.FirstLiveMember().claimedTrackLevel,
                "gliding to a node paid something out");
        }

        // ---- B2: closed mid-fly-in, opened again ----------------------------------

        // OnDisable's own comment: "a panel closed mid-glide would reopen still
        // animating toward a level the player has since left". The reopen has
        // to be a FIRST paint again -- level, scroll, hover and burst rigs all
        // back where a fresh open puts them.
        [UnityTest]
        public IEnumerator ClosedMidFlyInItReopensAtTheFlyInsOwnDestinationWithNothingLeftLit()
        {
            yield return OpenTheTrack(level: 30, claimed: 30);

            var panel = Named("RewardTrackPanel");
            var content = Rect("TrackContent");
            var viewport = Rect("TrackViewport");

            // Pushed somewhere the fly-in would never leave it, then closed
            // while the glide toward that node is still live.
            Press("TrackDot38");
            yield return null;
            panel.SetActive(false);
            yield return null;

            // A burst rig left lit is the specific thing OnDisable's second
            // paragraph is about: StopAllCoroutines kills whatever would have
            // switched it off.
            foreach (var rig in BurstRigs())
            {
                Assert.IsFalse(rig.activeSelf,
                    $"{rig.name} was left lit by the close, so the next visit opens on a frozen " +
                    "explosion hanging over a node");
            }

            panel.SetActive(true);
            yield return Settle(0.25f);

            float expected = RewardTrackLayout.ScrollFor(30, viewport.rect.width);
            Assert.AreEqual(expected, content.anchoredPosition.x, 1f,
                "reopening the panel did not fly back to the player's own level - it resumed from " +
                "wherever the glide it was closed during had got to");

            // AND IT IS STILL WIRED EXACTLY ONCE. Wire() is guarded by _wired,
            // so a node press after the cycle claims once, not twice.
            SquadFixture.FirstLiveMember().claimedTrackLevel = 0;
            Refresh();
            yield return null;

            Press("TrackDot20");
            yield return null;

            Assert.AreEqual(20, SquadFixture.FirstLiveMember().claimedTrackLevel,
                "pressing a node after a close/open cycle did not collect exactly up to that node");
        }

        // ---- C9: a scene arriving over a claim ------------------------------------------

        // CLAIMS ARE IDEMPOTENT BY WATERMARK, which is what makes the bursts
        // safe to kill mid-flight: the payout happened before them. What this
        // rules out is the reverse -- a claim that a torn-down panel could
        // somehow repeat, or half-apply.
        [UnityTest]
        public IEnumerator ASceneArrivingOverTheClaimBurstsPaysExactlyOnce()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            Press("TrackCollectButton");
            yield return null;

            var member = SquadFixture.FirstLiveMember();
            int claimed = member.claimedTrackLevel;
            int points = member.unspentStatPoints;
            Assert.AreEqual(30, claimed, "fixture: the collect did not pay");
            Assert.Greater(points, 0, "fixture: no stat points were handed over");

            // Out from under the bursts.
            yield return SceneManager.LoadSceneAsync(Navigation.MainMenu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var after = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == member.definitionId);
            Assert.AreEqual(claimed, after.claimedTrackLevel,
                "the watermark moved after the panel was destroyed");
            Assert.AreEqual(points, after.unspentStatPoints,
                "stat points changed after the panel was destroyed, so a claim was still being " +
                "applied while the scene was going away");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- D4: a save taken during the bursts, then a second claim -----------------------

        // TheSameLevelsAreNeverPaidTwice presses collect twice in one session.
        // This puts a save and a reload between the two presses, which is the
        // version that matters: the watermark is the ONLY thing standing
        // between a reload and a second payout, and a reload is exactly when a
        // value held only in memory would go missing.
        [UnityTest]
        public IEnumerator ASaveTakenDuringTheBurstsSurvivesAReloadAndPaysNothingASecondTime()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            Press("TrackCollectButton");
            yield return null;

            var member = SquadFixture.FirstLiveMember();
            string characterId = member.definitionId;
            int claimed = member.claimedTrackLevel;
            int points = member.unspentStatPoints;

            // Mid-burst: off the cache and back off disk.
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            var reloaded = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == characterId);
            Assert.AreEqual(claimed, reloaded.claimedTrackLevel,
                "the watermark did not reach disk while the collection bursts were still playing");
            Assert.AreEqual(points, reloaded.unspentStatPoints,
                "the stat points did not reach disk while the bursts were still playing");

            // And the panel, still open over the reloaded save, must refuse to
            // pay again.
            Refresh();
            yield return null;
            Press("TrackCollectButton");
            yield return null;

            var again = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == characterId);
            Assert.AreEqual(points, again.unspentStatPoints,
                "collecting again after a reload paid for levels that were already collected");
        }

        // ---- fixture ----------------------------------------------------------------------

        // The same path SystemMenuCaptureTests takes, because it is the only
        // one there is: the panel is an inactive child of the dossier, inside a
        // pane of the system menu, inside the hub.
        private static IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
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
            // synchronously -- .claude/rules/tests.md.
            yield return null;
            yield return null;
        }

        private static void Refresh()
        {
            var track = Object.FindAnyObjectByType<RewardTrackController>(FindObjectsInactive.Include);
            Assert.IsNotNull(track, "the reward track panel is gone");
            track.Refresh();
        }

        private static IEnumerator Settle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static GameObject Named(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private static RectTransform Rect(string name) =>
            Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include)
                .FirstOrDefault(r => r.name == name);

        // The RIG ROOTS only -- "TrackBurst0", not "TrackBurstRays0". The
        // controller switches the roots off and leaves their children's own
        // activeSelf alone, which is correct and would make a looser match read
        // every child as still lit.
        private static GameObject[] BurstRigs() =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(t => t.name.StartsWith("TrackBurst")
                            && t.name.Substring("TrackBurst".Length).All(char.IsDigit)
                            && t.name.Length > "TrackBurst".Length)
                .Select(t => t.gameObject)
                .ToArray();

        private static void Press(string buttonName)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .FirstOrDefault(b => b.name == buttonName);

            Assert.IsNotNull(button, $"the reward track has no '{buttonName}'");
            button.onClick.Invoke();
        }
    }
}
