using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;

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

            // The gate now plays a ~0.7s mock-up transition before it does
            // any of the things this file checks -- see HubController's
            // BeginDescentTransition. Set absurdly high rather than to
            // something merely fast: AnimateZoom/FadeToBlack decide whether
            // to yield AFTER scaling elapsed time by this multiplier, so each
            // phase's own loop resolves on its first check regardless of how
            // fast batchmode happens to be framing. That is NOT the same as
            // the whole transition finishing in the same frame the gate was
            // pressed in, though -- BeginDescentTransition strings three such
            // phases together with `yield return AnimateZoom(...)` /
            // `yield return FadeToBlack(...)`, and stepping from one to the
            // next still costs Unity's coroutine driver a real engine frame
            // apiece. PressStartRunGateAndWaitForTheDraft (and the resume
            // wait in ResumingARunDoesNotOfferAgain) poll for the actual
            // outcome instead of assuming a fixed frame count for exactly
            // that reason.
            HubController.MotionSpeedMultiplier = 100000f;
        }

        [TearDown]
        public void Restore()
        {
            HubController.MotionSpeedMultiplier = 1f;
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

        // Presses the gate and waits for the mock-up transition to hand off
        // to the draft, POLLED rather than a fixed frame count.
        //
        // MotionSpeedMultiplier (see HubController's own comment on it)
        // makes each AnimateZoom/FadeToBlack phase resolve on its very first
        // check, but stepping from one of BeginDescentTransition's own
        // `yield return AnimateZoom(...)` statements to the next still costs
        // Unity's coroutine driver a real engine frame per phase -- three
        // phases, so up to three real frames before Open() actually runs,
        // not the one a naive reading of "the multiplier clears it inside a
        // single check" would suggest. A fixed two-frame wait passed most of
        // the time and failed unpredictably depending on where in a run this
        // particular click landed; polling with a bounded real-time deadline
        // is exact either way and still fails the calling test (via its own
        // subsequent assertion) if the draft genuinely never opens.
        private IEnumerator PressStartRunGateAndWaitForTheDraft()
        {
            Click("StartRunGate");

            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_draft.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator TheDraftStartsClosedAndTheGateOpensIt()
        {
            yield return OpenTheHub();
            Assert.IsFalse(_draft.gameObject.activeSelf);

            yield return PressStartRunGateAndWaitForTheDraft();

            Assert.IsTrue(_draft.gameObject.activeSelf, "beginning a descent did not offer a relic");
            Assert.AreEqual(0, _navigations, "the map loaded before the draft was answered");
        }

        [UnityTest]
        public IEnumerator ThreeCardsAreOfferedAndTheyAreAllDifferent()
        {
            yield return OpenTheHub();
            yield return PressStartRunGateAndWaitForTheDraft();

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
            yield return PressStartRunGateAndWaitForTheDraft();

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
            yield return PressStartRunGateAndWaitForTheDraft();

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
            yield return PressStartRunGateAndWaitForTheDraft();

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
            yield return PressStartRunGateAndWaitForTheDraft();
            Click("DraftDescendButton");
            yield return null;

            int before = _navigations;

            Click("StartRunGate");

            // POLLED for the same reason PressStartRunGateAndWaitForTheDraft
            // is: the mock-up transition costs up to three real engine
            // frames stepping through its own phases regardless of
            // MotionSpeedMultiplier, whether it ends at the draft or (like
            // here, on resume) walks straight past it to the map.
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_navigations == before && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_draft.gameObject.activeSelf, "a resumed run was offered a second relic");
            Assert.AreEqual(before + 1, _navigations, "resuming did not go straight to the map");
        }

        [UnityTest]
        public IEnumerator TheOfferSurvivesReloadingBeforeChoosing()
        {
            // Rolled from the run's own seed, so quitting to the hub and coming
            // back cannot re-roll a draft the player did not like.
            yield return OpenTheHub();
            yield return PressStartRunGateAndWaitForTheDraft();

            var first = Enumerable.Range(0, 3)
                .Select(i => Named($"DraftCard{i}").GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

            yield return OpenTheHub();
            yield return PressStartRunGateAndWaitForTheDraft();

            var second = Enumerable.Range(0, 3)
                .Select(i => Named($"DraftCard{i}").GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

            CollectionAssert.AreEqual(first, second, "the draft re-rolled itself on reload");
        }

        // ---- level 70: choose your starting relics --------------------------------

        private static int VisibleCards() => 3;

        private List<string> OnScreenNames() =>
            Enumerable.Range(0, VisibleCards())
                .Select(i => Named($"DraftCard{i}"))
                .Where(go => go != null && go.activeSelf)
                .Select(go => go.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

        [UnityTest]
        public IEnumerator BelowLevelSeventyTheDraftIsStillThreeRandomCards()
        {
            yield return OpenTheHub();
            yield return PressStartRunGateAndWaitForTheDraft();

            Assert.IsFalse(Named("DraftNextPage").activeSelf,
                "an unlevelled squad is shown paging it has not earned");
            Assert.IsFalse(Named("DraftPageLabel").activeSelf);
        }

        [UnityTest]
        public IEnumerator LevelSeventyOffersTheWholePoolInsteadOfADraw()
        {
            yield return OpenTheHub();
            LevelTheSquadTo(70);

            yield return PressStartRunGateAndWaitForTheDraft();

            Assert.IsTrue(Named("DraftNextPage").activeSelf,
                "the whole pool is on offer and there is no way to page through it");

            // Every relic the player is allowed to see, gathered by paging.
            var seen = new List<string>();
            seen.AddRange(OnScreenNames());

            int guard = 0;
            while (Named("DraftNextPage").GetComponent<Button>().interactable && guard++ < 20)
            {
                Click("DraftNextPage");
                yield return null;
                seen.AddRange(OnScreenNames());
            }

            Assert.Less(guard, 20, "paging never reached the end");

            // Mechanic (g): a relic can also be gated on the party having a
            // convergence/ultimate ability -- this test's squad has none,
            // so a relic requiring one (Rampaging Bull's Horn) is correctly
            // absent from the pool even though its own unlockedBy is empty.
            int expected = ContentDatabase.Relics.Count(r =>
                r != null && r.Data.IsUnlockedFromTheStart && !r.Data.RequiresConvergenceAbility);
            Assert.GreaterOrEqual(seen.Distinct().Count(), expected,
                $"paging showed {seen.Distinct().Count()} relics but {expected} are unlocked from the start");
        }

        // THE PAGE TURN MUST NOT LOSE THE CHOICE. _selected indexes the whole
        // offer rather than the three cards, precisely so a player who picks
        // something and then looks at the next page still has it picked -- the
        // same trap the deselect-on-second-press rule exists to avoid, arriving
        // from the other direction.
        [UnityTest]
        public IEnumerator AChoiceSurvivesLookingAtTheNextPage()
        {
            yield return OpenTheHub();
            LevelTheSquadTo(70);

            yield return PressStartRunGateAndWaitForTheDraft();

            string picked = OnScreenNames().First();
            Click("DraftCard0");
            yield return null;

            Click("DraftNextPage");
            yield return null;
            Click("DraftPrevPage");
            yield return null;

            Click("DraftDescendButton");
            yield return null;

            Assert.AreEqual(1, RunManager.Run.relicIds.Count,
                "the selection was lost by paging away and back");

            string takenName = ContentDatabase.Relics.First(r => r.id == RunManager.Run.relicIds[0]).Data.DisplayName;
            Assert.AreEqual(picked, takenName, "a different relic was taken than the one chosen");
        }

        // ---- the reward track's starting relics ---------------------------------

        private static void LevelTheSquadTo(int level)
        {
            foreach (var character in Save.ActiveSquad())
            {
                character.level = level;
            }

            SaveSlotManager.SaveCurrent();
        }

        [UnityTest]
        public IEnumerator ALevelledSquadDraftsMoreThanOneRelic()
        {
            yield return OpenTheHub();
            LevelTheSquadTo(25);

            yield return PressStartRunGateAndWaitForTheDraft();

            // Round one.
            Click("DraftCard0");
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            Assert.AreEqual(1, RunManager.Run.relicIds.Count, "the first pick did not land");
            Assert.IsTrue(_draft.gameObject.activeSelf,
                "the draft closed after one pick, so the level-25 reward hands out nothing");

            // Round two.
            Click("DraftCard0");
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            Assert.AreEqual(2, RunManager.Run.relicIds.Count);
            Assert.IsFalse(_draft.gameObject.activeSelf, "the draft ran past the two rounds it was owed");
            Assert.IsTrue(RunManager.Run.relicDrafted);
        }

        [UnityTest]
        public IEnumerator AnUnlevelledSquadStillDraftsExactlyOne()
        {
            // The fallback is part of the reward: getting it wrong takes the
            // draft away from every character below level 25.
            yield return OpenTheHub();
            yield return PressStartRunGateAndWaitForTheDraft();

            Click("DraftCard0");
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            Assert.AreEqual(1, RunManager.Run.relicIds.Count);
            Assert.IsFalse(_draft.gameObject.activeSelf, "one relic is still the whole draft at level 1");
        }

        [UnityTest]
        public IEnumerator TheSecondRoundDoesNotOfferWhatWasAlreadyTaken()
        {
            // Draft() draws without replacement WITHIN an offer, which was the
            // whole story when there was only one offer. Across rounds nothing
            // stopped a relic coming back, and being offered what you are
            // already carrying reads as a bug.
            yield return OpenTheHub();
            LevelTheSquadTo(60);

            yield return PressStartRunGateAndWaitForTheDraft();

            Click("DraftCard0");
            yield return null;
            Click("DraftDescendButton");
            yield return null;

            string taken = RunManager.Run.relicIds[0];

            var offered = Enumerable.Range(0, 3)
                .Select(i => Named($"DraftCard{i}"))
                .Where(go => go != null && go.activeSelf)
                .Select(go => go.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(t => t.name.EndsWith("Name")).text)
                .ToList();

            var takenName = ContentDatabase.Relics.First(r => r.id == taken).Data.DisplayName;

            CollectionAssert.DoesNotContain(offered, takenName,
                "the second round offered the relic the first round just took");
        }

        [UnityTest]
        public IEnumerator DecliningEndsTheWholeDraftRatherThanAskingAgain()
        {
            // A player who does not want what is on offer should not have to
            // press Descend four times to say so. The alternative -- re-offering
            // until they accept -- is a draft they cannot leave, which the
            // Descend button exists to prevent.
            yield return OpenTheHub();
            LevelTheSquadTo(60);

            yield return PressStartRunGateAndWaitForTheDraft();

            Click("DraftDescendButton");
            yield return null;

            Assert.IsEmpty(RunManager.Run.relicIds);
            Assert.IsFalse(_draft.gameObject.activeSelf, "declining did not end the draft");
            Assert.IsTrue(RunManager.Run.relicDrafted);
        }
    }
}
