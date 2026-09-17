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

        // The required action: Kindle the root of path 0, always affordable
        // from a fresh save (TalentLifecycleTests' own comment: "the root of
        // a path is free and parentless"), through Submit alone -- select the
        // orb, Submit to focus the detail panel, Move Down onto InvestButton,
        // Submit again to kindle.
        [UnityTest]
        public IEnumerator Submit_OnInvestButton_KindlesTheSelectedOrb_ExactlyOnce()
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

            yield return Move(0f, -1f);
            Assert.AreEqual(Node("InvestButton"), EventSystem.current.currentSelectedGameObject,
                "should be standing on InvestButton before the kindling Submit");

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, character.unlockedTalentIds.Count,
                "Submit on InvestButton should kindle the selected orb exactly once");
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
