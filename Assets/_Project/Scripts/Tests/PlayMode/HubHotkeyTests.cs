using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // C and I open the character overlay; Escape closes it.
    //
    // THE KEY PRESS ITSELF IS NOT TESTED, and cannot be: legacy Input reads the
    // real keyboard and has no headless injection point. What is testable -- and
    // what actually breaks -- is the seam underneath it. HubController.Update
    // does nothing but read a key and call ToggleCharacterOverlay; these drive
    // that method directly, which is also exactly what the Character Sheet
    // building calls, so the button and the key cannot drift apart.
    //
    // v1 split EquipmentController the same way for the same reason. The split
    // is the point, not an accident of testing.
    public class HubHotkeyTests
    {
        private string _root;
        private HubController _hub;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-hotkey-" + System.Guid.NewGuid().ToString("N"));
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
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");
        }

        [UnityTest]
        public IEnumerator TheToggleOpensAndClosesTheOverlay()
        {
            yield return OpenTheHub();
            Assert.IsFalse(_hub.CharacterOverlayIsOpen, "the hub opens with the overlay down");

            _hub.ToggleCharacterOverlay();
            yield return null;
            Assert.IsTrue(_hub.CharacterOverlayIsOpen);

            _hub.ToggleCharacterOverlay();
            yield return null;
            Assert.IsFalse(_hub.CharacterOverlayIsOpen, "the same key closes it again");
        }

        [UnityTest]
        public IEnumerator SetIsIdempotentSoAHeldKeyCannotFlicker()
        {
            yield return OpenTheHub();

            _hub.SetCharacterOverlay(true);
            _hub.SetCharacterOverlay(true);
            yield return null;

            Assert.IsTrue(_hub.CharacterOverlayIsOpen);

            _hub.SetCharacterOverlay(false);
            _hub.SetCharacterOverlay(false);
            yield return null;

            Assert.IsFalse(_hub.CharacterOverlayIsOpen);
        }

        [UnityTest]
        public IEnumerator TheBuildingAndTheKeyGoThroughTheSameDoor()
        {
            // The building used to call characterOverlayPanel.SetActive(true)
            // directly. Two ways in that do not share a path are two places a
            // future guard (a pause menu, a dialogue) has to be remembered.
            yield return OpenTheHub();

            var building = _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .First(t => t.name == "CharacterSheetBuilding")
                .GetComponent<Button>();

            building.onClick.Invoke();
            yield return null;
            Assert.IsTrue(_hub.CharacterOverlayIsOpen);

            // And the key closes what the building opened.
            _hub.ToggleCharacterOverlay();
            yield return null;
            Assert.IsFalse(_hub.CharacterOverlayIsOpen);
        }

        [Test]
        public void TogglingSurvivesAMissingOverlayRatherThanThrowing()
        {
            // Graceful degradation is the house style, and Update() runs every
            // frame -- an unguarded null here would be a NullReferenceException
            // per frame, forever, on any scene that mounts the hub controller
            // without an overlay (the descent map, next).
            //
            // Built INACTIVE on purpose: a live GameObject would run OnEnable,
            // and Refresh() reaches for a currency label this bare object does
            // not have. The subject here is the three overlay methods, not the
            // hub's whole lifecycle.
            var bare = new GameObject("BareHub");
            bare.SetActive(false);
            var controller = bare.AddComponent<HubController>();

            Assert.DoesNotThrow(() => controller.ToggleCharacterOverlay());
            Assert.DoesNotThrow(() => controller.SetCharacterOverlay(true));
            Assert.IsFalse(controller.CharacterOverlayIsOpen, "no overlay is never 'open'");

            Object.DestroyImmediate(bare);
        }
    }
}
