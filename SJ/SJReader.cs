using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SJ
{
    /// <summary>
    /// Inferred type of the read JSON.
    /// </summary>
    public enum SJType
    {
        Error,

        Number,
        String,
        Bool,
        Null,

        Object,
        Array,
        End,
        Comment
    }

    /// <summary>
    /// Base class for <i>most</i> JSON readers.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// using SJ;
    /// using System;
    /// 
    /// // An example class would look like this:
    /// // (In fact, this is just SJStringReader)
    /// public sealed class SJExampleReader : SJReader
    /// {
    ///     private string _Data;
    ///     public string Data
    ///     {
    ///         [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ///         get => _Data;
    ///         set
    ///         {
    ///             if (string.Equals(value, _Data, StringComparison.Ordinal))
    ///             {
    ///                 return;
    ///             }
    /// 
    ///             _Data = value;
    ///             Reset();
    ///         }
    ///     }
    ///
    ///     public SJExampleReader()
    ///     { }
    ///     public SJExampleReader(string data)
    ///     {
    ///         _Data = data;
    ///     }
    /// 
    ///     // This is used as a limiter.
    ///     public override int Length => _Data?.Length ?? 0;
    ///     // Because I ported the pointer stuff as-is, some parts of the parser may read off by one.
    ///     // It is recommended to do an bound check and return EOF of your choice
    ///     protected override char At(int i) => i >= 0 && i < Length ? _Data[i] : '\0';
    ///     // Because each value slice is evaluated lazily, the data ranges must persist and should be representable easily as a range (making arbitrary Streams much harder)
    ///     // Note that there isn't much of a reason to do this, if you read the data as soon as it's received from the SJReader.
    ///     protected override ReadOnlySpan<char> Slice(int start, int length) => string.IsNullOrEmpty(_Data) ? ReadOnlySpan<char>.Empty : _Data.AsSpan(start, length);
    /// }
    /// ]]>
    /// </example>
    public abstract class SJReader
    {
        // TODO:
        // * Generic class (with the character unit type), or to avoid boxing, byte version that does ASCII (ignores UTF-8 but should work)
        // * SJType.Comment captures [explicit Read()] and ignoreCommentParsing [auto skip with yielded Read()]

        /// <summary>
        /// Exception thrown on an error case while reading JSON.
        /// </summary>
        public class ReadException : Exception
        {
            public ReadException(SJReader reader) : base($"{reader.Error}{(reader.Location(out int line, out int col) ? $" | at line={line} col={col}" : "")}")
            { }
            public ReadException(string message, int line, int col) : base($"{message} | at line={line} col={col}")
            { }
            public ReadException(string message) : base(message)
            { }
        }

        /// <summary>
        /// Value result from <see cref="Read"/>.
        /// </summary>
        public struct Value
        {
            public readonly SJReader reader;
            public SJType type;
            public int start, end;
            public int depth;

            public Value(SJReader reader)
            {
                this.reader = reader;
                type = SJType.Error;
                start = end = 0;
                depth = 0;
            }
            public Value(SJReader reader, SJType type, int start, int end, int depth)
            {
                this.reader = reader;
                this.type = type;
                this.start = start;
                this.end = end;
                this.depth = depth;
            }

            public static Value Error() => new Value(null, SJType.Error, -1, -1, -1);
            public static Value Error(SJReader reader) => new Value(reader, SJType.Error, -1, -1, -1);
            public static Value Null() => new Value(null, SJType.Null, -1, -1, -1);
            public static Value Null(SJReader reader) => new Value(reader, SJType.Null, -1, -1, -1);

            public readonly int Length
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => end - start;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly char At(int i) => reader.At(start + i);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<char> Slice() => reader.Slice(start, end - start);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<char> Slice(int start)
            {
                Debug.Assert(start < Length);
                return reader.Slice(this.start + start, Length - start);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ReadOnlySpan<char> Slice(int start, int length)
            {
                Debug.Assert((start + length) < Length);
                return reader.Slice(this.start + start, length);
            }

            public override readonly string ToString()
            {
                return $"[SJReader.Value] type:{type}, start:{start}, end:{end}, depth:{depth}";
            }
        }

        // config
        /// <summary>
        /// Maximum read depth as generally recursion is used to read.
        /// </summary>
        public int maxDepth = 128;
        /// <summary>
        /// <br>Allows comments on JSON file : </br><br/>
        /// <br>// &lt;-- single line</br>
        /// <br>/* &lt;-- until end token --&gt; */</br>
        /// </summary>
        public bool allowComments = false;
        /// <summary>
        /// Captured JSON comments are skipped
        /// <br>(they are not pushed as JSON values from <see cref="Read"/> method)</br>
        /// </summary>
        public bool ignoreCapturedComments = true;
        protected bool _ThrowOnError = false;
        /// <summary>
        /// When an erroreneous case occurs (or <see cref="Error"/> is set to anything other 
        /// than null/<see cref="string.Empty"/>), setting the <see cref="Error"/> will throw.
        /// <br>Setting this <see langword="true"/> while the <see cref="Error"/> 
        /// is not null will throw the currently existing error.</br>
        /// </summary>
        public bool ThrowOnError
        {
            get => _ThrowOnError;
            set
            {
                _ThrowOnError = value;

                if (_ThrowOnError && !string.IsNullOrEmpty(Error))
                {
                    ThrowError();
                }
            }
        }

        // state
        public int current = 0;
        public int depth = 0;
        protected string _Error = string.Empty;
        /// <summary>
        /// The error string detailing why the reader failed.
        /// </summary>
        public string Error
        {
            get => _Error;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _Error = string.Empty;
                    return;
                }

                _Error = value;
                if (_ThrowOnError)
                {
                    ThrowError();
                }
            }
        }
        public Stack<SJType> lastRecursableTypes;
        /// <summary>
        /// State for checking colons in <see cref=""/>
        /// </summary>
        protected int _ColonsRequired = -1;

        /// <summary>
        /// Length of the data to read.
        /// </summary>
        public abstract int Length { get; }
        /// <summary>
        /// Get a character at <paramref name="i"/>.
        /// <br><b>Note:</b> This can receive OOB reads, generally at <paramref name="i"/> == <see cref="Length"/></br>
        /// <br>In this case, it is necessary to handle that exact condition with '\0' / EOF character of your choice.</br>
        /// </summary>
        protected abstract char At(int i);
        /// <summary>
        /// Create a "char" slice, starting from <paramref name="start"/> with <paramref name="length"/>.
        /// </summary>
        protected abstract ReadOnlySpan<char> Slice(int start, int length);

        /// <summary>
        /// <see cref="SJType.Number"/> continuation check, Note that this reader does no parsing or unescaping.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool IsNumberContinuation(char c) => (c >= '0' && c <= '9') || c == 'e' || c == 'E' || c == '.' || c == '-' || c == '+';
        /// <summary>
        /// String inspector for token.
        /// <br>Outputs <paramref name="c"/> as <c>{c}/{(int)c}</c></br>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static string Tk(char c) => c == '\0' ? "\\0/0" : $"{c}/{(int)c}";

        /// <summary>
        /// Validates and skips comment. This ignores the <see cref="allowComments"/> and parses the comment.
        /// <br><b>Note : </b> This method only skips and validates comments, if the character at <see cref="current"/> is '/'.
        /// It will fail on white space intentionally, but won't set error (because it isn't comment).
        /// You should check <c>At(current) == '/' &amp;&amp; <see cref="SkipComment"/></c></br>
        /// </summary>
        /// <returns>Whether if the comment block was valid. If <see cref="ThrowOnError"/> is set, this method will throw.</returns>
        protected bool SkipComment()
        {
            if (current < Length)
            {
                // I should validate the current character twice, for external uses.
                char ch = At(current);
                if (ch == '/')
                {
                    char next = At(++current);
                    if (next == '/')
                    {
                        while (ch != '\n')
                        {
                            if (current >= Length)
                            {
                                // Ended with // .... <EOF
                                return true;
                            }

                            ch = At(++current);
                        }

                        // the whitespace '\n' will be implicitly skipped.
                        return true;
                    }
                    else if (next == '*')
                    {
                        while (ch != '*' && next != '/')
                        {
                            if (current >= Length)
                            {
                                Error = $"Expected */, got '{Tk(ch)}+{Tk(next)}'";
                                return false;
                            }

                            ch = At(++current);
                            next = At(current + 1);
                        }

                        // skip the '*/' (as those are actual non-ws chars)
                        current += 2;
                        return true;
                    }
                    else
                    {
                        Error = $"Unexpected comment token '{Tk(next)}', expected '/ or *'";
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            // Nothing to skip
            return true;
        }
        /// <summary>
        /// Check remaining tokens after the JSON document is finished. (depth == 0)
        /// <br>Does parsing of comments if <see cref="allowComments"/></br>
        /// </summary>
        /// <returns>
        /// <see langword="false"/> if the ending has invalid characters. <see langword="true"/> if the document ending is valid.
        /// The <see cref="Error"/> value is assigned with relevant detail.
        /// </returns>
        protected bool CheckEOF()
        {
            if (depth < 0)
            {
                return false;
            }

            // assert EOF or only whitespace if depth is zero.
            for (; depth == 0 && current < Length; current++)
            {
                char ch = At(current);

                if (allowComments && ch == '/')
                {
                    if (!SkipComment())
                    {
                        Error = $"[end] {Error}";
                        return false;
                    }
                }
                else if (!char.IsWhiteSpace(ch))
                {
                    Error = $"Unexpected '{Tk(ch)}' after end of JSON document";
                    return false;
                }
            }

            return true;
        }
        /// <summary>
        /// Throws the currently stored error. It does not check whether if an error is available.
        /// </summary>
        /// <exception cref="ReadException"></exception>
        public virtual void ThrowError()
        {
            throw new ReadException(this);
        }

        // Base sj.h
        /// <summary>
        /// Top level JSON read, <see cref="SJType.Object"/> and <see cref="SJType.Array"/> requires special iteration.
        /// </summary>
        public virtual Value Read()
        {
            lastRecursableTypes ??= new Stack<SJType>();
            var result = new Value(this);
        _Top:
            if (!string.IsNullOrEmpty(Error))
            {
                result.type = SJType.Error;
                result.start = result.end = 0;
                result.depth = -1;
                return result;
            }
            if (Length <= 0)
            {
                Error = "Empty data";
                goto _Top;
            }
            if (current >= Length)
            {
                Error = "Reached EOF";
                goto _Top;
            }
            if (maxDepth > 0 && depth > maxDepth)
            {
                Error = $"Exceeded maximum depth {maxDepth}";
                goto _Top;
            }

            result.start = current;
            char ch = At(current);
            // Skip : White Space
            if (char.IsWhiteSpace(ch))
            {
                current++;
                goto _Top;
            }
            // Validate : Comments
            else if (allowComments && ch == '/')
            {
                // Perhaps I could have added a "SJType.Comment" Value, but the problems are :
                // 1: Comments can be placed anywhere and are not "strictly structured"
                // 2: Comments will require calling Read() (not a big problem) and doing while (!EOF) {} (is the bigger problem.
                // the top "reading call stack" is popped without any "moving forward" and the chance for an error to be thrown)
                // Though they aren't recursive, so that's a "bonus".
                // I could add it later, requiring calling "Read" and yielding back when the SJType is Comment (also checking EOF?)
                // To not break compatibility, I could add "ignoreComments = true" by default to skip comments while reading.
                SkipComment(); // This sets Error
                goto _Top;
            }
            // Validate : Object == KV Colon, Array == Comma
            else if (ch == ':' || ch == ',')
            {
                if (lastRecursableTypes.TryPeek(out SJType lastType))
                {
                    if (ch == ':')
                    {
                        if (lastType == SJType.Array)
                        {
                            Error = "Unexpected ':' in array";
                            goto _Top;
                        }
                        else if (_ColonsRequired == 0)
                        {
                            Error = "Unexpected ':', exceeding required";
                            goto _Top;
                        }

                        _ColonsRequired = Math.Max(-1, _ColonsRequired - 1);
                    }
                }
                else
                {
                    Error = "Unexpected ':' or ',' in toplevel";
                }

                current++;
                goto _Top;
            }
            else if (_ColonsRequired > 0)
            {
                Error = $"Unknown token '{Tk(ch)}', expected ':'";
                goto _Top;
            }
            // Parse : Number
            else if (char.IsDigit(ch) || ch == '-')
            {
                result.type = SJType.Number;
                while (current < Length && IsNumberContinuation(At(current)))
                {
                    ch = At(current++);
                }
                result.end = current;

                if (!CheckEOF())
                {
                    goto _Top;
                }
            }
            // Parse : String
            else if (ch == '"')
            {
                ch = At(++current);
                result.type = SJType.String;
                result.start = current;

                while (true)
                {
                    if (current >= Length)
                    {
                        Error = "Unclosed string";
                        goto _Top;
                    }
                    // Do have to handle the current escape here
                    if (ch == '"' && (current > 0 && At(current - 1) != '\\'))
                    {
                        break;
                    }

                    if (ch == '\\' || current < Length)
                    {
                        ch = At(++current);
                    }
                }
                result.end = current++;

                if (!CheckEOF())
                {
                    goto _Top;
                }
                return result;
            }
            // Parse : Array | Object
            else if (ch == '{' || ch == '[')
            {
                result.type = (ch == '{') ? SJType.Object : SJType.Array;
                lastRecursableTypes.Push(result.type);
                result.depth = ++depth;
                ch = At(++current);
            }
            else if (ch == '}' || ch == ']')
            {
                result.type = SJType.End;
                depth--;
                SJType lastType = lastRecursableTypes.Pop();
                if (depth < 0 || lastType != ((ch == '}') ? SJType.Object : SJType.Array))
                {
                    Error = (ch == '}') ? "Stray '}'" : "Stray ']'";
                    goto _Top;
                }

                ch = At(++current);
                result.end = current;

                if (!CheckEOF())
                {
                    goto _Top;
                }
            }
            // Parse : null | true | false 
            else if (ch == 'n' || ch == 't' || ch == 'f')
            {
                result.type = (ch == 'n') ? SJType.Null : SJType.Bool;

                if (Slice(current, 4).Equals("null", StringComparison.Ordinal))
                {
                    current += 4;
                }
                else if (Slice(current, 5).Equals("false", StringComparison.Ordinal))
                {
                    current += 5;
                }
                else if (Slice(current, 4).Equals("true", StringComparison.Ordinal))
                {
                    current += 4;
                }
                else
                {
                    Error = $"Unknown token '{Tk(ch)}'";
                    goto _Top;
                }
                result.end = current;

                if (!CheckEOF())
                {
                    goto _Top;
                }
            }
            // Fail
            else
            {
                Error = $"Unknown token '{Tk(ch)}'";
                goto _Top;
            }

            return result;
        }
        /// <summary>
        /// Moves <see cref="depth"/> and <see cref="current"/> until a depth.
        /// </summary>
        protected void DiscardUntil(int depth)
        {
            var value = Value.Null();
            while (this.depth != depth && value.type != SJType.Error)
            {
                value = Read();
            }
        }
        /// <summary>
        /// Iterate the values of <paramref name="arrayValue"/> if it's an <see cref="SJType.Array"/>.
        /// </summary>
        public bool IterateArray(Value arrayValue, out Value result)
        {
            if (arrayValue.type != SJType.Array)
            {
                result = Value.Error(this);
                return false;
            }

            // ↓ if the array contains other recursive elements, this dives and goes back to the current array depth
            DiscardUntil(arrayValue.depth);
            result = Read();
            return result.type != SJType.Error && result.type != SJType.End;
        }
        /// <summary>
        /// Iterate the key/value of <paramref name="objectValue"/> if it's an <see cref="SJType.Object"/>.
        /// </summary>
        public bool IterateObject(Value objectValue, out Value key, out Value value)
        {
            if (objectValue.type != SJType.Object)
            {
                key = value = Value.Error(this);
                return false;
            }

            DiscardUntil(objectValue.depth);
            key = Read();
            if (key.type == SJType.Error || key.type == SJType.End)
            {
                value = Value.Error(this);
                return false;
            }
            if (key.type != SJType.String)
            {
                value = Value.Error(this);
                Error = "Expected String as key";
                return false;
            }

            _ColonsRequired = 1;
            value = Read();
            if (value.type == SJType.Error)
            {
                return false;
            }
            if (value.type == SJType.End)
            {
                Error = "Unexpected object end";
                return false;
            }
            _ColonsRequired = -1;

            return true;
        }
        /// <summary>
        /// Get the current readout location.
        /// </summary>
        public bool Location(out int line, out int column)
        {
            if (Length <= 0)
            {
                line = column = 0;
                return false;
            }

            line = column = 1;
            for (int i = 0; i < current; i++)
            {
                char cur = At(i);
                if (cur == '\n')
                {
                    if ((i + 1) < Length)
                    {
                        column = 1;
                        line++;
                    }
                }
                else
                {
                    column++;
                }
            }

            return true;
        }
        /// <summary>
        /// Reset the read state.
        /// <br>This is called when <see cref="Data"/> is set to a new value.</br>
        /// </summary>
        public void Reset()
        {
            current = 0;
            depth = 0;
            Error = null;
            lastRecursableTypes?.Clear();
        }

        public override string ToString()
        {
            return $"[SJReader] current={current}, depth={depth}, error={Error}";
        }
    }
}
