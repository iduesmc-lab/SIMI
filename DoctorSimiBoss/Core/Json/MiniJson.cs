using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DoctorSimiBoss.Core.Json
{
    /// <summary>
    /// Minimal JSON reader with no external dependencies.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers double, plus string, bool and null.
    /// It exists so the boss core does not depend on a JSON library whose presence
    /// in the game's Managed folder has not been verified.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var reader = new Reader(json);
            reader.SkipWhitespace();
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw reader.Error("Unexpected trailing content");
            return value;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text)
            {
                _text = text;
                // Tolerate a UTF-8 BOM decoded as U+FEFF.
                if (_text.Length > 0 && _text[0] == '﻿') _pos = 1;
            }

            public bool AtEnd => _pos >= _text.Length;

            public FormatException Error(string message)
            {
                int line = 1, column = 1;
                for (int i = 0; i < _pos && i < _text.Length; i++)
                {
                    if (_text[i] == '\n') { line++; column = 1; }
                    else column++;
                }
                return new FormatException(message + " at line " + line + ", column " + column);
            }

            public void SkipWhitespace()
            {
                while (!AtEnd && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            public object ReadValue()
            {
                if (AtEnd) throw Error("Unexpected end of JSON");
                char c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private void ExpectLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0)
                    throw Error("Expected '" + literal + "'");
                _pos += literal.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++; // {
                SkipWhitespace();
                if (!AtEnd && _text[_pos] == '}') { _pos++; return result; }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _text[_pos] != '"') throw Error("Expected property name");
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _text[_pos] != ':') throw Error("Expected ':'");
                    _pos++;
                    SkipWhitespace();
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated object");
                    char c = _text[_pos++];
                    if (c == '}') return result;
                    if (c != ',') throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (!AtEnd && _text[_pos] == ']') { _pos++; return result; }

                while (true)
                {
                    SkipWhitespace();
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated array");
                    char c = _text[_pos++];
                    if (c == ']') return result;
                    if (c != ',') throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _pos++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = _text[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }

                    if (AtEnd) throw Error("Unterminated escape");
                    char e = _text[_pos++];
                    switch (e)
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
                            if (_pos + 4 > _text.Length) throw Error("Invalid unicode escape");
                            string hex = _text.Substring(_pos, 4);
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Error("Invalid unicode escape");
                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            throw Error("Invalid escape '\\" + e + "'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _pos;
                if (_text[_pos] == '-') _pos++;
                while (!AtEnd && "0123456789+-.eE".IndexOf(_text[_pos]) >= 0) _pos++;
                string token = _text.Substring(start, _pos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error("Invalid number '" + token + "'");
                return value;
            }
        }
    }
}
