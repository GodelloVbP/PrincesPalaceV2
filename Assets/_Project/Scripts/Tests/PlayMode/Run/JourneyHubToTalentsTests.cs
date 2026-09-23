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

            yield return MoveUp(); // root -> the centre tier-1 child (the lane convention leaving the root)
            AssertSelectedName("Orb0_2", "Up from the root should reach the centre tier-1 stone");

            yield return PressSubmit(); // OnOrbPressed -> selects this orb (refused, so no auto-kindle), reveals the detail panel

            // Down from a tier-1 orb follows the constellation's own lane
            // back to the root (unchanged -- Up/Down stay in lane, the
            // owner's restated model, 2026-09-23). This is a focus move
            // only (no click), so _selectedSlot stays on the tier-1 orb.
            yield return MoveDown();
            AssertSelectedName("Orb0_0", "Down from a selected tier-1 orb should follow the tree to its root");

            // INVEST'S NEW ROUTE (owner, 2026-09-23: root -> arrow -> Invest
            // is retired now that the root's own Right reaches tier 1's
            // right-hand stone like every other single-stone level; "give
            // InvestButton another reachable route (Down from the root...)"
            // is what RefreshOrbNavigation now wires, unconditionally, since
            // the root has nothing below it in the tree to begin with).
            // Selecting the tier-1 orb above already made InvestButton
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

        // HARDWARE PLAY-TEST ROUND 1, ITEM 1: "I found no way to move in the
        // talent screen." Driven on the REAL Hub -> Talents building -> Submit
        // path. REWRITTEN 2026-09-23 for the owner's restated model: the
        // root now behaves exactly like the convergence (Left/Right reach
        // tier 1's own flanking stones, a second press from THAT stone
        // reaches the arrow), and Invest is reached by the root's own Down
        // rather than through the right-hand arrow -- TalentGamepadNavigation
        // Tests carries the unit-level proof for each of these individually
        // (Left/Right_FromTheRoot_ReachesThe*Child,
        // Right/Left_At*Stone_ReachesThe*Arrow,
        // Down_FromTheRoot_ReachesInvestButton_Unconditionally); this test's
        // own job is only proving the same graph holds on the REAL built
        // scene, not re-deriving it.
        //
        // Literal node names, one ring, every direction off the entry.
        [UnityTest]
        public IEnumerator EveryDirectionOffTheEntryOrbGoesSomewhere_AndTheChromeIsReachable()
        {
            yield return ReachTalentsFromTheHub();

            // Up/Down: the tree, unchanged -- lane-preserving, the one axis
            // the owner's restated model left untouched.
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

            // INVEST, THE ROOT'S OWN NEW ROUTE: Down from the root reaches
            // it directly and unconditionally now (root -> arrow -> Invest
            // is retired), and Up from Invest returns to the root -- the
            // same round trip the old route made, one hop shorter.
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
