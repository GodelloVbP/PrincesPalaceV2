using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // The text-style main menu lights one word at a time: the mouse and the
    // pad share one selection (HoverSelects), and the pad arrow stands level
    // with a two-line item's caption (FocusAnchor). Driven through the real
    // dispatcher and a real pointer move, like every JourneyFixture segment.
    public class MainMenuPointerTests : JourneyFixture
    {
        private static readonly Color32 Lit = new Color32(227, 193, 102, 255);

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-mainmenu-pointer-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            TakeOverInput();
            yield return null;
            yield return null;
        }

        // The button carries its own (empty) label; the word the player sees
        // is the child named after it.
        private static TMP_Text Caption(string button) =>
            Node(button).transform.Find(button + "Caption").GetComponent<TMP_Text>();

        private static bool IsLit(string button) =>
            (Color32)Caption(button).canvasRenderer.GetColor() is var c
            && c.r == Lit.r && c.g == Lit.g && c.b == Lit.b;

        private static readonly string[] Items =
            { "NewRunButton", "LoadButton", "SettingsButton", "ExitButton" };

        [UnityTest]
        public IEnumerator PointerEnterOnLoad_SelectsLoad_AndOnlyLoadIsLit()
        {
            yield return LoadMenu();
            AssertSelectedName("NewRunButton", "precondition: with no save the entry is New Run");

            yield return SettleMouseAt(Node("LoadButton"));
            yield return WaitUntil(() => IsLit("LoadButton"), 2f, "the hovered word should light");
            yield return WaitUntil(() => !IsLit("NewRunButton"), 2f, "the word the pad left selected should go idle");

            AssertSelectedName("LoadButton", "hover selects, so hover and selection are one state");
            foreach (var item in Items)
            {
                Assert.AreEqual(item == "LoadButton", IsLit(item),
                    $"{item}: exactly one item may carry the lit colour");
            }
        }

        [UnityTest]
        public IEnumerator WithTheLoadPanelOpen_HoveringABaseItem_DoesNotChangeTheSelection()
        {
            yield return LoadMenu();

            yield return Click(Node("LoadButton"));
            yield return DriveFrame();
            var inPanel = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(inPanel, "precondition: the open Load list holds the selection");
            Assert.AreNotEqual("LoadButton", inPanel.name, "precondition: focus moved into the panel");

            yield return SettleMouseAt(Node("SettingsButton"));

            Assert.AreSame(inPanel, EventSystem.current.currentSelectedGameObject,
                "the dimmer stops the pointer, so a base item under it must not take focus");
        }

        [UnityTest]
        public IEnumerator TheArrowBesideContinue_IsLevelWithItsCaption_NotTheRowCentre()
        {
            SaveSlotManager.Forget();
            var save = SaveSlotManager.CurrentSave;
            Assert.IsNotNull(save);
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            yield return LoadMenu();
            AssertSelectedName("ContinueButton", "precondition: a save makes Continue the entry");

            Input.Horizontal = 1f;
            yield return DriveFrame();
            Input.Horizontal = 0f;
            yield return DriveFrame();

            var marker = NavigationInputModule.Marker;
            Assert.IsTrue(NavigationInputModule.LastInputWasPad, "precondition: the pad is driving");
            Assert.IsTrue(marker.IsShown, "the arrow shows for the pad");
            Assert.AreSame(Node("ContinueButton").transform, marker.Target);

            var canvas = (RectTransform)marker.transform.parent;
            var caption = Caption("ContinueButton").rectTransform;
            var row = (RectTransform)Node("ContinueButton").transform;
            float captionY = canvas.InverseTransformPoint(caption.TransformPoint(caption.rect.center)).y;
            float rowY = canvas.InverseTransformPoint(row.TransformPoint(row.rect.center)).y;

            Assert.Greater(Mathf.Abs(captionY - rowY), 10f, "fixture: the caption sits clear of the row centre");
            Assert.AreEqual(captionY, marker.LocalPosition.y, 0.5f,
                "the arrow is level with CONTINUE - SLOT, not between it and the subline");
        }
    }
}
