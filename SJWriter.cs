using System;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace SJ
{
    /// <summary>
    /// Base class for <i>most</i> JSON writers.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// using SJ;
    /// using System;
    /// 
    /// // An example class would look like this:
    /// public sealed class SJExampleWriter : SJWriter
    /// {
    ///     private readonly WriteSource data;
    /// 
    ///     public SJExampleWriter(WriteSource data)
    ///     {
    ///         this.data = data ?? throw new ArgumentNullException(nameof(data));
    ///     }
    /// 
    ///     public override void Append(char c) => data.Write(c);
    ///     public override void Append(ReadOnlySpan<char> s) => data.Write(s);
    /// 
    ///     public override void Reset()
    ///     {
    ///         base.Reset();
    ///         data.Clear(); // Or reset the position to start and truncate remaining data.
    ///     }
    ///     public override string ToString()
    ///     {
    ///         // Convert the resulting data to string, if necessary.
    ///         return data.ToString();
    ///     }
    /// }
    /// ]]>
    /// </example>
    public abstract class SJWriter
    {
        // A span based writer would be nice, but then I have to pass it
        // into the method instead of having the span stored inside the writer.
        // I could create a "SJSpanWriter" that does "the same behaviour",
        // but it's repeated code. So let's be "heap only" for the time being.

        // Unity does not support System.Range
#pragma warning disable IDE0057

        /// <summary>
        /// Exception thrown on an error case while reading JSON.
        /// </summary>
        public class WriteException : Exception
        {
            public WriteException(SJWriter writer) : base($"{writer.Error} | at char={writer.count}")
            { }
            public WriteException(string message, int count) : base($"{message} | at char={count}")
            { }
            public WriteException(string message) : base(message)
            { }
        }
        /// <summary>
        /// A tracker for type information that can be stacked and indexed (i.e. recursive data)
        /// </summary>
        public struct WriteStackInfo
        {
            /// <summary>
            /// Write type, this is generally either 
            /// <see cref="SJType.Array"/> or <see cref="SJType.Object"/>
            /// </summary>
            public SJType type;
            /// <summary>
            /// Currently stored write index (used more like a "count").
            /// </summary>
            public int index;
            /// <summary>
            /// Whether if this entry is valid or not.
            /// </summary>
            public readonly bool Valid => type != SJType.Error && type != SJType.End && index >= 0;

            public static readonly WriteStackInfo Invalid = new WriteStackInfo(SJType.Error) { index = -1 };
            public WriteStackInfo(SJType type)
            {
                this.type = type;
                index = 0;
            }
            public static implicit operator WriteStackInfo(SJType type) => new WriteStackInfo(type);
        }

        public readonly struct ObjectScope : IDisposable
        {
            private readonly SJWriter writer;
            private readonly bool success;

            public ObjectScope(SJWriter writer)
            {
                this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
                success = writer.BeginObject();
            }

            public void Dispose()
            {
                if (success)
                {
                    writer.EndObject();
                }
            }
        }
        public readonly struct ArrayScope : IDisposable
        {
            private readonly SJWriter writer;
            private readonly bool success;

            public ArrayScope(SJWriter writer)
            {
                this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
                success = writer.BeginArray();
            }

            public void Dispose()
            {
                if (success)
                {
                    writer.EndArray();
                }
            }
        }

        // settings
        /// <summary>
        /// The indent size to use. If this is 0 or less, no pretty printing is done.
        /// </summary>
        public int indentSize = 0;
        /// <summary>
        /// Whether if this writer has written a JSON document that is done.
        /// </summary>
        public bool finish = false;
        /// <summary>
        /// Maximum size for <see cref="writeStack"/>
        /// </summary>
        public int maxDepth = 128;
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
        /// <summary>
        /// Amount written by this writer.
        /// </summary>
        public int count;
        protected string _Error = string.Empty;
        /// <summary>
        /// The error string detailing why the writer failed.
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
        public Stack<WriteStackInfo> writeStack = new Stack<WriteStackInfo>();
        public WriteStackInfo Top
        {
            get => writeStack.Count > 0 ? writeStack.Peek() : WriteStackInfo.Invalid;
            set
            {
                if (!writeStack.TryPop(out _))
                {
                    Error = "Tried to set the top of the write stack while the stack depth is zero.";
                    return;
                }

                writeStack.Push(value);
            }
        }

        public int Depth => writeStack.Count;
        // This state doesn't need to be put into the write stack
        // as from what I see on JSON.parse, keys must be string, so there is no recursion on those
        // Though it has to be reset correctly in this case.
        /// <summary>
        /// 0 = none expected, 1 = key, 2 = value
        /// </summary>
        protected int kvState = 0;
        /// <summary>
        /// Need to write key in a key/value entry.
        /// </summary>
        public bool NeedKey => kvState == 1;
        /// <summary>
        /// Need to write value in a key/value entry.
        /// </summary>
        public bool NeedValue => kvState == 2;

        /// <summary>
        /// Append to the underlying StringBuilder/Stream-like object.
        /// <br>You can use this to write anything in anywhere, but it isn't recommended.</br>
        /// </summary>
        public abstract void Append(char c);
        /// <summary>
        /// <inheritdoc cref="Append(char)"/>
        /// </summary>
        public virtual void Append(ReadOnlySpan<char> s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                Append(s[i]);
            }
        }

        /// <summary>
        /// Throws the currently stored error. It does not check whether if an error is available.
        /// </summary>
        /// <exception cref="WriteException"></exception>
        public virtual void ThrowError()
        {
            throw new WriteException(this);
        }

        // The base writers
        private static readonly Action<SJWriter, char> selfAppend = (SJWriter writer, char c) => writer.Append(c);
        protected void WriteIndent(int depth)
        {
            if (depth <= 0 || indentSize <= 0)
            {
                return;
            }

            Span<char> indent = indentSize < 32 ? stackalloc char[indentSize] : new char[indentSize];
            for (int i = 0; i < indent.Length; i++) { indent[i] = ' '; }
            for (int i = 0; i < depth; i++)
            {
                Append(indent);
            }
            count += indentSize * depth;
        }
        protected void WriteEscaped(ReadOnlySpan<char> data, SJEscape.EscapeOptions options)
        {
            count += SJEscape.Escape(this, selfAppend, data, options);
        }
        protected void PrepareValue()
        {
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return;
            }

            if (Top.Valid)
            {
                if (!NeedValue)
                {
                    if (Top.index > 0)
                    {
                        Append(',');
                        count++;
                    }
                    if (indentSize > 0)
                    {
                        Append('\n');
                        count++;

                        WriteIndent(Depth);
                    }
                }

                // keys shouldn't affect the write index as it's a pair
                if (!NeedKey)
                {
                    var t = Top;
                    t.index++;
                    Top = t;
                }
            }
        }
        protected void PrepareEndValue(int prevIndex)
        {
            if (indentSize > 0 && prevIndex > 0)
            {
                Append('\n');
                count++;

                WriteIndent(Depth);
            }
        }

        public bool BeginObject()
        {
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return false;
            }
            if (NeedKey)
            {
                Error = "Expected writing object key";
                return false;
            }
            if (maxDepth > 0 && Depth >= maxDepth)
            {
                Error = $"Exceeded max write depth '{maxDepth}'";
                return false;
            }

            PrepareValue();
            Append('{');
            count++;

            writeStack.Push(SJType.Object);

            // Wait for a key
            kvState = 1;

            return true;
        }
        public bool EndObject()
        {
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return false;
            }
            if (NeedValue)
            {
                Error = "Expected writing object value";
                return false;
            }
            if (!Top.Valid || Top.type != SJType.Object)
            {
                Error = $"Invalid EndObject for toplevel with type '{Top.type}', expected Object";
                return false;
            }

            // Can end object
            kvState = 0;
            int prevIndex = Top.index;
            writeStack.Pop();

            if (Depth <= 0)
            {
                finish = true;
            }
            else if (Top.type == SJType.Object)
            {
                kvState = 1;
            }

            PrepareEndValue(prevIndex);
            Append('}');
            count++;

            return true;
        }

        public bool BeginArray()
        {
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return false;
            }
            if (NeedKey)
            {
                Error = "Expected writing object key";
                return false;
            }
            if (maxDepth > 0 && Depth >= maxDepth)
            {
                Error = $"Exceeded max write depth '{maxDepth}'";
                return false;
            }

            PrepareValue();
            Append('[');
            count++;
            kvState = 0;

            writeStack.Push(SJType.Array);
            return true;
        }
        public bool EndArray()
        {
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return false;
            }
            if (NeedKey)
            {
                Error = "Expected writing object key";
                return false;
            }
            if (!Top.Valid || Top.type != SJType.Array)
            {
                Error = $"Invalid EndObject for toplevel with type '{Top.type}', expected Array";
                return false;
            }

            int prevIndex = Top.index;
            writeStack.Pop();
            if (Depth <= 0)
            {
                finish = true;
            }
            else if (Top.type == SJType.Object)
            {
                kvState = 1;
            }

            PrepareEndValue(prevIndex);
            Append(']');
            count++;

            return true;
        }

        /// <summary>
        /// Write a key, while <see cref="NeedKey"/> is true.
        /// <br><b>Info :</b> Base implementation does not check duplicates.</br>
        /// </summary>
        /// <param name="name">Name of the key entry.</param>
        /// <param name="options">Options for escaping the <paramref name="name"/> string.</param>
        /// <returns>Whether if the write was successful.</returns>
        public virtual bool WriteKey(
            ReadOnlySpan<char> name, SJEscape.EscapeOptions options = SJEscape.EscapeOptions.None
        )
        {
            if (!NeedKey)
            {
                Error = "Did not expect an object key";
                return false;
            }

            PrepareValue();

            Append('"'); count++;
            WriteEscaped(name, options);
            Append('"'); count++;

            Append(':'); count++;

            if (indentSize > 0)
            {
                Append(' ');
                count++;
            }

            kvState = 2;

            return true;
        }
        /// <summary>
        /// Writes a literal value with the rules for the top level object or array.
        /// <br><b>Warning :</b> Using this, you can write invalid JSON
        /// (because it writes the value as is, but with the value rules and indenting).
        /// This method is not recommended for use, unless you know what you are doing.</br>
        /// <br>If on a object or array, comma is appended before the <paramref name="data"/> 
        /// (on <see cref="PrepareValue"/>). Prefer <c>/* */</c> style comment 
        /// blocks if you want to write comments.</br>
        /// <br>This also can be used as a faster way to write numbers.</br>
        /// </summary>
        /// <returns>Whether if the write was successful.</returns>
        public virtual bool WriteLiteralValue(ReadOnlySpan<char> data)
        {
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return false;
            }
            if (NeedKey)
            {
                Error = "Expected writing object key, got WriteLiteral";
                return false;
            }

            PrepareValue();

            Append(data);
            count += data.Length;

            if (Depth <= 0)
            {
                // Number is top level
                finish = true;
            }
            else if (Top.type == SJType.Object)
            {
                kvState = 1;
            }

            return true;
        }
        public bool WriteNumber(float number, string format = "R")
        {
            // number.ToString("G999") is totally reasonable and possible. So for floats, this is done.
            // Integers get a fixed size buffer.
            int capacity = 32, written = 0;
            Span<char> data = stackalloc char[capacity];
            char[] rented = null;

            try
            {
                while (!number.TryFormat(data, out written, format, CultureInfo.InvariantCulture))
                {
                    if (!(rented is null)) ArrayPool<char>.Shared.Return(rented);

                    capacity *= 2;
                    data = rented = ArrayPool<char>.Shared.Rent(capacity);
                }

                return WriteLiteralValue(data.Slice(0, written));
            }
            finally
            {
                if (!(rented is null))
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }
        public bool WriteNumber(double number, string format = "R")
        {
            int capacity = 32, written = 0;
            Span<char> data = stackalloc char[capacity];
            char[] rented = null;

            try
            {
                while (!number.TryFormat(data, out written, format, CultureInfo.InvariantCulture))
                {
                    if (!(rented is null)) ArrayPool<char>.Shared.Return(rented);

                    capacity *= 2;
                    data = rented = ArrayPool<char>.Shared.Rent(capacity);
                }

                return WriteLiteralValue(data.Slice(0, written));
            }
            finally
            {
                if (!(rented is null))
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }
        public bool WriteLong(long number, string format = "G")
        {
            Span<char> data = stackalloc char[32]; // long.MinValue.ToString().Length == 20

            if (!number.TryFormat(data, out int written, format, CultureInfo.InvariantCulture))
            {
                Error = $"Failed to convert number to string {number} with format {format}";
                return false;
            }

            return WriteLiteralValue(data.Slice(0, written));
        }
        public bool WriteULong(ulong number, string format = "G")
        {
            Span<char> data = stackalloc char[32]; // ulong.MaxValue.ToString().Length == 20

            if (!number.TryFormat(data, out int written, format, CultureInfo.InvariantCulture))
            {
                Error = $"Failed to convert number to string {number} with format {format}";
                return false;
            }

            return WriteLiteralValue(data.Slice(0, written));
        }
        public bool WriteString(
            ReadOnlySpan<char> value, SJEscape.EscapeOptions options = SJEscape.EscapeOptions.None
        )
        {
            // This one does some things different enough that it can't be "WriteLiteral"
            if (finish)
            {
                Error = "Attempt to write into a finished JSONWriter";
                return false;
            }
            if (NeedKey)
            {
                // eh sure, works, similar intent
                return WriteKey(value, options);
            }

            PrepareValue();

            Append('"');
            count++;

            WriteEscaped(value, options);

            Append('"');
            count++;

            if (Depth <= 0)
            {
                finish = true;
            }
            else if (Top.type == SJType.Object)
            {
                kvState = 1;
            }

            return true;
        }
        public bool WriteBool(bool value) => WriteLiteralValue(value ? "true" : "false");
        public bool WriteNull() => WriteLiteralValue("null");

        // These are like "extensions"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Write(float number, string format = "R") => WriteNumber(number, format);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Write(double number, string format = "R") => WriteNumber(number, format);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Write(long number, string format = "G") => WriteLong(number, format);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Write(ulong number, string format = "G") => WriteULong(number, format);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Write(ReadOnlySpan<char> value, SJEscape.EscapeOptions options = SJEscape.EscapeOptions.None) => WriteString(value, options);
        public bool Write(string value, SJEscape.EscapeOptions options = SJEscape.EscapeOptions.None)
        {
            // For the case of "Write" without any "info", the string overload is called for `null`
            // There will be an explicit check only for this. For anything else, null is treated as `default` or `string.Empty`
            // Because of this, it is suggested not to use `Write` for keys and strings. For anything else, it will work fine.
            if (value is null)
            {
                return WriteNull();
            }

            return WriteString(value, options);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Write(bool value) => WriteBool(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, float number, string format = "R")
        {
            if (!WriteKey(key))
            {
                return false;
            }
            return WriteNumber(number, format);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, double number, string format = "R")
        {
            if (!WriteKey(key))
            {
                return false;
            }
            return WriteNumber(number, format);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, long number, string format = "G")
        {
            if (!WriteKey(key))
            {
                return false;
            }
            return WriteLong(number, format);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, ulong number, string format = "G")
        {
            if (!WriteKey(key))
            {
                return false;
            }
            return WriteULong(number, format);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, ReadOnlySpan<char> value, SJEscape.EscapeOptions options = SJEscape.EscapeOptions.None)
        {
            if (!WriteKey(key))
            {
                return false;
            }

            return WriteString(value, options);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, string value, SJEscape.EscapeOptions options = SJEscape.EscapeOptions.None)
        {
            if (!WriteKey(key))
            {
                return false;
            }
            // No need to "worry about key", but still doing the null thing
            if (value is null)
            {
                return WriteNull();
            }

            return WriteString(value, options);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteKV(ReadOnlySpan<char> key, bool value)
        {
            if (!WriteKey(key))
            {
                return false;
            }
            return WriteBool(value);
        }

        public ArrayScope Array() => new ArrayScope(this);
        public ArrayScope ArrayKV(ReadOnlySpan<char> key)
        {
            if (!WriteKey(key))
            {
                // No operation is done if key fails.
                return new ArrayScope();
            }

            return new ArrayScope(this);
        }

        public ObjectScope Object() => new ObjectScope(this);
        public ObjectScope ObjectKV(ReadOnlySpan<char> key)
        {
            if (!WriteKey(key))
            {
                // No operation is done if key fails.
                return new ObjectScope();
            }

            return new ObjectScope(this);
        }

        public virtual void Reset()
        {
            count = 0;
            kvState = 0;
            finish = false;
            Error = null;
            writeStack.Clear();
        }
#pragma warning restore IDE0057
    }
}
