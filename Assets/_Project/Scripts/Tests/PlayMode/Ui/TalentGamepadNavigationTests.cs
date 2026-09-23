using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md, screen 4: Talent's orbs,
    // wired as a TREE, not a Grid -- TalentController.RefreshOrbNavigation's
    // own header explains why a fixed-width Grid cannot express a skeleton
    // whose rows are 1 wide (root, the convergence, the capstone) and 3 wide
    // (everything else). Pinned against path 0's own skeleton, GameObject
    // names "Orb{path}_{slot}" (TalentScreen.cs:602), literal throughout --
    // never recomputed from TalentSkeleton in the assertions themselves.
    public class TalentGamepadNavigationTests
    {
        private string _root;
        private ScriptedBaseInput _input;
        private TalentController _talents;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-talent-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator LoadTheTree()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the Talents scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            yield return null;
            yield return null;

            _talents = Object.FindAnyObjectByType<TalentController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_talents, "the Talents scene has no TalentController");
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Move(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
        }

        private static GameObject Node(string name) => GameObject.Find(name);

        [UnityTest]
        public IEnumerator EntryIsTheRootOfPath0()
        {
            yield return LoadTheTree();

            Assert.AreEqual(Node("Orb0_0"), EventSystem.current.currentSelectedGameObject,
                "the entry is the first orb -- path 0's root, slot 0");
        }

        // OWNER, 2026-09-23, FINAL CORRECTION (supersedes this file's own
        // earlier "Left/Right name a level's two fixed ends, absolutely"
        // reading, itself already past the Rail-wrap "Right from Shatter
        // goes to The Flock" fix and the "continue up the strand"
        // replacement): Left/Right step ONE LANE AT A TIME within a 3-wide
        // level -- middle to a side and back -- never a jump straight to
        // the far side. Right from the middle stone reaches the level's
        // own right-hand stone...
        [UnityTest]
        public IEnumerator Right_FromTheCentreStone_ReachesTheLevelsRightStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_2"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_3"), EventSystem.current.currentSelectedGameObject,
                "Right from the middle stone should reach the level's own right-hand stone");
        }

        // ...and Left from the middle stone reaches the left-hand one.
        [UnityTest]
        public IEnumerator Left_FromTheCentreStone_ReachesTheLevelsLeftStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_2"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("Orb0_1"), EventSystem.current.currentSelectedGameObject,
                "Left from the middle stone should reach the level's own left-hand stone");
        }

        // FROM A SIDE STONE, the direction back TOWARD the middle reaches
        // it -- Right from the left-hand stone, Left from the right-hand
        // one -- never a jump past it to the far side.
        [UnityTest]
        public IEnumerator Right_FromTheLeftStone_ReachesTheLevelsMiddleStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_1"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_2"), EventSystem.current.currentSelectedGameObject,
                "Right from the left-hand stone should step to the level's own middle stone, not " +
                "jump past it to the right-hand one");
        }

        [UnityTest]
        public IEnumerator Left_FromTheRightStone_ReachesTheLevelsMiddleStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_3"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("Orb0_2"), EventSystem.current.currentSelectedGameObject,
                "Left from the right-hand stone should step to the level's own middle stone, not " +
                "jump past it to the left-hand one");
        }

        // ONLY A SECOND PRESS PAST AN OUTER LANE reaches the page arrow --
        // Right from the level's own right-hand stone (already the far
        // edge in that direction, nothing further that way in the tree),
        // and the mirror on the left. No wrap, no "continue up the
        // strand": both retired well before this correction.
        [UnityTest]
        public IEnumerator Right_AtTheRightStone_ReachesTheNextPageArrow()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_3"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("NextPathButton"), EventSystem.current.currentSelectedGameObject,
                "Right from the level's own right-hand stone (already the outer lane in that " +
                "direction) should reach the next-page arrow, not wrap and not continue up the strand");
        }

        [UnityTest]
        public IEnumerator Left_AtTheLeftStone_ReachesThePrevPageArrow()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_1"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("PrevPathButton"), EventSystem.current.currentSelectedGameObject,
                "Left from the level's own left-hand stone (already the outer lane in that direction) " +
                "should reach the prev-page arrow");
        }

        // THE SHATTER/SPLINTERING/FLOCK CASE, checked directly against the
        // restated model rather than assumed: Shatter (slot 13) is the
        // RIGHT-hand stone of the level right after the convergence, so
        // Right from it now reaches the arrow (proven above via slot 3, the
        // same shape one level earlier) -- Splintering (slot 16, the
        // right-hand stone of the NEXT level, directly above Shatter in the
        // same DxSlot +1 lane) is reached by UP alone, in one press:
        // _skeletonUpChild's lane-preserving pick (owner's later
        // correction, 2026-09-23: "Up/Down stay in the lane you're in")
        // sends a chain slot with exactly one child straight to it, and
        // Parents[16] == [13] makes 16 that one child -- there is no
        // "recentre through the level's middle stone" step for a plain
        // chain slot, only for a single-stone level's own Up/Down (Up_
        // FromTheRoot_ReachesTheCentreChild is that different case). Pinned
        // on slot numbers, not this path's authored names, per this file's
        // own convention -- The Flock (slot 11) is the mirroring LEFT-hand
        // stone of Shatter's own level.
        [UnityTest]
        public IEnumerator Right_FromShatter_ReachesTheArrow_NotSplinteringDirectly()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_13"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("NextPathButton"), EventSystem.current.currentSelectedGameObject,
                "Shatter (slot 13) is already its level's own right-hand stone, so Right reaches the " +
                "arrow -- Splintering is reached by Up instead, not by Right");
        }

        [UnityTest]
        public IEnumerator Up_FromShatter_ReachesSplinteringDirectly()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_13"));
            yield return null;

            yield return Move(0f, 1f);

            Assert.AreEqual(Node("Orb0_16"), EventSystem.current.currentSelectedGameObject,
                "Up from Shatter (slot 13, DxSlot +1) should land on Splintering (slot 16), the same " +
                "DxSlot +1 stone one level up -- lane-preserving, in one press");
        }

        // OWNER, 2026-09-23: "Levels with a single stone (root, convergence,
        // capstone): ... Left/Right go to the flanking stones of the
        // adjacent level." The convergence (slot 10) borrows its child
        // level's (the one right above it) own left/right pair -- the
        // arrows are then reached by pressing again from THAT side stone
        // (Right_FromTheLeftStone_ReachesTheLevelsRightStone and its
        // mirror above already prove the second-press rule generically).
        [UnityTest]
        public IEnumerator Left_FromTheConvergence_ReachesTheLeftChild()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_10"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("Orb0_11"), EventSystem.current.currentSelectedGameObject,
                "Left from the convergence should reach its child level's own left-hand stone");
        }

        [UnityTest]
        public IEnumerator Right_FromTheConvergence_ReachesTheRightChild()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_10"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_13"), EventSystem.current.currentSelectedGameObject,
                "Right from the convergence should reach its child level's own right-hand stone");
        }

        // The root (slot 0) is the SAME shape as the convergence -- a
        // single stone feeding a triple -- so it behaves the same way now
        // (owner, 2026-09-23: "Make the root behave the same way"), rather
        // than the old direct-to-arrow link JourneyHubToTalentsTests used
        // to pin (that route is retired; see that file's own update for
        // Invest's new one).
        [UnityTest]
        public IEnumerator Left_FromTheRoot_ReachesTheLeftChild()
        {
            yield return LoadTheTree();

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("Orb0_1"), EventSystem.current.currentSelectedGameObject,
                "Left from the root should reach tier 1's own left-hand stone, the same shape the " +
                "convergence uses -- not the page arrow directly");
        }

        [UnityTest]
        public IEnumerator Right_FromTheRoot_ReachesTheRightChild()
        {
            yield return LoadTheTree();

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_3"), EventSystem.current.currentSelectedGameObject,
                "Right from the root should reach tier 1's own right-hand stone");
        }

        // The capstone (slot 20) has nothing ABOVE it, so it borrows from
        // the level BELOW instead -- the one adjacent 3-wide level it has
        // (owner, 2026-09-23: "the capstone too if it has a level below
        // with sides"). Reaching the arrow from here is Left/Right, THEN
        // Left/Right again from that side stone, the same second-press
        // rule as everywhere else.
        [UnityTest]
        public IEnumerator Left_FromTheCapstone_ReachesItsParentLevelsLeftStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_20"));
            yield return null;

            yield return Move(-1f, 0f);

            Assert.AreEqual(Node("Orb0_17"), EventSystem.current.currentSelectedGameObject,
                "Left from the capstone should reach the level below it's own left-hand stone, since " +
                "the capstone has no level above to borrow from");
        }

        [UnityTest]
        public IEnumerator Right_FromTheCapstone_ReachesItsParentLevelsRightStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_20"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_19"), EventSystem.current.currentSelectedGameObject,
                "Right from the capstone should reach the level below it's own right-hand stone");
        }

        [UnityTest]
        public IEnumerator Up_FromTheRoot_ReachesTheCentreChild()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_0"));
            yield return null;

            yield return Move(0f, 1f);

            Assert.AreEqual(Node("Orb0_2"), EventSystem.current.currentSelectedGameObject,
                "the root feeds all three tier-1 stones -- Up should pick the centre column (DxSlot 0), " +
                "the convention leaving any single-stone level uses");
        }

        [UnityTest]
        public IEnumerator Down_FromATier1Stone_ReturnsToTheRoot()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_2"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("Orb0_0"), EventSystem.current.currentSelectedGameObject,
                "Down should follow the skeleton's own Parents back to the root");
        }

        // OWNER, 2026-09-23: "Up/Down stay in the lane (column) you're in
        // ... the same holds for left and middle." A SIDE stone's Up/Down
        // must land on the SAME DxSlot one level over, not the centre --
        // the earlier (now-retired) "recentre on every Up/Down" reading of
        // this rule would have sent this to Orb0_5 instead.
        [UnityTest]
        public IEnumerator Up_FromASideStone_StaysInLane()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_1"));
            yield return null;

            yield return Move(0f, 1f);

            Assert.AreEqual(Node("Orb0_4"), EventSystem.current.currentSelectedGameObject,
                "Up from a DxSlot -1 stone should land on the DxSlot -1 stone of the level above, not " +
                "recentre onto the level's own middle stone");
        }

        [UnityTest]
        public IEnumerator Down_FromASideStone_StaysInLane()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_4"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("Orb0_1"), EventSystem.current.currentSelectedGameObject,
                "Down from a DxSlot -1 stone should land on the DxSlot -1 stone of the level below");
        }

        // Leaving a single-stone level, the stone Up/Down lands on is this
        // method's own choice (owner, 2026-09-23: "the stone you land on
        // is up to you, but state it") -- the CENTRE one, the same
        // convention Up_FromTheRoot_ReachesTheCentreChild already pins for
        // the root. Proven from BOTH side lanes of the level right after
        // the convergence, in separate tests: both converge on the same
        // stone.
        [UnityTest]
        public IEnumerator Down_FromTheLeftLane_LandsOnTheConvergencesSingleStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_11"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("Orb0_10"), EventSystem.current.currentSelectedGameObject,
                "Down from the DxSlot -1 stone in the level right after the convergence should land " +
                "on the convergence itself -- the only stone that level has");
        }

        [UnityTest]
        public IEnumerator Down_FromTheRightLane_LandsOnTheConvergencesSingleStone()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_13"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("Orb0_10"), EventSystem.current.currentSelectedGameObject,
                "Down from the DxSlot +1 stone in the level right after the convergence should also " +
                "land on the convergence itself");
        }

        // OWNER, 2026-09-23: "Give InvestButton another reachable route
        // (Down from the root ...)" -- the old route (root -> arrow ->
        // Invest) is retired now that the root's own Left/Right reach tier
        // 1's stones instead. The root has nothing below it in the tree
        // (Parents.Length == 0), so its Down was always unclaimed; it is
        // spent here UNCONDITIONALLY, not only once the root itself is
        // selected -- proven by selecting a DIFFERENT orb (tier 1, still
        // refused) to reveal InvestButton, then reaching it from the root
        // without ever submitting on the root at all.
        [UnityTest]
        public IEnumerator Down_FromTheRoot_ReachesInvestButton_Unconditionally()
        {
            yield return LoadTheTree();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            _talents.Refresh();
            yield return null;

            // InvestButton stays shown once ANY orb is selected (only
            // NotAuthored hides it), whether or not that orb is itself
            // reachable -- selecting tier 1's refused stone here, never the
            // root, is what proves the root's own Down link is
            // unconditional rather than tied to the root being selected.
            EventSystem.current.SetSelectedGameObject(Node("Orb0_2"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            var investButton = Node("InvestButton");
            Assert.IsTrue(investButton.activeInHierarchy, "selecting a refused orb should still reveal InvestButton");

            EventSystem.current.SetSelectedGameObject(Node("Orb0_0"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(investButton, EventSystem.current.currentSelectedGameObject,
                "Down from the root should reach InvestButton unconditionally -- the root was never " +
                "itself selected here, only a different orb was, to prove the link does not depend on it");
        }

        // OWNER, 2026-09-23 (revising the 2026-09-19 hardware play-test call
        // below): a single Submit on an unkindled star was found to fire too
        // eagerly on hardware -- the new contract is that the FIRST Submit
        // on an unkindled star only SELECTS it (shows its detail, same as
        // Submit on a refused star already did), and a SECOND Submit on that
        // same selected star -- or a Submit on InvestButton, unchanged --
        // is what actually kindles it. The root of path 0 is always
        // affordable and parentless from a fresh save (TalentLifecycleTests'
        // own comment), so it is enough to prove both halves without ever
        // moving onto InvestButton.
        [UnityTest]
        public IEnumerator Submit_OnAKindleableStar_SelectsThenKindles_TwoPressesNotOne()
        {
            yield return LoadTheTree();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            _talents.Refresh();
            yield return null;

            EventSystem.current.SetSelectedGameObject(Node("Orb0_0"));
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(0, character.unlockedTalentIds.Count,
                "the first Submit on an unkindled star should only select it, not kindle it");
            Assert.AreEqual(Node("Orb0_0"), EventSystem.current.currentSelectedGameObject,
                "the first Submit should select the star so its detail panel shows");

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "a second Submit on the already-selected star should kindle it");
            Assert.AreEqual(Node("Orb0_0"), EventSystem.current.currentSelectedGameObject,
                "the star stays selected so the detail panel updates to show it kindled");
        }

        // The other half of the same brief: a star Evaluate refuses (here,
        // tier 1 with the root not yet kindled -- PrerequisiteMissing) must
        // only select and explain, never kindle, however many times Submit
        // lands on it.
        [UnityTest]
        public IEnumerator Submit_OnALockedStar_SelectsItAndExplainsWhy_NeverKindles()
        {
            yield return LoadTheTree();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            _talents.Refresh();
            yield return null;

            EventSystem.current.SetSelectedGameObject(Node("Orb0_1"));
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(0, character.unlockedTalentIds.Count,
                "a star Evaluate refuses must never be kindled by Submit");
            Assert.AreEqual(Node("Orb0_1"), EventSystem.current.currentSelectedGameObject,
                "Submit on a refused star still selects it, so the panel can say why");
        }

        // OWNER HARDWARE PLAY-TEST, 2026-09-19: "RB LB scrolls you between
        // different talent trees" -- since reassigned, same hardware round,
        // to the TRIGGERS (LT/RT are project-wide trigger shortcuts,
        // ProjectSettings/InputManager.asset's TriggerLeft/TriggerRight,
        // read by NavigationInputModule.Process, offered to whichever
        // context is top via NavContext.RaiseTabStep/INavTabStrip), freeing
        // RB/LB for section/character paging below -- TalentController
        // implements the interface with StepTab as StepPath verbatim, so
        // the trigger and the on-screen paging arrows can never disagree
        // about where a page lands.
        [UnityTest]
        public IEnumerator TabNext_StepsToTheNextConstellation_AndEntersItsRoot()
        {
            yield return LoadTheTree();

            _input.TriggerRight = 1f;
            yield return DriveFrame();
            _input.TriggerRight = 0f;

            Assert.AreEqual(Node("Orb1_0"), EventSystem.current.currentSelectedGameObject,
                "RT should page to constellation 1 (StepPath(1)) and reselect that path's own root -- " +
                "the same out-of-declared-set reselection every context gets when RefreshOrbNavigation " +
                "rebuilds the selectable set around the new path");
        }

        [UnityTest]
        public IEnumerator TabPrev_AtTheFirstConstellation_ClampsRatherThanWrapping()
        {
            yield return LoadTheTree();

            _input.TriggerLeft = 1f;
            yield return DriveFrame();
            _input.TriggerLeft = 0f;

            Assert.AreEqual(Node("Orb0_0"), EventSystem.current.currentSelectedGameObject,
                "paging is a clamped line (ConstellationLayout.Step), not a loop -- LT at the first " +
                "constellation should leave the selection exactly where it was (StepPath's own early-out " +
                "when Step(...) returns the same index)");
        }

        // OWNER HARDWARE PLAY-TEST, 2026-09-19: "To go to the next character
        // or prior you can press LT or RT." StepSection is StepCharacter,
        // verbatim. INavSectionStrip landed after this file's other tests
        // were written (input-seam agent's own contract,
        // Domain/UiKit/INavCancelClaim.cs) -- driven here the same way the
        // shoulder tests above drive TabNext/TabPrev, through the trigger
        // AXES ScriptedBaseInput exposes as levels rather than edges
        // (NavigationInputModule.TriggerPressed owns the press edge).
        //
        // A character switch repaints the SAME orb GameObjects for a
        // different character's tree rather than swapping which nodes exist,
        // so node identity cannot prove which character is showing the way
        // it proves which constellation is; the ember count PaintMeter
        // writes (TalentEmberCount, "{embers} EMBERS") is the one thing on
        // screen that is a stated function of Current, so it stands in.
        [UnityTest]
        public IEnumerator TriggerRight_StepsToTheNextCharacter()
        {
            yield return LoadTheTree();

            var roster = SaveSlotManager.CurrentSave.roster;
            Assert.Greater(roster.Count, 1,
                "fixture: a fresh save's roster carries one Character per authored definition");

            roster[0].embers = 5;
            roster[1].embers = 40;
            _talents.Refresh();
            yield return null;

            var emberText = Node("TalentEmberCount").GetComponent<TMPro.TMP_Text>();
            Assert.AreEqual("5 EMBERS", emberText.text,
                "fixture: character 0 (5 embers) should be showing before the shoulder");

            _input.TabNextDown = true;
            yield return DriveFrame();

            Assert.AreEqual("40 EMBERS", emberText.text,
                "RB (StepSection(1) -> StepCharacter(1)) should page to character 1 and repaint its own embers");
        }

        [UnityTest]
        public IEnumerator TriggerLeft_AtTheFirstCharacter_ClampsRatherThanWrapping()
        {
            yield return LoadTheTree();

            var roster = SaveSlotManager.CurrentSave.roster;
            Assert.Greater(roster.Count, 1,
                "fixture: a fresh save's roster carries one Character per authored definition");

            roster[0].embers = 5;
            _talents.Refresh();
            yield return null;

            var emberText = Node("TalentEmberCount").GetComponent<TMPro.TMP_Text>();

            _input.TabPrevDown = true;
            yield return DriveFrame();

            Assert.AreEqual("5 EMBERS", emberText.text,
                "paging is a clamped line, not a loop -- LB at character 0 should leave the selection " +
                "exactly where it was (StepCharacter's own Step(...) clamp)");
        }

        [UnityTest]
        public IEnumerator DownFromTheSelectedCapstoneReturnsToItsTreeParent()
        {
            yield return LoadTheTree();

            var capstone = Node("Orb0_20");
            Assert.IsNotNull(capstone, "fixture: path zero needs its capstone orb");
            EventSystem.current.SetSelectedGameObject(capstone);
            capstone.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "Down from the capstone should stay in the tree");
            StringAssert.StartsWith("Orb0_", selected.name,
                "Down from the capstone must follow the constellation toward its parent, not jump to Invest");
            Assert.AreNotEqual("Orb0_20", selected.name, "Down should actually leave the capstone");
        }

        [UnityTest]
        public IEnumerator CancelReturnsToTheHub()
        {
            yield return LoadTheTree();

            var navigated = new System.Collections.Generic.List<string>();
            Navigation.LoadOverride = scene => navigated.Add(scene);

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, navigated.Count, "Cancel should navigate exactly once");
            Assert.AreEqual(Navigation.Hub, navigated[0], "Cancel should take the same path TalentBackButton does");
        }
    }
}
