using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE PROMPT ROW of the menu's footer (plan C12): [glyph] Label pairs, right
    // aligned, saying what the buttons do for the focused control and the
    // current pending state.
    //
    // POLLED, not pushed. What the row says depends on three things that change
    // under it with no event this frame owns: which control the EventSystem has
    // selected, which device the player last used (NavigationInputModule.
    // LastInputWasPad), and a pane's pending state (a Party carry starts with
    // focus staying where it is). The prompts are recomputed each frame and
    // painted only when the result differs from what is showing, which costs a
    // short list comparison while the menu is open and nothing while it is not.
    //
    // The slots are authored once (SystemMenuScreen.BuildFooter) and moved here:
    // the pure arithmetic is MenuFooterLayout, and this only measures the text
    // and applies the answer, as the tab bar does.
    public class MenuPromptFooter : MonoBehaviour
    {
        [SerializeField] internal SystemMenuController menu;
        [SerializeField] internal RectTransform[] promptPlates;
        [SerializeField] internal TMP_Text[] promptGlyphTexts;
        [SerializeField] internal TMP_Text[] promptLabels;

        // A hair of room beyond the measured text so a label never wraps on a
        // rounding difference between measuring and drawing.
        private const float LabelSlack = 4f;

        private readonly List<MenuPromptView> _shown = new List<MenuPromptView>();
        private bool _painted;

        // How many pairs are drawn. Read by tests; the pairs themselves are the
        // named prompt slots in the scene.
        public int ShownCount { get; private set; }

        private void OnEnable() => Refresh();

        private void Update() => Refresh();

        public void Refresh()
        {
            if (menu == null || promptLabels == null) return;

            var device = PromptGlyphs.DeviceFor(NavigationInputModule.LastInputWasPad);
            var views = MenuPrompts.Resolve(menu.CurrentPrompts(), menu.Context, device);

            if (_painted && SameAsShown(views)) return;

            Paint(views);
        }

        private bool SameAsShown(IReadOnlyList<MenuPromptView> views)
        {
            if (views.Count != _shown.Count) return false;

            for (int i = 0; i < views.Count; i++)
            {
                var a = views[i];
                var b = _shown[i];
                if (a.GlyphText != b.GlyphText || a.GlyphTintHex != b.GlyphTintHex || a.Label != b.Label)
                {
                    return false;
                }
            }

            return true;
        }

        private void Paint(IReadOnlyList<MenuPromptView> views)
        {
            _painted = true;
            _shown.Clear();
            _shown.AddRange(views);

            int slots = Mathf.Min(promptPlates.Length, Mathf.Min(promptGlyphTexts.Length, promptLabels.Length));
            int wanted = Mathf.Min(views.Count, slots);

            var glyphWidths = new float[wanted];
            var labelWidths = new float[wanted];
            for (int i = 0; i < wanted; i++)
            {
                glyphWidths[i] = promptGlyphTexts[i].GetPreferredValues(views[i].GlyphText).x;
                labelWidths[i] = promptLabels[i].GetPreferredValues(views[i].Label).x + LabelSlack;
            }

            var placed = MenuFooterLayout.Arrange(glyphWidths, labelWidths);
            ShownCount = placed.Count;

            for (int i = 0; i < slots; i++)
            {
                bool shown = i < placed.Count;
                promptPlates[i].gameObject.SetShown(shown);
                promptLabels[i].gameObject.SetShown(shown);
                if (!shown) continue;

                var pair = placed[i];
                promptPlates[i].sizeDelta = new Vector2(pair.PlateWidth, MenuFooterLayout.GlyphHeight);
                promptPlates[i].anchoredPosition = new Vector2(pair.PlateCentreX, 0f);

                promptGlyphTexts[i].SetContent(views[i].GlyphText);
                if (ColorUtility.TryParseHtmlString(views[i].GlyphTintHex, out var tint)) promptGlyphTexts[i].color = tint;

                var labelRect = (RectTransform)promptLabels[i].transform;
                labelRect.sizeDelta = new Vector2(pair.LabelWidth, labelRect.sizeDelta.y);
                labelRect.anchoredPosition = new Vector2(pair.LabelCentreX, 0f);
                promptLabels[i].SetContent(views[i].Label);
            }
        }
    }
}
