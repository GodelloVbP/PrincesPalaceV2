using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The dossier's XP bar, and whether it stays on its own track.
    //
    // THE BUG THIS EXISTS FOR: the fill was driven by anchors --
    // `anchorMax.x = fraction` with sizeDelta zeroed -- which looks equivalent
    // to setting a width and is not. ANCHORS ARE RELATIVE TO THE PARENT. The
    // parent is column A, not the 226px track, so a bar meant to cross the
    // track crossed that fraction of the whole column: out through its own
    // "258 left" caption, past the column divider, and on into the loadout
    // stage beside it.
    //
    // The Options slider's fill records the identical mistake in its own
    // comment, made and fixed while this one was still live -- which is the
    // reason this is a test rather than another comment. It measures the two
    // RECTS against each other, so it holds whatever drives the fill: anchors,
    // width, or something neither of us has thought of.
    public class DossierXpBarTests
    {
        private SystemMenuController _menu;

        [UnityTest]
        public IEnumerator TheFillNeverLeavesItsTrack()
        {
            yield return OpenTheDossier();

            var track = RectNamed("DossierXpTrack");
            var fill = RectNamed("DossierXpFill");

            var trackCorners = Corners(track);
            var fillCorners = Corners(fill);

            Assert.GreaterOrEqual(fillCorners.xMin, trackCorners.xMin - 0.5f,
                "the XP fill starts left of its own track");
            Assert.LessOrEqual(fillCorners.xMax, trackCorners.xMax + 0.5f,
                $"the XP fill runs {fillCorners.xMax - trackCorners.xMax:F0}px past the right end of " +
                "its track - it is being sized against something other than the track");
        }

        // Every character, because the fill is only wrong at fractions that
        // happen to be large: a character early in a level would have looked
        // fine while one near the top of it painted across the next column.
        [UnityTest]
        public IEnumerator ItStaysOnTheTrackForEveryCharacter()
        {
            yield return OpenTheDossier();

            var next = _menu.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "DossierNextCharacter");
            Assert.IsNotNull(next, "the dossier has no next-character button");

            for (int i = 0; i < 4; i++)
            {
                var track = Corners(RectNamed("DossierXpTrack"));
                var fill = Corners(RectNamed("DossierXpFill"));

                Assert.LessOrEqual(fill.xMax, track.xMax + 0.5f,
                    $"character {i}: the XP fill runs past its track");
                Assert.LessOrEqual(fill.width, track.width + 0.5f,
                    $"character {i}: the XP fill is wider than the track it sits on");

                next.onClick.Invoke();
                yield return null;
            }
        }

        // The whole dossier fills the pane, which is what the fill bug made
        // impossible to see: a bar escaping its column was hidden among 118px
        // of dead margin on either side of a screen that did not fill its box.
        [UnityTest]
        public IEnumerator TheDossierFillsTheContentPane()
        {
            yield return OpenTheDossier();

            var root = RectNamed("DossierPanel") ?? RectNamed("CharacterDossier");
            if (root == null) Assert.Ignore("the dossier's root node was renamed; update this test");

            Assert.AreEqual(SystemMenuLayout.PanelWidth, root.rect.width, 1f,
                "the dossier is not as wide as the pane it is hosted in");
            Assert.AreEqual(SystemMenuLayout.ContentHeight, root.rect.height, 1f,
                "the dossier is not as tall as the pane it is hosted in");
        }

        // ---- fixture ------------------------------------------------------------

        private IEnumerator OpenTheDossier()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            _menu.Open();
            _menu.Select(SystemMenuTab.CharacterInventory);
            yield return null;
            yield return null;
        }

        private RectTransform RectNamed(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name) as RectTransform;

        // World-space, because the two rects have different parents and pivots
        // and comparing anchoredPosition would be comparing two coordinate
        // systems.
        private static Rect Corners(RectTransform rect)
        {
            Assert.IsNotNull(rect, "a rect this test needs was not found");

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
