using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Content
{
    // The dialogue stage's half of event validation (docs/PLAN_DIALOGUE_STAGE.md,
    // phase D1): backdrops, a page's cast and lines, and the presence check.
    // Its own file so the rest of the resolver can grow (new effect and
    // requirement kinds) without the two changes meeting in one hunk.
    public static partial class EventEntryResolver
    {
        // Contract 13. D2 re-pins MaxLineLength by measuring the longest line
        // at 4:3 and 21:9; the cap counts the raw string, tags included, so a
        // line's visible text is never longer than what gets measured.
        public const int MaxLinesPerPage = 12;
        public const int MaxLineLength = 200;

        public const string DefaultBackdrop = "Assets/_Project/Art/Backgrounds/Dungeon.png";
        public const string BackgroundArtRoot = "Assets/_Project/Art/Backgrounds/";

        // The speaker a line uses to say "nobody": no bust, no name plate.
        public const string NarrationSpeaker = "narration";

        private const string ItalicOpen = "<i>";
        private const string ItalicClose = "</i>";

        // A backdrop is Assets-relative (ArtPathConvention "backdrop") and is
        // filed either with the shared backgrounds or in the event's own art
        // folder -- the two places a delete-this-event pass can reason about.
        private static bool TryResolveBackdrop(string label, string eventId, string backdrop, out string error)
        {
            if (!ArtPathConvention.Check(label, "backdrop", backdrop, out error)) return false;
            if (string.IsNullOrWhiteSpace(backdrop)) return true;

            string trimmed = backdrop.Trim();
            if (trimmed.StartsWith(BackgroundArtRoot, StringComparison.Ordinal)) return true;

            if (trimmed.StartsWith(EventArtRoot, StringComparison.Ordinal))
            {
                return IsFiledInItsEventFolder(label, eventId, trimmed, out error, "backdrop");
            }

            error = $"{label}: backdrop '{trimmed}' must be filed under {BackgroundArtRoot} or this event's own " +
                    $"{EventArtRoot}{eventId}/ folder.";
            return false;
        }

        private static bool TryResolveLines(RawEventPage raw, string pageLabel,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            out ResolvedEventCastMember[] cast, out ResolvedEventLine[] lines, out string error)
        {
            cast = Array.Empty<ResolvedEventCastMember>();
            lines = Array.Empty<ResolvedEventLine>();

            var rawLines = raw.lines ?? Array.Empty<RawEventLine>();
            var rawCast = raw.cast ?? Array.Empty<RawEventCastMember>();

            if (rawLines.Length > MaxLinesPerPage)
            {
                error = $"{pageLabel}: has {rawLines.Length} lines, over the {MaxLinesPerPage} allowed.";
                return false;
            }

            // Declared sides first, so a line's side can be read off them.
            var declared = new Dictionary<string, DialogueSide>(StringComparer.Ordinal);
            foreach (var row in rawCast)
            {
                if (row == null) continue;

                string character = (row.character ?? "").Trim();
                string castLabel = $"{pageLabel} cast '{character}'";
                if (!TryKnownCharacter(character, pageLabel, "cast entry", characterDisplayNamesById, out _, out error))
                {
                    return false;
                }

                if (declared.ContainsKey(character))
                {
                    error = $"{castLabel}: listed twice -- a character stands on one side of a page.";
                    return false;
                }

                if (!TryParseExactName(row.side, out DialogueSide side))
                {
                    error = $"{castLabel}: side '{row.side}' is not left or right.";
                    return false;
                }

                declared.Add(character, side);
            }

            // Contract 5: undeclared speakers alternate by first appearance,
            // counting EVERY distinct speaker on the page, declared or not --
            // so adding a declaration never moves anybody else.
            var sides = new Dictionary<string, DialogueSide>(StringComparer.Ordinal);
            var castOrder = new List<ResolvedEventCastMember>();
            var resolvedLines = new List<ResolvedEventLine>(rawLines.Length);

            for (int i = 0; i < rawLines.Length; i++)
            {
                var row = rawLines[i] ?? new RawEventLine();
                string lineLabel = $"{pageLabel} line #{i + 1}";
                string speaker = (row.speaker ?? "").Trim();
                string expressionText = (row.expression ?? "").Trim();
                string text = row.text ?? "";

                if (string.IsNullOrEmpty(speaker))
                {
                    error = $"{lineLabel}: speaker is required -- a character id, or '{NarrationSpeaker}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    error = $"{lineLabel}: has no text.";
                    return false;
                }

                if (!WithinCap(lineLabel, "text", text, MaxLineLength, out error)
                    || !TryCheckLineMarkup(lineLabel, text, out error))
                {
                    return false;
                }

                if (string.Equals(speaker, NarrationSpeaker, StringComparison.OrdinalIgnoreCase))
                {
                    if (expressionText.Length > 0)
                    {
                        error = $"{lineLabel}: narration has expression '{expressionText}' -- narration shows no bust, " +
                                "so it takes no expression.";
                        return false;
                    }

                    resolvedLines.Add(new ResolvedEventLine("", true, DialogueExpression.Neutral, DialogueSide.Left, text));
                    continue;
                }

                if (!TryKnownCharacter(speaker, lineLabel, "speaker", characterDisplayNamesById, out _, out error))
                {
                    return false;
                }

                var expression = DialogueExpression.Neutral;
                if (expressionText.Length > 0 && !TryParseExactName(expressionText, out expression))
                {
                    error = $"{lineLabel}: expression '{expressionText}' is not a known DialogueExpression (" +
                            string.Join(", ", Enum.GetNames(typeof(DialogueExpression)).Select(n => n.ToLowerInvariant())) + ").";
                    return false;
                }

                if (!sides.TryGetValue(speaker, out var lineSide))
                {
                    lineSide = declared.TryGetValue(speaker, out var pinned)
                        ? pinned
                        : (sides.Count % 2 == 0 ? DialogueSide.Left : DialogueSide.Right);
                    sides.Add(speaker, lineSide);
                    castOrder.Add(new ResolvedEventCastMember(speaker, lineSide));
                }

                resolvedLines.Add(new ResolvedEventLine(speaker, false, expression, lineSide, text));
            }

            foreach (string character in declared.Keys)
            {
                if (!sides.ContainsKey(character))
                {
                    error = $"{pageLabel} cast '{character}': never speaks on this page -- a cast entry only places a speaker.";
                    return false;
                }
            }

            cast = castOrder.ToArray();
            lines = resolvedLines.ToArray();
            error = null;
            return true;
        }

        // Contract 12: <i>...</i> only, lowercase, balanced and not nested.
        // Any other '<' is refused rather than guessed at, because TMP parses
        // tags it knows and prints the rest -- a <b> the font cannot draw, or a
        // <color> a typo half-closes, would reach the player either way.
        private static bool TryCheckLineMarkup(string label, string text, out string error)
        {
            bool open = false;
            int at = 0;
            while ((at = text.IndexOf('<', at)) >= 0)
            {
                if (string.CompareOrdinal(text, at, ItalicOpen, 0, ItalicOpen.Length) == 0)
                {
                    if (open)
                    {
                        error = $"{label}: text opens <i> inside an <i> -- italics do not nest.";
                        return false;
                    }

                    open = true;
                    at += ItalicOpen.Length;
                    continue;
                }

                if (string.CompareOrdinal(text, at, ItalicClose, 0, ItalicClose.Length) == 0)
                {
                    if (!open)
                    {
                        error = $"{label}: text closes </i> with no <i> open.";
                        return false;
                    }

                    open = false;
                    at += ItalicClose.Length;
                    continue;
                }

                int end = text.IndexOf('>', at);
                string tag = end < 0 ? text.Substring(at) : text.Substring(at, end - at + 1);
                error = $"{label}: text contains '{tag}' -- the only markup a dialogue line may use is " +
                        "<i>...</i> (lowercase); every other tag, <b> included, is refused.";
                return false;
            }

            if (open)
            {
                error = $"{label}: text opens <i> and never closes it.";
                return false;
            }

            error = null;
            return true;
        }

        // Contract 4, checked once per event over the resolved page graph.
        // A page EventCastPresence could not reach from the start page has
        // no set, and its speakers are skipped: nothing plays there.
        private static bool TryCheckSpeakersPresent(ResolvedEventDefinition evt, string eventLabel, out string error)
        {
            var guaranteed = EventCastPresence.GuaranteedByPage(evt);
            foreach (var page in evt.Pages)
            {
                if (page == null || !guaranteed.TryGetValue(page.Id, out var present)) continue;

                for (int i = 0; i < page.Lines.Length; i++)
                {
                    var line = page.Lines[i];
                    if (line.IsNarration || present.Contains(line.SpeakerId)) continue;

                    error = $"{eventLabel} page '{page.Id}' line #{i + 1}: speaker '{line.SpeakerId}' is not guaranteed " +
                            "to be in the party here. Add an inParty requirement naming " +
                            $"'{line.SpeakerId}' (or a memberLevel/ability one that names them) to the event's requires, " +
                            $"or to a choice or outcome on EVERY route into page '{page.Id}'.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Case-insensitive match against an enum's member NAMES only.
        // Enum.TryParse would also take "3" or "1, 2" and hand back a value no
        // author meant.
        private static bool TryParseExactName<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
        {
            parsed = default;
            string trimmed = (value ?? "").Trim();
            foreach (string name in Enum.GetNames(typeof(TEnum)))
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    parsed = (TEnum)Enum.Parse(typeof(TEnum), name);
                    return true;
                }
            }

            return false;
        }
    }
}
