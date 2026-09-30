using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // THE MAP'S HINT NAMES A KEY (plan C20), and the key follows the device in
    // hand. What the hint says is DossierAttentionTests' and the EditMode
    // CharacterAttentionTests'; this holds that it is repainted when the player
    // changes hands with the map already up, and not only when the menu closes.
    public class MapAttentionHintTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-hint-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string HintText() =>
            Resources.FindObjectsOfTypeAll<TMP_Text>()
                .First(t => t.name == "MapAttentionLabel" && t.gameObject.scene.IsValid()).text;

        [UnityTest]
        public IEnumerator TheHintNamesTheKeyOfTheDeviceInHandAndFollowsAChange()
        {
            RunManager.StartRun(4242UL);
            SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == "bear").unspentStatPoints = 2;

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            TakeOverInput();
            yield return null;

            // The pad is the device until something else moves.
            yield return DriveFrame();
            Assert.IsTrue(NavigationInputModule.LastInputWasPad, "fixture: the pad is not the device in hand");
            Assert.AreEqual("Points to spend — press Start", HintText());

            Input.MousePosition = new Vector2(420f, 310f);
            yield return DriveFrame();
            yield return DriveFrame();
            Assert.IsFalse(NavigationInputModule.LastInputWasPad, "fixture: the mouse did not take over");
            Assert.AreEqual("Points to spend — press C", HintText());
        }
    }
}
