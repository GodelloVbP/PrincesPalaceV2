using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Free scrolling on the normal map view: drag (with the EventSystem's own
    // threshold), clamped to the leg, and re-pinned by the next move. Driven
    // through the real module with a scripted mouse, so the threshold under
    // test is the engine's and not a copy of it.
    public class MapFreeScrollTests
    {
        private string _root;
        private MapController _map;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-scroll-" + System.Guid.NewGuid().ToString("N"));
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
            NavSceneReuse.AfterTest(_input);
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator OpenTheMap()
        {
            RunManager.StartRun(4242);
            yield return SharedScene.Ensure("Map");

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map);
            Assert.IsFalse(_map.IsWalking);
            _map.Refresh();
            _input = NavSceneReuse.TakeOverInput();
            yield return null;
        }

        private GameObject Named(string name) =>
            _map.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private Vector2 ScreenOf(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.position);
        }

        // Press at `from`, travel to `to` in `steps` frames, release.
        private IEnumerator Drag(Vector2 from, Vector2 to, int steps)
        {
            _input.MousePosition = from;
            yield return null;
            _input.MouseButton0Down = true;
            _input.MouseButton0Held = true;
            yield return null;
            _input.MouseButton0Down = false;

            for (int i = 1; i <= steps; i++)
            {
                _input.MousePosition = Vector2.Lerp(from, to, i / (float)steps);
                yield return null;
            }

            _input.MouseButton0Held = false;
            _input.MouseButton0Up = true;
            yield return null;
            _input.MouseButton0Up = false;
            yield return null;
        }

        private static Vector2 BareWood() => new Vector2(8f, Screen.height * 0.5f);

        [UnityTest]
        public IEnumerator ADragPansTheMapAndStaysInsideTheLeg()
        {
            yield return OpenTheMap();
            Assert.Greater(_map.MaxScroll, 400f, "vacuity: the leg is taller than the window");

            _map.ScrollBy(-1e6f);
            Assert.AreEqual(0f, _map.ScrollY, 0.001f, "the top of the leg is the floor");

            var start = BareWood();
            yield return Drag(start, start + new Vector2(0f, 150f), 6);
            Assert.Greater(_map.ScrollY, 0.5f, "dragging up should slide the content up");

            float before = _map.ScrollY;
            yield return Drag(start, start - new Vector2(0f, 150f), 6);
            Assert.Less(_map.ScrollY, before, "dragging down should slide it back");

            _map.ScrollBy(1e6f);
            Assert.AreEqual(_map.MaxScroll, _map.ScrollY, 0.01f, "the heart is the ceiling");
            _map.ScrollBy(-1e6f);
            Assert.AreEqual(0f, _map.ScrollY, 0.001f);
        }

        [UnityTest]
        public IEnumerator APressThatNeverCrossesTheDragThresholdStillClicksItsRoom()
        {
            yield return OpenTheMap();

            var room = RunManager.Choices().First();
            var tile = Named($"MapNode{MapGraph.Of(RunManager.Map).IndexOf(room.Id)}");
            var at = ScreenOf((RectTransform)tile.transform);
            Assert.IsTrue(new Rect(0, 0, Screen.width, Screen.height).Contains(at),
                "vacuity: the room is on screen at the test resolution");

            float before = _map.ScrollY;
            yield return Drag(at, at + new Vector2(2f, 2f), 2);

            Assert.AreEqual(before, _map.ScrollY, 0.001f, "a click must not pan");
            Assert.IsTrue(_map.IsWalking || RunManager.CurrentNode.Id == room.Id, "the room did not take the click");

            for (float waited = 0f; waited < 8f && _map.IsWalking; waited += Time.deltaTime) yield return null;
            SharedScene.MarkDirty("walked to a room");
        }

        [UnityTest]
        public IEnumerator ADragThatStartsOnARoomPansAndDoesNotClickIt()
        {
            yield return OpenTheMap();

            var room = RunManager.Choices().First();
            var tile = Named($"MapNode{MapGraph.Of(RunManager.Map).IndexOf(room.Id)}");
            var at = ScreenOf((RectTransform)tile.transform);
            Assert.IsTrue(new Rect(0, 0, Screen.width, Screen.height).Contains(at), "vacuity: the room is on screen");

            _map.ScrollBy(-1e6f);
            yield return Drag(at, at + new Vector2(0f, 120f), 6);

            Assert.Greater(_map.ScrollY, 0.5f, "the drag should have panned");
            Assert.IsFalse(_map.IsWalking, "a drag must not walk to the room it started on");
        }

        [UnityTest]
        public IEnumerator AMoveReturnsTheCameraToItsPinOnTheParty()
        {
            yield return OpenTheMap();

            _map.Refresh();
            float pinned = _map.ScrollY;
            _map.ScrollBy(_map.MaxScroll > pinned + 200f ? 200f : -200f);
            Assert.Greater(Mathf.Abs(_map.ScrollY - pinned), 0.5f, "vacuity: the camera was moved off its pin");

            var room = RunManager.Choices().First();
            Named($"MapNode{MapGraph.Of(RunManager.Map).IndexOf(room.Id)}")
                .GetComponent<Button>().onClick.Invoke();
            for (float waited = 0f; waited < 8f && _map.IsWalking; waited += Time.deltaTime) yield return null;
            Assert.IsFalse(_map.IsWalking);

            float arrived = _map.ScrollY;
            _map.Refresh();
            Assert.AreEqual(_map.ScrollY, arrived, 0.5f, "the camera did not come back to the pin on the new room");
        }

        [UnityTest]
        public IEnumerator TheOverviewDoesNotScrollFreely()
        {
            yield return OpenTheMap();

            _map.SetOverview(true);
            float before = _map.ScrollY;
            _map.ScrollBy(300f);
            Assert.AreEqual(before, _map.ScrollY, 0.001f);
            _map.SetOverview(false);
        }

        [UnityTest]
        public IEnumerator TheOverviewHintNamesSpaceOnAKeyboard()
        {
            if (Input.GetJoystickNames().Any(n => !string.IsNullOrEmpty(n)))
            {
                Assert.Ignore("a pad is connected to this machine");
            }

            yield return OpenTheMap();

            var hint = Named("MapOverviewLabel");
            Assert.IsTrue(hint.activeSelf);
            Assert.AreEqual("SPACE  ·  OVERVIEW", hint.GetComponent<TMP_Text>().text);

            _map.SetOverview(true);
            Assert.AreEqual("OVERVIEW  ·  LOOK ONLY  ·  SPACE TO RETURN", hint.GetComponent<TMP_Text>().text);
            _map.SetOverview(false);
        }

        [UnityTest]
        public IEnumerator TheTitleReadsFloorAndRoomAndTheGoldIsABareAmountBesideTheCoin()
        {
            yield return OpenTheMap();
            var run = RunManager.Run;

            Assert.AreEqual($"FLOOR {run.floor}  ·  ROOM {run.roomsWalked}",
                Named("MapTitleLabel").GetComponent<TMP_Text>().text);
            Assert.AreEqual(run.gold.ToString(), Named("MapGoldLabel").GetComponent<TMP_Text>().text);
            Assert.IsTrue(Named("MapGoldIcon").activeSelf);
            Assert.IsNull(Named("MapDepthLabel"));
            Assert.IsNotNull(Named("MapVignette"));
        }
    }
}
