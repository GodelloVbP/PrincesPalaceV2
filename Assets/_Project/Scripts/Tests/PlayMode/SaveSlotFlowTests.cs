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
}
}
