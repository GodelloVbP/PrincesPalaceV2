using System;

namespace PrincesPalace.Domain.UiKit
{
    // Every piece of user-facing text in the game, as a value.
    //
    // This is Contract A, and it exists because of a specific v1 failure: the
    // builder wrote a label's initial text and a controller rewrote it at
    // runtime, with nothing tying the two together. 116 literals, four of them
    // confirmed drifted -- the builder writing "Gold: 0    Relics: 0" while the
    // controller wrote $"Gold: {g}    Relics: {r}", the four-space separator
    // copied by hand and therefore copyable wrong.
    //
    // The fix is not discipline. Ui.Label and Ui.Button accept ONLY a UiString,
    // so a bare literal at a construction site does not compile, and controllers
    // set text exclusively through Core's UiText extensions -- which take the
    // same UiString. Builder text and runtime text become the same template by
    // construction, and those four drift pairs collapse into four entries.
    public readonly struct UiString
    {
        // Stable identifier. The seam a localisation table would key on later;
        // nothing depends on that yet, but naming it now costs nothing and
        // retrofitting it across 116 sites would not.
        public readonly string Key;

        // A composite format string ("Gold: {0}") or a plain literal.
        public readonly string Template;

        // What the text-fit audit measures. A template's rendered width depends
        // on values that do not exist at build time, so the author states the
        // realistic worst case ("Gold: 999999") and E1 measures THAT against the
        // solved box. Without it, every templated label would be measured at its
        // shortest and every overflow would ship.
        public readonly string AuditSample;

        // Content-derived text (a character or item name out of JSON). Exempt
        // from the manifest because it is data, not authored UI copy -- but
        // marked, so the lint can tell a legitimate content name from someone
        // smuggling a literal through FromContent("...").
        public readonly bool IsContent;

        private UiString(string key, string template, string auditSample, bool isContent)
        {
            Key = key;
            Template = template;
            AuditSample = auditSample;
            IsContent = isContent;
        }

        // Internal so the manifest is the only place entries are declared.
        internal static UiString Define(string key, string template, string sample = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A UiString needs a key.", nameof(key));
            if (template == null) throw new ArgumentNullException(nameof(template));
            return new UiString(key, template, sample ?? template, isContent: false);
        }

        public static UiString FromContent(string value) =>
            new UiString("content", value ?? string.Empty, value ?? string.Empty, isContent: true);

        // A label that starts blank and is filled from live state -- an enemy's
        // name, a damage figure, a skill description.
        //
        // Named rather than written as FromContent(""), for two reasons. The
        // lint bans a literal inside FromContent and cannot tell a deliberately
        // blank one from a smuggled word, so without this every runtime-filled
        // label on a screen would have to be exempted individually. And it says
        // what it means at the call site: not "the content happens to be a
        // space" but "nothing is authored here; the controller owns this text".
        //
        // Measured by nothing: E1 has no sample because there is no authored
        // copy to overflow. What the RUNTIME puts here is bounded by its own
        // template's AuditSample at the call site that writes it.
        public static readonly UiString Runtime =
            new UiString("runtime", string.Empty, string.Empty, isContent: true);

        public bool IsValid => Template != null;

        // True when this entry interpolates, and therefore must carry its own
        // AuditSample rather than being measured at its template's own width.
        public bool IsTemplated => Template != null && Template.IndexOf('{') >= 0;

        public string Format(params object[] args)
        {
            if (Template == null) return string.Empty;
            return args == null || args.Length == 0 ? Template : string.Format(Template, args);
        }

        public override string ToString() => Key + ":" + Template;
    }
}
