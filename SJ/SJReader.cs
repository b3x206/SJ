using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static SJ.SJReader;

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
    ///     // It is recommended to do a bound check and return EOF of your choice
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
        // * Generic class (with the character unit type), or to avoid boxing,
        //   byte version that does ASCII (ignores UTF-8, but should work as all JSON tokens are within ascii)

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
            /// <summary>
            /// <see cref="SJReader.depth"/> of the <see cref="reader"/> while this Value was being created.
            /// </summary>
            public readonly int depth;
            /// <summary>
            /// Object typed value depth of this value. This is used for <see cref="DiscardUntil(int)"/>.
            /// <br><b>Only applicable when type is <see cref="SJType.Object"/>, <see cref="SJType.Array"/> or <see cref="SJType.End"/>!</b></br>
            /// </summary>
            public int objectDepth;

            public Value(SJReader reader)
            {
                this.reader = reader;
                type = SJType.Error;
                start = end = 0;
                objectDepth = 0;
                depth = reader?.depth ?? 0;
            }
            public Value(SJReader reader, SJType type, int start, int end, int objectDepth, int depth)
            {
                this.reader = reader;
                this.type = type;
                this.start = start;
                this.end = end;
                this.objectDepth = objectDepth;
                this.depth = depth;
            }

            public static Value Error() => new Value(null, SJType.Error, -1, -1, -1, -1);
            public static Value Error(SJReader reader) => new Value(reader, SJType.Error, -1, -1, -1, reader?.depth ?? -1);
            public static Value Null() => new Value(null, SJType.Null, -1, -1, -1, -1);
            public static Value Null(SJReader reader) => new Value(reader, SJType.Null, -1, -1, -1, reader?.depth ?? -1);

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
                return $"[SJReader.Value] type:{type}, start:{start}, end:{end}, depth:{objectDepth}";
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
        /// <br><b>Caution:</b> If you set this <see langword="false"/>, the document must be 
        /// read differently compared to the basic method, as EOF validation and other things 
        /// are done implicitly (eg: getting next value, because comments can be anywhere) depending on this value.
        /// </br>
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
        /// <summary>
        /// Whether if the reader ended. If you want to validate end of JSON document,
        /// you should do this if <see cref="ignoreCapturedComments"/> is <see langword="false"/>:
        /// <c>
        /// <br>while (!reader.End)</br>
        /// <br>{</br>
        /// <br>var value = reader.Read();</br>
        /// <br>switch (value.type) ... processing code, if type is comment next "Read" must be called again to proceed ... </br>
        /// <br>}</br>
        /// </c>
        /// <br/><br/>
        /// This will also be marked <see langword="true"/> if <see cref="Error"/> exists.
        /// </summary>
        public bool Ended => !string.IsNullOrEmpty(Error) || current >= Length;

        public Stack<SJType> lastRecursableTypes;
        
        protected enum ExpectState { None = 0, Comma = 1 << 0, Key = 1 << 1, Colon = 1 << 2, Value = 1 << 3 }
        /// <summary>
        /// State for checking certain tokens within types like <see cref="SJType.Object"/> and <see cref="SJType.Array"/>
        /// </summary>
        protected ExpectState _ExpectState = ExpectState.None;
        /// <summary>
        /// Last key read on a object key/value pair. If a non-comment value block is returned while this is set,
        /// while being on a <see cref="SJType.Object"/>, this was the key that was for it.
        /// </summary>
        public Value LastKey { get; protected set; }

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
        /// <returns>Whether if the comment block was valid.</returns>
        protected bool SkipComment()
        {
            (int start, int end) = GetCommentRange(true);
            if (start >= 0 && end >= 0)
            {
                current = end;
                return true;
            }

            return false;
        }
        /// <summary>
        /// Read a range of comments, starting from <see cref="current"/> for the reader data.
        /// </summary>
        /// <param name="setError">Set <see cref="Error"/> if the comment block is invalid.</param>
        /// <returns>Start and ending range. If the block isn't a comment, -1 is returned for both regions</returns>
        protected (int start, int end) GetCommentRange(bool setError)
        {
            int start = current, end = current;
            if (start < Length)
            {
                // I should validate the current character twice, for external uses.
                char ch = At(end);
                if (ch == '/')
                {
                    char next = At(++end);
                    if (next == '/')
                    {
                        while (ch != '\n')
                        {
                            if (end >= Length)
                            {
                                // Ended with // .... <EOF
                                return (start, end);
                            }

                            ch = At(++end);
                        }
                        return (start, end);
                    }
                    else if (next == '*')
                    {
                        while (ch != '*' && next != '/')
                        {
                            if (end >= Length)
                            {
                                if (setError)
                                {
                                    Error = $"Expected */, got '{Tk(ch)}+{Tk(next)}'";
                                }
                                return (-1, -1);
                            }

                            ch = At(++end);
                            next = At(end + 1);
                        }

                        // skip the '*/'
                        end += 2;
                        return (start, end);
                    }
                    else
                    {
                        if (setError)
                        {
                            Error = $"Unexpected comment token '{Tk(next)}', expected '/ or *'";
                        }
                        return (-1, -1);
                    }
                }
                else
                {
                    // Not skipping while on a comment block
                    return (-1, -1);
                }
            }

            // Nothing to skip, but not an "error"
            return (-1, -1);
        }
        /// <summary>
        /// Check remaining tokens after the JSON document is finished. (depth == 0)
        /// <br>Does parsing of comments if <see cref="allowComments"/></br>
        /// </summary>
        /// <returns>
        /// <see langword="false"/> if the ending has invalid characters. <see langword="true"/> if the document 
        /// ending is valid. Note that for JSC without ignore comments, validating EOF is done explicitly and this method only skips whitespace.
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
                    if (ignoreCapturedComments)
                    {
                        if (!SkipComment())
                        {
                            Error = $"[end] {Error}";
                            return false;
                        }
                    }
                    else
                    {
                        // Yield for parsing the comment line as current points to == '/', as it's "not EOF"
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
                result.objectDepth = -1;
                return result;
            }
            if (Length <= 0)
            {
                Error = "Empty data";
                goto _Top;
            }
            if (current >= Length)
            {
                // Reaching end of file is no longer an error, but it should return an empty "end" value.
                // Error = "Reached end of data";
                // goto _Top;
                result.type = SJType.End;
                result.end = current;
                result.objectDepth = -1;
                return result;
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
            // Validate / Parse : Comments
            else if (allowComments && ch == '/')
            {
                // Perhaps I could have added a "SJType.Comment" Value, but the problems are :
                // 1: Comments can be placed anywhere and are not "strictly structured"
                // 2: Comments will require calling Read() (not a big problem) and doing while (!reader.EOF) {}
                //    (is the bigger problem for support. bandaid fix is to use ignoreCapturedComments to opt in to the new looping. however the new loop method _should_ support the old read.)
                //    (future SJ update will make it better hopefully)
                if (ignoreCapturedComments)
                {
                    SkipComment();
                }
                else
                {
                    result.type = SJType.Comment;
                    (result.start, result.end) = GetCommentRange(true);
                    if (string.IsNullOrEmpty(Error) && result.start >= 0 && result.end >= 0)
                    {
                        current = result.end;
                        return result;
                    }
                }
                goto _Top;
            }
            // Validate : Object == KV Colon, Array == Comma
            else if (ch == ':' || ch == ',')
            {
                if (lastRecursableTypes.TryPeek(out var lastType))
                {
                    if (ch == ':')
                    {
                        if (lastType != SJType.Object)
                        {
                            Error = "Unexpected ':' in top level array";
                            goto _Top;
                        }
                        else if ((_ExpectState & ExpectState.Colon) != ExpectState.Colon)
                        {
                            Error = "Unexpected ':', exceeding required";
                            goto _Top;
                        }

                        _ExpectState &= ~ExpectState.Colon;
                    }
                    else if (ch == ',')
                    {
                        // both Array and Object get commas
                        if ((_ExpectState & ExpectState.Comma) != ExpectState.Comma)
                        {
                            Error = "Unexpected ',', exceeding required";
                            goto _Top;
                        }

                        _ExpectState &= ~ExpectState.Comma;
                    }
                    else
                    {
                        // Unreachable
                        Error = $"ch '{Tk(ch)}' was somehow mutated on comma/colon check and no longer pleases any condition";
                        goto _Top;
                    }
                }
                else
                {
                    Error = "Unexpected ':' or ',' in top level nothing";
                }

                current++;
                goto _Top;
            }
            else if ((_ExpectState & ExpectState.Colon) == ExpectState.Colon)
            {
                // Colons are strict, they are seperator for key/value
                Error = $"Unexpected '{Tk(ch)}', expected ':'";
                goto _Top;
            }
            // Parse : Array | Object End (overrides comma)
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
                // Should also check whether if there were any values previously, as comma is not required
                if ((_ExpectState & ExpectState.Comma) != ExpectState.Comma)
                {
                    Error = "Trailing comma character for the last value before end";
                    goto _Top;
                }

                // Likely a comma is required for the next entry.
                // However this semantic could fail for array like [[]] if the array / object declarations are lacking)
                if (depth > 0)
                {
                    _ExpectState |= ExpectState.Comma;
                }

                ch = At(++current);
                result.end = current;

                if (!CheckEOF())
                {
                    goto _Top;
                }
            }
            // Validate: Commas
            else if ((_ExpectState & ExpectState.Comma) == ExpectState.Comma)
            {
                // Commas are more flexible and can be found anywhere else,
                // however they are not required for object declarations,
                // but they are required for literals and object endings.
                Error = $"Unknown token '{Tk(ch)}', expected ','";
                goto _Top;
            }
            // Parse : Array | Object Start
            else if (ch == '{' || ch == '[')
            {
                if (ch == '{')
                {
                    _ExpectState |= ExpectState.Key;
                    result.type = SJType.Object;
                }
                else
                {
                    result.type = SJType.Array;
                }
                lastRecursableTypes.Push(result.type);
                result.objectDepth = ++depth;
                ch = At(++current);

                // If starting an object, comma is not expected and this is object "declaration" without comma
                _ExpectState &= ~ExpectState.Comma;
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
            while (!Ended && this.depth != depth && value.type != SJType.Error)
            {
                value = Read();
            }
        }
        /// <summary>
        /// Iterate the values of <paramref name="value"/> if it's an <see cref="SJType.Array"/> while the value entry depth is same.
        /// <br>If the <paramref name="value"/> is <see cref="SJType.Object"/>, the 
        /// last read key and value is stored in <see cref="LastKey"/> and <see cref="LastValue"/>.
        /// Both of these values are available until </br>
        /// </summary>
        public bool IterateValues(Value value, out Value result)
        {
            if (value.type != SJType.Array)
            {
                result = Value.Error(this);
                return false;
            }

            DiscardUntil(value.objectDepth);
            result = Read();
            if (result.type != SJType.Object && result.type != SJType.Array && result.type != SJType.Comment)
            {
                // Did not start an object/array, is simply the next value on the array.
                _ExpectState |= ExpectState.Comma;
            }
            return result.type != SJType.Error && result.type != SJType.End;
        }
        public enum ObjectEntry { None, Key, Value }
        /// <summary>
        /// Iterate <see cref="SJType.Object"/> typed <paramref name="value"/> with selector for the entry type.
        /// </summary>
        /// <param name="value">Value that is <see cref="SJType.Object"/>.</param>
        /// <param name="result">Value entry within the object declaration. What this is depends on <paramref name="type"/>.</param>
        /// <param name="type">
        /// Object type of <paramref name="result"/>.
        /// Valid keys are <see cref="ObjectEntry.Key"/>, values are
        /// </param>
        /// <returns>Whether if more data is available to read from <paramref name="value"/>.</returns>
        public bool IterateObject(Value value, out ObjectEntry type, out Value result)
        {
            type = ObjectEntry.None;
            if (value.type != SJType.Object)
            {
                result = Value.Error(this);
                return false;
            }

            DiscardUntil(value.objectDepth);
            result = Read();
            if (result.type == SJType.Error || result.type == SJType.End)
            {
                result = Value.Error(this);
                return false;
            }
            // read must not return SJType.Comment while "ignoreCapturedComments" is true
            if (result.type == SJType.Comment)
            {
                // next comment bradar
                return true;
            }

            if ((_ExpectState & ExpectState.Key) == ExpectState.Key)
            {
                if (result.type != SJType.String)
                {
                    result = Value.Error(this);
                    Error = "Expected String as key";
                    return false;
                }

                type = ObjectEntry.Key;
                LastKey = result;
                _ExpectState |= ExpectState.Colon;

                return true;
            }
            else
            {
                type = ObjectEntry.Value;
                // Value needs comma afterwards, but SJType.End could end it, so the check does that 
                _ExpectState &= ~ExpectState.Colon;
                // Expect both key and comma for the next KV read
                _ExpectState |= ExpectState.Comma | ExpectState.Key;

                return true;
            }
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
            _ExpectState = ExpectState.None;
            Error = null;
            lastRecursableTypes?.Clear();
        }

        public override string ToString()
        {
            return $"[SJReader] current={current}, depth={depth}, error={Error}";
        }
    }
}
