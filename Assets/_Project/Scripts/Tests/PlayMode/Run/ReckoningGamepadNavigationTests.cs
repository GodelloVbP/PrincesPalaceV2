using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 3b, item 2: the Reckoning. The
    // offer cards are a Rail that wraps, Submit is the card's own onClick (so
    // the pad takes an item through exactly the path the mouse does), the
    // comparison box follows the selection through job 1's shared tooltip
    // path, and the summary the choice sweeps to is a second state of the
    // same context rather than a screen with nothing operable on it.
    //
    // Driven through the REAL production dispatcher on the REAL Fight scene,
    // the shape DefeatGamepadNavigationTests established for this screen's
    // sibling -- NavigationInputModule and its Contexts stack only exist on a
    // scene's own EventSystem, so a bare controller could not be driven at
    // all.
    //
    // ONE MOVE PRESS PER TEST (SystemMenuGamepadNavigationTests' header has
    // why: the framework's own move debounce is a real-time timestamp outside
    // the scripted-input seam).
    public class ReckoningGamepadNavigationTests
    {
        private string _root;
        private ReckoningController _reckoning;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reck-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
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

        // Three offers, which is the row every player below level 50 sees --
        // and the count the Rail's wrap is worth asserting against (a Rail of
        // one has no neighbour and UiNavLinkBuilder deliberately links it
        // nowhere).
        private static List<ItemOffer> Offers()
        {
            // The HIGHEST tiers with art, so every squad member has
            // something to gain from every card and the comparison body is
            // never empty (SquadComparisonBody returning "" is a state
            // OnOfferHover treats as "nothing to show", and a test that
            // happened to land on it would be asserting against the wrong
            // reason).
            var picks = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderByDescending(i => i.tier)
                .Take(3)
                .ToList();

            Assert.AreEqual(3, picks.Count, "need three equippable items with art to offer");

            return picks.Select(i => new ItemOffer(i.id, i.tier, 0)).ToList();
        }

        private IEnumerator ShowTheReckoning(List<ItemOffer> offers)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the fight scene's EventSystem is not running NavigationInputModule");

            // Before any frame runs: a previously-loaded scene's EventSystem
            // can still be current.
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_reckoning, "the Reckoning was never wired into the fight scene");

            _reckoning.Show(Reward(), offers);
            yield return null;
        }

        private IEnumerator ShowTheChoice() => ShowTheReckoning(Offers());

        private static Selectable Control(string name) =>
            Resources.FindObjectsOfTypeAll<Selectable>()
                .FirstOrDefault(s => s.name == name && s.gameObject.scene.IsValid());

        private static GameObject Node(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid())
                ?.gameObject;

        private static GameObject Tooltip => Node("ReckoningOfferTooltip");

        private static TMPro.TMP_Text TooltipText =>
            Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
                .FirstOrDefault(t => t.name == "ReckoningOfferTooltipText" && t.gameObject.scene.IsValid());

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Press(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
        }

        private static void AssertSelected(string name, string because)
        {
            var expected = Control(name);
            Assert.IsNotNull(expected, $"the Reckoning drew no control named {name}");
            Assert.AreEqual(expected.gameObject, EventSystem.current.currentSelectedGameObject, because);
        }

        // ---- the choice ---------------------------------------------------------

        [UnityTest]
        public IEnumerator EntryIsTheFirstOfferCard()
        {
            yield return ShowTheChoice();

            AssertSelected("ReckoningOffer0",
                "the screen opens on the choice, so its entry is the leftmost card -- selected by Show() " +
                "itself rather than left for the dispatcher's next frame, so no frame draws the choice " +
                "with nothing focused");
        }

        [UnityTest]
        public IEnumerator RightFromTheFirstOffer_ReachesTheSecond()
        {
            yield return ShowTheChoice();

            yield return Press(1f, 0f);

            AssertSelected("ReckoningOffer1", "the offers are a Rail: Right steps to the next card");
        }

        [UnityTest]
        public IEnumerator LeftFromTheFirstOffer_WrapsToTheLast()
        {
            yield return ShowTheChoice();

            yield return Press(-1f, 0f);

            AssertSelected("ReckoningOffer2",
                "a Rail wraps by the owner's own default (plan section 12.3), and nothing about this row " +
                "argues for clamping: there is no neighbouring group to hand off to");
        }

        [UnityTest]
        public IEnumerator Submit_OnAnOffer_RunsThePickExactlyOnce()
        {
            yield return ShowTheChoice();

            // Counted on the BUTTON, not on what Take does: "exactly once" is
            // a claim about the dispatch (one press must not also arrive as a
            // click), and RunOrchestrator.TakeOffer's own effect is split
            // between the bag and an empty loadout slot, which would make a
            // count of the bag a weaker signal than it looks.
            int picks = 0;
            var card = (Button)Control("ReckoningOffer0");
            card.onClick.AddListener(() => picks++);

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, picks, "one Submit on a card should run its pick exactly once");
            Assert.IsTrue(_reckoning.HasTakenAnItem, "and the pick should actually have been taken");
        }

        [UnityTest]
        public IEnumerator AfterTheChoiceIsSpent_TheSelectionMovesToContinue()
        {
            yield return ShowTheChoice();

            _input.SubmitDown = true;
            yield return DriveFrame();

            // The phase change reconfigures the context; the selection follows
            // on the next Process() through the dispatcher's own reselection
            // rule (the taken card is outside the new declared set), which is
            // exactly the case that rule exists for.
            yield return null;

            AssertSelected("ReckoningContinueButton",
                "the offers are inert once one is taken, so leaving the stick on a card would strand the " +
                "player on a screen with nothing that answers Submit");
        }

        [UnityTest]
        public IEnumerator Cancel_WhileTheChoiceIsOpen_DoesNothing()
        {
            yield return ShowTheChoice();

            int dismissed = 0;
            _reckoning.Dismissed = () => dismissed++;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.AreEqual(0, dismissed,
                "Show() opens on the offers with no Continue and no skip -- you always take something -- so " +
                "there is nothing for a back press to do and the mouse path offers no way out either");
            Assert.IsFalse(_reckoning.HasTakenAnItem, "and it certainly must not pick one for the player");
            AssertSelected("ReckoningOffer0", "and the selection is where it was");
        }

        [UnityTest]
        public IEnumerator Cancel_OnTheSummary_LeavesTheSameWayContinueDoes()
        {
            // No offers at all: Show() goes straight to the summary rather
            // than putting up an empty choice with no way out, which is the
            // one phase where leaving IS on offer to the mouse.
            yield return ShowTheReckoning(new List<ItemOffer>());

            int dismissed = 0;
            _reckoning.Dismissed = () => dismissed++;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, dismissed,
                "the summary has Continue, so Cancel spends itself on the same action rather than being " +
                "a no-op the pad has to work around");
        }

        [UnityTest]
        public IEnumerator OnTheSummary_UpFromContinue_ReachesTheTabStrip()
        {
            yield return ShowTheReckoning(new List<ItemOffer>());

            AssertSelected("ReckoningContinueButton",
                "precondition: the summary's entry is its own primary action");

            yield return Press(0f, 1f);

            AssertSelected("ReckoningTab0",
                "the tabs are there to be read; Up is how a pad asks for them");
        }

        // ---- the tooltip, focus-driven (job 1) ----------------------------------

        [UnityTest]
        public IEnumerator SelectingAnOffer_ShowsItsComparison()
        {
            yield return ShowTheChoice();

            // The entry is already selected by Show(), so this is the
            // selection path with no pointer involved anywhere.
            Assert.IsTrue(Tooltip.activeSelf,
                "the selected card's comparison should be up without a cursor ever touching it");
            Assert.IsNotEmpty(TooltipText.text, "and it should have the squad comparison in it");
        }

        [UnityTest]
        public IEnumerator HoveringAnotherOfferWhileOneIsSelected_DoesNotSwapIt()
        {
            yield return ShowTheChoice();

            string before = TooltipText.text;
            Assert.IsNotEmpty(before, "precondition: the selected card's box is up");

            var hover = Control("ReckoningOffer1").GetComponent<HoverIndex>();
            Assert.IsNotNull(hover, "ReckoningOffer1 carries no HoverIndex");
            hover.OnPointerEnter(null);
            yield return null;

            Assert.AreEqual(before, TooltipText.text,
                "focus outranks hover -- the box stays on the card the stick is standing on");
        }

        [UnityTest]
        public IEnumerator MovingTheSelectionOffAnOffer_MovesTheComparisonWithIt()
        {
            yield return ShowTheChoice();

            // The BOX'S POSITION, not its text: two items can produce the
            // same comparison lines (two tier-mates against an empty slot
            // read alike), whereas the box is placed beside whichever card
            // it belongs to, so where it sits is the claim worth asserting.
            var box = (RectTransform)Tooltip.transform;
            Vector2 before = box.anchoredPosition;

            yield return Press(1f, 0f);

            AssertSelected("ReckoningOffer1", "precondition: Right reached the second card");
            Assert.IsTrue(Tooltip.activeSelf, "the box belongs to the selection, so it follows it");
            Assert.AreNotEqual(before.x, box.anchoredPosition.x,
                "and it is placed beside the card it moved to, not left behind on the last one");
        }

        [UnityTest]
        public IEnumerator AContextPushedOverTheReckoning_ForceClosesTheTooltip()
        {
            yield return ShowTheChoice();

            Assert.IsTrue(Tooltip.activeSelf, "precondition: the box is up");

            // Pushed directly: nothing in the fight puts a context over the
            // Reckoning today (it is itself the thing that goes over the
            // fight), so this is the only honest way to prove the rule fires
            // rather than waiting for the first screen that needs it.
            var modal = new Domain.UiKit.NavContext(entry: null, selectables: null, cancel: null);
            NavigationInputModule.Contexts.Push(modal);
            yield return null;

            Assert.IsFalse(Tooltip.activeSelf,
                "a context going up over the screen force-closes its tooltip, off the stack's own Changed " +
                "event rather than a poll");

            NavigationInputModule.Contexts.Remove(modal);
        }
    }
}
