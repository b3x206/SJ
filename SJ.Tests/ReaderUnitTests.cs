using System.Text;

namespace SJ.Tests;

[TestClass]
public sealed class ReaderUnitTests
{
    // Root Data
    const string DataEmptyObject = @"{}", DataEmptyArray = @"[]", DataRootString = @"""Hello world!""",
        DataRootNumber = @"42.42", DataRootBool = @"false", DataRootNull = @"null", DataEmpty = "";

    // JSON Data
    const string Data1 = @"{
        ""foo"": [""bar"", ""baz""],
        ""idk"": 42,
        ""mango"": { ""6"": 7 }
}";
    const string Data1Invalid = @"{
        ""foo"": ""bar"", ""baz""],
        ""id], .
// Are comments valid? No
";

    const string Data1Nested = @"{
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
    ""nesting"": [{
        ""depth_1"": [
            { ""depth_2"": ""We're deep now"" }
        ]
    }],
    ""logic"": [true, false, null],
    ""empty"": { ""obj"": {}, ""arr"": [] }
}";
    // ↓ the sj.h does not complain what was there before, "if it starts/ends a recursive object it was valid". this one will complain though.
    const string DataStackingInvalid = @"{
    ""key"": { ""another key"": [{]}, ]
}";

    const string DataJSC = @"/* Let's start our invalid JSON journey! */
// Also some more lines.
{
        // I like annotating my JSON, but it gets deleted when it's serialized :(
        /* wen eta fix this? */
        ""Array"": [1,2,3,4,5,6,7,8, /* oops convenient comment */ 9,10,11],
        ""Objects"": { ""Yes"": true, ""No"": false, ""Empty??"": null, ""Whatever"": ""/* Not really a comment. */"" } // I like putting comments where inconvenient
} // Our line ends with */";
    const string DataJSCInvalid = @"/* Let's start our invalid JSON journey! */
// Also some more lines.
{
        // I like annotating my JSON, but it gets deleted when it's serialized :(
        /* wen eta fix this?
} // Our line ends with */";

    // File Data
    static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string ValidName = Path.Combine(BaseDirectory, "Files", "valid.json"),
        LargeName = Path.Combine(BaseDirectory, "Files", "5mb.json"),
        LargeMinName = Path.Combine(BaseDirectory, "Files", "5mb.min.json"),
        InvalidUnterminated = Path.Combine(BaseDirectory, "Files", "unterminated.json"),
        InvalidNoColon = Path.Combine(BaseDirectory, "Files", "missing_colon.json"),
        InvalidBinary = Path.Combine(BaseDirectory, "Files", "binary.json");

    static void ReadInner(StringBuilder sb, SJReader reader, SJReader.Value root)
    {
        switch (root.type)
        {
            default:
            case SJType.Error:
                {
                    reader.Location(out int l, out int c);
                    reader.ThrowError();
                    break;
                }

            case SJType.Array:
                {
                    bool first = true;
                    sb.Append('[');
                    while (reader.IterateArray(root, out var array))
                    {
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, array);

                        first = false;
                    }
                    sb.Append(']');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    break;
                }
            case SJType.Object:
                {
                    bool first = true;
                    sb.Append('{');
                    while (reader.IterateObject(root, out var k, out var v))
                    {
                        if (!first)
                            sb.Append(',');

                        ReadInner(sb, reader, k);
                        sb.Append(':');
                        ReadInner(sb, reader, v);

                        first = false;
                    }
                    sb.Append('}');
                    if (!string.IsNullOrEmpty(reader.Error))
                    {
                        goto case SJType.Error;
                    }
                    break;
                }
            case SJType.String:
                {
                    sb.Append('"').Append(root.Slice()).Append('"');
                    break;
                }
            case SJType.Number:
            case SJType.Null:
            case SJType.Bool:
                {
                    sb.Append(root.Slice());
                    break;
                }
        }
    }
    const int DefaultSbCapacity = 512;
    static StringBuilder Read(string data)
    {
        var sb = new StringBuilder(DefaultSbCapacity);
        Read(sb, data);
        return sb;
    }
    static void Read(StringBuilder sb, string data)
    {
        sb.Clear();
        var reader = new SJStringReader(data);
        var root = reader.Read();

        ReadInner(sb, reader, root);
    }
    static StringBuilder ReadJSC(string data)
    {
        var sb = new StringBuilder(DefaultSbCapacity);
        ReadJSC(sb, data);
        return sb;
    }
    static void ReadJSC(StringBuilder sb, string data)
    {
        sb.Clear();
        var reader = new SJStringReader(data)
        {
            allowComments = true
        };
        var root = reader.Read();

        ReadInner(sb, reader, root);
    }
    static StringBuilder Read(SJReader reader)
    {
        var sb = new StringBuilder(DefaultSbCapacity);
        var root = reader.Read();

        ReadInner(sb, reader, root);
        return sb;
    }

    // Basic tests: Read and Empty
    [TestMethod]
    public void TestBasic() => Read(Data1);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestBasicInvalid() => Read(Data1Invalid);
    [TestMethod]
    public void TestSlightlyComplicated() => Read(Data1Nested);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestStacking() => Read(DataStackingInvalid);
    [TestMethod]
    public void TestJSC() => ReadJSC(DataJSC);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalid() => ReadJSC(DataJSCInvalid);
    [TestMethod]
    public void TestEmptyValues()
    {
        string[] datas = [DataEmptyObject, DataEmptyArray, DataRootString, DataRootNumber, DataRootBool, DataRootNull];
        var sb = new StringBuilder(64);

        for (int i = 0; i < datas.Length; i++)
        {
            string data = datas[i];
            Read(sb, data);

            // Since the empty builder for the sample code is simple but somewhat matching, it should equal.
            Assert.IsTrue(sb.Equals(data), $"Result data should equal StringBuilder:\n{data} != {sb}");
        }
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestEmptyString()
    {
        Read(DataEmpty);
        Assert.Fail("Should have thrown exception for invalid data.");
    }

    /// <summary>
    /// This method must throw <see cref="SJReader.ReadException"/>
    /// </summary>
    /// <param name="tks">This argument must be the opening and enclosing tags for a recursive data structure.</param>
    /// <exception cref="ArgumentException"></exception>
    static void TestMaxRecursion(string tks)
    {
        if (string.IsNullOrEmpty(tks))
        {
            throw new ArgumentException($"'{nameof(tks)}' cannot be null or empty.", nameof(tks));
        }
        if (tks.Length != 2)
        {
            throw new ArgumentException($"'{nameof(tks)}' must exactly have two characters, one for opening and one for closing.", nameof(tks));
        }

        var reader = new SJStringReader();

        // If this is "ever changed" to less. 128 stack frames seem reasonable.
        reader.maxDepth = Math.Max(128, reader.maxDepth);
        reader.Data = string.Concat(string.Join("", Enumerable.Repeat(tks[0], reader.maxDepth + 1)), string.Join("", Enumerable.Repeat(tks[1], reader.maxDepth + 1)));

        Read(reader);
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxArrayRecursion() => TestMaxRecursion("[]");
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxObjectRecursion() => TestMaxRecursion("{}");

    // "valid.json" is from some random website.
    // Other JSON data sets are from https://microsoftedge.github.io/Demos/json-dummy-data/
    static void TestFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException($"'{nameof(fileName)}' cannot be null or empty.", nameof(fileName));
        }

        Assert.IsTrue(File.Exists(fileName), $"Test file not found at: {fileName}, please include with the test project.");
        Read(File.ReadAllText(fileName));
    }
    /// <summary>
    /// Tests reading a basic JSON file (100kb-ish).
    /// </summary>
    [TestMethod]
    public void TestFile() => TestFile(ValidName);
    /// <summary>
    /// Tests reading large JSON file.
    /// </summary>
    [TestMethod]
    [Timeout(30000)] // ← Change this if your PC is slower, but it's unlikely you will need this.
    public void TestVeryLarge()
    {
        TestFile(LargeName);
        TestFile(LargeMinName);
    }
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidUnterminated() => TestFile(InvalidUnterminated);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidNoColon() => TestFile(InvalidNoColon);
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidBinary() => TestFile(InvalidBinary); // This could also throw IO error, but it don't.
}
