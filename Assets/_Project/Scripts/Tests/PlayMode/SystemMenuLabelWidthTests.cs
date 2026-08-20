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
    // Do the tab bar's authored label widths describe the labels the game
    // actually draws?
    //
    // THE ONE THING NOTHING CHECKED. SystemMenuTabDef.LabelWidth carries the
    // design's measurements, and the entire bar -- box widths, gaps, dividers,
    // underline lengths, the capacity guard -- is arithmetic over them. Its own
    // header called the numbers a liability, on the grounds that a label could
    // be changed without its width. The liability was worse than that: they
    // were measured at .14em letter-spacing in a browser prototype and the
    // emitter had no way to express letter-spacing at all, so every label drew
    // about a quarter narrower than the box built for it and the selected tab's
    // underline overhung its own word by roughly 40px a side.
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

        [UnityTest]
        public IEnumerator EveryTabLabelIsAsWideAsTheBarWasBuiltFor()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

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
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

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
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var hubTitle = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.name == "HubTitleLabel");

            Assert.IsNotNull(hubTitle, "the hub has no title label");
            Assert.AreEqual(0f, hubTitle.characterSpacing, 0.01f,
                "tracking has leaked into labels that never asked for it");
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
