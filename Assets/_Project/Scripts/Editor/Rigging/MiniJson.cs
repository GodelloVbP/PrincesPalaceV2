using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Minimal recursive-descent JSON reader for rig.json.
//
// JsonUtility cannot parse this file's shape: "outline" is a list of
// [x,y] pairs (a jagged array) and "weights" is a list of lists of
// objects -- both are shapes JsonUtility silently fails on rather than
// errors on, which is worse than not having a parser at all. This is a
// closed, self-contained reader against a schema this project's own Python
// tool (tools/rig_actor.py) produces, not a general-purpose library.
namespace PrincesPalace.Editor.Rigging
{
    public abstract class JsonValue
    {
        public virtual Dictionary<string, JsonValue> AsObject() => throw new System.InvalidOperationException("not an object");
        public virtual List<JsonValue> AsArray() => throw new System.InvalidOperationException("not an array");
        public virtual double AsNumber() => throw new System.InvalidOperationException("not a number");
        public virtual string AsString() => throw new System.InvalidOperationException("not a string");

        public JsonValue this[string key] => AsObject()[key];
        public JsonValue this[int index] => AsArray()[index];
        public int Count => AsArray().Count;
        public int AsInt() => (int)System.Math.Round(AsNumber());
    }

    internal sealed class JsonObject : JsonValue
    {
        private readonly Dictionary<string, JsonValue> _v;
        public JsonObject(Dictionary<string, JsonValue> v) => _v = v;
        public override Dictionary<string, JsonValue> AsObject() => _v;
    }

    internal sealed class JsonArray : JsonValue
    {
        private readonly List<JsonValue> _v;
        public JsonArray(List<JsonValue> v) => _v = v;
        public override List<JsonValue> AsArray() => _v;
    }

    internal sealed class JsonNumber : JsonValue
    {
        private readonly double _v;
        public JsonNumber(double v) => _v = v;
        public override double AsNumber() => _v;
    }

    internal sealed class JsonString : JsonValue
    {
        private readonly string _v;
        public JsonString(string v) => _v = v;
        public override string AsString() => _v;
    }

    public static class MiniJson
    {
        public static JsonValue Parse(string text)
        {
            int i = 0;
            var result = ParseValue(text, ref i);
            SkipWhitespace(text, ref i);
            return result;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return new JsonString(ParseString(s, ref i));
            if (c == 't') { i += 4; return new JsonNumber(1); }
            if (c == 'f') { i += 5; return new JsonNumber(0); }
            if (c == 'n') { i += 4; return null;}
            return ParseNumber(s, ref i);
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, JsonValue>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (s[i] == '}') { i++; return new JsonObject(dict); }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                i++; // :
                var val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
            }
            return new JsonObject(dict);
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            var list = new List<JsonValue>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (s[i] == ']') { i++; return new JsonArray(list); }
            while (true)
            {
                var val = ParseValue(s, ref i);
                list.Add(val);
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
            }
            return new JsonArray(list);
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char esc = s[i];
                    switch (esc)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'u':
                            string hex = s.Substring(i + 1, 4);
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(esc); break;
                    }
                    i++;
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            i++; // closing quote
            return sb.ToString();
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            {
                i++;
            }
            string num = s.Substring(start, i - start);
            return new JsonNumber(double.Parse(num, CultureInfo.InvariantCulture));
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
