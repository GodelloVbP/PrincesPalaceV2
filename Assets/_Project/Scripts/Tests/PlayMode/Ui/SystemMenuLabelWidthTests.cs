using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Does the tab bar fit the labels the game actually draws?
    //
    // THE ONE THING NOTHING CHECKED, and it was wrong for months.
    // SystemMenuTabDef.LabelWidth was measured at .14em letter-spacing in a
    // browser prototype while the emitter had no way to express letter-spacing
    // at all, so every label drew about a quarter narrower than the box built
    // for it and the selected tab's underline overhung its own word by roughly
    // 40px a side.
    //
    // THE RUNTIME NO LONGER DEPENDS ON THOSE NUMBERS. The controller measures
    // the real labels and re-lays the bar from them, so a tab can be reworded
    // freely. What the authored figures still do is give the BUILD something to
    // emit from -- the scene exists before any text does -- which is why the
    // first test below keeps them honest: UiAudit and the capacity guard both
    // run against the authored bar, and an approximation that drifts far enough
    // would audit a bar the player never sees.
    //
    // Nothing could see it. Domain has no font metrics by design, the EditMode
    // suite is Domain-only, and UiTextFitAudit only asks whether text OVERFLOWS
    // its box -- a label far narrower than its box passes that happily.
    //
    // So this is the check that closes it, and it has to be a PlayMode test in
    // a real scene: the question is what TMP draws with the font the game is
    // wearing, and that is not answerable anywhere else.
    public class SystemMenuLabelWidthTests
    {
        // The authored numbers came off a browser at .14em, which adds a
        // trailing space after the last glyph that TMP does not, and were then
        // rounded. Measured, the gap runs 3.7 to 6.1px across the five labels.
        //
        // Wide enough not to fail on that, narrow enough to catch what matters:
        // dropping the tracking moves these by 19 to 57px, and renaming a tab
        // without re-measuring moves them further.
        private const float Tolerance = 8f;

        [TearDown]
        public void Restore() => SharedScene.AfterTest();

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). Every test
        // measures built labels; the two that open the menu leave it open, so
        // it is closed again here and the next test opens it the way a fresh
        // hub does.
        private static IEnumerator TheHub()
        {
            yield return SharedScene.Ensure("Hub");

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (menu != null && menu.IsOpen) menu.Close();
        }

        [UnityTest]
        public IEnumerator EveryTabLabelIsAsWideAsTheBarWasBuiltFor()
        {
            yield return TheHub();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            foreach (var def in SystemMenuTabs.All)
            {
                var label = LabelFor(menu, def.Key);

                // THE PREMISE, PINNED FIRST. Without the tracking the widths
                // below are wrong by a quarter, and the assertion after it is
                // the only thing in the build that would notice.
                Assert.AreEqual(SystemMenuLayout.TabLabelTracking, label.characterSpacing, 0.01f,
                    $"the {def.Key} tab is not tracked at the spacing its authored width was " +
                    "measured at, so every box, gap and underline on this bar is laid out around " +
                    "a number nothing draws");

                float drawn = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x;

                Assert.AreEqual(def.LabelWidth, drawn, Tolerance,
                    $"the {def.Key} tab claims a label {def.LabelWidth:F0}px wide and TMP draws it " +
                    $"{drawn:F1}px. The bar's boxes, gaps, dividers and underline are all arithmetic " +
                    "over the claim, so re-measure it rather than nudging the layout.");
            }
        }

        // The lintel's title carries the design's other tracking value, and it
        // has no width table to check against -- so this checks the one thing
        // that can go wrong silently: that it is applied at all.
        [UnityTest]
        public IEnumerator TheLintelTitleCarriesItsOwnTracking()
        {
            yield return TheHub();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            var title = menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == "SystemLintelTitle")?.gameObject
                .GetComponent<TMP_Text>();

            Assert.IsNotNull(title, "the lintel has no title label");
            Assert.AreEqual(SystemMenuLayout.LintelTitleTracking, title.characterSpacing, 0.01f,
                "the run title is not tracked at the .22em the design sets for it");
        }

        // Nothing else in the game asks for tracking, and the default has to
        // stay zero -- applying it everywhere would move text on every screen
        // to fix one bar. This is what says the property is opt-in rather than
        // something that leaked into the emitter's defaults.
        [UnityTest]
        public IEnumerator OrdinaryLabelsAreNotTracked()
        {
            yield return TheHub();

            // NOT HubTitleLabel any more: the button-theme migration gave it
            // TypographyRole.CeremonialTitle (the brief's own "Hub screen
            // title" carve-out), which authors 2em of tracking on purpose --
            // asserting 0 on it now would be pinning the bug this test exists
            // to catch. A building nameplate never asked for a role at all,
            // so it is still the untouched control this check wants.
            var buildingCaption = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.name == "RelicsBuildingCaption");

            Assert.IsNotNull(buildingCaption, "the hub has no RelicsBuilding nameplate");
            Assert.AreEqual(0f, buildingCaption.characterSpacing, 0.01f,
                "tracking has leaked into labels that never asked for it");
        }

        // THE BAR LAYS OUT AROUND WHAT IS DRAWN, which is the property that
        // replaces "the authored number happens to be right".
        //
        // The authored widths are still there -- the scene has to be emitted
        // before any text exists to measure -- but they are now a build-time
        // approximation the controller corrects on first open. So this asserts
        // the thing that actually matters: each tab's BOX is its own label plus
        // the declared padding, whatever the label turns out to be.
        [UnityTest]
        public IEnumerator EachTabsBoxIsBuiltRoundItsOwnDrawnLabel()
        {
            yield return TheHub();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            menu.Open();
            yield return null;
            yield return null;

            var shown = menu.VisibleTabs;
            CollectionAssert.IsNotEmpty(shown, "the bar shows no tabs");

            foreach (int index in shown)
            {
                var def = SystemMenuTabs.All[index];
                var label = LabelFor(menu, def.Key);
                var box = (RectTransform)label.transform.parent;

                float drawn = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x;

                // Mode A shares the row out evenly and does not size a box to
                // its label at all, so the check that means something in both
                // modes is that the label FITS with its padding intact.
                Assert.GreaterOrEqual(box.rect.width, drawn + SystemMenuLayout.MeasuredPadX,
                    $"the {def.Key} tab's box is {box.rect.width:F0}px around a label that draws " +
                    $"{drawn:F1}px - the bar is laid out for a label other than the one on it");
            }
        }

        // And the rule under the selected tab is the width of the WORD, not of
        // the box. It kept its authored width before, so a mis-measured label
        // showed up as a rule overhanging its own word by 40px a side.
        [UnityTest]
        public IEnumerator TheUnderlineIsTheWidthOfTheWordItMarks()
        {
            yield return TheHub();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            menu.Open();
            menu.Select(SystemMenuTab.Options);
            yield return null;
            yield return null;

            var label = LabelFor(menu, "Options");
            float drawn = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x;

            var underline = menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == "SystemTabOptionsUnderline") as RectTransform;

            Assert.IsNotNull(underline, "the Options tab has no underline");
            Assert.AreEqual(SystemMenuLayout.UnderlineWidth(drawn), underline.rect.width, 1.5f,
                $"the underline is {underline.rect.width:F0}px under a word that draws {drawn:F1}px");
        }

        private static TMP_Text LabelFor(SystemMenuController menu, string key)
        {
            var go = menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == $"SystemTab{key}")?.gameObject;
            Assert.IsNotNull(go, $"the bar has no tab named SystemTab{key}");

            // The label is a child of the button, the same way UiEmitResult.Tmp
            // looks for it.
            var label = go.GetComponentInChildren<TMP_Text>(includeInactive: true);
            Assert.IsNotNull(label, $"SystemTab{key} carries no text");
            return label;
        }
    }
}
