using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.DebugMenu;

namespace PrincesPalace.PlayModeTests
{
    // What the map paints at runtime, checked AFTER the frames that could
    // undo it -- plus the debug menu the map now mounts.
    //
    // Both paint bugs here were invisible to every test that read the map
    // straight after Refresh(): the room tints were wiped a few frames later
    // by the press animator easing each tile's Image back to white, and the
    // hover rim was sized once at build time to the boss tile, so it only
    // went wrong on the rooms PaintNode shrinks.
    public class MapPaintSurvivesTests
    {
        private string _root;
        private MapController _map;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-paint-" + System.Guid.NewGuid().ToString("N"));
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
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _map.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static IEnumerator Settle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private IEnumerator OpenTheMap()
        {
            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map, "the Map scene has no MapController");
        }

        private Button[] ShownRooms() =>
            _map.GetComponentsInChildren<Button>(includeInactive: true)
                .Where(b => b.gameObject.activeSelf && b.name.StartsWith("MapNode") && !b.name.Contains("Marker"))
                .ToArray();

        [UnityTest]
        public IEnumerator RoomTintsAreStillThereAFewFramesLater()
        {
            yield return OpenTheMap();
            yield return Settle(0.4f);

            // Some room on a fresh leg is not the one you stand on and not one
            // you can walk to -- AHEAD or CLOSED, both drawn dimmer than white.
            var dimmed = ShownRooms()
                .Select(b => (Image)b.targetGraphic)
                .Where(image => image != null && image.color.r < 0.9f)
                .ToList();

            Assert.IsNotEmpty(dimmed,
                "every room tile is back at full white, so nothing on the map says which rooms are behind, " +
                "ahead or out of reach");
        }

        [UnityTest]
        public IEnumerator AnOrdinaryRoomsHoverRimHugsTheTileItIsDrawnAt()
        {
            yield return OpenTheMap();

            // An ordinary room: PaintNode shrinks it below the boss-sized box
            // every tile is declared at.
            var room = ShownRooms().FirstOrDefault(b => ((RectTransform)b.transform).rect.width < 120f);
            Assert.IsNotNull(room, "fixture: a fresh leg has ordinary rooms");

            var rim = room.transform.Cast<Transform>().FirstOrDefault(t => t.name == "HoverRim") as RectTransform;
            Assert.IsNotNull(rim, "fixture: the room has a hover rim");

            var tile = ((RectTransform)room.transform).rect;

            // 4 is Ui's HoverRimPad, stated as a literal on purpose.
            Assert.AreEqual(tile.width + 8f, rim.rect.width, 0.5f, "the rim is sized for some other tile");
            Assert.AreEqual(tile.height + 8f, rim.rect.height, 0.5f, "the rim is sized for some other tile");
        }

        [UnityTest]
        public IEnumerator TheDebugMenuOpensOnTheMapAndItsJumpRedrawsTheLeg()
        {
            yield return OpenTheMap();

            _map.SetDebugMenu(true);
            yield return null;
            yield return null;
            Assert.IsTrue(_map.DebugMenuIsOpen);

            Click($"DebugCategory{(int)DebugCategory.Tools}");
            yield return null;
            Click("DebugRow1"); // jump to this leg's boss
            yield return null;

            var bossIndex = RunManager.Map.Boss;
            Assert.IsTrue(RunManager.Choices().Any(n => n.Id == bossIndex.Id), "fixture: the jump did not happen");

            // The map repainted on the debug menu's Changed callback, not on
            // the next room: the boss tile is walkable NOW.
            var walkable = ShownRooms().Where(b => b.interactable).ToList();
            Assert.IsNotEmpty(walkable);
            Assert.IsTrue(walkable.Count <= RunManager.Choices().Count,
                "the map still offers the rooms from before the jump");
        }
    }
}
