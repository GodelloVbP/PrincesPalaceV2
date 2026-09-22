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

        [UnityTest]
        public IEnumerator Right_AcrossTier1_StepsSiblingToSibling()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_1"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_2"), EventSystem.current.currentSelectedGameObject,
                "tier 1 is a 3-wide Rail ordered by DxSlot ascending -- Right from the left stone " +
                "(DxSlot -1) should reach the centre one (DxSlot 0)");
        }

        [UnityTest]
        public IEnumerator Right_WrapsAcrossTheTriple()
        {
            yield return LoadTheTree();
            EventSystem.current.SetSelectedGameObject(Node("Orb0_3"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("Orb0_1"), EventSystem.current.currentSelectedGameObject,
                "Rail is wrap by the owner default -- Right from the rightmost stone (DxSlot +1) " +
                "should wrap to the leftmost (DxSlot -1)");
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
                "the convention every convergence in this skeleton follows");
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

        [UnityTest]
        public IEnumerator Down_FromTheSelectedOrb_ReachesInvestButton_OverridingTheSkeletonLink()
        {
            yield return LoadTheTree();

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            _talents.Refresh();
            yield return null;

            // Root has no skeleton parent to fall back on -- an unselected
            // root's own Down link is unset entirely. Selecting it is what
            // gives Down a target at all (RefreshOrbNavigation's own override,
            // re-resolved on every Refresh -- here, from OnOrbPressed's Submit).
            //
            // This Submit ALSO kindles the root now (owner, 2026-09-19: see
            // Submit_OnAKindleableStar_KindlesItImmediately below) -- fine for
            // what this test actually checks, since InvestButton stays shown
            // (only NotAuthored hides it) whether or not the star underneath
            // it is already lit, and the Down override is unconditional on
            // `_selectedSlot >= 0` rather than on the button's interactable
            // state.
            EventSystem.current.SetSelectedGameObject(Node("Orb0_0"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            var investButton = Node("InvestButton");
            Assert.IsTrue(investButton.activeInHierarchy, "selecting an authored, reachable root should reveal InvestButton");

            yield return Move(0f, -1f);

            Assert.AreEqual(investButton, EventSystem.current.currentSelectedGameObject,
                "Down from the SELECTED root should reach InvestButton, overriding the skeleton's own " +
                "(nonexistent) parent link for exactly this one orb");
        }

        // OWNER HARDWARE PLAY-TEST, 2026-09-19: "It is incredibly unintuitive
        // to get into the right part where one can kindle with a controller
        // ... Make A on a star that is unkindled the way to kindle it."
        //
        // OnOrbPressed is Button.onClick, which a mouse click and a pad
        // Submit on the focused orb both already drive (Unity's own
        // OnPointerClick/OnSubmit each call Press()) -- so this is the one
        // press both devices share, not a pad-only shortcut. The root of
        // path 0 is always affordable and parentless from a fresh save
        // (TalentLifecycleTests' own comment), so one Submit on it is enough
        // to prove the whole thing: no Move onto InvestButton, no second
        // press.
        [UnityTest]
        public IEnumerator Submit_OnAKindleableStar_KindlesItImmediately_OnePressNotTwo()
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

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "one Submit on a reachable star should kindle it -- no second press on InvestButton required");
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
