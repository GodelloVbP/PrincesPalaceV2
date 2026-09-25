using System;
using System.Collections.Generic;
using System.Globalization;

namespace PrincesPalace.Domain.Tests
{
    // ENOUGH JSON TO ASK A COMMITTED FILE A QUESTION, and no more.
    //
    // WHY NOT JsonUtility. Because a test that calls it is a test Unity has to
    // host, and three of the four files excluded from the dotnet loop today are
    // excluded for exactly that. The files these fixtures read
    // (Resources/StanceManifest.json, ContentData/skills.json,
    // Art/Sheets/hand_assembled.json) are hand-authored JSON whose shapes are
    // stable and whose keys are all scalars or one level of nesting -- a brace
    // scanner answers every question asked of them, in one file, in both hosts.
    //
    // WHY NOT A REGEX PER FIXTURE. HandAssembledArtTests has one and it is
    // fine; a second and a third would be three parsers that agree until one of
    // them meets a nested object. Splitting on brace DEPTH, with strings and
    // escapes respected, is the part that is easy to get subtly wrong once and
    // then copy.
    //
    // WHAT IT DELIBERATELY IS NOT: a JSON parser. It does not validate, it does
    // not resolve types, and a key lookup finds the FIRST occurrence at any
    // depth inside the block it is given -- so ask it for a key that is unique
    // within that block, or narrow the block first with ObjectFor.
    internal static class JsonBlocks
    {
        // Every top-level {...} of the array at `arrayKey`, as raw text.
        internal static List<string> ObjectsInArray(string body, string arrayKey)
        {
            var blocks = new List<string>();

            int at = body.IndexOf("\"" + arrayKey + "\"", StringComparison.Ordinal);
            if (at < 0)
            {
                return blocks;
            }

            at = body.IndexOf('[', at);
            if (at < 0)
            {
                return blocks;
            }

            int depth = 0;
            int start = -1;
            bool inString = false;
            bool escaped = false;

            for (int i = at; i < body.Length; i++)
            {
                char c = body[i];

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        blocks.Add(body.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    break;
                }
            }

            return blocks;
        }

        // The keys of the object at `key`, at ITS OWN depth only.
        //
        // Depth matters here rather than being pedantry: hand_assembled.json
        // holds two blocks side by side, and a scan that ran to the end of the
        // file would read the second block's field names as the first block's
        // ids. That is not hypothetical -- it is what the regex this replaced
        // would have done the moment a second block was added below it.
        internal static List<string> KeysOfObject(string body, string key)
        {
            var keys = new List<string>();
            string block = ObjectFor(body, key);
            if (block == null)
            {
                return keys;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int stringStart = -1;

            for (int i = 0; i < block.Length; i++)
            {
                char c = block[i];

                if (inString)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c != '"') continue;

                    inString = false;
                    if (depth != 1) continue;

                    // A string at depth 1 is a key only if a colon follows it.
                    int after = i + 1;
                    while (after < block.Length && char.IsWhiteSpace(block[after])) after++;
                    if (after < block.Length && block[after] == ':')
                    {
                        keys.Add(block.Substring(stringStart + 1, i - stringStart - 1));
                    }

                    continue;
                }

                if (c == '"') { inString = true; stringStart = i; }
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }

            return keys;
        }

        // The {...} value of `key` inside `block`, or null when the key is absent.
        internal static string ObjectFor(string block, string key)
        {
            int at = block.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            at = block.IndexOf('{', at);
            if (at < 0)
            {
                return null;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = at; i < block.Length; i++)
            {
                char c = block[i];

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return block.Substring(at, i - at + 1);
                    }
                }
            }

            return null;
        }

        // The string value of `key`, or null when absent. Only the escapes this
        // project's own files actually use are undone.
        internal static string String(string block, string key)
        {
            int at = ValueAt(block, key);
            if (at < 0 || block[at] != '"')
            {
                return null;
            }

            var text = new System.Text.StringBuilder();
            for (int i = at + 1; i < block.Length; i++)
            {
                char c = block[i];
                if (c == '\\' && i + 1 < block.Length)
                {
                    char next = block[++i];
                    text.Append(next == 'n' ? '\n' : next == 't' ? '\t' : next);
                    continue;
                }

                if (c == '"') break;
                text.Append(c);
            }

            return text.ToString();
        }

        // The numeric value of `key`, or null when absent or not a number.
        internal static double? Number(string block, string key)
        {
            int at = ValueAt(block, key);
            if (at < 0)
            {
                return null;
            }

            int end = at;
            while (end < block.Length && (char.IsDigit(block[end]) || block[end] == '-'
                                          || block[end] == '+' || block[end] == '.'
                                          || block[end] == 'e' || block[end] == 'E'))
            {
                end++;
            }

            if (end == at)
            {
                return null;
            }

            return double.TryParse(block.Substring(at, end - at), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value)
                ? value
                : (double?)null;
        }

        // The boolean value of `key`, or null when absent or not a boolean.
        // JsonUtility writes a bool unquoted, so String cannot read one and a
        // Number lookup would answer null on the word rather than on the miss.
        internal static bool? Bool(string block, string key)
        {
            int at = ValueAt(block, key);
            if (at < 0)
            {
                return null;
            }

            if (string.CompareOrdinal(block, at, "true", 0, 4) == 0) return true;
            if (string.CompareOrdinal(block, at, "false", 0, 5) == 0) return false;
            return null;
        }

        internal static bool HasKey(string block, string key)
        {
            return ValueAt(block, key) >= 0;
        }

        // The array of numbers at `key`, or null when absent or not an array.
        // Only what SpellEmitter.weights needs: a flat JSON array of plain
        // numbers, no nested objects or arrays inside it -- ObjectsInArray
        // already owns the harder case of an array of {...} blocks.
        internal static List<double> Numbers(string block, string key)
        {
            int at = ValueAt(block, key);
            if (at < 0 || block[at] != '[')
            {
                return null;
            }

            int depth = 0;
            int end = -1;

            for (int i = at; i < block.Length; i++)
            {
                char c = block[i];
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }

            if (end < 0)
            {
                return null;
            }

            var values = new List<double>();
            foreach (string piece in block.Substring(at + 1, end - at - 1).Split(','))
            {
                string trimmed = piece.Trim();
                if (trimmed.Length == 0) continue;

                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        // The array of plain strings at `key`, or an empty list when absent or
        // not an array. Only what a recipe's `"names": ["f0", "f1", ...]` needs
        // -- a flat array of quoted strings, no nested objects inside it, same
        // scope as Numbers above but for the type recipes actually use it for.
        internal static List<string> Strings(string block, string key)
        {
            var values = new List<string>();

            int at = ValueAt(block, key);
            if (at < 0 || block[at] != '[')
            {
                return values;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int stringStart = -1;

            for (int i = at; i < block.Length; i++)
            {
                char c = block[i];

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"')
                    {
                        inString = false;
                        values.Add(block.Substring(stringStart, i - stringStart));
                    }
                    continue;
                }

                if (c == '"') { inString = true; stringStart = i + 1; continue; }
                if (c == '[') depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0) break;
                }
            }

            return values;
        }

        // Where `key`'s value starts, skipping the colon and any whitespace.
        // The quotes around the key are part of the search, which is what stops
        // "groundLine" matching "_groundLineNote".
        private static int ValueAt(string block, string key)
        {
            string needle = "\"" + key + "\"";
            int at = 0;

            while (true)
            {
                at = block.IndexOf(needle, at, StringComparison.Ordinal);
                if (at < 0)
                {
                    return -1;
                }

                int i = at + needle.Length;
                while (i < block.Length && char.IsWhiteSpace(block[i])) i++;
                if (i < block.Length && block[i] == ':')
                {
                    i++;
                    while (i < block.Length && char.IsWhiteSpace(block[i])) i++;
                    return i;
                }

                at += needle.Length;
            }
        }
    }
}
