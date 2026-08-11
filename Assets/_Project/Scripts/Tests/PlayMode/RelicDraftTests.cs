using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The draft, driven through the real hub scene.
    //
    // RelicPool's rules are covered without a scene by RelicPoolTests. What is
    // left for PlayMode is only what genuinely needs one: that the gate button
    // actually reaches the draft, that choosing writes to the run, and -- the
    // one that matters most -- that a resumed run does not get asked again.
    public class RelicDraftTests
    {
        private string _root;
        private HubController _hub;
        private RelicDraftController _draft;
        private int _navigations;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-draft-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            _navigations = 0;
            Navigation.LoadOverride = _ => _navigations++;
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");

            _draft = _hub.GetComponentInChildren<RelicDraftController>(includeInactive: true);
            Assert.IsNotNull(_draft, "the draft was never wired into the hub");
        }

        [UnityTest]
        public IEnumerator TheDraftStartsClosedAndTheGateOpensIt()
        {
            yield return OpenTheHub();
            Assert.IsFalse(_draft.gameObject.activeSelf);

            Click("StartRunGate");
            yield return null;
            yield return null;

            Assert.IsTrue(_draft.gameObject.activeSelf, "beginning a descent did not offer a relic");
            Assert.AreEqual(0, _navigations, "the map loaded before the draft was answered");
        }

        [UnityTest]
        public IEnumerator ThreeCardsAreOfferedAndTheyAreAllDifferent()
        {
            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;

            var shown = Enumerable.Range(0, 3)
                .Select(i => Named($"DraftCard{i}"))
                .Where(go => go != null && go.activeSelf)
                .ToList();

            Assert.AreEqual(3, shown.Count, "the starting pool should be able to fill three cards");

            var names = shown
                .Select(go => go.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

            CollectionAssert.AllItemsAreUnique(names, "the same relic was offered twice");
        }

        [UnityTest]
        public IEnumerator TakingOneWritesItToTheRunAndReachesTheMap()
        {
            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;

            Click("DraftCard0");
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            CollectionAssert.IsNotEmpty(RunManager.Run.relicIds, "nothing was taken");
            Assert.IsTrue(RunManager.Run.relicDrafted);
            Assert.AreEqual(1, _navigations, "the descent never started");
            Assert.IsFalse(_draft.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator DescendingWithoutChoosingIsAllowedAndStillCountsAsDrafted()
        {
            // Declining is a legal answer. What must NOT happen is being asked
            // again, which is why relicDrafted is its own flag rather than
            // being inferred from the list being empty.
            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;

            Click("DraftDescendButton");
            yield return null;

            Assert.IsEmpty(RunManager.Run.relicIds);
            Assert.IsTrue(RunManager.Run.relicDrafted, "declining would be asked again forever");
        }

        [UnityTest]
        public IEnumerator PressingTheSameCardTwiceDeselectsIt()
        {
            // The screen's whole job is comparing three things. A first click
            // that is final would be a trap.
            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;

            Click("DraftCard0");
            yield return null;
            Click("DraftCard0");
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            Assert.IsEmpty(RunManager.Run.relicIds, "the second press did not undo the first");
        }

        [UnityTest]
        public IEnumerator ResumingARunDoesNotOfferAgain()
        {
            // THE one that matters. Offering on every hub visit would let a
            // player re-roll the draft by walking back and forth, which is the
            // same class of problem as a map that regenerates.
            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            int before = _navigations;

            Click("StartRunGate");
            yield return null;

            Assert.IsFalse(_draft.gameObject.activeSelf, "a resumed run was offered a second relic");
            Assert.AreEqual(before + 1, _navigations, "resuming did not go straight to the map");
        }

        [UnityTest]
        public IEnumerator TheOfferSurvivesReloadingBeforeChoosing()
        {
            // Rolled from the run's own seed, so quitting to the hub and coming
            // back cannot re-roll a draft the player did not like.
            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;

            var first = Enumerable.Range(0, 3)
                .Select(i => Named($"DraftCard{i}").GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

            yield return OpenTheHub();
            Click("StartRunGate");
            yield return null;
            yield return null;

            var second = Enumerable.Range(0, 3)
                .Select(i => Named($"DraftCard{i}").GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

            CollectionAssert.AreEqual(first, second, "the draft re-rolled itself on reload");
        }
    }
}
