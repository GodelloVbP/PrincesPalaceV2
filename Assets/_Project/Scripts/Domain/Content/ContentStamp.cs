using System;
using System.Collections.Generic;
using System.Text;

namespace PrincesPalace.Domain.Content
{
    // WHAT A COMPLETED CONTENT BUILD LEAVES BEHIND.
    //
    // ContentBuilder writes one of these to Resources/Content/content_stamp.json
    // as its very last act, past every asset and past validation. Everything
    // that wants to know whether the generated tree can be trusted reads it.
    // The claim it supports is deliberately narrow and worth stating exactly:
    //
    //   the generated tree was produced from THESE inputs by THIS builder,
    //   and the build ran to completion.
    //
    // It does NOT detect a hand-edit to an asset's contents. Nothing here
    // hashes the outputs. The "content is generated, never hand-edited" rule
    // and the destructive rebuild are what cover that, and adding per-asset
    // hashing would be a much larger promise than the one this file makes.
    //
    // IN DOMAIN, AND WRITTEN AND READ HERE RATHER THAN THROUGH JsonUtility.
    // The producer is Editor code and the consumer is an EditMode test that
    // runs under `dotnet test` with no Unity at all -- and JsonUtility is the
    // one thing the fast host cannot have (see the three Compile Remove
    // entries in PrincesPalace.Domain.Tests.csproj, every one of them there
    // for exactly this). A format whose writer and reader sit in the same
    // engine-free file cannot drift between the two hosts, which is the whole
    // reason this is not simply a [Serializable] class.
    public sealed class ContentStamp
    {
        // Hex sha256 of every input ContentBuilder reads. See ContentInputHash
        // for the file set and why it is drawn that wide.
        public string InputHash { get; set; }

        // Folder leaf under Resources/Content -> the sorted asset names
        // written into it. Asset NAMES, not record ids, and the distinction
        // matters for exactly one type: a spell tier has no id of its own and
        // is written as "level_3", so names are what a reader can check
        // against the files on disk. For the other ten the name IS the id.
        public SortedDictionary<string, List<string>> IdsByFolder { get; }
            = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        public string Render()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"inputHash\": \"").Append(InputHash ?? "").Append("\",\n");
            sb.Append("  \"types\": {\n");

            bool firstType = true;
            foreach (var pair in IdsByFolder)
            {
                if (!firstType) sb.Append(",\n");
                firstType = false;

                sb.Append("    \"").Append(pair.Key).Append("\": [");
                var ids = new List<string>(pair.Value);
                ids.Sort(StringComparer.Ordinal);
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('"').Append(ids[i]).Append('"');
                }

                sb.Append(']');
            }

            sb.Append("\n  }\n}\n");
            return sb.ToString();
        }

        // A SCANNER FOR THE ONE SHAPE Render EMITS, not a JSON library.
        //
        // Deliberately narrow: content ids and folder names are
        // [A-Za-z0-9_] by every resolver's own rules, so there are no escapes
        // to handle and no numbers, nulls or nesting to walk. Anything it does
        // not recognise throws with the offset rather than defaulting -- a
        // freshness check that silently read an unparseable stamp as "empty"
        // would report every asset as unlisted and blame the wrong thing.
        public static ContentStamp Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException("content_stamp.json is empty.");
            }

            var stamp = new ContentStamp();
            int cursor = 0;

            stamp.InputHash = ValueAfterKey(text, "inputHash", ref cursor);

            int types = text.IndexOf("\"types\"", cursor, StringComparison.Ordinal);
            if (types < 0)
            {
                throw new FormatException("content_stamp.json has no \"types\" block.");
            }

            cursor = text.IndexOf('{', types);
            if (cursor < 0)
            {
                throw new FormatException("content_stamp.json's \"types\" is not an object.");
            }

            cursor++;

            while (true)
            {
                int quote = text.IndexOf('"', cursor);
                int close = text.IndexOf('}', cursor);
                if (quote < 0 || (close >= 0 && close < quote))
                {
                    break;
                }

                string folder = ReadString(text, ref quote);
                cursor = quote;

                int open = text.IndexOf('[', cursor);
                int end = text.IndexOf(']', cursor);
                if (open < 0 || end < 0 || end < open)
                {
                    throw new FormatException($"content_stamp.json's \"{folder}\" has no id array.");
                }

                var ids = new List<string>();
                int scan = open + 1;
                while (scan < end)
                {
                    int idQuote = text.IndexOf('"', scan);
                    if (idQuote < 0 || idQuote > end) break;
                    ids.Add(ReadString(text, ref idQuote));
                    scan = idQuote;
                }

                stamp.IdsByFolder[folder] = ids;
                cursor = end + 1;
            }

            return stamp;
        }

        private static string ValueAfterKey(string text, string key, ref int cursor)
        {
            int at = text.IndexOf("\"" + key + "\"", cursor, StringComparison.Ordinal);
            if (at < 0)
            {
                throw new FormatException($"content_stamp.json has no \"{key}\".");
            }

            int colon = text.IndexOf(':', at);
            if (colon < 0)
            {
                throw new FormatException($"content_stamp.json's \"{key}\" has no value.");
            }

            int quote = text.IndexOf('"', colon);
            if (quote < 0)
            {
                throw new FormatException($"content_stamp.json's \"{key}\" is not a string.");
            }

            string value = ReadString(text, ref quote);
            cursor = quote;
            return value;
        }

        // Reads the string starting at the opening quote `at` points to, and
        // leaves `at` one past the closing quote.
        private static string ReadString(string text, ref int at)
        {
            int end = text.IndexOf('"', at + 1);
            if (end < 0)
            {
                throw new FormatException($"content_stamp.json has an unterminated string at offset {at}.");
            }

            string value = text.Substring(at + 1, end - at - 1);
            at = end + 1;
            return value;
        }
    }
}
