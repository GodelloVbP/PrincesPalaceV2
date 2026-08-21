using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.PlayModeTests
{
    // The reward track paying out, through the screen that does the paying.
    //
    // THROUGH THE PANEL, not by calling ClaimTrackRewards directly, and that is
    // the whole design of this file. The claim has exactly ONE production call
    // site -- RewardTrackController.Claim -- and a test that reached past it
    // would pass just as happily if that site were deleted, leaving a game
    // where stat points can no longer be collected at all and a green suite
    // saying otherwise.
    //
    // That argument is inherited rather than invented: it is the one
    // RewardApplierTests carried while the applier was the call site
    // (architecture_audit.md F17, AUDIT #46). The site moved when collection
    // became manual, so the coverage moved with it.
    public class RewardTrackClaimTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            // BEFORE the scene loads, which is the ordering that matters: the
            // hub reads the save on the way up, and an override applied after
            // would test against the developer's own save file and then write
            // to it.
            _root = Path.Combine(Path.GetTempPath(), "pp-track-claim-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Opens the hub, levels the squad, and gets the reward track on screen.
        //
        // The same path SystemMenuCaptureTests takes, because it is the only
        // one there is: the panel is an inactive child of the dossier, inside a
        // pane of the system menu, inside the hub.
        private static IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
                character.claimedTrackLevel = claimed;
                character.unspentStatPoints = 0;
                character.earnedFavor = 0;
                character.bonusMaxHealth = 0;
                character.bonusExpPermille = 0;
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
            // synchronously -- docs/CODE_STANDARDS.md section 5, and clicking a
            // button before it has had that frame is the flake this avoids.
            yield return null;
            yield return null;
        }

        private static Button Find(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == name);

        private static Character First() => SaveSlotManager.CurrentSave.ActiveSquad()[0];

        // The migration case, end to end: a character carrying twenty-nine
        // unclaimed levels presses one button and is settled.
        [UnityTest]
        public IEnumerator CollectAllSettlesEveryWaitingLevel()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            var collect = Find("TrackCollectButton");
            Assert.IsNotNull(collect, "the reward track has no collect button");
            Assert.IsTrue(collect.gameObject.activeInHierarchy,
                "the collect button is hidden while thirty levels are owed - " +
                "it appearing IS the notification that the track owes something");

            collect.onClick.Invoke();
            yield return null;

            var character = First();
            Assert.AreEqual(30, character.claimedTrackLevel,
                "pressing collect did not move the watermark to the level reached");
            Assert.Greater(character.unspentStatPoints, 0,
                "no stat points were handed over for twenty-nine levels of track");
            Assert.Greater(character.earnedFavor, 0,
                "no Favor was handed over for twenty-nine levels of track");
        }

        // The bug the watermark exists to prevent, now reachable the way a
        // player would actually reach it: press collect twice.
        [UnityTest]
        public IEnumerator TheSameLevelsAreNeverPaidTwice()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            var collect = Find("TrackCollectButton");
            collect.onClick.Invoke();
            yield return null;

            int points = First().unspentStatPoints;
            int favor = First().earnedFavor;

            // The button has hidden itself by now, which is most of the answer.
            // Pressing it anyway is the half that matters: hiding a control is
            // presentation, and a claim that paid again when invoked would be a
            // bug wearing a disabled button.
            collect.onClick.Invoke();
            yield return null;

            Assert.AreEqual(points, First().unspentStatPoints,
                "stat points were paid a second time for levels already collected");
            Assert.AreEqual(favor, First().earnedFavor,
                "Favor was paid a second time for levels already collected");
        }

        // Nothing owed means nothing to press. The button is the count of what
        // is waiting, so a squad in lockstep must not see one.
        [UnityTest]
        public IEnumerator WithNothingOwedThereIsNoCollectButton()
        {
            yield return OpenTheTrack(level: 30, claimed: 30);

            var collect = Find("TrackCollectButton");
            Assert.IsNotNull(collect, "the reward track has no collect button node at all");
            Assert.IsFalse(collect.gameObject.activeInHierarchy,
                "the collect button is offering to collect nothing");
        }

        // CLICKING A WAITING NODE collects up to it, which is the other half of
        // section 4 and the one the collect button is a shortcut for.
        //
        // Claiming is SEQUENTIAL: pressing level 20 with a watermark at 0
        // settles everything from 2 to 20 and stops, so claimedTrackLevel stays
        // a single integer and there is never a collected hole below an
        // uncollected level.
        [UnityTest]
        public IEnumerator PressingAWaitingNodeCollectsEverythingUpToIt()
        {
            yield return OpenTheTrack(level: 30, claimed: 0);

            var node = Find("TrackDot20");
            Assert.IsNotNull(node, "the rail has no node for level 20");

            node.onClick.Invoke();
            yield return null;

            // 30, NOT 20.
            //
            // ClaimTrackRewards settles the whole gap to the character's level
            // rather than to an arbitrary stopping point, and pinning that here
            // is deliberate: the alternative -- paying only as far as the node
            // pressed -- needs a second number on the save ("paid to here, but
            // only this much was asked for"), and the watermark's entire value
            // is that there is one.
            Assert.AreEqual(30, First().claimedTrackLevel,
                "pressing a waiting node settled part of the debt and left the rest");
        }

        // A node that cannot be collected must still do something. A hundred
        // dots that do nothing when pressed teach the player that none of them
        // do, and the ones that DO matter become invisible.
        [UnityTest]
        public IEnumerator PressingAnUnreachedNodeGlidesRatherThanClaiming()
        {
            yield return OpenTheTrack(level: 30, claimed: 30);

            var content = Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(r => r.name == "TrackContent");
            Assert.IsNotNull(content, "the reward track has no content rect");

            float before = content.anchoredPosition.x;

            var node = Find("TrackDot90");
            Assert.IsNotNull(node, "the rail has no node for level 90");
            node.onClick.Invoke();

            // The glide is 460ms and the fly-in it cancels is longer, so this
            // waits out both rather than sampling mid-flight.
            yield return new WaitForSecondsRealtime(0.8f);

            Assert.AreEqual(30, First().claimedTrackLevel,
                "pressing an unreached node paid something out");
            Assert.AreNotEqual(before, content.anchoredPosition.x,
                "pressing an unreached node did nothing at all");
        }
    }
}
