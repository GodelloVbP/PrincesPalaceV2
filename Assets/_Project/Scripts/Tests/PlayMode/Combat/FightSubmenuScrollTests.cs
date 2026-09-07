using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The submenu's SCROLL, driven the way a player drives it.
    //
    // FightSubmenuLayoutTests already pins the arithmetic and passes; every
    // number it checks was right while the bar was still unusable in play.
    // What it cannot see is the controller throwing the scroll away, because
    // the reset lives in the view and the reset is the whole bug: the list is
    // re-anchored on every repaint, and a repaint happens on every hover.
    //
    // Driven through the ITEM branch rather than the SKILL one because a
    // twelve-entry satchel is one constructor call, while a twelve-skill actor
    // needs a levelled character and a talent root behind it. The two branches
    // are the same rows, the same rects and the same scroll -- CurrentRows is
    // the only thing that differs.
    public class FightSubmenuScrollTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void RestoreBeatSpeed() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private RectTransform Rect(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the Fight scene");
            return (RectTransform)go.transform;
        }

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        // Twelve, against a window that shows eight: four rows of travel, which
        // is enough that a row at the bottom of the list is unambiguously
        // outside the window at rest.
        private const int SatchelSize = 12;

        private IEnumerator LoadFightWithALongList()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var kit = PlayModeSparkFixture.Kit();
            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, enemyKits, new SeededRandom(9));
            session.Begin();

            var satchel = new List<SatchelStack>();
            for (int i = 0; i < SatchelSize; i++)
            {
                satchel.Add(new SatchelStack($"potion_{i}", $"Potion {i}", 3, false));
            }

            _fight.Bind(session, EncounterClass.Normal, satchel);
            yield return null;

            // Verb2 is ITEM -- OnVerbPressed's own switch: 0 ATTACK, 1 SKILL,
            // 2 ITEM, 3 MOVE.
            Click("Verb2");
            yield return null;

            Assert.IsTrue(Named("SubmenuColumn").activeSelf, "the item list did not open");
            Assert.IsTrue(Named("SubmenuScrollThumb").activeSelf,
                "a twelve-row list must show a bar -- the fixture is not exercising the scroll at all");
        }

        // Is `row` wholly inside the window that clips it?
        //
        // WORLD SPACE, for the reason FightFlowTests gives at its own
        // measurement: a row's anchoredPosition is its fixed slot in the pool
        // and means nothing on its own. Where it lands on screen relative to
        // the viewport is the claim, and that survives however many rects are
        // nested between the two.
        private static bool InsideWindow(RectTransform row, RectTransform viewport)
        {
            float rowHalf = row.rect.height * 0.5f * row.lossyScale.y;
            float windowHalf = viewport.rect.height * 0.5f * viewport.lossyScale.y;

            return row.position.y - rowHalf >= viewport.position.y - windowHalf - 0.5f
                   && row.position.y + rowHalf <= viewport.position.y + windowHalf + 0.5f;
        }

        // THE BUG, stated as the player meets it: scroll the list, then do
        // anything at all, and the list is back at the top.
        //
        // "Anything at all" is not an exaggeration. RefreshSubmenu re-anchors
        // the list on every RefreshUi, and RefreshUi runs when a row is
        // hovered, when an enemy plate is hovered, when a beat lands. Scrolling
        // with the wheel moves the rows UNDER a stationary pointer, so the
        // notch itself changes which row is hovered and springs the list back
        // -- which is why the bar reads as dead rather than as jumpy.
        [UnityTest]
        public IEnumerator ARepaintDoesNotThrowTheScrolledListBackToTheTop()
        {
            yield return LoadFightWithALongList();

            var content = Rect("SubmenuContent");
            var thumb = Rect("SubmenuScrollThumb");

            float restingContent = content.anchoredPosition.y;
            float restingThumb = thumb.anchoredPosition.y;

            var wheel = Rect("SubmenuViewport").GetComponent<ListScroll>();
            Assert.IsNotNull(wheel, "nothing is listening for a wheel over the list");
            wheel.Scrolled(FightSubmenuLayout.RowPitch * 2f);
            yield return null;

            float scrolledContent = content.anchoredPosition.y;
            Assert.AreNotEqual(restingContent, scrolledContent,
                "two notches of wheel moved nothing -- the list never scrolled in the first place");
            Assert.AreNotEqual(restingThumb, thumb.anchoredPosition.y, "the thumb did not follow the list");

            // A repaint with nothing about the list changed: same branch, same
            // twelve rows, same everything. This is what a hover costs.
            _fight.RefreshUi();
            yield return null;

            Assert.AreEqual(scrolledContent, content.anchoredPosition.y, 0.01f,
                "a repaint threw the list back to the top of the list");
            Assert.AreEqual(FightSubmenuLayout.ThumbCentreY(SatchelSize, FightSubmenuLayout.RowPitch * 2f),
                thumb.anchoredPosition.y, 0.01f,
                "the bar snapped back with it");
        }

        // Opening a list is still the moment it starts at the top. The fix for
        // the above must not turn into a menu that remembers where the LAST
        // list was left.
        [UnityTest]
        public IEnumerator ReopeningTheListStartsItAtTheTopAgain()
        {
            yield return LoadFightWithALongList();

            var content = Rect("SubmenuContent");
            float top = content.anchoredPosition.y;

            Rect("SubmenuViewport").GetComponent<ListScroll>().Scrolled(FightSubmenuLayout.RowPitch * 2f);
            yield return null;
            Assert.AreNotEqual(top, content.anchoredPosition.y);

            Click("SubmenuBack");
            yield return null;
            Click("Verb2");
            yield return null;

            Assert.AreEqual(top, content.anchoredPosition.y, 0.01f,
                "a freshly opened list must show its first row, not wherever it was left");
        }

        // The other half of the same defect: the selection can walk off the
        // window and nothing follows it. Arrowing down a twelve-row list moves
        // the highlight onto rows the viewport is clipping, so the player is
        // navigating a list they cannot see.
        [UnityTest]
        public IEnumerator ArrowingPastTheWindowScrollsTheSelectedRowIntoIt()
        {
            yield return LoadFightWithALongList();

            var viewport = Rect("SubmenuViewport");

            // Selection opens at -1, so the first step lands on row 0 and the
            // twelfth on row 11 -- the last entry in the satchel.
            for (int i = 0; i < SatchelSize; i++)
            {
                _fight.MoveFocus(1);
                yield return null;
            }

            Assert.IsTrue(InsideWindow(Rect($"CharacterSkill{SatchelSize - 1}"), viewport),
                "the last row is selected and sitting outside the window that clips it");

            // And back up the other way: row 0 has to come back into view.
            for (int i = 0; i < SatchelSize - 1; i++)
            {
                _fight.MoveFocus(-1);
                yield return null;
            }

            Assert.IsTrue(InsideWindow(Rect("CharacterSkill0"), viewport),
                "arrowing back to the top of the list left the window at the bottom");
        }

        // The same claim as ArrowingPastTheWindowScrollsTheSelectedRowIntoIt,
        // pinned against the CONTENT RECT rather than only against the row's
        // world position -- both have to move, or a viewport that happened to
        // be tall enough to already contain row 11 would pass the other test
        // for the wrong reason.
        [UnityTest]
        public IEnumerator ScrollingSelectionToRowElevenMovesTheContentAndKeepsItInView()
        {
            yield return LoadFightWithALongList();

            var content = Rect("SubmenuContent");
            var viewport = Rect("SubmenuViewport");
            float resting = content.anchoredPosition.y;

            for (int i = 0; i < SatchelSize; i++)
            {
                _fight.MoveFocus(1);
                yield return null;
            }

            Assert.AreNotEqual(resting, content.anchoredPosition.y,
                "the content rect never moved -- selecting row 11 did not scroll the list");
            Assert.IsTrue(InsideWindow(Rect($"CharacterSkill{SatchelSize - 1}"), viewport),
                "row 11 is selected but sits outside the viewport that clips it");
        }

        // A list that FITS must not have gained a scroll it never had. Eight
        // rows against an eight-row window: no bar, no travel, nothing moves.
        [UnityTest]
        public IEnumerator AShortListStillShowsNoBarAndDoesNotMove()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var kit = PlayModeSparkFixture.Kit();
            var enemyKits = foes.Select(f => new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false)).ToList();
            var session = new FightSession(new CombatEncounter(new[] { hero }, foes),
                new List<PlayerKit> { kit }, enemyKits, new SeededRandom(9));
            session.Begin();

            var satchel = new List<SatchelStack>();
            for (int i = 0; i < FightSubmenuLayout.RowsInView; i++)
            {
                satchel.Add(new SatchelStack($"potion_{i}", $"Potion {i}", 1, false));
            }

            _fight.Bind(session, EncounterClass.Normal, satchel);
            yield return null;
            Click("Verb2");
            yield return null;

            Assert.IsFalse(Named("SubmenuScrollThumb").activeSelf,
                "a list that fits must show no bar");
            Assert.IsFalse(Named("SubmenuScrollTrack").activeSelf);

            var content = Rect("SubmenuContent");
            float resting = content.anchoredPosition.y;

            _fight.MoveFocus(1);
            _fight.MoveFocus(1);
            _fight.RefreshUi();
            yield return null;

            Assert.AreEqual(resting, content.anchoredPosition.y, 0.01f,
                "a list with nowhere to go moved anyway");
        }

        // ---- the real input path, not the seam --------------------------------
        //
        // Every test above drives ListScroll.Scrolled directly -- proof the
        // ARITHMETIC is right, and exactly the shape that let 9a208f3 land
        // unable to reproduce a real player's report. What none of them
        // exercise is EventSystem/GraphicRaycaster actually finding a row
        // under the pointer and bubbling the wheel up to whatever object owns
        // ListScroll -- which is the one thing a mouse in the real game does
        // that a direct method call cannot.
        // The NEAREST canvas above `from`, NOT the root one -- FightScreen
        // wraps its whole HUD (submenu included) in "FightHud", a nested
        // Canvas built by Ui.NestedCanvas (see BuildSubmenuColumn's own
        // tree and EmitNestedCanvas). UiEmitter
        // gives every NestedCanvas its OWN GraphicRaycaster (EmitNestedCanvas,
        // line ~573) precisely because Unity's GraphicRegistry books a
        // raycastable Graphic against its NEAREST enclosing Canvas, not the
        // outermost one -- the outer "Canvas"'s own GraphicRaycaster never
        // sees a row's Plate at all. Reaching for the root here, the more
        // "obvious" choice, is exactly the wrong one and returns zero hits
        // for everything under the HUD.
        private static (GraphicRaycaster raycaster, Camera camera) FindRaycastSurface(Transform from)
        {
            var canvas = from.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, "fixture: the node under test is not inside any Canvas");
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(raycaster,
                $"fixture: '{canvas.name}', the nearest Canvas above '{from.name}', has no GraphicRaycaster of its own");
            return (raycaster, canvas.worldCamera);
        }

        private static GameObject TopmostHitAt(GraphicRaycaster raycaster, Camera camera, Vector2 worldPosition,
            out PointerEventData eventData)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
            eventData = new PointerEventData(EventSystem.current) { position = screenPoint };

            var hits = new List<RaycastResult>();
            raycaster.Raycast(eventData, hits);

            Assert.IsNotEmpty(hits, $"nothing raycast-hittable sits at {worldPosition} -- the point the test " +
                                     "aimed at is not reachable by a real pointer at all");

            // pointerPressRaycast, not a bare position, matters for what
            // comes next -- ListScroll's own track-grab path (Seek) converts
            // eventData.position back to a local point via
            // RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
            // position, eventData.pressEventCamera, ...), and pressEventCamera
            // is READ from this raycast result's module.eventCamera. Left
            // unset, that conversion reads the point as Overlay space, which
            // is wrong for this canvas's ScreenSpaceCamera mode -- exactly
            // what a real EventSystem always sets before either handler runs.
            eventData.pointerPressRaycast = hits[0];
            return hits[0].gameObject;
        }

        // THE ACTUAL BUG REPORT, reproduced as closely as a test can: point at
        // a ROW (not at the viewport's own transparent catcher, not at
        // ListScroll's GameObject directly) and turn the wheel.
        [UnityTest]
        public IEnumerator WheelOverARowsPlateBubblesUpToTheRealListScroll()
        {
            yield return LoadFightWithALongList();

            var content = Rect("SubmenuContent");
            var thumb = Rect("SubmenuScrollThumb");

            // Row 3: comfortably inside the resting window (RowsInView is 8,
            // per AShortListStillShowsNoBarAndDoesNotMove above), so this hits
            // a real, currently-visible row rather than one already clipped.
            var row = Named("CharacterSkill3");
            Assert.IsNotNull(row, "fixture: no CharacterSkill3 row in the pool");

            // THE PLATE, specifically -- "the themed row's own raycast-
            // receiving Image" the brief names, and the SAME Image the row's
            // own Button uses as its targetGraphic (see UiEmitter's own
            // comment on WireThemedButton), so this is provably the surface a
            // real click on the row also lands on.
            var plate = row.GetComponent<Button>()?.targetGraphic;
            Assert.IsNotNull(plate, "fixture: the row's Button has no targetGraphic (no Plate wired)");

            var (raycaster, camera) = FindRaycastSurface(plate.transform);
            var hit = TopmostHitAt(raycaster, camera, plate.transform.position, out var eventData);

            float restingContent = content.anchoredPosition.y;
            float restingThumb = thumb.anchoredPosition.y;

            // NEGATIVE, per ListScroll.OnScroll's own sign convention: it
            // negates scrollDelta.y before handing pixels to Scrolled, so a
            // notch that moves the list DOWN (the direction every other list
            // here scrolls a wheel notch) is asked for with a NEGATIVE
            // eventData.scrollDelta.y here.
            eventData.scrollDelta = new Vector2(0f, -1f);
            ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.scrollHandler);
            yield return null;

            Assert.AreNotEqual(restingContent, content.anchoredPosition.y,
                "a wheel notch over the row's own Plate, raycast through the real GraphicRaycaster and " +
                "bubbled via ExecuteEvents, moved nothing -- ListScroll is not reachable from where a " +
                "player's pointer actually lands");
            Assert.AreNotEqual(restingThumb, thumb.anchoredPosition.y, "the thumb did not follow the real scroll");
        }

        // THE SCROLLBAR'S OWN SURFACE. The thumb itself is decorative
        // (AsDecor, no raycast target -- see BuildSubmenuFrame's own
        // comment); the TRACK is what WireSubmenuScroll re-enables
        // raycastTarget on and wires Seeked to, so a real grab-and-drag lands
        // on the track underneath the thumb, not the thumb's own pixels.
        [UnityTest]
        public IEnumerator DraggingTheTrackSeeksTheRealList()
        {
            yield return LoadFightWithALongList();

            var track = Rect("SubmenuScrollTrack");
            var content = Rect("SubmenuContent");
            float restingContent = content.anchoredPosition.y;

            var (raycaster, camera) = FindRaycastSurface(track);

            // The BOTTOM of the track, so a seek there means "show me the end
            // of the list" -- unambiguous against the resting (top) position.
            var bottomOfTrack = track.position - new Vector3(0f, track.rect.height * 0.5f * track.lossyScale.y, 0f);
            var hit = TopmostHitAt(raycaster, camera, bottomOfTrack, out var eventData);

            Assert.AreEqual("SubmenuScrollTrack", hit.name,
                "a pointer at the bottom of the scrollbar's track did not land on the track itself -- " +
                "something else (the thumb, the frame) is intercepting the raycast there");

            ExecuteEvents.Execute(hit, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;

            Assert.AreNotEqual(restingContent, content.anchoredPosition.y,
                "a real click at the bottom of the track, through GraphicRaycaster, did not seek the list");

            // A click at the VERY bottom of the track asks for fromTop == 1,
            // which is the list's maximum scroll -- pinned against
            // FightSubmenuLayout's own arithmetic rather than eyeballed, the
            // same way ScrollingSelectionToRowElevenMovesTheContentAndKeeps
            // ItInView above pins ThumbCentreY.
            float expectedScroll = FightSubmenuLayout.ScrollAt(SatchelSize, 1f);
            float expectedContentY = FightSubmenuLayout.ContentY(SatchelSize, expectedScroll);
            Assert.AreEqual(expectedContentY, content.anchoredPosition.y, 1f,
                "the track seek landed somewhere other than the bottom of the list");
        }
    }
}
