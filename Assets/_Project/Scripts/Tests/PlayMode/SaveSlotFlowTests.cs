using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Save slots, driven through the real scene.
    //
    // Every one of these writes files, so they run against a throwaway root
    // rather than the player's actual saves -- SaveSystem.RootOverride exists
    // for exactly this. A test suite that can delete someone's progress is not
    // a test suite anyone will run.
    public class SaveSlotFlowTests
    {
        private string _root;

        // Where a click WOULD have gone. Recorded rather than followed: picking
        // a slot now enters the hub, and actually loading it mid-test destroys
        // the very objects every assertion below is about.
        private readonly List<string> _navigated = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _navigated.Clear();
            Navigation.LoadOverride = scene => _navigated.Add(scene);

            _root = Path.Combine(Path.GetTempPath(), "pp-slot-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            Navigation.Reset();

            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static GameObject Named(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        private static string SlotButtonText(int slot) =>
            Named($"Slot{slot}Button").GetComponentInChildren<TMP_Text>(true).text;

        private static string ResetRowText(int slot) =>
            Named($"ResetSlot{slot}Label").GetComponent<TMP_Text>().text;

        [UnityTest]
        public IEnumerator AnUntouchedSlotReadsAsEmpty()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            Assert.AreEqual("Slot 1: Empty", SlotButtonText(0));
        }

        [UnityTest]
        public IEnumerator ChoosingASlot_CreatesItAndSetsTheCurrentSlot()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            Assert.IsFalse(SaveSystem.SlotExists(2));
            Click("Slot2Button");

            Assert.IsTrue(SaveSystem.SlotExists(2), "picking an empty slot should write it immediately");
            Assert.AreEqual(2, SaveSlotManager.CurrentSlot);
            StringAssert.Contains("gold", SlotButtonText(2), "the label should stop saying Empty");
        }

        [UnityTest]
        public IEnumerator BothScreensDescribeTheSameSlotIdentically()
        {
            // v1's actual bug: the Play screen and the Options screen formatted
            // this independently and disagreed, and one of them printed an `exp`
            // field nothing ever wrote. One manifest entry serves both now.
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
            Click("Slot1Button");
            Click("OptionsButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            Assert.AreEqual(SlotButtonText(1), ResetRowText(1));
        }

        [UnityTest]
        public IEnumerator DeleteAsksFirst_AndDoesNotDeleteUntilConfirmed()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
            Click("Slot0Button");
            Click("OptionsButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            var confirm = Named("ResetConfirmPanel");
            Assert.IsFalse(confirm.activeSelf, "the confirmation should start closed");

            Click("ResetSlot0DeleteButton");

            Assert.IsTrue(confirm.activeSelf, "Delete should open the confirmation, not delete");
            Assert.IsTrue(SaveSystem.SlotExists(0), "the slot must survive until the deletion is confirmed");
        }

        [UnityTest]
        public IEnumerator ConfirmingActuallyDeletes_AndCancellingDoesNot()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
            Click("Slot0Button");
            Click("Slot1Button");
            Click("OptionsButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            Click("ResetSlot0DeleteButton");
            Click("ResetConfirmNoButton");
            Assert.IsTrue(SaveSystem.SlotExists(0), "Cancel must leave the slot intact");
            Assert.IsFalse(Named("ResetConfirmPanel").activeSelf, "Cancel should close the confirmation");

            Click("ResetSlot1DeleteButton");
            Click("ResetConfirmYesButton");
            Assert.IsFalse(SaveSystem.SlotExists(1), "Delete should actually delete once confirmed");
            Assert.AreEqual("Slot 2: Empty", ResetRowText(1), "and the row should say so");
        }

        [UnityTest]
        public IEnumerator DeletingAnAlreadyEmptySlot_DoesNotOpenAPointlessConfirmation()
        {
            // Confirmations that appear when nothing is at stake are how people
            // learn to click through the ones that matter.
            yield return LoadMenu();
            Click("OptionsButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            Click("ResetSlot3DeleteButton");

            Assert.IsFalse(Named("ResetConfirmPanel").activeSelf);
        }

        [UnityTest]
        public IEnumerator ReopeningPlayAfterADeletion_ShowsTheSlotAsEmptyAgain()
        {
            // No cross-panel event wires these together - the Play list refreshes
            // in OnEnable. This is the path that makes that sufficient.
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
            Click("Slot4Button");
            Click("CloseSaveSlotButton");

            Click("OptionsButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
            Click("ResetSlot4DeleteButton");
            Click("ResetConfirmYesButton");
            Click("CloseOptionsButton");

            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
            Assert.AreEqual("Slot 5: Empty", SlotButtonText(4));
        }
    
        [UnityTest]
        public IEnumerator ChoosingASlotEntersTheHub()
        {
            // The other half of the wiring: the slot is written first, so a
            // player who picks one and immediately quits still finds it there.
            yield return LoadMenu();

            Click("PlayButton");

            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;

            Click("Slot0Button");

            CollectionAssert.Contains(_navigated, Navigation.Hub);
            Assert.IsTrue(SaveSystem.SlotExists(0), "the slot must be written BEFORE the scene changes");
        }

        private static GameObject FindGo(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == name && g.scene.IsValid());

        private static void ClickByName(string name)
        {
            var go = FindGo(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");

            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, $"'{name}' has no Button to press");
            button.onClick.Invoke();
        }

        // ---- deleting a slot actually deletes it ---------------------------

        [UnityTest]
        public IEnumerator DeletingTheSlotYouAreIn_DoesNotComeBackOnTheNextSave()
        {
            // DRIVEN THROUGH THE BUTTONS, not by calling what the controller
            // calls. The first version of this test ran SaveSlotManager.Forget
            // itself with a comment saying "what ConfirmDelete now does" -- so
            // it passed whether or not ConfirmDelete did anything, which is the
            // exact shape architecture_audit.md F17 was written about an hour
            // before it was typed.
            //
            // The bug: SaveSlotManager holds ONE SaveData for the current slot
            // and SaveCurrent writes it wherever CurrentSlot points. Deleting
            // the file left that copy alone, so the next Persist -- starting a
            // run, buying an upgrade, anything -- wrote the whole save back with
            // every item and every worn piece of gear on it. It looked like the
            // delete had worked, because the slot list refreshes off the disk it
            // had just been removed from.
            const int Slot = 2;

            SaveSlotManager.CurrentSlot = Slot;
            SaveSlotManager.Forget();
            var save = SaveSlotManager.CurrentSave;
            save.roster.FirstOrDefault()?.equipment
                .Set(Domain.Equipment.EquipmentSlot.Weapon1, "health_potion", plus: 2);
            SaveSlotManager.SaveCurrent();
            Assert.IsTrue(SaveSystem.SlotExists(Slot), "fixture check: the slot should exist before deletion");

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            // BY NAME, because the controller's fields are `internal` and
            // InternalsVisibleTo is granted to the Editor assembly only -- a
            // PlayMode test is deliberately kept from reaching into controller
            // state, so it has to press what a player presses.
            // OPEN OPTIONS FIRST, because that is where the reset UI lives and
            // the panel starts inactive -- so ResetProgressController.Start has
            // not run and none of its listeners are wired until a player opens
            // it. Found by this test failing on the assertion below rather than
            // on the one it was written for, which is why that assertion is
            // there.
            ClickByName("OptionsButton");
            yield return null;
            yield return null;

            ClickByName($"ResetSlot{Slot}DeleteButton");
            yield return null;

            // PROVES THE CLICK LANDED. Start() wires these listeners a frame
            // after activation, so a test that clicks too early presses a button
            // with nothing attached -- and then the assertion at the bottom
            // fails for a reason that has nothing to do with what it is testing.
            var confirm = FindGo("ResetConfirmPanel");
            Assert.IsNotNull(confirm, "the reset screen has no ResetConfirmPanel");
            Assert.IsTrue(confirm.activeInHierarchy,
                "the delete button did not open the confirmation, so its listener was not wired yet " +
                "and nothing below this line is testing what it claims to");

            ClickByName("ResetConfirmYesButton");

            // The write that used to resurrect it.
            SaveSlotManager.SaveCurrent();

            Assert.IsFalse(SaveSystem.SlotExists(Slot),
                "the deleted slot was written straight back from the in-memory copy, so the save and " +
                "everything on it survived being deleted");
        }

}
}
