using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // Pure path arithmetic for a character's dialogue bust -- no engine
    // types, so the dialogue stage's "which sprite do I try next" logic is
    // testable without a Resources folder or a running Editor.
    //
    // WHY A FOLDER AND NOT A FILE. RawCharacterEntry.dialogueBustPath names
    // the folder tools/normalize_dialogue_busts.py wrote
    // (Resources/Portraits/Dialogue/<characterId>/), one PNG per expression
    // on ONE shared canvas per character (see the tool's own header for why
    // a shared canvas). The runtime picks the expression, not content, so
    // the field cannot name a file the way plateArt or portraitPath do.
    public static class DialogueBust
    {
        // Seven expressions tools/normalize_dialogue_busts.py writes; "neutral"
        // is also the universal fallback (Fallbacks below), so it is not
        // just one of the seven -- it is the one every character is expected
        // to eventually have.
        public const string Neutral = "neutral";

        // "<folder>/<expression>", Resources.Load-shaped (no extension) --
        // the same join ArtPathConvention's header describes for every other
        // RuntimeLoaded field. An empty folder means the character has no
        // bust art at all, and returning "" rather than throwing lets the
        // caller's existing "empty path means no art" branch handle it with
        // no special case for this one field.
        public static string ResourcePath(string bustFolder, string expression)
        {
            if (string.IsNullOrWhiteSpace(bustFolder)) return "";
            return $"{bustFolder.TrimEnd('/')}/{expression}";
        }

        // The order the dialogue stage tries loading a bust in: the
        // requested expression, then "neutral". Two entries, not one per
        // expression, because DIALOGUE_D0_BRIEF's own note is what this
        // exists for: Shawn has no neutral bust today and Bjorn has ONLY a
        // neutral one -- every other expression falls back to it rather than
        // showing nothing, and a character with neither yet still shows
        // nothing (the runtime's Resources.Load already returns null for
        // that, with no third rung needed here).
        //
        // NO DUPLICATE: requesting "neutral" itself must not yield
        // ["neutral", "neutral"] -- a caller iterating this list to build a
        // deduplicated retry chain would otherwise try the same load twice
        // for no reason.
        public static IEnumerable<string> Fallbacks(string expression)
        {
            bool isNeutral = string.Equals(expression, Neutral, System.StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(expression) && !isNeutral)
            {
                yield return expression;
            }

            yield return Neutral;
        }

        // The file name (no extension) a resolved expression is stored
        // under: the enum member, lowercased -- "Surprised" -> "surprised".
        public static string FileNameOf(DialogueExpression expression) =>
            expression.ToString().ToLowerInvariant();

        // The first Resources path in the fallback chain that `exists`
        // accepts, or "" when none does (or the character has no folder).
        // The existence test is the caller's, so the content build can ask
        // the file system and the runtime can ask Resources.Load with the
        // same walk -- the two can never disagree about which rung wins.
        public static string FirstAvailable(string bustFolder, string expression, System.Func<string, bool> exists)
        {
            if (string.IsNullOrWhiteSpace(bustFolder) || exists == null) return "";

            foreach (string candidate in Fallbacks(expression))
            {
                string path = ResourcePath(bustFolder, candidate);
                if (exists(path)) return path;
            }

            return "";
        }
    }
}
