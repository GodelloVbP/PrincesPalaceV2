using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The overarching menu's behaviour: it opens, it closes, and exactly one
    // tab is selected at a time.
    //
    // The last of those is the one that matters for a skeleton whose panes are
    // empty: a pane and its underline are two halves of "which tab am I on",
    // and they can disagree without anything looking broken until a designer
    // fills the panes and wonders why the highlight lies.
    public class SystemMenuTests
    {
        private SystemMenuController _menu;

        private IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");
        }

        private GameObject[] Panes() =>
            (GameObject[])typeof(SystemMenuController)
                .GetField("panes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_menu);

        private GameObject[] Underlines() =>
            (GameObject[])typeof(SystemMenuController)
                .GetField("tabUnderlines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_menu);

        [UnityTest]
        public IEnumerator ItStartsClosedAndOpens()
        {
            yield return OpenTheHub();

            Assert.IsFalse(_menu.IsOpen, "the overarching menu is open on load");

            _menu.Open();
            yield return null;
            Assert.IsTrue(_menu.IsOpen);

            _menu.Close();
            yield return null;
            Assert.IsFalse(_menu.IsOpen);
        }

        [UnityTest]
        public IEnumerator EveryTabIsWiredToItsOwnPaneAndUnderline()
        {
            yield return OpenTheHub();
            _menu.Open();

            // A frame, so Start() has run and cannot come along afterwards and
            // reset the selection behind a later assertion.
            yield return null;

            var panes = Panes();
            var underlines = Underlines();

            Assert.AreEqual(SystemMenuTabs.Count, panes.Length, "a tab has no pane");
            Assert.AreEqual(SystemMenuTabs.Count, underlines.Length, "a tab has no underline");

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                _menu.Select(i);
                yield return null;

                string key = SystemMenuTabs.All[i].Key;

                var livePanes = panes.Where(p => p.activeSelf).Select(p => p.name).ToList();
                CollectionAssert.AreEqual(new[] { $"SystemPane{key}" }, livePanes,
                    $"selecting tab {i} ({key}) should leave exactly its own pane showing");

                var liveMarks = underlines.Where(u => u.activeSelf).Select(u => u.name).ToList();
                CollectionAssert.AreEqual(new[] { $"SystemTab{key}Underline" }, liveMarks,
                    $"selecting tab {i} ({key}) highlighted a different tab than the pane it opened - " +
                    "the underline and the pane disagree about which tab is selected");
            }
        }

        // Start() runs a frame after the object is activated, so its default
        // must not overwrite a tab the opener already picked. The first
        // screenshot of this menu was exactly that: Options showing, Character
        // underlined, because the default landed second.
        [UnityTest]
        public IEnumerator OpeningStraightOntoATabIsNotResetByTheDefault()
        {
            yield return OpenTheHub();

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);

            // The frame on which Start() lands.
            yield return null;
            yield return null;

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.Options), _menu.SelectedIndex,
                "the default selection overwrote a tab the caller had already chosen");
        }

        [UnityTest]
        public IEnumerator ClickingATabSelectsIt()
        {
            yield return OpenTheHub();
            _menu.Open();
            yield return null;

            var button = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "SystemTabOptions");
            Assert.IsNotNull(button, "the Options tab has no button");

            button.onClick.Invoke();
            yield return null;

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.Options), _menu.SelectedIndex);
        }
    }
}
