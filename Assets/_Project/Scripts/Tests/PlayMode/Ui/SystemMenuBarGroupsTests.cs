using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The tab bar as a player sees it: two groups on one row (spec section 2).
    // The Domain arithmetic is pinned in SystemMenuScreenTests from literal
    // widths; this holds the running scene to the same rules with whatever TMP
    // measured, so it states the rules as numbers rather than recomputing them.
    public class SystemMenuBarGroupsTests
    {
        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            Time.timeScale = 1f;
        }

        private static IEnumerator Open(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static SystemMenuController Menu() =>
            Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);

        private static Transform Tab(SystemMenuController menu, string key) =>
            menu.GetComponentsInChildren<Transform>(includeInactive: true).First(t => t.name == $"SystemTab{key}");

        private static TMP_Text LabelOf(SystemMenuController menu, string key) =>
            Tab(menu, key).GetComponentInChildren<TMP_Text>(includeInactive: true);

        // Left and right edge of each visible tab's box, in panel space.
        private static (float Left, float Right)[] Boxes(SystemMenuController menu)
        {
            return menu.VisibleTabs
                .Select(i =>
                {
                    var rect = (RectTransform)Tab(menu, SystemMenuTabs.All[i].Key);
                    float centre = rect.anchoredPosition.x + SystemMenuLayout.HalfWidth;
                    return (centre - rect.sizeDelta.x * 0.5f, centre + rect.sizeDelta.x * 0.5f);
                })
                .ToArray();
        }

        [UnityTest]
        public IEnumerator TheCampBarPacksTheGameGroupLeftAndTheSystemGroupFlushRight()
        {
            yield return Open("Hub");
            var menu = Menu();
            menu.Open();
            yield return null;

            var boxes = Boxes(menu);
            Assert.AreEqual(4, boxes.Length, "the camp bar should have four tabs");

            Assert.AreEqual(40f, boxes[0].Left, 0.5f, "the game group does not start at the left inset");
            Assert.AreEqual(1560f, boxes[3].Right, 0.5f, "the system group does not end flush at the right inset");

            Assert.AreEqual(48f, boxes[1].Left - boxes[0].Right, 0.5f, "Character and Party are not 48px apart");
            Assert.AreEqual(48f, boxes[3].Left - boxes[2].Right, 0.5f, "Options and Exit are not 48px apart");
            Assert.GreaterOrEqual(boxes[2].Left - boxes[1].Right, 96f,
                "the gap between the groups is under its 96px minimum");
        }

        [UnityTest]
        public IEnumerator TheDescentBarKeepsTheSameRulesWithTheRunTabAdded()
        {
            RunManager.StartRun(4242);
            yield return Open("Map");
            var menu = Menu();
            menu.Open();
            yield return null;

            var boxes = Boxes(menu);
            Assert.AreEqual(5, boxes.Length, "the descent bar should have five tabs");

            Assert.AreEqual(40f, boxes[0].Left, 0.5f);
            Assert.AreEqual(1560f, boxes[4].Right, 0.5f);
            Assert.AreEqual(48f, boxes[1].Left - boxes[0].Right, 0.5f);
            Assert.AreEqual(48f, boxes[2].Left - boxes[1].Right, 0.5f);
            Assert.AreEqual(48f, boxes[4].Left - boxes[3].Right, 0.5f);
            Assert.GreaterOrEqual(boxes[3].Left - boxes[2].Right, 96f);
        }

        [UnityTest]
        public IEnumerator EveryVisibleLabelIsItsFullFormWhenTheFullSetFits()
        {
            yield return Open("Hub");
            var menu = Menu();
            menu.Open();
            yield return null;

            foreach (int index in menu.VisibleTabs)
            {
                var def = SystemMenuTabs.All[index];
                var label = LabelOf(menu, def.Key);
                Assert.AreEqual(def.Label.Template, label.text,
                    $"{def.Key} is not showing its full label although the full set fits");
            }
        }

        // The system group is quiet until selected: TextMuted, then GoldLight.
        [UnityTest]
        public IEnumerator SystemTabsAreMutedUntilSelectedAndThenGold()
        {
            yield return Open("Hub");
            var menu = Menu();
            menu.Open();
            yield return null;

            var options = LabelOf(menu, "Options");

            AssertColour("#8A7AA0", options.color, "an unselected system tab should be TextMuted");

            menu.Select(SystemMenuTab.Options);
            yield return null;

            AssertColour("#FFE0A8", options.color, "the selected system tab should be GoldLight");

            menu.Select(SystemMenuTab.Character);
            yield return null;

            AssertColour("#8A7AA0", options.color, "leaving the system tab should quiet it again");
        }

        private static void AssertColour(string hex, Color actual, string message)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var expected));
            Assert.AreEqual(expected.r, actual.r, 0.01f, message);
            Assert.AreEqual(expected.g, actual.g, 0.01f, message);
            Assert.AreEqual(expected.b, actual.b, 0.01f, message);
        }
    }
}
