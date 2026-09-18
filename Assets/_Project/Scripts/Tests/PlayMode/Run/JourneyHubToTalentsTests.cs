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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 6: the Hub's
    // own Talents building, unlike Shop a real Hub button
    // (HubController.WireNavigation's own four staged buildings), reached
    // between descents rather than through the Map.
    //
    // Hub's own grid puts the gate (StartRunGate) dead centre, equidistant
    // from both building columns (HubController.WireNavigation's own
    // comment) -- from the gate: Up reaches CharacterSheetBuilding
    // (explicit), then Left reaches PrincipalityBuilding (same Grid row,
    // column 0), then Up reaches TalentsBuilding (Grid row-stepping, same
    // column) -- three presses, none of them guessed: every one is a link
    // HubController.WireNavigation itself declares.
    //
    // DEVIATION FROM THE BRIEF'S OWN "invest on an eligible orb... and be
    // refused on an unauthored one": the specific slot a character's tree
    // has NO authored talent for (TalentPage.Refusal.NotAuthored) is a fact
    // about content, not about structure -- which slot that is, or whether
    // one exists at all for whichever character ends up standing here, is
    // not a stable thing for a test to pin. The refusal proven here instead
    // is PrerequisiteMissing: a tier-1 orb pressed before its path's root is
    // unlocked, which TalentPage.Evaluate's own ordering
    // (TalentGamepadNavigationTests already reads it) guarantees by
    // construction -- a real, "both eligibility shapes exist" refusal (plan
    // section 2's own verification note) that does not depend on which
    // character or which content happens to be authored today.
    //
    // TWO SEPARATE JOURNEYS, EACH RECONSTRUCTED, rather than one continuous
    // session chaining both outcomes: investing the root REMOVES the very
    // PrerequisiteMissing condition the refused case needs (a tier-1 orb's
    // prerequisite is the root), so proving both in one uninterrupted
    // pad session would mean refusing first and investing after -- which
    // still leaves the cursor standing on InvestButton with no dispatcher-
    // proven way back onto the tree (RefreshOrbNavigation's own override is
    // stated one-directional, Down only) short of a Cancel that would leave
    // the screen. Every existing single-screen gamepad-nav file in this
    // project already reconstructs its own precondition per test rather
    // than chaining unrelated outcomes through one selection walk; this
    // segment follows the same shape twice.
    public class JourneyHubToTalentsTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-talents-" + System.Guid.NewGuid().ToString("N"));
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

            AssertSelectedName("StartRunGate", "the gate is the hub's own stated primary action and entry");

            yield return MoveUp(); // gate -> PrincipalityBuilding, the left arm above it
            AssertSelectedName("PrincipalityBuilding", "Up from the gate should reach PrincipalityBuilding");

            yield return MoveUp(); // further up the same arm
            AssertSelectedName("TalentsBuilding", "Up again should reach TalentsBuilding");

            yield return PressSubmit(); // Navigation.Go(Talents), a REAL scene load
            yield return WaitForScene("Talents", 5f, "Submit on TalentsBuilding should load the Talents scene");
            yield return null;
            yield return null;
            TakeOverInput();

            AssertSelectedName("Orb0_0", "the entry is the first orb -- path 0's root, slot 0");
        }

        private IEnumerator CancelBackToTheHub()
        {
            yield return PressCancel();
            yield return WaitForScene("Hub", 5f,
                "Cancel from the Talents tree should take the same path TalentBackButton does, back to the Hub");
            yield return null;
            yield return null;
            TakeOverInput();

            AssertSelectedName("StartRunGate", "back on the hub, the gate is its own stated primary action and entry");
        }

        [UnityTest]
        public IEnumerator ATier1Orb_BeforeItsRootIsUnlocked_IsRefused_CancelReturnsToTheHub()
        {
            yield return ReachTalentsFromTheHub();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();

            var talents = Object.FindAnyObjectByType<TalentController>(FindObjectsInactive.Include);
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");
            talents.Refresh();
            yield return null;

            yield return MoveUp(); // root -> the centre tier-1 child (the convergence convention)
            AssertSelectedName("Orb0_2", "Up from the root should reach the centre tier-1 stone");

            yield return PressSubmit(); // OnOrbPressed -> selects this orb, reveals the detail panel
            yield return MoveDown(); // Down from the SELECTED orb reaches InvestButton
            AssertSelectedName("InvestButton", "should be standing on InvestButton before the refused press");

            yield return PressSubmit(); // Kindle() -> TalentOps.Kindle refuses: PrerequisiteMissing

            Assert.AreEqual(0, character.unlockedTalentIds.Count,
                "a tier-1 orb pressed before its root is unlocked should not have kindled anything");
            Assert.IsNotEmpty(TalentRefusalText(),
                "the refusal should be painted, not a silent dead press on an unreachable-looking orb");

            yield return CancelBackToTheHub();
        }

        [UnityTest]
        public IEnumerator TheRootOrb_AlwaysEligible_KindlesExactlyOnce_CancelReturnsToTheHub()
        {
            yield return ReachTalentsFromTheHub();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();

            var talents = Object.FindAnyObjectByType<TalentController>(FindObjectsInactive.Include);
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");
            talents.Refresh();
            yield return null;

            yield return PressSubmit(); // OnOrbPressed on the root, still standing on the entry
            yield return MoveDown(); // Down from the SELECTED root reaches InvestButton, overriding the
                                      // skeleton's own (nonexistent) parent link for exactly this one orb
            AssertSelectedName("InvestButton", "should be standing on InvestButton before the eligible press");

            yield return PressSubmit(); // Kindle() -> TalentOps.Kindle succeeds: the root is free and parentless

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "Submit on InvestButton should kindle the root exactly once");

            yield return CancelBackToTheHub();
        }

        // HARDWARE PLAY-TEST ROUND 1, ITEM 1: "I found no way to move in the
        // talent screen." Driven on the REAL Hub -> Talents building -> Submit
        // path (the hand-built TalentGamepadNavigationTests fixture passed
        // throughout, and still does -- the gap was never the fixture, it was
        // that three of the entry orb's four directions had no link at all and
        // the screen's own chrome was never declared: measured on this exact
        // path before the fix, Orb0_0 read up=Orb0_2, down=null, left=null,
        // right=null, and TalentBackButton was still on the scene-build
        // default Navigation.Mode.Automatic).
        //
        // Literal node names, one ring, every direction off the entry.
        [UnityTest]
        public IEnumerator EveryDirectionOffTheEntryOrbGoesSomewhere_AndTheChromeIsReachable()
        {
            yield return ReachTalentsFromTheHub();

            // Up/Down: the tree, unchanged -- the one axis that already worked.
            yield return MoveUp();
            AssertSelectedName("Orb0_2", "Up from the root should still reach the centre tier-1 stone");
            yield return MoveDown();
            AssertSelectedName("Orb0_0", "Down should follow the skeleton back to the root");

            // Selecting the root is what puts a live control in the panel at
            // all: investButton is SetShown(false) until an orb is selected,
            // and respecButton until something has been earned, so a pad
            // arriving on this screen has an empty column to its right until
            // it presses something. That is the screen's own design, not a
            // nav gap -- Refresh's own PaintInvestButton decides it.
            yield return PressSubmit();
            Assert.IsTrue(Node("InvestButton").activeInHierarchy,
                "selecting the root should reveal InvestButton");

            // Right: off the sky's centre column onto the arrow flanking it,
            // then on into the panel column down the right of the screen.
            yield return MoveRight();
            AssertSelectedName("NextPathButton",
                "Right from a one-wide tier orb should reach the arrow drawn at the sky's right edge");

            yield return MoveRight();
            AssertSelectedName("InvestButton",
                "Right from the right-hand arrow should reach the panel's own action, level with it");

            yield return MoveLeft();
            AssertSelectedName("NextPathButton", "Left from the panel action should come back to the arrow");

            yield return MoveUp();
            AssertSelectedName("NextCharacterButton",
                "up-right of the arrow is the panel's head, where the character pager is drawn");

            yield return MoveLeft();
            AssertSelectedName("PrevCharacterButton", "the character pager is a clamped Rail of two");

            yield return MoveLeft();
            AssertSelectedName("NextPathButton", "Left off the pager's left end should come back to the arrow");

            yield return MoveLeft();
            AssertSelectedName("Orb0_0",
                "Left from the right-hand arrow returns to the root, this screen's own declared entry");

            // Left: the other arrow, and Back in the corner above it.
            yield return MoveLeft();
            AssertSelectedName("PrevPathButton",
                "Left from a one-wide tier orb should reach the arrow drawn at the sky's left edge");

            yield return MoveUp();
            AssertSelectedName("TalentBackButton",
                "Back sits in the top-left corner above the left arrow, and was outside the graph entirely before this");

            yield return MoveDown();
            AssertSelectedName("PrevPathButton", "Down from Back should come back to the left arrow");

            yield return MoveRight();
            AssertSelectedName("Orb0_0", "Right from the left-hand arrow returns to the root");
        }
    }
}
