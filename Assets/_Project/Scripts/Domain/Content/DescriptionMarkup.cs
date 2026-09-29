using System;
using System.Collections.Generic;
using System.Text;

namespace PrincesPalace.Domain.Content
{
    // What a renderer can draw right now. Bold needs a real bold face and
    // icons need the sprite asset; either can be missing (a fresh clone, a
    // stripped build), and Expand then degrades that token instead of
    // leaving its raw text on screen.
    public readonly struct DescriptionRenderCaps
    {
        public readonly bool Bold;
        public readonly bool Icons;

        public DescriptionRenderCaps(bool bold, bool icons)
        {
            Bold = bold;
            Icons = icons;
        }

        public static DescriptionRenderCaps Plain => new DescriptionRenderCaps(false, false);
        public static DescriptionRenderCaps Full => new DescriptionRenderCaps(true, true);
    }

    // The authoring grammar of skill, talent and relic descriptions, defined
    // once.
    //
    //   **text**   bold. Every `**` is a delimiter, taken in order: the first
    //              opens, the second closes. There is no escape and no
    //              nesting, so `2**3` on its own is an unbalanced-marker
    //              error rather than a power; write "2 to the 3rd".
    //   {name}     an inline icon, name from Icons below.
    //
    // Authors write tokens, never rich-text tags: Validate refuses '<', so the
    // renderer's tags can never be typed by hand or collide with a token.
    //
    // Validate runs at content build. Expand runs at display time on every
    // SetContent string, event dialogue included, so it changes nothing it
    // cannot fully resolve: a well-formed token is rewritten, anything else
    // (a lone `**`, an unknown `{x}`, a lone brace) stays literal. Build-time
    // validation is what keeps such text out of descriptions.
    public static class DescriptionMarkup
    {
        // The font a bold run switches to (Core.TmpAssetResolver builds it)
        // and the sprite asset an icon is read from (Core.TmpAssetResolver
        // loads it). The names are the TMP tag values, so they live here
        // beside the tags Expand writes.
        public const string BoldFontName = "SourceSans3-Bold SDF";
        public const string IconSpriteAssetName = "DescriptionIcons";

        // One row per icon. Token is what an author writes, SpriteName is the
        // sprite in the asset TmpBootstrap.GenerateDescriptionIcons packs
        // (and the PNG stem under Resources/Icons/Ability), PlainWord is what
        // an icon degrades to. There is no wool, HP, gold or Fury art, so
        // those stay words in the description.
        public readonly struct Icon
        {
            public readonly string Token;
            public readonly string SpriteName;
            public readonly string PlainWord;

            public Icon(string token, string spriteName, string plainWord)
            {
                Token = token;
                SpriteName = spriteName;
                PlainWord = plainWord;
            }
        }

        public static readonly IReadOnlyList<Icon> Icons = new[]
        {
            new Icon("fire", "element_fire", "Fire"),
            new Icon("ice", "element_ice", "Ice"),
            new Icon("nature", "element_nature", "Nature"),
            new Icon("physical", "element_physical", "Physical"),
            new Icon("poison", "element_poison", "Poison"),
            new Icon("arcane", "element_arcane", "Arcane"),
            new Icon("earth", "element_earth", "Earth"),
            new Icon("lightning", "element_lightning", "Lightning"),
            new Icon("water", "element_water", "Water"),
            new Icon("wind", "element_wind", "Wind"),
            new Icon("void", "element_void", "Void"),
            new Icon("mana", "resource_mana", "mana"),
            new Icon("cooldown", "resource_cooldown", "cooldown"),
            new Icon("cost", "resource_cost", "cost"),
        };

        // The grammar as docs/CONTENT_SCHEMA.md prints it (ContentSchema
        // appends this and the Icons table); the class header states the same
        // rules for whoever edits the code.
        public const string GrammarDoc =
            "Skill, talent and relic descriptions may carry two authoring tokens; nothing else is " +
            "interpreted, and a description containing `<` is refused.\n\n" +
            "- `**text**` renders bold. Every `**` is a delimiter taken in order (first opens, second closes), " +
            "so markers must balance, may not be empty, and do not nest. There is no escape: write \"2 to the 3rd\", not `2**3`.\n" +
            "- `{name}` renders an inline icon; `name` must be a token in the table below, and an unknown one " +
            "fails the content build naming the row. An icon may sit inside a bold run.\n\n" +
            "When the bold face or the icon sprite asset is unavailable at runtime, bold drops to plain text and " +
            "an icon to its plain word. Wool, HP, gold and Fury have no icon art and stay words.\n\n";

        public static bool TryGetIcon(string token, out Icon icon)
        {
            for (int i = 0; i < Icons.Count; i++)
            {
                if (Icons[i].Token == token)
                {
                    icon = Icons[i];
                    return true;
                }
            }

            icon = default;
            return false;
        }

        // Null when the text is well-formed, otherwise every problem found,
        // joined. Empty and null text are valid.
        public static string Validate(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var errors = new List<string>();
            if (text.IndexOf('<') >= 0)
            {
                errors.Add("contains '<'; write **bold** and {icon} tokens, never rich-text tags");
            }

            int markers = 0;
            int lastMarkerEnd = -1;
            bool open = false;

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    markers++;
                    if (open && lastMarkerEnd == i)
                    {
                        errors.Add($"empty bold at index {i - 2}: '****' wraps nothing");
                    }

                    open = !open;
                    i += 2;
                    lastMarkerEnd = i;
                }
                else if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        errors.Add($"'{{' at index {i} is never closed");
                        i++;
                        continue;
                    }

                    string name = text.Substring(i + 1, close - i - 1);
                    if (!TryGetIcon(name, out _))
                    {
                        errors.Add($"unknown icon '{{{name}}}'; known: {KnownTokens()}");
                    }

                    i = close + 1;
                }
                else if (c == '}')
                {
                    errors.Add($"'}}' at index {i} closes nothing");
                    i++;
                }
                else
                {
                    i++;
                }
            }

            if (markers % 2 != 0)
            {
                errors.Add("unbalanced '**': every bold run needs an opening and a closing marker");
            }

            return errors.Count == 0 ? null : string.Join("; ", errors);
        }

        // The icon table as a markdown table, for CONTENT_SCHEMA.md.
        public static string IconTableDoc()
        {
            var sb = new StringBuilder();
            sb.Append("| Token | Sprite (Resources/Icons/Ability) | Plain word |\n|---|---|---|\n");
            foreach (Icon icon in Icons)
            {
                sb.Append("| `{").Append(icon.Token).Append("}` | `").Append(icon.SpriteName)
                  .Append("` | ").Append(icon.PlainWord).Append(" |\n");
            }

            return sb.ToString();
        }

        // The resolvers' form of Validate: the message names the row.
        public static bool Check(string label, string description, out string error)
        {
            string problem = Validate(description);
            error = problem == null ? null : $"{label}: description {problem}.";
            return problem == null;
        }

        // Rewrites the well-formed tokens in `text` for a renderer with the
        // given caps. Text without a token comes back as the same string.
        public static string Expand(string text, DescriptionRenderCaps caps)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (text.IndexOf("**", StringComparison.Ordinal) < 0 && text.IndexOf('{') < 0) return text;

            var sb = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    // A pair needs a closing marker with something between.
                    int close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        if (caps.Bold) sb.Append("<font=\"").Append(BoldFontName).Append("\">");
                        sb.Append(ExpandRun(text, i + 2, close, caps));
                        if (caps.Bold) sb.Append("</font>");
                        i = close + 2;
                        continue;
                    }

                    // Unpaired: both stars stay, so a later `**` cannot pair
                    // with half of this one.
                    sb.Append("**");
                    i += 2;
                    continue;
                }
                else if (c == '{' && TryReadIcon(text, i, out Icon icon, out int next))
                {
                    AppendIcon(sb, icon, caps);
                    i = next;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        // The inside of a bold run: only icons are expanded, `**` cannot
        // occur in it.
        private static string ExpandRun(string text, int start, int end, DescriptionRenderCaps caps)
        {
            var sb = new StringBuilder(end - start);
            int i = start;
            while (i < end)
            {
                if (text[i] == '{' && TryReadIcon(text, i, out Icon icon, out int next) && next <= end)
                {
                    AppendIcon(sb, icon, caps);
                    i = next;
                }
                else
                {
                    sb.Append(text[i]);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static bool TryReadIcon(string text, int at, out Icon icon, out int next)
        {
            int close = text.IndexOf('}', at + 1);
            if (close > at && TryGetIcon(text.Substring(at + 1, close - at - 1), out icon))
            {
                next = close + 1;
                return true;
            }

            icon = default;
            next = at;
            return false;
        }

        private static void AppendIcon(StringBuilder sb, Icon icon, DescriptionRenderCaps caps)
        {
            if (caps.Icons)
            {
                sb.Append("<sprite=\"").Append(IconSpriteAssetName).Append("\" name=\"").Append(icon.SpriteName).Append("\">");
            }
            else
            {
                sb.Append(icon.PlainWord);
            }
        }

        private static string KnownTokens()
        {
            var names = new string[Icons.Count];
            for (int i = 0; i < names.Length; i++) names[i] = Icons[i].Token;
            return string.Join(", ", names);
        }
    }
}
