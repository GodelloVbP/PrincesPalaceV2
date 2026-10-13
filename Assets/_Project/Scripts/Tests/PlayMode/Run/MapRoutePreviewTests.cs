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
    // What the map tells a player choosing a route (AUDIT #226, #254): the card
    // for a hovered or focused room, the cue only where a press works, the
    // reason a refused press gives, and the book nudge.
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

        private static int IndexOf(RealmNode node) => MapColumns.Of(RunManager.Map).IndexOf(node.Id);

        private GameObject NodeObject(RealmNode node) => Named($"MapNode{IndexOf(node)}");

        private TMP_Text CaptionOf(RealmNode node) => Named($"MapNode{IndexOf(node)}Name").GetComponent<TMP_Text>();

        private static void Pointer(GameObject go, bool enter)
        {
            var data = new PointerEventData(EventSystem.current);
            if (enter) ExecuteEvents.Execute(go, data, ExecuteEvents.pointerEnterHandler);
            else ExecuteEvents.Execute(go, data, ExecuteEvents.pointerExitHandler);
        }

        private static void Click(GameObject go) =>
            ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var colour);
            return colour;
        }

        private static readonly Dictionary<RoomType, string> TitleByType = new Dictionary<RoomType, string>
        {
            { RoomType.Entry, "START" }, { RoomType.Fight, "FIGHT" }, { RoomType.EliteFight, "ELITE" },
            { RoomType.Boss, "BOSS" }, { RoomType.Treasure, "TREASURE" }, { RoomType.Rest, "REST" },
            { RoomType.Shop, "SHOP" }, { RoomType.Event, "EVENT" },
        };

        private static readonly Dictionary<RoomType, string> BodyStartByType = new Dictionary<RoomType, string>
        {
            { RoomType.Entry, "Where this stretch" }, { RoomType.Fight, "Enemies: " },
            { RoomType.EliteFight, "Enemies: " }, { RoomType.Boss, "Enemies: " },
            { RoomType.Treasure, "A stash of " }, { RoomType.Rest, "The squad rests" },
            { RoomType.Shop, "A trader." }, { RoomType.Event, "Something waits here." },
        };

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
                    RoomResolver.Resolve(run, RoomType.Treasure);

                    Assert.AreEqual(RoomResolver.Last.Gold, facts.StashGold, $"seed {seed} node {node.Id}");
                    Assert.That(facts.StashGold, Is.InRange(15, 30));
                    RoomResolver.Reset();
                    found++;
                }
            }

            Assert.GreaterOrEqual(found, 1, "no seed produced a treasure room to compare");
        }

        // ---- the card ------------------------------------------------------------

        [UnityTest]
        public IEnumerator HoveringAnyRoomShowsItsCardAndLeavingHidesIt()
        {
            var seen = new HashSet<RoomType>();

            foreach (ulong seed in new ulong[] { 4242, 7, 99 })
            {
                yield return OpenTheMap(seed);
                var panel = Named("MapPreviewPanel");
                Assert.IsFalse(panel.activeSelf, "the card should start hidden");

                foreach (var node in RunManager.Map.Nodes)
                {
                    var go = NodeObject(node);
                    Pointer(go, true);
                    yield return null;

                    Assert.IsTrue(panel.activeSelf, $"no card for {node.Type} (seed {seed})");
                    StringAssert.StartsWith(TitleByType[node.Type], Named("MapPreviewTitle").GetComponent<TMP_Text>().text);
                    StringAssert.StartsWith(BodyStartByType[node.Type],
                        Named("MapPreviewBody").GetComponent<TMP_Text>().text, $"{node.Type} (seed {seed})");
                    seen.Add(node.Type);

                    Pointer(go, false);
                    yield return null;
                    Assert.IsFalse(panel.activeSelf, $"the card stayed up after leaving {node.Type}");
                }
            }

            Assert.GreaterOrEqual(seen.Count, 5, "three legs covered too few room types to trust this");
            CollectionAssert.Contains(seen, RoomType.Entry);
            CollectionAssert.Contains(seen, RoomType.Boss);
        }

        [UnityTest]
        public IEnumerator PadFocusShowsTheSameCardAsThePointer()
        {
            yield return OpenTheMap();

            var choice = RunManager.Choices().First();
            var go = NodeObject(choice);
            var panel = Named("MapPreviewPanel");

            ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            yield return null;

            Assert.IsTrue(panel.activeSelf, "focusing a room showed no card");
            StringAssert.StartsWith(TitleByType[choice.Type], Named("MapPreviewTitle").GetComponent<TMP_Text>().text);

            ExecuteEvents.Execute(go, new BaseEventData(EventSystem.current), ExecuteEvents.deselectHandler);
            yield return null;
            Assert.IsFalse(panel.activeSelf, "losing focus left the card up");
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

        // ---- the refusal ---------------------------------------------------------

        [UnityTest]
        public IEnumerator PressingARoomTooFarAheadSaysSoOnItsCaptionThenRestoresIt()
        {
            yield return OpenTheMap();

            var far = RunManager.Map.Nodes.First(n => n.Offset >= 2);
            var caption = CaptionOf(far);
            string before = caption.text;
            Assert.AreEqual(TitleByType[far.Type], before, "fixture: the caption should be the room's name");

            Click(NodeObject(far));
            yield return null;

            Assert.AreEqual("TOO FAR AHEAD", caption.text);
            Assert.That(caption.color.r, Is.EqualTo(Hex("#E0786E").r).Within(0.01f));
            Assert.That(caption.color.g, Is.EqualTo(Hex("#E0786E").g).Within(0.01f));
            Assert.IsFalse(_map.IsWalking, "a refused press must not start a walk");

            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(before, caption.text, "the caption did not come back");
            Assert.That(caption.color.g, Is.EqualTo(Hex("#EDE6FF").g).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator PressingThePartysOwnRoomSaysYouAreHere()
        {
            yield return OpenTheMap();

            var caption = CaptionOf(RunManager.CurrentNode);
            Click(NodeObject(RunManager.CurrentNode));
            yield return null;

            Assert.AreEqual("YOU ARE HERE", caption.text);
        }

        [UnityTest]
        public IEnumerator AForcedPressOnAnUnreachableRoomIsRefusedToo()
        {
            // onClick.Invoke bypasses interactable, the route a script or a
            // stray submit would take into the legality guard.
            yield return OpenTheMap();

            var far = RunManager.Map.Nodes.First(n => n.Offset >= 2);
            NodeObject(far).GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual("TOO FAR AHEAD", CaptionOf(far).text);
            Assert.IsFalse(_map.IsWalking);
        }

        [UnityTest]
        public IEnumerator ASecondPressMidWalkIsAnsweredOnTheCaption()
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

            button.onClick.Invoke();
            yield return null;
            Assert.AreEqual("ON THE MOVE", CaptionOf(quiet).text);

            for (float waited = 0f; waited < 5f && _map.IsWalking; waited += Time.deltaTime) yield return null;
            Assert.IsFalse(_map.IsWalking, "the walk should have finished");
        }

        // ---- the book nudge ------------------------------------------------------

        [UnityTest]
        public IEnumerator TheNudgeIsABigBadgeShownExactlyWhenSomethingWaits()
        {
            yield return OpenTheMap();

            var badge = Named("MapAttentionBadge");
            var label = Named("MapAttentionLabel").GetComponent<TMP_Text>();
            var bear = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == "bear");

            bear.unspentStatPoints = 0;
            _map.Refresh();
            Assert.IsFalse(badge.activeSelf, "the badge shows with nothing waiting");
            Assert.IsFalse(label.gameObject.activeSelf);

            bear.unspentStatPoints = 2;
            _map.Refresh();
            Assert.IsTrue(badge.activeSelf, "the badge is missing with points to spend");
            Assert.IsTrue(label.gameObject.activeSelf);
            Assert.GreaterOrEqual(label.fontSize, 26f, "the nudge is back to a small line");

            var size = ((RectTransform)badge.transform).rect.size;
            Assert.GreaterOrEqual(size.x, 700f);
            Assert.GreaterOrEqual(size.y, 80f);
        }
    }
}
