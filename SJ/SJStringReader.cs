using System;
using System.Runtime.CompilerServices;

namespace SJ
{
    /// <summary>
    /// Uses <see cref="string"/> as JSON read resource.
    /// </summary>
    /// <example>
    /// <![CDATA[
    /// // This example shows how you can recursively read from a SJReader
    /// using SJ;
    /// using System.Text;
    /// 
    /// var data = "{ \"foo\": [\"bar\", \"baz\"], \"idk\": 42, \"mango\": { \"6\": 7 } }";
    /// var reader = new SJStringReader(data);
    /// 
    /// SJReader.Value root = reader.Read();
    /// var sb = new StringBuilder(512);
    /// 
    /// ReadInner(sb, reader, root);
    /// static void ReadInner(StringBuilder sb, SJReader reader, SJReader.Value root)
    /// {
    ///     switch (root.type)
    ///     {
    ///         default:
    ///         case SJType.Error:
    ///             {
    ///                 reader.Location(out int l, out int c);
    ///                 Console.WriteLine($"ReadInner: Error '{reader.Error}' at 'line={l}, column={c}'");
    ///                 // ^ This will output multiple times, you can add a global flag if that is undesired.
    ///                 break;
    ///             }
    /// 
    ///         case SJType.Array:
    ///             {
    ///                 bool first = true;
    ///                 sb.Append('[');
    ///                 while (reader.IterateArray(root, out var array))
    ///                 {
    ///                     if (!first)
    ///                         sb.Append(',');
    /// 
    ///                     ReadInner(sb, reader, array);
    /// 
    ///                     first = false;
    ///                 }
    ///                 sb.Append(']');
    ///                 if (!string.IsNullOrEmpty(reader.Error))
    ///                 {
    ///                     goto case SJType.Error;
    ///                 }
    ///                 break;
    ///             }
    ///         case SJType.Object:
    ///             {
    ///                 bool first = true;
    ///                 sb.Append('{');
    ///                 while (reader.IterateObject(root, out var k, out var v))
    ///                 {
    ///                     if (!first)
    ///                         sb.Append(',');
    /// 
    ///                     ReadInner(sb, reader, k);
    ///                     sb.Append(':');
    ///                     ReadInner(sb, reader, v);
    /// 
    ///                     first = false;
    ///                 }
    ///                 sb.Append('}');
    ///                 if (!string.IsNullOrEmpty(reader.Error))
    ///                 {
    ///                     goto case SJType.Error;
    ///                 }
    ///                 break;
    ///             }
    ///         case SJType.String:
    ///             {
    ///                 // If unescaping the string is desired, you can use SJEscape.Unescape(string)
    ///                 sb.Append('"').Append(root.Slice()).Append('"');
    ///                 break;
    ///             }
    ///         case SJType.Number:
    ///         case SJType.Null:
    ///         case SJType.Bool:
    ///             {
    ///                 sb.Append(root.Slice());
    ///                 break;
    ///             }
    ///     }
    /// }
    /// ]]>
    /// </example>
    public sealed class SJStringReader : SJReader
    {
        private string _data;
        public string Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data;
            set
            {
                if (string.Equals(value, _data, StringComparison.Ordinal))
                {
                    return;
                }

                _data = value;
                Reset();
            }
        }

        public override int Length => _data?.Length ?? 0;

        // Because I ported the pointer arithmetic directly, should check EOF / do a bounds check
        // Eh, branch predictors exist for a reason (activating larp overdrive)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override char At(int i) => i < Data.Length ? _data[i] : '\0';
        protected override ReadOnlySpan<char> Slice(int start, int length) => Data.AsSpan(start, length);

        public SJStringReader()
        { }
        public SJStringReader(string data)
        {
            _data = data;
        }
    }
}

