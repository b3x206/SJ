using System;
using System.Text;
using System.Diagnostics;

namespace SJ
{
    /// <summary>
    /// Provides the basic JSON string escaping algorithm. (since JSON escapes are UTF-16 and the code is somewhat trivial)
    /// </summary>
    public static class SJEscape
    {
        [Flags]
        public enum EscapeOptions
        {
            /// <summary>
            /// Escape nothing, the string is written as is.
            /// </summary>
            None = 0,
            /// <summary>
            /// Escape multiple <see cref="char"/> UTF-16 sequences. (for ucs-2?)
            /// </summary>
            SurrogatePair = 1 << 0,
            /// <summary>
            /// Escape any character that is not within ASCII range.
            /// This also applies the <see cref="SurrogatePair"/>, so it isn't really necessary to include 
            /// </summary>
            NonAscii = 1 << 1,

            All = ~0,
        }

        private static int HexChar2Int(char c)
        {
            if (c >= '0' && c <= '9') { return c - '0'; }
            if (c >= 'a' && c <= 'f') { return c - 'a' + 10; }
            if (c >= 'A' && c <= 'F') { return c - 'A' + 10; }

            return -1;
        }
        private static void To4DigitHex(char c, int index, ref Span<char> chars)
        {
            Debug.Assert((chars.Length - index) >= 4);
            const string Digits = "0123456789ABCDEF";
            for (int i = 0, j = 12; i < 4; i++, j -= 4)
            {
                chars[i + index] = Digits[((c >> j) & 0xF)];
            }
        }
        private static void To4DigitHex(char c, ref Span<char> chars) => To4DigitHex(c, 0, ref chars);

        /// <summary>
        /// Escapes <paramref name="content"/> according to JSON. 
        /// Does not surround <paramref name="content"/> with '"' character, but escapes for that.
        /// </summary>
        /// <param name="appendAction">Action called when a character is to be appended into the arbitrary buffer.</param>
        /// <param name="content">Content to escape, this mustn't have broken surrogate pairs.</param>
        /// <exception cref="ArgumentException"></exception>
        public static void Escape<TSelf>(TSelf self, Action<TSelf, char> appendAction, ReadOnlySpan<char> content, EscapeOptions escapeOpts = EscapeOptions.None)
        {
            if (appendAction is null)
            {
                throw new ArgumentNullException(nameof(appendAction));
            }

            Span<char> hex = stackalloc char[4];

            // https://www.ietf.org/rfc/rfc4627.txt
            for (int i = 0; i < content.Length; i++)
            {
                char cur = content[i];
                char next = (i < (content.Length - 1)) ? content[i + 1] : '\0';
                switch (cur)
                {
                    case '"':
                        appendAction(self, '\\');
                        appendAction(self, cur);
                        continue;
                    case '\\':
                        appendAction(self, '\\');
                        appendAction(self, cur);
                        continue;
                    case '\b':
                        appendAction(self, '\\');
                        appendAction(self, 'b');
                        continue;
                    case '\f':
                        appendAction(self, '\\');
                        appendAction(self, 'f');
                        continue;
                    case '\n':
                        appendAction(self, '\\');
                        appendAction(self, 'n');
                        continue;
                    case '\r':
                        appendAction(self, '\\');
                        appendAction(self, 'r');
                        continue;
                    case '\t':
                        appendAction(self, '\\');
                        appendAction(self, 't');
                        continue;
                }

                if (char.IsControl(cur))
                {
                    // write as : \uXXXX
                    To4DigitHex(cur, ref hex);
                    appendAction(self, '\\');
                    appendAction(self, 'u');
                    for (int j = 0; j < hex.Length; j++)
                    {
                        appendAction(self, hex[j]);
                    }

                    continue;
                }

                // must be strict with output data
                // oh well let the user handle it's own mojibake. or not
                // ?? : https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/JSON/stringify#well-formed_json.stringify
                bool expectSurrogatePair = char.IsHighSurrogate(cur);
                if (expectSurrogatePair && !char.IsLowSurrogate(next))
                {
                    throw new ArgumentException("Invalid surrogate pair in given argument.", nameof(content));
                }
                if (expectSurrogatePair && (escapeOpts & EscapeOptions.SurrogatePair) == EscapeOptions.SurrogatePair)
                {
                    // write as : \uXXXX\uXXXX
                    To4DigitHex(cur, ref hex);
                    appendAction(self, '\\');
                    appendAction(self, 'u');
                    for (int j = 0; j < hex.Length; j++)
                    {
                        appendAction(self, hex[j]);
                    }

                    To4DigitHex(next, ref hex);
                    appendAction(self, '\\');
                    appendAction(self, 'u');
                    for (int j = 0; j < hex.Length; j++)
                    {
                        appendAction(self, hex[j]);
                    }

                    // iterate twice for pair
                    i++;
                    continue;
                }

                if (cur > sbyte.MaxValue && (escapeOpts & EscapeOptions.NonAscii) == EscapeOptions.NonAscii)
                {
                    // write as : \uXXXX
                    To4DigitHex(cur, ref hex);
                    appendAction(self, '\\');
                    appendAction(self, 'u');
                    for (int j = 0; j < hex.Length; j++)
                    {
                        appendAction(self, hex[j]);
                    }

                    continue;
                }

                appendAction(self, cur);
            }
        }
        /// <inheritdoc cref="Escape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char}, EscapeOptions)"/>
        public static string Escape(ReadOnlySpan<char> content, EscapeOptions escapeOpts = EscapeOptions.None)
        {
            var sb = new StringBuilder(content.Length + (content.Length / 2)); // Content is "shorter"
            Escape(sb, (s, c) => s.Append(c), content, escapeOpts);
            return sb.ToString();
        }
        /// <inheritdoc cref="Escape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char}, EscapeOptions)"/>
        public static string Escape(string content, EscapeOptions escapeOpts = EscapeOptions.None) =>
            Escape(content.AsSpan(), escapeOpts);

        /// <summary>
        /// Unescapes <paramref name="content"/> with escapes.
        /// </summary>
        /// <param name="appendAction">Action called when a character is to be appended into the arbitrary buffer.</param>
        /// <param name="content">Content to unescape.</param>
        public static void Unescape<TSelf>(TSelf self, Action<TSelf, char> appendAction, ReadOnlySpan<char> content)
        {
            if (appendAction is null)
            {
                throw new ArgumentNullException(nameof(appendAction));
            }

            // This method is "more lenient" about incorrect escape characters.
            for (int i = 0; i < content.Length; i++)
            {
                char cur = content[i];

                if (cur == '\\')
                {
                    i++;
                    if (i >= content.Length)
                    {
                        appendAction(self, cur);
                        continue;
                    }

                    cur = content[i];
                    char next = (i < (content.Length - 1)) ? content[i + 1] : '\0';

                    switch (cur)
                    {
                        case '"':
                        case '\\':
                            appendAction(self, cur);
                            continue;

                        case 'b':
                            appendAction(self, '\b');
                            continue;
                        case 'f':
                            appendAction(self, '\f');
                            continue;
                        case 'n':
                            appendAction(self, '\n');
                            continue;
                        case 'r':
                            appendAction(self, '\r');
                            continue;
                        case 't':
                            appendAction(self, '\t');
                            continue;

                        case 'u':
                            {
                                // Strictly expect 4 digits
                                // Otherwise the escaper, especially with ascii, logic breaks
                                char result = '\0';
                                next = char.ToUpper(next);
                                int nDigit = 0;
                                for (int di = HexChar2Int(next); nDigit < 4 && di >= 0 && (nDigit + i) < content.Length; nDigit++)
                                {
                                    result = (char)((result << 4) | di);

                                    // cur = content[nDigit + i + 1];
                                    next = char.ToUpper(((nDigit + i + 1) < (content.Length - 1)) ? content[(nDigit + i + 1) + 1] : '\0');
                                    di = HexChar2Int(next);
                                }

                                // note : nDigit becomes 4 because the condition normally stops at 4, which is target
                                if (nDigit == 4)
                                {
                                    // UTF-16 point
                                    appendAction(self, (char)(result & char.MaxValue));
                                    i += nDigit;
                                }
                                else
                                {
                                    appendAction(self, cur);
                                }

                                continue;
                            }

                        default:
                            appendAction(self, cur);
                            continue;
                    }
                }

                appendAction(self, cur);
            }
        }
        /// <inheritdoc cref="Unescape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char})"/>
        public static string Unescape(ReadOnlySpan<char> content)
        {
            var sb = new StringBuilder(content.Length); // Content is "longer"
            Unescape(sb, (s, c) => s.Append(c), content);
            return sb.ToString();
        }
        /// <inheritdoc cref="Unescape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char})"/>
        public static string Unescape(string content) =>
            Unescape(content.AsSpan());
    }
}
