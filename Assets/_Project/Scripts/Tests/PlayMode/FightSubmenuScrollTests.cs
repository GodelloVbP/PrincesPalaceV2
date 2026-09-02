using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
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
            // 2 ITEM, 3 HOLD BACK.
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
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
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
    }
}
