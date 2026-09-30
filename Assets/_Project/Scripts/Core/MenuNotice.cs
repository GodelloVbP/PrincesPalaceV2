using TMPro;
using UnityEngine;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE MENU'S ONE NOTICE LINE (plan C11, AUDIT #259): a refusal or a
    // confirmation, in the same place every time, on the footer's left.
    //
    // It replaces the mechanisms the panes grew for themselves -- the Party
    // toast, the dossier's already-known line -- so a message reads the same
    // wherever it comes from and there is one clock to keep right.
    //
    // TWO KINDS OF LINE. A notice is transient: it holds for its seconds, fades,
    // and is gone. A state line is persistent: it stays until cleared, and shows
    // whenever no notice is up. A notice always wins the line, and the state
    // line comes back under it.
    //
    // UNSCALED TIME. The menu pauses the game at timeScale 0 while it is open,
    // so a scaled clock would hold every notice forever.
    //
    // Attached at build time to the footer's notice label (ScreenRegistry.
    // WireSystemMenu) and reached through SystemMenuController.ShowNotice, so a
    // pane never holds a reference to it.
    public class MenuNotice : MonoBehaviour
    {
        public const float DefaultSeconds = 3f;
        public const float FadeSeconds = 0.4f;

        private TMP_Text _label;
        private string _notice = string.Empty;
        private float _remaining;
        private string _state = string.Empty;

        // What the line reads right now, whichever kind supplies it. Tests and
        // the footer's own painting read this rather than the label.
        public string Text => _remaining > 0f ? _notice : _state;

        public bool NoticeIsUp => _remaining > 0f;

        private TMP_Text Label
        {
            get
            {
                if (_label == null) _label = GetComponent<TMP_Text>();
                return _label;
            }
        }

        // A NEW notice replaces the one showing and restarts the clock: a second
        // refusal a moment after the first must read as the second, not finish
        // the first's fade under different words.
        public void Show(UiString text, float seconds = DefaultSeconds) =>
            Show(text.Format(), seconds);

        public void Show(UiString text, object[] args, float seconds = DefaultSeconds) =>
            Show(text.Format(args), seconds);

        private void Show(string formatted, float seconds)
        {
            _notice = formatted ?? string.Empty;
            _remaining = seconds + FadeSeconds;
            Paint();
        }

        // The persistent line: it stays until ClearState, under any notice.
        public void SetState(UiString text) => SetState(text, System.Array.Empty<object>());

        public void SetState(UiString text, object[] args)
        {
            _state = text.Format(args);
            Paint();
        }

        public void ClearState()
        {
            _state = string.Empty;
            Paint();
        }

        // A notice belongs to the visit that raised it. Closing the menu drops
        // it, so the next open does not greet the player with a refusal from
        // the last one.
        private void OnDisable()
        {
            _remaining = 0f;
            _notice = string.Empty;
            Paint();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // The clock, separate from Update so a test can drive it without
        // waiting on real time.
        internal void Advance(float seconds)
        {
            if (_remaining <= 0f) return;

            _remaining = Mathf.Max(0f, _remaining - seconds);
            Paint();
        }

        // The last FadeSeconds of a notice fade it out; the state line under it
        // is drawn at full strength once the notice is gone.
        private void Paint()
        {
            var label = Label;
            if (label == null) return;

            label.SetContent(Text);

            float alpha = 1f;
            if (_remaining > 0f && _remaining < FadeSeconds) alpha = _remaining / FadeSeconds;
            label.alpha = alpha;
        }
    }
}
