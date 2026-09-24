using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Binds the Options pane to GameSettings.
    //
    // EVERY CONTROL WRITES THROUGH IMMEDIATELY. That is the design's rule --
    // no confirm, no apply button -- and GameSettings already worked that way:
    // its setters save to PlayerPrefs and push at the device on the spot. So
    // this holds no pending state of its own, which is also why there is
    // nothing here to lose if the menu is closed mid-change.
    //
    // Rows are addressed BY KEY, matching OptionRows.AllRows by index. A switch
    // on the key rather than on the index means adding a row to that table
    // cannot silently bind the wrong control to the wrong setting -- it lands
    // on the default and throws instead.
    public class OptionsController : MonoBehaviour
    {
        [SerializeField] internal GameObject[] rowHovers;

        // Attached at build time (ScreenRegistry.WireOptions), one per
        // RowHovers node -- the ONE component now handling that node's
        // movement and hover (plan section 7), replacing the old plain
        // Panel + runtime-added HoverIndex. Parallel to rowHovers rather
        // than replacing it: rowHovers stays because UiAutoBind already
        // binds it by name, and Fade() below still wants the plain
        // GameObject for its Image lookup.
        [SerializeField] internal OptionRow[] rows;

        // Two dense sets, each carrying the GameSettings key its controls drive.
        // Keyed rather than positional because the rows are heterogeneous, and
        // the padded-with-nulls alternative is what UiWiringSweep refused.
        [SerializeField] internal string[] sliderKeys;
        [SerializeField] internal Image[] sliderTracks;
        [SerializeField] internal RectTransform[] sliderFills;
        [SerializeField] internal TMP_Text[] sliderValues;

        [SerializeField] internal string[] stepperKeys;
        [SerializeField] internal Button[] stepPrev;
        [SerializeField] internal Button[] stepNext;
        [SerializeField] internal TMP_Text[] stepperValues;

        [SerializeField] internal Button restoreDefaults;

        private bool _wired;

        private void OnEnable()
        {
            Wire();

            // Runs every open, not just the first -- Wire()'s reset (inside its
            // _wired guard) only ever fires once. Without this, closing Options
            // while the mouse sits over a row leaves that row's hover plate at
            // HoverAlpha for every reopen after, since nothing else clears it.
            for (int i = 0; rowHovers != null && i < rowHovers.Length; i++)
            {
                if (rowHovers[i] == null) continue;
                Fade(rowHovers[i], 0f);
            }

            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            var allRows = OptionRows.AllRows;

            for (int i = 0; rowHovers != null && i < rowHovers.Length; i++)
            {
                if (rowHovers[i] == null) continue;

                // Emitted at its tint and faded to nothing, so raising it is an
                // alpha change. The design's States table asks for a row TINT,
                // and a plate that switches on and off cannot be given a fade
                // later without rewriting this.
                //
                // An alpha-0 Image still raycasts, which is what keeps the row
                // hoverable while it is invisible.
                rowHovers[i].SetActive(true);
                Fade(rowHovers[i], 0f);

                // OptionRow is the ONE component handling this row's movement
                // and hover now (plan section 7) -- attached at build time
                // (ScreenRegistry.WireOptions), not runtime-added the way
                // HoverIndex used to be, since RuntimeNavWiring.Apply below
                // needs every row's Selectable to already exist.
                if (rows == null || i >= rows.Length || rows[i] == null) continue;
                int index = i;
                rows[index].HoverChanged = entered => Fade(rowHovers[index], entered ? HoverAlpha : 0f);

                if (allRows == null || i >= allRows.Count) continue;
                string key = allRows[i].Key;

                // Left/Right's meaning depends on the row's KIND, decided
                // here rather than by OptionRow itself -- it stays a pure
                // input adapter with no idea what GameSettings or a key is.
                rows[index].OnLeftRight = allRows[i].Kind == OptionKind.Stepper
                    ? delta => Step(key, delta)
                    : delta => SetSlider(key, Mathf.Clamp01(SliderValue(key) + delta * SliderStep));
            }

            // A List group, clamp (plan section 5/7's owner default) -- Up/Down
            // steps row to row, never wrapping past the first or last card.
            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("optionsRows", UiNavGroupKind.List, rows));

            for (int i = 0; sliderTracks != null && i < sliderTracks.Length; i++)
            {
                if (sliderTracks[i] == null || sliderKeys == null || i >= sliderKeys.Length) continue;
                string key = sliderKeys[i];
                var bar = sliderTracks[i].gameObject.AddComponent<BarSlider>();
                bar.Changed = value => SetSlider(key, value);
            }

            for (int i = 0; stepPrev != null && i < stepPrev.Length; i++)
            {
                if (stepperKeys == null || i >= stepperKeys.Length) continue;
                string key = stepperKeys[i];
                if (stepPrev[i] != null) stepPrev[i].onClick.AddListener(() => Step(key, -1));
                if (stepNext[i] != null) stepNext[i].onClick.AddListener(() => Step(key, +1));

                // NEVER a navigation target, and never selected by a mouse
                // click either (plan section 7) -- clicking one still fires
                // its onClick (independent of the selection side effect
                // Navigation.Mode.None suppresses), and does nothing to the
                // row's own remembered focus.
                SetNoNavigation(stepPrev[i]);
                SetNoNavigation(stepNext[i]);
            }

            if (restoreDefaults != null) restoreDefaults.onClick.AddListener(RestoreDefaults);
        }

        // One row's Left/Right press moves a slider by this much -- the
        // row's own step size, declared once here rather than restated at
        // its one caller above (docs/CODE_STANDARDS.md section 6).
        private const float SliderStep = 0.05f;

        // UnityEngine.UI.Navigation spelled out in full: PrincesPalace.
        // Navigation (the map/graph state type) sits in this same
        // namespace and would otherwise shadow it -- phase 1's own hazard
        // list calls this out explicitly.
        private static void SetNoNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            var nav = selectable.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            selectable.navigation = nav;
        }

        // The design's row tint, #C8AAE60D, is 5% -- so this is the alpha the
        // tree already declared, restored rather than invented.
        private const float HoverAlpha = 0.05f;

        private static void Fade(GameObject go, float alpha)
        {
            var image = go == null ? null : go.GetComponent<Image>();
            if (image == null) return;

            var colour = image.color;
            image.color = new Color(colour.r, colour.g, colour.b, alpha);
        }

        public void Refresh()
        {
            for (int i = 0; sliderValues != null && i < sliderValues.Length; i++)
            {
                if (sliderKeys == null || i >= sliderKeys.Length) continue;
                float value = SliderValue(sliderKeys[i]);

                if (sliderValues[i] != null)
                {
                    sliderValues[i].SetContent(Mathf.RoundToInt(value * 100f) + "%");
                }

                if (sliderFills != null && i < sliderFills.Length && sliderFills[i] != null)
                {
                    // WIDTH, not anchors. The fill is pivoted at the track's
                    // left edge, so growing its width extends it rightwards and
                    // nothing else has to be consistent for that to hold.
                    // Anchoring 0..value looked equivalent and was not: anchors
                    // are relative to the PARENT, so the bar spanned that
                    // fraction of the whole card and struck through its own row.
                    var fill = sliderFills[i];
                    fill.sizeDelta = new Vector2(
                        OptionsLayout.TrackWidth * Mathf.Clamp01(value), fill.sizeDelta.y);
                }
            }

            for (int i = 0; stepperValues != null && i < stepperValues.Length; i++)
            {
                if (stepperKeys == null || i >= stepperKeys.Length || stepperValues[i] == null) continue;
                stepperValues[i].SetContent(StepperLabel(stepperKeys[i]));
            }
        }

        // ---- the bindings -------------------------------------------------------

        private static float SliderValue(string key)
        {
            switch (key)
            {
                case "sound": return GameSettings.SoundVolume;
                case "music": return GameSettings.MusicVolume;
                default: throw new ArgumentOutOfRangeException(nameof(key), key,
                    "an Options slider has no GameSettings value behind it");
            }
        }

        private void SetSlider(string key, float value)
        {
            switch (key)
            {
                case "sound": GameSettings.SetSoundVolume(value); break;
                case "music": GameSettings.SetMusicVolume(value); break;
                default: throw new ArgumentOutOfRangeException(nameof(key), key,
                    "an Options slider has no GameSettings setter behind it");
            }

            Refresh();
        }

        private static string StepperLabel(string key)
        {
            switch (key)
            {
                case "resolution": return GameSettings.Resolutions[GameSettings.ResolutionIndex].Label;
                case "window": return GameSettings.WindowModeLabels[GameSettings.WindowModeIndex];
                case "fps": return GameSettings.FpsLimits[GameSettings.FpsLimitIndex] + " fps";
                // BattleSpeed.Nearest rather than IndexOf(GameSettings.BattleSpeed)
                // directly: the stored value is already an exact row's Display
                // (GameSettings.Load/SetBattleSpeed both route through Nearest),
                // but reading it back through Nearest again is what keeps this
                // label honest if that guarantee is ever violated from outside.
                case "battlespeed":
                    return UiStrings.OptionsBattleSpeedValue.Format(
                        BattleSpeed.Nearest(GameSettings.BattleSpeed).DisplayNumber);
                default: throw new ArgumentOutOfRangeException(nameof(key), key,
                    "an Options stepper has no GameSettings value behind it");
            }
        }

        // CLAMPED, not wrapped. Wrapping a resolution list means one click past
        // 4K silently drops the player to 1280x720, which on a stepper they are
        // holding down is a nasty surprise; the ends of these lists are ends.
        //
        // Public, like Refresh and RestoreDefaults beside it: T3 and T6
        // (docs/archive/PLAN_BATTLE_SPEED.md) both step a row directly rather than
        // finding and clicking its Button, and PlayMode has no
        // InternalsVisibleTo grant to reach this at `internal`.
        public void Step(string key, int delta)
        {
            switch (key)
            {
                case "resolution":
                    GameSettings.SetResolutionIndex(
                        Clamp(GameSettings.ResolutionIndex + delta, GameSettings.Resolutions.Length));
                    break;
                case "window":
                    GameSettings.SetWindowModeIndex(
                        Clamp(GameSettings.WindowModeIndex + delta, GameSettings.WindowModeLabels.Length));
                    break;
                case "fps":
                    GameSettings.SetFpsLimitIndex(
                        Clamp(GameSettings.FpsLimitIndex + delta, GameSettings.FpsLimits.Length));
                    break;
                case "battlespeed":
                {
                    // The CURRENT row's index, by identity (Nearest of an
                    // already-exact value returns that row), stepped and
                    // clamped exactly like every other stepper here, then
                    // handed to SetBattleSpeed as a DISPLAY number -- Nearest
                    // there maps an exact row's own Display back to itself,
                    // so this never drifts off the table it just walked.
                    int index = BattleSpeed.IndexOf(BattleSpeed.Nearest(GameSettings.BattleSpeed));
                    if (index < 0) index = BattleSpeed.IndexOf(BattleSpeed.Default);
                    index = Clamp(index + delta, BattleSpeed.Rows.Count);
                    GameSettings.SetBattleSpeed(BattleSpeed.Rows[index].Display);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key,
                        "an Options stepper has no GameSettings setter behind it");
            }

            Refresh();
        }

        private static int Clamp(int index, int length) =>
            length == 0 ? 0 : Mathf.Clamp(index, 0, length - 1);

        // The same defaults GameSettings itself starts from. Not a separate
        // table: a second list of defaults is a second thing to forget.
        public void RestoreDefaults()
        {
            GameSettings.SetSoundVolume(GameSettings.DefaultVolume);
            GameSettings.SetMusicVolume(GameSettings.DefaultVolume);
            GameSettings.SetWindowModeIndex(GameSettings.DefaultWindowModeIndex);
            GameSettings.SetFpsLimitIndex(GameSettings.DefaultFpsLimitIndex);
            GameSettings.SetResolutionIndex(GameSettings.DefaultResolutionIndexForDisplay());
            GameSettings.SetBattleSpeed(BattleSpeed.DefaultDisplay);
            Refresh();
        }
    }
}
