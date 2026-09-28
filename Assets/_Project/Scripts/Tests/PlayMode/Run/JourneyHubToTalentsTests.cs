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
    // own Talents building, a real Hub button reached between descents
    // rather than through the Map.
    //
    // From the gate (StartRunGate, dead centre between both building
    // columns): Up reaches CharacterSheetBuilding, Left reaches
    // PrincipalityBuilding (same Grid row, column 0), Up reaches
    // TalentsBuilding (Grid row-stepping, same column) -- every link
    // HubController.WireNavigation itself declares.
    //
    // The refusal proven here is PrerequisiteMissing (a tier-1 orb pressed
    // before its path's root is unlocked), not NotAuthored: which slot has
    // no authored talent is a fact about content, not structure, so it is
    // not stable to pin.
    //
    // Two separate journeys, each reconstructed, rather than one continuous
    // session: investing the root removes the PrerequisiteMissing condition
    // the refused case needs, and proving both in one pad session would
    // leave the cursor on InvestButton with no dispatcher-proven way back
    // onto the tree short of a Cancel that leaves the screen.
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

            yield return MoveUp(); // root -> the centre tier-1 child (the lane convention leaving the root)
            AssertSelectedName("Orb0_2", "Up from the root should reach the centre tier-1 stone");

            yield return PressSubmit(); // OnOrbPressed -> selects this orb (refused, so no auto-kindle), reveals the detail panel

            // Down from a tier-1 orb follows the constellation's own lane
            // back to the root -- Up/Down stay in lane. This is a focus
            // move only (no click), so _selectedSlot stays on the tier-1
            // orb.
            yield return MoveDown();
            AssertSelectedName("Orb0_0", "Down from a selected tier-1 orb should follow the tree to its root");

            // The root's own Right reaches tier 1's right-hand stone like
            // every other single-stone level, so InvestButton's route is
            // Down from the root, which RefreshOrbNavigation wires
            // unconditionally since the root has nothing below it in the
            // tree to begin with. Selecting the tier-1 orb above already
            // made InvestButton
            // active (TalentGamepadNavigationTests.Down_FromTheRoot_
            // ReachesInvestButton_Unconditionally proves the link itself
            // does not depend on which orb was selected), so one press
            // reaches it directly.
            yield return MoveDown(); // root -> InvestButton
            AssertSelectedName("InvestButton", "should be standing on InvestButton before the refused press");

            yield return PressSubmit(); // Kindle() -> TalentOps.Kindle refuses: PrerequisiteMissing, still against the tier-1 orb

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

        // Driven on the REAL Hub -> Talents building -> Submit path. The
        // root behaves like the convergence (Left/Right reach tier 1's
        // flanking stones, a second press from that stone reaches the
        // arrow), and Invest is reached by the root's Down rather than
        // through the right-hand arrow -- TalentGamepadNavigationTests
        // carries the unit-level proof for each of these individually
        // (Left/Right_FromTheRoot_ReachesThe*Child,
        // Right/Left_At*Stone_ReachesThe*Arrow,
        // Down_FromTheRoot_ReachesInvestButton_Unconditionally); this
        // test's job is only proving the same graph holds on the REAL
        // built scene, not re-deriving it.
        //
        // Literal node names, one ring, every direction off the entry.
        [UnityTest]
        public IEnumerator EveryDirectionOffTheEntryOrbGoesSomewhere_AndTheChromeIsReachable()
        {
            yield return ReachTalentsFromTheHub();

            // Up/Down: the tree, lane-preserving.
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

            // Down from the root reaches InvestButton directly and
            // unconditionally, and Up from Invest returns to the root.
            yield return MoveDown();
            AssertSelectedName("InvestButton", "Down from the root should reach InvestButton directly");

            yield return MoveUp();
            AssertSelectedName("Orb0_0", "Up from InvestButton should return to the root");

            // RIGHT: the root behaves like the convergence now -- Right
            // reaches tier 1's own right-hand stone, a real talent, not the
            // arrow. A second Right from THAT stone (already the level's
            // fixed target) is what reaches the arrow.
            yield return MoveRight();
            AssertSelectedName("Orb0_3",
                "Right from the root should reach tier 1's own right-hand stone, the same shape the " +
                "convergence uses");

            yield return MoveRight();
            AssertSelectedName("NextPathButton",
                "Right again, already standing on the level's own right-hand stone, should reach the " +
                "arrow drawn at the sky's right edge");

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

            // LEFT: the mirror -- tier 1's own left-hand stone, then the
            // left arrow on the second press, then Back above it.
            yield return MoveLeft();
            AssertSelectedName("Orb0_1",
                "Left from the root should reach tier 1's own left-hand stone");

            yield return MoveLeft();
            AssertSelectedName("PrevPathButton",
                "Left again, already standing on the level's own left-hand stone, should reach the " +
                "arrow drawn at the sky's left edge");

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
