using static SJ.Tests.TestData;
using static SJ.Tests.ReaderTester;
using System.Text;
using System.Reflection;
using System.Xml.Linq;

namespace SJ.Tests;

/// <summary>
/// Base unit tests for a <see cref="SJReader"/> that must pass.
/// </summary>
/// <typeparam name="TReader">Type for the target <see cref="SJReader"/></typeparam>
public abstract class ReaderUnitTests<TReader> where TReader : SJReader
{
    // uh oh:
    // * https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#avoid-coding-logic-in-unit-tests
    // anyways, "it works"(tm). if only the data attributes could have been extended better.

    // Config
    /// <summary>
    /// Create <typeparamref name="TReader"/> with the data for <paramref name="data"/>.
    /// </summary>
    public abstract TReader CreateWithString(string data);
    /// <summary>
    /// Create <typeparamref name="TReader"/> with encoded data, if encoding related matters are to be tested. (for StringReader, this just tests the System libraries, but it will make sense for buffered readers)
    /// </summary>
    /// <param name="data">
    /// Data stream, can be created by a stream or something else.
    /// The stream can be disposed by this method if the <typeparamref name="TReader"/> does not rely on it.
    /// </param>
    /// <param name="enc">Encoding of the stream, if applicable.</param>
    public abstract TReader CreateFromStream(Stream data, Encoding? enc);
    /// <summary>
    /// Re-encodes <paramref name="data"/> to a stream and feeds it into <see cref="CreateWithStream(Stream)"/>.
    /// </summary>
    /// <param name="data">Data to create from.</param>
    /// <param name="enc">Encoding to mutilate <paramref name="data"/>'s bits.</param>
    public virtual TReader CreateWithEncodedString(string data, Encoding enc)
    {
        var ms = new MemoryStream(enc.GetBytes(data));
        return CreateFromStream(ms, enc);
    }
    /// <summary>
    /// Create a string that will exceed <paramref name="maxDepth"/>.
    /// </summary>
    /// <param name="tks">This argument must be the opening and enclosing tags for a recursive data structure, making it exactly 2 chars.</param>
    /// <exception cref="ArgumentException"></exception>
    protected TReader CreateWithMaxRecursionString(string tks, int maxDepth)
    {
        if (string.IsNullOrEmpty(tks))
        {
            throw new ArgumentException($"'{nameof(tks)}' cannot be null or empty.", nameof(tks));
        }
        if (tks.Length != 2)
        {
            throw new ArgumentException($"'{nameof(tks)}' must exactly have two characters, one for opening and one for closing.", nameof(tks));
        }

        if (tks[0] == '{')
        {
            // keys are needed for object
            // {"a":{"a":...
            return CreateWithString(string.Concat(tks[0], string.Join("", Enumerable.Repeat($"\"a\":{tks[0]}", maxDepth)), string.Join("", Enumerable.Repeat(tks[1], maxDepth + 1))));
        }
        else
        {
            return CreateWithString(string.Concat(string.Join("", Enumerable.Repeat(tks[0], maxDepth + 1)), string.Join("", Enumerable.Repeat(tks[1], maxDepth + 1))));
        }
    }
    /// <summary>
    /// Depth of property used on "MaxNRecursion". Override if your class initializes this property differently on a constructor.
    /// </summary>
    public virtual int MaxRecursionDepth => 128;
    /// <summary>
    /// Processors for the encoding parameter in the tests.
    /// </summary>
    public static IEnumerable<object[]> EncodedStringProcessors =>
        [[Encoding.UTF8], [Encoding.Unicode], [Encoding.BigEndianUnicode], [Encoding.UTF32]];
    public static string GetEncodedTestName(MethodInfo info, object[] data)
    {
        var data0 = data[0];
        if (ReferenceEquals(data0, Encoding.Unicode))
        {
            return $"{info.Name}_UnicodeEncoding";
        }
        if (ReferenceEquals(data0, Encoding.BigEndianUnicode))
        {
            return $"{info.Name}_BigEndianUnicodeEncoding";
        }
        return $"{info.Name}_{data0}";
    }
    /// <summary>
    /// Collection of root data (that are either empty or not)
    /// </summary>
    public static IEnumerable<object[]> JsonRootDataProcessors => [[JsonDataEmptyObject], [JsonDataEmptyArray], [JsonDataRootString], [JsonDataRootNumber], [JsonDataRootBool], [JsonDataRootNull]];
    public static string GetRootDataProcessorTestName(MethodInfo info, object[] data) => $"{info.Name}_{data[0]}";

    // Tests
    // Basic tests: Read and Empty
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestBasic() => Read(CreateWithString(JsonData1));
    [TestMethod]
    [ExpectedException(typeof(SJReader.ReadException))]
    [Timeout(TestTimeout.Short)]
    public void TestBasicInvalid() => Read(CreateWithString(JsonDataInvalid));
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestSlightlyComplicated() => Read(CreateWithString(JsonData2));
    // Emoji spams will get differently encoded strings
    [TestMethod]
    [Timeout(TestTimeout.Mid)]
    [DynamicData(nameof(EncodedStringProcessors), DynamicDataDisplayName = nameof(GetEncodedTestName))]
    public void TestEmojiSpam(Encoding enc)
    {
        var reader = CreateWithEncodedString(JsonData3, enc);
        var root = reader.Read();
        Assert.AreEqual(SJType.Object, root.type, $"Expected root to be object, it is instead this : {root}");
        while (reader.IterateObject(root, out SJReader.Value k, out SJReader.Value v))
        {
            Assert.AreEqual(new string(k.Slice()), JsonDataSpamKey, "Keys must be equal");
            Assert.AreEqual(SJType.Key, k.type);
            Assert.AreEqual(new string(v.Slice()), JsonDataSpamValue, "Values must be equal");
            Assert.AreEqual(SJType.String, v.type);
        }
    }
    [TestMethod]
    [Timeout(TestTimeout.Mid)]
    [DynamicData(nameof(EncodedStringProcessors), DynamicDataDisplayName = nameof(GetEncodedTestName))]
    public void TestEmojiSpamLiteral(Encoding enc)
    {
        // Buffered readers can struggle with this with invalidly truncated read from the resources with other encodings.
        var reader = CreateWithEncodedString($"\"{DataEmojiSpam}\"", enc);
        var root = reader.Read();
        Assert.AreEqual(SJType.String, root.type);
        Assert.IsTrue(root.Slice().SequenceEqual(DataEmojiSpam), "Sliced value must equal the string inside the literal.");
    }
    // Check tricky conditions on the comma/colon state
    [TestMethod]
    [DataRow(64)]
    [Timeout(TestTimeout.Short)]
    public void TestValidDeepObjects(int depth) => Read(CreateWithMaxRecursionString("{}", depth));
    [TestMethod]
    [DataRow(64)]
    [Timeout(TestTimeout.Short)]
    public void TestValidDeepArray(int depth) => Read(CreateWithMaxRecursionString("[]", depth));
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [DataRow(@"[""hello"" 1 2 3 4 5]")]
    [DataRow(@"{""hello"": 124 ""world!"": ""world: yo"" ""look"": ""no comma! waow bradar please JSON code make no mistakes""}")]
    [DataRow(@"{""hello"" 124, ""gurt"" ""yo"", ""yogurt"" ""yes""}")]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestCommaColonState(string data) => Read(CreateWithString(data));

    // Discarding
    protected static void TestDiscard(SJReader reader, string discardKey = JsonDataDiscardKey)
    {
        // Manual reading
        ArgumentNullException.ThrowIfNull(reader);
        reader.ThrowOnError = true;

        var root = reader.Read();
        while (reader.IterateObject(root, out SJReader.Value k, out SJReader.Value v))
        {
            if (k.Slice().Equals(discardKey, StringComparison.Ordinal))
            {
                Console.WriteLine($"Skipping very secret key '{k.Slice()}'");
                continue;
            }

            switch (v.type)
            {
                default:
                    Console.WriteLine($"{k.Slice()} : {v.Slice()}");
                    break;

                case SJType.Object:
                    Console.WriteLine($"{k.Slice()} :");
                    while (reader.IterateObject(v, out SJReader.Value k2, out SJReader.Value v2))
                    {
                        Console.WriteLine($"  {k2.Slice()} : {v2.Slice()}");
                    }
                    break;
                case SJType.Array:
                    Console.WriteLine($"{k.Slice()} :");
                    while (reader.IterateArray(v, out SJReader.Value av))
                    {
                        Console.WriteLine($"  {av.Slice()}");
                    }
                    break;
            }
        }
    }
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestDiscard() => TestDiscard(CreateWithString(JsonDataDiscard));
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestDiscardInvalid() => TestDiscard(CreateWithString(JsonDataDiscardInvalid));

    // JSC
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestJSC() => ReadJSC(CreateWithString(JsonDataComment));
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalid() => ReadJSC(CreateWithString(JsonDataCommentInvalid));

    // JSC with Capture
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    public void TestJSCWithCapture() => ReadJSC(CreateWithString(JsonDataComment), false);
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [DataRow(@"{ ""key"" /* inline */ : ""value"" }")]
    [DataRow(@"{ ""key"" : /* inline */ ""value"" }")]
    [DataRow(@"{ ""key"" : ""value"" /* trailing */ , ""key2"": ""value2"" }")]
    [DataRow(@"[ 1, 2, 3 /* trailing array */, 4, 5]")]
    [DataRow(@"[ 1, 2, 3 /* trailing array */]")]
    public void TestJSCWithTrickyCapture(string data) => ReadJSC(CreateWithString(data), false);
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestJSCInvalidWithCapture() => ReadJSC(CreateWithString(JsonDataInvalid), false);
    // Do also read literals with no ignore JSC to test "Ended".
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [DynamicData(nameof(JsonRootDataProcessors), DynamicDataDisplayName = nameof(GetRootDataProcessorTestName))]
    public void TestJSCRootLiteralsWithCapture(string data)
    {
        // Micro$oft pls
        string CommentLeft = "// This is a comment!" + Environment.NewLine;
        string CommentRight = "// One more comment!" + Environment.NewLine + "/* Is it the end of file? */"; // All whitespace (surrounding the document) is ignored.

        Assert.AreEqual(data, ReadJSC(CreateWithString(data), false).ToString());
        // To spice it up, surround root data with comments on second iter.
        var data2 = $"{CommentLeft}{data}{CommentRight}";
        Assert.AreEqual(data2, ReadJSC(CreateWithString(data2), false).ToString());
    }

    // Empty root
    [TestMethod]
    [DynamicData(nameof(JsonRootDataProcessors), DynamicDataDisplayName = nameof(GetRootDataProcessorTestName))]
    public void TestRootLiterals(string data)
    {
        // Since the empty builder for the sample code is simple but somewhat matching, it should equal.
        Assert.AreEqual(data, Read(CreateWithString(data)).ToString());
    }
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    // No longer throws.
    public void TestEmptyString() => Read(CreateWithString(DataEmpty));

    // Stack
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestStacking() => Read(CreateWithString(JsonDataStackingInvalid));
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxArrayRecursion() => Read((CreateWithMaxRecursionString("[]", MaxRecursionDepth)));
    [TestMethod]
    [Timeout(TestTimeout.Short)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestMaxObjectRecursion() => Read(CreateWithMaxRecursionString("{}", MaxRecursionDepth));

    // - File tests
    public virtual Encoding JsonFileEncoding => Encoding.UTF8;
    /// <summary>
    /// Tests reading a basic JSON file (100kb-ish).
    /// </summary>
    [TestMethod]
    [Timeout(TestTimeout.Mid)]
    [DataRow(JsonFileValidName)]
    public void TestFile(string name) => Read(CreateFromStream(LoadAsmFile(name), JsonFileEncoding));
    /// <summary>
    /// Tests reading large JSON file.
    /// </summary>
    [TestMethod]
    [Timeout(TestTimeout.Long)]
    [DataRow(JsonFileLargeName)]
    [DataRow(JsonFileLargeMinName)]
    public void TestVeryLarge(string name) => Read(CreateFromStream(LoadAsmFile(name), JsonFileEncoding));
    [TestMethod]
    [Timeout(TestTimeout.Mid)]
    [DataRow(JsonFileInvalidBinary)]
    [DataRow(JsonFileInvalidNoColon)]
    [DataRow(JsonFileInvalidUnterminated)]
    [ExpectedException(typeof(SJReader.ReadException))]
    public void TestInvalidFile(string name) => Read(CreateFromStream(LoadAsmFile(name), JsonFileEncoding));
}
