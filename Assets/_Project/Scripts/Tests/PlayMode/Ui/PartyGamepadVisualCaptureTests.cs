using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // PLAN SECTION 8'S DEMAND, which no assertion can settle: the four states
    // a Party slot can be in, all on screen at once, in one picture the owner
    // looks at. The reused ThemedButtonState ratio (SelectedGlowAlpha at
    // SelectedGlowScale, PartyController.PaintSelectHalo) is a CANDIDATE
    // treatment applied to this capture, not a verified fix -- if the states
    // are not distinguishable at a glance, section 8 says the answer is a
    // fifth visual channel, not further tuning of these numbers.
    //
    // The four, as this fixture arranges them:
    //   - the CARRIED SOURCE: seat 1, picked up (gold glow + gold ring).
    //   - a DISTINCT VALID DESTINATION: seat 0, which the model lights the
    //     same gold while a carry is live (Formation.IsValidDestination).
    //   - the SELECTED SUBMIT TARGET: seat 2, carrying the new halo ON TOP of
    //     that same destination gold -- the hardest of the four to tell
    //     apart, which is exactly why it is in the picture.
    //   - a MOUSE HOVER on an unrelated slot: roster card 1, pointed at
    //     through the module's own raycast (ScriptedBaseInput.MousePosition),
    //     not by calling a hover handler. Card 1 rather than card 0 because
    //     card 0 is the carried character's own card and already wears the
    //     model's SELECTED tag -- a hover on it would prove nothing.
    //
    // THE FOURTH STATE HAS NOTHING TO DRAW, and the capture is how that was
    // found rather than argued. Party's seats and cards are NoChrome()
    // Buttons with no Hovers() scale, no HoverIndex and no ThemedButtonState
    // (grepped: neither PartyScreen nor PartyController mentions any of the
    // three) -- so this screen has never had a hover treatment at all, and
    // the picture shows three states, not four. That is a finding for the
    // owner, not a fixture bug: if hover is meant to read here, a hover
    // channel has to exist before it can be told apart from selection.
    //
    // CONTENT-SHAPE LIMIT, stated rather than hidden (.claude/rules/tests.md):
    // characters.json authors three characters, so the roster is three cards,
    // all of them seated -- there is no "fifth card" to hover as section 8's
    // wording imagines. A roster card is hovered instead; it is still a slot
    // unrelated to the carry, which is what that state is there to show.
    //
    // NOT A GATE. run_tests_parallel.ps1 passes -nographics, where
    // CanvasCapture.IsSupported is false and this ignores itself -- the same
    // guard every other *CaptureTests fixture carries. Driven by
    // tools/screenshot.ps1 -Runtime -RuntimeFilter
    // PartyGamepadVisualCaptureTests.
    public class PartyGamepadVisualCaptureTests
    {
        // FLAT, not a subfolder -- tools/screenshot.ps1's copy-back check
        // reads this directory's own top level only.
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        private string _root;
        private ScriptedBaseInput _input;
        private SystemMenuController _menu;
        private PartyController _party;
        private Canvas _canvas;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-party-capture-" + System.Guid.NewGuid().ToString("N"));
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
            SaveSystem.RootOverride = null;
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator TheFourPartyStatesInOnePicture()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter " +
                              "PartyGamepadVisualCaptureTests");
            }

            yield return OpenTheParty();

            // BEFORE: nothing carried, nothing selected -- the baseline the
            // other picture has to be read against.
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            Shoot("party_gamepad_states_before");

            // 1 + 2: pick seat 1 up. The model lights the source AND every
            // valid destination (seats 0 and 2) at once; nothing about that
            // is new here, and it is half of what the owner is judging.
            EventSystem.current.SetSelectedGameObject(Seat(PartySeat.Middle));
            yield return null;
            yield return SubmitFrame();
            Assert.AreEqual("sheep", _party.Formation.SelectedId, "fixture: seat 1 was not picked up");

            // 3: the Submit target -- the selection walked one seat on, so the
            // halo sits over a slot the model is ALSO lighting as a
            // destination.
            EventSystem.current.SetSelectedGameObject(Seat(PartySeat.Rear));
            yield return null;

            // 4: a real mouse hover on an unrelated roster card, through the module's own
            // raycast. Reported rather than asserted: whether a scripted
            // pointer resolves against this scene's ScreenSpaceCamera canvas
            // is not this capture's claim, and a picture missing one of the
            // four states is more useful than no picture at all.
            yield return HoverCard(1);

            Shoot("party_gamepad_states_four");
        }

        // ---- fixture ---------------------------------------------------------------

        private IEnumerator OpenTheParty()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the Hub scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub carries no SystemMenuController");
            _menu.Open();
            _menu.Select(SystemMenuTab.Party);
            yield return null;

            _party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_party, "the hub carries no PartyController");

            _canvas = Object.FindAnyObjectByType<Canvas>();
            Assert.IsNotNull(_canvas, "the Hub scene has no Canvas to render");
            yield return null;
        }

        private IEnumerator SubmitFrame()
        {
            _input.SubmitDown = true;
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator HoverCard(int index)
        {
            var card = NodeNamed($"PartyCard{index}Button");
            var rect = card.transform as RectTransform;
            _input.MousePosition = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, rect.position);

            // Two frames: one for the module to raycast the new position, one
            // for whatever the hover changed to have been laid out and drawn.
            yield return null;
            yield return null;

            Debug.Log($"[PartyGamepadVisualCapture] pointer over '{card.name}' at {_input.MousePosition} -- " +
                      "check the picture for whether the hover actually landed");
        }

        private GameObject Seat(int index) => NodeNamed($"PartySeat{index}Button");

        private GameObject NodeNamed(string name)
        {
            var go = _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
            Assert.IsNotNull(go, $"the Party pane has no '{name}'");
            return go;
        }

        private void Shoot(string fileName)
        {
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, fileName + ".png");
            CanvasCapture.RenderToFile(_canvas, path);
            FileAssert.Exists(path);
        }
    }
}
