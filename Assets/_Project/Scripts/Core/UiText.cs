using TMPro;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The ONLY way a controller is allowed to change label text.
    //
    // This is the runtime half of Contract A. The build half (Ui.Label/Ui.Button)
    // accepts only a UiString; without this, a controller could still write
    // label.text = $"Gold: {g}" and re-open the exact drift v1 shipped. With it,
    // both halves interpolate the SAME template out of the SAME manifest entry,
    // and the lint refuses a bare `.text =` anywhere outside this file and the
    // emitter.
    //
    // TMP_Text, never the legacy Text component: v2 is TMP from day one, so
    // there is no second text type for a call site to pick wrongly.
    public static class UiText
    {
        public static void Set(this TMP_Text label, UiString text)
        {
            if (label == null) return;
            label.text = text.Format();
        }

        public static void Set(this TMP_Text label, UiString text, params object[] args)
        {
            if (label == null) return;
            label.text = text.Format(args);
        }

        // Content-derived text (a character or item name out of JSON). Separate
        // from Set so that reading a call site tells you immediately whether the
        // words came from the manifest or from data -- and so the lint can tell
        // a legitimate content name from a literal smuggled through
        // UiString.FromContent("...").
        public static void SetContent(this TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value ?? string.Empty;
        }
    }
}
