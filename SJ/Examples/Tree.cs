using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SJ.Examples
{
    // A basic tree structure.
    sealed class SJTree
    {
        // Since the SJ value is lazily evaluated, we should copy the data here too.
        public SJType type;

        public string stringValue;
        public bool boolValue;
        public double numberValue;
        public Lazy<List<SJTree>> arrayValue = new Lazy<List<SJTree>>();
        // ↓ Keys are asserted to be "string"
        public Lazy<Dictionary<string, SJTree>> objectValue = new Lazy<Dictionary<string, SJTree>>();

        // Note : Does not check cyclic references, but those can be handled with a Dictionary<SJTree, string /* for path */>
        //        with a reference to the "node" that will hold a references table.
        public static SJTree FromJSON(SJReader reader)
        {
            var t = new SJTree();
            WriteFrom(t, reader);
            return t;
        }
        public static void WriteFrom(SJTree tree, SJReader reader)
        {
            if (tree is null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            if (!string.IsNullOrEmpty(reader.Error))
            {
                // Either reset the reader, or throw an argument exception.
                throw new ArgumentException($"Given SJReader has an error : {reader.Error}", nameof(reader));
            }

            WriteFromInternal(tree, reader, reader.Read());
        }
        private static void WriteFromInternal(SJTree tree, SJReader reader, SJReader.Value root)
        {
            tree.type = root.type;

            switch (root.type)
            {
                default:
                case SJType.Error:
                    {
                        // Throw the error with one of the following methods
                        // 1. reader.ThrowError = true; → Mutates the reader. Could be undesired if exception is being caught.
                        // 2. throw new SJReader.ReadException(reader); → Throws error without changing 
                        // 3. reader.ThrowError(); → Less verbose
                        reader.ThrowError();
                        break;
                    }

                case SJType.Array:
                    {
                        while (reader.IterateArray(root, out var v))
                        {
                            var node = new SJTree();
                            WriteFromInternal(node, reader, v);
                            tree.arrayValue.Value.Add(node);
                        }

                        if (!string.IsNullOrEmpty(reader.Error))
                        {
                            goto case SJType.Error;
                        }
                        break;
                    }
                case SJType.Object:
                    {
                        while (reader.IterateObject(root, out var k, out var v))
                        {
                            var node = new SJTree();
                            WriteFromInternal(node, reader, v);
                            tree.objectValue.Value[SJEscape.Unescape(k.Slice())] = node;
                        }

                        if (!string.IsNullOrEmpty(reader.Error))
                        {
                            goto case SJType.Error;
                        }
                        break;
                    }

                case SJType.String:
                    {
                        // If unescaping the string is desired, you can use SJEscape.Unescape(string)
                        tree.stringValue = SJEscape.Unescape(root.Slice());
                        break;
                    }
                case SJType.Number:
                    {
                        tree.numberValue = double.Parse(root.Slice(), NumberStyles.AllowThousands | NumberStyles.Float, CultureInfo.InvariantCulture);
                        break;
                    }
                case SJType.Bool:
                    {
                        tree.boolValue = char.ToLower(root.Slice()[0]) == 't';
                        break;
                    }
                case SJType.Null:
                    {
                        break;
                    }
            }
        }

        public override string ToString()
        {
            switch (type)
            {
                default:
                    return "!! ERROR !!";

                case SJType.Object:
                    if (objectValue.IsValueCreated)
                    {
                        var sb = new StringBuilder(128);
                        bool first = true;
                        foreach (var kv in objectValue.Value)
                        {
                            if (!first)
                            {
                                sb.Append(' ').Append(',');
                            }

                            sb.Append('"').Append(kv.Key.ToString()).Append('"').Append(':').Append(kv.Value.ToString());
                            first = false;
                        }
                        return sb.ToString();
                    }
                    else
                    {
                        return "{}";
                    }
                case SJType.Array:
                    if (arrayValue.IsValueCreated)
                    {
                        var sb = new StringBuilder(128);
                        bool first = true;
                        foreach (var v in arrayValue.Value)
                        {
                            if (!first)
                            {
                                sb.Append(' ').Append(',');
                            }

                            sb.Append(v.ToString());
                            first = false;
                        }
                        return sb.ToString();
                    }
                    else
                    {
                        return "[]";
                    }

                case SJType.Number:
                    return numberValue.ToString();
                case SJType.String:
                    return stringValue;
                case SJType.Bool:
                    return boolValue.ToString();
                case SJType.Null:
                    return "null";
            }
        }
    }

    sealed class Tree
    {
        const string TestData = @"{
  ""numbers"": [0, -1, 1.23, 1.0e-5, 1000000],
  ""strings"": {
    ""basic"": ""Hello World"",
    ""escaped"": ""Quote: \"", Backslash: \\, Tab: \t, Newline: \n"",
    ""unicode_BMP"": ""Euro: \u20AC"",
    ""emoji_surrogate"": ""Pizza: \uD83C\uDF55"",
    ""emoji_with_variant"": ""The 🅱 variant: \uD83C\uDD71\uFE0F"",
    ""raw_emoji"": ""🍕"",
    ""non_ascii_literal"": ""你好, ¡Hola!, Grüße""
  },
  ""nesting"": [
    {
      ""depth_1"": [
        { ""depth_2"": ""We're deep now"" }
      ]
    }
  ],
  ""logic"": [true, false, null],
  ""empty"": { ""obj"": {}, ""arr"": [] }
}";

        static void Main(string[] args)
        {
            string data = args.Length > 1 ? args[1] : TestData;
            var reader = new SJStringReader(data);
            var tree = SJTree.FromJSON(reader);

            Console.WriteLine("Parsed SJ tree :");
            Console.WriteLine(tree.ToString());
        }
    }
}
