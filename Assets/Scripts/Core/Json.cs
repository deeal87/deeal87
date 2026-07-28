using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SunnyStop.Core
{
    /// <summary>
    /// Minimal JSON reader.
    ///
    /// Unity's JsonUtility cannot express the level format (nested arrays for cells,
    /// optional fields), and pulling in a JSON package would put a dependency inside
    /// Core - which has to stay engine-free so the same code can be unit-tested
    /// headless in CI. 200 lines of recursive descent is the cheaper trade.
    /// </summary>
    public sealed class JsonValue
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type { get; private set; }

        private bool _bool;
        private double _number;
        private string _string;
        private List<JsonValue> _array;
        private Dictionary<string, JsonValue> _object;

        public static JsonValue Parse(string text)
        {
            int index = 0;
            JsonValue value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
                throw new FormatException($"trailing content at offset {index}");
            return value;
        }

        // ----- accessors ----------------------------------------------------- //

        public bool IsNull => Type == Kind.Null;
        public int Count => Type == Kind.Array ? _array.Count : 0;

        public JsonValue this[int i] => _array[i];

        public JsonValue this[string key] =>
            Type == Kind.Object && _object.TryGetValue(key, out JsonValue v) ? v : null;

        public bool Has(string key) => Type == Kind.Object && _object.ContainsKey(key);

        public IEnumerable<JsonValue> Items()
        {
            if (Type != Kind.Array) yield break;
            foreach (JsonValue v in _array) yield return v;
        }

        public string AsString() => Type == Kind.String
            ? _string
            : throw new FormatException($"expected string, got {Type}");

        public int AsInt() => Type == Kind.Number
            ? (int)Math.Round(_number)
            : throw new FormatException($"expected number, got {Type}");

        public bool AsBool() => Type == Kind.Bool
            ? _bool
            : throw new FormatException($"expected bool, got {Type}");

        public int IntOr(string key, int fallback) =>
            this[key] is { Type: Kind.Number } v ? v.AsInt() : fallback;

        public string StringOr(string key, string fallback) =>
            this[key] is { Type: Kind.String } v ? v.AsString() : fallback;

        public bool BoolOr(string key, bool fallback) =>
            this[key] is { Type: Kind.Bool } v ? v.AsBool() : fallback;

        // ----- parser -------------------------------------------------------- //

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
                i++;
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of input");

            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return new JsonValue { Type = Kind.String, _string = ParseString(s, ref i) };
                case 't':
                    Expect(s, ref i, "true");
                    return new JsonValue { Type = Kind.Bool, _bool = true };
                case 'f':
                    Expect(s, ref i, "false");
                    return new JsonValue { Type = Kind.Bool, _bool = false };
                case 'n':
                    Expect(s, ref i, "null");
                    return new JsonValue { Type = Kind.Null };
                default: return ParseNumber(s, ref i);
            }
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException($"expected '{literal}' at offset {i}");
            i += literal.Length;
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            var result = new JsonValue
            {
                Type = Kind.Object,
                _object = new Dictionary<string, JsonValue>()
            };
            i++; // '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }

            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    throw new FormatException($"expected ':' at offset {i}");
                i++;
                result._object[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new FormatException($"expected ',' or '}}' at offset {i}");
            }
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            var result = new JsonValue { Type = Kind.Array, _array = new List<JsonValue>() };
            i++; // '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }

            while (true)
            {
                result._array.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new FormatException($"expected ',' or ']' at offset {i}");
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
                throw new FormatException($"expected string at offset {i}");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("bad \\u escape");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: throw new FormatException($"bad escape '\\{esc}'");
                }
            }
            throw new FormatException("unterminated string");
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' ||
                                    s[i] == 'e' || s[i] == 'E' ||
                                    s[i] == '-' || s[i] == '+'))
                i++;
            if (start == i) throw new FormatException($"expected value at offset {start}");
            string slice = s.Substring(start, i - start);
            if (!double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture,
                                 out double parsed))
                throw new FormatException($"bad number '{slice}'");
            return new JsonValue { Type = Kind.Number, _number = parsed };
        }
    }
}
