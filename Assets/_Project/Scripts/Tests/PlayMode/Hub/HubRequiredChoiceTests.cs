using System.Collections;
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
    // A Bjorn with no Fury engine root earns no Fury, so the gate will not
    // take a squad containing one down: it sends the player to that
    // character's Talents page, and says so on its caption.
    public class HubRequiredChoiceTests
    {
        private string _root;
        private string _loaded;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-hub-choice-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            _loaded = null;
            Navigation.LoadOverride = scene => _loaded = scene;
            TalentController.RequestedCharacterId = null;
            HubController.MotionSpeedMultiplier = 200f;
        }

        [TearDown]
        public void Restore()
        {
            HubController.MotionSpeedMultiplier = 1f;
            TalentController.RequestedCharacterId = null;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static IEnumerator OpenTheHub(string root)
        {
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new System.Collections.Generic.List<string> { "bear" };
            var bear = save.roster.First(c => c.definitionId == "bear");
            bear.unlockedTalentIds.Clear();
            if (root != null) bear.unlockedTalentIds.Add(root);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static void PressGate(HubController hub) =>
            hub.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "StartRunGate").onClick.Invoke();

        [UnityTest]
        public IEnumerator BjornWithNoRoot_GateSendsThePlayerToHisTalentsAndSaysWhy()
        {
            yield return OpenTheHub(null);
            var hub = Object.FindAnyObjectByType<HubController>();

            var caption = hub.GetComponentsInChildren<TMP_Text>(includeInactive: true)
                .First(t => t.name == "StartRunGateCaption");
            StringAssert.Contains("CHOOSE", caption.text);

            PressGate(hub);
            yield return null;

            Assert.AreEqual(Navigation.Talents, _loaded);
            Assert.AreEqual("bear", TalentController.RequestedCharacterId);
            Assert.IsFalse(RunManager.HasRun, "a blocked squad must not start a run");
        }

        [UnityTest]
        public IEnumerator BjornWithARoot_GateStartsTheDescent()
        {
            yield return OpenTheHub("bear_sen_root");
            var hub = Object.FindAnyObjectByType<HubController>();

            PressGate(hub);
            yield return new WaitForSecondsRealtime(1f);

            Assert.AreNotEqual(Navigation.Talents, _loaded);
            Assert.IsTrue(RunManager.HasRun);
        }
    }
}
