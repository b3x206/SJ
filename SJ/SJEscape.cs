using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SJ
{
    /// <summary>
    /// Provides the basic JSON string escaping algorithm. (since JSON escapes are UTF-16 and the code is somewhat trivial)
    /// </summary>
    public static class SJEscape
    {
        private static readonly byte[] hex2IntTable;
        private enum EscapeTableAction : byte
        {
            None = 0,                  // No escape
            // ...                     // Anything as the target char value, within sbyte.MaxValue
            Self = sbyte.MaxValue + 1, // Escape as \\self
            Hex                        // Escape as \u4HEXDIG
        }
        private static readonly EscapeTableAction[] escapeTable;
        static SJEscape()
        {
            // Hex2IntTable
            const byte AsciiTableMax = sbyte.MaxValue + 1;
            hex2IntTable = new byte[AsciiTableMax];
            Array.Fill(hex2IntTable, byte.MaxValue); // Because 0 is a valid value
            for (int i = '0'; i <= '9'; i++) hex2IntTable[i] = (byte)(i - '0');
            for (int i = 'a'; i <= 'f'; i++) hex2IntTable[i] = (byte)(10 + (i - 'a'));
            for (int i = 'A'; i <= 'f'; i++) hex2IntTable[i] = (byte)(10 + (i - 'A'));

            // EscapeTable
            escapeTable = new EscapeTableAction[AsciiTableMax];
            // JSON only cares escaping certain control ranges, not the unicode stuff
            for (int i = 0; i < 0x1F; i++) escapeTable[i] = EscapeTableAction.Hex;
            // The other escapes
            escapeTable[(byte)'\\'] = EscapeTableAction.Self;
            escapeTable[(byte)'"'] = EscapeTableAction.Self;
            escapeTable[(byte)'\b'] = (EscapeTableAction)'b';
            escapeTable[(byte)'\f'] = (EscapeTableAction)'f';
            escapeTable[(byte)'\n'] = (EscapeTableAction)'n';
            escapeTable[(byte)'\r'] = (EscapeTableAction)'r';
            escapeTable[(byte)'\t'] = (EscapeTableAction)'t';

            // TODO : The other way, UnescapeTable
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
        private static void SBAppendAction(StringBuilder sb, char c) => sb.Append(c);

        /// <summary>
        /// Escapes <paramref name="content"/> according to JSON. 
        /// Does not surround <paramref name="content"/> with '"' character, but escapes for that.
        /// </summary>
        /// <param name="appendAction">Action called when a character is to be appended into the arbitrary buffer.</param>
        /// <param name="content">Content to escape, this mustn't have broken surrogate pairs.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns>Count of written characters.</returns>
        public static int Escape<TSelf>(TSelf self, Action<TSelf, char> appendAction, ReadOnlySpan<char> content, bool asciiOnly = false)
        {
            if (appendAction is null)
            {
                throw new ArgumentNullException(nameof(appendAction));
            }

            int count = 0;
            Span<char> hex = stackalloc char[4];

            // https://www.ietf.org/rfc/rfc4627.txt
            for (int i = 0; i < content.Length; i++)
            {
                char cur = content[i];
                char next = (i < (content.Length - 1)) ? content[i + 1] : '\0';
                if (cur < escapeTable.Length)
                {
                    switch (escapeTable[cur])
                    {
                        case EscapeTableAction.None:
                            break;

                        default:
                            appendAction(self, '\\');
                            appendAction(self, (char)escapeTable[cur]);
                            count += 2;
                            break;

                        case EscapeTableAction.Self:
                            appendAction(self, '\\');
                            appendAction(self, cur);
                            count += 2;
                            break;

                        case EscapeTableAction.Hex:
                            // write as : \uXXXX
                            To4DigitHex(cur, ref hex);
                            appendAction(self, '\\');
                            appendAction(self, 'u');
                            count += 2;
                            for (int j = 0; j < hex.Length; j++)
                            {
                                appendAction(self, hex[j]);
                                count++;
                            }
                            break;
                    }
                }
                else if (asciiOnly)
                {
                    // write as : \uXXXX
                    To4DigitHex(cur, ref hex);
                    appendAction(self, '\\');
                    appendAction(self, 'u');
                    count += 2;
                    for (int j = 0; j < hex.Length; j++)
                    {
                        appendAction(self, hex[j]);
                        count++;
                    }
                }
                else
                {
                    // Range outside of ascii
                    appendAction(self, cur);
                    count++;
                }
            }

            return count;
        }
        /// <inheritdoc cref="Escape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char}, bool)"/>
        public static string Escape(ReadOnlySpan<char> content, bool asciiOnly = false)
        {
            var sb = new StringBuilder(content.Length + (content.Length / 2)); // Content is "shorter"
            Escape(sb, SBAppendAction, content, asciiOnly);
            return sb.ToString();
        }
        /// <inheritdoc cref="Escape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char}, bool)"/>
        public static string Escape(string content, bool asciiOnly = false) =>
            Escape(content.AsSpan(), asciiOnly);

        /// <summary>
        /// Unescapes <paramref name="content"/> with escapes.
        /// </summary>
        /// <remarks>
        /// This is more permissive on what it accepts, as it will accept invalid escapes like "\\k" and unescape it like "k"
        /// </remarks>
        /// <param name="appendAction">Action called when a character is to be appended into the arbitrary buffer.</param>
        /// <param name="content">Content to unescape.</param>
        /// <returns>Count of written characters.</returns>
        public static int Unescape<TSelf>(TSelf self, Action<TSelf, char> appendAction, ReadOnlySpan<char> content)
        {
            if (appendAction is null)
            {
                throw new ArgumentNullException(nameof(appendAction));
            }

            int count = 0;
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
                        count++;
                        break;
                    }
                    cur = content[i];

                    switch (cur)
                    {
                        case '"':
                        case '\\':
                            appendAction(self, cur);
                            count++;
                            continue;

                        case 'b':
                            appendAction(self, '\b');
                            count++;
                            continue;
                        case 'f':
                            appendAction(self, '\f');
                            count++;
                            continue;
                        case 'n':
                            appendAction(self, '\n');
                            count++;
                            continue;
                        case 'r':
                            appendAction(self, '\r');
                            count++;
                            continue;
                        case 't':
                            appendAction(self, '\t');
                            count++;
                            continue;

                        case 'u':
                            {
                                // Strictly expect 4 digits
                                const int TargetDelta = 5; // length(u1234)
                                char result = '\0';
                                int start = i;
                                i++;

                                while (i < content.Length && (i - start) < TargetDelta)
                                {
                                    cur = content[i];
                                    // Character is within the range
                                    if (cur >= hex2IntTable.Length)
                                    {
                                        break;
                                    }
                                    // Whether if digit is valid
                                    int digit = hex2IntTable[cur];
                                    if (digit == 0xFF)
                                    {
                                        break;
                                    }

                                    result = (char)((result << 4) | digit);
                                    i++;
                                }

                                if ((i - start) == TargetDelta)
                                {
                                    // UTF-16 point
                                    appendAction(self, (char)(result & char.MaxValue));
                                    count++;
                                }
                                // otherwise escape as is
                                else
                                {
                                    i -= TargetDelta; // i will be incremented beyond 'u'
                                    appendAction(self, cur);
                                    count++;
                                }

                                continue;
                            }

                        default:
                            appendAction(self, cur);
                            count++;
                            continue;
                    }
                }
                else
                {
                    appendAction(self, cur);
                    count++;
                }
            }

            return count;
        }
        /// <inheritdoc cref="Unescape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char})"/>
        public static string Unescape(ReadOnlySpan<char> content)
        {
            var sb = new StringBuilder(content.Length); // Content is "longer"
            Unescape(sb, SBAppendAction, content);
            return sb.ToString();
        }
        /// <inheritdoc cref="Unescape{TSelf}(TSelf, Action{TSelf, char}, ReadOnlySpan{char})"/>
        public static string Unescape(string content) =>
            Unescape(content.AsSpan());
    }
}
