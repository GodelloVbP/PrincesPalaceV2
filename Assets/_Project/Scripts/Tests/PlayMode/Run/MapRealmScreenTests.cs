using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // The realm map screen's own claims (docs/PLAN_RUN_MAP.md section 7): the
    // node grammar, the pair of cards, the fog over routes a choice closes off,
    // the heart with its anchors and roots, and the opening shot.
    public class MapRealmScreenTests
    {
        private string _root;
        private MapController _map;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-map-realm-" + System.Guid.NewGuid().ToString("N"));
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
            TestGlobals.ResetMapOpening();
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
            yield return Show();
        }

        private IEnumerator Show()
        {
            yield return SharedScene.Ensure("Map");
            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsNotNull(_map, "the Map scene has no MapController");
            _map.Refresh();
        }

        private GameObject NodeObject(RealmNode node) => Named(RealmPaths.ButtonName(node));

        // Through the EventSystem, so whatever the map selected on load (its
        // leftmost choice) is deselected first, as a pad press would. A room
        // that is already the selection gets the event again: the map's repaint
        // closed its card without moving the selection.
        private static void Focus(GameObject go)
        {
            if (EventSystem.current.currentSelectedGameObject == go)
            {
                ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
                return;
            }

            EventSystem.current.SetSelectedGameObject(go);
        }

        private static void Blur(GameObject go) =>
            ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.deselectHandler);

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var colour);
            return colour;
        }

        // ---- the node grammar -----------------------------------------------------

        [UnityTest]
        public IEnumerator AFightShowsOnePipPerStrengthAndAChipSizedByIt_AndNothingElseDoes()
        {
            int fights = 0;
            int withChips = 0;
            var strengthsSeen = new System.Collections.Generic.HashSet<int>();

            foreach (ulong seed in new ulong[] { 4242, 7, 19, 31 })
            {
                RunManager.ResetForTests();
                yield return OpenTheMap(seed);

                var run = RunManager.Run;
                foreach (var node in RunManager.Map.Nodes)
                {
                    var go = NodeObject(node);
                    var pips = Enumerable.Range(0, 3).Count(p => Named($"{go.name}Pip{p}").activeSelf);
                    var chip = Named($"{go.name}Chip");

                    bool fights_ = node.Type == RoomType.Fight || node.Type == RoomType.EliteFight;
                    if (!fights_)
                    {
                        Assert.AreEqual(0, pips, $"{node.Type} {go.name} shows pips");
                        Assert.IsFalse(chip.activeSelf, $"{node.Type} {go.name} shows a chip");
                        continue;
                    }

                    fights++;
                    int strength = RealmOpening.StrengthOf(run, node.Id);
                    strengthsSeen.Add(strength);
                    Assert.AreEqual(strength, pips, $"{go.name} shows the wrong number of pips");

                    var kind = RealmPrizes.ChipOf(run, node).Kind;
                    Assert.AreEqual(kind != PrizeKind.None, chip.activeSelf, $"{go.name} chip visibility");
                    if (!chip.activeSelf) continue;

                    withChips++;
                    var size = ((RectTransform)chip.transform).sizeDelta;
                    Assert.AreEqual(MapLayout.ChipWidthFor(strength), size.x, 0.01f, $"{go.name} chip width");
                    Assert.AreEqual(MapLayout.ChipHeightFor(strength), size.y, 0.01f, $"{go.name} chip height");
                    Assert.IsNotEmpty(Named($"{go.name}ChipText").GetComponent<TMP_Text>().text);
                }
            }

            Assert.GreaterOrEqual(fights, 6, "vacuity: the sample reached fights");
            Assert.Greater(withChips, 0, "vacuity: the sample reached prize chips");
            Assert.GreaterOrEqual(strengthsSeen.Count, 2, "vacuity: the sample reached more than one strength");
        }

        // ---- the pair of cards and the fog -----------------------------------------

        // A pocket on the left of the spine, with a single room past it: from
        // the spine room it leaves, the party chooses between the pocket and the
        // spine's next room.
        private static ulong SeedWithASkippablePocket() =>
            RealmPaths.SeedWhere(map => SkippablePocket(map) != null, "a pocket the spine can skip");

        private static RealmPocket SkippablePocket(RealmMap map) =>
            map.Pockets.FirstOrDefault(p => p.Stretch % 2 == 1
                && map.SpineAt(p.Stretch).Count() == 1 && map.SpineAt(p.Stretch + 1).Count() == 1);

        private IEnumerator StandAtASkippableChoice()
        {
            ulong seed = SeedWithASkippablePocket();
            RunManager.StartRun(seed);
            var pocket = SkippablePocket(RunManager.Map);
            RealmPaths.StandBefore(RunManager.Map.SpineAt(pocket.Stretch + 1).First());
            yield return Show();
        }

        [UnityTest]
        public IEnumerator TwoOpenRoutesAreComparedSideBySide()
        {
            yield return StandAtASkippableChoice();

            var choices = RunManager.Choices();
            Assert.AreEqual(2, choices.Count, "fixture: the pocket and the spine's next room");
            var graph = MapGraph.Of(RunManager.Map);
            var left = choices.OrderBy(n => graph.PositionOf(n.Id).X).First();
            var right = choices.OrderBy(n => graph.PositionOf(n.Id).X).Last();

            var preview = Named("MapPreviewPanel");
            var compare = Named("MapComparePanel");

            Focus(NodeObject(left));
            yield return null;

            Assert.IsTrue(preview.activeSelf);
            Assert.IsTrue(compare.activeSelf, "two routes are open and the second card is missing");
            StringAssert.StartsWith(MapNodeCard.Caption(left.Type), Named("MapPreviewTitle").GetComponent<TMP_Text>().text);
            StringAssert.StartsWith("OR", Named("MapCompareTitle").GetComponent<TMP_Text>().text);
            StringAssert.Contains(MapNodeCard.Caption(right.Type), Named("MapCompareTitle").GetComponent<TMP_Text>().text);
            Assert.Less(((RectTransform)preview.transform).anchoredPosition.x, 0f);
            Assert.Greater(((RectTransform)compare.transform).anchoredPosition.x, 0f);

            // The pair swap when the other route is the one in focus.
            Blur(NodeObject(left));
            Focus(NodeObject(right));
            yield return null;
            StringAssert.StartsWith(MapNodeCard.Caption(right.Type), Named("MapPreviewTitle").GetComponent<TMP_Text>().text);
            StringAssert.Contains(MapNodeCard.Caption(left.Type), Named("MapCompareTitle").GetComponent<TMP_Text>().text);

            Blur(NodeObject(right));
            yield return null;
            Assert.IsFalse(preview.activeSelf);
            Assert.IsFalse(compare.activeSelf, "losing focus left the second card up");
        }

        [UnityTest]
        public IEnumerator AOneRouteRealmShowsASingleCenteredCard()
        {
            // The entry of a fresh run offers one room on most seeds.
            ulong seed = RealmPaths.SeedWhere(
                map => map.ReachableFrom(map.Entry.Id).Count == 1, "an entry with a single exit");
            yield return OpenTheMap(seed);

            var choice = RunManager.Choices().Single();
            Focus(NodeObject(choice));
            yield return null;

            Assert.IsTrue(Named("MapPreviewPanel").activeSelf);
            Assert.IsFalse(Named("MapComparePanel").activeSelf, "there is nothing to compare a lone route with");
            Assert.AreEqual(0f, ((RectTransform)Named("MapPreviewPanel").transform).anchoredPosition.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator SkippingAPocketVeilsItsRooms_AndEnteringItVeilsNothing()
        {
            yield return StandAtASkippableChoice();

            var map = RunManager.Map;
            var pocket = SkippablePocket(map);
            var spineNext = map.SpineAt(pocket.Stretch + 1).First();
            var head = map.Node(pocket.Rooms[0]);

            bool Veiled(RealmNode node) => Named(RealmPaths.ButtonName(node) + "Veil").activeSelf;

            Focus(NodeObject(spineNext));
            yield return null;
            foreach (var node in map.Nodes)
            {
                Assert.AreEqual(pocket.Rooms.Contains(node.Id), Veiled(node),
                    $"node {node.Id} ({node.Role}) veil under the spine choice");
            }

            StringAssert.Contains("closes off",
                Named("MapPreviewBody").GetComponent<TMP_Text>().text, "the card says what the choice shuts");

            Blur(NodeObject(spineNext));
            yield return null;
            Assert.IsFalse(map.Nodes.Any(Veiled), "losing focus left fog behind");

            Focus(NodeObject(head));
            yield return null;
            Assert.IsFalse(map.Nodes.Any(Veiled), "a pocket rejoins the spine, so it closes nothing");
            StringAssert.DoesNotContain("closes off", Named("MapPreviewBody").GetComponent<TMP_Text>().text);
        }

        // ---- the heart -----------------------------------------------------------

        // A floor-14 realm with its party before the boss: several anchors
        // alive in their pockets.
        private IEnumerator StandBeforeTheBossOnFloor14()
        {
            RunManager.StartRun(4242);
            var run = RunManager.Run;
            run.legStartStep = 13 * RealmMap.SpineLength;
            run.step = run.legStartStep;
            run.floor = 14;
            run.clearedNodeIds.Clear();
            run.currentNodeId = RunManager.Map.Entry.Id;
            RealmPaths.StandBefore(RunManager.Map.Boss);
            yield return Show();
        }

        [UnityTest]
        public IEnumerator TheHeartStandsItsLivingAnchorsInFormationAndTheRestQueue()
        {
            yield return StandBeforeTheBossOnFloor14();

            var run = RunManager.Run;
            var living = RunEncounter.LivingAnchors(run);
            Assert.GreaterOrEqual(living.Count, 3, "fixture: floor 14 leaves several anchors alive");

            var graph = MapGraph.Of(RunManager.Map);
            for (int slot = 0; slot < MapGraph.HeartSlots; slot++)
            {
                var portrait = Named($"MapHeartPortrait{slot}");
                Assert.AreEqual(slot < living.Count, portrait.activeSelf, $"slot {slot}");
                if (slot >= living.Count) continue;

                var at = ((RectTransform)portrait.transform).anchoredPosition;
                Assert.AreEqual(graph.SlotCentre(slot).X, at.x, 0.01f, $"slot {slot} x");
                Assert.AreEqual(graph.IsFrontSlot(slot) ? 150f : 92f,
                    ((RectTransform)portrait.transform).sizeDelta.y, 0.01f, $"slot {slot} is drawn at its rank's size");
                Assert.IsNotEmpty(Named($"MapHeartName{slot}").GetComponent<TMP_Text>().text, $"slot {slot} has no name");
            }
        }

        [UnityTest]
        public IEnumerator ARootRunsFromEveryAnchorAndWithersOnceItIsSlain()
        {
            yield return StandBeforeTheBossOnFloor14();

            var map = RunManager.Map;
            var anchors = map.Nodes.Where(n => n.IsAnchor).OrderBy(n => n.Id).ToList();
            Assert.GreaterOrEqual(anchors.Count, 3);

            int ActiveRoots() => Enumerable.Range(0, MapLayout.RootSegmentCapacity)
                .Count(i => Named($"MapRoot{i}").activeSelf);

            Assert.AreEqual(anchors.Count * MapLayout.SegmentsPerLink, ActiveRoots(), "one root per anchor");
            Assert.AreEqual(Hex(MapScreen.RootLivingHex), Named("MapRoot0").GetComponent<Image>().color,
                "the first anchor stands, so its root is living");

            RunManager.Run.slainAnchorIds.Add(anchors[0].Id);
            _map.Refresh();

            Assert.AreEqual(anchors.Count * MapLayout.SegmentsPerLink, ActiveRoots(),
                "a slain anchor keeps its root, withered");
            Assert.AreEqual(Hex(MapScreen.RootWitheredHex), Named("MapRoot0").GetComponent<Image>().color,
                "the first anchor is slain, so its root has withered");
            Assert.AreEqual(Hex(MapScreen.RootLivingHex), Named("MapRoot6").GetComponent<Image>().color,
                "the next anchor still stands");

            var living = RunEncounter.LivingAnchors(RunManager.Run);
            Assert.AreEqual(anchors.Count - 1, living.Count);
            Assert.AreEqual(living.Count, Enumerable.Range(0, MapGraph.HeartSlots).Count(s => Named($"MapHeartPortrait{s}").activeSelf),
                "the slain anchor no longer stands at the heart");
        }

        // ---- the opening ---------------------------------------------------------

        [UnityTest]
        public IEnumerator ANewRealmOpensOnTheHeartAndTravelsUpToTheParty()
        {
            SharedScene.MarkDirty("loads the map with the opening shot on");
            MapController.OpeningEnabled = true;
            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _map = Object.FindAnyObjectByType<MapController>();
            var content = (RectTransform)Named("MapContent").transform;
            var viewport = (RectTransform)Named("MapViewport").transform;
            float bottom = content.sizeDelta.y - viewport.rect.height;

            Assert.IsTrue(_map.IsOpening, "a fresh realm should open with the shot");
            Assert.Greater(content.anchoredPosition.y, bottom * 0.8f, "the shot starts on the heart");

            // Skipped, the camera is where the party stands: the entry's own
            // row is the follow line, so no scroll at all.
            _map.SkipOpening();
            Assert.IsFalse(_map.IsOpening);
            Assert.AreEqual(0f, content.anchoredPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator TheOpeningFinishesOnItsOwnWithinItsSeconds()
        {
            SharedScene.MarkDirty("loads the map with the opening shot on");
            MapController.OpeningEnabled = true;
            RunManager.StartRun(4242);

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;

            _map = Object.FindAnyObjectByType<MapController>();
            var content = (RectTransform)Named("MapContent").transform;

            for (float waited = 0f; waited < 4f && _map.IsOpening; waited += Time.deltaTime) yield return null;

            Assert.IsFalse(_map.IsOpening, "the opening should finish inside 2.5 seconds, 4 given");
            Assert.AreEqual(0f, content.anchoredPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator AMidRealmMapOpensWhereThePartyIs()
        {
            SharedScene.MarkDirty("loads the map with the opening shot on");
            MapController.OpeningEnabled = true;
            RunManager.StartRun(4242);
            Assert.IsTrue(RunManager.MoveTo(1));
            RunManager.ClearCurrentRoom();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _map = Object.FindAnyObjectByType<MapController>();
            Assert.IsFalse(_map.IsOpening, "only a fresh realm opens with the shot");
        }
    }
}
