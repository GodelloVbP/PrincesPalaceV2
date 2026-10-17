using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // What the map tells a player choosing a route (AUDIT #226, #254): the facts
    // behind each room, the cue only where a press works, and the
    // reason a refused press gives.
    public class MapRoutePreviewTests
    {
        private string _root;
        private MapController _map;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-route-" + System.Guid.NewGuid().ToString("N"));
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
            SharedScene.AfterTest();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _map.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator OpenTheMap(ulong seed = 4242)
        {
            RunManager.StartRun(seed);
            yield return SharedScene.Ensure("Map");

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            Assert.IsFalse(_map.IsWalking, "fixture: the previous test left a walk in flight");
            _map.Refresh();
        }

        private static int IndexOf(RealmNode node) => MapGraph.Of(RunManager.Map).IndexOf(node.Id);

        private GameObject NodeObject(RealmNode node) => Named($"MapNode{IndexOf(node)}");

        private static void Pointer(GameObject go, bool enter)
        {
            var data = new PointerEventData(EventSystem.current);
            if (enter) ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
            else ExecuteEvents.Execute(go, data, ExecuteEvents.pointerExitHandler);
        }

        private static void Click(GameObject go) =>
            ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        // ---- the data ------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFightsPreviewIsTheRosterTheRoomLaterFields()
        {
            int checkedRooms = 0;

            foreach (ulong seed in new ulong[] { 4242, 7, 99 })
            {
                yield return OpenTheMap(seed);
                var run = RunManager.Run;
                var save = SaveSlotManager.CurrentSave;

                foreach (var node in RunManager.Map.Nodes.Where(n =>
                             n.Type == RoomType.Fight || n.Type == RoomType.EliteFight || n.Type == RoomType.Boss))
                {
                    var facts = MapNodePreview.For(run, node);

                    // What MoveTo writes, then what the fight builds from it.
                    run.currentNodeId = node.Id;
                    run.step = run.legStartStep + node.Offset;
                    var roster = RunEncounter.For(save, run, node.Type);

                    var names = roster.EnemyIds.Select(id => ContentDatabase.GetEnemy(id).Data.DisplayName).ToList();
                    CollectionAssert.AreEqual(names, facts.EnemyNames, $"seed {seed} node {node.Id} ({node.Type})");
                    Assert.Greater(facts.Gold, 0, $"seed {seed} node {node.Id} quotes no gold");
                    Assert.Greater(facts.Experience, 0, $"seed {seed} node {node.Id} quotes no exp");
                    checkedRooms++;
                }
            }

            Assert.GreaterOrEqual(checkedRooms, 6, "the walk found almost no fights to compare");
        }

        [UnityTest]
        public IEnumerator ATreasurePreviewIsTheStashTheRoomThenPays()
        {
            int found = 0;

            // Treasure stands only in pockets, beside the realm's anchor and
            // shop, so it is rare on floor 1: seeds without one are skipped
            // before the map is opened.
            for (ulong seed = 1; seed <= 2000 && found < 3; seed++)
            {
                if (RealmPaths.FirstOfType(RealmPaths.FirstRealm(seed), RoomType.Treasure) == null) continue;
                yield return OpenTheMap(seed);
                var run = RunManager.Run;

                foreach (var node in RunManager.Map.Nodes.Where(n => n.Type == RoomType.Treasure))
                {
                    var facts = MapNodePreview.For(run, node);

                    run.currentNodeId = node.Id;
                    run.step = run.legStartStep + node.Offset;
                    RoomResolver.Resolve(RoomType.Treasure);

                    Assert.AreEqual(RoomResolver.Last.Gold, facts.StashGold, $"seed {seed} node {node.Id}");
                    Assert.That(facts.StashGold, Is.InRange(2, 5));
                    RoomResolver.Reset();
                    found++;
                }
            }

            Assert.GreaterOrEqual(found, 1, "no seed produced a treasure room to compare");
        }

        [UnityTest]
        public IEnumerator AnEventsPreviewIsTheTitleTheRoomThenOpensWith()
        {
            int found = 0;

            for (ulong seed = 1; seed <= 2000 && found < 4; seed++)
            {
                if (RealmPaths.FirstOfType(RealmPaths.FirstRealm(seed), RoomType.Event) == null) continue;
                yield return OpenTheMap(seed);
                var run = RunManager.Run;

                foreach (var node in RunManager.Map.Nodes.Where(n => n.Type == RoomType.Event))
                {
                    var facts = MapNodePreview.For(run, node);
                    Assert.IsNotEmpty(facts.EventTitle, $"seed {seed} node {node.Id}: the card names no event");

                    // What MoveTo writes, then what arrival opens.
                    run.currentNodeId = node.Id;
                    run.step = run.legStartStep + node.Offset;
                    Assert.IsTrue(RunOrchestrator.EnsureEvent(), $"seed {seed} node {node.Id}");

                    Assert.AreEqual(ContentDatabase.Events.First(e => e.Data.Id == run.eventId).Data.StartPage.Title, facts.EventTitle,
                        $"seed {seed} node {node.Id}");

                    run.eventId = "";
                    run.eventsSeen.Clear();
                    found++;
                }
            }

            Assert.GreaterOrEqual(found, 1, "no seed produced an event room to compare");
        }

        // What the map shows is what you get: B's card is decided on first sight
        // and survives A's arrival marking its own event seen, B's promised
        // event turning seen (its roll would now differ), and a reload.
        [UnityTest]
        public IEnumerator AnEventRoomKeepsItsPromisedEventAcrossAnotherArrivalAndAReload()
        {
            ulong seed = 0;
            for (ulong s = 1; s <= 4000 && seed == 0; s++)
            {
                if (RealmPaths.FirstRealm(s).Nodes.Count(n => n.Type == RoomType.Event) >= 2) seed = s;
            }
            Assert.AreNotEqual(0UL, seed, "no seed under 4000 has two event rooms");

            yield return OpenTheMap(seed);
            var run = RunManager.Run;
            var rooms = RunManager.Map.Nodes.Where(n => n.Type == RoomType.Event).Take(2).ToArray();
            var a = rooms[0];
            var b = rooms[1];

            string promised = MapNodePreview.For(run, b).EventTitle;
            Assert.IsNotEmpty(promised);
            string promisedId = run.nodeEvents.Single(e => e.nodeId == b.Id).eventId;

            // Enter A: its event is marked seen. Then B's own event turns seen
            // too, so a fresh roll could no longer return it.
            run.currentNodeId = a.Id;
            run.step = run.legStartStep + a.Offset;
            Assert.IsTrue(RunOrchestrator.EnsureEvent());
            run.eventId = "";
            if (!run.eventsSeen.Contains(promisedId)) run.eventsSeen.Add(promisedId);
            Assert.AreEqual(promised, MapNodePreview.For(run, b).EventTitle, "the card changed after another arrival");

            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            run = RunManager.Run;
            Assert.AreEqual(promisedId, run.nodeEvents.Single(e => e.nodeId == b.Id).eventId, "the promise did not survive a reload");

            run.currentNodeId = b.Id;
            run.step = run.legStartStep + b.Offset;
            Assert.IsTrue(RunOrchestrator.EnsureEvent());
            Assert.AreEqual(promisedId, run.eventId, "arrival opened a different event than the card promised");
        }

        // Two event rooms previewed back-to-back never promise the same event,
        // on a seed where the bare roll gives both the same one.
        [Test]
        public void TwoEventRoomsAreNeverPromisedTheSameEvent()
        {
            int checkedSeeds = 0;
            for (ulong seed = 1; seed <= 4000 && checkedSeeds < 1; seed++)
            {
                if (RealmPaths.FirstRealm(seed).Nodes.Count(n => n.Type == RoomType.Event) < 2) continue;
                RunManager.StartRun(seed);
                var run = RunManager.Run;
                var save = SaveSlotManager.CurrentSave;
                var rooms = RunManager.Map.Nodes.Where(n => n.Type == RoomType.Event).Take(2).ToArray();
                string x = RunOrchestrator.RollEvent(save, run, run.legStartStep + rooms[0].Offset, rooms[0].Id);
                string y = RunOrchestrator.RollEvent(save, run, run.legStartStep + rooms[1].Offset, rooms[1].Id);
                if (string.IsNullOrEmpty(x) || x != y) continue;

                string first = RunOrchestrator.DecideEvent(save, run, run.legStartStep + rooms[0].Offset, rooms[0].Id);
                string second = RunOrchestrator.DecideEvent(save, run, run.legStartStep + rooms[1].Offset, rooms[1].Id);
                Assert.AreEqual(x, first);
                Assert.AreNotEqual(first, second, $"seed {seed}: both rooms promised the same event");
                checkedSeeds++;
            }

            Assert.AreEqual(1, checkedSeeds, "no seed under 4000 rolls the same event for two rooms");
        }

        // A bot run decides every event room at leg start, in map order, through
        // the seam the card preview uses (AUDIT #275): same seed, same events a
        // player's previewed map reserves, and no two rooms share one.
        [Test]
        public void TheBotReservesEveryEventRoomAtLegStartLikeThePreview()
        {
            int checkedSeeds = 0;
            for (ulong seed = 1; seed <= 4000 && checkedSeeds < 3; seed++)
            {
                if (RealmPaths.FirstRealm(seed).Nodes.Count(n => n.Type == RoomType.Event) < 2) continue;
                RunManager.StartRun(seed);
                var run = RunManager.Run;
                var rooms = RunManager.Map.Nodes.Where(n => n.Type == RoomType.Event).ToArray();

                foreach (var node in RunManager.Map.Nodes) MapNodePreview.For(run, node);
                var previewed = run.nodeEvents.Select(e => $"{e.nodeId}:{e.eventId}").ToList();
                Assert.AreEqual(rooms.Length, previewed.Count, $"seed {seed}: fixture, one pick per event room");

                run.nodeEvents.Clear();
                run.eventsSeen.Clear();
                RunOrchestrator.ReserveLegEvents();

                CollectionAssert.AreEqual(previewed, run.nodeEvents.Select(e => $"{e.nodeId}:{e.eventId}").ToList(), $"seed {seed}");
                Assert.AreEqual(rooms.Length, run.nodeEvents.Select(e => e.eventId).Distinct().Count(), $"seed {seed}: two rooms share an event");
                checkedSeeds++;
            }

            Assert.AreEqual(3, checkedSeeds, "no seeds under 4000 with two event rooms");
        }

        // ---- the cue -------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheHoverRimIsLiveOnlyWhereAPressWorks()
        {
            yield return OpenTheMap();

            int live = 0;
            int dead = 0;
            foreach (var node in RunManager.Map.Nodes)
            {
                var go = NodeObject(node);
                var rim = go.GetComponent<HoverBox>();
                Assert.IsNotNull(rim, $"{go.name} has no hover box");
                Assert.AreEqual(go.GetComponent<Button>().interactable, rim.enabled, $"{go.name} ({node.Type})");
                if (rim.enabled) live++; else dead++;
            }

            Assert.Greater(live, 0, "the entry offers somewhere to go");
            Assert.Greater(dead, 0, "a leg has rooms the entry cannot reach");
        }

        [UnityTest]
        public IEnumerator OnlyARoomThatCanBeEnteredCarriesTheSoftGlow()
        {
            yield return OpenTheMap();

            int lit = 0;
            foreach (var node in RunManager.Map.Nodes)
            {
                var go = NodeObject(node);
                var glow = Named($"MapNode{IndexOf(node)}Glow");
                Assert.IsNotNull(glow, $"{go.name} has no glow ring");
                Assert.AreEqual(go.GetComponent<Button>().interactable, glow.activeSelf, $"{go.name} ({node.Type})");
                if (glow.activeSelf) lit++;
            }

            Assert.Greater(lit, 0, "vacuity: the entry offers somewhere to go");
        }

        [UnityTest]
        public IEnumerator TheGlowPulsesWithinItsAuthoredAlphaBand()
        {
            yield return OpenTheMap();

            var reachable = RunManager.Choices().First();
            var glow = Named($"MapNode{IndexOf(reachable)}Glow").GetComponent<Image>();

            float low = float.MaxValue;
            float high = float.MinValue;
            for (float waited = 0f; waited < 2.8f; waited += Time.deltaTime)
            {
                yield return null;
                low = Mathf.Min(low, glow.color.a);
                high = Mathf.Max(high, glow.color.a);
            }

            Assert.GreaterOrEqual(low, 0.12f - 0.001f, "the glow dipped under its floor");
            Assert.LessOrEqual(high, 0.38f + 0.001f, "the glow rose over its ceiling");
            Assert.Greater(high - low, 0.15f, "the glow did not pulse through one period");
        }

        // ---- the refusal, in no words --------------------------------------------

        // The tile flashes toward red and shakes, then returns exactly as it was.
        private IEnumerator ExpectTheCue(RealmNode node, System.Action press)
        {
            var go = NodeObject(node);
            var rect = (RectTransform)go.transform;
            var plate = (Image)go.GetComponent<Button>().targetGraphic;
            var rest = plate.color;
            var restAt = rect.anchoredPosition;

            press();

            float moved = 0f;
            float tinted = 0f;
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
                moved = Mathf.Max(moved, Mathf.Abs(rect.anchoredPosition.x - restAt.x));
                var c = plate.color;
                tinted = Mathf.Max(tinted, Mathf.Abs(c.r - rest.r) + Mathf.Abs(c.g - rest.g) + Mathf.Abs(c.b - rest.b));
            }

            Assert.Greater(moved, 0.5f, "the tile did not shake");
            Assert.Greater(tinted, 0.05f, "the tile did not flash");

            yield return new WaitForSecondsRealtime(0.35f + 0.3f);

            Assert.That(plate.color.r, Is.EqualTo(rest.r).Within(0.002f));
            Assert.That(plate.color.g, Is.EqualTo(rest.g).Within(0.002f));
            Assert.That(plate.color.b, Is.EqualTo(rest.b).Within(0.002f));
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(restAt.x).Within(0.001f), "the tile did not settle");
        }

        [UnityTest]
        public IEnumerator PressingARoomTooFarAheadShakesAndFlashesItsTile()
        {
            yield return OpenTheMap();

            var far = RunManager.Map.Nodes.First(n => n.Offset >= 2);
            yield return ExpectTheCue(far, () => Click(NodeObject(far)));
            Assert.IsFalse(_map.IsWalking, "a refused press must not start a walk");
        }

        [UnityTest]
        public IEnumerator PressingThePartysOwnRoomGivesTheSameCue()
        {
            yield return OpenTheMap();

            var here = RunManager.CurrentNode;
            yield return ExpectTheCue(here, () => Click(NodeObject(here)));
        }

        [UnityTest]
        public IEnumerator AForcedPressOnAnUnreachableRoomIsRefusedToo()
        {
            // onClick.Invoke bypasses interactable, the route a script or a
            // stray submit would take into the legality guard.
            yield return OpenTheMap();

            var far = RunManager.Map.Nodes.First(n => n.Offset >= 2);
            yield return ExpectTheCue(far, () => NodeObject(far).GetComponent<Button>().onClick.Invoke());
            Assert.IsFalse(_map.IsWalking);
        }

        [UnityTest]
        public IEnumerator ASecondPressMidWalkGivesTheCue()
        {
            // A treasure opening its pocket: quiet, and one column over.
            yield return OpenTheMap(RealmPaths.SeedWhere(
                m => RealmPaths.PocketHead(m, RoomType.Treasure) != null, "a treasure opening a pocket"));
            var quiet = RealmPaths.PocketHead(RunManager.Map, RoomType.Treasure);
            RealmPaths.StandBefore(quiet);
            _map.Refresh();
            var button = NodeObject(quiet).GetComponent<Button>();

            button.onClick.Invoke();
            yield return null;
            Assert.IsTrue(_map.IsWalking, "fixture: the first press should start a walk");

            var rect = (RectTransform)NodeObject(quiet).transform;
            float restX = rect.anchoredPosition.x;
            button.onClick.Invoke();

            float moved = 0f;
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
                moved = Mathf.Max(moved, Mathf.Abs(rect.anchoredPosition.x - restX));
            }

            Assert.Greater(moved, 0.5f, "the second press went unanswered");

            for (float waited = 0f; waited < 5f && _map.IsWalking; waited += Time.deltaTime) yield return null;
            Assert.IsFalse(_map.IsWalking, "the walk should have finished");
        }
    }
}
